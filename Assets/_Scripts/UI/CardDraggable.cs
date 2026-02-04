// using UnityEngine;
// using UnityEngine.EventSystems;
// using Unity.Netcode;

// public class CardDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
// {
//     [Header("Debug")]
//     [SerializeField] private bool skipNetworkChecks = false;

//     [Header("Drag Settings")]
//     // [SerializeField] private float dragSpeed = 30f;
    
//     [Header("Visual Feedback")]
//     [SerializeField] private Color selectedColor = new Color(1f, 1f, 0.5f, 1f);
//     [SerializeField] private Color cannotPlayColor = new Color(1f, 0.3f, 0.3f, 1f);
//     [SerializeField] private Vector3 selectedScale = new Vector3(1.2f, 1.2f, 1f);
    
//     [Header("Pivot Settings")]
//     [SerializeField] private float maxTiltAngle = 15f; // Maximum rotation angle
//     [SerializeField] private float tiltSpeed = 5f; // How fast it rotates
//     [SerializeField] private bool enablePivot = true; // Toggle on/off

//     private Vector3 lastPosition;
//     private Quaternion originalRotation;
    
//     private Vector3 originalPosition;
//     private Transform originalParent;
//     private Vector3 originalScale;
//     private Color originalColor;
//     private bool isDragging;
//     private bool isSelected;
//     private bool isOnBoard;
    
//     private SpriteRenderer cardBackgroundRenderer;
//     private CardVisual cardVisual;
    
//     public static CardDraggable SelectedCard { get; private set; }

//     public bool IsDragging => isDragging;
//     public bool IsSelected => isSelected;
//     public bool IsOnBoard => isOnBoard;

//     private void Awake()
//     {
//         cardVisual = GetComponent<CardVisual>();
//         cardBackgroundRenderer = GetComponent<SpriteRenderer>();
        
//         if (cardBackgroundRenderer != null)
//         {
//             originalColor = cardBackgroundRenderer.color;
//         }
//         originalScale = transform.localScale;
//         originalRotation = transform.localRotation;
//     }

//     public void OnPointerClick(PointerEventData eventData)
//     {
//         if (isDragging) return;
//         if (!IsPlayerCard()) return;

//         if (!CanBePlayed())
//         {
//             ShowCannotPlayFeedback();
//             return;
//         }

//         if (isSelected)
//         {
//             DeselectCard();
//         }
//         else
//         {
//             SelectCard();
//         }
//     }

//     public void SelectCard()
//     {
//         if (SelectedCard != null && SelectedCard != this)
//         {
//             SelectedCard.DeselectCard();
//         }

//         isSelected = true;
//         SelectedCard = this;
        
//         if (cardBackgroundRenderer != null)
//         {
//             cardBackgroundRenderer.color = selectedColor;
//         }

//         Debug.Log($"CARD DRAGGABLE || Card selected");
//     }

//     public void DeselectCard()
//     {
//         if (!isSelected) return;

//         isSelected = false;
//         if (SelectedCard == this)
//         {
//             SelectedCard = null;
//         }

//         if (cardBackgroundRenderer != null)
//         {
//             cardBackgroundRenderer.color = originalColor;
//         }

//         Debug.Log($"CARD DRAGGABLE || Card deselected");
//     }

//     public void OnBeginDrag(PointerEventData eventData)
//     {   
//         if (!IsPlayerCard()) 
//         {
//             Debug.Log($"CARD DRAGGABLE || Drag blocked - not a player card");
//             return;
//         }

//         if (!CanBePlayed())
//         {
//             ShowCannotPlayFeedback();
//             return;
//         }

//         if (isSelected)
//         {
//             DeselectCard();
//         }

//         isDragging = true;
//         originalPosition = transform.position;
//         originalParent = transform.parent;
//         lastPosition = transform.position;

//         // Remember if card was on board
//         if (BoardManager.Instance != null)
//         {
//             isOnBoard = BoardManager.Instance.IsCardOnBoard(gameObject, true);
//         }

//         if (cardBackgroundRenderer != null)
//         {
//             cardBackgroundRenderer.sortingOrder = 200;
//         }

//         // Slightly enlarge while dragging
//         transform.localScale = originalScale * 1.15f;

//         Debug.Log($"CARD DRAGGABLE || Started dragging");
//     }

//     public void OnDrag(PointerEventData eventData)
//     {
//         if (!isDragging) return;

//         Vector3 mousePosition = Camera.main.ScreenToWorldPoint(eventData.position);
//         mousePosition.z = 0;

//         // Use instant positioning instead of lerp for more responsive dragging
//         // This ensures the card is exactly where the mouse is when dropped
//         transform.position = mousePosition;
        
