using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance;

    [Header("Board Settings")]
    [SerializeField] private int slotsPerPlayer = 3;
    
    [Header("Board Zones")]
    [SerializeField] private Transform playerBoardZone;
    [SerializeField] private Transform opponentBoardZone;

    [Header("Slot Prefab")]
    [SerializeField] private GameObject slotPrefab;

    private List<BoardSlot> playerSlots = new List<BoardSlot>();
    private List<BoardSlot> opponentSlots = new List<BoardSlot>();

    private Dictionary<int, GameObject> playerBoardCards = new Dictionary<int, GameObject>();
    private Dictionary<int, GameObject> opponentBoardCards = new Dictionary<int, GameObject>();

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
        CreateBoardSlots();
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

    private void CreateBoardSlots()
    {
        if (slotPrefab == null)
        {
            Debug.LogWarning("BOARD MANAGER || Slot prefab not assigned. Slots will need to be created manually.");
            return;
        }

        // player slots
        CreateSlotsForZone(playerBoardZone, true, playerSlots);
        
        // opponent slots
        CreateSlotsForZone(opponentBoardZone, false, opponentSlots);

        Debug.Log($"BOARD MANAGER || Created {slotsPerPlayer} slots for each player");
    }

    private void CreateSlotsForZone(Transform zone, bool isPlayerZone, List<BoardSlot> slotList)
    {
        if (zone == null) return;

        int slotLayer = zone.gameObject.layer;

        for (int i = 0; i < slotsPerPlayer; i++)
        {
            GameObject slotObj = Instantiate(slotPrefab, zone);
            slotObj.name = $"{(isPlayerZone ? "Player" : "Opponent")}Slot_{i}";

            slotObj.layer = slotLayer;

            BoxCollider2D collider = slotObj.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = slotObj.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(1.5f, 2f);
            }
            
            BoardSlot slot = slotObj.GetComponent<BoardSlot>();
            if (slot != null)
            {
#if UNITY_EDITOR
                slot.SetSlotProperties(i, isPlayerZone);
#endif
                slotList.Add(slot);
            }

            float spacing = 2f;
            float startX = -(slotsPerPlayer - 1) * spacing / 2f;
            slotObj.transform.localPosition = new Vector3(startX + i * spacing, 0, 0);
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
            }
        }
        else
        {
            if (!opponentSlots.Contains(slot))
            {
                opponentSlots.Add(slot);
            }
        }
    }

    public void OnCardPlacedInSlot(BoardSlot slot, GameObject card, bool shouldNotifyServer = true)
    {
        if (slot == null || card == null) return;

        CardVisual cardVisual = card.GetComponent<CardVisual>();
        if (cardVisual == null) return;

        if (slot.IsPlayerSlot)
        {
            playerBoardCards[slot.SlotIndex] = card;

            if (shouldNotifyServer)
            {
                CardDraggable draggable = card.GetComponent<CardDraggable>();
                if (draggable != null)
                {
                    draggable.PlayCard();
                }
            }
        }
        else
        {
            opponentBoardCards[slot.SlotIndex] = card;
        }

        if (slot.IsPlayerSlot)
        {
            CardDraggable draggable = card.GetComponent<CardDraggable>();
            if (draggable != null)
            {
                draggable.PlayCard();
            }
        }

        Debug.Log($"BOARD MANAGER || Card {cardVisual.CardID} placed in {(slot.IsPlayerSlot ? "Player" : "Opponent")} slot {slot.SlotIndex}");
    }

    public void OnCardRemovedFromSlot(BoardSlot slot, GameObject card)
    {
        if (slot == null) return;

        if (slot.IsPlayerSlot)
        {
            playerBoardCards.Remove(slot.SlotIndex);
        }
        else
        {
            opponentBoardCards.Remove(slot.SlotIndex);
        }

        Debug.Log($"BOARD MANAGER || Card removed from {(slot.IsPlayerSlot ? "Player" : "Opponent")} slot {slot.SlotIndex}");
    }

    public void ReturnCardToHand(GameObject card)
    {
        if (card == null) return;

        if (CardManager.Instance != null)
        {
            Transform handZone = GameObject.Find("PlayerHandZone")?.transform;
            if (handZone != null)
            {
                card.transform.SetParent(handZone);
                card.transform.localPosition = Vector3.zero;
            }
        }

        Debug.Log($"BOARD MANAGER || Card returned to hand");
    }

    public List<GameObject> GetPlayerBoardCards()
    {
        List<GameObject> cards = new List<GameObject>();
        foreach (var kvp in playerBoardCards)
        {
            if (kvp.Value != null)
            {
                cards.Add(kvp.Value);
            }
        }
        return cards;
    }

    public List<GameObject> GetOpponentBoardCards()
    {
        List<GameObject> cards = new List<GameObject>();
        foreach (var kvp in opponentBoardCards)
        {
            if (kvp.Value != null)
            {
                cards.Add(kvp.Value);
            }
        }
        return cards;
    }

    public GameObject GetCardInSlot(int slotIndex, bool isPlayerSlot)
    {
        var dict = isPlayerSlot ? playerBoardCards : opponentBoardCards;
        if (dict.TryGetValue(slotIndex, out GameObject card))
        {
            return card;
        }
        return null;
    }

    public bool IsSlotOccupied(int slotIndex, bool isPlayerSlot)
    {
        return GetCardInSlot(slotIndex, isPlayerSlot) != null;
    }

    public void ClearBoard()
    {
        foreach (var slot in playerSlots)
        {
            if (slot != null)
            {
                slot.ClearSlot();
            }
        }

        foreach (var slot in opponentSlots)
        {
            if (slot != null)
            {
                slot.ClearSlot();
            }
        }

        playerBoardCards.Clear();
        opponentBoardCards.Clear();

        Debug.Log("BOARD MANAGER || Board cleared");
    }

    public void PlaceOpponentCard(int cardId, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= opponentSlots.Count) return;

        BoardSlot targetSlot = opponentSlots[slotIndex];
        if (targetSlot == null) return;

        if (CardManager.Instance != null)
        {
            GameObject card = CardManager.Instance.SpawnCard(cardId, false);
            if (card != null)
            {
                CardDraggable draggable = card.GetComponent<CardDraggable>();
                if (draggable != null)
                {
                    draggable.enabled = false;
                }
                targetSlot.PlaceCard(card);
            }
        }

        Debug.Log($"BOARD MANAGER || Opponent placed card {cardId} in slot {slotIndex}");
    }

    public BoardSlot GetSlot(int slotIndex, bool isPlayerSlot)
    {
        var slots = isPlayerSlot ? playerSlots : opponentSlots;
        if (slotIndex >= 0 && slotIndex < slots.Count)
        {
            return slots[slotIndex];
        }
        return null;
    }

    public int GetPlayerCardCount()
    {
        return playerBoardCards.Count;
    }

    public int GetOpponentCardCount()
    {
        return opponentBoardCards.Count;
    }
}