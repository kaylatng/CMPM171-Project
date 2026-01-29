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

    [Header("Card Layout Settings")]
    [SerializeField] private float cardSpacing = 1.5f;
    [SerializeField] private float cardArcHeight = 0.5f; // arc WIP
    [SerializeField] private float cardRotationAngle = 5f;


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

        ArrangeCardsInHand(targetZone);

        Debug.Log($"CARD MANAGER || Spawned card {cardId} in {(isPlayerCard ? "player" : "opponent")} hand");
        return newCard;
    }

    private void ArrangeCardsInHand(Transform handZone)
    {
        if (handZone == null) return;

        int cardCount = handZone.childCount;
        if (cardCount == 0) return;

        float totalWidth = (cardCount - 1) * cardSpacing;
        float startX = -totalWidth / 2f;

        for (int i = 0; i < cardCount; i++)
        {
            Transform card = handZone.GetChild(i);
            
            float normalizedPosition = cardCount > 1 ? (float)i / (cardCount - 1) : 0.5f;
            float xPos = startX + (i * cardSpacing);
            
            // create arc effect
            float yPos = -cardArcHeight * 4f * (normalizedPosition - 0.5f) * (normalizedPosition - 0.5f) + cardArcHeight;
            
            card.localPosition = new Vector3(xPos, yPos, 0);
            
            // create fan rotation effect
            float rotationZ = Mathf.Lerp(cardRotationAngle, -cardRotationAngle, normalizedPosition);
            card.localRotation = Quaternion.Euler(0, 0, rotationZ);
            
            // Optional: Set sorting order based on position (leftmost cards behind)
            SpriteRenderer spriteRenderer = card.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = i;
            }
        }
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
                    spriteRenderer.color = Color.white; // change to red for code testing
                }
            }

            // store object is hidden card
            if (cardVisual != null)
            {
                cardVisual.CardID = -1;
            }

            CardDraggable draggable = cardObject.GetComponent<CardDraggable>();
            if (draggable != null)
            {
                draggable.enabled = false;
            }
        }
    }

    private void ApplyCardData(GameObject cardObject, CardData cardData, SpriteRenderer spriteRenderer)
    {
        // render sprite here

        // assign name text, mana cost, attack damage displays here
        return;
    }

    public void RemoveCardFromHand(GameObject cardObject)
    {
        if (cardObject == null) return;
        
        Transform parent = cardObject.transform.parent;
        
        // deparent it - don't destroy
        cardObject.transform.SetParent(null);
        
        // rearrange remaining cards in hand
        if (parent != null && (parent == playerHandZone || parent == opponentHandZone))
        {
            ArrangeCardsInHand(parent);
        }
    }

    public void RemoveCardFromZone(GameObject cardObject)
    {
        if (cardObject != null)
        {
            Transform parent = cardObject.transform.parent;
            Destroy(cardObject);
            
            // rearrange remaining cards after removal
            if (parent != null)
            {
                ArrangeCardsInHand(parent);
            }
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

    public CardLibrary GetCardLibrary()
    {
        return cardLibrary;
    }

    public void RevealOpponentCard(GameObject cardObject, int cardId)
    {
        if (cardObject == null || cardId < 0) return;

        InitializeCardVisual(cardObject, cardId, true);
        Debug.Log($"CARD MANAGER || Revealed opponent card {cardId}");
    }
}