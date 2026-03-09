using UnityEngine;
using UnityEngine.EventSystems;

public class CardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    public System.Action OnHoverEnter;
    public System.Action OnHoverExit;

    public System.Action<Vector2> OnHoverMove01;

    private bool isHovering = false;
    private CardDraggable draggable;

    private SpriteRenderer spriteRenderer;
    private Camera cam;
    private BoxCollider2D box;

    private void Awake()
    {
        draggable = GetComponent<CardDraggable>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        cam = Camera.main;
        box = GetComponent<BoxCollider2D>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Don't show hover effect while dragging
        if (draggable != null && draggable.IsDragging) return;

        if (!isHovering)
        {
            isHovering = true;
            var cardVisual = GetComponent<CardVisual>();
            if (cardVisual != null) cardVisual.SetHovered(true);
            OnHoverEnter?.Invoke();
        }
        
        EmitMove(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isHovering)
        {
            isHovering = false;
            var cardVisual = GetComponent<CardVisual>();
            if (cardVisual != null) cardVisual.SetHovered(false);
            OnHoverExit?.Invoke();
        }
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (!isHovering) return;
        if (draggable != null && draggable.IsDragging) return;

        EmitMove(eventData);
    }

    private void EmitMove(PointerEventData eventData)
    {
        if (box == null || cam == null) return;

        Vector3 world = cam.ScreenToWorldPoint(eventData.position);
        world.z = transform.position.z;

        Vector3 local = transform.InverseTransformPoint(world);

        Vector2 half = box.size * 0.5f;
        Vector2 offset = box.offset;

        float x01 = Mathf.InverseLerp(offset.x - half.x, offset.x + half.x, local.x);
        float y01 = Mathf.InverseLerp(offset.y - half.y, offset.y + half.y, local.y);

        OnHoverMove01?.Invoke(new Vector2(x01, y01));
    }



    private void OnDisable()
    {
        // Clear hover when disabled
        if (isHovering)
        {
            isHovering = false;
            var cardVisual = GetComponent<CardVisual>();
            if (cardVisual != null) cardVisual.SetHovered(false);
            OnHoverExit?.Invoke();
        }
    }
}