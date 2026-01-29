using UnityEngine;
using Unity.Netcode;

public class CardManager : MonoBehaviour
{
    public static CardManager Instance;

    [Header("References")]
    [SerializeField] private CardLibrary cardLibrary;
    [SerializeField] private GameObject cardPrefab;

    [Header("Hand Zones")]
    private Transform playerHandZone;
    private Transform opponentHandZone;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        AssignHandZones();
    }

    public void AssignHandZones()
    {
        if (playerHandZone == null)
        {
            GameObject pZone = GameObject.Find("PlayerHandZone");
            if (pZone != null)
            {
                playerHandZone = pZone.transform;
                Debug.Log("CARD MANAGER || Player hand zone found");
            }
            else
            {
                Debug.LogWarning("CARD MANAGER || PlayerHandZone not found in scene!");
            }
        }

        if (opponentHandZone == null)
        {
            GameObject oZone = GameObject.Find("OpponentHandZone");
            if (oZone != null)
            {
                opponentHandZone = oZone.transform;
                Debug.Log("CARD MANAGER || Opponent hand zone found");
            }
            else
            {
                Debug.LogWarning("CARD MANAGER || OpponentHandZone not found in scene!");
            }
        }
    }

    public GameObject SpawnCard(int cardId, bool isPlayerCard)
    {
        if (playerHandZone == null || opponentHandZone == null)
        {
            AssignHandZones();
        }

        Transform targetZone = isPlayerCard ? playerHandZone : opponentHandZone;
        
        if (targetZone == null)
        {
            Debug.LogError($"CARD MANAGER || Cannot spawn card - {(isPlayerCard ? "Player" : "Opponent")} hand zone is null!");
            return null;
        }

        GameObject newCard = Instantiate(cardPrefab, targetZone);
        newCard.transform.SetParent(targetZone, false);

        InitializeCardVisual(newCard, cardId, isPlayerCard);

        Debug.Log($"CARD MANAGER || Spawned card {cardId} in {(isPlayerCard ? "player" : "opponent")} hand");
        return newCard;
    }

    public void InitializeCardVisual(GameObject cardObject, int cardId, bool revealCard)
    {
        if (cardObject == null)
        {
            Debug.LogError("CARD MANAGER || Cannot initialize null card object");
            return;
        }

        CardVisual cardVisual = cardObject.GetComponent<CardVisual>();
        SpriteRenderer spriteRenderer = cardObject.GetComponent<SpriteRenderer>();
        
        if (spriteRenderer == null)
        {
            spriteRenderer = cardObject.GetComponentInChildren<SpriteRenderer>();
        }

        if (revealCard && cardId >= 0)
        {
            // player card - show it fully
            if (cardVisual != null)
            {
                cardVisual.Initialize(cardId);
            }

            // TO-DO: implement sync to GetCardByID
            /*
            if (cardLibrary != null)
            {
                CardData cardData = cardLibrary.GetCardByID(cardId);
                if (cardData != null)
                {
                    ApplyCardData(cardObject, cardData, spriteRenderer);
                }
            }
            */
        }
        else
        {
            // opponent card - show card back
            if (spriteRenderer != null)
            {
                if (cardLibrary != null && cardLibrary.cardBack != null)
                {
                    spriteRenderer.sprite = cardLibrary.cardBack;
                }
                else
                {
                    // fallback: use red color for hidden cards
                    spriteRenderer.color = Color.red;
                }
            }

            // store object is hidden card
            if (cardVisual != null)
            {
                cardVisual.CardID = -1;
            }
        }
    }

    private void ApplyCardData(GameObject cardObject, CardData cardData, SpriteRenderer spriteRenderer)
    {
        // render sprite here

        // assign name text, mana cost, attack damage displays here
        return;
    }

    public void RemoveCardFromZone(GameObject cardObject)
    {
        if (cardObject != null)
        {
            Destroy(cardObject);
        }
    }

    public void ClearHandZone(bool isPlayerZone)
    {
        Transform targetZone = isPlayerZone ? playerHandZone : opponentHandZone;
        
        if (targetZone == null) return;

        foreach (Transform child in targetZone)
        {
            Destroy(child.gameObject);
        }

        Debug.Log($"CARD MANAGER || Cleared {(isPlayerZone ? "player" : "opponent")} hand zone");
    }

    public int GetCardCountInZone(bool isPlayerZone)
    {
        Transform targetZone = isPlayerZone ? playerHandZone : opponentHandZone;
        return targetZone != null ? targetZone.childCount : 0;
    }

    public void RevealOpponentCard(GameObject cardObject, int cardId)
    {
        if (cardObject == null || cardId < 0) return;

        InitializeCardVisual(cardObject, cardId, true);
        Debug.Log($"CARD MANAGER || Revealed opponent card {cardId}");
    }
}