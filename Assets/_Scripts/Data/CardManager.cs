// using UnityEngine;
// using Unity.Netcode;
// using System.Collections;
// using System.Collections.Generic;

// public class CardManager : MonoBehaviour
// {
//     public static CardManager Instance;

//     [Header("References")]
//     [SerializeField] private CardLibrary cardLibrary;
//     [SerializeField] private GameObject cardPrefab;

//     [Header("Hand Zones")]
//     private Transform playerHandZone;
//     private Transform opponentHandZone;

//     [Header("Card Layout Settings")]
//     [SerializeField] private float cardSpacing = 1.5f;
//     [SerializeField] private float maxCardSpread = 8f; // Maximum horizontal spread
//     [SerializeField] private float cardArcHeight = 0.2f;
//     [SerializeField] private float maxCardRotation = 5f; // Max rotation in degrees
    
//     [Header("Animation Settings")]
//     [SerializeField] private float cardMoveSpeed = 12f; // Speed for lerping to position
//     [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
//     [SerializeField] private float hoverLiftHeight = 0.3f;
//     [SerializeField] private float hoverScale = 1.1f;
//     [SerializeField] private float hoverRotationReduction = 0.7f; // Reduce rotation when hovering
    
//     [Header("Sorting")]
//     [SerializeField] private int baseSortingOrder = 0;
//     [SerializeField] private int hoverSortingOrder = 100;

//     // Track cards for smooth updates
//     private List<CardInHand> playerHandCards = new List<CardInHand>();
//     private List<CardInHand> opponentHandCards = new List<CardInHand>();

//     private class CardInHand
//     {
//         public GameObject cardObject;
//         public Vector3 targetPosition;
//         public Quaternion targetRotation;
//         public int targetSortingOrder;
//         public bool isHovered;
//         public float hoverProgress; // 0 to 1 for smooth hover transition
//     }

//     private void Awake()
//     {
//         if (Instance == null) Instance = this;
//         else
//         {
//             Destroy(gameObject);
//             return;
//         }
//     }

//     private void Start()
//     {
//         AssignHandZones();
//     }

//     private void Update()
//     {
//         // Smoothly update all cards in hands
//         UpdateHandCards(playerHandCards);
//         UpdateHandCards(opponentHandCards);
//     }

//     public void AssignHandZones()
//     {
//         if (playerHandZone == null)
//         {
//             GameObject pZone = GameObject.Find("PlayerHandZone");
//             if (pZone != null)
//             {
//                 playerHandZone = pZone.transform;
//                 Debug.Log("CARD MANAGER || Player hand zone found");
//             }
//             else
//             {
//                 Debug.LogWarning("CARD MANAGER || PlayerHandZone not found in scene!");
//             }
//         }

//         if (opponentHandZone == null)
//         {
//             GameObject oZone = GameObject.Find("OpponentHandZone");
//             if (oZone != null)
//             {
//                 opponentHandZone = oZone.transform;
//                 Debug.Log("CARD MANAGER || Opponent hand zone found");
//             }
//             else
//             {
//                 Debug.LogWarning("CARD MANAGER || OpponentHandZone not found in scene!");
//             }
//         }
//     }

//     public GameObject SpawnCard(int cardId, bool isPlayerCard)
//     {
//         if (playerHandZone == null || opponentHandZone == null)
//         {
//             AssignHandZones();
//         }

//         Transform targetZone = isPlayerCard ? playerHandZone : opponentHandZone;
        
//         if (targetZone == null)
//         {
//             Debug.LogError($"CARD MANAGER || Cannot spawn card - {(isPlayerCard ? "Player" : "Opponent")} hand zone is null!");
//             return null;
//         }

//         GameObject newCard = Instantiate(cardPrefab, targetZone);
//         newCard.transform.SetParent(targetZone, false);

//         InitializeCardVisual(newCard, cardId, isPlayerCard);

//         // Add hover listener for Balatro-style hover effect
//         AddCardHoverListener(newCard, isPlayerCard);

//         // Arrange cards with smooth animation
//         ArrangeCardsInHand(targetZone);

//         Debug.Log($"CARD MANAGER || Spawned card {cardId} in {(isPlayerCard ? "player" : "opponent")} hand");
//         return newCard;
//     }

