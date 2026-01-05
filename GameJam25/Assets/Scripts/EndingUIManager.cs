using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndingUIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject timidPanel;
    [SerializeField] private GameObject focusedPanel;
    [SerializeField] private GameObject combativePanel;

    [Header("Main Menu Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // ending Thoughts Hook
    [Header("Ending Thoughts")]
    [Tooltip("if assigned, will spawn ending thoughts using the same dialogue/thought button system.")]
    [SerializeField] private DialogueQTEManager qteManager;
    [SerializeField] private SceneMusicPlayer gameInstance;

    [Tooltip("Focused ending")]
    [SerializeField] private string focusedEndingThoughtText = "I love you.";

    [SerializeField] private Image blackFade;

    [SerializeField] private float fadeDuration = 5f;
    [SerializeField] private AnimationCurve smoothCurve = new AnimationCurve(new Keyframe[] { new Keyframe(0f, 0f), new Keyframe(1f, 1f) });

    private readonly WaitForSeconds skipFrame = new WaitForSeconds(0.0001f);
    private float timerCurrent;
    private float currentVol;

    private void Start()
    {
        // make sure all endings are hidden at start
        timidPanel.SetActive(false);
        focusedPanel.SetActive(false);
        combativePanel.SetActive(false);

        // auto find if you forgot to drag it in
        if (qteManager == null)
            qteManager = Object.FindFirstObjectByType<DialogueQTEManager>();

        // listen for ending signal from AudioClipManager
        AudioClipManager.Instance.endingReached.AddListener(OnEndingReached);
    }

    private void OnEndingReached(AudioClipSO endingClip)
    {
        if (endingClip == null) return;

        // hide all -> then show the one we want
        timidPanel.SetActive(false);
        focusedPanel.SetActive(false);
        combativePanel.SetActive(false);

        //pause the game
        //Time.timeScale = 0f;

        //spawn ending thoughts
        if (qteManager != null)
        {
            ChoiceType endingType = endingClip.GetEndingType();

            if (endingType == ChoiceType.Focused)
            {
                qteManager.PlayEndingThoughts(endingType, focusedEndingThoughtText);
            }
            else
            {
                qteManager.PlayEndingThoughts(endingType);
            }
        }

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
        
        float wait = seconds - fadeDuration;

        // since we froze timeScale, use realtime wait
        yield return new WaitForSeconds(wait);
        blackFade.gameObject.SetActive(true);
        yield return StartCoroutine(DoFade(0, 1));

        Time.timeScale = 1f;
        //gameInstance.SetMusicVolume(currentVol);
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private IEnumerator DoFade(float start, float end)
    {
        timerCurrent = 0f;
        currentVol = gameInstance.GetMusicVolume();

        while (timerCurrent <= fadeDuration)
        {
            timerCurrent += Time.deltaTime;
            Color c = blackFade.color;
            blackFade.color = new Color(c.r, c.g, c.b, Mathf.Lerp(start, end, smoothCurve.Evaluate(timerCurrent / fadeDuration)));

            gameInstance.SetMusicVolNonPref(Mathf.Lerp(currentVol, 0, smoothCurve.Evaluate(timerCurrent / fadeDuration)));
            yield return skipFrame;
        }
    }
}
