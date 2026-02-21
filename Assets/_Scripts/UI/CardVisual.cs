using UnityEngine;
using System.Collections;
using TMPro;

public class CardVisual : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI nameText;

    [Header("Sprite Renderers")]
    [SerializeField] private SpriteRenderer cardRenderer;   // base/background
    [SerializeField] private SpriteRenderer faceRenderer;   // art
    [SerializeField] private SpriteRenderer frameRenderer;  // tier frame overlay

    public int CardID;

    private CardData currentCardData;
    private Sprite frontBaseSprite;

    private bool isFaceDown = false;
    private Sprite cardBackSprite;

    public CardData CurrentCardData => currentCardData;
    public bool IsFaceDown => isFaceDown;

    private const int FaceOffset = 10;
    private const int FrameOffset = 20;

    private void Awake()
    {
        AutoWireIfMissing();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            AutoWireIfMissing();
    }
#endif

    private void AutoWireIfMissing()
    {
        // Base renderer: prefer SpriteRenderer on this object,
        // otherwise look for Square / CardBase / Base.
        if (cardRenderer == null)
        {
            cardRenderer = GetComponent<SpriteRenderer>();
            if (cardRenderer == null)
            {
                cardRenderer = FindChildRendererAnyOf("Square", "CardBase", "Base", "Background");
            }
        }

        // Face renderer: common names
        if (faceRenderer == null)
        {
            faceRenderer = FindChildRendererAnyOf("CardFace", "Face", "Art");
        }

        // Frame renderer: your project seems to use CardFrame
        if (frameRenderer == null)
        {
            
        Transform frame = transform.Find("Frame");
        if (frame == null) frame = transform.Find("CardFrame");
        if (frame != null) frameRenderer = frame.GetComponent<SpriteRenderer>();

        }

        if(frontBaseSprite == null && cardRenderer != null)
        {
            frontBaseSprite = cardRenderer.sprite;
        }
    }

    private SpriteRenderer FindChildRendererAnyOf(params string[] names)
    {
        foreach (string n in names)
        {
            Transform t = transform.Find(n);
            if (t != null)
            {
                var sr = t.GetComponent<SpriteRenderer>();
                if (sr != null) return sr;
            }
        }

        // fallback: deep search by name
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            foreach (string n in names)
            {
                if (child.name == n)
                {
                    var sr = child.GetComponent<SpriteRenderer>();
                    if (sr != null) return sr;
                }
            }
        }

        return null;
    }

    public void SetSortingOrder(int baseOrder)
    {
        AutoWireIfMissing();

        // Pick a "reference" sorting layer from whichever renderer exists
        int sortingLayerId =
            (cardRenderer != null) ? cardRenderer.sortingLayerID :
            (faceRenderer != null) ? faceRenderer.sortingLayerID :
            (frameRenderer != null) ? frameRenderer.sortingLayerID :
            SortingLayer.NameToID("Default");

        if (cardRenderer != null)
        {
            cardRenderer.sortingLayerID = sortingLayerId;
            cardRenderer.sortingOrder = baseOrder;
        }

        if (faceRenderer != null)
        {
            faceRenderer.sortingLayerID = sortingLayerId;
            faceRenderer.sortingOrder = baseOrder + FaceOffset;
        }

        if (frameRenderer != null)
        {
            frameRenderer.sortingLayerID = sortingLayerId;
            frameRenderer.sortingOrder = baseOrder + FrameOffset;
        }
    }

    public void Initialize(int id, CardData data)
    {
        AutoWireIfMissing();

        CardID = id;
        currentCardData = data;

        if (nameText != null)
            nameText.text = (data != null && !string.IsNullOrEmpty(data.cardName)) ? data.cardName : "";

        ApplyVisualsFromData();

        // Preserve current face-down state
        SetFaceDown(isFaceDown, cardBackSprite);
    }

    private void ApplyVisualsFromData()
    {
        if (currentCardData == null)
        {
            if (faceRenderer != null) faceRenderer.enabled = false;
            if (frameRenderer != null) frameRenderer.enabled = false;

            if (cardRenderer != null)
            {
                // don’t force sprite null if its used as base sprite
                // cardRenderer.sprite = null;
                cardRenderer.color = (CardID >= 60) ? Color.yellow : Color.white;
                cardRenderer.enabled = true;
            }
            return;
        }

        // Face art
        if (faceRenderer != null)
        {
            faceRenderer.sprite = currentCardData.cardArt;
            faceRenderer.enabled = !isFaceDown;
        }

        // Tier frame
        if (frameRenderer != null)
        {
            frameRenderer.sprite = currentCardData.tierFrame;
            frameRenderer.enabled = !isFaceDown && currentCardData.tierFrame != null;
        }
        else
        {
            Debug.LogWarning($"CARD VISUAL || Frame renderer is NULL on {name}. Rename child to 'CardFrame' or 'Frame'.");
        }

        // Background tint
        if (cardRenderer != null)
        {
    
            cardRenderer.color = currentCardData.themeColor;
            cardRenderer.enabled = true;
        }

        Debug.Log($"CARD VISUAL || {name} applied tier={currentCardData.tier} frame={(currentCardData.tierFrame ? currentCardData.tierFrame.name : "NULL")}");
    }

    public void RefreshFrame()
    {
        AutoWireIfMissing();

        if (currentCardData == null) return;
        if (frameRenderer == null) return;

        frameRenderer.sprite = currentCardData.tierFrame;
        frameRenderer.enabled = !isFaceDown && currentCardData.tierFrame != null;

        Debug.Log($"CARD VISUAL || RefreshFrame tier={currentCardData.tier} sprite={(frameRenderer.sprite ? frameRenderer.sprite.name : "NULL")}");
    }

    public void SetFaceDown(bool faceDown, Sprite cardBack = null)
    {
        isFaceDown = faceDown;
        if (cardBack != null) cardBackSprite = cardBack;

        AutoWireIfMissing();

        if (isFaceDown)
        {
            if (faceRenderer != null) faceRenderer.enabled = false;
            if (frameRenderer != null) frameRenderer.enabled = false;

            
            Transform squareChild = transform.Find("Square");
            if (squareChild != null)
            {
                SpriteRenderer squareSr = squareChild.GetComponent<SpriteRenderer>();
                if (squareSr != null && squareSr != cardRenderer) squareSr.enabled = false;
            }

            if (cardRenderer != null)
            {
                if (cardBackSprite != null)
                {
                    cardRenderer.sprite = cardBackSprite;
                    cardRenderer.color = Color.white;
                }
                cardRenderer.enabled = true;
            }
        }
        else
        {
            // face-up
            if (faceRenderer != null) faceRenderer.enabled = true;

            if (frameRenderer != null)
            {
                frameRenderer.sprite = (currentCardData != null) ? currentCardData.tierFrame : null;
                frameRenderer.enabled = (currentCardData != null && currentCardData.tierFrame != null);
            }

            Transform squareChild = transform.Find("Square");
            if (squareChild != null)
            {
                SpriteRenderer squareSr = squareChild.GetComponent<SpriteRenderer>();
                if (squareSr != null && squareSr != cardRenderer) squareSr.enabled = true;
            }

            if (cardRenderer != null)
            {
                // restores base/front sprite if it was swapped to cardBack before
                 if (frontBaseSprite != null)
                    cardRenderer.sprite = frontBaseSprite;

                 if (currentCardData != null)
                    cardRenderer.color = currentCardData.themeColor;

                 cardRenderer.enabled = true;
            }
        }

        CardShadow shadow = GetComponent<CardShadow>();
        if (shadow != null) shadow.UpdateShadowSprite();
    }

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