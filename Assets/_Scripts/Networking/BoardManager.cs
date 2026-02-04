using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance;

    [Header("Board Settings")]
    [SerializeField] private int maxCardsPerBoard = 3;
    
    [Header("Board Zones")]
    [SerializeField] private Transform playerBoardZone;
    [SerializeField] private Transform opponentBoardZone;

    [Header("Card Layout")]
    [SerializeField] private float cardSpacing = 2.0f;
    [SerializeField] private float cardMoveSpeed = 12f;
    [SerializeField] private AnimationCurve layoutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

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

        // Notify network if needed
        if (isPlayerCard)
        {
            NotifyServerCardPlaced(card);
        }

        return true;
    }

    private void ArrangeCardsOnBoard(bool isPlayerBoard)
    {
        List<GameObject> cards = isPlayerBoard ? playerBoardCards : opponentBoardCards;
        Transform zone = isPlayerBoard ? playerBoardZone : opponentBoardZone;

        if (cards.Count == 0) return;

        // Calculate total width and starting position
        float totalWidth = (cards.Count - 1) * cardSpacing;
        float startX = -totalWidth / 2f;

        for (int i = 0; i < cards.Count; i++)
        {
            GameObject card = cards[i];
            if (card == null) continue;

            // Calculate target position
            float xPos = startX + (i * cardSpacing);
            Vector3 targetPos = zone.position + new Vector3(xPos, 0, 0);

            // Store target position for smooth movement
            cardTargetPositions[card] = targetPos;
            cardBoardIndices[card] = i;

            // Reset rotation and scale
            card.transform.rotation = Quaternion.identity;
            card.transform.localScale = Vector3.one;

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
        foreach (var kvp in cardTargetPositions)
        {
            GameObject card = kvp.Key;
            Vector3 targetPos = kvp.Value;

            if (card == null) continue;

            // Smooth movement to target position
            card.transform.position = Vector3.Lerp(
                card.transform.position,
                targetPos,
                Time.deltaTime * cardMoveSpeed
            );
        }
    }

    public void RemoveCardFromBoard(GameObject card, bool isPlayerBoard, bool notifyServer = true)
    {
        if (card == null) return;

        List<GameObject> cards = isPlayerBoard ? playerBoardCards : opponentBoardCards;

        if (cards.Contains(card))
        {
            cards.Remove(card);
            cardTargetPositions.Remove(card);
            cardBoardIndices.Remove(card);

            // Rearrange remaining cards
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