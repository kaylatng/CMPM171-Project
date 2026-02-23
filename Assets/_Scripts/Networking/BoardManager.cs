using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
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
    [Tooltip("Fixed X positions for the 3 slots (left, center, right). Cards stay in the slot they are placed in.")]
    [SerializeField] private float[] slotPositionsX = new float[] { -2f, 0f, 2f };

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

    // Attack phase: queue of attack data; played one by one with opponent attacks first
    private List<AttackResultData> pendingAttackResults = new List<AttackResultData>();

    // Opponent board sync from authoritative server state (so multiple cards / pairs show correctly)
    private PlayerNetwork cachedOpponentPlayer;
    private bool opponentBoardSyncSubscribed;

    // Opponent cards that were on board at end of last reveal (by slot index). New cards played this round stay face-down until next reveal.
    private List<int> revealedOpponentCardIdsBySlot = new List<int>();

    public struct AttackResultData
    {
        public ulong attackerClientId;
        public int slotIndex;
        public int damageDealt;
        public int chargesRemaining;
        public bool removeCard;
    }

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

    private void OnDestroy()
    {
        if (cachedOpponentPlayer != null && opponentBoardSyncSubscribed)
        {
            cachedOpponentPlayer.UnsubscribeFromDataChanged(OnOpponentPlayerDataChanged);
            opponentBoardSyncSubscribed = false;
            cachedOpponentPlayer = null;
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

        // Subscribe to opponent's board state once we have 2 clients (so opponent board stays in sync with server)
        EnsureOpponentBoardSyncSubscribed();
    }

    private void EnsureOpponentBoardSyncSubscribed()
    {
        if (opponentBoardSyncSubscribed || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
            return;
        if (NetworkManager.Singleton.ConnectedClientsList.Count < 2)
            return;

        ulong localId = NetworkManager.Singleton.LocalClientId;
        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
        {
            if (kvp.Key == localId) continue;
            var client = kvp.Value;
            if (client?.PlayerObject == null) continue;
            var otherPlayer = client.PlayerObject.GetComponent<PlayerNetwork>();
            if (otherPlayer == null) continue;

            cachedOpponentPlayer = otherPlayer;
            cachedOpponentPlayer.SubscribeToDataChanged(OnOpponentPlayerDataChanged);
            opponentBoardSyncSubscribed = true;
            // Initial sync so board matches server state (e.g. after reconnect or late join)
            SyncOpponentBoardFromServerState(cachedOpponentPlayer.GetBoardCards());
            Debug.Log($"BOARD MANAGER || Subscribed to opponent board sync (client {kvp.Key})");
            break;
        }
    }

    private void OnOpponentPlayerDataChanged(PlayerNetwork.PlayerData previous, PlayerNetwork.PlayerData next)
    {
        SyncOpponentBoardFromServerState(next.BoardCardIds);
    }

    /// <summary>Place a card on the opponent board from server state sync (does not remove from opponent hand).</summary>
    private void PlaceOpponentCardFromSync(int cardId, int index)
    {
        if (CardManager.Instance == null) return;
        GameObject card = Instantiate(CardManager.Instance.GetCardPrefab());
        if (card == null) return;
        // Face-up only if this slot had this same card at end of last reveal (already revealed). New cards stay face-down until next reveal.
        bool alreadyRevealed = index < revealedOpponentCardIdsBySlot.Count && revealedOpponentCardIdsBySlot[index] == cardId;
        CardManager.Instance.InitializeCardVisual(card, cardId, alreadyRevealed);
        CardDraggable draggable = card.GetComponent<CardDraggable>();
        if (draggable != null) { draggable.enabled = false; draggable.SetOnBoard(true); }
        CardShadow shadow = card.GetComponent<CardShadow>();
        if (shadow == null) card.AddComponent<CardShadow>();
        TryPlaceCard(card, false, index);
    }

    /// <summary>Sync opponent board from authoritative server state. Only replace cards that don't match so existing cards don't rerender/snap.</summary>
    private void SyncOpponentBoardFromServerState(FixedList32Bytes<int> boardCardIds)
    {
        if (boardCardIds.Length == 0)
        {
            ClearOpponentBoardCardsOnly();
            return;
        }

        // Check if current opponent board already matches (avoid any change)
        if (opponentBoardCards.Count == boardCardIds.Length)
        {
            bool match = true;
            for (int i = 0; i < opponentBoardCards.Count && i < boardCardIds.Length; i++)
            {
                var visual = opponentBoardCards[i].GetComponent<CardVisual>();
                if (visual == null || visual.CardID != boardCardIds[i]) { match = false; break; }
            }
            if (match)
            {
                ArrangeCardsOnBoard(false);
                return;
            }
        }

        // Incremental update: only remove/replace cards that don't match; keep matching cards so they don't rerender
        for (int i = 0; i < boardCardIds.Length; i++)
        {
            int needCardId = boardCardIds[i];
            if (needCardId < 0) continue;

            bool replacedSlot = false;
            if (i < opponentBoardCards.Count)
            {
                var visual = opponentBoardCards[i].GetComponent<CardVisual>();
                if (visual != null && visual.CardID == needCardId)
                    continue; // same card at same slot, keep it
                RemoveOneOpponentCardAt(i);
                replacedSlot = true; // card was swapped back to hand on server, so hand count unchanged
            }
            PlaceOpponentCardFromSync(needCardId, i);
            // New card from opponent hand (empty slot): remove one card from opponent hand on our view
            if (!replacedSlot && CardManager.Instance != null)
                CardManager.Instance.RemoveOneOpponentHandCard();
        }

        // Remove excess cards (server has fewer than we do)
        while (opponentBoardCards.Count > boardCardIds.Length)
            RemoveOneOpponentCardAt(opponentBoardCards.Count - 1);
    }

    /// <summary>Remove and destroy one opponent board card at the given index. Used for incremental sync.</summary>
    private void RemoveOneOpponentCardAt(int index)
    {
        if (index < 0 || index >= opponentBoardCards.Count) return;
        GameObject card = opponentBoardCards[index];
        opponentBoardCards.RemoveAt(index);
        cardBoardIndices.Remove(card);
        cardTargetPositions.Remove(card);
        if (card != null) Destroy(card);
        ArrangeCardsOnBoard(false);
    }

    private void ClearOpponentBoardCardsOnly()
    {
        List<GameObject> copy = new List<GameObject>(opponentBoardCards);
        foreach (GameObject card in copy)
        {
            opponentBoardCards.Remove(card);
            cardBoardIndices.Remove(card);
            cardTargetPositions.Remove(card);
            if (card != null) Destroy(card);
        }
        ArrangeCardsOnBoard(false);
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

        // Place new cards at target position immediately so they don't lerp from center
        if (cardTargetPositions.TryGetValue(card, out Vector3 targetPos))
            card.transform.localPosition = targetPos;

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

    /// <summary>Number of local player board cards currently with attack intent (tilt). Used for UI resource preview.</summary>
    public int GetLocalPendingAttackCount()
    {
        var seen = new HashSet<GameObject>();
        int count = 0;
        foreach (BoardSlot slot in playerSlots)
        {
            if (slot?.OccupyingCard == null) continue;
            if (!seen.Add(slot.OccupyingCard)) continue;
            var d = slot.OccupyingCard.GetComponent<CardDraggable>();
            if (d != null && d.IsAttacking) count++;
        }
        foreach (GameObject go in playerBoardCards)
        {
            if (go == null || !seen.Add(go)) continue;
            var d = go.GetComponent<CardDraggable>();
            if (d != null && d.IsAttacking) count++;
        }
        return count;
    }

    /// <summary>Submit all local attack intents to the server (call when player clicks Ready). Clears local isAttacking after each submit.</summary>
    public void SubmitLocalAttackIntents()
    {
        var seen = new HashSet<GameObject>();
        foreach (BoardSlot slot in playerSlots)
        {
            if (slot?.OccupyingCard == null) continue;
            if (!seen.Add(slot.OccupyingCard)) continue;
            var d = slot.OccupyingCard.GetComponent<CardDraggable>();
            if (d != null && d.IsAttacking)
                d.SubmitAttackIntent();
        }
        foreach (GameObject go in playerBoardCards)
        {
            if (go == null || !seen.Add(go)) continue;
            var d = go.GetComponent<CardDraggable>();
            if (d != null && d.IsAttacking)
                d.SubmitAttackIntent();
        }
    }

    /// <summary>
    /// Clear attack tilt on all player board cards. Call at start of reveal so cards are straight before animations.
    /// </summary>
    public void ClearAllAttackTilts()
    {
        // Player slots
        foreach (BoardSlot slot in playerSlots)
        {
            if (slot == null || slot.OccupyingCard == null) continue;
            CardVisual cv = slot.OccupyingCard.GetComponent<CardVisual>();
            if (cv != null)
                cv.SetScheduledToAttack(false);
            var d = slot.OccupyingCard.GetComponent<CardDraggable>();
            if (d != null)
                d.ClearAttackIntent();
        }
        // Player zone-placed cards
        foreach (GameObject card in playerBoardCards)
        {
            if (card == null) continue;
            CardVisual cv = card.GetComponent<CardVisual>();
            if (cv != null)
                cv.SetScheduledToAttack(false);
            var d = card.GetComponent<CardDraggable>();
            if (d != null)
                d.ClearAttackIntent();
        }
    }

    /// <summary>
    /// Called at start of reveal phase: clear tilts, then flip opponent cards, then merge, then (later) attack animations.
    /// </summary>
    public void StartRevealSequence()
    {
        ClearAllAttackTilts();
        StartCoroutine(RevealOpponentCardsThenMerge());
    }

    private IEnumerator RevealOpponentCardsThenMerge()
    {
        // Clear so we rebuild for this round; will capture at end of reveal
        revealedOpponentCardIdsBySlot.Clear();

        // Collect face-down cards per board, then flip opponent first (left-to-right), then player (left-to-right)
        List<GameObject> opponentFaceDown = new List<GameObject>();
        List<GameObject> playerFaceDown = new List<GameObject>();
        foreach (GameObject card in opponentBoardCards)
        {
            if (card == null) continue;
            CardVisual v = card.GetComponent<CardVisual>();
            if (v != null && v.IsFaceDown) opponentFaceDown.Add(card);
        }
        foreach (GameObject card in playerBoardCards)
        {
            if (card == null) continue;
            CardVisual v = card.GetComponent<CardVisual>();
            if (v != null && v.IsFaceDown) playerFaceDown.Add(card);
        }
        int SlotOrder(GameObject a, GameObject b)
        {
            int idxA = cardBoardIndices.TryGetValue(a, out int iA) ? iA : 0;
            int idxB = cardBoardIndices.TryGetValue(b, out int iB) ? iB : 0;
            return idxA.CompareTo(idxB);
        }
        opponentFaceDown.Sort(SlotOrder);
        playerFaceDown.Sort(SlotOrder);

        // Flip one by one: opponent cards first (left to right), then player cards (same order on host and client)
        foreach (GameObject card in opponentFaceDown)
        {
            if (card == null) continue;
            CardVisual visual = card.GetComponent<CardVisual>();
            if (visual != null && visual.IsFaceDown)
            {
                yield return StartCoroutine(visual.FlipToReveal(revealFlipDuration));
                yield return new WaitForSeconds(delayBetweenReveals);
            }
        }
        foreach (GameObject card in playerFaceDown)
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

        // Capture opponent board state so only these cards stay face-up next round; new cards stay face-down until next reveal
        revealedOpponentCardIdsBySlot.Clear();
        for (int i = 0; i < opponentBoardCards.Count; i++)
        {
            if (opponentBoardCards[i] == null) continue;
            var v = opponentBoardCards[i].GetComponent<CardVisual>();
            if (v != null)
                revealedOpponentCardIdsBySlot.Add(v.CardID);
        }
    }

    private void ArrangeCardsOnBoard(bool isPlayerBoard)
    {
        List<GameObject> cards = isPlayerBoard ? playerBoardCards : opponentBoardCards;
        Transform zone = isPlayerBoard ? playerBoardZone : opponentBoardZone;

        if (zone == null || cards.Count == 0)
        {
            return;
        }

        // Fixed slot positions for 3 slots: index 0 = left, 1 = center, 2 = right. No centering.
        float verticalOffset = isPlayerBoard ? -1.2f : 0f;
        for (int i = 0; i < cards.Count; i++)
        {
            GameObject card = cards[i];
            if (card == null) continue;

            // Skip cards that are in a BoardSlot—they are locked to the slot's position
            CardDraggable draggable = card.GetComponent<CardDraggable>();
            if (draggable != null && draggable.CurrentSlot != null)
            {
                cardBoardIndices[card] = i;
                continue;
            }

            float x = (slotPositionsX != null && i < slotPositionsX.Length) ? slotPositionsX[i] : (i == 0 ? -cardSpacing : (i == 1 ? 0f : cardSpacing));
            Vector3 targetPos = new Vector3(x, verticalOffset, 0f);
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
        // Smoothly lerp cards to their target positions (only cards in zone path; slot cards are locked to their slot)
        foreach (var kvp in cardTargetPositions)
        {
            GameObject card = kvp.Key;
            Vector3 targetPos = kvp.Value;

            if (card != null)
            {
                CardDraggable draggable = card.GetComponent<CardDraggable>();
                if (draggable != null && draggable.IsDragging)
                    continue; // Don't update position while dragging
                if (draggable != null && draggable.CurrentSlot != null)
                    continue; // Card is in a slot—slot controls position, don't move it here

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

    /// <summary>Get the card in a specific slot (slot-based board). Returns null if slot empty or not found.</summary>
    public GameObject GetCardInSlot(int slotIndex, bool isPlayerBoard)
    {
        List<BoardSlot> slots = isPlayerBoard ? playerSlots : opponentSlots;
        foreach (BoardSlot slot in slots)
        {
            if (slot != null && slot.SlotIndex == slotIndex)
                return slot.OccupyingCard;
        }
        return null;
    }

    /// <summary>Get board index (0,1,2) for a card placed via zone path. Returns -1 if not on board.</summary>
    public int GetCardBoardIndex(GameObject card, bool isPlayerBoard)
    {
        if (card == null) return -1;
        if (cardBoardIndices.TryGetValue(card, out int idx))
            return idx;
        List<GameObject> board = isPlayerBoard ? playerBoardCards : opponentBoardCards;
        int i = board.IndexOf(card);
        return i >= 0 ? i : -1;
    }

    /// <summary>Get card at board index (zone or slot path). Tries slot first, then zone list.</summary>
    public GameObject GetCardAtBoardIndex(int slotIndex, bool isPlayerBoard)
    {
        GameObject fromSlot = GetCardInSlot(slotIndex, isPlayerBoard);
        if (fromSlot != null) return fromSlot;
        List<GameObject> board = isPlayerBoard ? playerBoardCards : opponentBoardCards;
        if (slotIndex >= 0 && slotIndex < board.Count)
            return board[slotIndex];
        return null;
    }

    /// <summary>Called when a player schedules an attack in planning. Shows tilt only on the local player's cards; opponent's attack plan is hidden.</summary>
    public void OnAttackScheduled(ulong attackerClientId, int slotIndex)
    {
        bool isAttackerLocal = Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.LocalClientId == attackerClientId;
        if (!isAttackerLocal)
            return; // Opponent's cards do not tilt; hide their attack plan
        GameObject card = GetCardAtBoardIndex(slotIndex, true);
        if (card != null)
        {
            CardVisual cv = card.GetComponent<CardVisual>();
            if (cv != null)
                cv.SetScheduledToAttack(true);
        }
    }

    /// <summary>Queue one attack result (called from client RPC). Play with PlayAttacksSequence so opponent attacks first, one by one.</summary>
    public void ReceiveAttackData(ulong attackerClientId, int slotIndex, int damageDealt, int chargesRemaining, bool removeCard)
    {
        pendingAttackResults.Add(new AttackResultData
        {
            attackerClientId = attackerClientId,
            slotIndex = slotIndex,
            damageDealt = damageDealt,
            chargesRemaining = chargesRemaining,
            removeCard = removeCard
        });
    }

    /// <summary>Play all queued attacks one by one: opponent's attacks first, then local player's. Call after all ReceiveAttackData.</summary>
    public void PlayAttacksSequence()
    {
        StartCoroutine(PlayAttacksSequenceCoroutine());
    }

    private IEnumerator PlayAttacksSequenceCoroutine()
    {
        ulong localId = Unity.Netcode.NetworkManager.Singleton != null ? Unity.Netcode.NetworkManager.Singleton.LocalClientId : 0;
        // Opponent attacks first (attacker != local), then local player's attacks
        pendingAttackResults.Sort((a, b) =>
        {
            bool aOpponent = a.attackerClientId != localId;
            bool bOpponent = b.attackerClientId != localId;
            if (aOpponent && !bOpponent) return -1;
            if (!aOpponent && bOpponent) return 1;
            return 0;
        });
        foreach (var data in pendingAttackResults)
        {
            yield return StartCoroutine(PlaySingleAttackResultCoroutine(data.attackerClientId, data.slotIndex, data.damageDealt, data.chargesRemaining, data.removeCard));
        }
        pendingAttackResults.Clear();
    }

    /// <summary>Play one attack result in reveal phase: animate card hitting opponent then return, consume charge, remove if 0.</summary>
    public void PlaySingleAttackResult(ulong attackerClientId, int slotIndex, int damageDealt, int chargesRemaining, bool removeCard)
    {
        StartCoroutine(PlaySingleAttackResultCoroutine(attackerClientId, slotIndex, damageDealt, chargesRemaining, removeCard));
    }

    [Header("Attack Animation")]
    [SerializeField] private float attackFlyDuration = 0.35f;
    [SerializeField] private float attackReturnDuration = 0.25f;

    private IEnumerator PlaySingleAttackResultCoroutine(ulong attackerClientId, int slotIndex, int damageDealt, int chargesRemaining, bool removeCard)
    {
        bool isAttackerLocal = Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.LocalClientId == attackerClientId;
        GameObject card = GetCardAtBoardIndex(slotIndex, isAttackerLocal);
        if (card == null)
            yield break;

        Vector3 startPos = card.transform.position;
        Vector3 targetPos = isAttackerLocal && opponentBoardZone != null
            ? opponentBoardZone.position
            : (playerBoardZone != null ? playerBoardZone.position : startPos + new Vector3(0, 2f, 0));

        float elapsed = 0f;
        while (elapsed < attackFlyDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / attackFlyDuration;
            card.transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }
        card.transform.position = targetPos;

        elapsed = 0f;
        while (elapsed < attackReturnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / attackReturnDuration;
            card.transform.position = Vector3.Lerp(targetPos, startPos, t);
            yield return null;
        }
        card.transform.position = startPos;

        CardVisual cv = card.GetComponent<CardVisual>();
        if (cv != null)
        {
            cv.SetScheduledToAttack(false);
            cv.SetCharges(chargesRemaining);
        }

        if (removeCard && card != null)
        {
            BoardSlot slot = card.GetComponent<CardDraggable>()?.CurrentSlot;
            if (slot != null)
                slot.RemoveCard(notifyManager: true);
            else
                RemoveCardFromBoard(card, isAttackerLocal, notifyServer: false);
            Destroy(card);
        }
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

        // Face-up only if this slot had this same card at end of last reveal (already revealed). New cards stay face-down until next reveal.
        bool alreadyRevealed = index < revealedOpponentCardIdsBySlot.Count && revealedOpponentCardIdsBySlot[index] == cardId;
        GameObject card = Instantiate(CardManager.Instance.GetCardPrefab());
        if (card != null)
        {
            CardManager.Instance.InitializeCardVisual(card, cardId, alreadyRevealed);
            
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