using UnityEngine;
using TMPro;

public class CardVisual : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private SpriteRenderer cardRenderer;
    [SerializeField] private SpriteRenderer faceRenderer;
    [SerializeField] private SpriteRenderer frameRenderer;

    public int CardID;
    
    // IMPORTANT: Store the current CardData so we can check the actual tier
    private CardData currentCardData;
    
    // Public getter for current card data
    public CardData CurrentCardData => currentCardData;

    public void Initialize(int id, CardData data)
    {
        CardID = id;
        currentCardData = data; // Store the actual CardData reference

        if (cardRenderer != null)
        {
            cardRenderer.sortingOrder = 0; // Background layer
        }
        
        if (faceRenderer != null)
        {
            faceRenderer.sortingOrder = 1; // Face layer (above background)
        }

        if (frameRenderer != null)
        {
            frameRenderer.sortingOrder = 2; // Frame layer (above face)
        }
        
        if (data != null)
        {
            if (faceRenderer != null)
            {
                faceRenderer.sprite = data.cardArt;
            }
            
            if (frameRenderer != null)
            {
                frameRenderer.sprite = data.tierFrame;
            }

            // Apply theme color to background
            if (cardRenderer != null)
            {
                cardRenderer.color = data.themeColor;
            }
        }
        else
        {
            // Fallback if data is missing
            if (cardRenderer != null)
            {
                cardRenderer.color = (id >= 60) ? Color.yellow : Color.white;
            }
        }
    }
    
    /// Get the current tier of this card (accounts for upgrades)
    public int GetCurrentTier()
    {
        return currentCardData != null ? currentCardData.tier : 1;
    }
}