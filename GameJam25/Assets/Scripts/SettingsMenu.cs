using UnityEngine;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider dialogueSlider;

    [Header("Targets")]
    [SerializeField] private SceneMusicPlayer musicPlayer;     // drag MenuMusic object here (optional now)
    [SerializeField] private AudioClipManager dialogueManager; // drag AudioManager object here (optional now)

    private const string MUSIC_KEY = "music_volume";
    private const string DIALOGUE_KEY = "dialogue_volume";

    private void Start()
    {
        float savedMusic = PlayerPrefs.GetFloat(MUSIC_KEY, 0.35f);
        float savedDialogue = PlayerPrefs.GetFloat(DIALOGUE_KEY, 1f);

        // keep global music volume in sync even before we touch sliders
        SceneMusicPlayer.GlobalMusicVolume = savedMusic;

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

        // apply immediately so you hear it right away
        SetMusicVolume(savedMusic);
        SetDialogueVolume(savedDialogue);
    }

    public void SetMusicVolume(float value)
    {
        PlayerPrefs.SetFloat(MUSIC_KEY, value);

        // ALWAYS update global so it carries across scenes
        SceneMusicPlayer.GlobalMusicVolume = value;

        // If you dragged a specific music player, update it (this will update both menu+game via SceneMusicPlayer)
        if (musicPlayer != null)
        {
            musicPlayer.SetMusicVolume(value);
            return;
        }

        // fallback: find any SceneMusicPlayer in the scene and update it
        SceneMusicPlayer found = FindObjectOfType<SceneMusicPlayer>();
        if (found != null)
        {
            found.SetMusicVolume(value);
        }
    }

    public void SetDialogueVolume(float value)
    {
        PlayerPrefs.SetFloat(DIALOGUE_KEY, value);

        if (dialogueManager != null)
        {
            dialogueManager.SetDialogueVolume(value);
            return;
        }

        // fallback: dialogue manager only exists in playable scene
        AudioClipManager found = FindObjectOfType<AudioClipManager>();
        if (found != null)
        {
            found.SetDialogueVolume(value);
        }
    }
}
