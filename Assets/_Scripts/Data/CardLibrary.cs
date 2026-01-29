using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewCardLibrary", menuName = "Cards/Library")]
public class CardLibrary : ScriptableObject
{
  public List<CardData> allCards = new List<CardData>();
  public Sprite cardBack;

  public CardData GetCardByID(int id){
    foreach (CardData card in allCards) {
      if (card != null && card.cardID == id) {
        return card;
      }
    }
    return null;
  }
}
