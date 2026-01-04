using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pauseUI;
    private DialogueQTEManager qteManager;
    [SerializeField] private GameObject settingsUI;

    private void Awake()
    {
        // this finds the QTE manager in the scene once
        qteManager = Object.FindFirstObjectByType<DialogueQTEManager>();
        if (qteManager == null )
            Debug.LogWarning("Could not find the QTEManager, cannot pause timer");
    }

    public void BackToGame()
    {
        if (UISFXManager.Instance != null)
        {
            UISFXManager.Instance.PlayClick();
        }

        pauseUI.gameObject.SetActive(false);

        // unpause logic for QTE/timer
        if (qteManager != null)
        {
            qteManager.SetPaused(false);
        }

        //Time.timeScale = 1f;

        if (AudioClipManager.Instance != null)
        {
            AudioClipManager.Instance.PauseDialogue(false);
        }
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

        // pause logic for QTE/timer
        if (qteManager != null)
        {
            qteManager.SetPaused(true);
        }

        //Time.timeScale = 0f;

        if (AudioClipManager.Instance != null)
        {
            AudioClipManager.Instance.PauseDialogue(true);
        }
    }

    public void LoadMainMenu()
    {
        if (UISFXManager.Instance != null)
        {
            UISFXManager.Instance.PlayClick();
        }

        //Time.timeScale = 1f;

        // unpause QTE if it exists so it doesn't stay paused after scene swap
        if (qteManager != null)
        {
            qteManager.SetPaused(false);
        }

        if (AudioClipManager.Instance != null)
        {
            AudioClipManager.Instance.StopDialogue();
        }

        SceneManager.LoadScene(0);
    }
}
