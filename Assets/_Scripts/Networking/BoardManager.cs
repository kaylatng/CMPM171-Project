using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance;

    [SerializeField] private CardLibrary library;

    [Header("Board Settings")]
    [SerializeField] private int maxCardsPerBoard = 3;
    
    [Header("Board Zones")]
    [SerializeField] private Transform playerBoardZone;
    [SerializeField] private Transform opponentBoardZone;

    [Header("Card Layout")]
    [SerializeField] private float cardSpacing = 2.0f;
    [SerializeField] private float cardMoveSpeed = 12f;
    [SerializeField] private AnimationCurve layoutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Reveal Phase")]
    [SerializeField] private float revealFlipDuration = 0.25f;
    [SerializeField] private float delayBetweenReveals = 0.35f;

    [Header("Merge Settings")]
    [SerializeField] private float mergeAnimationDuration = 0.5f;
    [SerializeField] private Color mergeFlashColor = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private float mergeScalePulse = 1.3f;
    [SerializeField] private AnimationCurve mergeScaleCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Visual Feedback")]
    [SerializeField] private Color validDropColor = new Color(0.5f, 1f, 0.5f, 0.3f);
    [SerializeField] private Color invalidDropColor = new Color(1f, 0.3f, 0.3f, 0.3f);
    [SerializeField] private Color normalZoneColor = new Color(1f, 1f, 1f, 0.1f);

    private List<GameObject> playerBoardCards = new List<GameObject>();
    private List<GameObject> opponentBoardCards = new List<GameObject>();

    // For smooth card positioning
    private Dictionary<GameObject, Vector3> cardTargetPositions = new Dictionary<GameObject, Vector3>();
    private Dictionary<GameObject, int> cardBoardIndices = new Dictionary<GameObject, int>();

    private SpriteRenderer playerZoneRenderer;
    private SpriteRenderer opponentZoneRenderer;

    // Track board slots for blinking feedback
    private List<BoardSlot> playerSlots = new List<BoardSlot>();
    private List<BoardSlot> opponentSlots = new List<BoardSlot>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        SetupBoardZones();
        SetupZoneVisuals();
    }

    private void Update()
    {
        // Smoothly move cards to their target positions
        UpdateCardPositions();
        
        // Update slot blinking based on card selection/dragging
        UpdateSlotBlinking();
    }
    
    private void UpdateSlotBlinking()
    {
        // Check if a card is selected or being dragged from hand
        bool shouldBlink = false;
        
        // Check if a card is selected
        if (CardDraggable.SelectedCard != null)
        {
            CardDraggable selected = CardDraggable.SelectedCard;
            
            // Check if card is in hand (not on board)
            // When dragging, card is unparented, so check IsOnBoard property
            if (!selected.IsOnBoard)
            {
                // Also verify it's not currently on board
                if (!IsCardOnBoard(selected.gameObject, true))
                {
                    shouldBlink = true;
                }
            }
        }
        
        // Also check if any card is being dragged (even if not selected)
        if (!shouldBlink)
        {
            // Check all cards in player hand
            Transform handZone = GameObject.Find("PlayerHandZone")?.transform;
            if (handZone != null)
            {
                for (int i = 0; i < handZone.childCount; i++)
                {
                    Transform child = handZone.GetChild(i);
                    CardDraggable draggable = child.GetComponent<CardDraggable>();
                    if (draggable != null && draggable.IsDragging)
                    {
                        shouldBlink = true;
                        break;
                    }
                }
            }
            
            // Also check unparented cards that might be dragged (they're unparented during drag)
            // We'll check if any CardDraggable is currently dragging and not on board
            if (!shouldBlink)
            {
                CardDraggable[] allDraggables = FindObjectsOfType<CardDraggable>();
                foreach (CardDraggable draggable in allDraggables)
                {
                    if (draggable != null && draggable.IsDragging && !draggable.IsOnBoard)
                    {
                        // Check if it's a player card (not opponent)
                        Transform parent = draggable.transform.parent;
                        if (parent == null || parent.name == "PlayerHandZone")
                        {
                            shouldBlink = true;
                            break;
                        }
                    }
                }
            }
        }
        
        // Update blinking state for all player slots
        foreach (BoardSlot slot in playerSlots)
        {
            if (slot != null)
            {
                slot.SetBlinking(shouldBlink);
            }
        }
    }
    
    public void RegisterSlot(BoardSlot slot)
    {
        if (slot == null) return;
        
        if (slot.IsPlayerSlot)
        {
            if (!playerSlots.Contains(slot))
            {
                playerSlots.Add(slot);
                Debug.Log($"BOARD MANAGER || Registered player slot {slot.SlotIndex}");
            }
        }
        else
        {
            if (!opponentSlots.Contains(slot))
            {
                opponentSlots.Add(slot);
                Debug.Log($"BOARD MANAGER || Registered opponent slot {slot.SlotIndex}");
            }
        }
    }

    private void SetupBoardZones()
    {
        if (playerBoardZone == null)
        {
            GameObject pZone = GameObject.Find("PlayerBoardZone");
            if (pZone != null)
            {
                playerBoardZone = pZone.transform;
                Debug.Log("BOARD MANAGER || Player board zone found");
            }
            else
            {
                Debug.LogWarning("BOARD MANAGER || PlayerBoardZone not found!");
            }
        }

        if (opponentBoardZone == null)
        {
            GameObject oZone = GameObject.Find("OpponentBoardZone");
            if (oZone != null)
            {
                opponentBoardZone = oZone.transform;
                Debug.Log("BOARD MANAGER || Opponent board zone found");
            }
            else
            {
                Debug.LogWarning("BOARD MANAGER || OpponentBoardZone not found!");
            }
        }
    }

    private void SetupZoneVisuals()
    {
        // Add visual feedback to board zones
        if (playerBoardZone != null)
        {
            playerZoneRenderer = playerBoardZone.GetComponent<SpriteRenderer>();
            if (playerZoneRenderer == null)
            {
                playerZoneRenderer = playerBoardZone.gameObject.AddComponent<SpriteRenderer>();
                playerZoneRenderer.sprite = CreateZoneSprite();
                playerZoneRenderer.sortingOrder = -10;
            }
            playerZoneRenderer.color = normalZoneColor;
            // Hide the sprite renderer (keep collider for drop detection)
            playerZoneRenderer.enabled = false;

            // Add collider for drop detection
            BoxCollider2D collider = playerBoardZone.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = playerBoardZone.gameObject.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(8f, 1f); // Wide enough for 3 cards
            }
        }

        if (opponentBoardZone != null)
        {
            opponentZoneRenderer = opponentBoardZone.GetComponent<SpriteRenderer>();
            if (opponentZoneRenderer == null)
            {
                opponentZoneRenderer = opponentBoardZone.gameObject.AddComponent<SpriteRenderer>();
                opponentZoneRenderer.sprite = CreateZoneSprite();
                opponentZoneRenderer.sortingOrder = -10;

            }
            opponentZoneRenderer.color = normalZoneColor;
            // Hide the sprite renderer (keep collider for drop detection)
            opponentZoneRenderer.enabled = false;

            BoxCollider2D collider = opponentBoardZone.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = opponentBoardZone.gameObject.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(8f, 1f);
            }
        }
    }

    private Sprite CreateZoneSprite()
    {
        // Create a simple rectangular sprite for the zone
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
    }

    public bool CanPlaceCardOnBoard(bool isPlayerCard)
    {
        List<GameObject> targetBoard = isPlayerCard ? playerBoardCards : opponentBoardCards;
        return targetBoard.Count < maxCardsPerBoard;
    }

    public bool TryPlaceCard(GameObject card, bool isPlayerCard, int? preferredIndex = null)
    {
        if (card == null)
        {
            Debug.LogError("BOARD MANAGER || Cannot place null card");
            return false;
        }

        List<GameObject> targetBoard = isPlayerCard ? playerBoardCards : opponentBoardCards;
        Transform targetZone = isPlayerCard ? playerBoardZone : opponentBoardZone;

        // Check if board is full
        if (targetBoard.Count >= maxCardsPerBoard)
        {
            Debug.Log($"BOARD MANAGER || Board full! ({targetBoard.Count}/{maxCardsPerBoard})");
            ShowInvalidDropFeedback(isPlayerCard);
            return false;
        }

        // Check if card is already on this board
        if (targetBoard.Contains(card))
        {
            Debug.Log("BOARD MANAGER || Card already on board, repositioning");
            // Just rearrange
            ArrangeCardsOnBoard(isPlayerCard);
            return true;
        }

        // Remove from other board if present
        RemoveCardFromBoard(card, !isPlayerCard, notifyServer: false);

        // Add to board
        card.transform.SetParent(targetZone, true);
        
        if (preferredIndex.HasValue && preferredIndex.Value < targetBoard.Count)
        {
            targetBoard.Insert(preferredIndex.Value, card);
        }
        else
        {
            targetBoard.Add(card);
        }

        cardBoardIndices[card] = targetBoard.IndexOf(card);

        // Set card as on board for shadow visibility
        CardDraggable draggable = card.GetComponent<CardDraggable>();
        if (draggable != null)
        {
            draggable.SetOnBoard(true);
        }
        
        // Ensure shadow component exists for all cards on board (player and opponent)
        CardShadow shadow = card.GetComponent<CardShadow>();
        if (shadow == null)
        {
            shadow = card.AddComponent<CardShadow>();
        }

        // Arrange all cards
        ArrangeCardsOnBoard(isPlayerCard);

        Debug.Log($"BOARD MANAGER || Placed card on {(isPlayerCard ? "player" : "opponent")} board ({targetBoard.Count}/{maxCardsPerBoard})");

        // CHECK FOR MERGES after placing the card (player only; opponent merges only in reveal phase)
        if (isPlayerCard)
        {
            CheckAndMergeCards(isPlayerCard);
        }

        // Network notification
        if (isPlayerCard)
        {
            NotifyServerCardPlaced(card);
        }

        return true;
    }

    /// Check for duplicate cards with MATCHING TIERS and merge them.
    /// Keys on the CardData reference directly — since IDs are 100/101/102 per tier,
    /// same CardData object = same card type AND same tier. No ID math needed.
    /// Merges into the LEFT-MOST card (first in the list).
    private void CheckAndMergeCards(bool isPlayerBoard)
    {
        List<GameObject> currentBoard = isPlayerBoard ? playerBoardCards : opponentBoardCards;

        // Group cards by their exact CardData reference.
        // Same CardData = same card type AND same tier (100 = T1, 101 = T2, 102 = T3).
        Dictionary<CardData, List<GameObject>> cardsByData = new Dictionary<CardData, List<GameObject>>();

        for (int i = 0; i < currentBoard.Count; i++)
        {
            CardVisual visual = currentBoard[i].GetComponent<CardVisual>();
            if (visual == null || visual.CurrentCardData == null) continue;

            CardData data = visual.CurrentCardData;

            Debug.Log($"BOARD MANAGER || Card {i}: {data.cardName} (ID: {data.cardID}, Tier: {data.tier})");

            if (!cardsByData.ContainsKey(data))
                cardsByData[data] = new List<GameObject>();

            cardsByData[data].Add(currentBoard[i]);
        }

        // Find any pair sharing the same CardData
        foreach (var kvp in cardsByData)
        {
            if (kvp.Value.Count >= 2)
            {
                CardData matchedData = kvp.Key;
                Debug.Log($"BOARD MANAGER || Match: {matchedData.cardName} Tier {matchedData.tier} x{kvp.Value.Count} - merging!");

                GameObject targetCard = kvp.Value[0];                     // LEFTMOST (upgraded)
                GameObject cardToMerge = kvp.Value[kvp.Value.Count - 1];  // RIGHTMOST (flies in)

                StartCoroutine(PerformMerge(cardToMerge, targetCard, isPlayerBoard));
                break; // One merge at a time
            }
        }
    }


    /// <summary>
    /// Perform the merge animation and upgrade
    /// cardToMerge (right card) flies INTO targetCard (left card)
    /// </summary>
    private System.Collections.IEnumerator PerformMerge(GameObject cardToMerge, GameObject targetCard, bool isPlayerBoard)
    {
        Debug.Log($"BOARD MANAGER || Merging cards - {cardToMerge.name} flying into {targetCard.name}");

        // Get the visual components
        CardVisual mergeVisual = cardToMerge.GetComponent<CardVisual>();
        CardVisual targetVisual = targetCard.GetComponent<CardVisual>();

        if (targetVisual == null || mergeVisual == null)
        {
            Debug.LogError("BOARD MANAGER || Cannot merge - missing CardVisual component");
            yield break;
        }

        // Get current tier data from the actual card (not the base tier)
        CardData currentData = targetVisual.CurrentCardData;
        
        if (currentData == null)
        {
            Debug.LogError($"BOARD MANAGER || Cannot find current card data");
            yield break;
        }

        // ANIMATE THE MERGE
        Vector3 mergeStartPos = cardToMerge.transform.position;
        Vector3 targetPos = targetCard.transform.position;
        Vector3 mergeStartScale = cardToMerge.transform.localScale;
        Vector3 targetStartScale = targetCard.transform.localScale;

        SpriteRenderer mergeSR = cardToMerge.GetComponent<SpriteRenderer>();
        SpriteRenderer targetSR = targetCard.GetComponent<SpriteRenderer>();
        
        Color mergeOriginalColor = mergeSR != null ? mergeSR.color : Color.white;
        Color targetOriginalColor = targetSR != null ? targetSR.color : Color.white;

        float elapsed = 0f;

        // Animate the RIGHT card flying INTO the LEFT card
        while (elapsed < mergeAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / mergeAnimationDuration;
            float curveT = mergeScaleCurve.Evaluate(t);

            // Move the merging card to the target (NO SHRINKING - stays full size)
            cardToMerge.transform.position = Vector3.Lerp(mergeStartPos, targetPos, t);
            cardToMerge.transform.localScale = mergeStartScale; // Keep original scale
            
            // Pulse the target card
            targetCard.transform.localScale = Vector3.Lerp(targetStartScale, targetStartScale * mergeScalePulse, curveT);
            
            // Flash colors on BOTH cards
            if (mergeSR != null)
            {
                mergeSR.color = Color.Lerp(mergeOriginalColor, mergeFlashColor, t);
            }
            if (targetSR != null)
            {
                targetSR.color = Color.Lerp(targetOriginalColor, mergeFlashColor, curveT);
            }

            yield return null;
        }

        // Remove the merged card from board
        List<GameObject> currentBoard = isPlayerBoard ? playerBoardCards : opponentBoardCards;
        currentBoard.Remove(cardToMerge);
        cardBoardIndices.Remove(cardToMerge);
        cardTargetPositions.Remove(cardToMerge);
        Destroy(cardToMerge);

        // UPGRADE THE TARGET CARD TO NEXT TIER
        if (currentData.nextTier != null)
        {
            Debug.Log($"BOARD MANAGER || Upgrading card from tier {currentData.tier} to tier {currentData.nextTier.tier}");
            
            // Update the visual with new tier data
            targetVisual.Initialize(targetVisual.CardID, currentData.nextTier);
            
            // Visual feedback for upgrade
            if (targetSR != null)
            {
                targetSR.color = mergeFlashColor;
            }
            
            yield return new WaitForSeconds(0.2f);
            
            if (targetSR != null)
            {
                targetSR.color = currentData.nextTier.themeColor;
            }
        }
        else
        {
            Debug.Log($"BOARD MANAGER || Card is already max tier {currentData.tier}");
            
            // Restore original color if already max tier
            if (targetSR != null)
            {
                targetSR.color = targetOriginalColor;
            }
        }

        // Reset target card scale
        targetCard.transform.localScale = targetStartScale;

        // Rearrange remaining cards
        ArrangeCardsOnBoard(isPlayerBoard);

        Debug.Log($"BOARD MANAGER || Merge complete! Remaining cards: {currentBoard.Count}");

        // Check again — the upgraded card may now match another card on the board
        CheckAndMergeCards(isPlayerBoard);
    }

    /// <summary>
    /// Called at start of reveal phase: flip opponent cards one by one, then run merge if possible.
    /// </summary>
    public void StartRevealSequence()
    {
        StartCoroutine(RevealOpponentCardsThenMerge());
    }

    private IEnumerator RevealOpponentCardsThenMerge()
    {
        // Reveal opponent cards one by one
        List<GameObject> cardsToReveal = new List<GameObject>(opponentBoardCards);
        foreach (GameObject card in cardsToReveal)
        {
            if (card == null) continue;
            CardVisual visual = card.GetComponent<CardVisual>();
            if (visual != null && visual.IsFaceDown)
            {
                yield return StartCoroutine(visual.FlipToReveal(revealFlipDuration));
                yield return new WaitForSeconds(delayBetweenReveals);
            }
        }

        // After all revealed, check for merges and play merge animation
        yield return new WaitForSeconds(0.15f);
        CheckAndMergeCards(false);
    }

    private void ArrangeCardsOnBoard(bool isPlayerBoard)
    {
        List<GameObject> cards = isPlayerBoard ? playerBoardCards : opponentBoardCards;
        Transform zone = isPlayerBoard ? playerBoardZone : opponentBoardZone;

        if (zone == null || cards.Count == 0)
        {
            return;
        }

        // Calculate positions
        float totalWidth = (cards.Count - 1) * cardSpacing;
        float startX = -totalWidth / 2f;

        for (int i = 0; i < cards.Count; i++)
        {
            GameObject card = cards[i];
            if (card == null) continue;

            // Set target position
            float verticalOffset = isPlayerBoard ? -1.2f : 0;
            Vector3 targetPos = new Vector3(startX + (i * cardSpacing), verticalOffset, 0);
            cardTargetPositions[card] = targetPos;
            cardBoardIndices[card] = i;

            // Set sorting order (face-down cards keep card back above prefab "Square" by using 2 + index)
            SpriteRenderer sr = card.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                var cv = card.GetComponent<CardVisual>();
                if (cv != null && cv.IsFaceDown)
                    sr.sortingOrder = 2 + i;
                else
                    sr.sortingOrder = i;
            }
        }
    }

    private void UpdateCardPositions()
    {
        // Smoothly lerp cards to their target positions
        foreach (var kvp in cardTargetPositions)
        {
            GameObject card = kvp.Key;
            Vector3 targetPos = kvp.Value;

            if (card != null)
            {
                // Check if card is being dragged
                CardDraggable draggable = card.GetComponent<CardDraggable>();
                if (draggable != null && draggable.IsDragging)
                {
                    continue; // Don't update position while dragging
                }

                // Smooth movement
                card.transform.localPosition = Vector3.Lerp(
                    card.transform.localPosition,
                    targetPos,
                    Time.deltaTime * cardMoveSpeed
                );
            }
        }
    }

    public void RemoveCardFromBoard(GameObject card, bool isPlayerBoard, bool notifyServer = true)
    {
        if (card == null) return;

        List<GameObject> cards = isPlayerBoard ? playerBoardCards : opponentBoardCards;

        if (cards.Contains(card))
        {
            cards.Remove(card);
            cardBoardIndices.Remove(card);
            cardTargetPositions.Remove(card);

            ArrangeCardsOnBoard(isPlayerBoard);

            Debug.Log($"BOARD MANAGER || Removed card from {(isPlayerBoard ? "player" : "opponent")} board");

            if (notifyServer && isPlayerBoard)
            {
                NotifyServerCardRemoved(card);
            }
        }
    }

    public void ReturnCardToHand(GameObject card)
    {
        if (card == null) return;

        // Remove from both boards
        RemoveCardFromBoard(card, true, notifyServer: false);
        RemoveCardFromBoard(card, false, notifyServer: false);

        // Return to hand
        if (CardManager.Instance != null)
        {
            Transform handZone = GameObject.Find("PlayerHandZone")?.transform;
            if (handZone != null)
            {
                card.transform.SetParent(handZone);
                Debug.Log("BOARD MANAGER || Card returned to hand");
            }
        }
    }

    public bool IsCardOnBoard(GameObject card, bool isPlayerBoard)
    {
        List<GameObject> cards = isPlayerBoard ? playerBoardCards : opponentBoardCards;
        return cards.Contains(card);
    }

    public int GetCardCount(bool isPlayerBoard)
    {
        List<GameObject> cards = isPlayerBoard ? playerBoardCards : opponentBoardCards;
        return cards.Count;
    }

    public List<GameObject> GetBoardCards(bool isPlayerBoard)
    {
        return isPlayerBoard ? new List<GameObject>(playerBoardCards) : new List<GameObject>(opponentBoardCards);
    }

    public void ClearBoard(bool isPlayerBoard)
    {
        List<GameObject> cards = isPlayerBoard ? playerBoardCards : opponentBoardCards;
        
        foreach (GameObject card in cards)
        {
            if (card != null)
            {
                Destroy(card);
            }
        }

        cards.Clear();
        cardTargetPositions.Clear();
        cardBoardIndices.Clear();
        Debug.Log($"BOARD MANAGER || Cleared {(isPlayerBoard ? "player" : "opponent")} board");
    }

    public void ShowValidDropFeedback(bool isPlayerBoard)
    {
        SpriteRenderer renderer = isPlayerBoard ? playerZoneRenderer : opponentZoneRenderer;
        if (renderer != null)
        {
            StopAllCoroutines();
            StartCoroutine(FlashZoneColor(renderer, validDropColor));
        }
    }

    public void ShowInvalidDropFeedback(bool isPlayerBoard)
    {
        SpriteRenderer renderer = isPlayerBoard ? playerZoneRenderer : opponentZoneRenderer;
        if (renderer != null)
        {
            StopAllCoroutines();
            StartCoroutine(FlashZoneColor(renderer, invalidDropColor));
        }
    }

    private System.Collections.IEnumerator FlashZoneColor(SpriteRenderer renderer, Color flashColor)
    {
        Color original = renderer.color;
        renderer.color = flashColor;
        yield return new WaitForSeconds(0.3f);
        renderer.color = original;
    }

    // Slot-based card placement notification
    // The bool parameter can be either isFromHand or shouldNotifyServer - both indicate server notification should happen
    public void OnCardPlacedInSlot(BoardSlot slot, GameObject card, bool shouldNotifyServer = true)
    {
        if (slot == null || card == null) return;
        
        // If card is being placed in a player slot and should notify server
        if (slot.IsPlayerSlot && shouldNotifyServer)
        {
            NotifyServerCardPlaced(card);
        }
        
        Debug.Log($"BOARD MANAGER || Card placed in slot {slot.SlotIndex} (notify server: {shouldNotifyServer})");
    }
    
    public void OnCardRemovedFromSlot(BoardSlot slot, GameObject card)
    {
        if (slot == null || card == null) return;
        
        // Remove card from board tracking if it exists
        if (playerBoardCards.Contains(card))
        {
            RemoveCardFromBoard(card, true, notifyServer: false);
        }
        else if (opponentBoardCards.Contains(card))
        {
            RemoveCardFromBoard(card, false, notifyServer: false);
        }
        
        Debug.Log($"BOARD MANAGER || Card removed from slot {slot.SlotIndex}");
    }

    // Network integration points
    private void NotifyServerCardPlaced(GameObject card)
    {
        CardVisual visual = card.GetComponent<CardVisual>();
        if (visual == null) return;

        int cardId = visual.CardID;
        int index = playerBoardCards.IndexOf(card);

        // Call your existing network RPC
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject?.GetComponent<PlayerNetwork>();
            if (localPlayer != null)
            {
                localPlayer.PlayCardToSlotServerRpc(cardId, index);
            }
        }
    }

    private void NotifyServerCardRemoved(GameObject card)
    {
        // Implement if you need server notification for card removal
        Debug.Log("BOARD MANAGER || Card removed (network notification)");
    }

    public void PlaceOpponentCard(int cardId, int index)
    {
        if (CardManager.Instance == null)
        {
            Debug.LogError("BOARD MANAGER || CardManager.Instance is null!");
            return;
        }
        // Remove one card from opponent hand
        CardManager.Instance.RemoveOneOpponentHandCard();

        // Create the card face-down (revealed in reveal phase)
        GameObject card = Instantiate(CardManager.Instance.GetCardPrefab());
        if (card != null)
        {
            CardManager.Instance.InitializeCardVisual(card, cardId, false);
            
            CardDraggable draggable = card.GetComponent<CardDraggable>();
            if (draggable != null)
            {
                draggable.enabled = false;
                // Set as on board so shadow will show
                draggable.SetOnBoard(true);
            }
            
            // Ensure shadow component exists for opponent cards
            CardShadow shadow = card.GetComponent<CardShadow>();
            if (shadow == null)
            {
                shadow = card.AddComponent<CardShadow>();
            }
            
            TryPlaceCard(card, false, index);
            
            Debug.Log($"BOARD MANAGER || Opponent placed card {cardId} at index {index}");
        }
    }

    // Helper to check if position is over a board zone
    public bool IsPositionOverBoard(Vector3 worldPosition, out bool isPlayerBoard)
    {
        isPlayerBoard = true;

        // Check player board with expanded bounds for easier dropping
        if (playerBoardZone != null)
        {
            BoxCollider2D collider = playerBoardZone.GetComponent<BoxCollider2D>();
            if (collider != null)
            {
                // Use bounds checking with slight expansion for easier dropping
                Bounds bounds = collider.bounds;
                bounds.Expand(0.5f); // Add 0.5 units of padding for easier drops
                
                if (bounds.Contains(worldPosition))
                {
                    isPlayerBoard = true;
                    Debug.Log($"BOARD MANAGER || Position {worldPosition} is over PLAYER board");
                    return true;
                }
            }
            else
            {
                Debug.LogWarning("BOARD MANAGER || PlayerBoardZone missing BoxCollider2D!");
            }
        }

        // Check opponent board
        if (opponentBoardZone != null)
        {
            BoxCollider2D collider = opponentBoardZone.GetComponent<BoxCollider2D>();
            if (collider != null)
            {
                Bounds bounds = collider.bounds;
                bounds.Expand(0.5f);
                
                if (bounds.Contains(worldPosition))
                {
                    isPlayerBoard = false;
                    Debug.Log($"BOARD MANAGER || Position {worldPosition} is over OPPONENT board");
                    return true;
                }
            }
            else
            {
                Debug.LogWarning("BOARD MANAGER || OpponentBoardZone missing BoxCollider2D!");
            }
        }

        Debug.Log($"BOARD MANAGER || Position {worldPosition} is NOT over any board");
        return false;
    }

    // Debug visualization - shows board zones in Scene view
    private void OnDrawGizmos()
    {
        if (playerBoardZone != null)
        {
            BoxCollider2D collider = playerBoardZone.GetComponent<BoxCollider2D>();
            if (collider != null)
            {
                Gizmos.color = new Color(0, 1, 0, 0.3f); // Green for player board
                Bounds bounds = collider.bounds;
                bounds.Expand(0.5f); // Show expanded bounds
                Gizmos.DrawCube(bounds.center, bounds.size);
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }
        
        if (opponentBoardZone != null)
        {
            BoxCollider2D collider = opponentBoardZone.GetComponent<BoxCollider2D>();
            if (collider != null)
            {
                Gizmos.color = new Color(1, 0, 0, 0.3f); // Red for opponent board
                Bounds bounds = collider.bounds;
                bounds.Expand(0.5f);
                Gizmos.DrawCube(bounds.center, bounds.size);
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }
    }
}