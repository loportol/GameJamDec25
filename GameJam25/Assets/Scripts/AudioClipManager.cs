using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public enum ChoiceType
{
    Combative,
    Timid,
    Focused
}

public class AudioClipManager : MonoBehaviour
{
    [HideInInspector] public UnityEvent<AudioClipSO> dialogueHasStarted = new UnityEvent<AudioClipSO>();
    [HideInInspector] public UnityEvent dialogueHasEnded = new UnityEvent();

    // EndingUIManager can listen to this
    [HideInInspector] public UnityEvent<AudioClipSO> endingReached = new UnityEvent<AudioClipSO>();

    public AudioClipSO startingAudioClip;

    public static AudioClipManager Instance { get; private set; }
    private AudioSource audioSource;
    private AudioClipSO clipToPlay;

    // track pause state
    private bool isPaused = false;

    // authoritative "dialogue active" state (prevents QTE conflicts)
    private bool dialogueActive = false;

    // dialogue volume system
    private const string DIALOGUE_KEY = "dialogue_volume";
    public static float GlobalDialogueVolume = 1f;

    private Coroutine playRoutine = null;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        audioSource = GetComponent<AudioSource>();
        clipToPlay = startingAudioClip;

        // load saved volume right away
        GlobalDialogueVolume = PlayerPrefs.GetFloat(DIALOGUE_KEY, 1f);
        if (audioSource != null)
        {
            audioSource.volume = GlobalDialogueVolume;
        }
    }

    // used by SettingsMenu dialogue slider
    public void SetDialogueVolume(float value)
    {
        GlobalDialogueVolume = value;
        PlayerPrefs.SetFloat(DIALOGUE_KEY, value);

        if (audioSource != null)
        {
            audioSource.volume = GlobalDialogueVolume;
        }
    }

    public float GetDialogueVolume() => GlobalDialogueVolume;

    public void PlayDialogue()
    {
        if (audioSource == null)
        {
            Debug.LogWarning("PlayDialogue called but AudioSource is missing.");
            return;
        }

        if (clipToPlay == null)
        {
            Debug.LogWarning("PlayDialogue called but clipToPlay is null.");
            return;
        }

        // if we’re currently playing, don’t stack calls
        if (audioSource.isPlaying) return;

        Debug.Log("Playing audio: " + clipToPlay.name);

        // stop any stale routine (safety)
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        // whenever dialogue starts, we're not paused + we are active
        isPaused = false;
        dialogueActive = true;

        // if this clip is an ending, trigger ending UI too (but still run end logic)
        if (clipToPlay.IsEnding())
        {
            AudioClip endClip = clipToPlay.GetAudioClip();
            if (endClip == null)
            {
                Debug.LogWarning("Ending clip returned null AudioClip.");
                dialogueActive = false;
                return;
            }

            audioSource.clip = endClip;
            audioSource.volume = GlobalDialogueVolume;
            audioSource.Play();

            dialogueHasStarted.Invoke(clipToPlay);
            endingReached.Invoke(clipToPlay);

            playRoutine = StartCoroutine(PlayDialogueCoroutine(clipToPlay));
            return;
        }

        dialogueHasStarted.Invoke(clipToPlay);
        playRoutine = StartCoroutine(PlayDialogueCoroutine(clipToPlay));
    }

    private IEnumerator PlayDialogueCoroutine(AudioClipSO clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("PlayDialogueCoroutine got null clip.");
            dialogueActive = false;
            yield break;
        }

        AudioClip unityClip = clip.GetAudioClip();
        if (unityClip == null)
        {
            Debug.LogWarning("PlayDialogueCoroutine got AudioClipSO with null AudioClip.");
            dialogueActive = false;
            yield break;
        }

        audioSource.clip = unityClip;
        audioSource.volume = GlobalDialogueVolume;
        audioSource.Play();

        // choose skip fallback right away
        clipToPlay = clip.GetNextClipIfChoiceSkipped();

        // wait until audio actually starts reporting time
        float startTimeout = 1.0f;
        float t = 0f;
        while (audioSource.clip != null && audioSource.time <= 0f && t < startTimeout)
        {
            t += Time.deltaTime;
            yield return null;
        }

        // now wait for real end (Pause-safe)
        // IMPORTANT: don't use isPlaying alone because Pause() makes isPlaying false
        while (audioSource.clip == unityClip)
        {
            if (isPaused)
            {
                yield return null;
                continue;
            }

            // if the clip is basically done, break
            if (audioSource.time >= unityClip.length - 0.01f)
            {
                break;
            }

            yield return null;
        }

        // mark inactive BEFORE invoking, so QTE won't block on IsDialogueActive()
        dialogueActive = false;

        // now it ACTUALLY ended
        dialogueHasEnded.Invoke();

        playRoutine = null;
    }

    // QTE calls this when a player clicks a response button.
    public void ChooseResponse(ClipResponse choice)
    {
        if (choice == null)
        {
            Debug.LogWarning("ChooseResponse called with null choice; defaulting to skip.");
            PlayDialogue();
            return;
        }

        if (choice.nextClipToPlay != null)
        {
            clipToPlay = choice.nextClipToPlay;
        }
        else
        {
            Debug.LogWarning("No next clip set on chosen response; defaulting to skipped-choice clip.");
            // clipToPlay already set to nextClipIfChoiceSkipped inside PlayDialogueCoroutine
        }

        PlayDialogue();
    }

    public void PauseDialogue(bool pause)
    {
        if (audioSource == null) return;

        isPaused = pause;

        if (pause)
        {
            audioSource.Pause();
        }
        else
        {
            audioSource.UnPause();
        }
    }

    public void StopDialogue()
    {
        if (audioSource == null) return;

        audioSource.Stop();

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        isPaused = false;
        dialogueActive = false;
    }

    public float GetPlaybackTime()
    {
        if (audioSource == null) return 0f;
        var clip = audioSource.clip;
        if (clip == null) return 0f;

        // Guard against cases where the AudioSource resource isn't a loaded AudioClip
        // (Unity will warn when attempting to read `time` in that case).
        try
        {
            if (clip.loadState != AudioDataLoadState.Loaded) return 0f;
        }
        catch
        {
            return 0f;
        }

        return audioSource.time;
    }

    public bool IsPlaying()
    {
        return audioSource != null && audioSource.isPlaying;
    }

    public bool IsDialogueActive()
    {
        // dialogueActive is authoritative and will flip false before dialogueHasEnded fires
        return dialogueActive;
    }

    public bool IsPaused()
    {
        return isPaused;
    }

    public float GetCurrentClipLength()
    {
        if (audioSource == null) return 0f;
        var clip = audioSource.clip;
        if (clip == null) return 0f;
        try
        {
            if (clip.loadState != AudioDataLoadState.Loaded) return 0f;
        }
        catch
        {
            return 0f;
        }
        return clip.length;
    }
}
