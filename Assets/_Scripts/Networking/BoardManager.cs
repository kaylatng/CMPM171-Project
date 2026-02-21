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

    [Header("Card Layout (legacy / NON-slot cards)")]
    
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

    // Legacy lists 
    private readonly List<GameObject> playerBoardCards = new();
    private readonly List<GameObject> opponentBoardCards = new();

    // Legacy smooth motion 
    private readonly Dictionary<GameObject, Vector3> cardTargetPositions = new();
    private readonly Dictionary<GameObject, int> cardBoardIndices = new();

    private SpriteRenderer playerZoneRenderer;
    private SpriteRenderer opponentZoneRenderer;

    // Slots
    private readonly List<BoardSlot> playerSlots = new();
    private readonly List<BoardSlot> opponentSlots = new();

    private bool isMerging = false;
    private Coroutine revealRoutine;

    private struct MergeKey
    {
        public int cardID;
        public int tier;

        public MergeKey(int id, int t)
        {
            cardID = id;
            tier = t;
        }
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        SetupBoardZones();
        SetupZoneVisuals();
        RefreshSlotsFromScene();
        Debug.Log($"BUILD CHECK || Scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} || BoardManager={Application.version} || {Application.unityVersion}");
    }

    private void Update()
    {
        UpdateCardPositions();   // ignores slot cards
        UpdateSlotBlinking();    // controls slot guide blinking
    }

    // Slot registration / blinking
    
    public void RegisterSlot(BoardSlot slot)
    {
        if (slot == null) return;

        if (slot.IsPlayerSlot)
        {
            if (!playerSlots.Contains(slot)) playerSlots.Add(slot);
        }
        else
        {
            if (!opponentSlots.Contains(slot)) opponentSlots.Add(slot);
        }
    }

   
private bool slotsInitialized = false;

private void RefreshSlotsFromScene()
{
    playerSlots.Clear();
    opponentSlots.Clear();

    // Find zones by StartsWith
    if (playerBoardZone == null)
    {
        var p = GameObject.FindObjectsOfType<Transform>(true)
            .FirstOrDefault(t => t.name.StartsWith("PlayerBoardZone"));
        playerBoardZone = p;
    }

    if (opponentBoardZone == null)
    {
        var o = GameObject.FindObjectsOfType<Transform>(true)
            .FirstOrDefault(t => t.name.StartsWith("OpponentBoardZone"));
        opponentBoardZone = o;
    }

    // Find all slots in the scene 
    var allSlots = FindObjectsByType<BoardSlot>(FindObjectsSortMode.None);

    foreach (var slot in allSlots)
    {
        if (slot == null) continue;

        bool? isPlayer = GetIsPlayerSlotByHierarchy(slot.transform);
        if (isPlayer == true) playerSlots.Add(slot);
        else if (isPlayer == false) opponentSlots.Add(slot);
    }

    // stable ordering
    playerSlots.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
    opponentSlots.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));

    slotsInitialized = true;
    Debug.Log($"BOARD MANAGER || RefreshSlotsFromScene -> playerSlots={playerSlots.Count} opponentSlots={opponentSlots.Count}");
}

