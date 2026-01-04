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
    public static float GlobalSfxVolume = 0.6f;
    private const string SFX_KEY = "sfx_volume";

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
        GlobalSfxVolume = PlayerPrefs.GetFloat(SFX_KEY, GlobalSfxVolume);
        if (!PlayerPrefs.HasKey("sfx_volume"))
        {
            PlayerPrefs.SetFloat("sfx_volume", clickVolume);
        }
        GlobalSfxVolume = PlayerPrefs.GetFloat("sfx_volume", clickVolume);
        }

    public void PlayClick()
    {
        if (clickClip == null || source == null) return;

        // tiny pitch randomization 
        source.pitch = Random.Range(pitchMin, pitchMax);
        source.PlayOneShot(clickClip, GlobalSfxVolume);
    }
    public void SetSfxVolume(float value)
{
    GlobalSfxVolume = value;
    PlayerPrefs.SetFloat(SFX_KEY, value);
}
}