//     public GameObject SpawnCardAtParent(int cardId, bool revealCard, Transform parent)
//     {
//         if (parent == null)
//         {
//             Debug.LogError("CARD MANAGER || Cannot spawn card - parent is null!");
//             return null;
//         }

//         GameObject newCard = Instantiate(cardPrefab, parent);
//         newCard.transform.SetParent(parent, false);
//         newCard.transform.localPosition = Vector3.zero;
//         newCard.transform.localRotation = Quaternion.identity;
//         newCard.transform.localScale = Vector3.one;

//         InitializeCardVisual(newCard, cardId, revealCard);

//         Debug.Log($"CARD MANAGER || Spawned card {cardId} directly at parent {parent.name}");
//         return newCard;
//     }

//     private void ArrangeCardsInHand(Transform handZone)
//     {
//         if (handZone == null) return;

//         List<CardInHand> handCards = handZone == playerHandZone ? playerHandCards : opponentHandCards;
        
//         // Clear and rebuild card list
//         handCards.Clear();
        
//         int cardCount = handZone.childCount;
//         if (cardCount == 0) return;

//         // Calculate layout
//         float actualSpacing = Mathf.Min(cardSpacing, maxCardSpread / Mathf.Max(1, cardCount - 1));
//         float totalWidth = (cardCount - 1) * actualSpacing;
//         float startX = -totalWidth / 2f;

//         for (int i = 0; i < cardCount; i++)
//         {
//             Transform cardTransform = handZone.GetChild(i);
            
//             // Create card data
//             CardInHand cardData = new CardInHand
//             {
//                 cardObject = cardTransform.gameObject
//             };

//             float normalizedPosition = cardCount > 1 ? (float)i / (cardCount - 1) : 0.5f;
//             float xPos = startX + (i * actualSpacing);
            
//             // Create arc effect using parabola
//             float yPos = -cardArcHeight * 4f * (normalizedPosition - 0.5f) * (normalizedPosition - 0.5f) + cardArcHeight;
            
//             cardData.targetPosition = new Vector3(xPos, yPos, 0);
            
//             // Create fan rotation effect
//             float rotationZ = Mathf.Lerp(maxCardRotation, -maxCardRotation, normalizedPosition);
//             cardData.targetRotation = Quaternion.Euler(0, 0, rotationZ);
            
//             // Set sorting order (leftmost cards behind)
//             cardData.targetSortingOrder = baseSortingOrder + i;
            
//             handCards.Add(cardData);
//         }
//     }

//     private void UpdateHandCards(List<CardInHand> handCards)
//     {
//         foreach (var cardData in handCards)
//         {
//             if (cardData.cardObject == null) continue;

//             // Smooth hover transition
//             float targetHover = cardData.isHovered ? 1f : 0f;
//             cardData.hoverProgress = Mathf.Lerp(cardData.hoverProgress, targetHover, Time.deltaTime * 10f);

//             // Calculate final position with hover offset
//             Vector3 finalPosition = cardData.targetPosition;
//             Quaternion finalRotation = cardData.targetRotation;
//             float finalScale = 1f;
//             int finalSortingOrder = cardData.targetSortingOrder;

//             if (cardData.hoverProgress > 0.01f)
//             {
//                 // Apply hover effects
//                 finalPosition.y += hoverLiftHeight * cardData.hoverProgress;
//                 finalScale = Mathf.Lerp(1f, hoverScale, cardData.hoverProgress);
                
//                 // Reduce rotation when hovered (more upright)
//                 float currentRotation = finalRotation.eulerAngles.z;
//                 if (currentRotation > 180f) currentRotation -= 360f;
//                 currentRotation *= Mathf.Lerp(1f, hoverRotationReduction, cardData.hoverProgress);
//                 finalRotation = Quaternion.Euler(0, 0, currentRotation);
                
//                 finalSortingOrder = hoverSortingOrder;
//             }

//             // Smooth movement using lerp
//             cardData.cardObject.transform.localPosition = Vector3.Lerp(
//                 cardData.cardObject.transform.localPosition,
//                 finalPosition,
//                 Time.deltaTime * cardMoveSpeed
//             );

