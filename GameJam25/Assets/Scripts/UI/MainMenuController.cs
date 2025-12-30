using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuUI;
    [SerializeField] private GameObject creditsUI;

    public void BackToMain()
    {
        mainMenuUI.gameObject.SetActive(true);
        creditsUI.gameObject.SetActive(false);
    }

    public void OpenCredits()
    {
        mainMenuUI.gameObject.SetActive(false);
        creditsUI.gameObject.SetActive(true);
    }

    public void StartGame()
    {
        SceneManager.LoadScene(1);
    }

    public void ExitGame()
    {
        Application.Quit();

        // for testing in the Unity Editor
        #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}
