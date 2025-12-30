using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioClipManager : MonoBehaviour
{
    public List<AudioClipSO> audioClips;

    public static AudioClipManager Instance { get; private set; }
    private AudioSource audioSource;
    private AudioClipSO currentClip;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        audioSource = GetComponent<AudioSource>();
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

        AudioClipSO clipToPlay = audioClips[0]; // 0 is placeholder
        currentClip = audioClips[0];
        audioSource.clip = clipToPlay.GetAudioClip();
        audioSource.Play();
        Debug.Log("Playing audio");
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
        if (!audioSource.isPlaying) return;
        audioSource.Stop();
    }
}
/*
Simplified: Point - and - Click Search based on Audio
The player will be fed a dialogue audio line 
While the dialogue is playing different “thoughts” will appear on screen to be used as responses
The player has a limited amount of time to respond to when the audio is finished (Quick Time Event)
*/