//             cardData.cardObject.transform.localRotation = Quaternion.Lerp(
//                 cardData.cardObject.transform.localRotation,
//                 finalRotation,
//                 Time.deltaTime * cardMoveSpeed
//             );

//             cardData.cardObject.transform.localScale = Vector3.Lerp(
//                 cardData.cardObject.transform.localScale,
//                 Vector3.one * finalScale,
//                 Time.deltaTime * cardMoveSpeed
//             );

//             // Update sorting order
//             SpriteRenderer sr = cardData.cardObject.GetComponent<SpriteRenderer>();
//             if (sr != null)
//             {
//                 sr.sortingOrder = finalSortingOrder;
//             }
//         }
//     }

//     private void AddCardHoverListener(GameObject card, bool isPlayerCard)
//     {
//         if (!isPlayerCard) return; // Only add hover to player cards

//         // Add a simple hover component
//         CardHoverEffect hoverEffect = card.GetComponent<CardHoverEffect>();
//         if (hoverEffect == null)
//         {
//             hoverEffect = card.AddComponent<CardHoverEffect>();
//         }

//         // Subscribe to hover events
//         hoverEffect.OnHoverEnter += () => OnCardHoverEnter(card);
//         hoverEffect.OnHoverExit += () => OnCardHoverExit(card);
//     }

//     private void OnCardHoverEnter(GameObject card)
//     {
//         // Find card in hand and mark as hovered
//         CardInHand cardData = playerHandCards.Find(c => c.cardObject == card);
//         if (cardData != null)
//         {
//             cardData.isHovered = true;
//         }
//     }

//     private void OnCardHoverExit(GameObject card)
//     {
//         // Find card in hand and unmark hover
//         CardInHand cardData = playerHandCards.Find(c => c.cardObject == card);
//         if (cardData != null)
//         {
//             cardData.isHovered = false;
//         }
//     }

//     public void InitializeCardVisual(GameObject cardObject, int cardId, bool revealCard)
//     {
//         if (cardObject == null)
//         {
//             Debug.LogError("CARD MANAGER || Cannot initialize null card object");
//             return;
//         }

//         CardVisual cardVisual = cardObject.GetComponent<CardVisual>();
//         SpriteRenderer spriteRenderer = cardObject.GetComponent<SpriteRenderer>();
        
//         if (spriteRenderer == null)
//         {
//             spriteRenderer = cardObject.GetComponentInChildren<SpriteRenderer>();
//         }

//         if (revealCard && cardId >= 0)
//         {
//             if (cardVisual != null)
//             {
//                 CardData data = cardLibrary.GetTierOneAssetFromPool(cardId);
//                 cardVisual.Initialize(cardId, data);
//             }
//         }
//         else
//         {
//             // Opponent card - show card back
//             if (spriteRenderer != null)
//             {
//                 if (cardLibrary != null && cardLibrary.cardBack != null)
//                 {
//                     spriteRenderer.sprite = cardLibrary.cardBack;
//                 }
//                 else
//                 {
//                     spriteRenderer.color = Color.white;
//                 }
//             }

//             if (cardVisual != null)
//             {
//                 cardVisual.CardID = -1;
//             }

//             CardDraggable draggable = cardObject.GetComponent<CardDraggable>();
//             if (draggable != null)
//             {
//                 draggable.enabled = false;
//             }
//         }
//     }

//     public void RemoveCardFromHand(GameObject cardObject)
//     {
//         if (cardObject == null) return;
        
//         Transform parent = cardObject.transform.parent;
        
//         // Remove from tracking lists
//         playerHandCards.RemoveAll(c => c.cardObject == cardObject);
//         opponentHandCards.RemoveAll(c => c.cardObject == cardObject);
        
//         // Deparent it - don't destroy
//         cardObject.transform.SetParent(null);
        
//         // Rearrange remaining cards in hand
//         if (parent != null && (parent == playerHandZone || parent == opponentHandZone))
//         {
//             ArrangeCardsInHand(parent);
//         }
//     }

//     public void RemoveCardFromZone(GameObject cardObject)
//     {
//         if (cardObject != null)
//         {
//             Transform parent = cardObject.transform.parent;
            
