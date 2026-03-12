using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class CardVisual : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private SpriteRenderer cardRenderer;
    [SerializeField] private SpriteRenderer faceRenderer;
    [SerializeField] private SpriteRenderer frameRenderer;
    [Tooltip("Optional. Assign in order: Element 0 = Star1, 1 = Star2, 2 = Star3. Count shown = card data maxCharges.")]
    [SerializeField] private SpriteRenderer[] chargeStars;

    public int CardID;
    
    // IMPORTANT: Store the current CardData so we can check the actual tier
    private CardData currentCardData;
    
    // Face-down state (opponent cards until reveal phase)
    private bool isFaceDown = false;
    private Sprite cardBackSprite;
    private bool hasBeenRevealed; // once true, card stays face-up

    // Visual override: opponent hand cards should hide frame + stars only while in opponent hand.
    private bool hideFrameAndStars = false;

    // Attack charges (runtime); when 0 and attack is used, card is removed
    private int currentCharges = 1;
    private bool scheduledToAttack = false;
    private const float AttackTiltAngle = -18f; // lean toward opponent (negative Z rotation)

    // Hover sorting: when hovered, card renders above all other game board UI
    private const int HoverTopSortingOrder = 1000;
    private int currentBaseSortingOrder = 0;
    private bool isHovered = false;

    [Header("Charge Star Tween (Upgrade)")]
    [SerializeField] private float upgradeStarPopScale = 1.35f;
    [SerializeField] private float upgradeStarPopDuration = 0.16f;
    [SerializeField] private float upgradeStarStagger = 0.04f;
    [SerializeField] private AnimationCurve upgradeStarPopCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    private Coroutine upgradeStarTweenCoroutine;

    // Public getter for current card data
    public CardData CurrentCardData => currentCardData;
    public bool IsFaceDown => isFaceDown;
    public int CurrentCharges => currentCharges;
    public bool ScheduledToAttack => scheduledToAttack;

    public void SetHideFrameAndStars(bool hide)
    {
        hideFrameAndStars = hide;
        if (frameRenderer != null) frameRenderer.enabled = !hideFrameAndStars && !isFaceDown;
        RefreshChargeStars();
    }

    public void Initialize(int id, CardData data)
    {
        CardID = id;
        currentCardData = data; // Store the actual CardData reference
        currentCharges = (data != null && data.maxCharges > 0) ? data.maxCharges : 1;
        scheduledToAttack = false;

        // Default local layering; board/hand managers will override base order per card
        UpdateSorting(0);
        
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

        RefreshChargeStars();
        Debug.Log($"CARD VISUAL || Initialize id={id}, data={data?.name}, tierFrame={data?.tierFrame}");
    }
    
    public void UpdateSorting(int baseSortingOrder)
    {
        currentBaseSortingOrder = baseSortingOrder;
        if (!isHovered)
        {
            ApplySorting();
        }
    }

    /// <summary>
    /// Set whether this card is hovered. When true, card renders on top of all other game board UI.
    /// </summary>
    public void SetHovered(bool hovered)
    {
        if (isHovered == hovered) return;
        isHovered = hovered;
        ApplySorting();
    }

    private void ApplySorting()
    {
        int baseOrder = isHovered ? HoverTopSortingOrder : currentBaseSortingOrder;

        if (cardRenderer != null)
        {
            cardRenderer.sortingOrder = baseOrder;
        }

        if (faceRenderer != null)
        {
            if (cardRenderer != null)
            {
                faceRenderer.sortingLayerID = cardRenderer.sortingLayerID;
            }
            faceRenderer.sortingOrder = baseOrder + 1;
        }

        if (frameRenderer != null)
        {
            if (cardRenderer != null)
            {
                frameRenderer.sortingLayerID = cardRenderer.sortingLayerID;
            }
            frameRenderer.sortingOrder = baseOrder + 2;
        }

        if (chargeStars != null)
        {
            for (int i = 0; i < chargeStars.Length; i++)
            {
                if (chargeStars[i] != null)
                {
                    if (cardRenderer != null)
                    {
                        chargeStars[i].sortingLayerID = cardRenderer.sortingLayerID;
                    }
                    chargeStars[i].sortingOrder = baseOrder + 3;
                }
            }
        }

        // Ensure the actual Star1/Star2/Star3 renderers (hierarchy) are sorted too.
        // This covers the common case where the visible star SpriteRenderers are not the same objects referenced by chargeStars[].
        Transform starRoot = transform.Find("TiltPivot");
        if (starRoot == null) starRoot = transform;
        for (int i = 1; i <= 3; i++)
        {
            Transform star = starRoot.Find("Star" + i);
            if (star == null) continue;
            SpriteRenderer sr = star.GetComponentInChildren<SpriteRenderer>();
            if (sr == null) continue;
            if (cardRenderer != null) sr.sortingLayerID = cardRenderer.sortingLayerID;
            sr.sortingOrder = baseOrder + 3;
        }
    }

    /// <summary>
    /// Show stars based on the card's current remaining charges.
    /// Uses GameObjects named Star1/Star2/Star3 on the card (under TiltPivot if present),
    /// and also updates the optional chargeStars array for correct sorting.
    /// </summary>
    private void RefreshChargeStars()
    {
        // Number of visible stars = remaining charges on this card
        int visibleCharges = Mathf.Max(0, currentCharges);
        bool showStars = !isFaceDown && !hideFrameAndStars;
        Transform starRoot = transform.Find("TiltPivot");
        if (starRoot == null) starRoot = transform;
        for (int i = 1; i <= 3; i++)
        {
            Transform star = starRoot.Find("Star" + i);
            if (star != null)
            {
                bool visible = showStars && (i <= visibleCharges);
                star.gameObject.SetActive(visible);
                SpriteRenderer sr = star.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) sr.enabled = visible;
            }
        }
        // Also update SpriteRenderer.enabled on chargeStars for sorting order consistency
        if (chargeStars != null)
        {
            for (int i = 0; i < chargeStars.Length; i++)
            {
                if (chargeStars[i] != null)
                    chargeStars[i].enabled = showStars && (i < visibleCharges);
            }
        }
    }

    /// <summary>
    /// Upgrade-only effect: make the currently visible stars "pop" (scale large then tween back).
    /// Called by merge/upgrade logic after Initialize(nextTier).
    /// </summary>
    public void TweenUpgradeStars()
    {
        if (!isActiveAndEnabled) return;
        if (isFaceDown) return;
        if (upgradeStarPopDuration <= 0f) return;

        if (upgradeStarTweenCoroutine != null)
        {
            StopCoroutine(upgradeStarTweenCoroutine);
            upgradeStarTweenCoroutine = null;
        }
        upgradeStarTweenCoroutine = StartCoroutine(TweenUpgradeStarsCoroutine());
    }

    private IEnumerator TweenUpgradeStarsCoroutine()
    {
        Transform starRoot = transform.Find("TiltPivot");
        if (starRoot == null) starRoot = transform;

        int visibleCharges = Mathf.Clamp(currentCharges, 0, 3);
        if (visibleCharges <= 0) yield break;

        var stars = new List<Transform>(visibleCharges);
        var baseScales = new List<Vector3>(visibleCharges);

        for (int i = 1; i <= visibleCharges; i++)
        {
            Transform star = starRoot.Find("Star" + i);
            if (star == null) continue;
            stars.Add(star);
            baseScales.Add(star.localScale);
        }

        if (stars.Count == 0) yield break;

        float dur = Mathf.Max(0.01f, upgradeStarPopDuration);
        float stagger = Mathf.Max(0f, upgradeStarStagger);
        float total = dur + (stagger * (stars.Count - 1));

        float time = 0f;
        while (time < total)
        {
            time += Time.deltaTime;

            for (int i = 0; i < stars.Count; i++)
            {
                float tStar = Mathf.Clamp01((time - (i * stagger)) / dur);
                float eased = upgradeStarPopCurve != null ? upgradeStarPopCurve.Evaluate(tStar) : tStar;

                Vector3 baseScale = baseScales[i];
                Vector3 popScale = baseScale * upgradeStarPopScale;
                stars[i].localScale = Vector3.Lerp(popScale, baseScale, eased);
            }

            yield return null;
        }

        for (int i = 0; i < stars.Count; i++)
        {
            stars[i].localScale = baseScales[i];
        }
    }
    
    /// Get the current tier of this card (accounts for upgrades)
    public int GetCurrentTier()
    {
        return currentCardData != null ? currentCardData.tier : 1;
    }

    /// <summary>
    /// Set card face-down (card back) or face-up (show art). For opponent cards until reveal.
    /// Once revealed, the card stays face-up (ignores future SetFaceDown(true)).
    /// </summary>
    public void SetFaceDown(bool faceDown, Sprite cardBack = null)
    {
        if (cardBack != null) cardBackSprite = cardBack;
        if (faceDown && hasBeenRevealed) return; // keep face-up after first reveal
        if (!faceDown) hasBeenRevealed = true;
        isFaceDown = faceDown;

        if (isFaceDown)
        {
            if (faceRenderer != null) faceRenderer.enabled = false;
            if (frameRenderer != null) frameRenderer.enabled = false;
            // Hide prefab "Square" child so its white sprite doesn't render over the card back
            var squareChild = transform.Find("Square");
            if (squareChild != null)
            {
                var squareSr = squareChild.GetComponent<SpriteRenderer>();
                if (squareSr != null) squareSr.enabled = false;
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
            if (faceRenderer != null) faceRenderer.enabled = true;
            if (frameRenderer != null) frameRenderer.enabled = !hideFrameAndStars;
            // Re-enable prefab "Square" child when face-up (e.g. after reveal)
            var squareChild = transform.Find("Square");
            if (squareChild != null)
            {
                var squareSr = squareChild.GetComponent<SpriteRenderer>();
                if (squareSr != null) squareSr.enabled = true;
            }
            if (cardRenderer != null)
            {
                if (currentCardData != null)
                {
                    cardRenderer.sprite = null;
                    cardRenderer.color = currentCardData.themeColor;
                }
            }
        }

        RefreshChargeStars();
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

    /// <summary>
    /// Set whether this card is scheduled to attack (planning phase). Applies tilt visual.
    /// </summary>
    public void SetScheduledToAttack(bool scheduled)
    {
        scheduledToAttack = scheduled;
        ApplyAttackTilt();
    }

    /// <summary>
    /// Apply or clear attack tilt based on scheduledToAttack.
    /// </summary>
    public void ApplyAttackTilt()
    {
        float targetZ = scheduledToAttack ? AttackTiltAngle : 0f;
        transform.localRotation = Quaternion.Euler(0f, 0f, targetZ);
    }

    /// <summary>
    /// Set current charges (e.g. after loading from server). Used to sync with server state.
    /// </summary>
    public void SetCharges(int charges)
    {
        currentCharges = Mathf.Max(0, charges);
        RefreshChargeStars();
    }

    /// <summary>
    /// Consume one attack charge. Returns true if charges remain, false if now 0.
    /// </summary>
    public bool ConsumeCharge()
    {
        currentCharges = Mathf.Max(0, currentCharges - 1);
        RefreshChargeStars();
        return currentCharges > 0;
    }

    /// <summary>
    /// Attack damage from current card data.
    /// </summary>
    public int GetAttackDamage()
    {
        return currentCardData != null ? currentCardData.attackDamage : 0;
    }
}