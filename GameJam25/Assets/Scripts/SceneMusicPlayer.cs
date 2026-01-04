using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneMusicPlayer : MonoBehaviour
{
    [Header("Music")]
    [SerializeField] private AudioClip musicClip;
    [SerializeField] private float volume = 0.35f;

    [Header("Identity")]
    [SerializeField] private bool isMenuMusic = false; // check this in MainMenu version

    private AudioSource audioSource;

    private static SceneMusicPlayer menuInstance;
    private static SceneMusicPlayer gameInstance;

    // NEW: one shared volume for BOTH menu + game music
    public static float GlobalMusicVolume = 0.35f;

    private const string MUSIC_KEY = "music_volume";

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = musicClip;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D

        if (!PlayerPrefs.HasKey(MUSIC_KEY))
        {
            PlayerPrefs.SetFloat(MUSIC_KEY, volume); // your inspector default
        }

        GlobalMusicVolume = PlayerPrefs.GetFloat(MUSIC_KEY, volume);
        audioSource.volume = GlobalMusicVolume;

        if (isMenuMusic)
        {
            if (menuInstance != null) { Destroy(gameObject); return; }
            menuInstance = this;
        }
        else
        {
            if (gameInstance != null) { Destroy(gameObject); return; }
            gameInstance = this;
        }

        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        Play();
    }

    // sets the GLOBAL volume so it applies to BOTH players
    public void SetMusicVolume(float value)
    {
        GlobalMusicVolume = value;
        PlayerPrefs.SetFloat(MUSIC_KEY, value);

        // apply to this instance immediately
        if (audioSource != null)
        {
            audioSource.volume = GlobalMusicVolume;
        }

        // also apply to the other instance if it exists
        if (menuInstance != null && menuInstance.audioSource != null)
        {
            menuInstance.audioSource.volume = GlobalMusicVolume;
        }
        if (gameInstance != null && gameInstance.audioSource != null)
        {
            gameInstance.audioSource.volume = GlobalMusicVolume;
        }
    }

    public float GetMusicVolume()
    {
        return GlobalMusicVolume;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        // optional safety: clear the singleton reference if this was it
        if (menuInstance == this) menuInstance = null;
        if (gameInstance == this) gameInstance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // keep volume synced when scenes switch
        if (audioSource != null)
        {
            audioSource.volume = GlobalMusicVolume;
        }

        // IF you're in MainMenu, menu music plays and game music stops
        // IF you're in the Game scene, game music plays and menu music stops
        bool inMainMenu = scene.name == "MainMenu";
        bool shouldPlay = isMenuMusic ? inMainMenu : !inMainMenu;

        if (shouldPlay) Play();
        else Stop();
    }

    private void Play()
    {
        if (!audioSource.isPlaying)
            audioSource.Play();
    }

    private void Stop()
    {
        if (audioSource.isPlaying)
            audioSource.Stop();
    }
}