//         // TILT/PIVOT EFFECT
//         if (enablePivot)
//         {
//             // Calculate movement direction
//             Vector3 movement = transform.position - lastPosition;
            
//             if (movement.magnitude > 0.001f) // Only tilt if actually moving
//             {
//                 // Calculate tilt angle based on horizontal movement
//                 float tiltAngle = Mathf.Clamp(movement.x * maxTiltAngle * 100f, -maxTiltAngle, maxTiltAngle);
                
//                 // Create target rotation
//                 Quaternion targetRotation = Quaternion.Euler(0, 0, -tiltAngle);
                
//                 // Smoothly rotate toward target
//                 transform.localRotation = Quaternion.Lerp(
//                     transform.localRotation, 
//                     targetRotation, 
//                     tiltSpeed * Time.deltaTime
//                 );
//             }
            
//             lastPosition = transform.position;
//         }

//         // Show visual feedback for valid/invalid drop zones
//         if (BoardManager.Instance != null)
//         {
//             if (BoardManager.Instance.IsPositionOverBoard(transform.position, out bool isPlayerBoard))
//             {
//                 if (isPlayerBoard)
//                 {
//                     // Check if we can place here
//                     if (BoardManager.Instance.CanPlaceCardOnBoard(true) || isOnBoard)
//                     {
//                         BoardManager.Instance.ShowValidDropFeedback(true);
//                     }
//                     else
//                     {
//                         BoardManager.Instance.ShowInvalidDropFeedback(true);
//                     }
//                 }
//             }
//         }
//     }

//     public void OnEndDrag(PointerEventData eventData)
//     {
//         if (!isDragging)
//         {
//             return;
//         }
        
//         isDragging = false;
        
//         // Reset scale and rotation
//         transform.localScale = originalScale;
//         transform.localRotation = originalRotation;

//         if (cardBackgroundRenderer != null)
//         {
//             cardBackgroundRenderer.sortingOrder = 0;
//         }

//         // IMPORTANT: Use the card's actual position, not mouse position
//         // This is more accurate because of the lerp delay
//         Vector3 dropPosition = transform.position;
//         dropPosition.z = 0;

//         Debug.Log($"CARD DRAGGABLE || Drop position: {dropPosition}");

//         // Check if dropped on board area
//         if (BoardManager.Instance != null)
//         {
//             if (BoardManager.Instance.IsPositionOverBoard(dropPosition, out bool isPlayerBoard))
//             {
//                 Debug.Log($"CARD DRAGGABLE || Over board zone: {(isPlayerBoard ? "Player" : "Opponent")}");
                
//                 if (isPlayerBoard)
//                 {
//                     // Try to place on player board
//                     if (TryPlaceOnBoard())
//                     {
//                         Debug.Log("CARD DRAGGABLE || Card placed on board successfully");
//                         return;
//                     }
//                     else
//                     {
//                         Debug.Log("CARD DRAGGABLE || Failed to place on board, returning to hand");
//                     }
//                 }
//                 else
//                 {
//                     Debug.Log("CARD DRAGGABLE || Cannot place on opponent board");
//                 }
//             }
//             else
//             {
//                 Debug.Log("CARD DRAGGABLE || Not over any board zone");
//             }
//         }
//         else
//         {
//             Debug.LogError("CARD DRAGGABLE || BoardManager.Instance is null!");
//         }

//         // Return to original position
//         ReturnToOriginalPosition();
//     }

//     private bool TryPlaceOnBoard()
//     {
//         if (BoardManager.Instance == null) return false;

//         // Validation check
//         if (!CanBePlayed())
//         {
//             ShowCannotPlayFeedback();
//             return false;
//         }

//         // Try to place card
//         bool placed = BoardManager.Instance.TryPlaceCard(gameObject, true);
        
//         if (placed)
//         {
//             isOnBoard = true;
            
//             // Remove from hand tracking
//             if (CardManager.Instance != null)
//             {
//                 CardManager.Instance.RemoveCardFromHand(gameObject);
//             }
//         }

//         return placed;
//     }

//     private void ReturnToOriginalPosition()
//     {
//         if (isOnBoard && BoardManager.Instance != null)
//         {
//             // Was on board, return to board
//             BoardManager.Instance.TryPlaceCard(gameObject, true);
//         }
//         else if (originalParent != null)
//         {
//             // Was in hand, return to hand
//             transform.SetParent(originalParent);
//             // CardManager will handle repositioning
//         }

//         Debug.Log($"CARD DRAGGABLE || Card returned to original position");
//     }

