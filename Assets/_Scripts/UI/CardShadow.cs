using UnityEngine;
using UnityEngine.UI;

public class CardShadow : MonoBehaviour
{
    [Header("Shadow Appearance")]
    [SerializeField] private Color shadowColor = new Color(0, 0, 0, 0.3f);
    [SerializeField] private Vector3 shadowOffset = new Vector3(0.1f, -0.1f, 0.01f);
    [SerializeField] private Vector3 dragShadowOffset = new Vector3(0.15f, -0.15f, 0.01f); // make bigger offset when dragging
    [SerializeField] private Vector3 shadowScale = new Vector3(1.0f, 1.0f, 1f);

    [Header("Rendering")]
    [Tooltip("Sorting offset relative to the card SpriteRenderer. -1 means shadow draws just behind the card.")]
    [SerializeField] private int shadowSortingOffset = -1;
    
    [Header("Motion")]
    [SerializeField] private float interpolationSpeed = 8f;
    [SerializeField] private float dragInterpolationSpeed = 4f;
    
    [Header("Visibility")]
    [SerializeField] private bool showInHand = false;
    [SerializeField] private bool showWhenDragging = true;
    [SerializeField] private bool showOnBoard = true;
    
    private GameObject shadowObject;
    private SpriteRenderer shadowRenderer;
    private Image shadowImage;
    private SpriteRenderer cardRenderer;
    private Image cardImage;
    private CardDraggable draggable;
    private RectTransform cardRectTransform;
    
    private Vector3 shadowTargetLocalPos;
    private bool wasJustCreated = true;

    private void Awake()
    {
        draggable = GetComponent<CardDraggable>();
        cardRenderer = GetComponent<SpriteRenderer>();
        cardImage = GetComponent<Image>();
        cardRectTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        CreateShadowSprite();
        // Start invisible, will be updated in LateUpdate
        if (shadowRenderer != null)
        {
            shadowRenderer.enabled = false;
        }
        wasJustCreated = false;
    }

    private void CreateShadowSprite()
    {
        // If this card is UI (under a Canvas), create a UI shadow so it sorts correctly with the background UI.
        if (cardRectTransform != null && GetComponentInParent<Canvas>() != null)
        {
            CreateUIShadow();
            return;
        }

        CreateSpriteShadow();
    }

    private void CreateSpriteShadow()
    {
        shadowObject = new GameObject("Shadow");
        shadowObject.transform.SetParent(transform);
        shadowObject.transform.localPosition = shadowOffset;
        shadowObject.transform.localRotation = Quaternion.identity;
        shadowObject.transform.localScale = shadowScale;

        shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
        shadowImage = null;

        UpdateShadowSprite();

        shadowRenderer.color = shadowColor;
        UpdateShadowSorting();

        shadowTargetLocalPos = shadowOffset;
    }

    private void CreateUIShadow()
    {
        shadowObject = new GameObject("Shadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        // Make shadow a sibling directly behind the card in the UI hierarchy.
        Transform parent = transform.parent != null ? transform.parent : transform;
        shadowObject.transform.SetParent(parent, false);

        int cardIndex = transform.GetSiblingIndex();
        shadowObject.transform.SetSiblingIndex(Mathf.Max(0, cardIndex));

        RectTransform shadowRt = shadowObject.GetComponent<RectTransform>();
        RectTransform cardRt = cardRectTransform;

        shadowRt.anchorMin = cardRt.anchorMin;
        shadowRt.anchorMax = cardRt.anchorMax;
        shadowRt.pivot = cardRt.pivot;
        shadowRt.sizeDelta = cardRt.sizeDelta;
        shadowRt.anchoredPosition = cardRt.anchoredPosition + (Vector2)shadowOffset;
        shadowRt.localRotation = Quaternion.identity;
        shadowRt.localScale = shadowScale;

        shadowImage = shadowObject.GetComponent<Image>();
        shadowRenderer = null;

        UpdateShadowSprite();

        shadowImage.color = shadowColor;

        shadowTargetLocalPos = shadowOffset;
    }

    private void UpdateShadowSorting()
    {
        if (shadowRenderer == null) return;

        // Follow the *current* card sorting (some scripts change sortingOrder at rest/drag).
        // Use the minimum sortingOrder among the card's sprite renderers so the shadow never ends up above any card layer.
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

        int minOrder = int.MaxValue;
        int layerId = 0;
        bool foundAny = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null || sr == shadowRenderer) continue;

            if (!foundAny)
            {
                layerId = sr.sortingLayerID;
                foundAny = true;
            }

            if (sr == cardRenderer)
            {
                layerId = cardRenderer.sortingLayerID;
            }

            if (sr.sortingOrder < minOrder)
            {
                minOrder = sr.sortingOrder;
            }
        }

        if (!foundAny)
        {
            shadowRenderer.sortingLayerName = "Default";
            shadowRenderer.sortingOrder = shadowSortingOffset;
            return;
        }

