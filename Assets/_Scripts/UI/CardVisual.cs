using UnityEngine;
using System.Collections;
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
    
    // Face-down state (opponent cards until reveal phase)
    private bool isFaceDown = false;
    private Sprite cardBackSprite;

    // Public getter for current card data
    public CardData CurrentCardData => currentCardData;
    public bool IsFaceDown => isFaceDown;

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

    /// <summary>
    /// Set card face-down (card back) or face-up (show art). For opponent cards until reveal.
    /// </summary>
    public void SetFaceDown(bool faceDown, Sprite cardBack = null)
    {
        isFaceDown = faceDown;
        if (cardBack != null) cardBackSprite = cardBack;

        if (isFaceDown)
        {
            if (faceRenderer != null) faceRenderer.enabled = false;
            if (frameRenderer != null) frameRenderer.enabled = false;
            if (cardRenderer != null)
            {
                if (cardBackSprite != null)
                {
                    cardRenderer.sprite = cardBackSprite;
                    cardRenderer.color = Color.white;
                }
                cardRenderer.sortingOrder = 2; // card back above default background
                cardRenderer.enabled = true;
            }
        }
        else
        {
            if (faceRenderer != null) faceRenderer.enabled = true;
            if (frameRenderer != null) frameRenderer.enabled = true;
            if (cardRenderer != null)
            {
                cardRenderer.sortingOrder = 0; // background layer when face-up
                if (currentCardData != null)
                {
                    cardRenderer.sprite = null;
                    cardRenderer.color = currentCardData.themeColor;
                }
            }
        }

        CardShadow shadow = GetComponent<CardShadow>();
        if (shadow != null) shadow.UpdateShadowSprite();
    }

    /// <summary>
    /// Play a short flip animation then reveal the card. Used in reveal phase.
    /// </summary>
    public IEnumerator FlipToReveal(float duration = 0.25f)
    {
        if (!isFaceDown) yield break;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 midScale = new Vector3(0.02f, startScale.y, startScale.z);

        while (elapsed < duration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration * 0.5f);
            transform.localScale = Vector3.Lerp(startScale, midScale, t);
            yield return null;
        }
        SetFaceDown(false);
        elapsed = 0f;
        while (elapsed < duration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration * 0.5f);
            transform.localScale = Vector3.Lerp(midScale, startScale, t);
            yield return null;
        }
        transform.localScale = startScale;
    }
}