private bool? GetIsPlayerSlotByHierarchy(Transform t)
{
    while (t != null)
    {
        if (t.name.StartsWith("PlayerBoardZone")) return true;
        if (t.name.StartsWith("OpponentBoardZone")) return false;
        t = t.parent;
    }
    return null; // couldn't classify
}

    private void UpdateSlotBlinking()
    {
        bool shouldBlink = false;

        // If a card is selected and not already on board, blink
        if (CardDraggable.SelectedCard != null)
        {
            CardDraggable selected = CardDraggable.SelectedCard;
            if (!selected.IsOnBoard)
            {
                if (!IsCardOnBoard(selected.gameObject, true))
                    shouldBlink = true;
            }
        }

        // If any hand card is actively being dragged, blink
        if (!shouldBlink)
        {
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
        }

        foreach (BoardSlot slot in playerSlots)
        {
            if (slot != null) slot.SetBlinking(shouldBlink);
        }
    }

    // Zones / visuals
 
    private void SetupBoardZones()
    {
        if (playerBoardZone == null)
        {
            GameObject pZone = GameObject.Find("PlayerBoardZone");
            if (pZone != null) playerBoardZone = pZone.transform;
        }

        if (opponentBoardZone == null)
        {
            GameObject oZone = GameObject.Find("OpponentBoardZone");
            if (oZone != null) opponentBoardZone = oZone.transform;
        }
    }

    private void SetupZoneVisuals()
    {
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
            playerZoneRenderer.enabled = false;

            BoxCollider2D collider = playerBoardZone.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = playerBoardZone.gameObject.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(8f, 1f);
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
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
    }
private Coroutine flashRoutine;

public void ShowValidDropFeedback(bool isPlayerBoard)
{
    SpriteRenderer renderer = isPlayerBoard ? playerZoneRenderer : opponentZoneRenderer;
    if (renderer != null)
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashZoneColor(renderer, validDropColor));
    }
}

