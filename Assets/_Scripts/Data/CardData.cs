using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "Cards/Card")]
public class CardData : ScriptableObject
{
	[Header("Identification")]
	public string cardName;      // "Starcrossed"
	public int cardID;           // e.g., 101, 102, 103
	public int tier = 1;         // 1, 2, or 3
	public bool isModifier;      // true for Duplicator

	[Header("Stats")]
	public int manaCost;         // set to 0 for Duplicator
	public int attackDamage;

	[Header("Usage")]
	public int maxCharges;       // charge before the card expires
	public bool destroyOnEmpty = true;

	[Header("Visuals")]
	public Sprite cardArt;       // art goes here
	public Color themeColor;     // placeholder

	[Header("Progression")]
	public CardData nextTier;    // link to the next version

	[TextArea]
	public string description;   // "create 1 tier copy" or "the cupid guides you" etc...
}
