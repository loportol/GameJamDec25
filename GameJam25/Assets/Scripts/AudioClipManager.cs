using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioClipManager : MonoBehaviour
{
    public List<AudioClipSO> audioClips;

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

        if (audioClips.Count > 0)
            clipToPlay = audioClips[0];
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

        StartCoroutine(PlayQueue(clipToPlay));
        Debug.Log("Playing audio");
    }

    private IEnumerator PlayQueue(AudioClipSO clip)
    {
        audioSource.clip = clip.GetAudioClip();
        audioSource.Play();

        yield return new WaitUntil(() => audioSource.isPlaying);
        yield return new WaitUntil(() => !audioSource.isPlaying);

        GetNextClipToPlay(clip);
    }

    private void GetNextClipToPlay(AudioClipSO clip)
    {
        AudioClipSO nextClip = clip.GetNextClip();
        if (nextClip)
        {
            clipToPlay = nextClip;
            PlayDialogue();
            return;
        }
        Debug.LogWarning("No next clip has been set");
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

/*
Simplified: Point - and - Click Search based on Audio
The player will be fed a dialogue audio line 
While the dialogue is playing different “thoughts” will appear on screen to be used as responses
The player has a limited amount of time to respond to when the audio is finished (Quick Time Event)
*/
