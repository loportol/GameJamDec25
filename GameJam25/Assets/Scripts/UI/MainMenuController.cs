using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuUI;
    [SerializeField] private GameObject creditsUI;

    public void BackToMain()
    {
        if (UISFXManager.Instance != null)
    {
        UISFXManager.Instance.PlayClick();
    }
        mainMenuUI.gameObject.SetActive(true);
        creditsUI.gameObject.SetActive(false);
    }

    public void OpenCredits()
    {
        if (UISFXManager.Instance != null)
    {
        UISFXManager.Instance.PlayClick();
    }
        mainMenuUI.gameObject.SetActive(false);
        creditsUI.gameObject.SetActive(true);
    }

    public void StartGame()
    {
        if (UISFXManager.Instance != null)
    {
        UISFXManager.Instance.PlayClick();
    }
        SceneManager.LoadScene(1);
    }

    public void ExitGame()
    {
        if (UISFXManager.Instance != null)
    {
        UISFXManager.Instance.PlayClick();
    }
        Application.Quit();

        // for testing in the Unity Editor
        #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}
