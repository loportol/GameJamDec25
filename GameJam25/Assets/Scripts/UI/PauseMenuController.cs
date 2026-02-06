using UnityEngine;
using UnityEngine.SceneManagement;
using Yarn.Unity;

public class PauseMenuController : MonoBehaviour
{
    public GameObject dialogue;

    [SerializeField] private GameObject pauseUI;
    private DialogueManager qteManager;
    [SerializeField] private GameObject settingsUI;
    private DialogueRunner yarnRunner;
    private LinePresenter presenter;


    private void Awake()
    {
        // this finds the QTE manager in the scene once
        qteManager = Object.FindFirstObjectByType<DialogueManager>();
        if (qteManager == null)
            Debug.LogWarning("Could not find the QTEManager in Awake, will try again when pausing.");
        
        // this finds the DialogueRunner in the scene once
        yarnRunner = Object.FindFirstObjectByType<DialogueRunner>();
        if (yarnRunner == null)
            Debug.LogWarning("Could not find the DialogueRunner in Awake, will try again when pausing.");

        presenter = Object.FindFirstObjectByType<LinePresenter>();
        if (presenter == null)
            Debug.LogWarning("Could not find LinePresenter in Awake; subtitles may not pause.");    
    }

    // make sure we ALWAYS have a reference even if something loaded weird / object changed
    private void EnsureQTEManager()
    {
        if (qteManager == null)
        {
            qteManager = Object.FindFirstObjectByType<DialogueManager>();
            if (qteManager == null)
                Debug.LogWarning("EnsureQTEManager: still could not find DialogueQTEManager.");
        }
    }

    public void BackToGame()
    {
        if (UISFXManager.Instance != null)
        {
            UISFXManager.Instance.PlayClick();
        }

        pauseUI.gameObject.SetActive(false);
        dialogue.gameObject.SetActive(true);

        // unpause logic for QTE/timer
        //EnsureQTEManager();

        Time.timeScale = 1f;

        if (AudioClipManager.Instance != null)
        {
            AudioClipManager.Instance.PauseDialogue(false);
        }

        if (yarnRunner != null)
            yarnRunner.enabled = true;

        if (presenter != null)
            presenter.enabled = true;
    }

    public void BackFromSettings()
    {
        if (UISFXManager.Instance != null)
        {
            UISFXManager.Instance.PlayClick();
        }
        settingsUI.gameObject.SetActive(false);
        pauseUI.gameObject.SetActive(true);
    }

    public void OpenSettings()
    {
        if (UISFXManager.Instance != null)
        {
            UISFXManager.Instance.PlayClick();
        }
        pauseUI.gameObject.SetActive(false);
        if (settingsUI != null) settingsUI.gameObject.SetActive(true);
    }

    public void PauseGame()
    {
        if (UISFXManager.Instance != null)
        {
            UISFXManager.Instance.PlayClick();
        }

        pauseUI.gameObject.SetActive(true);
        dialogue.gameObject.SetActive(false);

        Time.timeScale = 0f;

        if (AudioClipManager.Instance != null)
        {
            AudioClipManager.Instance.PauseDialogue(true);
        }

        if (yarnRunner != null)
            yarnRunner.enabled = false;
        
        if (yarnRunner != null)
            yarnRunner.enabled = false;

        if (presenter != null)
            presenter.enabled = false;
    }

    public void LoadMainMenu()
    {
        if (UISFXManager.Instance != null)
        {
            UISFXManager.Instance.PlayClick();
        }

        Time.timeScale = 1f;

        // unpause QTE if it exists so it doesn't stay paused after scene swap

        if (AudioClipManager.Instance != null)
        {
            AudioClipManager.Instance.StopDialogue();
        }

        SceneManager.LoadScene(0);
    }

    public void RestartGame()
    {
        if (UISFXManager.Instance != null)
        {
            UISFXManager.Instance.PlayClick();
        }

        Time.timeScale = 1f;

        if (AudioClipManager.Instance != null)
        {
            AudioClipManager.Instance.StopDialogue();
        }
        if (yarnRunner != null)
            yarnRunner.enabled = true;

        if (yarnRunner != null)
            yarnRunner.enabled = true;

        if (presenter != null)
            presenter.enabled = true;

        SceneManager.LoadScene(1);
    }
}
