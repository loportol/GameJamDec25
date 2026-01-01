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

    public AudioClipSO startingAudioClip;

    public static AudioClipManager Instance { get; private set; }
    private AudioSource audioSource;
    private AudioClipSO clipToPlay;

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
    }

    private void Update()
    {
        // For testing purposes
        if (Input.GetKeyDown(KeyCode.P))
        {
            PlayDialogue();
        }
    }

    public void PlayDialogue()
    {
        if (audioSource.isPlaying) return;

        dialogueHasStarted.Invoke(clipToPlay);
        StartCoroutine(PlayDialogueCoroutine(clipToPlay));
        Debug.Log("Playing audio");
        // spawn buttons once dialogue starts
    }

    private IEnumerator PlayDialogueCoroutine(AudioClipSO clip)
    {
        audioSource.clip = clip.GetAudioClip();
        audioSource.Play();
        clipToPlay = clip.GetNextClipIfChoiceSkipped();

        yield return new WaitUntil(() => audioSource.isPlaying);
        yield return new WaitUntil(() => !audioSource.isPlaying);

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
}