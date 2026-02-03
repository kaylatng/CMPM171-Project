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
        if (data != null)
        {
            // Set text from data
            // nameText.text = data.cardName;

            // Set the unique card image
            if (faceRenderer != null)
            {
                faceRenderer.sprite = data.cardArt; 
            }

            // Apply theme color
            cardRenderer.color = data.themeColor;
        } else {
            // Fallback if data is missing
            cardRenderer.color = (id >= 60) ? Color.yellow : Color.white;
        }
    }
}
