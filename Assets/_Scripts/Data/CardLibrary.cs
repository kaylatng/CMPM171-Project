using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewCardLibrary", menuName = "Cards/Library")]
public class CardLibrary : ScriptableObject
{
    public List<CardData> allCards = new List<CardData>();
    public Sprite cardBack;

    private Dictionary<int, CardData> cardLookup;

    private void OnEnable()
    {
        BuildLookup();
    }

    private void BuildLookup()
    {
        cardLookup = new Dictionary<int, CardData>();

        foreach (CardData card in allCards)
        {
            if (card == null) continue;

            if (cardLookup.ContainsKey(card.cardID))
            {
                Debug.LogWarning($"CARD LIBRARY || Duplicate cardID found: {card.cardID} ({card.cardName}). Overwriting.");
            }

            cardLookup[card.cardID] = card;
            Debug.Log($"CARD LIBRARY || Built lookup with {cardLookup.Count} cards. Keys: {string.Join(",", cardLookup.Keys)}");

        }
    }

    public CardData GetCardByID(int id)
    {
        if (cardLookup == null || cardLookup.Count == 0)
        {
            BuildLookup();
        }

        if (cardLookup.TryGetValue(id, out CardData card))
        {
            return card;
        }

        Debug.LogWarning($"CARD LIBRARY || No CardData found for ID: {id}");
        return null;
    }
}

