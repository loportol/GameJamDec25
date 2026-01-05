using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

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

    // store active buttons so we can enable/disable them and clear them
    private readonly List<ThoughtButtonUI> activeButtons = new List<ThoughtButtonUI>();

    private bool responded = false;
    private Coroutine timerRoutine;
    private Coroutine spawnRoutine;

    private bool isPaused = false;
    private bool timerIsActive = false;
    private bool inResponseWindow = false; // only true AFTER audio ends

    private MomPortraitRoutes portraitManager;

    private void Start()
    {
        // dialogue starts, spawn thoughts (NOT clickable yet)
        AudioClipManager.Instance.dialogueHasStarted.AddListener(OnDialogueStarted);

        //  dialogue ends, enable clicking + start timer
        AudioClipManager.Instance.dialogueHasEnded.AddListener(OnDialogueEnded);

        // timer UI setup
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

        // Only allow clicking if we're NOT paused AND we're in the response window
        SetButtonsInteractable(!paused && inResponseWindow);

        // hide timer UI while paused so it doesn't look like it's draining
        if (timerSlider != null)
        {
            if (paused)
            {
                timerSlider.gameObject.SetActive(false);
            }
            else
            {
                // only show timer again if we are currently in the response window
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
        inResponseWindow = false; // audio is playing, no clicking
        ClearButtons();
        timerSlider.gameObject.SetActive(false);

        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnDuringAudio(clip));
    }

    private void OnDialogueEnded()
    {
        // if paused, don't start timer/click phase yet
        if (isPaused) return;

        inResponseWindow = true; // now clicking is allowed

        // safety: only enter response window if audio is REALLY done
        if (AudioClipManager.Instance != null && AudioClipManager.Instance.IsDialogueActive())
            return;

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
            // clips may have noResponse=true -> in that case no buttons are expected
            yield break;
        }

        // sort by spawnTime so we can treat them like interaction timestamps
        List<ClipResponse> ordered = new List<ClipResponse>(responses);
        ordered.Sort((a, b) => a.spawnTime.CompareTo(b.spawnTime));

        float clipLength = clip.GetClipLength();
        if (clipLength <= 0.01f)
        {
            clipLength = AudioClipManager.Instance.GetCurrentClipLength();
        }

        SetButtonsInteractable(false);

        // for each interaction timestamp: wait until that time -> then drip-spawn its buttons
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

            // drip spawn across spawnWindow (so it feels like thoughts creeping in)
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

            // IMPORTANT: spawn as coroutine so we can wait a frame and lock size before placing
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

    private IEnumerator SpawnOneThoughtButtonCoroutine(ClipResponse clipResponse)
    {
        GameObject buttonObj = Instantiate(thoughtButtonPrefab, spawnAreaRect);
        buttonObj.transform.localScale *= clipResponse.responseSize;

        RectTransform rect = buttonObj.GetComponent<RectTransform>();
        ThoughtButtonUI button = buttonObj.GetComponent<ThoughtButtonUI>();

        // set content FIRST (this usually changes TMP size)
        button.Setup(clipResponse, OnResponseSelected);

        // Let TMP/layout compute sizes (THIS is the missing piece that causes "overlap later")
        yield return null;

        // force rebuild now that TMP has had a frame
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        // lock size so it can't expand after placement
        LayoutElement le = buttonObj.GetComponent<LayoutElement>();
        if (le == null) le = buttonObj.AddComponent<LayoutElement>();
        le.preferredWidth = rect.rect.width;
        le.preferredHeight = rect.rect.height;

        ContentSizeFitter csf = buttonObj.GetComponent<ContentSizeFitter>();
        if (csf != null) csf.enabled = false;

        HorizontalLayoutGroup hlg = buttonObj.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) hlg.enabled = false;

        // rebuild again after locking
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        // now place it using its FINAL size
        rect.anchoredPosition = GetSpiralSpawnPosition(rect);

        button.SetInteractable(false);
        activeButtons.Add(button);
    }

    // Expands outward from the center (spiral) until it finds a non-overlapping spot.
    private Vector2 GetSpiralSpawnPosition(RectTransform buttonRect)
    {
        // bounds inside area (account for button size so it doesn't clip)
        float halfW = buttonRect.rect.width * 0.5f;
        float halfH = buttonRect.rect.height * 0.5f;

        float minX = spawnAreaRect.rect.xMin + halfW;
        float maxX = spawnAreaRect.rect.xMax - halfW;
        float minY = spawnAreaRect.rect.yMin + halfH;
        float maxY = spawnAreaRect.rect.yMax - halfH;

        Vector2 center = spawnAreaRect.rect.center;

        float maxRadiusX = Mathf.Min(center.x - minX, maxX - center.x);
        float maxRadiusY = Mathf.Min(center.y - minY, maxY - center.y);
        float maxRadius = Mathf.Max(5f, Mathf.Min(maxRadiusX, maxRadiusY));

        // golden angle spiral
        const float goldenAngle = 2.39996323f;

        for (int attempt = 0; attempt < maxSpawnTries; attempt++)
        {
            // progress expands outward; centerBias makes earlier attempts tighter near center
            float p = (maxSpawnTries <= 1) ? 1f : (float)attempt / (maxSpawnTries - 1);
            float radius = Mathf.Lerp(0f, maxRadius, Mathf.Pow(p, 1f / Mathf.Max(0.01f, centerBias)));

            float angle = attempt * goldenAngle;

            // slight jitter so it doesn't look too patterned
            float jitter = 0.15f;
            float jx = Random.Range(-jitter, jitter);
            float jy = Random.Range(-jitter, jitter);

            float x = center.x + Mathf.Cos(angle) * radius + jx * radius;
            float y = center.y + Mathf.Sin(angle) * radius + jy * radius;

            x = Mathf.Clamp(x, minX, maxX);
            y = Mathf.Clamp(y, minY, maxY);

            Vector2 candidate = new Vector2(x, y);

            buttonRect.anchoredPosition = candidate;

            if (OverlapsNoSpawnZones(buttonRect)) continue;
            if (OverlapsOtherButtons(buttonRect)) continue;

            return candidate;
        }

        // last resort: anywhere inside (still clamped)
        float rx = Random.Range(minX, maxX);
        float ry = Random.Range(minY, maxY);
        return new Vector2(rx, ry);
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
        Rect ra = GetWorldRect(a, overlapPadding);
        Rect rb = GetWorldRect(b, overlapPadding);
        return ra.Overlaps(rb);
    }

    private Rect GetWorldRect(RectTransform rt, float padding)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        Vector2 min = corners[0];
        Vector2 max = corners[2];

        min -= new Vector2(padding, padding);
        max += new Vector2(padding, padding);

        return new Rect(min, max - min);
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

    private void ClearButtons()
    {
        foreach (ThoughtButtonUI button in activeButtons)
        {
            if (button != null) Destroy(button.gameObject);
        }
        activeButtons.Clear();
    }
}
