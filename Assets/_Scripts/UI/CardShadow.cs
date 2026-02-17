using UnityEngine;

public class CardShadow : MonoBehaviour
{
    [Header("Shadow Appearance")]
    [SerializeField] private Color shadowColor = new Color(0, 0, 0, 0.3f);
    [SerializeField] private Vector3 shadowOffset = new Vector3(0.15f, -0.15f, 0.05f);
    [SerializeField] private Vector3 dragShadowOffset = new Vector3(0.2f, -0.2f, 0.05f); // make bigger offset when dragging
    [SerializeField] private Vector3 shadowScale = new Vector3(1.05f, 1.05f, 1f);
    
    [Header("Motion")]
    [SerializeField] private float interpolationSpeed = 8f;
    [SerializeField] private float dragInterpolationSpeed = 4f;
    
    [Header("Visibility")]
    [SerializeField] private bool showInHand = false;
    [SerializeField] private bool showWhenDragging = true;
    [SerializeField] private bool showOnBoard = true;
    
    private GameObject shadowObject;
    private SpriteRenderer shadowRenderer;
    private SpriteRenderer cardRenderer;
    private CardDraggable draggable;
    
    private Vector3 shadowTargetLocalPos;
    private bool wasJustCreated = true;

    private void Awake()
    {
        draggable = GetComponent<CardDraggable>();
        cardRenderer = GetComponent<SpriteRenderer>();
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
        // Create shadow as child object
        shadowObject = new GameObject("Shadow");
        shadowObject.transform.SetParent(transform);
        shadowObject.transform.localPosition = shadowOffset;
        shadowObject.transform.localRotation = Quaternion.identity;
        shadowObject.transform.localScale = shadowScale;
        
        // Add sprite renderer
        shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
        
        UpdateShadowSprite();
        
        // Style the shadow
        shadowRenderer.color = shadowColor;
        shadowRenderer.sortingLayerName = cardRenderer != null ? cardRenderer.sortingLayerName : "Default";
        shadowRenderer.sortingOrder = -10; // Way behind everything
        
        shadowTargetLocalPos = shadowOffset;
    }

    private void LateUpdate()
    {
        if (shadowObject == null || shadowRenderer == null) return;
        
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
            shadowRenderer.enabled = false;
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
        
        shadowRenderer.enabled = shouldShow;
    }

    /// <summary>
    /// Update shadow to match card's current sprite (face or card back when face-down).
    /// Call this if you change the card art at runtime or when flipping.
    /// </summary>
    public void UpdateShadowSprite()
    {
        if (shadowRenderer == null) return;
        
        CardVisual cardVisual = GetComponent<CardVisual>();
        // When face-down, card renderer shows the back; use it so shadow matches
        if (cardVisual != null && cardVisual.IsFaceDown && cardRenderer != null)
        {
            shadowRenderer.sprite = cardRenderer.sprite;
            return;
        }
        
        // Try to get face renderer first (visible front)
        foreach (Transform child in transform)
        {
            SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
            if (sr != null && child.name.Contains("Face") && sr.enabled)
            {
                shadowRenderer.sprite = sr.sprite;
                return;
            }
        }
        
        // Fallback to card renderer
        if (cardRenderer != null)
        {
            shadowRenderer.sprite = cardRenderer.sprite;
        }
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