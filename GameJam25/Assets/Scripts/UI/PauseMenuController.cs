using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pauseUI;

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