        shadowRenderer.sortingLayerID = layerId;
        shadowRenderer.sortingOrder = minOrder + shadowSortingOffset;
    }

    private void LateUpdate()
    {
        if (shadowObject == null) return;
        
        // Update visibility
        UpdateVisibility();

        // Update target offset based on drag state
        if (draggable != null && draggable.IsDragging)
        {
            shadowTargetLocalPos = dragShadowOffset;
        }
        else
        {
            shadowTargetLocalPos = shadowOffset;
        }
        
        // Smooth follow with interpolation
        float speed = (draggable != null && draggable.IsDragging) ? dragInterpolationSpeed : interpolationSpeed;

        // UI shadow path (keeps it properly layered with UI background).
        if (shadowImage != null)
        {
            if (shadowImage.enabled && cardRectTransform != null)
            {
                RectTransform shadowRt = (RectTransform)shadowObject.transform;
                shadowRt.anchoredPosition = Vector2.Lerp(
                    shadowRt.anchoredPosition,
                    cardRectTransform.anchoredPosition + (Vector2)shadowTargetLocalPos,
                    speed * Time.deltaTime
                );
            }
            return;
        }

        // Sprite shadow path.
        if (shadowRenderer == null) return;

        // Keep sorting synced even if other scripts change it.
        if (shadowRenderer.enabled)
        {
            UpdateShadowSorting();
        }
        
        shadowObject.transform.localPosition = Vector3.Lerp(
            shadowObject.transform.localPosition,
            shadowTargetLocalPos,
            speed * Time.deltaTime
        );
        
        // Shadow doesn't rotate with card (stays flat for depth effect)
        shadowObject.transform.localRotation = Quaternion.identity;
    }

    private void UpdateVisibility()
    {
        if (wasJustCreated)
        {
            if (shadowRenderer != null) shadowRenderer.enabled = false;
            if (shadowImage != null) shadowImage.enabled = false;
            return;
        }

        bool shouldShow = false;
        
        if (draggable != null)
        {
            // Show when dragging
            if (showWhenDragging && draggable.IsDragging)
            {
                shouldShow = true;
            }
            // Show when on board
            else if (showOnBoard && draggable.IsOnBoard)
            {
                shouldShow = true;
            }
            // Show in hand (usually disabled)
            else if (showInHand && !draggable.IsOnBoard && !draggable.IsDragging)
            {
                shouldShow = true;
            }
        }
        
        if (shadowRenderer != null) shadowRenderer.enabled = shouldShow;
        if (shadowImage != null) shadowImage.enabled = shouldShow;
    }

    /// <summary>
    /// Update shadow to match card's current sprite (face or card back when face-down).
    /// Call this if you change the card art at runtime or when flipping.
    /// </summary>
    public void UpdateShadowSprite()
    {
        if (shadowRenderer == null && shadowImage == null) return;
        
        CardVisual cardVisual = GetComponent<CardVisual>();

        // When face-down, try to use the card's currently shown sprite.
        if (cardVisual != null && cardVisual.IsFaceDown)
        {
            if (cardRenderer != null)
            {
                SetShadowSprite(cardRenderer.sprite);
                return;
            }
            if (cardImage != null)
            {
                SetShadowSprite(cardImage.sprite);
                return;
            }
        }
        
        // Try to get face renderer first (visible front)
        foreach (Transform child in transform)
        {
            SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
            if (sr != null && child.name.Contains("Face") && sr.enabled)
            {
                SetShadowSprite(sr.sprite);
                return;
            }

            Image img = child.GetComponent<Image>();
            if (img != null && child.name.Contains("Face") && img.enabled)
            {
                SetShadowSprite(img.sprite);
                return;
            }
        }
        
        // Fallback to card renderer
        if (cardRenderer != null)
        {
            SetShadowSprite(cardRenderer.sprite);
            return;
        }
        if (cardImage != null)
        {
            SetShadowSprite(cardImage.sprite);
        }
    }

    private void SetShadowSprite(Sprite sprite)
    {
        if (shadowRenderer != null) shadowRenderer.sprite = sprite;
        if (shadowImage != null) shadowImage.sprite = sprite;
    }

    /// <summary>
    /// Dynamically change shadow offset (e.g., for hover effects)
    /// </summary>
    public void SetShadowOffset(Vector3 newOffset, bool instant = false)
    {
        shadowTargetLocalPos = newOffset;
        
        if (instant && shadowObject != null)
        {
            shadowObject.transform.localPosition = newOffset;
        }
    }

    /// <summary>
    /// Set shadow color/alpha
    /// </summary>
    public void SetShadowColor(Color color)
    {
        shadowColor = color;
        if (shadowRenderer != null)
        {
            shadowRenderer.color = color;
        }
    }

    private void OnDestroy()
    {
        if (shadowObject != null)
        {
            Destroy(shadowObject);
        }
    }
}