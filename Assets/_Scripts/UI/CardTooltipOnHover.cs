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

    [Header("Mini Tooltip (Hand)")]
    [SerializeField] private GameObject miniTooltipContainer;
    [SerializeField] private TextMeshProUGUI miniNameText;
    [SerializeField] private TextMeshProUGUI miniTierText;
    [SerializeField] private TextMeshProUGUI miniDamageText;
    [Tooltip("World-space offset from the card for the mini tooltip (hand).")]
    [SerializeField] private Vector3 miniWorldOffset = new Vector3(0.8f, 0f, 0f);

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
        if (miniTooltipContainer != null)
            miniTooltipContainer.SetActive(false);

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
        bool onBoard = draggable != null && draggable.IsOnBoard;
        bool faceUp = cardVisual == null || !cardVisual.IsFaceDown;

        if (onBoard && faceUp)
        {
            // Full tooltip when on board and face-up
            UpdateTooltipText();
            UpdateTooltipPosition();
            SetTooltipVisible(true);
            SetMiniTooltipVisible(false);
        }
        else if (!onBoard)
        {
            // Mini tooltip when in hand
            UpdateMiniTooltipText();
            UpdateMiniTooltipPosition();
            SetMiniTooltipVisible(true);
            SetTooltipVisible(false);
        }
        else
        {
            SetTooltipVisible(false);
            SetMiniTooltipVisible(false);
        }
    }

    private void HandleHoverExit()
    {
        SetTooltipVisible(false);
        SetMiniTooltipVisible(false);
    }

    // Keep tooltip following card while hovering / moving
    private void HandleHoverMove01(Vector2 _)
    {
        if (tooltipContainer != null && tooltipContainer.activeSelf)
            UpdateTooltipPosition();
        if (miniTooltipContainer != null && miniTooltipContainer.activeSelf)
            UpdateMiniTooltipPosition();
    }

    private void SetTooltipVisible(bool visible)
    {
        if (tooltipContainer != null)
            tooltipContainer.SetActive(visible);
    }

    private void SetMiniTooltipVisible(bool visible)
    {
        if (miniTooltipContainer != null)
            miniTooltipContainer.SetActive(visible);
    }

    private void UpdateMiniTooltipText()
    {
        if (cardVisual == null) return;

        var data = cardVisual.CurrentCardData;
        string nameStr = data != null ? data.cardName : "Unknown";
        string tierStr = data != null ? $"Tier {data.tier}" : string.Empty;
        string damageStr = $"Damage: {cardVisual.GetAttackDamage()}";

        if (miniNameText != null) miniNameText.text = nameStr;
        if (miniTierText != null) miniTierText.text = tierStr;
        if (miniDamageText != null) miniDamageText.text = damageStr;
    }

    private void UpdateMiniTooltipPosition()
    {
        if (miniTooltipContainer == null) return;

        RectTransform rt = miniTooltipContainer.transform as RectTransform;
        if (rt == null) return;

        if (mainCam == null)
            mainCam = Camera.main;

        Vector3 worldPos = transform.position + miniWorldOffset;
        Vector3 screenPos = mainCam != null
            ? mainCam.WorldToScreenPoint(worldPos)
            : worldPos;

        rt.position = screenPos;
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
            damageText.text = $"Damage: {cardVisual.GetAttackDamage()}";

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

