using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Netcode;

public class CardDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("Debug")]
    [SerializeField] private bool skipNetworkChecks = false;
    
    [Header("Visual Feedback")]
    [SerializeField] private Color selectedColor = new Color(1f, 1f, 0.5f, 1f);
    [SerializeField] private Color cannotPlayColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private Vector3 selectedScale = new Vector3(1.2f, 1.2f, 1f);
    
    [Header("Pivot Settings")]
    [SerializeField] private float maxTiltAngle = 10f;
    [SerializeField] private float tiltSpeed = 15f;
    [SerializeField] private bool enablePivot = true;

    // Core state
    private Vector3 originalPosition;
    private Transform originalParent;
    private Vector3 originalScale;
    private Color originalColor;
    private Quaternion originalRotation;
    private bool isDragging;
    private bool isSelected;
    private bool isOnBoard;
    private bool isAttacking; // Toggle attack intent during Planning; committed when Ready
    
    // Dragging state
    private Vector3 dragOffset;
    private float currentTilt = 0f;
    private Vector3 lastDragPosition;
    private Camera mainCamera;
    
    private SpriteRenderer cardBackgroundRenderer;
    private CardVisual cardVisual;
    private BoardSlot currentSlot;
    
    public static CardDraggable SelectedCard { get; private set; }

    public bool IsDragging => isDragging;
    public bool IsSelected => isSelected;
    public bool IsOnBoard => isOnBoard;
    public bool IsAttacking => isAttacking;
    public BoardSlot CurrentSlot => currentSlot;

    private void Awake()
    {
        cardVisual = GetComponent<CardVisual>();
        cardBackgroundRenderer = GetComponent<SpriteRenderer>();
        mainCamera = Camera.main;
        
        if (cardBackgroundRenderer != null)
        {
            originalColor = cardBackgroundRenderer.color;
        }
        originalScale = transform.localScale;
        originalRotation = transform.localRotation;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDragging) return;
        if (!IsPlayerCard()) return;

        // If we've already clicked Ready this turn, ignore any further board interactions.
        if (!skipNetworkChecks &&
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsClient)
        {
            var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject?.GetComponent<PlayerNetwork>();
            if (localPlayer != null && localPlayer.IsPlayerReady())
            {
                Debug.Log("CARD DRAGGABLE || Click blocked - player is Ready");
                return;
            }
        }

        // Toggle Attack (Planning Phase only): first tap = set attack intent + tilt, second tap = undo
        if (isOnBoard && GameManager.Instance != null && GameManager.Instance.CanAttack())
        {
            // Inform tutorial that the player tapped a board card to attack.
            if (UITutorialController.Instance != null)
            {
                UITutorialController.Instance.NotifyBoardCardTappedForAttack();
            }

            if (isAttacking)
            {
                SetAttackIntent(false);
                return;
            }
            if (CanToggleAttackOn())
            {
                SetAttackIntent(true);
                return;
            }
            // Can't turn attack on (insufficient resources or charges) - optional feedback
            if (IsOnBoard && GetAttackSlotIndex() >= 0)
                ShowCannotPlayFeedback();
            return;
        }

        // Card in hand: placement is drag-and-drop only; no tap-to-select-slot
    }

    public void SelectCard()
    {
        if (SelectedCard != null && SelectedCard != this)
        {
            SelectedCard.DeselectCard();
        }

        isSelected = true;
        SelectedCard = this;
        
        if (cardBackgroundRenderer != null)
        {
            cardBackgroundRenderer.color = selectedColor;
        }

        Debug.Log($"CARD DRAGGABLE || Card selected");
    }

    public void DeselectCard()
    {
        if (!isSelected) return;

        isSelected = false;
        if (SelectedCard == this)
        {
            SelectedCard = null;
        }

        if (cardBackgroundRenderer != null)
        {
            cardBackgroundRenderer.color = originalColor;
        }

        Debug.Log($"CARD DRAGGABLE || Card deselected");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {   
        if (!IsPlayerCard()) 
        {
            Debug.Log($"CARD DRAGGABLE || Drag blocked - not a player card");
            return;
        }

        // If we've already clicked Ready this turn, completely block picking up cards from hand
        // and play the same feedback as a "no AP" attempt. This must apply even when skipNetworkChecks is true.
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsClient)
        {
            var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject?.GetComponent<PlayerNetwork>();
            if (localPlayer != null && localPlayer.IsPlayerReady())
            {
                Debug.Log("CARD DRAGGABLE || Drag blocked - player is Ready");
                ShowCannotPlayFeedback();
                if (GameManagerUI.Instance != null)
                {
                    GameManagerUI.Instance.PlayNoActionPointsFeedback();
                }
                return;
            }
        }

        if (!CanBePlayed())
        {
            ShowCannotPlayFeedback();
            return;
        }

        if (isSelected)
        {
            DeselectCard();
        }

        // Start dragging
        isDragging = true;
        originalPosition = transform.position;
        originalParent = transform.parent;
        
        // Calculate offset between mouse and card center
        Vector3 mouseWorldPos = GetMouseWorldPosition(eventData);
        dragOffset = transform.position - mouseWorldPos;
        lastDragPosition = transform.position;
        
        // Remember board state
        if (BoardManager.Instance != null)
        {
            isOnBoard = BoardManager.Instance.IsCardOnBoard(gameObject, true);
        }

        // CRITICAL: Unparent from hand zone so CardManager doesn't reposition it
        Transform parentBeforeDrag = originalParent;
        transform.SetParent(null);
        
        // Immediately rearrange remaining cards in hand to fill the gap
        if (CardManager.Instance != null && parentBeforeDrag != null && !isOnBoard)
        {
            // This will trigger the hand to readjust and close the gap
            StartCoroutine(RearrangeHandAfterFrame(parentBeforeDrag));
        }

        if (cardBackgroundRenderer != null)
        {
            cardBackgroundRenderer.sortingOrder = 200;
        }

        // Slightly enlarge while dragging
        transform.localScale = originalScale * 1.15f;

        Debug.Log($"CARD DRAGGABLE || Started dragging");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        // Get mouse position in world space
        Vector3 targetPosition = GetMouseWorldPosition(eventData) + dragOffset;
        targetPosition.z = 0;

        // INSTANT position update (no lerp - this prevents glitching)
        transform.position = targetPosition;
        
        // Calculate tilt based on movement velocity
        if (enablePivot)
        {
            Vector3 velocity = transform.position - lastDragPosition;
            
            // Calculate target tilt based on horizontal movement
            float targetTilt = 0f;
            if (velocity.magnitude > 0.001f)
            {
                // Moving right = tilt right (negative angle)
                // Moving left = tilt left (positive angle)
                targetTilt = Mathf.Clamp(-velocity.x * 300f, -maxTiltAngle, maxTiltAngle);
            }
            
            // Smooth tilt interpolation
            currentTilt = Mathf.Lerp(currentTilt, targetTilt, tiltSpeed * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0, 0, currentTilt);
        }
        
        lastDragPosition = transform.position;

        // Show visual feedback for drop zones
        if (BoardManager.Instance != null)
        {
            if (BoardManager.Instance.IsPositionOverBoard(transform.position, out bool isPlayerBoard))
            {
                if (isPlayerBoard)
                {
                    if (BoardManager.Instance.CanPlaceCardOnBoard(true) || isOnBoard)
                    {
                        BoardManager.Instance.ShowValidDropFeedback(true);
                    }
                    else
                    {
                        BoardManager.Instance.ShowInvalidDropFeedback(true);
                    }
                }
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }
        
        isDragging = false;
        
        // Reset visual properties
        transform.localScale = originalScale;
        transform.localRotation = originalRotation;
        currentTilt = 0f;

        if (cardBackgroundRenderer != null)
        {
            cardBackgroundRenderer.sortingOrder = 0;
        }

        // Use current position for drop check
        Vector3 dropPosition = transform.position;
        dropPosition.z = 0;

        Debug.Log($"CARD DRAGGABLE || Drop position: {dropPosition}");

        // Check if dropped on board
        if (BoardManager.Instance != null)
        {
            if (BoardManager.Instance.IsPositionOverBoard(dropPosition, out bool isPlayerBoard))
            {
                Debug.Log($"CARD DRAGGABLE || Over board zone: {(isPlayerBoard ? "Player" : "Opponent")}");
                
                if (isPlayerBoard)
                {
                    if (TryPlaceOnBoard())
                    {
                        Debug.Log("CARD DRAGGABLE || Card placed on board successfully");
                        return;
                    }
                    else
                    {
                        Debug.Log("CARD DRAGGABLE || Failed to place on board, returning to hand");
                    }
                }
                else
                {
                    Debug.Log("CARD DRAGGABLE || Cannot place on opponent board");
                }
            }
            else
            {
                Debug.Log("CARD DRAGGABLE || Not over any board zone");
            }
        }

        // Return to original position
        ReturnToOriginalPosition();
    }

    private Vector3 GetMouseWorldPosition(PointerEventData eventData)
    {
        if (mainCamera == null) mainCamera = Camera.main;
        
        Vector3 mousePos = eventData.position;
        mousePos.z = Mathf.Abs(mainCamera.transform.position.z);
        return mainCamera.ScreenToWorldPoint(mousePos);
    }

    private bool TryPlaceOnBoard()
    {
        if (BoardManager.Instance == null) return false;

        // Remember whether this card started in the player hand before placing.
        bool fromPlayerHand = originalParent != null && originalParent.name == "PlayerHandZone";

        if (!CanBePlayed())
        {
            ShowCannotPlayFeedback();
            return false;
        }

        bool placed = BoardManager.Instance.TryPlaceCard(gameObject, true);
        
        if (placed)
        {
            isOnBoard = true;
            
            if (CardManager.Instance != null)
            {
                CardManager.Instance.RemoveCardFromHand(gameObject);
            }

            // Inform tutorial (if active) that a card from hand has been placed on the player board.
            if (UITutorialController.Instance != null)
            {
                UITutorialController.Instance.NotifyCardPlacedOnPlayerBoardFromHand(gameObject, fromPlayerHand);
            }
        }

        return placed;
    }

    private void ReturnToOriginalPosition()
    {
        if (isOnBoard && BoardManager.Instance != null)
        {
            // Was on board, return to board
            BoardManager.Instance.TryPlaceCard(gameObject, true);
        }
        else if (originalParent != null)
        {
            // Return to hand
            transform.SetParent(originalParent);
            
            // Trigger hand rearrangement to include this card again
            if (CardManager.Instance != null)
            {
                CardManager.Instance.ArrangeCardsInHand(originalParent);
            }
        }

        Debug.Log($"CARD DRAGGABLE || Card returned to original position");
    }

    public bool CanBePlayed()
    {
        // When skipNetworkChecks is true (editor/testing), still enforce AP if we have a live player,
        // but skip phase/ownership checks so cards remain draggable in offline tests.
        if (skipNetworkChecks)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                var lp = NetworkManager.Singleton.LocalClient?.PlayerObject?.GetComponent<PlayerNetwork>();
                if (lp != null)
                {
                    // Even in skipNetworkChecks mode, once Ready is pressed this card should not be playable.
                    if (lp.IsPlayerReady())
                    {
                        Debug.Log("CARD DRAGGABLE || Cannot play (skipNetworkChecks) - player is Ready");
                        return false;
                    }

                    int pendingAttacksTest = BoardManager.Instance != null ? BoardManager.Instance.GetLocalPendingAttackCount() : 0;
                    int effectiveApTest = lp.GetCurrentActionPoints() - pendingAttacksTest;
                    if (effectiveApTest <= 0)
                    {
                        Debug.Log("CARD DRAGGABLE || Cannot play (skipNetworkChecks) - no effective AP remaining");
                        if (GameManagerUI.Instance != null)
                        {
                            GameManagerUI.Instance.PlayNoActionPointsFeedback();
                        }
                        return false;
                    }
                }
            }
            return true;
        }

        if (GameManager.Instance == null || !GameManager.Instance.CanPlayCards())
        {
            Debug.Log($"CARD DRAGGABLE || Cannot play - not in Planning phase");
            return false;
        }

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            return false;
        }

        var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject?.GetComponent<PlayerNetwork>();
        if (localPlayer == null)
        {
            return false;
        }

        // Once the player has clicked Ready, they can no longer play/move cards this turn.
        if (localPlayer.IsPlayerReady())
        {
            Debug.Log("CARD DRAGGABLE || Cannot play - player is Ready");
            return false;
        }

        // Effective AP = current AP minus any pending attack intents (to match UI display).
        int pendingAttacks = BoardManager.Instance != null ? BoardManager.Instance.GetLocalPendingAttackCount() : 0;
        int currentAp = localPlayer.GetCurrentActionPoints();
        int effectiveAP = currentAp - pendingAttacks;

        if (effectiveAP <= 0)
        {
            Debug.Log($"CARD DRAGGABLE || Cannot play - no effective AP remaining");
            if (GameManagerUI.Instance != null)
            {
                GameManagerUI.Instance.PlayNoActionPointsFeedback();
            }
            return false;
        }

        if (cardVisual == null) return false;

        // Mana is no longer required to play cards; as long as AP and phase checks pass, the card can be played.
        if (CardManager.Instance != null && CardManager.Instance.GetCardLibrary() != null)
        {
            CardData cardData = CardManager.Instance.GetCardLibrary().GetTierOneAssetFromPool(cardVisual.CardID);
            if (cardData == null) return false;
        }

        return true;
    }

    private void ShowCannotPlayFeedback()
    {
        if (cardBackgroundRenderer != null)
        {
            StartCoroutine(FlashCannotPlay());
        }
    }

    private System.Collections.IEnumerator FlashCannotPlay()
    {
        Color original = cardBackgroundRenderer.color;
        cardBackgroundRenderer.color = cannotPlayColor;
        
        Vector3 originalPos = transform.localPosition;
        float shakeAmount = 0.1f;
        float shakeDuration = 0.2f;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float x = originalPos.x + Random.Range(-shakeAmount, shakeAmount);
            float y = originalPos.y + Random.Range(-shakeAmount, shakeAmount);
            transform.localPosition = new Vector3(x, y, originalPos.z);
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalPos;
        cardBackgroundRenderer.color = original;
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
        if (!enabled) return false;

        Transform parent = transform.parent;
        if (parent != null)
        {
            if (parent.name == "PlayerHandZone") return true;
            if (parent.name == "PlayerBoardZone") return true;
        }
        
        if (BoardManager.Instance != null)
        {
            return BoardManager.Instance.IsCardOnBoard(gameObject, true);
        }
        
        return false;
    }

    /// <summary>Can we turn attack intent ON? Requires 1 AP (after pending) and >0 charges. Mana no longer required.</summary>
    private bool CanToggleAttackOn()
    {
        if (!isOnBoard || isAttacking) return false;
        if (GetAttackSlotIndex() < 0) return false;
        if (GameManager.Instance == null || !GameManager.Instance.CanAttack()) return false;
        if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsClient) return false;
        var localPlayer = Unity.Netcode.NetworkManager.Singleton.LocalClient?.PlayerObject?.GetComponent<PlayerNetwork>();
        if (localPlayer == null) return false;
        if (localPlayer.IsPlayerReady()) return false; // cannot change attack intents after Ready
        int pending = BoardManager.Instance != null ? BoardManager.Instance.GetLocalPendingAttackCount() : 0;
        int effectiveAP = localPlayer.GetCurrentActionPoints() - pending;
        if (effectiveAP < 1)
		{
			if (GameManagerUI.Instance != null)
			{
				GameManagerUI.Instance.PlayNoActionPointsFeedback();
			}
			return false;
		}
        if (cardVisual == null || cardVisual.CurrentCharges <= 0) return false;
        return true;
    }

    private bool CanPerformAttack()
    {
        if (!isOnBoard) return false;
        if (GetAttackSlotIndex() < 0) return false;
        if (GameManager.Instance == null || !GameManager.Instance.CanAttack()) return false;
        if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsClient) return false;
        var localPlayer = Unity.Netcode.NetworkManager.Singleton.LocalClient?.PlayerObject?.GetComponent<PlayerNetwork>();
        if (localPlayer == null) return false;
		if (localPlayer.GetCurrentActionPoints() <= 0)
		{
			if (GameManagerUI.Instance != null)
			{
				GameManagerUI.Instance.PlayNoActionPointsFeedback();
			}
			return false;
		}
        if (localPlayer.IsPlayerReady()) return false; // cannot perform attacks directly after Ready
        if (cardVisual == null) return false;
        if (cardVisual.CurrentCharges <= 0) return false;
        return true;
    }

    public void SetAttackIntent(bool attacking)
    {
        if (isAttacking == attacking) return;
        isAttacking = attacking;
        if (cardVisual != null)
            cardVisual.SetScheduledToAttack(attacking);
    }

    /// <summary>Clear local attack intent (e.g. after submitting on Ready or at start of Reveal).</summary>
    public void ClearAttackIntent()
    {
        if (!isAttacking) return;
        isAttacking = false;
        if (cardVisual != null)
            cardVisual.SetScheduledToAttack(false);
    }

    /// <summary>Slot index for attack RPC: from currentSlot (slot path) or BoardManager list (zone path). Returns -1 if unknown.</summary>
    private int GetAttackSlotIndex()
    {
        if (currentSlot != null)
            return currentSlot.SlotIndex;
        if (BoardManager.Instance != null)
            return BoardManager.Instance.GetCardBoardIndex(gameObject, true);
        return -1;
    }

    /// <summary>Send attack intent to server (called when player confirms Ready). Clears local isAttacking after sending.</summary>
    public void SubmitAttackIntent()
    {
        if (!isAttacking) return;
        int slotIndex = GetAttackSlotIndex();
        if (slotIndex < 0) return;
        var localPlayer = Unity.Netcode.NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<PlayerNetwork>();
        if (localPlayer != null)
        {
            localPlayer.RequestAttackServerRpc(slotIndex);
            ClearAttackIntent();
            Debug.Log($"CARD DRAGGABLE || Attack submitted for slot {slotIndex}");
        }
    }

    public void SetOnBoard(bool onBoard)
    {
        isOnBoard = onBoard;
    }
    
    public void SetCurrentSlot(BoardSlot slot)
    {
        currentSlot = slot;
    }
    
    private System.Collections.IEnumerator RearrangeHandAfterFrame(Transform handZone)
    {
        // Wait one frame to ensure unparenting is complete
        yield return null;
        
        if (CardManager.Instance != null && handZone != null)
        {
            CardManager.Instance.ArrangeCardsInHand(handZone);
        }
    }
}