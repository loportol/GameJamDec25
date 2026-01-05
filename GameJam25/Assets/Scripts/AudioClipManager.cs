using System.Collections;
using System.Collections.Generic;
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

    // track pause state so paused doesn't count as ended
    private bool isPaused = false;

    // dialogue volume system 
    private const string DIALOGUE_KEY = "dialogue_volume";
    public static float GlobalDialogueVolume = 1f;

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

        // load saved volume right away so audio doesn't "randomly" change between runs
        GlobalDialogueVolume = PlayerPrefs.GetFloat(DIALOGUE_KEY, 1f);
        if (audioSource != null)
        {
            audioSource.volume = GlobalDialogueVolume;
        }
    }

    private void Update()
    {
        // For testing purposes
        //if (Input.GetKeyDown(KeyCode.P))
        //{
            //PlayDialogue();
       // }
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

    public float GetDialogueVolume()
    {
        return GlobalDialogueVolume;
    }

    public void PlayDialogue()
    {
        if (audioSource.isPlaying) return;

        // if this clip is an ending, trigger ending UI instead of normal loop
        if (clipToPlay != null && clipToPlay.IsEnding())
        {
            endingReached.Invoke(clipToPlay);
            return;
        }

        dialogueHasStarted.Invoke(clipToPlay);
        StartCoroutine(PlayDialogueCoroutine(clipToPlay));
        Debug.Log("Playing audio");
        // spawn buttons once dialogue starts
    }

    private IEnumerator PlayDialogueCoroutine(AudioClipSO clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("PlayDialogueCoroutine got null clip.");
            yield break;
        }

        audioSource.clip = clip.GetAudioClip();
        audioSource.volume = GlobalDialogueVolume; // keep volume consistent
        audioSource.Play();

        // whenever dialogue starts, we're not paused
        isPaused = false;

        clipToPlay = clip.GetNextClipIfChoiceSkipped();

        // wait until audio actually starts playing
        yield return new WaitUntil(() => audioSource.clip != null && audioSource.time > 0f);

        // do NOT wait for "!isPlaying" because Pause() makes isPlaying false
        // instead, wait until we've reached the end of the clip by time
        while (audioSource.clip != null && audioSource.time < audioSource.clip.length)
        {
            // if audio is paused, time will not move anyway, so this just waits safely
            yield return null;
        }

        // now it ACTUALLY ended
        dialogueHasEnded.Invoke();
    }

    // QTE calls this when a player actually clicks a response button.
    public void ChooseResponse(ClipResponse choice)
    {
        if (choice == null)
        {
            Debug.LogWarning("ChooseResponse called with null choice; defaulting to skip.");
            PlayDialogue(); // will use nextClipIfChoiceSkipped that we already set during playback
            return;
        }

        // if a next clip exists, use it. If not, fall back to whatever skip is set to.
        if (choice.nextClipToPlay != null)
        {
            clipToPlay = choice.nextClipToPlay;
        }
        else
        {
            Debug.LogWarning("No next clip set on chosen response; defaulting to skipped-choice clip.");
            // clipToPlay was already set to nextClipIfChoiceSkipped inside PlayDialogueCoroutine
            // so we can just leave it alone here.
        }

        PlayDialogue();
    }

    public void PauseDialogue(bool pause)
    {
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
        audioSource.Stop();
    }

    public float GetPlaybackTime()
    {
        // how many seconds into the current audio clip we are
        return audioSource != null ? audioSource.time : 0f;
    }

    public bool IsPlaying()
    {
        return audioSource != null && audioSource.isPlaying;
    }
    public bool IsDialogueActive()
{
    return (audioSource != null && audioSource.clip != null && audioSource.time < audioSource.clip.length);
}

public bool IsPaused()
{
    return isPaused;
}

    public float GetCurrentClipLength()
    {
        return (audioSource != null && audioSource.clip != null) ? audioSource.clip.length : 0f;
    }
}
