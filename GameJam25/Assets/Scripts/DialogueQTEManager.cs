using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.Events;

public class DialogueQTEManager : MonoBehaviour
{
    [Header("Button Spawning")]
    public GameObject thoughtButtonPrefab;
    public RectTransform canvasRect;

    [Header("Timer")]
    [SerializeField] private Slider timerSlider;
    [SerializeField] private float responseTime = 5f;

    private bool responded = false;

    private void Start()
    {
        AudioClipManager.Instance.dialogueHasEnded.AddListener(() =>
        {
            timerSlider.gameObject.SetActive(true);
            StartCoroutine(ResponseTimer());
        });

        timerSlider.maxValue = responseTime;
        timerSlider.value = responseTime;
        timerSlider.gameObject.SetActive(false);
    }

    public void StartDialogue(List<string> words, List<string> correctWords)
    {
        responded = false;
        SpawnThoughts(words, correctWords);
        //StartCoroutine(ResponseTimer());
    }

    void SpawnThoughts(List<string> words, List<string> correctWords)
    {
        foreach (string word in words)
        {
            GameObject button = Instantiate(thoughtButtonPrefab, canvasRect);

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition = GetRandomPosition();

            bool isCorrect = correctWords.Contains(word);

            button.GetComponent<ThoughtButtonUI>()
                .Setup(word, isCorrect, OnResponseSelected);
        }
    }

    Vector2 GetRandomPosition()
    {
        float x = Random.Range(-canvasRect.rect.width / 2 + 100,
            canvasRect.rect.width / 2 - 100);
        float y = Random.Range(-canvasRect.rect.height / 2 + 50,
            canvasRect.rect.height / 2 - 50);
        return new Vector2(x, y);
    }

    private IEnumerator ResponseTimer()
    {
        Debug.Log("Started timer");
        float timeRemaining = responseTime;
        timerSlider.value = responseTime;

        while (timeRemaining > 0f)
        {
            timeRemaining -= Time.deltaTime;
            timerSlider.value = timeRemaining;

            yield return null;
        }

        timerSlider.value = 0f;
        timerSlider.gameObject.SetActive(false);

        if (!responded)
        {
            Debug.Log("FAILED: No response in time");
            ClearButtons();
            AudioClipManager.Instance.PlayDialogue();   
        } /*else
        {
            AudioClipManager.PlayNextDialogue();
        }*/
        // set up once ClipResponse is integrated into the buttons
    }

    void OnResponseSelected(bool correct)
    {
        responded = true;

        if (correct)
            Debug.Log("CORRECT RESPONSE");
        else
            Debug.Log("WRONG RESPONSE");

        ClearButtons();
    }

    void ClearButtons()
    {
        foreach (ThoughtButtonUI button in FindObjectsOfType<ThoughtButtonUI>())
        {
            Destroy(button.gameObject);
        }
    }
}
