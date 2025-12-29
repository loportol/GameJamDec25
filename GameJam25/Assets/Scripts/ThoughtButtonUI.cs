using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ThoughtButtonUI : MonoBehaviour
{
    public TextMeshProUGUI buttonText;

    private bool isCorrect;
    private System.Action<bool> onSelected;

    public void Setup(string text, bool correct, System.Action<bool> callback)
    {
        buttonText.text = text;
        isCorrect = correct;
        onSelected = callback;
    }

    public void OnClick()
    {
        onSelected?.Invoke(isCorrect);
        Destroy(gameObject); // Remove button after selection
    }
}

