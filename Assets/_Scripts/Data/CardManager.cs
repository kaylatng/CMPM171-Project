using System.Collections.Generic;
using UnityEngine;

public class CardManager : MonoBehaviour
{
    public static CardManager Instance;

    [Header("References")]
    public CardLibrary cardLibrary;

    [Header("Hand Settings")]
    public int maxHandSize = 5;

    [Header("Runtime State")]
    public List<int> handCardIds = new List<int>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    // CardManager does not store allCards.
    // asks the CardLibrary for CardData.
    public CardData GetCardData(int id)
    {
        if (cardLibrary == null)
        {
            Debug.LogError("CARD MANAGER || Missing CardLibrary reference.");
            return null;
        }

        return cardLibrary.GetCardByID(id);
    }

    public bool CanDraw()
    {
        return handCardIds.Count < maxHandSize;
    }

    public bool AddCardToHand(int cardId)
    {
        if (!CanDraw())
        {
            Debug.Log("CARD MANAGER || Hand full.");
            return false;
        }

        CardData card = GetCardData(cardId);
        if (card == null)
        {
            Debug.LogWarning($"CARD MANAGER || Tried to add invalid cardId {cardId}");
            return false;
        }

        handCardIds.Add(cardId);
        Debug.Log($"CARD MANAGER || Added card {card.cardName} (ID {cardId}) to hand.");
        return true;
    }

    public bool RemoveCardFromHand(int cardId)
    {
        bool removed = handCardIds.Remove(cardId);
        if (removed) Debug.Log($"CARD MANAGER || Removed card ID {cardId} from hand.");
        return removed;
    }

    public void ClearHand()
    {
        handCardIds.Clear();
    }

    // draw from DeckManager into hand
    public bool DrawFromDeck()
    {
        if (DeckManager.Instance == null)
        {
            Debug.LogError("CARD MANAGER || DeckManager.Instance missing.");
            return false;
        }

        int cardId = DeckManager.Instance.DrawCard();
        if (cardId == -1)
        {
            Debug.Log("CARD MANAGER || Deck empty.");
            return false;
        }

        return AddCardToHand(cardId);
    }

#if UNITY_EDITOR
    [ContextMenu("TEST: Draw Card")]
    private void TestDraw()
    {
        DrawFromDeck();
    }
#endif
}
