using UnityEngine;
using UnityEngine.EventSystems;

public class BoardSlot : MonoBehaviour, IPointerClickHandler, IDropHandler
{
    [Header("Slot Settings")]
    [SerializeField] private int slotIndex; // 0, 1, or 2
    [SerializeField] private bool isPlayerSlot; // true for player, false for opponent
    
    [Header("Visual Feedback")]
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.3f);
    [SerializeField] private Color occupiedColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    [SerializeField] private Color hoverColor = new Color(0.8f, 1f, 0.8f, 0.5f);
    
    private SpriteRenderer spriteRenderer;
    private GameObject occupyingCard;
    private bool isHovered;

    public int SlotIndex => slotIndex;

    public bool IsPlayerSlot 
    { 
        get 
        {
            // check parent zone name - if it's "PlayerBoardZone", it belongs to the local player
            if (transform.parent != null)
            {
                bool isLocalPlayerSlot = transform.parent.name == "PlayerBoardZone";
                if (isLocalPlayerSlot != isPlayerSlot)
                {
                    Debug.Log($"BOARD SLOT || Slot {name} - Serialized isPlayerSlot: {isPlayerSlot}, Actual (by zone): {isLocalPlayerSlot}");
                }
                return isLocalPlayerSlot;
            }
            return isPlayerSlot;
        }
    }

    public bool IsOccupied => occupyingCard != null;
    public GameObject OccupyingCard => occupyingCard;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        
        UpdateVisual();
    }

    private void Start()
    {
        if (BoardManager.Instance != null)
        {
            BoardManager.Instance.RegisterSlot(this);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isPlayerSlot) return; // can't click opponent slots
        
        if (CardDraggable.SelectedCard != null)
        {
            TryPlaceCard(CardDraggable.SelectedCard.gameObject);
        }
        else if (occupyingCard != null)
        {
            CardDraggable cardDraggable = occupyingCard.GetComponent<CardDraggable>();
            if (cardDraggable != null)
            {   
                // nested check for fallback
                if (isPlayerSlot)
                {
                    cardDraggable.SelectCard();
                }
            }
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (!isPlayerSlot) return;
        
        GameObject droppedObject = eventData.pointerDrag;
        if (droppedObject != null)
        {
            CardDraggable cardDraggable = droppedObject.GetComponent<CardDraggable>();
            if (cardDraggable != null)
            {
                TryPlaceCard(droppedObject);
            }
        }
    }

    public bool TryPlaceCard(GameObject card)
    {
        if (card == null) return false;

        CardDraggable cardDraggable = card.GetComponent<CardDraggable>();
        if (cardDraggable == null) return false;

        // if this slot is already occupied, swap cards
        if (IsOccupied)
        {
            SwapCards(card);
            return true;
        }

        // place card in this empty slot
        PlaceCard(card);
        return true;
    }

    public void PlaceCard(GameObject card)
    {
        if (card == null) return;

        occupyingCard = card;

        Transform originalParent = card.transform.parent;

        card.transform.SetParent(transform, false);

        card.transform.localPosition = Vector3.zero;
        card.transform.localRotation = Quaternion.identity;
        card.transform.localScale = Vector3.one;
        
        CardDraggable cardDraggable = card.GetComponent<CardDraggable>();
        if (cardDraggable != null)
        {
            cardDraggable.SetCurrentSlot(this);
            cardDraggable.DeselectCard();
        }

        SpriteRenderer sr = card.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = 0; // Reset to default
        }

        UpdateVisual();
        
        if (BoardManager.Instance != null)
        {
            bool isFromHand = originalParent != null && originalParent.name == "PlayerHandZone";
            BoardManager.Instance.OnCardPlacedInSlot(this, card, isFromHand);
        }

        Debug.Log($"BOARD SLOT || Card placed in {(isPlayerSlot ? "Player" : "Opponent")} slot {slotIndex}");
    }

    private void SwapCards(GameObject incomingCard)
    {
        if (!IsOccupied || incomingCard == null) return;

        CardDraggable incomingDraggable = incomingCard.GetComponent<CardDraggable>();
        CardDraggable occupyingDraggable = occupyingCard.GetComponent<CardDraggable>();
        
        if (incomingDraggable == null || occupyingDraggable == null) return;

        // get slot the incoming card came from
        BoardSlot previousSlot = incomingDraggable.CurrentSlot;

        GameObject tempCard = occupyingCard;
        
        // clear this slot
        occupyingCard = null;
        UpdateVisual();

        occupyingCard = incomingCard;
        incomingCard.transform.SetParent(transform, false);
        incomingCard.transform.localPosition = Vector3.zero;
        incomingCard.transform.localRotation = Quaternion.identity;
        incomingCard.transform.localScale = Vector3.one;
        
        incomingDraggable.SetCurrentSlot(this);
        incomingDraggable.DeselectCard();
        
        UpdateVisual();

        if (BoardManager.Instance != null)
        {
            BoardManager.Instance.OnCardPlacedInSlot(this, incomingCard, shouldNotifyServer: true);
        }
        
        // incoming card came from another slot, put the displaced card there
        if (previousSlot != null && previousSlot != this)
        {
            // Don't notify server for the return placement
            tempCard.transform.SetParent(previousSlot.transform, false);
            tempCard.transform.localPosition = Vector3.zero;
            tempCard.transform.localRotation = Quaternion.identity;
            tempCard.transform.localScale = Vector3.one;
            
            previousSlot.occupyingCard = tempCard;
            occupyingDraggable.SetCurrentSlot(previousSlot);
            previousSlot.UpdateVisual();
            
            if (BoardManager.Instance != null)
            {
                BoardManager.Instance.OnCardPlacedInSlot(previousSlot, tempCard, shouldNotifyServer: false);
            }
        }
        else
        {
            // card came from hand, return displaced card to hand
            if (BoardManager.Instance != null)
            {
                BoardManager.Instance.ReturnCardToHand(tempCard);
            }
        }
        Debug.Log($"BOARD SLOT || Swapped cards in slot {slotIndex}");
    }

    public void RemoveCard(bool notifyManager = true)
    {
        if (!IsOccupied) return;

        GameObject removedCard = occupyingCard;
        occupyingCard = null;
        
        CardDraggable cardDraggable = removedCard.GetComponent<CardDraggable>();
        if (cardDraggable != null)
        {
            cardDraggable.SetCurrentSlot(null);
        }

        UpdateVisual();
        
        if (notifyManager && BoardManager.Instance != null)
        {
            BoardManager.Instance.OnCardRemovedFromSlot(this, removedCard);
        }

        Debug.Log($"BOARD SLOT || Card removed from {(isPlayerSlot ? "Player" : "Opponent")} slot {slotIndex}");
    }

    public void ClearSlot()
    {
        if (IsOccupied)
        {
            Destroy(occupyingCard);
            occupyingCard = null;
            UpdateVisual();
        }
    }

    private void UpdateVisual()
    {
        if (spriteRenderer == null) return;

        if (isHovered && isPlayerSlot && !IsOccupied)
        {
            spriteRenderer.color = hoverColor;
        }
        else if (IsOccupied)
        {
            spriteRenderer.color = occupiedColor;
        }
        else
        {
            spriteRenderer.color = emptyColor;
        }
    }

    public void OnPointerEnter()
    {
        if (!isPlayerSlot) return;
        isHovered = true;
        UpdateVisual();
    }

    public void OnPointerExit()
    {
        isHovered = false;
        UpdateVisual();
    }

    private void OnMouseEnter()
    {
        OnPointerEnter();
    }

    private void OnMouseExit()
    {
        OnPointerExit();
    }

    // FIXED: Made public and removed UNITY_EDITOR conditional so it works at runtime!
    public void SetSlotProperties(int index, bool playerSlot)
    {
        slotIndex = index;
        isPlayerSlot = playerSlot;
        gameObject.name = $"{(playerSlot ? "Player" : "Opponent")}Slot_{index}";
        Debug.Log($"BOARD SLOT || Set slot properties - Index: {slotIndex}, IsPlayer: {isPlayerSlot}, Name: {gameObject.name}");
    }
}