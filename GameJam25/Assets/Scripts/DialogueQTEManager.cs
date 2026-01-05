using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

// NOTE: Remove this in Unity unless you truly need it.
// It can cause weird platform/build issues (and you’re not using it here).
// using System.Runtime.InteropServices.WindowsRuntime;

public class DialogueQTEManager : MonoBehaviour
{
    [Header("Button Spawning")]
    public GameObject thoughtButtonPrefab;

    // this should be a RectTransform that defines where thoughts are allowed to spawn
    public RectTransform spawnAreaRect;

    [Header("No-Spawn Zones (UI)")]
    public List<RectTransform> noSpawnZones = new List<RectTransform>();

    [Header("Timer")]
    [SerializeField] private Slider timerSlider;
    [SerializeField] private float responseTime = 5f;

    [Header("Spawn Pacing (make thoughts come in bit-by-bit)")]
    [Tooltip("If true, spawning uses the AUDIO playback time (pauses naturally if audio pauses).")]
    [SerializeField] private bool useAudioTime = true;

    [Tooltip("How much of the available window we actually use for spawning (0.9 = use 90% of the window).")]
    [Range(0.1f, 1f)]
    [SerializeField] private float windowFillPercent = 0.9f;

    [Tooltip("If the window between interactions is tiny, we still want a minimum time to drip thoughts in.")]
    [SerializeField] private float minSpawnWindowSeconds = 0.5f;

    [Tooltip("If the window is huge, cap it so we don't spawn painfully slow.")]
    [SerializeField] private float maxSpawnWindowSeconds = 6f;

    [Tooltip("Extra small delay between spawns so it feels like \"thoughts\" and not a machine gun.")]
    [SerializeField] private float extraDelayBetweenThoughts = 0.08f;

    [Header("Spawn Placement (no overlap + center bias)")]
    [Tooltip("How close to center thoughts prefer to spawn. Higher = tighter cluster.")]
    [SerializeField] private float centerBias = 2.2f;

    [Tooltip("Extra padding so buttons don't feel like they're touching.")]
    [SerializeField] private float overlapPadding = 10f;

    [Tooltip("How many tries before we give up and place it anyway.")]
    [SerializeField] private int maxSpawnTries = 120;

    [Header("Spawn Fallback (keep all spawns)")]
    [SerializeField] private bool shrinkToFitIfNeeded = true;

    [SerializeField] private float minButtonScale = 0.55f;  // don’t shrink past this
    [SerializeField] private float shrinkStep = 0.92f;      // each retry multiplies scale by this

    [Header("Size Variety (some thoughts bigger/smaller)")]
    [Tooltip("If enabled, each spawned thought gets a random size multiplier.")]
    [SerializeField] private bool varyThoughtSizes = true;

    [Tooltip("Random size multiplier applied per thought. e.g. (0.85, 1.25)")]
    [SerializeField] private Vector2 thoughtSizeMultiplierRange = new Vector2(0.85f, 1.25f);

    [Tooltip("If crowded, sometimes we shrink to fit; otherwise we allow overflow outside the spawn area.")]
    [Range(0f, 1f)]
    [SerializeField] private float chanceToShrinkInsteadOfOverflow = 0.45f;

    [Header("Overflow Fallback (allow thoughts outside the safe area)")]
    [Tooltip("If true, when the spiral cannot find space, place outside spawnAreaRect instead of center/inside fallback.")]
    [SerializeField] private bool allowOverflowOutsideSpawnArea = true;

    [Tooltip("How far outside the spawn area (radius multiplier) overflow placements go.")]
    [SerializeField] private float overflowRadiusMultiplier = 1.35f;

    // store active buttons so we can enable/disable them and clear them
    private readonly List<ThoughtButtonUI> activeButtons = new List<ThoughtButtonUI>();

    private bool responded = false;
    private Coroutine timerRoutine;
    private Coroutine spawnRoutine;

    private bool isPaused = false;
    private bool timerIsActive = false;
    private bool inResponseWindow = false; // only true AFTER audio ends

    // BUILD-SAFE: if OnDialogueEnded fires while audio still reports "active" (common in builds),
    // we keep trying until it becomes safe to enable buttons.
    private bool responseWindowPending = false;
    private Coroutine responseWindowRoutine;

