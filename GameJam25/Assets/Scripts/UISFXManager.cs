using UnityEngine;

public class UISFXManager : MonoBehaviour
{
    public static UISFXManager Instance { get; private set; }

    [Header("Clips")]
    [SerializeField] private AudioClip clickClip;

    [Header("Tuning")]
    [SerializeField] private float clickVolume = 0.6f;
    [SerializeField] private float pitchMin = 0.97f;
    [SerializeField] private float pitchMax = 1.03f;

    private AudioSource source;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f; // 2D
    }

    public void PlayClick()
    {
        if (clickClip == null || source == null) return;

        // tiny pitch randomization 
        source.pitch = Random.Range(pitchMin, pitchMax);
        source.PlayOneShot(clickClip, clickVolume);
    }
}
