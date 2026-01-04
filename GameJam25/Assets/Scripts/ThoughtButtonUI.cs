using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ThoughtButtonUI : MonoBehaviour
{
    public TextMeshProUGUI buttonText;

    private ClipResponse clipResponse;
    private System.Action<ClipResponse> onSelected;
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    public void Setup(ClipResponse response, System.Action<ClipResponse> callback)
    {
        clipResponse = response;
        onSelected = callback;

        if (buttonText != null)
            buttonText.text = response.response;
    }

    public void SetInteractable(bool canClick)
    {
        if (button != null)
            button.interactable = canClick;
    }

    public void OnClick()
    {
        // if somehow interactable wasn't set correctly, block anyway
        if (button != null && !button.interactable)
            return;

        onSelected?.Invoke(clipResponse);
        Destroy(gameObject);
    }
}
