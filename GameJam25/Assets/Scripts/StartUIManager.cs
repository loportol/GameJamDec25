using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StartUIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private Image blackFade;

    [SerializeField] private float m_fadeDuration = 5f;
    [SerializeField] private float m_stayDuration = 2f;
    [SerializeField] private AnimationCurve m_smoothCurve = new AnimationCurve(new Keyframe[] { new Keyframe(0f, 0f), new Keyframe(1f, 1f) });

    private readonly WaitForSeconds m_skipFrame = new WaitForSeconds(0.01f);
    private float m_timerCurrent;

    [Header("Clips")]
    [SerializeField] private AudioClip doorOpen;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (blackFade != null)
        {
            StartCoroutine(gameOpening());
        }
    }

    // Update is called once per frame
    void Update()
    {
        
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

        //Start the audio
        StartAudio();
    }

    private IEnumerator DoFade(float start, float end)
    {
        m_timerCurrent = 0f;

        while (m_timerCurrent <= m_fadeDuration)
        {
            m_timerCurrent += Time.deltaTime;
            Color c = blackFade.color;
            blackFade.color = new Color(c.r, c.g, c.b, Mathf.Lerp(start, end, m_smoothCurve.Evaluate(m_timerCurrent / m_fadeDuration)));
            yield return m_skipFrame;
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
