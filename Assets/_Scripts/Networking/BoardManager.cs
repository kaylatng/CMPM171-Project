using UnityEngine;
using Unity.Netcode;
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

        // Arrange all cards
        ArrangeCardsOnBoard(isPlayerCard);

        Debug.Log($"BOARD MANAGER || Placed card on {(isPlayerCard ? "player" : "opponent")} board ({targetBoard.Count}/{maxCardsPerBoard})");

        // CHECK FOR MERGES after placing the card
        CheckAndMergeCards(isPlayerCard);

        // Network notification
        if (isPlayerCard)
        {
            NotifyServerCardPlaced(card);
        }

        return true;
    }

    /// <summary>
    /// Check for duplicate cards with MATCHING TIERS and merge them
    /// Merges into the LEFT-MOST card (first in the list)
    /// </summary>
    private void CheckAndMergeCards(bool isPlayerBoard)
    {
        List<GameObject> currentBoard = isPlayerBoard ? playerBoardCards : opponentBoardCards;
        
        Debug.Log($"BOARD MANAGER || Checking for merges on {(isPlayerBoard ? "player" : "opponent")} board - {currentBoard.Count} cards");
        
        // Build a dictionary of (assetID, tier) -> list of cards
        Dictionary<(int assetId, int tier), List<GameObject>> cardsByIdAndTier = new Dictionary<(int, int), List<GameObject>>();

        for (int i = 0; i < currentBoard.Count; i++)
        {
            CardVisual visual = currentBoard[i].GetComponent<CardVisual>();
            if (visual != null)
            {
                int poolId = visual.CardID;
                int assetId = library.GetMappedAssetID(poolId);
                
                // Get the ACTUAL current tier from the card (not the base tier)
                int tier = visual.GetCurrentTier();
                
                Debug.Log($"  Card {i}: Pool ID {poolId} → Asset ID {assetId}, Current Tier: {tier}");
                
                var key = (assetId, tier);
                if (!cardsByIdAndTier.ContainsKey(key))
                {
                    cardsByIdAndTier[key] = new List<GameObject>();
                }
                
                cardsByIdAndTier[key].Add(currentBoard[i]);
            }
        }

        // Find duplicates and merge them (same asset ID AND same tier)
        foreach (var kvp in cardsByIdAndTier)
        {
            (int assetId, int tier) = kvp.Key;
            List<GameObject> cardsWithSameIdAndTier = kvp.Value;

            // If we have 2 or more cards with the same asset ID AND tier, merge them
            if (cardsWithSameIdAndTier.Count >= 2)
            {
                Debug.Log($"BOARD MANAGER || Found {cardsWithSameIdAndTier.Count} cards with asset ID {assetId} and tier {tier} - merging!");
                
                // Log each card's tier for verification
                foreach (var card in cardsWithSameIdAndTier)
                {
                    CardVisual v = card.GetComponent<CardVisual>();
                    if (v != null)
                    {
                        Debug.Log($"  - Card {card.name}: Asset ID {assetId}, Tier {v.GetCurrentTier()}");
                    }
                }
                
                // Merge into the LEFT-MOST card (first in the list)
                GameObject targetCard = cardsWithSameIdAndTier[0]; // LEFTMOST (target)
                GameObject cardToMerge = cardsWithSameIdAndTier[cardsWithSameIdAndTier.Count - 1]; // RIGHTMOST (will fly to left)

                StartCoroutine(PerformMerge(cardToMerge, targetCard, isPlayerBoard));
                
                // Only merge one pair at a time to avoid complexity
                break;
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

            // Set sorting order
            SpriteRenderer sr = card.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
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

        // Create the card
        GameObject card = Instantiate(CardManager.Instance.GetCardPrefab());
        if (card != null)
        {
            CardManager.Instance.InitializeCardVisual(card, cardId, true);
            
            CardDraggable draggable = card.GetComponent<CardDraggable>();
            if (draggable != null)
            {
                draggable.enabled = false;
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