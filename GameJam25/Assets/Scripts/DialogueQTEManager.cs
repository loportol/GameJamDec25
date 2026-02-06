using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

// NOTE: This import is unnecessary in Unity and can cause platform issues in builds.
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

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private List<ThoughtButtonUI> activeButtons = new List<ThoughtButtonUI>();

    private bool responded = false;
    private Coroutine timerRoutine;
    private Coroutine spawnRoutine;

    private bool isPaused = false;
    private bool timerIsActive = false;
    private bool inResponseWindow = false; // only true AFTER audio ends

    private bool responseWindowPending = false;
    private Coroutine responseWindowRoutine;

    private MomPortraitRoutes portraitManager;

    // NEW: session token so old coroutines can safely abort
    private int dialogueRunId = 0;
    // When true, don't allow entering a response window or enabling buttons (used for endings)
    private bool suppressResponseWindow = false;

    private void Start()
    {
        if (AudioClipManager.Instance != null)
        {
            AudioClipManager.Instance.dialogueHasStarted.AddListener(OnDialogueStarted);
            AudioClipManager.Instance.dialogueHasEnded.AddListener(OnDialogueEnded);
        }
        else
        {
            if (debugLogs) Debug.Log("[QTE] AudioClipManager.Instance not found in Start().");
        }

        if (timerSlider != null)
        {
            timerSlider.maxValue = responseTime;
            timerSlider.value = responseTime;
            timerSlider.gameObject.SetActive(false);
        }
        else
        {
            if (debugLogs) Debug.Log("[QTE] timerSlider is not assigned in DialogueQTEManager.");
        }

        portraitManager = Object.FindFirstObjectByType<MomPortraitRoutes>();
        if (portraitManager == null)
            Debug.Log("Could not find MomPortraitRoutes, can't set portrait for mom");
    }

    private void StopSpawnRoutine(string reason)
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;

            if (debugLogs) Debug.Log($"[QTE] Spawn FORCE-STOP ({reason}).");
        }
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;

        SetButtonsInteractable(!paused && inResponseWindow);

        if (timerSlider != null)
        {
            if (paused)
            {
                timerSlider.gameObject.SetActive(false);
            }
            else
            {
                if (responseWindowPending)
                {
                    TryEnterResponseWindowNow();
                }

                if (timerIsActive)
                {
                    timerSlider.gameObject.SetActive(true);
                }
            }
        }
    }

    private void OnDialogueStarted(AudioClipSO clip)
    {
        // If an ending sequence is active, ignore new dialogue starts so we
        // don't accidentally clear the ending UI. This prevents ending
        // dialogue buttons from being destroyed until the game actually
        // transitions (for example back to the main menu).
        if (suppressResponseWindow)
        {
            if (debugLogs) Debug.Log("[QTE] Ignoring new dialogue start during ending.");
            return;
        }

        // bump session id so any old coroutine exits
        dialogueRunId++;

        if (debugLogs) Debug.Log($"[QTE] Dialogue STARTED. clip={(clip != null ? clip.name : "null")} runId={dialogueRunId}");

        responded = false;
        inResponseWindow = false;
        responseWindowPending = false;

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
        spawnRoutine = StartCoroutine(SpawnDuringAudio(clip, dialogueRunId));
    }

    private void OnDialogueEnded()
    {
        if (debugLogs) Debug.Log("[QTE] Dialogue ENDED event received.");

        responseWindowPending = true;

        // CRITICAL FIX: stop spawning as soon as audio ends
        StopSpawnRoutine("dialogue ended");

        if (isPaused)
        {
            if (debugLogs) Debug.Log("[QTE] Dialogue ended while paused -> response window pending.");
            return;
        }

        if (responseWindowRoutine != null) StopCoroutine(responseWindowRoutine);
        responseWindowRoutine = StartCoroutine(WaitForDialogueToReallyEndThenEnter());
    }

    private IEnumerator WaitForDialogueToReallyEndThenEnter()
    {
        const float maxWaitSeconds = 1.25f;
        float waited = 0f;

        float clipLen = 0f;
        if (AudioClipManager.Instance != null)
            clipLen = AudioClipManager.Instance.GetCurrentClipLength();

        while (AudioClipManager.Instance != null && AudioClipManager.Instance.IsDialogueActive())
        {
            if (isPaused) yield break;

            float t = AudioClipManager.Instance.GetPlaybackTime();
            if (clipLen > 0.01f && t >= (clipLen - 0.03f))
            {
                if (debugLogs) Debug.Log($"[QTE] Forcing end-of-dialogue (playbackTime={t:0.00}/{clipLen:0.00})");
                break;
            }

            waited += Time.deltaTime;
            if (waited >= maxWaitSeconds)
            {
                if (debugLogs) Debug.Log($"[QTE] Forcing response window after timeout ({maxWaitSeconds:0.00}s) - IsDialogueActive still true.");
                break;
            }

            yield return null;
        }

        TryEnterResponseWindowNow();
    }

    private void TryEnterResponseWindowNow()
    {
        if (!responseWindowPending) return;
        if (isPaused) return;

        if (suppressResponseWindow)
        {
            if (debugLogs) Debug.Log("[QTE] Suppressing response window for ending.");
            responseWindowPending = false;
            StopSpawnRoutine("ending - suppress response window");
            // Do NOT clear buttons here: ending UI intentionally remains until
            // the game transitions away (e.g., to main menu). Clearing would
            // destroy the ending thoughts immediately, which is undesired.
            return;
        }

        if (AudioClipManager.Instance != null && AudioClipManager.Instance.IsDialogueActive())
        {
            if (debugLogs) Debug.Log("[QTE] TryEnterResponseWindowNow blocked (IsDialogueActive still true).");
            return;
        }

        responseWindowPending = false;

        // CRITICAL FIX: ensure nothing can spawn after we enter the response window
        StopSpawnRoutine("entering response window");

        inResponseWindow = true;
        if (debugLogs) Debug.Log($"[QTE] ENTER response window. activeButtons={activeButtons.Count}");

        SetButtonsInteractable(true);
        timerIsActive = true;

        timerSlider.gameObject.SetActive(true);

        if (timerRoutine != null) StopCoroutine(timerRoutine);
        timerRoutine = StartCoroutine(ResponseTimer());
    }

    private IEnumerator SpawnDuringAudio(AudioClipSO clip, int runId)
    {
        if (clip == null)
        {
            Debug.LogWarning("DialogueQTEManager got null AudioClipSO.");
            yield break;
        }

        List<ClipResponse> responses = clip.GetResponses();
        if (responses == null || responses.Count == 0)
        {
            if (debugLogs) Debug.Log($"[QTE] SpawnDuringAudio: no responses for clip {clip.name} (skipping spawns).");
            yield break;
        }

        if (debugLogs) Debug.Log($"[QTE] Spawn START. clip={clip.name}, responseGroups={responses.Count} runId={runId}");

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
            // abort if a new dialogue started / run changed
            if (runId != dialogueRunId) yield break;

            // abort if we already entered response window / dialogue ended
            if (inResponseWindow || responseWindowPending) yield break;

            while (isPaused) yield return null;

            ClipResponse cr = ordered[idx];

            float startTime = Mathf.Max(0f, cr.spawnTime);

            float nextTime = clipLength;
            if (idx + 1 < ordered.Count)
                nextTime = Mathf.Max(startTime, ordered[idx + 1].spawnTime);

            float rawWindow = nextTime - startTime;
            float spawnWindow = Mathf.Clamp(rawWindow * windowFillPercent, minSpawnWindowSeconds, maxSpawnWindowSeconds);

            if (debugLogs) Debug.Log($"[QTE] Spawn group idx={idx} start={startTime:0.00}s window={spawnWindow:0.00}s numToSpawn={cr.numToSpawn}");

            bool reached = false;
            yield return StartCoroutine(WaitUntilDialogueTime(startTime, runId, reachedSetter: v => reached = v));

            // if audio ended before reaching target time, stop spawning entirely
            if (!reached) yield break;

            yield return StartCoroutine(SpawnClipResponseButtonsSlow(cr, spawnWindow, runId));
        }

        if (debugLogs) Debug.Log($"[QTE] Spawn STOP. clip={clip.name}, activeButtons={activeButtons.Count} runId={runId}");
    }

    // WaitUntilDialogueTime now tells caller whether we actually reached the time
    private IEnumerator WaitUntilDialogueTime(float targetSeconds, int runId, System.Action<bool> reachedSetter)
    {
        reachedSetter?.Invoke(false);

        if (!useAudioTime)
        {
            float waited = 0f;
            while (waited < targetSeconds)
            {
                if (runId != dialogueRunId) yield break;
                if (inResponseWindow || responseWindowPending) yield break;

                if (!isPaused)
                {
                    waited += Time.deltaTime;
                }
                yield return null;
            }

            reachedSetter?.Invoke(true);
            yield break;
        }

        const float maxStallSeconds = 0.75f;
        float stall = 0f;
        float lastT = -999f;

        while (AudioClipManager.Instance != null &&
            AudioClipManager.Instance.IsDialogueActive() &&
            AudioClipManager.Instance.GetPlaybackTime() < targetSeconds)
        {
            if (runId != dialogueRunId) yield break;
            if (inResponseWindow || responseWindowPending) yield break;

            if (isPaused)
            {
                yield return null;
                continue;
            }

            float t = AudioClipManager.Instance.GetPlaybackTime();
                if (Mathf.Abs(t - lastT) < 0.0001f)
                {
                    stall += Time.deltaTime;
                    if (stall >= maxStallSeconds)
                    {
                        if (debugLogs) Debug.Log($"[QTE] WaitUntilDialogueTime stall escape (t={t:0.00}, target={targetSeconds:0.00}).");
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

    private IEnumerator SpawnClipResponseButtonsSlow(ClipResponse clipResponse, float spawnWindowSeconds, int runId)
    {
        if (clipResponse == null) yield break;

        int count = Mathf.Max(1, clipResponse.numToSpawn);

        float interval = (count <= 1) ? spawnWindowSeconds : (spawnWindowSeconds / (count - 1));
        interval = Mathf.Max(0.01f, interval);

        for (int i = 0; i < count; i++)
        {
            if (runId != dialogueRunId) yield break;
            if (inResponseWindow || responseWindowPending) yield break;

            while (isPaused) yield return null;

            yield return StartCoroutine(SpawnOneThoughtButtonCoroutine(clipResponse, runId));

            float wait = interval + extraDelayBetweenThoughts;

            if (useAudioTime)
            {
                if (AudioClipManager.Instance == null) yield break;

                float start = AudioClipManager.Instance.GetPlaybackTime();
                while (AudioClipManager.Instance.IsDialogueActive() &&
                    (AudioClipManager.Instance.GetPlaybackTime() - start) < wait)
                {
                    if (runId != dialogueRunId) yield break;
                    if (inResponseWindow || responseWindowPending) yield break;

                    if (isPaused)
                    {
                        start = AudioClipManager.Instance.GetPlaybackTime();
                        yield return null;
                        continue;
                    }

                    yield return null;
                }

                // if audio ended during the drip wait, stop spawning
                if (!AudioClipManager.Instance.IsDialogueActive()) yield break;
            }
            else
            {
                float waited = 0f;
                while (waited < wait)
                {
                    if (runId != dialogueRunId) yield break;
                    if (inResponseWindow || responseWindowPending) yield break;

                    if (!isPaused)
                    {
                        waited += Time.deltaTime;
                    }
                    yield return null;
                }
            }
        }
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

    private IEnumerator SpawnOneThoughtButtonCoroutine(ClipResponse clipResponse, int runId)
    {
        if (runId != dialogueRunId) yield break;
        if (inResponseWindow || responseWindowPending) yield break;

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

        if (runId != dialogueRunId)
        {
            Destroy(buttonObj);
            yield break;
        }

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
        bool found = TryGetSpiralSpawnPosition(rect, out pos);

        if (!found)
        {
            bool shouldShrink = shrinkToFitIfNeeded && (Random.value < chanceToShrinkInsteadOfOverflow);

            if (shouldShrink)
            {
                float currentScale = buttonObj.transform.localScale.x;
                bool placed = false;

                while (currentScale > minButtonScale)
                {
                    currentScale *= shrinkStep;
                    buttonObj.transform.localScale = new Vector3(currentScale, currentScale, 1f);

                    yield return null;

                    if (runId != dialogueRunId)
                    {
                        Destroy(buttonObj);
                        yield break;
                    }

                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

                    LayoutElement le2 = buttonObj.GetComponent<LayoutElement>();
                    if (le2 != null)
                    {
                        le2.preferredWidth = rect.rect.width;
                        le2.preferredHeight = rect.rect.height;
                    }

                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

                    if (TryGetSpiralSpawnPosition(rect, out pos))
                    {
                        rect.anchoredPosition = pos;
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    rect.anchoredPosition = pos;
                }
            }
            else
            {
                rect.anchoredPosition = pos;
            }
        }
        else
        {
            rect.anchoredPosition = pos;
        }

        StartCoroutine(AnimateThoughtIn(cg, buttonObj.transform, buttonObj.transform.localScale));

        bool canClickNow = (!isPaused && inResponseWindow);
        button.SetInteractable(canClickNow);

        activeButtons.Add(button);

        if (debugLogs) Debug.Log($"[QTE] Spawned thought. clickableNow={canClickNow} inResponseWindow={inResponseWindow} activeButtons={activeButtons.Count}");
    }

    // --- placement helpers unchanged ---
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
        if (debugLogs) Debug.Log("[QTE] Timer START.");

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
            if (debugLogs) Debug.Log("[QTE] Timer END (no response) -> clearing + continuing dialogue (timid).");

            ClearButtons();
            AudioClipManager.Instance.PlayDialogue();
            if (portraitManager != null) portraitManager.SetRouteSprite(ChoiceType.Timid);
        }
        else
        {
            if (debugLogs) Debug.Log("[QTE] Timer END (responded).");
        }
    }

    private void OnResponseSelected(ClipResponse chosen)
    {
        if (debugLogs) Debug.Log($"[QTE] Response SELECTED: {(chosen != null ? chosen.choiceType.ToString() : "null")}");

        responded = true;
        timerIsActive = false;
        inResponseWindow = false;
        responseWindowPending = false;

        // CRITICAL FIX: stop spawning immediately on selection too
        StopSpawnRoutine("response selected");

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
            if (button != null)
            {
                // never enable buttons during an ending suppression
                if (canClick && suppressResponseWindow)
                {
                    button.SetInteractable(false);
                }
                else
                {
                    button.SetInteractable(canClick);
                }
            }
        }

        if (debugLogs) Debug.Log($"[QTE] SetButtonsInteractable({canClick}) activeButtons={activeButtons.Count} inResponseWindow={inResponseWindow} paused={isPaused}");
    }

    public void PlayEndingThoughts(ChoiceType endingType, string focusedText = "I can do this.")
    {
        // prevent any normal response window behavior for endings
        suppressResponseWindow = true;

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
        if (debugLogs) Debug.Log($"[QTE] ClearButtons. destroying={activeButtons.Count}");

        foreach (ThoughtButtonUI button in activeButtons)
        {
            if (button != null) Destroy(button.gameObject);
        }
        activeButtons.Clear();
    }
}