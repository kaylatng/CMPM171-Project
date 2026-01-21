using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewCardLibrary", menuName = "Cards/Library")]
public class CardLibrary : ScriptableObject
{
  public List<CardData> allCards = new List<CardData>();
  public Sprite cardBack;

  // FUNCTION TO BE IMPLEMENTED
  public CardData GetCardByID(int id){
    // return ID if within bounds of allCards.Count
    return null; // remove this
  }
}
