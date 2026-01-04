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
            // fallback: just wait real time 
            // pause safe waiting
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
            // stop progressing while paused
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
            // if paused -> don’t spawn during pause
            while (isPaused) yield return null;

            SpawnOneThoughtButton(clipResponse);

            // tiny delay makes it feel more organic
            float wait = interval + extraDelayBetweenThoughts;

            if (useAudioTime)
            {
                float start = AudioClipManager.Instance.GetPlaybackTime();
                while (AudioClipManager.Instance.IsDialogueActive() && (AudioClipManager.Instance.GetPlaybackTime() - start) < wait)
                {
                    // freeze this wait while paused
                    if (isPaused)
                    {
                        start = AudioClipManager.Instance.GetPlaybackTime(); // reset so we don't "skip" time after pause
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

    private void SpawnOneThoughtButton(ClipResponse clipResponse)
    {
        GameObject buttonObj = Instantiate(thoughtButtonPrefab, spawnAreaRect);

        buttonObj.transform.localScale *= clipResponse.responseSize;

        RectTransform rect = buttonObj.GetComponent<RectTransform>();
        rect.anchoredPosition = GetRandomSpawnPosition(rect);

        ThoughtButtonUI button = buttonObj.GetComponent<ThoughtButtonUI>();
        button.Setup(clipResponse, OnResponseSelected);

        button.SetInteractable(false);

        activeButtons.Add(button);
    }

    private Vector2 GetRandomSpawnPosition(RectTransform buttonRect)
    {
        // do rejection sampling: try up to N times to find a position
        // that doesn't overlap no-spawn zones or other buttons
        const int MAX_TRIES = 40;

        for (int attempt = 0; attempt < MAX_TRIES; attempt++)
        {
            Vector2 candidate = RandomPointInside(spawnAreaRect, buttonRect);

            // temp set so we can measure overlap using its rect
            buttonRect.anchoredPosition = candidate;

            if (OverlapsNoSpawnZones(buttonRect)) continue;
            if (OverlapsOtherButtons(buttonRect)) continue;

            return candidate;
        }

        return RandomPointInside(spawnAreaRect, buttonRect);
    }

    private Vector2 RandomPointInside(RectTransform area, RectTransform element)
    {
        // Keep inside spawn area with padding based on element size
        float halfW = element.rect.width * 0.5f;
        float halfH = element.rect.height * 0.5f;

        float minX = area.rect.xMin + halfW;
        float maxX = area.rect.xMax - halfW;
        float minY = area.rect.yMin + halfH;
        float maxY = area.rect.yMax - halfH;

        float x = Random.Range(minX, maxX);
        float y = Random.Range(minY, maxY);

        return new Vector2(x, y);
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
            if (otherRect == buttonRect) continue;
            if (RectsOverlap(buttonRect, otherRect)) return true;
        }
        return false;
    }

    private bool RectsOverlap(RectTransform a, RectTransform b)
    {
        Rect ra = GetRectInParentSpace(a);
        Rect rb = GetRectInParentSpace(b);
        return ra.Overlaps(rb);
    }

    private Rect GetRectInParentSpace(RectTransform rt)
    {
        Vector2 size = rt.rect.size;
        Vector2 pos = rt.anchoredPosition;
        return new Rect(pos - size * 0.5f, size);
    }

    private IEnumerator ResponseTimer()
    {
        float timeRemaining = responseTime;
        timerSlider.value = responseTime;

        while (timeRemaining > 0f && !responded)
        {
            // freeze timer while paused
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
            portraitManager.SetRouteSprite(ChoiceType.Timid);
        }
    }

    private void OnResponseSelected(ClipResponse chosen)
    {
        responded = true;
        timerIsActive = false;

        // stop timer if running
        if (timerRoutine != null) StopCoroutine(timerRoutine);
        timerSlider.gameObject.SetActive(false);

        ClearButtons();

        // tell audio manager what consequence to play next
        AudioClipManager.Instance.ChooseResponse(chosen);
        portraitManager.SetRouteSprite(chosen.choiceType);
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