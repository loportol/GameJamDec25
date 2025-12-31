using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DialogueQTEManager : MonoBehaviour
{
    public GameObject thoughtButtonPrefab;
    public RectTransform canvasRect;

    public float responseTime = 3f;

    private bool responded = false;

    public void StartDialogue(List<string> words, List<string> correctWords)
    {
        responded = false;
        SpawnThoughts(words, correctWords);
        StartCoroutine(ResponseTimer());
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

    IEnumerator ResponseTimer()
    {
        yield return new WaitForSeconds(responseTime);

        if (!responded)
        {
            Debug.Log("FAILED: No response in time");
            ClearButtons();
        }
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
