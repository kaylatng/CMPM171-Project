using UnityEngine;
using TMPro;

/// <summary>
/// Shows a screen-space tooltip to the right of a face-up card when hovered on the board.
/// Hook this up on the same GameObject as CardHoverEffect / CardVisual.
/// </summary>
public class CardTooltipOnHover : MonoBehaviour
{
    [Header("Hooks")]
    [SerializeField] private CardHoverEffect hover;
    [SerializeField] private CardDraggable draggable;
    [SerializeField] private CardVisual cardVisual;

    [Header("Tooltip (Screen Space)")]
    [SerializeField] private GameObject tooltipContainer;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI tierText;
    [SerializeField] private TextMeshProUGUI damageText;
    [SerializeField] private TextMeshProUGUI chargesText;

    [Tooltip("World-space offset from the card to where the tooltip should appear (to the right).")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0.8f, 0f, 0f);

    private Camera mainCam;

    private void Awake()
    {
        if (!hover) hover = GetComponent<CardHoverEffect>();
        if (!draggable) draggable = GetComponent<CardDraggable>();
        if (!cardVisual) cardVisual = GetComponent<CardVisual>();

        if (tooltipContainer != null)
            tooltipContainer.SetActive(false);

        mainCam = Camera.main;
    }

    private void OnEnable()
    {
        if (hover == null) return;
        hover.OnHoverEnter += HandleHoverEnter;
        hover.OnHoverExit += HandleHoverExit;
        hover.OnHoverMove01 += HandleHoverMove01;
    }

    private void OnDisable()
    {
        if (hover == null) return;
        hover.OnHoverEnter -= HandleHoverEnter;
        hover.OnHoverExit -= HandleHoverExit;
        hover.OnHoverMove01 -= HandleHoverMove01;
    }

    private void HandleHoverEnter()
    {
        // Only show when on board and face-up
        if (draggable != null && !draggable.IsOnBoard) return;
        if (cardVisual != null && cardVisual.IsFaceDown) return;

        UpdateTooltipText();
        UpdateTooltipPosition();
        SetTooltipVisible(true);
    }

    private void HandleHoverExit()
    {
        SetTooltipVisible(false);
    }

    // Keep tooltip following card while hovering / moving
    private void HandleHoverMove01(Vector2 _)
    {
        if (tooltipContainer != null && tooltipContainer.activeSelf)
        {
            UpdateTooltipPosition();
        }
    }

    private void SetTooltipVisible(bool visible)
    {
        if (tooltipContainer != null)
            tooltipContainer.SetActive(visible);
    }

    private void UpdateTooltipText()
    {
        if (cardVisual == null) return;

        var data = cardVisual.CurrentCardData;

        if (nameText != null)
            nameText.text = data != null ? data.cardName : "Unknown";

        if (tierText != null)
            tierText.text = data != null ? $"Tier {data.tier}" : string.Empty;

        if (damageText != null)
            damageText.text = $"DMG: {cardVisual.GetAttackDamage()}";

        if (chargesText != null)
            chargesText.text = $"Uses: {cardVisual.CurrentCharges}";
    }

    private void UpdateTooltipPosition()
    {
        if (tooltipContainer == null) return;

        RectTransform rt = tooltipContainer.transform as RectTransform;
        if (rt == null) return;

        if (mainCam == null)
            mainCam = Camera.main;

        // World position to the right of the card
        Vector3 worldPos = transform.position + worldOffset;

        // Convert to screen position for the Screen Space canvas
        Vector3 screenPos = mainCam != null
            ? mainCam.WorldToScreenPoint(worldPos)
            : worldPos;

        rt.position = screenPos;
    }
}

