using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pauseUI;
    [SerializeField] private GameObject settingsUI;

    public void BackToGame()
    {
        if (UISFXManager.Instance != null)
    {
        UISFXManager.Instance.PlayClick();
    }
        pauseUI.gameObject.SetActive(false);
        Time.timeScale = 1;
        AudioClipManager.Instance.PauseDialogue(false);
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
        Time.timeScale = 0;
        AudioClipManager.Instance.PauseDialogue(true);
    }

    public void LoadMainMenu()
    {
        if (UISFXManager.Instance != null)
    {
        UISFXManager.Instance.PlayClick();
    }
        AudioClipManager.Instance.StopDialogue();
        SceneManager.LoadScene(0);
    }
}
