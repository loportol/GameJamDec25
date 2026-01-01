using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ThoughtButtonUI : MonoBehaviour
{
    [Header("Wiring")]
    public TextMeshProUGUI buttonText;
    public Button button; // optional, but makes interactable control easy

    private ClipResponse responseData;
    private System.Action<ClipResponse> onSelected;

    // This sets up the button to represent ONE ClipResponse option.
    public void Setup(ClipResponse data, System.Action<ClipResponse> callback)
    {
        responseData = data;
        onSelected = callback;

        // show text
        if (buttonText != null) buttonText.text = data.response;

        // make sure we can toggle interactable later
        if (button == null) button = GetComponent<Button>();
    }

    public void SetInteractable(bool canClick)
    {
        if (button == null) button = GetComponent<Button>();
        button.interactable = canClick;
    }

    // Called by the Unity Button OnClick() event.
    public void OnClick()
    {
        onSelected?.Invoke(responseData);
        Destroy(gameObject); // remove after click so the screen clears naturally
    }
}
