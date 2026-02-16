using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using Unity.VisualScripting;

//Dialogue Manager Refactor

public class DialogueManager : MonoBehaviour
{
    // [Header("Button Spawning")]
    public GameObject thoughtButtonPrefab;

    // this should be a RectTransform that defines where thoughts are allowed to spawn
    public RectTransform spawnAreaRect;

    [Header("No-Spawn Zones (UI)")]
    public List<RectTransform> noSpawnZones = new List<RectTransform>();

    [Header("Timer")]
    [SerializeField] private Slider timerSlider;
    [SerializeField] private float responseTime = 5f;

    [Tooltip("How much of the available window we actually use for spawning (0.9 = use 90% of the window).")]
    [Range(0.1f, 1f)]
    [SerializeField] private float windowFillPercent = 0.9f;

    [SerializeField] private float minSpawnWindowSeconds = 0.5f;
    [SerializeField] private float maxSpawnWindowSeconds = 6f;

    [SerializeField] private float delayBetweenThoughts = 0.08f;

    [Header("Spawn Placement (no overlap + center bias)")]
    [Tooltip("How close to center thoughts prefer to spawn. Higher = tighter cluster.")]
    [SerializeField] private float centerBias = 2.2f;

    [Header("Button Variables")]
    [SerializeField] private float overlapPadding = 10f;
    [SerializeField] private float minButtonScale = 0.55f;  // don’t shrink past this

    private List<ThoughtButtonUI> activeButtons = new List<ThoughtButtonUI>();

    private bool responded = false;
    private Coroutine timerRoutine;
    private Coroutine spawnRoutine;

    //private bool isPaused = false;
    private bool timerIsActive = false;
    private bool inResponseWindow = false; // only true AFTER audio ends
    private bool isEnding = false;

    private Coroutine responseWindowRoutine;
    private MomPortraitRoutes portraitManager;

    private void Start()
    {
        if (AudioClipManager.Instance != null)
        {
            AudioClipManager.Instance.dialogueHasStarted.AddListener(onDialogueHasStarted);
            AudioClipManager.Instance.dialogueHasEnded.AddListener(onDialogueEnded);
        }

        if (timerSlider != null)
        {
            timerSlider.maxValue = responseTime;
            timerSlider.value = responseTime;
            timerSlider.gameObject.SetActive(false);
        }

        portraitManager = Object.FindFirstObjectByType<MomPortraitRoutes>();
        if (portraitManager == null)
            Debug.Log("Could not find MomPortraitRoutes, can't set portrait for mom");
    }

    //DIALOGUE STARTED

    private void onDialogueHasStarted(AudioClipSO clip)
    {
        responded = false;
        inResponseWindow = false;


        if (responseWindowRoutine != null)
        {
            StopCoroutine(responseWindowRoutine);
            responseWindowRoutine = null;
        }

        // important: stop any leftover spawn routine from a previous clip
        StopSpawnRoutine("new dialogue started");

        ClearButtons();
        timerSlider.gameObject.SetActive(false);

        // start spawning for THIS run
        if (!isEnding)
        {
            spawnRoutine = StartCoroutine(SpawnDuringAudio(clip));
        }
    }

    //Spawning Coroutines 
    private IEnumerator SpawnDuringAudio(AudioClipSO clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("DialogueQTEManager got null AudioClipSO.");
            yield break;
        }

        List<ClipResponse> responses = clip.GetResponses();
        if (responses == null || responses.Count == 0)
        {
            yield break;
        }

        List<ClipResponse> ordered = new List<ClipResponse>(responses);
        ordered.Sort((a, b) => a.spawnTime.CompareTo(b.spawnTime));

        float clipLength = clip.GetClipLength();
        if (clipLength <= 0.01f)
        {
            clipLength = AudioClipManager.Instance.GetCurrentClipLength();
        }

        SetButtonsInteractable(false);

