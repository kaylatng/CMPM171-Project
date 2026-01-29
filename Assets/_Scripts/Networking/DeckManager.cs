using System.Collections.Generic;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    public static DeckManager Instance;

    private List<int> masterDeck = new List<int>();

    private void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        GenerateInitialDeck();
        Shuffle();
    }
    
    void Start()
    {
        
    }

    void Update()
    {
        
    }

    //TEMPORARILY COMMDENTED OUT FOR SMALLER SCOPE CARDLOGIC TESTING, will uncomment to implement 66 cards 

   // private void GenerateInitialDeck() {
       // masterDeck.Clear();

        // add 60 spell cards (ID: 0-59)
       // for (int i = 0; i < 60; i++) {
        //    masterDeck.Add(i);
       // }

        // add 6 modifier spell cards (ID: 60-65)
       // for (int i = 60; i <= 65; i++) {
          //  masterDeck.Add(i);
       // }
       // Debug.Log($"DECK MANAGER || Deck Initialized: {masterDeck.Count} cards.");
    //}

   private void GenerateInitialDeck()
   {
        masterDeck.Clear();

        if (CardManager.Instance == null || CardManager.Instance.cardLibrary == null)
        {
            Debug.LogError("DECK MANAGER || Missing CardLibrary.");
            return;
        }

        foreach (CardData card in CardManager.Instance.cardLibrary.allCards)
        {
            if (card == null) continue;
            masterDeck.Add(card.cardID);
        }

        Debug.Log($"DECK MANAGER || Deck Initialized: {masterDeck.Count} cards.");
   }

    public void Shuffle()
    {
        for (int i = 0; i < masterDeck.Count; i++)
        {
            int temp = masterDeck[i];
            int randomIndex = Random.Range(i, masterDeck.Count);
            masterDeck[i] = masterDeck[randomIndex];
            masterDeck[randomIndex] = temp;
        }
    }

    public int DrawCard() {
        if (masterDeck.Count == 0) return -1;
        int cardId = masterDeck[0];
        masterDeck.RemoveAt(0);
        return cardId;
    }
}