//             // Remove from tracking
//             playerHandCards.RemoveAll(c => c.cardObject == cardObject);
//             opponentHandCards.RemoveAll(c => c.cardObject == cardObject);
            
//             Destroy(cardObject);
            
//             if (parent != null)
//             {
//                 ArrangeCardsInHand(parent);
//             }
//         }
//     }

//     public void RemoveOneOpponentHandCard()
//     {
//         if (opponentHandZone == null)
//         {
//             AssignHandZones();
//         }

//         if (opponentHandZone != null && opponentHandZone.childCount > 0)
//         {
//             GameObject cardToRemove = opponentHandZone.GetChild(0).gameObject;
//             Debug.Log($"CARD MANAGER || Removing one card from opponent hand");
//             RemoveCardFromZone(cardToRemove);
//         }
//         else
//         {
//             Debug.LogWarning("CARD MANAGER || Tried to remove card from empty opponent hand");
//         }
//     }

//     public void ClearHandZone(bool isPlayerZone)
//     {
//         Transform targetZone = isPlayerZone ? playerHandZone : opponentHandZone;
        
//         if (targetZone == null) return;

//         if (isPlayerZone)
//         {
//             playerHandCards.Clear();
//         }
//         else
//         {
//             opponentHandCards.Clear();
//         }

//         foreach (Transform child in targetZone)
//         {
//             Destroy(child.gameObject);
//         }

//         Debug.Log($"CARD MANAGER || Cleared {(isPlayerZone ? "player" : "opponent")} hand zone");
//     }

//     public int GetCardCountInZone(bool isPlayerZone)
//     {
//         Transform targetZone = isPlayerZone ? playerHandZone : opponentHandZone;
//         return targetZone != null ? targetZone.childCount : 0;
//     }

//     public CardLibrary GetCardLibrary()
//     {
//         return cardLibrary;
//     }

//     public GameObject GetCardPrefab()
//     {
//         return cardPrefab;
//     }

//     public void RevealOpponentCard(GameObject cardObject, int cardId)
//     {
//         if (cardObject == null || cardId < 0) return;

//         InitializeCardVisual(cardObject, cardId, true);
//         Debug.Log($"CARD MANAGER || Revealed opponent card {cardId}");
//     }
// }