//     public bool CanBePlayed()
//     {
//         if (skipNetworkChecks) return true;

//         // 1. PHASE CHECK
//         if (GameManager.Instance == null || !GameManager.Instance.CanPlayCards())
//         {
//             Debug.Log($"CARD DRAGGABLE || Cannot play - not in Planning phase");
//             return false;
//         }

//         // 2. PLAYER REFERENCE
//         if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
//         {
//             return false;
//         }

//         var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject?.GetComponent<PlayerNetwork>();
//         if (localPlayer == null)
//         {
//             return false;
//         }

//         // 3. ACTION POINTS CHECK
//         if (localPlayer.GetCurrentActionPoints() <= 0)
//         {
//             Debug.Log($"CARD DRAGGABLE || Cannot play - no AP remaining");
//             return false;
//         }

//         // 4. MANA COST CHECK
//         if (cardVisual == null) return false;

//         if (CardManager.Instance != null && CardManager.Instance.GetCardLibrary() != null)
//         {
//             CardData cardData = CardManager.Instance.GetCardLibrary().GetTierOneAssetFromPool(cardVisual.CardID);
//             if (cardData == null) return false;

//             int currentMana = localPlayer.GetCurrentMana();
//             if (currentMana < cardData.manaCost)
//             {
//                 Debug.Log($"CARD DRAGGABLE || Cannot play - not enough mana");
//                 return false;
//             }
//         }

//         return true;
//     }

//     private void ShowCannotPlayFeedback()
//     {
//         if (cardBackgroundRenderer != null)
//         {
//             StartCoroutine(FlashCannotPlay());
//         }
//     }

//     private System.Collections.IEnumerator FlashCannotPlay()
//     {
//         Color original = cardBackgroundRenderer.color;
//         cardBackgroundRenderer.color = cannotPlayColor;
        
//         Vector3 originalPos = transform.localPosition;
//         float shakeAmount = 0.1f;
//         float shakeDuration = 0.2f;
//         float elapsed = 0f;

//         while (elapsed < shakeDuration)
//         {
//             float x = originalPos.x + Random.Range(-shakeAmount, shakeAmount);
//             float y = originalPos.y + Random.Range(-shakeAmount, shakeAmount);
//             transform.localPosition = new Vector3(x, y, originalPos.z);
            
//             elapsed += Time.deltaTime;
//             yield return null;
//         }

//         transform.localPosition = originalPos;
//         cardBackgroundRenderer.color = original;
//     }

//     private void OnDestroy()
//     {
//         if (SelectedCard == this)
//         {
//             SelectedCard = null;
//         }
//     }

//     private bool IsPlayerCard()
//     {
//         if (!enabled) return false;

//         Transform parent = transform.parent;
//         if (parent != null)
//         {
//             if (parent.name == "PlayerHandZone") return true;
//             if (parent.name == "PlayerBoardZone") return true;
//         }
        
//         // Check if on player board
//         if (BoardManager.Instance != null)
//         {
//             return BoardManager.Instance.IsCardOnBoard(gameObject, true);
//         }
        
//         return false;
//     }

//     public void SetOnBoard(bool onBoard)
//     {
//         isOnBoard = onBoard;
//     }
// }

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
    
    // Dragging state
    private Vector3 dragOffset;
    private float currentTilt = 0f;
    private Vector3 lastDragPosition;
    private Camera mainCamera;
    
    private SpriteRenderer cardBackgroundRenderer;
    private CardVisual cardVisual;
    
    public static CardDraggable SelectedCard { get; private set; }

    public bool IsDragging => isDragging;
    public bool IsSelected => isSelected;
    public bool IsOnBoard => isOnBoard;

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

        if (!CanBePlayed())
        {
            ShowCannotPlayFeedback();
            return;
        }

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
        if (skipNetworkChecks) return true;

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

        if (localPlayer.GetCurrentActionPoints() <= 0)
        {
            Debug.Log($"CARD DRAGGABLE || Cannot play - no AP remaining");
            return false;
        }

        if (cardVisual == null) return false;

        if (CardManager.Instance != null && CardManager.Instance.GetCardLibrary() != null)
        {
            CardData cardData = CardManager.Instance.GetCardLibrary().GetTierOneAssetFromPool(cardVisual.CardID);
            if (cardData == null) return false;

            int currentMana = localPlayer.GetCurrentMana();
            if (currentMana < cardData.manaCost)
            {
                Debug.Log($"CARD DRAGGABLE || Cannot play - not enough mana");
                return false;
            }
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

    public void SetOnBoard(bool onBoard)
    {
        isOnBoard = onBoard;
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