using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartUIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private Image blackFade;

    [SerializeField] private float fadeDuration = 5f;
    [SerializeField] private AnimationCurve smoothCurve = new AnimationCurve(new Keyframe[] { new Keyframe(0f, 0f), new Keyframe(1f, 1f) });

    private readonly WaitForSeconds skipFrame = new WaitForSeconds(0.0001f);
    private float timerCurrent;

    [Header("Clips")]
    [SerializeField] private AudioClip doorOpen;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (blackFade != null)
        {
            StartCoroutine(gameOpening());
        }
    }

    private IEnumerator gameOpening()
    {
        //Play clip
        if (UISFXManager.Instance != null)
        {
            UISFXManager.Instance.playClip(doorOpen);
        }

        //Fade the black out
        yield return StartCoroutine(DoFade(1f, 0f));

        //Disable Game Object
        blackFade.color = new Color(0, 0, 0, 0);

        //Start the audio
        StartAudio();
    }

    private IEnumerator DoFade(float start, float end)
    {
        timerCurrent = 0f;

        while (timerCurrent <= fadeDuration)
        {
            timerCurrent += Time.deltaTime;
            Color c = blackFade.color;
            blackFade.color = new Color(c.r, c.g, c.b, Mathf.Lerp(start, end, smoothCurve.Evaluate(timerCurrent / fadeDuration)));
            yield return skipFrame;
        }
    }

    private void StartAudio()
    {
        if (AudioClipManager.Instance != null)
        {
            AudioClipManager.Instance.PlayDialogue(); ;
        }
    }
}
