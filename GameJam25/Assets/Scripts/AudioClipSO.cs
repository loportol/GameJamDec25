using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

[System.Serializable]
public class ClipResponse
{
    public string response;
    public int numToSpawn;
    public ChoiceType choiceType;
    public float spawnTime = 0; // 0 means its spawned from the beginning
    public float responseSize = 1f; // default is 1
    public AudioClipSO nextClipToPlay;
}

[CreateAssetMenu(menuName = "Scriptable Objects/Audio Clip")]
public class AudioClipSO : ScriptableObject
{
    [SerializeField] private AudioClip audioClip; 
    [SerializeField] private List<ClipResponse> responses;
    [SerializeField] private float clipLength;
    [SerializeField] private AudioClipSO nextClipIfChoiceSkipped;

    public List<ClipResponse> GetResponses()
    {
        return responses;
    }

    public AudioClip GetAudioClip()
    {
        return audioClip;
    }

    public float GetClipLength()
    {
        return clipLength;
    }

    public AudioClipSO GetNextClipIfChoiceSkipped()
    {
        return nextClipIfChoiceSkipped;
    }
}