using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pauseUI;
    private DialogueQTEManager qteManager;

    private void Awake()
    {
        // this finds the QTE manager in the scene once
        qteManager = Object.FindFirstObjectByType<DialogueQTEManager>();
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

        Time.timeScale = 1f;

        if (AudioClipManager.Instance != null)
        {
            AudioClipManager.Instance.PauseDialogue(false);
        }
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

        Time.timeScale = 0f;

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

        Time.timeScale = 1f;

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
