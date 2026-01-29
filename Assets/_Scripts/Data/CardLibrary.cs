using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewCardLibrary", menuName = "Cards/Library")]
public class CardLibrary : ScriptableObject
{
	public List<CardData> allCards = new List<CardData>();
	public Sprite cardBack;

	// returns scriptable object asset
	public CardData GetCardAssetByID(int assetID){
		for (int i = 0; i < allCards.Count; i++)
        {
            if (allCards[i] != null && allCards[i].cardID == assetID)
            {
                return allCards[i];
            }
        }
        return null;
	}

	// get tier 1 asset from a pool ID
	public int GetMappedAssetID(int poolID) 
    {
		// explanation: 0-9 -> 100, 10-19 -> 200 ... 60-65 -> 700
        return (poolID / 10 + 1) * 100;
    }
	
	// get tier 1 asset directly from a poolID
	public CardData GetTierOneAssetFromPool(int poolID) 
    {
        int assetID = GetMappedAssetID(poolID);
		return GetCardAssetByID(assetID);
    }
}
