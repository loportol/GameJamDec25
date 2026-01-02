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

    // store active buttons so we can enable/disable them and clear them
    private readonly List<ThoughtButtonUI> activeButtons = new List<ThoughtButtonUI>();

    private bool responded = false;
    private Coroutine timerRoutine;
    private Coroutine spawnRoutine;

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
    }

    private void OnDialogueStarted(AudioClipSO clip)
    {
        responded = false;
        ClearButtons();
        timerSlider.gameObject.SetActive(false);

        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnDuringAudio(clip));
    }

    private void OnDialogueEnded()
    {
        // NOW the player is allowed to click
        SetButtonsInteractable(true);

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

        float t = 0f;

        List<ClipResponse> remaining = new List<ClipResponse>(responses);

        while (remaining.Count > 0)
        {
            t += Time.deltaTime;

            for (int i = remaining.Count - 1; i >= 0; i--)
            {
                if (t >= remaining[i].spawnTime)
                {
                    SpawnClipResponseButtons(remaining[i]);
                    remaining.RemoveAt(i);
                }
            }

            yield return null;
        }

        SetButtonsInteractable(false);
    }

    private void SpawnClipResponseButtons(ClipResponse clipResponse)
    {
        if (clipResponse == null) return;

        int count = Mathf.Max(1, clipResponse.numToSpawn);

        for (int i = 0; i < count; i++)
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
            timeRemaining -= Time.deltaTime;
            timerSlider.value = timeRemaining;
            yield return null;
        }

        timerSlider.value = 0f;
        timerSlider.gameObject.SetActive(false);

        if (!responded)
        {
            ClearButtons();
            AudioClipManager.Instance.PlayDialogue();
        }
    }

    private void OnResponseSelected(ClipResponse chosen)
    {
        responded = true;

        // stop timer if running
        if (timerRoutine != null) StopCoroutine(timerRoutine);
        timerSlider.gameObject.SetActive(false);

        ClearButtons();

        // tell audio manager what consequence to play next
        AudioClipManager.Instance.ChooseResponse(chosen);
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
