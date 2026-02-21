using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class BoardSlot : MonoBehaviour, IPointerClickHandler, IDropHandler
{
    [Header("Slot Settings")]
    [SerializeField] private int slotIndex;
    [SerializeField] private bool isPlayerSlot;

    [Header("Visual Feedback")]
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.3f);
    [SerializeField] private Color occupiedColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    [SerializeField] private Color hoverColor = new Color(0.8f, 1f, 0.8f, 0.5f);
    [SerializeField] private Color cannotPlaceColor = new Color(1f, 0.3f, 0.3f, 0.5f);
    [SerializeField] private Color blinkColor = new Color(0.5f, 1f, 0.5f, 0.8f);

    [Header("Blinking Settings")]
    [SerializeField] private float blinkSpeed = 1.5f;

    private SpriteRenderer spriteRenderer;
    private GameObject occupyingCard;
    private bool isHovered;
    private bool isBlinking;
    private Coroutine blinkCoroutine;

 
    private bool guideVisible = false;

    public int SlotIndex => slotIndex;

    public bool IsOccupied => occupyingCard != null;
    public GameObject OccupyingCard => occupyingCard;

public bool IsPlayerSlot
{
    get
    {
     
        Transform t = transform;
        while (t != null)
        {
            if (t.name.StartsWith("PlayerBoardZone")) return true;
            if (t.name.StartsWith("OpponentBoardZone")) return false;
            t = t.parent;
        }

        return isPlayerSlot;
    }
}