public void ShowInvalidDropFeedback(bool isPlayerBoard)
{
    SpriteRenderer renderer = isPlayerBoard ? playerZoneRenderer : opponentZoneRenderer;
    if (renderer != null)
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashZoneColor(renderer, invalidDropColor));
    }
}
    private IEnumerator FlashZoneColor(SpriteRenderer renderer, Color flashColor)
    {
        Color original = renderer.color;
        renderer.color = flashColor;
        yield return new WaitForSeconds(0.3f);
        renderer.color = original;
    }

    // CardDraggable compatibility

    public bool CanPlaceCardOnBoard(bool isPlayerCard)
    {
        var slots = isPlayerCard ? playerSlots : opponentSlots;
        int occupied = slots.Count(s => s != null && s.IsOccupied);
        return occupied < maxCardsPerBoard;
    }

    public bool IsPositionOverBoard(Vector3 worldPosition, out bool isPlayerBoard)
    {
        isPlayerBoard = true;

        if (playerBoardZone != null)
        {
            BoxCollider2D collider = playerBoardZone.GetComponent<BoxCollider2D>();
            if (collider != null)
            {
                Bounds bounds = collider.bounds;
                bounds.Expand(0.5f);
                if (bounds.Contains(worldPosition))
                {
                    isPlayerBoard = true;
                    return true;
                }
            }
        }

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
                    return true;
                }
            }
        }

        return false;
    }

    public bool TryPlaceCard(GameObject card, bool isPlayerCard, int? preferredIndex = null)
    {
        if (card == null) return false;

        var slots = isPlayerCard ? playerSlots : opponentSlots;
        if (slots.Count == 0)
        {
            Debug.LogWarning("BOARD MANAGER || No slots registered; cannot place card via TryPlaceCard.");
            return false;
        }

        // Find preferred slot by SlotIndex, else first empty
        BoardSlot target = null;

        if (preferredIndex.HasValue)
            target = slots.FirstOrDefault(s => s != null && s.SlotIndex == preferredIndex.Value);

        if (target == null)
            target = slots.FirstOrDefault(s => s != null && !s.IsOccupied);

        if (target == null)
        {
            ShowInvalidDropFeedback(isPlayerCard);
            return false;
        }

        target.PlaceCard(card);
        return true;
    }

    // PlayerNetwork compatibility
    public List<GameObject> GetBoardCards(bool isPlayerBoard)
    {
    
        var slots = isPlayerBoard ? playerSlots : opponentSlots;
        if (slots != null && slots.Count > 0)
        {
            return slots
                .Where(s => s != null && s.IsOccupied && s.OccupyingCard != null)
                .OrderBy(s => s.SlotIndex)
                .Select(s => s.OccupyingCard)
                .ToList();
        }

        return isPlayerBoard ? new List<GameObject>(playerBoardCards) : new List<GameObject>(opponentBoardCards);
    }

    public bool IsCardOnBoard(GameObject card, bool isPlayerBoard)
    {
        if (card == null) return false;

        // Prefer slot truth if registered
        var slots = isPlayerBoard ? playerSlots : opponentSlots;
        if (slots != null && slots.Count > 0)
        {
            return slots.Any(s => s != null && s.OccupyingCard == card);
        }

        return isPlayerBoard ? playerBoardCards.Contains(card) : opponentBoardCards.Contains(card);
    }

    public void ReturnCardToHand(GameObject card)
    {
        if (card == null) return;

        // Clear slot link if any
        var cd = card.GetComponent<CardDraggable>();
        if (cd != null) cd.SetCurrentSlot(null);

        Transform handZone = GameObject.Find("PlayerHandZone")?.transform;
        if (handZone != null)
        {
            card.transform.SetParent(handZone, false);
        }
    }

    // Slot events 

    public void OnCardPlacedInSlot(BoardSlot slot, GameObject card, bool shouldNotifyServer = true)
    {
        if (slot == null || card == null) return;

     
        var cd = card.GetComponent<CardDraggable>();
        if (cd != null) cd.SetOnBoard(true);

        // Keep legacy lists in sync
        if (slot.IsPlayerSlot)
        {
            if (!playerBoardCards.Contains(card)) playerBoardCards.Add(card);
            opponentBoardCards.Remove(card);
        }
        else
        {
            if (!opponentBoardCards.Contains(card)) opponentBoardCards.Add(card);
            playerBoardCards.Remove(card);
        }

        // Sorting 
        var cv = card.GetComponent<CardVisual>();
        if (cv != null)
        {
            cv.SetSortingOrder(slot.SlotIndex * 100);

            // Enforce face state by side
            if (slot.IsPlayerSlot)
            {
                cv.SetFaceDown(false);
                cv.RefreshFrame();
            }
            else
            {
                // opponent cards hidden until reveal phase
                cv.SetFaceDown(true);
            }
        }

        // Networking
        if (slot.IsPlayerSlot && shouldNotifyServer)
        {
            NotifyServerCardPlaced(card, slot.SlotIndex);
        }

        // Player merges happen immediately on placement
        if (slot.IsPlayerSlot)
        {
            CheckAndMergePlayerSlots();
        }
    }

    public void OnCardRemovedFromSlot(BoardSlot slot, GameObject card)
    {
        if (slot == null || card == null) return;

        playerBoardCards.Remove(card);
        opponentBoardCards.Remove(card);

        cardTargetPositions.Remove(card);
        cardBoardIndices.Remove(card);
    }

    // Reveal phase
    
   public void StartRevealSequence()
{
    if (revealRoutine != null) StopCoroutine(revealRoutine);
    revealRoutine = StartCoroutine(WaitThenReveal());
}

