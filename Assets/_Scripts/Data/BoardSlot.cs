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
    [SerializeField] private Color cannotPlaceColor = new Color(1f, 0.3f, 0.3f, 0.5f);
    [SerializeField] private Color blinkColor = new Color(0.5f, 1f, 0.5f, 0.8f);
    
    [Header("Blinking Settings")]
    [SerializeField] private float blinkSpeed = 1.5f; // Blinks per second
    
    private SpriteRenderer spriteRenderer;
    private GameObject occupyingCard;
    private bool isHovered;
    private bool isBlinking;
    private Coroutine blinkCoroutine;

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
        // Player slots start hidden; they only show when a card is in hand
        if (IsPlayerSlot)
        {
            gameObject.SetActive(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isPlayerSlot) return; // can't click opponent slots
        
        if (CardDraggable.SelectedCard != null)
        {
            // check if the selected card can be played
            if (!CardDraggable.SelectedCard.CanBePlayed())
            {
                Debug.Log($"BOARD SLOT || Cannot place card - insufficient resources or wrong phase");
                ShowCannotPlaceFeedback();
                return;
            }

            TryPlaceCard(CardDraggable.SelectedCard.gameObject);
        }
        else if (occupyingCard != null)
        {
            CardDraggable cardDraggable = occupyingCard.GetComponent<CardDraggable>();
            if (cardDraggable != null)
            {
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
                // validation is already done in CardDraggable.OnEndDrag
                TryPlaceCard(droppedObject);
            }
        }
    }

    public bool TryPlaceCard(GameObject card)
    {
        if (card == null) return false;

        CardDraggable cardDraggable = card.GetComponent<CardDraggable>();
        if (cardDraggable == null) return false;

        // CLIENT-SIDE VALIDATION: check if card can be played
        if (!cardDraggable.CanBePlayed())
        {
            Debug.Log($"BOARD SLOT || Cannot place card - validation failed");
            ShowCannotPlaceFeedback();
            return false;
        }

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
            if (IsPlayerSlot)
                cardDraggable.SetOnBoard(true);
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
        if (IsPlayerSlot)
            incomingDraggable.SetOnBoard(true);
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
            CardDraggable tempDraggable = tempCard.GetComponent<CardDraggable>();
            if (tempDraggable != null)
                tempDraggable.SetOnBoard(false);
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
            cardDraggable.SetOnBoard(false);
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

        // Don't override blinking color
        if (isBlinking && blinkCoroutine != null)
        {
            return; // Blinking coroutine handles color
        }

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
    
    public void SetBlinking(bool shouldBlink)
    {
        // Only affect player slots (show/hide + blink)
        if (!IsPlayerSlot) return;
        
        if (shouldBlink)
        {
            // Show slot and start blinking
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            if (!isBlinking)
            {
                isBlinking = true;
                if (blinkCoroutine != null)
                {
                    StopCoroutine(blinkCoroutine);
                }
                blinkCoroutine = StartCoroutine(BlinkCoroutine());
            }
        }
        else
        {
            // Stop blinking and hide slot
            isBlinking = false;
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }
            UpdateVisual();
            gameObject.SetActive(false);
        }
    }
    
    private System.Collections.IEnumerator BlinkCoroutine()
    {
        Color baseColor = IsOccupied ? occupiedColor : emptyColor;
        
        while (isBlinking)
        {
            float elapsed = 0f;
            float halfCycle = 1f / blinkSpeed / 2f; // Half cycle duration
            
            // Fade in to blink color
            while (elapsed < halfCycle && isBlinking)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfCycle;
                spriteRenderer.color = Color.Lerp(baseColor, blinkColor, t);
                yield return null;
            }
            
            elapsed = 0f;
            
            // Fade out from blink color
            while (elapsed < halfCycle && isBlinking)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfCycle;
                spriteRenderer.color = Color.Lerp(blinkColor, baseColor, t);
                yield return null;
            }
        }
        
        // Ensure we restore normal color when stopping
        UpdateVisual();
    }

    private void ShowCannotPlaceFeedback()
    {
        if (spriteRenderer != null)
        {
            StartCoroutine(FlashCannotPlace());
        }
    }

    private System.Collections.IEnumerator FlashCannotPlace()
    {
        Color original = spriteRenderer.color;
        spriteRenderer.color = cannotPlaceColor;
        yield return new WaitForSeconds(0.2f);
        UpdateVisual(); // restore proper color based on state
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

    public void SetSlotProperties(int index, bool playerSlot)
    {
        slotIndex = index;
        isPlayerSlot = playerSlot;
        gameObject.name = $"{(playerSlot ? "Player" : "Opponent")}Slot_{index}";
        Debug.Log($"BOARD SLOT || Set slot properties - Index: {slotIndex}, IsPlayer: {isPlayerSlot}, Name: {gameObject.name}");
    }
}