using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

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
    [SerializeField] private float maxCardSpread = 8f; // Maximum horizontal spread
    [SerializeField] private float cardArcHeight = 0.2f;
    [SerializeField] private float maxCardRotation = 5f; // Max rotation in degrees
    
    [Header("Animation Settings")]
    [SerializeField] private float cardMoveSpeed = 12f; // Speed for lerping to position
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float hoverLiftHeight = 0.3f;
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float hoverRotationReduction = 0.7f; // Reduce rotation when hovering
    
    [Header("Sorting")]
    [SerializeField] private int baseSortingOrder = 0;
    [SerializeField] private int hoverSortingOrder = 100;

    // Track cards for smooth updates
    private List<CardInHand> playerHandCards = new List<CardInHand>();
    private List<CardInHand> opponentHandCards = new List<CardInHand>();

    private class CardInHand
    {
        public GameObject cardObject;
        public Vector3 targetPosition;
        public Quaternion targetRotation;
        public int targetSortingOrder;
        public bool isHovered;
        public float hoverProgress; // 0 to 1 for smooth hover transition
    }

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

    private void Update()
    {
        // Smoothly update all cards in hands
        UpdateHandCards(playerHandCards);
        UpdateHandCards(opponentHandCards);
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

        // Add hover listener for Balatro-style hover effect
        AddCardHoverListener(newCard, isPlayerCard);

        // Arrange cards with smooth animation
        ArrangeCardsInHand(targetZone);

        Debug.Log($"CARD MANAGER || Spawned card {cardId} in {(isPlayerCard ? "player" : "opponent")} hand");
        return newCard;
    }

    public GameObject SpawnCardAtParent(int cardId, bool revealCard, Transform parent)
    {
        if (parent == null)
        {
            Debug.LogError("CARD MANAGER || Cannot spawn card - parent is null!");
            return null;
        }

        GameObject newCard = Instantiate(cardPrefab, parent);
        newCard.transform.SetParent(parent, false);
        newCard.transform.localPosition = Vector3.zero;
        newCard.transform.localRotation = Quaternion.identity;
        newCard.transform.localScale = Vector3.one;

        InitializeCardVisual(newCard, cardId, revealCard);

        Debug.Log($"CARD MANAGER || Spawned card {cardId} directly at parent {parent.name}");
        return newCard;
    }

    public void ArrangeCardsInHand(Transform handZone)
    {
        if (handZone == null) return;

        List<CardInHand> handCards = handZone == playerHandZone ? playerHandCards : opponentHandCards;
        
        // Clear and rebuild card list
        handCards.Clear();
        
        int cardCount = handZone.childCount;
        if (cardCount == 0) return;

        Debug.Log($"CARD MANAGER || Arranging {cardCount} cards in {handZone.name}");

        // Calculate layout
        float actualSpacing = Mathf.Min(cardSpacing, maxCardSpread / Mathf.Max(1, cardCount - 1));
        float totalWidth = (cardCount - 1) * actualSpacing;
        float startX = -totalWidth / 2f;

        for (int i = 0; i < cardCount; i++)
        {
            Transform cardTransform = handZone.GetChild(i);
            
            // Create card data
            CardInHand cardData = new CardInHand
            {
                cardObject = cardTransform.gameObject
            };

            float normalizedPosition = cardCount > 1 ? (float)i / (cardCount - 1) : 0.5f;
            float xPos = startX + (i * actualSpacing);
            
            // Create arc effect using parabola
            float yPos = -cardArcHeight * 4f * (normalizedPosition - 0.5f) * (normalizedPosition - 0.5f) + cardArcHeight;
            
            cardData.targetPosition = new Vector3(xPos, yPos, 0);
            
            // Create fan rotation effect
            float rotationZ = Mathf.Lerp(maxCardRotation, -maxCardRotation, normalizedPosition);
            cardData.targetRotation = Quaternion.Euler(0, 0, rotationZ);
            
            // Set sorting order (leftmost cards behind)
            cardData.targetSortingOrder = baseSortingOrder + i;
            
            handCards.Add(cardData);
        }
    }

    private void UpdateHandCards(List<CardInHand> handCards)
    {
        foreach (var cardData in handCards)
        {
            if (cardData.cardObject == null) continue;

            // IMPORTANT: Skip cards that are being dragged
            CardDraggable draggable = cardData.cardObject.GetComponent<CardDraggable>();
            if (draggable != null && draggable.IsDragging)
            {
                continue; // Don't update position while dragging
            }

            // Smooth hover transition
            float targetHover = cardData.isHovered ? 1f : 0f;
            cardData.hoverProgress = Mathf.Lerp(cardData.hoverProgress, targetHover, Time.deltaTime * 10f);

            // Calculate final position with hover offset
            Vector3 finalPosition = cardData.targetPosition;
            Quaternion finalRotation = cardData.targetRotation;
            float finalScale = 1f;
            int finalSortingOrder = cardData.targetSortingOrder;

            if (cardData.hoverProgress > 0.01f)
            {
                // Apply hover effects
                finalPosition.y += hoverLiftHeight * cardData.hoverProgress;
                finalScale = Mathf.Lerp(1f, hoverScale, cardData.hoverProgress);
                
                // Reduce rotation when hovered (more upright)
                float currentRotation = finalRotation.eulerAngles.z;
                if (currentRotation > 180f) currentRotation -= 360f;
                currentRotation *= Mathf.Lerp(1f, hoverRotationReduction, cardData.hoverProgress);
                finalRotation = Quaternion.Euler(0, 0, currentRotation);
                
                finalSortingOrder = hoverSortingOrder;
            }

            // Smooth movement using lerp
            cardData.cardObject.transform.localPosition = Vector3.Lerp(
                cardData.cardObject.transform.localPosition,
                finalPosition,
                Time.deltaTime * cardMoveSpeed
            );

            cardData.cardObject.transform.localRotation = Quaternion.Lerp(
                cardData.cardObject.transform.localRotation,
                finalRotation,
                Time.deltaTime * cardMoveSpeed
            );

            cardData.cardObject.transform.localScale = Vector3.Lerp(
                cardData.cardObject.transform.localScale,
                Vector3.one * finalScale,
                Time.deltaTime * cardMoveSpeed
            );

            // Update sorting order
            SpriteRenderer sr = cardData.cardObject.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingOrder = finalSortingOrder;
            }
        }
    }

    private void AddCardHoverListener(GameObject card, bool isPlayerCard)
    {
        if (!isPlayerCard) return; // Only add hover to player cards

        // Add a simple hover component
        CardHoverEffect hoverEffect = card.GetComponent<CardHoverEffect>();
        if (hoverEffect == null)
        {
            hoverEffect = card.AddComponent<CardHoverEffect>();
        }

        // Subscribe to hover events
        hoverEffect.OnHoverEnter += () => OnCardHoverEnter(card);
        hoverEffect.OnHoverExit += () => OnCardHoverExit(card);
    }

    private void OnCardHoverEnter(GameObject card)
    {
        // Find card in hand and mark as hovered
        CardInHand cardData = playerHandCards.Find(c => c.cardObject == card);
        if (cardData != null)
        {
            cardData.isHovered = true;
        }
    }

    private void OnCardHoverExit(GameObject card)
    {
        // Find card in hand and unmark hover
        CardInHand cardData = playerHandCards.Find(c => c.cardObject == card);
        if (cardData != null)
        {
            cardData.isHovered = false;
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
            if (cardVisual != null)
            {
                CardData data = cardLibrary.GetTierOneAssetFromPool(cardId);
                cardVisual.Initialize(cardId, data);
            }
        }
        else
        {
            // Opponent card - show card back
            if (spriteRenderer != null)
            {
                if (cardLibrary != null && cardLibrary.cardBack != null)
                {
                    spriteRenderer.sprite = cardLibrary.cardBack;
                }
                else
                {
                    spriteRenderer.color = Color.white;
                }
            }

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

    public void RemoveCardFromHand(GameObject cardObject)
    {
        if (cardObject == null) return;
        
        Transform parent = cardObject.transform.parent;
        
        // If card was unparented (e.g., during drag), check which hand it belongs to
        if (parent == null)
        {
            // Check if card is in player hand list
            if (playerHandCards.Exists(c => c.cardObject == cardObject))
            {
                parent = playerHandZone;
            }
            // Check if card is in opponent hand list
            else if (opponentHandCards.Exists(c => c.cardObject == cardObject))
            {
                parent = opponentHandZone;
            }
        }
        
        // Remove from tracking lists
        playerHandCards.RemoveAll(c => c.cardObject == cardObject);
        opponentHandCards.RemoveAll(c => c.cardObject == cardObject);
        
        // Deparent it - don't destroy
        cardObject.transform.SetParent(null);
        
        // Rearrange remaining cards in hand
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
            
            // Remove from tracking
            playerHandCards.RemoveAll(c => c.cardObject == cardObject);
            opponentHandCards.RemoveAll(c => c.cardObject == cardObject);
            
            Destroy(cardObject);
            
            if (parent != null)
            {
                ArrangeCardsInHand(parent);
            }
        }
    }

    public void RemoveOneOpponentHandCard()
    {
        if (opponentHandZone == null)
        {
            AssignHandZones();
        }

        if (opponentHandZone != null && opponentHandZone.childCount > 0)
        {
            GameObject cardToRemove = opponentHandZone.GetChild(0).gameObject;
            Debug.Log($"CARD MANAGER || Removing one card from opponent hand");
            RemoveCardFromZone(cardToRemove);
        }
        else
        {
            Debug.LogWarning("CARD MANAGER || Tried to remove card from empty opponent hand");
        }
    }

    public void ClearHandZone(bool isPlayerZone)
    {
        Transform targetZone = isPlayerZone ? playerHandZone : opponentHandZone;
        
        if (targetZone == null) return;

        if (isPlayerZone)
        {
            playerHandCards.Clear();
        }
        else
        {
            opponentHandCards.Clear();
        }

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

    public GameObject GetCardPrefab()
    {
        return cardPrefab;
    }

    public void RevealOpponentCard(GameObject cardObject, int cardId)
    {
        if (cardObject == null || cardId < 0) return;

        InitializeCardVisual(cardObject, cardId, true);
        Debug.Log($"CARD MANAGER || Revealed opponent card {cardId}");
    }
}