        for (int idx = 0; idx < ordered.Count; idx++)
        {

            // abort if we already entered response window / dialogue ended
            if (inResponseWindow) yield break;

            ClipResponse cr = ordered[idx];

            float startTime = Mathf.Max(0f, cr.spawnTime);

            float nextTime = clipLength;
            if (idx + 1 < ordered.Count)
                nextTime = Mathf.Max(startTime, ordered[idx + 1].spawnTime);

            float rawWindow = nextTime - startTime;
            float spawnWindow = Mathf.Clamp(rawWindow * windowFillPercent, minSpawnWindowSeconds, maxSpawnWindowSeconds);

            Debug.Log($"[QTE] Spawn group idx={idx} start={startTime:0.00}s window={spawnWindow:0.00}s numToSpawn={cr.numToSpawn}");

            bool reached = false;
            yield return StartCoroutine(waitUntilDialogueTime(startTime, reachedSetter: v => reached = v));

            // if audio ended before reaching target time, stop spawning entirely
            if (!reached) yield break;

            yield return StartCoroutine(spawnClipResponseButtonsSlow(cr, spawnWindow));
        }
    }

    private IEnumerator waitUntilDialogueTime(float targetSeconds, System.Action<bool> reachedSetter)
    {
        const float maxStallSeconds = 0.75f;
        float stall = 0f;
        float lastT = -999f;

        while (AudioClipManager.Instance != null &&
            AudioClipManager.Instance.IsDialogueActive() &&
            AudioClipManager.Instance.GetPlaybackTime() < targetSeconds)
        {
            if (inResponseWindow) yield break;

            float t = AudioClipManager.Instance.GetPlaybackTime();
            if (Mathf.Abs(t - lastT) < 0.0001f)
            {
                stall += Time.deltaTime;
                if (stall >= maxStallSeconds)
                {
                    Debug.Log($"[QTE] WaitUntilDialogueTime stall escape (t={t:0.00}, target={targetSeconds:0.00}).");
                    break;
                }
            }
            else
            {
                stall = 0f;
                lastT = t;
            }

            yield return null;
        }

        // if dialogue ended before we hit the target time, do NOT spawn this group
        if (AudioClipManager.Instance == null || !AudioClipManager.Instance.IsDialogueActive())
        {
            reachedSetter?.Invoke(false);
            yield break;
        }

        reachedSetter?.Invoke(true);
    }

    private IEnumerator spawnClipResponseButtonsSlow(ClipResponse clipResponse, float spawnWindowSeconds)
    {
        if (clipResponse == null) yield break;

        int count = Mathf.Max(1, clipResponse.numToSpawn);

        float interval = (count <= 1) ? spawnWindowSeconds : (spawnWindowSeconds / (count - 1));
        interval = Mathf.Max(0.01f, interval);

        for (int i = 0; i < count; i++)
        {
            
            if (inResponseWindow) yield break;

            yield return StartCoroutine(spawnOneThoughtButtonCoroutine(clipResponse));

            float wait = interval;

            if (AudioClipManager.Instance == null) yield break;

            float start = AudioClipManager.Instance.GetPlaybackTime();

            yield return new WaitForSeconds(wait);
        }
    }

    private IEnumerator spawnOneThoughtButtonCoroutine(ClipResponse clipResponse)
    {

        GameObject buttonObj = Instantiate(thoughtButtonPrefab, spawnAreaRect);
        

        buttonObj.transform.localScale *= clipResponse.responseSize;

        CanvasGroup cg = buttonObj.GetComponent<CanvasGroup>();
        if (cg == null) cg = buttonObj.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        RectTransform rect = buttonObj.GetComponent<RectTransform>();
        ThoughtButtonUI button = buttonObj.GetComponent<ThoughtButtonUI>();
        activeButtons.Add(button);

        button.Setup(clipResponse, onResponseSelected);

        yield return null;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        LayoutElement le = buttonObj.GetComponent<LayoutElement>();
        if (le == null) le = buttonObj.AddComponent<LayoutElement>();
        le.preferredWidth = rect.rect.width;
        le.preferredHeight = rect.rect.height;

        ContentSizeFitter csf = buttonObj.GetComponent<ContentSizeFitter>();
        if (csf != null) csf.enabled = false;

        HorizontalLayoutGroup hlg = buttonObj.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) hlg.enabled = false;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        Vector2 pos;
        bool found = getSpawnPosition(rect, out pos);
        rect.anchoredPosition = pos;

        yield return StartCoroutine(AnimateThoughtIn(cg, buttonObj.transform, buttonObj.transform.localScale));

        bool canClickNow = (inResponseWindow);
        button.SetInteractable(canClickNow);
    }

    private IEnumerator AnimateThoughtIn(CanvasGroup cg, Transform t, Vector3 finalScale, float duration = 0.12f)
    {
        if (cg != null) cg.alpha = 0f;

        Vector3 startScale = finalScale * 0.92f;
        t.localScale = startScale;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / duration);

            float ease = 1f - Mathf.Pow(1f - p, 3f);

            if (cg != null) cg.alpha = ease;
            t.localScale = Vector3.LerpUnclamped(startScale, finalScale, ease);

            yield return null;
        }

        if (cg != null) cg.alpha = 1f;
        t.localScale = finalScale;
    }

    //DIALOGUE ENDED

    private void onDialogueEnded()
    {
        // CRITICAL FIX: stop spawning as soon as audio ends
        StopAllCoroutines();

        if (responseWindowRoutine != null) StopCoroutine(responseWindowRoutine);

        //responseWindowRoutine = StartCoroutine(WaitForDialogueToReallyEndThenEnter());

        if (!isEnding)
        {
            inResponseWindow = true;

            SetButtonsInteractable(true);
            timerIsActive = true;

            timerSlider.gameObject.SetActive(true);

            if (timerRoutine != null) StopCoroutine(timerRoutine);
            timerRoutine = StartCoroutine(startResponseTimer());
        }
       
    }

    private IEnumerator startResponseTimer()
    {

        float timeRemaining = responseTime;
        timerSlider.value = responseTime;

        while (timeRemaining > 0f && !responded)
        {

            timeRemaining -= Time.deltaTime;
            timerSlider.value = timeRemaining;
            yield return null;
        }

        timerSlider.value = 0f;
        timerSlider.gameObject.SetActive(false);
        timerIsActive = false;

        if (!responded)
        {

            ClearButtons();
            AudioClipManager.Instance.PlayDialogue();
            if (portraitManager != null) portraitManager.SetRouteSprite(ChoiceType.Timid);
        }
    }

    //BUTTON CALLBACK
    private void onResponseSelected(ClipResponse chosen)
    {
        responded = true;
        timerIsActive = false;
        inResponseWindow = false;

        // CRITICAL FIX: stop spawning immediately on selection too
        StopSpawnRoutine("response selected");

        if (timerRoutine != null) StopCoroutine(timerRoutine);
        timerSlider.gameObject.SetActive(false);

        ClearButtons();

        AudioClipManager.Instance.ChooseResponse(chosen);
        if (portraitManager != null) portraitManager.SetRouteSprite(chosen.choiceType);
    }


    //HELPER FUNCTIONS
    private void StopSpawnRoutine(string reason)
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private void ClearButtons()
    {
        foreach (ThoughtButtonUI button in activeButtons)
        {
            if (button != null) Destroy(button.gameObject);
        }
        activeButtons.Clear();
    }

    private void SetButtonsInteractable(bool canClick)
    {
        foreach (ThoughtButtonUI button in activeButtons)
        {
            if (button != null)
            {
                button.SetInteractable(canClick);
            }
        }
    }

    //BUTTON SPAWN HELPERS
    private bool getSpawnPosition(RectTransform buttonRect, out Vector2 foundPos)
    {
        float halfW = buttonRect.rect.width * 0.5f;
        float halfH = buttonRect.rect.height * 0.5f;

        float minX = spawnAreaRect.rect.xMin;
        float maxX = spawnAreaRect.rect.xMax;
        float minY = spawnAreaRect.rect.yMin;
        float maxY = spawnAreaRect.rect.yMax;

        Vector2 center = spawnAreaRect.rect.center;

        float radiusX = (maxX - minX) * 0.5f;
        float radiusY = (maxY - minY) * 0.5f;

        const float goldenAngle = 2.39996323f;
        float jitterPx = 4f;

        float fitMinX = spawnAreaRect.rect.xMin + halfW;
        float fitMaxX = spawnAreaRect.rect.xMax - halfW;
        float fitMinY = spawnAreaRect.rect.yMin + halfH;
        float fitMaxY = spawnAreaRect.rect.yMax - halfH;

        bool canFitInside = (fitMinX <= fitMaxX && fitMinY <= fitMaxY);

        if (canFitInside)
        {
            float fitRadiusX = (fitMaxX - fitMinX) * 0.5f;
            float fitRadiusY = (fitMaxY - fitMinY) * 0.5f;

            for (int attempt = 0; attempt < 120; attempt++)
            {
                float t = (attempt + 0.5f) / Mathf.Max(1f, 120);
                float r = Mathf.Sqrt(t);
                float angle = attempt * goldenAngle;

                float x = center.x + Mathf.Cos(angle) * r * fitRadiusX + Random.Range(-jitterPx, jitterPx);
                float y = center.y + Mathf.Sin(angle) * r * fitRadiusY + Random.Range(-jitterPx, jitterPx);

                x = Mathf.Clamp(x, fitMinX, fitMaxX);
                y = Mathf.Clamp(y, fitMinY, fitMaxY);

                Vector2 candidate = new Vector2(x, y);
                buttonRect.anchoredPosition = candidate;

                if (OverlapsNoSpawnZones(buttonRect)) continue;
                if (OverlapsOtherButtons(buttonRect)) continue;

                foundPos = candidate;
                return true;
            }
        }

        Vector2 bestCandidate = center;
        float bestScore = -1f;

        int extraSamples = Mathf.Max(300, 120 * 3);

        float searchRadiusX = radiusX;
        float searchRadiusY = radiusY;

        for (int attempt = 0; attempt < extraSamples; attempt++)
        {
            float t = (attempt + 0.5f) / Mathf.Max(1f, extraSamples);
            float r = Mathf.Sqrt(t);
            float angle = (attempt + 120) * goldenAngle;

            float x = center.x + Mathf.Cos(angle) * r * searchRadiusX + Random.Range(-jitterPx, jitterPx);
            float y = center.y + Mathf.Sin(angle) * r * searchRadiusY + Random.Range(-jitterPx, jitterPx);

            x = Mathf.Clamp(x, fitMinX, fitMaxX);
            y = Mathf.Clamp(y, fitMinY, fitMaxY);

            Vector2 candidate = new Vector2(x, y);

            if (CandidateHitsNoSpawnZones(buttonRect, candidate)) continue;

            float score = MinDistanceToOtherButtons(candidate);
            // prefer candidates closer to center; higher centerBias -> tighter cluster
            float distToCenter = Vector2.Distance(candidate, center);
            float maxSearchRadius = Mathf.Max(searchRadiusX, searchRadiusY);
            float centerScore = (maxSearchRadius - distToCenter) * centerBias * 0.05f;
            score += centerScore;

            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = candidate;
            }
        }

        foundPos = bestCandidate;
        return false;
    }

    private bool CandidateHitsNoSpawnZones(RectTransform buttonRect, Vector2 candidate)
    {
        Vector2 prev = buttonRect.anchoredPosition;
        buttonRect.anchoredPosition = candidate;

        bool bad = OverlapsNoSpawnZones(buttonRect);

        buttonRect.anchoredPosition = prev;
        return bad;
    }

    private bool OverlapsNoSpawnZones(RectTransform buttonRect)
    {
        foreach (RectTransform zone in noSpawnZones)
        {
            if (zone == null) continue;
            if (RectsOverlap(buttonRect, zone)) return true;
        }
        return false;
    }

    private bool OverlapsOtherButtons(RectTransform buttonRect)
    {
        foreach (ThoughtButtonUI other in activeButtons)
        {
            if (other == null) continue;
            RectTransform otherRect = other.GetComponent<RectTransform>();
            if (otherRect == null) continue;
            if (otherRect == buttonRect) continue;
            if (RectsOverlap(buttonRect, otherRect)) return true;
        }
        return false;
    }

    private bool RectsOverlap(RectTransform a, RectTransform b)
    {
        Rect ra = GetRectInSpawnAreaSpace(a, overlapPadding);
        Rect rb = GetRectInSpawnAreaSpace(b, overlapPadding);
        return ra.Overlaps(rb);
    }

    private Rect GetRectInSpawnAreaSpace(RectTransform rt, float padding)
    {
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(spawnAreaRect, rt);

        Vector2 size = bounds.size;
        Vector2 center = bounds.center;

        size += new Vector2(padding * 2f, padding * 2f);

        return new Rect(center - size * 0.5f, size);
    }

    private float MinDistanceToOtherButtons(Vector2 candidate)
    {
        float best = float.PositiveInfinity;

        for (int i = 0; i < activeButtons.Count; i++)
        {
            ThoughtButtonUI other = activeButtons[i];
            if (other == null) continue;

            RectTransform otherRect = other.GetComponent<RectTransform>();
            if (otherRect == null) continue;

            Vector2 otherPos = otherRect.anchoredPosition;
            float d = Vector2.Distance(candidate, otherPos);
            if (d < best) best = d;
        }

        if (float.IsPositiveInfinity(best)) best = 999999f;
        return best;
    }

    //ENDING THOUGHTS 
    public void playEndingThoughts(ChoiceType endingType, string focusedText = "I can do this.")
    {
        // prevent any normal response window behavior for endings

        StopAllCoroutines();

        responded = false;
        timerIsActive = false;
        inResponseWindow = false;

        if (timerSlider != null)
            timerSlider.gameObject.SetActive(false);

        ClearButtons();
        SetButtonsInteractable(false);

        isEnding = true;

        if (endingType == ChoiceType.Focused)
        {
            StartCoroutine(SpawnFocusedEndingThought(focusedText));
        }
        else
        {
            StartCoroutine(SpawnOverwhelmingEndingThoughts(endingType));
        }
    }

    private IEnumerator SpawnFocusedEndingThought(string text)
    {
        yield return new WaitForSeconds(25);
        yield return StartCoroutine(SpawnEndingThoughtButton(text, 1.0f));
    }

    private IEnumerator SpawnOverwhelmingEndingThoughts(ChoiceType endingType)
    {
        yield return new WaitForSeconds(5);

        int count = 30;
        float delay = 1f;
        float scale = 0.9f;

        string[] pool1 = new string[]
        {
            "too much", "stop", "i can't", "what if i'm wrong",
            "i'm sorry", "she hates me", "i'm stuck",
            "everything is loud", "i can't breathe", "make it stop"
        };

        string[] pool2 = new string[]
        {
            "I didn't tell her.", "stop", "I can't", "I can't tell her",
            "i'm sorry", "She won’t understand", "i'm stuck",
            "everything is loud", "i can't breathe", "make it stop"
        };

        for (int i = 0; i < count; i++)
        {
            string t = (endingType == ChoiceType.Combative)
                ? pool1[Random.Range(0, pool1.Length)]
                : pool2[Random.Range(0, pool2.Length)];

            yield return StartCoroutine(SpawnEndingThoughtButton(t, scale));
            yield return new WaitForSeconds(delay);
        }
    }

    private IEnumerator SpawnEndingThoughtButton(string buttonText, float scale)
    {
        GameObject buttonObj = Instantiate(thoughtButtonPrefab, spawnAreaRect);

        buttonObj.transform.localScale *= scale;

        CanvasGroup cg = buttonObj.GetComponent<CanvasGroup>();
        if (cg == null) cg = buttonObj.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        RectTransform rect = buttonObj.GetComponent<RectTransform>();

        TMP_Text tmp = buttonObj.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = buttonText;

        yield return null;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        LayoutElement le = buttonObj.GetComponent<LayoutElement>();
        if (le == null) le = buttonObj.AddComponent<LayoutElement>();
        le.preferredWidth = rect.rect.width;
        le.preferredHeight = rect.rect.height;

        ContentSizeFitter csf = buttonObj.GetComponent<ContentSizeFitter>();
        if (csf != null) csf.enabled = false;

        HorizontalLayoutGroup hlg = buttonObj.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) hlg.enabled = false;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        Vector2 pos;
        getSpawnPosition(rect, out pos);
        rect.anchoredPosition = pos;

        StartCoroutine(AnimateThoughtIn(cg, buttonObj.transform, buttonObj.transform.localScale));

        ThoughtButtonUI tb = buttonObj.GetComponent<ThoughtButtonUI>();
        if (tb != null) tb.SetInteractable(false);

        Button unityBtn = buttonObj.GetComponent<Button>();
        if (unityBtn != null) unityBtn.interactable = false;

        if (tb != null)
            activeButtons.Add(tb);
    }
}
