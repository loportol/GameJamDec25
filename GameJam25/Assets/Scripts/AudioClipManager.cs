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
    
    // call this after a choice has been made, only call if a button was selected
    private void PlayNextDialogue(ClipResponse clip)
    {
        AudioClipSO nextClip = clip.nextClipToPlay;
        if (nextClip)
        {
            clipToPlay = nextClip;
            Debug.LogWarning("No next clip has been set, defaulting to timid choice");
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