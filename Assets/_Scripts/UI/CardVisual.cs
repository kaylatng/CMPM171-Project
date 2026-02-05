using UnityEngine;
using TMPro;

public class CardVisual : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private SpriteRenderer cardRenderer;
    [SerializeField] private SpriteRenderer faceRenderer;
    public int CardID;

    public void Initialize(int id, CardData data)
    {
        CardID = id;

        if (cardRenderer != null)
        {
            cardRenderer.sortingOrder = 0; // Background layer
        }
        
        if (faceRenderer != null)
        {
            faceRenderer.sortingOrder = 1; // Face layer (above background)
        }
        
        if (data != null)
        {
            if (faceRenderer != null)
            {
                faceRenderer.sprite = data.cardArt;
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
}
