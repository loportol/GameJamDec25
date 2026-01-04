using UnityEngine;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider dialogueSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Targets (optional drag+drop)")]
    [SerializeField] private SceneMusicPlayer musicPlayer;     
    [SerializeField] private AudioClipManager dialogueManager; 
    [SerializeField] private UISFXManager sfxManager;

    private const string MUSIC_KEY = "music_volume";
    private const string DIALOGUE_KEY = "dialogue_volume";
    private const string SFX_KEY = "sfx_volume";

    private void Start()
    {
        float savedMusic = PlayerPrefs.GetFloat(MUSIC_KEY, 0.35f);
        float savedDialogue = PlayerPrefs.GetFloat(DIALOGUE_KEY, 1f);
        float savedSfx = PlayerPrefs.GetFloat(SFX_KEY, 0.6f);

        // keep globals synced so they apply across scenes
        SceneMusicPlayer.GlobalMusicVolume = savedMusic;
        AudioClipManager.GlobalDialogueVolume = savedDialogue;
        UISFXManager.GlobalSfxVolume = savedSfx;

        if (musicSlider != null)
        {
            musicSlider.minValue = 0f;
            musicSlider.maxValue = 1f;
            musicSlider.value = savedMusic;
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (dialogueSlider != null)
        {
            dialogueSlider.minValue = 0f;
            dialogueSlider.maxValue = 1f;
            dialogueSlider.value = savedDialogue;
            dialogueSlider.onValueChanged.AddListener(SetDialogueVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f;
            sfxSlider.maxValue = 1f;
            sfxSlider.value = savedSfx;
            sfxSlider.onValueChanged.AddListener(SetSfxVolume);
        }

        // apply immediately
        SetMusicVolume(savedMusic);
        SetDialogueVolume(savedDialogue);
        SetSfxVolume(savedSfx);
    }

    public void SetMusicVolume(float value)
    {
        PlayerPrefs.SetFloat(MUSIC_KEY, value);
        SceneMusicPlayer.GlobalMusicVolume = value;

        if (musicPlayer != null)
        {
            musicPlayer.SetMusicVolume(value);
            return;
        }

        // Unity 2023+ replacement for FindObjectOfType
        SceneMusicPlayer found = Object.FindFirstObjectByType<SceneMusicPlayer>();
        if (found != null) found.SetMusicVolume(value);
    }

    public void SetDialogueVolume(float value)
    {
        PlayerPrefs.SetFloat(DIALOGUE_KEY, value);
        AudioClipManager.GlobalDialogueVolume = value;

        if (dialogueManager != null)
        {
            dialogueManager.SetDialogueVolume(value);
            return;
        }

        AudioClipManager found = Object.FindFirstObjectByType<AudioClipManager>();
        if (found != null) found.SetDialogueVolume(value);
    }

    public void SetSfxVolume(float value)
    {
        PlayerPrefs.SetFloat(SFX_KEY, value);
        UISFXManager.GlobalSfxVolume = value;

        if (sfxManager != null)
        {
            sfxManager.SetSfxVolume(value);
            return;
        }

        UISFXManager found = Object.FindFirstObjectByType<UISFXManager>();
        if (found != null) found.SetSfxVolume(value);
    }
}
