using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach this to the grey AP info panel. When the player taps/clicks anywhere
/// on the panel, it notifies the UITutorialController to advance the tutorial.
/// </summary>
public class TutorialApOverlay : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (UITutorialController.Instance != null)
        {
            UITutorialController.Instance.NotifyApOverlayClicked();
        }
    }
}