    private MomPortraitRoutes portraitManager;

    private void Start()
    {
        AudioClipManager.Instance.dialogueHasStarted.AddListener(OnDialogueStarted);
        AudioClipManager.Instance.dialogueHasEnded.AddListener(OnDialogueEnded);

        timerSlider.maxValue = responseTime;
        timerSlider.value = responseTime;
        timerSlider.gameObject.SetActive(false);

        portraitManager = Object.FindFirstObjectByType<MomPortraitRoutes>();
        if (portraitManager == null)
            Debug.Log("Could not find MomPortraitRoutes, can't set portrait for mom");
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;

        // If dialogue already ended while paused, we "owe" the response window.
        // On unpause, attempt to enter immediately.
        if (!paused && responseWindowPending)
        {
            TryEnterResponseWindowNow();
        }

        SetButtonsInteractable(!paused && inResponseWindow);

        if (timerSlider != null)
        {
            if (paused)
            {
                timerSlider.gameObject.SetActive(false);
            }
            else
            {
                if (timerIsActive)
                {
                    timerSlider.gameObject.SetActive(true);
                }
            }
        }
    }

    private void OnDialogueStarted(AudioClipSO clip)
    {
        responded = false;
        inResponseWindow = false;
        responseWindowPending = false;

        if (responseWindowRoutine != null)
        {
            StopCoroutine(responseWindowRoutine);
            responseWindowRoutine = null;
        }

        ClearButtons();
        timerSlider.gameObject.SetActive(false);

        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnDuringAudio(clip));
    }

    private void OnDialogueEnded()
    {
        // We ALWAYS mark pending, because in builds "ended event" can fire
        // while the audio system still reports active for a few frames.
        responseWindowPending = true;

        // If paused, we can’t enter now. We'll enter on unpause.
        if (isPaused) return;

        // Start a retry routine that waits until dialogue is truly inactive.
        if (responseWindowRoutine != null) StopCoroutine(responseWindowRoutine);
        responseWindowRoutine = StartCoroutine(WaitForDialogueToReallyEndThenEnter());
    }

    private IEnumerator WaitForDialogueToReallyEndThenEnter()
    {
        // Small grace frame: helps with build-only event order differences
        yield return null;

        while (AudioClipManager.Instance != null && AudioClipManager.Instance.IsDialogueActive())
        {
            if (isPaused) yield break;
            yield return null;
        }

        TryEnterResponseWindowNow();
    }

    private void TryEnterResponseWindowNow()
    {
        if (!responseWindowPending) return;
        if (isPaused) return;

        if (AudioClipManager.Instance != null && AudioClipManager.Instance.IsDialogueActive())
            return;

        responseWindowPending = false;

        inResponseWindow = true;

        SetButtonsInteractable(true);
        timerIsActive = true;

        timerSlider.gameObject.SetActive(true);

        if (timerRoutine != null) StopCoroutine(timerRoutine);
        timerRoutine = StartCoroutine(ResponseTimer());
    }

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
            while (isPaused) yield return null;

            ClipResponse cr = ordered[idx];

            float startTime = Mathf.Max(0f, cr.spawnTime);

            float nextTime = clipLength;
            if (idx + 1 < ordered.Count)
                nextTime = Mathf.Max(startTime, ordered[idx + 1].spawnTime);

            float rawWindow = nextTime - startTime;

            float spawnWindow = Mathf.Clamp(rawWindow * windowFillPercent, minSpawnWindowSeconds, maxSpawnWindowSeconds);

            yield return WaitUntilDialogueTime(startTime);

            yield return SpawnClipResponseButtonsSlow(cr, spawnWindow);
        }
    }

    private IEnumerator WaitUntilDialogueTime(float targetSeconds)
    {
        if (!useAudioTime)
        {
            float waited = 0f;
            while (waited < targetSeconds)
            {
                if (!isPaused)
                {
                    waited += Time.deltaTime;
                }
                yield return null;
            }
            yield break;
        }

        while (AudioClipManager.Instance.IsDialogueActive() && AudioClipManager.Instance.GetPlaybackTime() < targetSeconds)
        {
            if (isPaused)
            {
                yield return null;
                continue;
            }

            yield return null;
        }
    }

    private IEnumerator SpawnClipResponseButtonsSlow(ClipResponse clipResponse, float spawnWindowSeconds)
    {
        if (clipResponse == null) yield break;

        int count = Mathf.Max(1, clipResponse.numToSpawn);

        float interval = (count <= 1) ? spawnWindowSeconds : (spawnWindowSeconds / (count - 1));
        interval = Mathf.Max(0.01f, interval);

        for (int i = 0; i < count; i++)
        {
            while (isPaused) yield return null;

            yield return StartCoroutine(SpawnOneThoughtButtonCoroutine(clipResponse));

            float wait = interval + extraDelayBetweenThoughts;

            if (useAudioTime)
            {
                float start = AudioClipManager.Instance.GetPlaybackTime();
                while (AudioClipManager.Instance.IsDialogueActive() && (AudioClipManager.Instance.GetPlaybackTime() - start) < wait)
                {
                    if (isPaused)
                    {
                        start = AudioClipManager.Instance.GetPlaybackTime();
                        yield return null;
                        continue;
                    }

                    yield return null;
                }
            }
            else
            {
                float waited = 0f;
                while (waited < wait)
                {
                    if (!isPaused)
                    {
                        waited += Time.deltaTime;
                    }
                    yield return null;
                }
            }
        }
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

    private bool CandidateHitsNoSpawnZones(RectTransform buttonRect, Vector2 candidate)
    {
        Vector2 prev = buttonRect.anchoredPosition;
        buttonRect.anchoredPosition = candidate;

        bool bad = OverlapsNoSpawnZones(buttonRect);

        buttonRect.anchoredPosition = prev;
        return bad;
    }

    private IEnumerator AnimateThoughtIn(CanvasGroup cg, Transform t, Vector3 finalScale, float duration = 0.12f)
    {
        if (cg != null) cg.alpha = 0f;

        Vector3 startScale = finalScale * 0.92f;
        t.localScale = startScale;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(elapsed / duration);

            float ease = 1f - Mathf.Pow(1f - p, 3f);

            if (cg != null) cg.alpha = ease;
            t.localScale = Vector3.LerpUnclamped(startScale, finalScale, ease);

            yield return null;
        }

        if (cg != null) cg.alpha = 1f;
        t.localScale = finalScale;
    }

    private IEnumerator SpawnOneThoughtButtonCoroutine(ClipResponse clipResponse)
    {
        GameObject buttonObj = Instantiate(thoughtButtonPrefab, spawnAreaRect);

        buttonObj.transform.localScale *= clipResponse.responseSize;

        if (varyThoughtSizes)
        {
            float mult = Random.Range(thoughtSizeMultiplierRange.x, thoughtSizeMultiplierRange.y);
            buttonObj.transform.localScale *= mult;
        }

        CanvasGroup cg = buttonObj.GetComponent<CanvasGroup>();
        if (cg == null) cg = buttonObj.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        RectTransform rect = buttonObj.GetComponent<RectTransform>();
        ThoughtButtonUI button = buttonObj.GetComponent<ThoughtButtonUI>();

        button.Setup(clipResponse, OnResponseSelected);

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
        TryGetSpiralSpawnPosition(rect, out pos);
        rect.anchoredPosition = pos;

        StartCoroutine(AnimateThoughtIn(cg, buttonObj.transform, buttonObj.transform.localScale));

        // IMPORTANT: stay disabled during audio
        button.SetInteractable(false);
        activeButtons.Add(button);
    }

    private bool TryGetSpiralSpawnPosition(RectTransform buttonRect, out Vector2 foundPos)
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

            for (int attempt = 0; attempt < maxSpawnTries; attempt++)
            {
                float t = (attempt + 0.5f) / Mathf.Max(1f, maxSpawnTries);
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

        int extraSamples = Mathf.Max(300, maxSpawnTries * 3);

        float searchRadiusX = radiusX * (allowOverflowOutsideSpawnArea ? overflowRadiusMultiplier : 1f);
        float searchRadiusY = radiusY * (allowOverflowOutsideSpawnArea ? overflowRadiusMultiplier : 1f);

        for (int attempt = 0; attempt < extraSamples; attempt++)
        {
            float t = (attempt + 0.5f) / Mathf.Max(1f, extraSamples);
            float r = Mathf.Sqrt(t);
            float angle = (attempt + maxSpawnTries) * goldenAngle;

            float x = center.x + Mathf.Cos(angle) * r * searchRadiusX + Random.Range(-jitterPx, jitterPx);
            float y = center.y + Mathf.Sin(angle) * r * searchRadiusY + Random.Range(-jitterPx, jitterPx);

            if (!allowOverflowOutsideSpawnArea)
            {
                x = Mathf.Clamp(x, fitMinX, fitMaxX);
                y = Mathf.Clamp(y, fitMinY, fitMaxY);
            }

            Vector2 candidate = new Vector2(x, y);

            if (CandidateHitsNoSpawnZones(buttonRect, candidate)) continue;

            float score = MinDistanceToOtherButtons(candidate);
            score += Vector2.Distance(candidate, center) * 0.05f;

            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = candidate;
            }
        }

        foundPos = bestCandidate;
        return false;
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

    private IEnumerator ResponseTimer()
    {
        float timeRemaining = responseTime;
        timerSlider.value = responseTime;

        while (timeRemaining > 0f && !responded)
        {
            if (isPaused)
            {
                yield return null;
                continue;
            }

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

    private void OnResponseSelected(ClipResponse chosen)
    {
        responded = true;
        timerIsActive = false;
        inResponseWindow = false;
        responseWindowPending = false;

        if (timerRoutine != null) StopCoroutine(timerRoutine);
        timerSlider.gameObject.SetActive(false);

        ClearButtons();

        AudioClipManager.Instance.ChooseResponse(chosen);
        if (portraitManager != null) portraitManager.SetRouteSprite(chosen.choiceType);
    }

    private void SetButtonsInteractable(bool canClick)
    {
        foreach (ThoughtButtonUI button in activeButtons)
        {
            if (button != null) button.SetInteractable(canClick);
        }
    }

    // ENDING THOUGHTS SYSTEM

    public void PlayEndingThoughts(ChoiceType endingType, string focusedText = "I can do this.")
    {
        StopAllCoroutines();

        responded = false;
        timerIsActive = false;
        inResponseWindow = false;
        responseWindowPending = false;

        if (timerSlider != null)
            timerSlider.gameObject.SetActive(false);

        ClearButtons();
        SetButtonsInteractable(false);

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
        // Using realtime so it isn’t affected by Time.timeScale (build/pause differences)
        yield return new WaitForSecondsRealtime(25);
        yield return StartCoroutine(SpawnEndingThoughtButton(text, 1.0f));
    }

    private IEnumerator SpawnOverwhelmingEndingThoughts(ChoiceType endingType)
    {
        yield return new WaitForSecondsRealtime(5);

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
            yield return new WaitForSecondsRealtime(delay);
        }
    }

    private IEnumerator SpawnEndingThoughtButton(string buttonText, float scale)
    {
        GameObject buttonObj = Instantiate(thoughtButtonPrefab, spawnAreaRect);

        buttonObj.transform.localScale *= scale;
        if (varyThoughtSizes)
        {
            float mult = Random.Range(thoughtSizeMultiplierRange.x, thoughtSizeMultiplierRange.y);
            buttonObj.transform.localScale *= mult;
        }

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
        TryGetSpiralSpawnPosition(rect, out pos);
        rect.anchoredPosition = pos;

        StartCoroutine(AnimateThoughtIn(cg, buttonObj.transform, buttonObj.transform.localScale));

        ThoughtButtonUI tb = buttonObj.GetComponent<ThoughtButtonUI>();
        if (tb != null) tb.SetInteractable(false);

        Button unityBtn = buttonObj.GetComponent<Button>();
        if (unityBtn != null) unityBtn.interactable = false;

        if (tb != null)
            activeButtons.Add(tb);
    }

    private void ClearButtons()
    {
        foreach (ThoughtButtonUI button in activeButtons)
        {
            if (button != null) Destroy(button.gameObject);
        }
        activeButtons.Clear();
    }
}
