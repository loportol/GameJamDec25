using System.Collections.Generic;
using System.Drawing;
using UnityEngine;
using static Unity.Collections.AllocatorManager;

[CreateAssetMenu(menuName = "Scriptable Objects/Audio Clip")]
public class AudioClipSO : ScriptableObject
{
    [SerializeField] private AudioClip audioClip; 
    [SerializeField] private List<string> responses;
    [SerializeField] private List<int> correctResponses;
    [SerializeField] private float clipLength;

    public List<string> GetResponses()
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

    public bool IsResponseCorrect(string response)
    {
        foreach (int index in correctResponses)
        {
            if (index < 0 || index >= responses.Count) continue;
 
            if (responses[index] == response)
            {
                return true;
            }
        }
        return false;
    }
}
