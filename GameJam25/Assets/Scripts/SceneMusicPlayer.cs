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

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = musicClip;
        audioSource.loop = true;
        audioSource.volume = volume;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D

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

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // IF you're in MainMenu, menu music plays and game music stops.
        // IF you're in the Game scene, game music plays and menu music stops.

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
