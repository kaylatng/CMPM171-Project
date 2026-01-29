using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Netcode;

public class CardDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("Drag Settings")]
    [SerializeField] private float dragSpeed = 20f;
    [SerializeField] private LayerMask boardSlotLayer;
    
    [Header("Visual Feedback")]
    [SerializeField] private Color selectedColor = new Color(1f, 1f, 0.5f, 1f);
    [SerializeField] private Vector3 selectedScale = new Vector3(1.1f, 1.1f, 1f);
    
    private Vector3 originalPosition;
    private Transform originalParent;
    private Vector3 originalScale;
    private Color originalColor;
    private bool isDragging;
    private bool isSelected;
    private Canvas canvas;
    private SpriteRenderer spriteRenderer;
    private BoardSlot currentSlot;
    
    // static reference to currently selected card (for click-to-place)
    public static CardDraggable SelectedCard { get; private set; }

    public BoardSlot CurrentSlot => currentSlot;
    public bool IsDragging => isDragging;
    public bool IsSelected => isSelected;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        originalScale = transform.localScale;
    }

    private void Start()
    {
        canvas = FindFirstObjectByType<Canvas>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // don't allow clicking if card is being dragged
        if (isDragging) return;

        if (!IsPlayerCard()) return;

        // toggle selection
        if (isSelected)
        {
            DeselectCard();
        }
        else
        {
            SelectCard();
        }
    }

    public void SelectCard()
    {
        if (SelectedCard != null && SelectedCard != this)
        {
            SelectedCard.DeselectCard();
        }

        isSelected = true;
        SelectedCard = this;
        
        if (spriteRenderer != null)
        {
            spriteRenderer.color = selectedColor;
        }
        transform.localScale = selectedScale;

        Debug.Log($"CARD DRAGGABLE || Card selected for placement");
    }

    public void DeselectCard()
    {
        if (!isSelected) return;

        isSelected = false;
        if (SelectedCard == this)
        {
            SelectedCard = null;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
        transform.localScale = originalScale;

        Debug.Log($"CARD DRAGGABLE || Card deselected");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {   
        if (!IsPlayerCard()) 
        {
            Debug.Log($"CARD DRAGGABLE || Drag blocked - not a player card. Parent: {transform.parent?.name}");
            return;
        }

        // deselect if selected
        if (isSelected)
        {
            DeselectCard();
        }

        isDragging = true;
        originalPosition = transform.position;
        originalParent = transform.parent;

        // move card to top of render order while dragging
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = 100;
        }

        Debug.Log($"CARD DRAGGABLE || Started dragging card");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(eventData.position);
        mousePosition.z = 0;

        transform.position = Vector3.Lerp(transform.position, mousePosition, dragSpeed * Time.deltaTime);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            Debug.Log($"CARD DRAGGABLE || OnEndDrag called but isDragging is false");
            return;
        }
        
        isDragging = false;

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = 0;
        }

        Debug.Log($"CARD DRAGGABLE || End drag at screen pos: {eventData.position}");
        BoardSlot targetSlot = GetSlotUnderMouse(eventData.position);

        if (targetSlot != null)
        {
            Debug.Log($"CARD DRAGGABLE || Found slot: {targetSlot.name}, IsPlayerSlot: {targetSlot.IsPlayerSlot}");
            
            if (targetSlot.IsPlayerSlot)
            {
                // valid drop - place card in slot
                if (currentSlot != null)
                {
                    currentSlot.RemoveCard();
                }
                
                targetSlot.TryPlaceCard(gameObject);
            }
            else
            {
                Debug.Log($"CARD DRAGGABLE || Slot is opponent slot, can't place here");
                ReturnToOriginalPosition();
            }
        }
        else
        {
            Debug.Log($"CARD DRAGGABLE || No slot found, returning to hand");
            // invalid drop - return to original position
            ReturnToOriginalPosition();
        }
    }

    private BoardSlot GetSlotUnderMouse(Vector2 screenPosition)
    {
        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(screenPosition);
        worldPosition.z = 0;

        Debug.Log($"CARD DRAGGABLE || Raycast from world pos: {worldPosition}");
        Debug.Log($"CARD DRAGGABLE || Layer mask value: {boardSlotLayer.value}");
        /*
        RaycastHit2D hit;
        if (boardSlotLayer != 0)
        {
            hit = Physics2D.Raycast(worldPosition, Vector2.zero, Mathf.Infinity, boardSlotLayer);
        }
        else
        {
            hit = Physics2D.Raycast(worldPosition, Vector2.zero);
        }

        if (hit.collider != null)
        {
            return hit.collider.GetComponent<BoardSlot>();
        }

        return null;
        */
        RaycastHit2D hit2d;
        if (boardSlotLayer != 0)
        {
            hit2d = Physics2D.Raycast(worldPosition, Vector2.zero, Mathf.Infinity, boardSlotLayer);
        }
        else
        {
            hit2d = Physics2D.Raycast(worldPosition, Vector2.zero);
        }

        if (hit2d.collider != null)
        {
            Debug.Log($"CARD DRAGGABLE || Found slot: {hit2d.collider.name}");
            return hit2d.collider.GetComponent<BoardSlot>();
        }

        Debug.Log($"CARD DRAGGABLE || No slot hit with layer mask");
        return null;
    }

    private void ReturnToOriginalPosition()
    {
        if (currentSlot != null)
        {
            // card was already on board - return to slot
            transform.SetParent(currentSlot.transform);
            transform.localPosition = Vector3.zero;
        }
        else if (originalParent != null)
        {
            // card was in hand - return to hand
            transform.SetParent(originalParent);
            transform.position = originalPosition;
        }

        Debug.Log($"CARD DRAGGABLE || Card returned to original position");
    }

    public void SetCurrentSlot(BoardSlot slot)
    {
        currentSlot = slot;
    }

    public bool CanBePlayed()
    {
        CardVisual cardVisual = GetComponent<CardVisual>();
        if (cardVisual == null) return false;

        // card data
        if (CardManager.Instance != null && CardManager.Instance.GetCardLibrary() != null)
        {
            CardData cardData = CardManager.Instance.GetCardLibrary().GetTierOneAssetFromPool(cardVisual.CardID);
            if (cardData == null) return false;

            // check player has enough mana
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject?.GetComponent<PlayerNetwork>();
                if (localPlayer != null)
                {
                    return localPlayer.GetCurrentMana() >= cardData.manaCost;
                }
            }
        }

        return true; // DEBUG DEFAULT TRUE 
    }

    public void PlayCard()
    {
        CardVisual cardVisual = GetComponent<CardVisual>();
        if (cardVisual == null) return;

        int cardId = cardVisual.CardID;
        int slotIndex = currentSlot != null ? currentSlot.SlotIndex : -1;
        Debug.Log($"CARD DRAGGABLE || Playing card {cardId} to slot {slotIndex}");

        // notify server that this card was played
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject?.GetComponent<PlayerNetwork>();
            if (localPlayer != null)
            {
                localPlayer.PlayCardToSlotServerRpc(cardVisual.CardID, slotIndex);
            }
        }
    }

    private void OnDestroy()
    {
        if (SelectedCard == this)
        {
            SelectedCard = null;
        }
    }

    private bool IsPlayerCard()
    {
        // check if this component is even enabled (opponent cards have this disabled)
        if (!enabled)
        {
            Debug.Log($"CARD DRAGGABLE || IsPlayerCard check - component is disabled");
            return false;
        }

        Transform parent = transform.parent;
        if (parent != null)
        {
            // check if in player hand zone
            if (parent.name == "PlayerHandZone")
            {
                Debug.Log($"CARD DRAGGABLE || IsPlayerCard check - in PlayerHandZone: TRUE");
                return true;
            }
            
            // check if parent is a player slot
            BoardSlot slot = parent.GetComponent<BoardSlot>();
            if (slot != null)
            {
                Debug.Log($"CARD DRAGGABLE || IsPlayerCard check - in BoardSlot, IsPlayerSlot: {slot.IsPlayerSlot}");
                return slot.IsPlayerSlot;
            }

            Debug.Log($"CARD DRAGGABLE || IsPlayerCard check - parent is '{parent.name}': FALSE");
        }
        else
        {
            Debug.Log($"CARD DRAGGABLE || IsPlayerCard check - no parent: FALSE");
        }
        
        return false;
    }
}