private void Awake()
{
    spriteRenderer = GetComponent<SpriteRenderer>();
    if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

    guideVisible = !IsPlayerSlot;

    ApplyRendererVisibility();
    UpdateVisual();
}
    private void Start()
    {
        if (BoardManager.Instance != null)
        {
            BoardManager.Instance.RegisterSlot(this);
        }

        if (IsPlayerSlot)
        {
            guideVisible = true;
            ApplyRendererVisibility();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsPlayerSlot) return;

        if (CardDraggable.SelectedCard != null)
        {
            if (!CardDraggable.SelectedCard.CanBePlayed())
            {
                ShowCannotPlaceFeedback();
                return;
            }

            TryPlaceCard(CardDraggable.SelectedCard.gameObject);
        }
        else if (occupyingCard != null)
        {
            CardDraggable cd = occupyingCard.GetComponent<CardDraggable>();
            if (cd != null) cd.SelectCard();
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (!IsPlayerSlot) return;

        GameObject droppedObject = eventData.pointerDrag;
        if (droppedObject != null)
        {
            CardDraggable cd = droppedObject.GetComponent<CardDraggable>();
            if (cd != null)
            {
                TryPlaceCard(droppedObject);
            }
        }
    }

    public bool TryPlaceCard(GameObject card)
    {
        if (card == null) return false;

        CardDraggable cd = card.GetComponent<CardDraggable>();
        if (cd == null) return false;

        if (!cd.CanBePlayed())
        {
            ShowCannotPlaceFeedback();
            return false;
        }

        if (IsOccupied)
        {
            SwapCards(card);
            return true;
        }

        PlaceCard(card);
        return true;
    }

    public void PlaceCard(GameObject card)
    {
        if (card == null) return;

        Transform originalParent = card.transform.parent;

        occupyingCard = card;

        // make sure slot stays active & card doesn't get hidden
    
        card.transform.SetParent(transform, false);
        card.transform.localPosition = Vector3.zero;
        card.transform.localRotation = Quaternion.identity;
        card.transform.localScale = Vector3.one;

        CardDraggable cd = card.GetComponent<CardDraggable>();
        if (cd != null)
        {
            cd.SetCurrentSlot(this);
            cd.DeselectCard();
            cd.SetOnBoard(true);
        }

        
        guideVisible = true; // keep it visible under card
        ApplyRendererVisibility();
        UpdateVisual();

        if (BoardManager.Instance != null)
        {
            bool isFromHand = originalParent != null && originalParent.name == "PlayerHandZone";
            BoardManager.Instance.OnCardPlacedInSlot(this, card, isFromHand);
        }
    }

    private void SwapCards(GameObject incomingCard)
    {
        if (!IsOccupied || incomingCard == null) return;

        CardDraggable incoming = incomingCard.GetComponent<CardDraggable>();
        CardDraggable occupying = occupyingCard.GetComponent<CardDraggable>();
        if (incoming == null || occupying == null) return;

        BoardSlot previousSlot = incoming.CurrentSlot;

        GameObject displaced = occupyingCard;

        // Clear this slot
        occupyingCard = null;
        UpdateVisual();

        // Put incoming here
        occupyingCard = incomingCard;
        incomingCard.transform.SetParent(transform, false);
        incomingCard.transform.localPosition = Vector3.zero;
        incomingCard.transform.localRotation = Quaternion.identity;
        incomingCard.transform.localScale = Vector3.one;

        incoming.SetCurrentSlot(this);
        incoming.DeselectCard();
        incoming.SetOnBoard(true);

        UpdateVisual();

        if (BoardManager.Instance != null)
        {
            BoardManager.Instance.OnCardPlacedInSlot(this, incomingCard, shouldNotifyServer: true);
        }

        // Put displaced back
        if (previousSlot != null && previousSlot != this)
        {
            displaced.transform.SetParent(previousSlot.transform, false);
            displaced.transform.localPosition = Vector3.zero;
            displaced.transform.localRotation = Quaternion.identity;
            displaced.transform.localScale = Vector3.one;

            previousSlot.occupyingCard = displaced;
            occupying.SetCurrentSlot(previousSlot);
            occupying.SetOnBoard(true);
            previousSlot.UpdateVisual();

            if (BoardManager.Instance != null)
            {
                BoardManager.Instance.OnCardPlacedInSlot(previousSlot, displaced, shouldNotifyServer: false);
            }
        }
        else
        {
            if (BoardManager.Instance != null)
            {
                BoardManager.Instance.ReturnCardToHand(displaced);
            }
        }
    }

    public void RemoveCard(bool notifyManager = true)
    {
        if (!IsOccupied) return;

        GameObject removedCard = occupyingCard;
        occupyingCard = null;

        CardDraggable cd = removedCard.GetComponent<CardDraggable>();
        if (cd != null)
        {
            cd.SetCurrentSlot(null);
            cd.SetOnBoard(false);
        }

        UpdateVisual();

        if (notifyManager && BoardManager.Instance != null)
        {
            BoardManager.Instance.OnCardRemovedFromSlot(this, removedCard);
        }
    }

    private void UpdateVisual()
    {
        if (spriteRenderer == null) return;
        if (isBlinking && blinkCoroutine != null) return;

      
        if (!guideVisible)
        {
            ApplyRendererVisibility();
            return;
        }

        if (isHovered && IsPlayerSlot && !IsOccupied) spriteRenderer.color = hoverColor;
        else if (IsOccupied) spriteRenderer.color = occupiedColor;
        else spriteRenderer.color = emptyColor;

        ApplyRendererVisibility();
    }

    private void ApplyRendererVisibility()
    {
        if (spriteRenderer == null) return;

        spriteRenderer.enabled = guideVisible;
    }

    public void SetBlinking(bool shouldBlink)
    {
        if (!IsPlayerSlot) return;

        // If a card is already placed in this slot, stop blinking 
        if (IsOccupied)
        {
            isBlinking = false;
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }
            guideVisible = false; 
            ApplyRendererVisibility();
            return;
        }

        if (shouldBlink)
        {
            guideVisible = true;
            ApplyRendererVisibility();

            if (!isBlinking)
            {
                isBlinking = true;
                if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
                blinkCoroutine = StartCoroutine(BlinkCoroutine());
            }
        }
        else
        {
            isBlinking = false;
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }

            
            guideVisible = false;
            ApplyRendererVisibility();
        }

        UpdateVisual();
    }

    private IEnumerator BlinkCoroutine()
    {
        Color baseColor = IsOccupied ? occupiedColor : emptyColor;

        while (isBlinking)
        {
            float elapsed = 0f;
            float halfCycle = 1f / blinkSpeed / 2f;

            while (elapsed < halfCycle && isBlinking)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfCycle;
                spriteRenderer.color = Color.Lerp(baseColor, blinkColor, t);
                yield return null;
            }

            elapsed = 0f;

            while (elapsed < halfCycle && isBlinking)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfCycle;
                spriteRenderer.color = Color.Lerp(blinkColor, baseColor, t);
                yield return null;
            }
        }

        UpdateVisual();
    }

    private void ShowCannotPlaceFeedback()
    {
        if (spriteRenderer != null)
        {
            guideVisible = true;
            ApplyRendererVisibility();
            StartCoroutine(FlashCannotPlace());
        }
    }

    private IEnumerator FlashCannotPlace()
    {
        spriteRenderer.color = cannotPlaceColor;
        yield return new WaitForSeconds(0.2f);
        UpdateVisual();
    }

    public void OnPointerEnter()
    {
        if (!IsPlayerSlot) return;
        isHovered = true;
        UpdateVisual();
    }

    public void OnPointerExit()
    {
        isHovered = false;
        UpdateVisual();
    }

    private void OnMouseEnter() => OnPointerEnter();
    private void OnMouseExit() => OnPointerExit();
}