private IEnumerator WaitThenReveal()
{
    RefreshSlotsFromScene();

    float timeout = 1.0f;
    float elapsed = 0f;

    while (elapsed < timeout)
    {
        int occ = opponentSlots.Count(s => s != null && s.IsOccupied && s.OccupyingCard != null);
        if (occ > 0) break;

        elapsed += Time.deltaTime;
        yield return null;
    }

    RefreshSlotsFromScene();

    int finalOcc = opponentSlots.Count(s => s != null && s.IsOccupied && s.OccupyingCard != null);
    Debug.Log($"REVEAL SEQ (after wait) client {NetworkManager.Singleton.LocalClientId} | occupiedOpp={finalOcc}");

    yield return RevealOpponentCardsThenMerge();
}

    private IEnumerator RevealOpponentCardsThenMerge()
    {
        var oppCards = opponentSlots
            .Where(s => s != null && s.IsOccupied && s.OccupyingCard != null)
            .OrderBy(s => s.SlotIndex)
            .Select(s => s.OccupyingCard)
            .ToList();

        foreach (GameObject card in oppCards)
        {
            if (card == null) continue;

            CardVisual visual = card.GetComponent<CardVisual>();
            if (visual != null && visual.IsFaceDown)
            {
                yield return StartCoroutine(visual.FlipToReveal(revealFlipDuration));
                yield return new WaitForSeconds(delayBetweenReveals);
            }
            else if (visual != null)
            {
            
                visual.SetFaceDown(false);
                visual.RefreshFrame();
            }
        }

        yield return new WaitForSeconds(0.15f);

        // opponent merges after reveal
        CheckAndMergeOpponentSlots();
    }

    // Merge logic
   
    private void CheckAndMergePlayerSlots()
    {
        if (isMerging) return;

        var occupied = playerSlots
            .Where(s => s != null && s.IsOccupied && s.OccupyingCard != null)
            .OrderBy(s => s.SlotIndex)
            .ToList();

        if (occupied.Count < 2) return;

        var mergeGroup = occupied
            .GroupBy(s =>
            {
                var cv = s.OccupyingCard.GetComponent<CardVisual>();
                var data = cv != null ? cv.CurrentCardData : null;
                if (data == null) return new MergeKey(-1, -1);
                return new MergeKey(data.cardID, data.tier);
            })
            .FirstOrDefault(g => g.Key.cardID != -1 && g.Count() >= 2);

        if (mergeGroup == null) return;

        var group = mergeGroup.OrderBy(s => s.SlotIndex).ToList();
        var targetSlot = group[0];                 // leftmost
        var fromSlot = group[group.Count - 1];     // rightmost

        StartCoroutine(PerformPlayerSlotMerge(fromSlot, targetSlot));
    }

    private IEnumerator PerformPlayerSlotMerge(BoardSlot fromSlot, BoardSlot toSlot)
    {
        if (isMerging) yield break;
        isMerging = true;

        GameObject cardToMerge = fromSlot.OccupyingCard;
        GameObject targetCard = toSlot.OccupyingCard;

        if (cardToMerge == null || targetCard == null)
        {
            isMerging = false;
            yield break;
        }

        var targetVisual = targetCard.GetComponent<CardVisual>();
        if (targetVisual == null || targetVisual.CurrentCardData == null)
        {
            isMerging = false;
            yield break;
        }

        CardData currentData = targetVisual.CurrentCardData;

    
        cardToMerge.transform.SetParent(null, true);

        // Render above during flight
        var mergeCV = cardToMerge.GetComponent<CardVisual>();
        if (mergeCV != null) mergeCV.SetSortingOrder((toSlot.SlotIndex * 100) + 50);

        Vector3 start = cardToMerge.transform.position;
        Vector3 end = targetCard.transform.position;

        Vector3 mergeStartScale = cardToMerge.transform.localScale;
        Vector3 targetStartScale = targetCard.transform.localScale;

        SpriteRenderer mergeSR = cardToMerge.GetComponent<SpriteRenderer>();
        SpriteRenderer targetSR = targetCard.GetComponent<SpriteRenderer>();

        Color mergeOriginalColor = mergeSR != null ? mergeSR.color : Color.white;
        Color targetOriginalColor = targetSR != null ? targetSR.color : Color.white;

        float elapsed = 0f;
        while (elapsed < mergeAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / mergeAnimationDuration);
            float curveT = mergeScaleCurve.Evaluate(t);

            if (cardToMerge != null)
            {
                cardToMerge.transform.position = Vector3.Lerp(start, end, t);
                cardToMerge.transform.localScale = mergeStartScale;
            }

            targetCard.transform.localScale = Vector3.Lerp(targetStartScale, targetStartScale * mergeScalePulse, curveT);

            if (mergeSR != null) mergeSR.color = Color.Lerp(mergeOriginalColor, mergeFlashColor, t);
            if (targetSR != null) targetSR.color = Color.Lerp(targetOriginalColor, mergeFlashColor, curveT);

            yield return null;
        }

        fromSlot.RemoveCard(notifyManager: true);
        if (cardToMerge != null) Destroy(cardToMerge);

        if (currentData.nextTier != null)
        {
            targetVisual.Initialize(targetVisual.CardID, currentData.nextTier);
            targetVisual.SetSortingOrder(toSlot.SlotIndex * 100);

            targetVisual.SetFaceDown(false);
            targetVisual.RefreshFrame();
        }
        else
        {
            if (targetSR != null) targetSR.color = targetOriginalColor;
        }

        targetCard.transform.localScale = targetStartScale;

        isMerging = false;

        yield return null;
        CheckAndMergePlayerSlots(); // chain merges
    }

    private void CheckAndMergeOpponentSlots()
    {
        if (isMerging) return;

        var occupied = opponentSlots
            .Where(s => s != null && s.IsOccupied && s.OccupyingCard != null)
            .OrderBy(s => s.SlotIndex)
            .ToList();

        if (occupied.Count < 2) return;

        var mergeGroup = occupied
            .GroupBy(s =>
            {
                var cv = s.OccupyingCard.GetComponent<CardVisual>();
                var data = cv != null ? cv.CurrentCardData : null;
                if (data == null) return new MergeKey(-1, -1);
                return new MergeKey(data.cardID, data.tier);
            })
            .FirstOrDefault(g => g.Key.cardID != -1 && g.Count() >= 2);

        if (mergeGroup == null) return;

        var group = mergeGroup.OrderBy(s => s.SlotIndex).ToList();
        var targetSlot = group[0];                 // leftmost
        var fromSlot = group[group.Count - 1];     // rightmost

        StartCoroutine(PerformOpponentSlotMerge(fromSlot, targetSlot));
    }

    private IEnumerator PerformOpponentSlotMerge(BoardSlot fromSlot, BoardSlot toSlot)
    {
        if (isMerging) yield break;
        isMerging = true;

        GameObject cardToMerge = fromSlot.OccupyingCard;
        GameObject targetCard = toSlot.OccupyingCard;

        if (cardToMerge == null || targetCard == null)
        {
            isMerging = false;
            yield break;
        }

        var targetVisual = targetCard.GetComponent<CardVisual>();
        if (targetVisual == null || targetVisual.CurrentCardData == null)
        {
            isMerging = false;
            yield break;
        }

        CardData currentData = targetVisual.CurrentCardData;

        cardToMerge.transform.SetParent(null, true);

        var mergeCV = cardToMerge.GetComponent<CardVisual>();
        if (mergeCV != null) mergeCV.SetSortingOrder((toSlot.SlotIndex * 100) + 50);

        Vector3 start = cardToMerge.transform.position;
        Vector3 end = targetCard.transform.position;

        Vector3 mergeStartScale = cardToMerge.transform.localScale;
        Vector3 targetStartScale = targetCard.transform.localScale;

        SpriteRenderer mergeSR = cardToMerge.GetComponent<SpriteRenderer>();
        SpriteRenderer targetSR = targetCard.GetComponent<SpriteRenderer>();

        Color mergeOriginalColor = mergeSR != null ? mergeSR.color : Color.white;
        Color targetOriginalColor = targetSR != null ? targetSR.color : Color.white;

        float elapsed = 0f;
        while (elapsed < mergeAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / mergeAnimationDuration);
            float curveT = mergeScaleCurve.Evaluate(t);

            if (cardToMerge != null)
            {
                cardToMerge.transform.position = Vector3.Lerp(start, end, t);
                cardToMerge.transform.localScale = mergeStartScale;
            }

            targetCard.transform.localScale = Vector3.Lerp(targetStartScale, targetStartScale * mergeScalePulse, curveT);

            if (mergeSR != null) mergeSR.color = Color.Lerp(mergeOriginalColor, mergeFlashColor, t);
            if (targetSR != null) targetSR.color = Color.Lerp(targetOriginalColor, mergeFlashColor, curveT);

            yield return null;
        }

        fromSlot.RemoveCard(notifyManager: true);
        if (cardToMerge != null) Destroy(cardToMerge);

        if (currentData.nextTier != null)
        {
            targetVisual.Initialize(targetVisual.CardID, currentData.nextTier);
            targetVisual.SetSortingOrder(toSlot.SlotIndex * 100);

            // After reveal, opponent should remain face up
            targetVisual.SetFaceDown(false);
            targetVisual.RefreshFrame();
        }
        else
        {
            if (targetSR != null) targetSR.color = targetOriginalColor;
        }

        targetCard.transform.localScale = targetStartScale;

        isMerging = false;

        yield return null;
        CheckAndMergeOpponentSlots(); // chain merges 
    }

    // Legacy motion (non-slots)
   
    private void UpdateCardPositions()
    {
        foreach (var kvp in cardTargetPositions.ToList())
        {
            GameObject card = kvp.Key;
            Vector3 targetPos = kvp.Value;
            if (card == null) continue;

            CardDraggable draggable = card.GetComponent<CardDraggable>();
            if (draggable != null)
            {
                if (draggable.IsDragging) continue;
                if (draggable.CurrentSlot != null) continue; 
            }

            card.transform.localPosition = Vector3.Lerp(
                card.transform.localPosition,
                targetPos,
                Time.deltaTime * cardMoveSpeed
            );
        }
    }

    // Networking
 
    private void NotifyServerCardPlaced(GameObject card, int slotIndex)
    {
        CardVisual visual = card.GetComponent<CardVisual>();
        if (visual == null) return;

        int cardId = visual.CardID;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject?.GetComponent<PlayerNetwork>();
            if (localPlayer != null)
            {
                localPlayer.PlayCardToSlotServerRpc(cardId, slotIndex);
            }
        }
    }

    // Called by PlayerNetwork (opponent plays a card)
    public void PlaceOpponentCard(int cardId, int index)
    {
        RefreshSlotsFromScene();

        if (CardManager.Instance == null)
        {
            Debug.LogError("BOARD MANAGER || CardManager.Instance is null!");
            return;
        }

        CardManager.Instance.RemoveOneOpponentHandCard();

        GameObject card = Instantiate(CardManager.Instance.GetCardPrefab());
        if (card == null) return;

        // false => opponent card (face down initially)
        CardManager.Instance.InitializeCardVisual(card, cardId, false);

        CardDraggable draggable = card.GetComponent<CardDraggable>();
        if (draggable != null)
        {
            draggable.enabled = false;
            draggable.SetOnBoard(true);
        }

        CardShadow shadow = card.GetComponent<CardShadow>();
        if (shadow == null) shadow = card.AddComponent<CardShadow>();


        BoardSlot slot = opponentSlots.FirstOrDefault(s => s != null && s.SlotIndex == index);
        if (slot != null)
        {
            slot.PlaceCard(card);
        }
        else
        {
            TryPlaceCard(card, false, index);
        }
    }

    // Gizmos 
 
    private void OnDrawGizmos()
    {
        if (playerBoardZone != null)
        {
            BoxCollider2D collider = playerBoardZone.GetComponent<BoxCollider2D>();
            if (collider != null)
            {
                Gizmos.color = new Color(0, 1, 0, 0.3f);
                Bounds bounds = collider.bounds;
                bounds.Expand(0.5f);
                Gizmos.DrawCube(bounds.center, bounds.size);
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }

        if (opponentBoardZone != null)
        {
            BoxCollider2D collider = opponentBoardZone.GetComponent<BoxCollider2D>();
            if (collider != null)
            {
                Gizmos.color = new Color(1, 0, 0, 0.3f);
                Bounds bounds = collider.bounds;
                bounds.Expand(0.5f);
                Gizmos.DrawCube(bounds.center, bounds.size);
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }
    }
}