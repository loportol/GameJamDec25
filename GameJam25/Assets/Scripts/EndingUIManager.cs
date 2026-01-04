using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndingUIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject timidPanel;
    [SerializeField] private GameObject focusedPanel;
    [SerializeField] private GameObject combativePanel;

    [Header("Main Menu Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void Start()
    {
        // make sure all endings are hidden at start
        timidPanel.SetActive(false);
        focusedPanel.SetActive(false);
        combativePanel.SetActive(false);

        // listen for ending signal from AudioClipManager
        AudioClipManager.Instance.endingReached.AddListener(OnEndingReached);
    }

    private void OnEndingReached(AudioClipSO endingClip)
    {
        if (endingClip == null) return;

        // hide all, then show the one we want
        timidPanel.SetActive(false);
        focusedPanel.SetActive(false);
        combativePanel.SetActive(false);

        // pause the game 
        Time.timeScale = 0f;

        // show correct ending panel
        switch (endingClip.GetEndingType())
        {
            case ChoiceType.Combative:
                combativePanel.SetActive(true);
                break;

            case ChoiceType.Focused:
                focusedPanel.SetActive(true);
                break;

            default:
                timidPanel.SetActive(true);
                break;
        }

        // automatically go back to main menu after the clip’s hold time
        StartCoroutine(ReturnToMenuAfter(endingClip.GetEndingHoldSeconds()));
    }

    private IEnumerator ReturnToMenuAfter(float seconds)
    {
        // since we froze timeScale, use realtime wait
        yield return new WaitForSecondsRealtime(seconds);

        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
