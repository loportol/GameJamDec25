using UnityEngine;
using UnityEngine.UI;

public class MomPortraitRoutes : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image momImage;

    [Header("Route Sprites")]
    [SerializeField] private Sprite timidSprite;
    [SerializeField] private Sprite focusedSprite;
    [SerializeField] private Sprite combativeSprite;

    private void Awake()
    {
        if (momImage == null)
        {
            momImage = GetComponent<Image>();
        }
    }

    // call this whenever the player's route changes
    public void SetRouteSprite(ChoiceType route)
    {
        if (momImage == null) return;

        switch (route)
        {
            case ChoiceType.Combative:
                momImage.sprite = combativeSprite;
                break;

            case ChoiceType.Focused:
                momImage.sprite = focusedSprite;
                break;

            default:
                momImage.sprite = timidSprite;
                break;
        }
    }
}
