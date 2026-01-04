using System.Collections.Generic;
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

    [Header("Ending")]
    [SerializeField] private bool isEnding = false;
    [SerializeField] private ChoiceType endingType = ChoiceType.Timid;

    [SerializeField] private string endingTitle;

    [TextArea(3, 6)]
    [SerializeField] private string endingBody;

    [SerializeField] private float endingHoldSeconds = 6f;


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

    // ending getters
    public bool IsEnding()
    {
        return isEnding;
    }

    public ChoiceType GetEndingType()
    {
        return endingType;
    }

    public string GetEndingTitle()
    {
        return endingTitle;
    }

    public string GetEndingBody()
    {
        return endingBody;
    }

    public float GetEndingHoldSeconds()
    {
        return endingHoldSeconds;
    }
}
