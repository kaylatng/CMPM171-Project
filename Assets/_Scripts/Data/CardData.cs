using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "Cards/Card")]
public class CardData : ScriptableObject
{
  [Header("Identification")]
  public string cardName;      // "Starcrossed"
  public int cardID;           // 0-65
  public int tier = 1;         // 1, 2, or 3
  public bool isModifier;      // true for Duplicator

  [Header("Stats")]
  public int manaCost;         // set to 0 for Duplicator
  public int attackDamage;

  [Header("Visuals")]
  public Sprite cardArt;       // art goes here
  public Color themeColor;     // Pink/Red placeholder

  [TextArea]
  public string description;   // "create 1 tier copy" or "the cupid guides you" etc...
}
