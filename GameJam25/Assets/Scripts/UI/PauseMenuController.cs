using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pauseUI;

    public void BackToGame()
    {
        pauseUI.gameObject.SetActive(false);
        Time.timeScale = 1;
        AudioClipManager.Instance.PauseDialogue(false);
    }

    public void PauseGame()
    {
        pauseUI.gameObject.SetActive(true);
        Time.timeScale = 0;
        AudioClipManager.Instance.PauseDialogue(true);
    }

    public void LoadMainMenu()
    {
        AudioClipManager.Instance.StopDialogue();
        SceneManager.LoadScene(0);
    }
}
