using UnityEngine;
using UnityEngine.EventSystems;

public class CardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public System.Action OnHoverEnter;
    public System.Action OnHoverExit;

    private bool isHovering = false;
    private CardDraggable draggable;

    private void Awake()
    {
        draggable = GetComponent<CardDraggable>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Don't show hover effect while dragging
        if (draggable != null && draggable.IsDragging) return;

        if (!isHovering)
        {
            isHovering = true;
            OnHoverEnter?.Invoke();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isHovering)
        {
            isHovering = false;
            OnHoverExit?.Invoke();
        }
    }

    private void OnDisable()
    {
        // Clear hover when disabled
        if (isHovering)
        {
            isHovering = false;
            OnHoverExit?.Invoke();
        }
    }
}