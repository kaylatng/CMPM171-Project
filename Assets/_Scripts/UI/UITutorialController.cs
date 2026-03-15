using UnityEngine;
using TMPro;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controls the in-game UI tutorial flow.
/// Attach this to any GameObject in the MainGame scene and assign references in the Inspector.
/// </summary>
public class UITutorialController : MonoBehaviour
{
    public static UITutorialController Instance { get; private set; }

    [Header("Canvas Root")]
    [SerializeField] private GameObject uiTutorialCanvas;

    [Header("Highlights")]
    [SerializeField] private GameObject deckHighlight;           // Outline/indicator over the deck object
    [SerializeField] private GameObject handCardHighlight;       // Outline/indicator for the hand card step
    [SerializeField] private GameObject attackCardHighlight;     // Outline/indicator for the attack card step
    [SerializeField] private GameObject readyHighlight;          // Outline/indicator for the Ready button step

    [Header("AP Info Overlay")]
    [SerializeField] private GameObject apInfoPanel;             // Grey panel explaining AP usage
    [SerializeField] private GameObject outgoingDamageArrowHighlight;  // Arrow pointing at outgoing damage display (blinks on step 1)
    [SerializeField] private TextMeshProUGUI damageOutgoingTextTutorial;  // Text for outgoing damage explanation (step 1)
    [SerializeField] private GameObject apArrowHighlight;        // Arrow pointing at AP display
    [SerializeField] private TextMeshProUGUI apTutorialText;     // Text for AP explanation (step 2)

    [Header("Undo Tip (on AP panel)")]
    [SerializeField] private GameObject undoArrowHighlight;      // Arrow pointing at Undo button
    [SerializeField] private TextMeshProUGUI undoTutorialText;   // Text shown only for the Undo tip

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI tutorialText;

    private enum TutorialStep
    {
        None,
        HighlightDeck,
        HighlightHandCard,
        HighlightAttackCard,
        HighlightAp,
        HighlightReady,
        MergeTip,
        UndoTip,
        Done
    }

    private TutorialStep currentStep = TutorialStep.None;
    private bool tutorialEnabled;

    /// <summary>0 = damage, 1 = AP, then we switch to UndoTip step.</summary>
    private int apPanelSubStep = 0;

    // One-time merge tip tracking
    private bool hasSeenFirstPlanningPhase = false;
    private bool hasShownMergeTip = false;

    // Flash routines for highlights
    private Coroutine deckFlashRoutine;
    private Coroutine handFlashRoutine;
    private Coroutine attackFlashRoutine;
    private Coroutine readyFlashRoutine;
    private Coroutine outgoingDamageArrowFlashRoutine;
    private Coroutine apArrowFlashRoutine;
    private Coroutine undoArrowFlashRoutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (uiTutorialCanvas == null)
        {
            // Try to find a canvas named "UITutorial" in the scene if not assigned.
            var found = GameObject.Find("UITutorial");
            if (found != null)
            {
                uiTutorialCanvas = found;
            }
        }

        tutorialEnabled = TutorialToggleHandler.TutorialSelected;

        if (uiTutorialCanvas != null)
        {
            uiTutorialCanvas.SetActive(tutorialEnabled);
        }

        // Reset highlights/text
        SetHighlight(deckHighlight, false);
        SetHighlight(handCardHighlight, false);
        SetHighlight(attackCardHighlight, false);
        SetHighlight(readyHighlight, false);
        SetHighlight(apArrowHighlight, false);
        SetHighlight(undoArrowHighlight, false);

        if (apInfoPanel != null)
        {
            apInfoPanel.SetActive(false);
        }
        if (apTutorialText != null)
        {
            apTutorialText.gameObject.SetActive(false);
        }
        if (damageOutgoingTextTutorial != null)
        {
            damageOutgoingTextTutorial.gameObject.SetActive(false);
        }

        // Ensure highlights are visual-only and do not block clicks.
        ConfigureHighlightForClicks(deckHighlight);
        ConfigureHighlightForClicks(handCardHighlight);
        ConfigureHighlightForClicks(attackCardHighlight);
        ConfigureHighlightForClicks(readyHighlight);
        ConfigureHighlightForClicks(outgoingDamageArrowHighlight);
        ConfigureHighlightForClicks(apArrowHighlight);
        ConfigureHighlightForClicks(undoArrowHighlight);
        if (tutorialText != null)
        {
            tutorialText.gameObject.SetActive(false);
            tutorialText.text = string.Empty;
        }
        if (undoTutorialText != null)
        {
            undoTutorialText.gameObject.SetActive(false);
        }

        if (tutorialEnabled)
        {
            BeginDeckStep();
        }
    }

    private void Update()
    {
        if (!tutorialEnabled) return;

        if (currentStep == TutorialStep.HighlightAp || currentStep == TutorialStep.UndoTip || currentStep == TutorialStep.MergeTip)
        {
            bool clicked = false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                clicked = true;
            }
#else
            if (Input.GetMouseButtonDown(0))
            {
                clicked = true;
            }
#endif

            if (clicked)
            {
                if (currentStep == TutorialStep.HighlightAp || currentStep == TutorialStep.UndoTip)
                {
                    NotifyApOverlayClicked();
                }
                else if (currentStep == TutorialStep.MergeTip)
                {
                    DismissMergeTip();
                }
            }
        }
    }

    private void BeginDeckStep()
    {
        currentStep = TutorialStep.HighlightDeck;

        if (uiTutorialCanvas != null)
            uiTutorialCanvas.SetActive(true);

        SetHighlight(deckHighlight, true);
        SetHighlight(handCardHighlight, false);

        // Start flashing the deck highlight color to guide the player.
        if (deckFlashRoutine == null && deckHighlight != null)
        {
            deckFlashRoutine = StartCoroutine(FlashDeckHighlight());
        }

        if (tutorialText != null)
        {
            tutorialText.gameObject.SetActive(true);
            tutorialText.text = "Click the shared deck to draw a card. Your opponent is using the same deck!";
        }
    }

    private void BeginHandCardStep()
    {
        currentStep = TutorialStep.HighlightHandCard;

        SetHighlight(deckHighlight, false);

        // Show the hand highlight where it has been manually placed in the scene.
        if (handCardHighlight != null)
        {
            SetHighlight(handCardHighlight, true);

            if (handFlashRoutine == null)
            {
                handFlashRoutine = StartCoroutine(FlashHandHighlight());
            }
        }

        if (tutorialText != null)
        {
            tutorialText.gameObject.SetActive(true);
            tutorialText.text = "Drag and drop a card from your hand to the middle of the screen.";
        }
    }

    private void BeginAttackCardStep()
    {
        currentStep = TutorialStep.HighlightAttackCard;

        SetHighlight(deckHighlight, false);
        SetHighlight(handCardHighlight, false);
        SetHighlight(readyHighlight, false);

        if (attackCardHighlight != null)
        {
            SetHighlight(attackCardHighlight, true);

            if (attackFlashRoutine == null)
            {
                attackFlashRoutine = StartCoroutine(FlashAttackHighlight());
            }
        }

        if (tutorialText != null)
        {
            tutorialText.gameObject.SetActive(true);
            tutorialText.text = "Tap on a card on the board to attack your opponent.";
        }
    }

    private void BeginApStep()
    {
        currentStep = TutorialStep.HighlightAp;
        apPanelSubStep = 0;

        SetHighlight(deckHighlight, false);
        SetHighlight(handCardHighlight, false);
        SetHighlight(attackCardHighlight, false);
        SetHighlight(readyHighlight, false);
        SetHighlight(outgoingDamageArrowHighlight, false);
        SetHighlight(apArrowHighlight, false);
        SetHighlight(undoArrowHighlight, false);

        if (apInfoPanel != null)
        {
            apInfoPanel.SetActive(true);
        }

        // Keep main tutorial text empty during AP step.
        if (tutorialText != null)
        {
            tutorialText.text = string.Empty;
            tutorialText.gameObject.SetActive(false);
        }

        // Step 1: Outgoing damage explanation first; show damage text and blink the damage arrow.
        if (apTutorialText != null)
        {
            apTutorialText.gameObject.SetActive(false);
        }
        if (damageOutgoingTextTutorial != null)
        {
            damageOutgoingTextTutorial.gameObject.SetActive(true);
            damageOutgoingTextTutorial.text = "Outgoing damage shows the total damage your attacking cards will deal to your opponent's health. First player to reach 0 HP loses! Click to continue.";
        }
        if (outgoingDamageArrowHighlight != null)
        {
            SetHighlight(outgoingDamageArrowHighlight, true);
            if (outgoingDamageArrowFlashRoutine == null)
            {
                outgoingDamageArrowFlashRoutine = StartCoroutine(FlashOutgoingDamageArrowHighlight());
            }
        }
        if (undoTutorialText != null)
        {
            undoTutorialText.gameObject.SetActive(false);
        }
    }

    private void BeginReadyStep()
    {
        currentStep = TutorialStep.HighlightReady;

        SetHighlight(deckHighlight, false);
        SetHighlight(handCardHighlight, false);
        SetHighlight(attackCardHighlight, false);

        if (readyHighlight != null)
        {
            SetHighlight(readyHighlight, true);

            if (readyFlashRoutine == null)
            {
                readyFlashRoutine = StartCoroutine(FlashReadyHighlight());
            }
        }

        if (tutorialText != null)
        {
            tutorialText.gameObject.SetActive(true);
            tutorialText.text = "Click READY, then wait for your opponent. You cannot edit your actions after clicking READY!";
        }
    }

    private void BeginMergeTipStep()
    {
        currentStep = TutorialStep.MergeTip;

        if (uiTutorialCanvas != null)
            uiTutorialCanvas.SetActive(true);

        SetHighlight(deckHighlight, false);
        SetHighlight(handCardHighlight, false);
        SetHighlight(attackCardHighlight, false);
        SetHighlight(readyHighlight, false);
        SetHighlight(outgoingDamageArrowHighlight, false);
        SetHighlight(apArrowHighlight, false);

        if (apInfoPanel != null)
        {
            apInfoPanel.SetActive(false);
        }
        if (apTutorialText != null)
        {
            apTutorialText.gameObject.SetActive(false);
        }
        if (damageOutgoingTextTutorial != null)
        {
            damageOutgoingTextTutorial.gameObject.SetActive(false);
        }

        if (tutorialText != null)
        {
            tutorialText.gameObject.SetActive(true);
            tutorialText.text = "Play cards with the same picture to increase its damage. Don't let your opponent upgrade before you!";
        }
    }

    private void CompleteTutorial()
    {
        currentStep = TutorialStep.Done;

        SetHighlight(deckHighlight, false);
        SetHighlight(handCardHighlight, false);
        SetHighlight(attackCardHighlight, false);
        SetHighlight(readyHighlight, false);
        SetHighlight(outgoingDamageArrowHighlight, false);

        if (tutorialText != null)
        {
            tutorialText.gameObject.SetActive(false);
        }
        if (apTutorialText != null)
        {
            apTutorialText.gameObject.SetActive(false);
        }
        if (damageOutgoingTextTutorial != null)
        {
            damageOutgoingTextTutorial.gameObject.SetActive(false);
        }
        if (undoTutorialText != null)
        {
            undoTutorialText.gameObject.SetActive(false);
        }
    }

    private void DismissMergeTip()
    {
        if (tutorialText != null)
        {
            tutorialText.gameObject.SetActive(false);
        }
        currentStep = TutorialStep.Done;
        hasShownMergeTip = true;
    }

    private void SetHighlight(GameObject highlight, bool enabled)
    {
        if (highlight == null) return;
        highlight.SetActive(enabled);
    }

    /// <summary>
    /// Make the given highlight object visual-only so it doesn't intercept UI or physics clicks.
    /// </summary>
    private void ConfigureHighlightForClicks(GameObject highlight)
    {
        if (highlight == null) return;

        // Disable UI raycasts on any graphics under this highlight.
        var graphics = highlight.GetComponentsInChildren<Graphic>(true);
        foreach (var g in graphics)
        {
            g.raycastTarget = false;
        }

        // Ensure any CanvasGroup present does not block raycasts or interaction.
        var groups = highlight.GetComponentsInChildren<CanvasGroup>(true);
        foreach (var cg in groups)
        {
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        // If there are colliders, disable them so physics raycasts pass through.
        var colliders2D = highlight.GetComponentsInChildren<Collider2D>(true);
        foreach (var c in colliders2D)
        {
            c.enabled = false;
        }
    }

    private System.Collections.IEnumerator FlashDeckHighlight()
    {
        if (deckHighlight == null)
        {
            deckFlashRoutine = null;
            yield break;
        }

        var sr = deckHighlight.GetComponent<SpriteRenderer>();
        var img = deckHighlight.GetComponent<Image>();
        if (sr == null && img == null)
        {
            deckFlashRoutine = null;
            yield break;
        }

        Color original = sr != null ? sr.color : img.color;
        float maxAlpha = original.a;
        float minAlpha = 0.15f * maxAlpha;

        while (tutorialEnabled && currentStep == TutorialStep.HighlightDeck && deckHighlight != null)
        {
            if (!deckHighlight.activeInHierarchy)
            {
                yield return null;
                continue;
            }

            // Smoothly ease alpha up and down over time.
            float t = Mathf.PingPong(Time.time * 2f, 1f);              // 0..1..0
            float eased = t * t * (3f - 2f * t);                       // smoothstep
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, eased);

            Color c = original;
            c.a = alpha;
            if (sr != null) sr.color = c;
            if (img != null) img.color = c;

            yield return null;
        }

        // Restore original color when done.
        if (deckHighlight != null)
        {
            if (sr != null) sr.color = original;
            if (img != null) img.color = original;
        }

        deckFlashRoutine = null;
    }

    private System.Collections.IEnumerator FlashHandHighlight()
    {
        if (handCardHighlight == null)
        {
            handFlashRoutine = null;
            yield break;
        }

        var sr = handCardHighlight.GetComponent<SpriteRenderer>();
        var img = handCardHighlight.GetComponent<Image>();
        if (sr == null && img == null)
        {
            handFlashRoutine = null;
            yield break;
        }

        Color original = sr != null ? sr.color : img.color;
        float maxAlpha = original.a;
        float minAlpha = 0.15f * maxAlpha;

        while (tutorialEnabled && currentStep == TutorialStep.HighlightHandCard && handCardHighlight != null)
        {
            if (!handCardHighlight.activeInHierarchy)
            {
                yield return null;
                continue;
            }

            // Smoothly ease alpha up and down over time.
            float t = Mathf.PingPong(Time.time * 2f, 1f);              // 0..1..0
            float eased = t * t * (3f - 2f * t);                       // smoothstep
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, eased);

            Color c = original;
            c.a = alpha;
            if (sr != null) sr.color = c;
            if (img != null) img.color = c;

            yield return null;
        }

        // Restore original color when done.
        if (handCardHighlight != null)
        {
            if (sr != null) sr.color = original;
            if (img != null) img.color = original;
        }

        handFlashRoutine = null;
    }

    private System.Collections.IEnumerator FlashAttackHighlight()
    {
        if (attackCardHighlight == null)
        {
            attackFlashRoutine = null;
            yield break;
        }

        var sr = attackCardHighlight.GetComponent<SpriteRenderer>();
        var img = attackCardHighlight.GetComponent<Image>();
        if (sr == null && img == null)
        {
            attackFlashRoutine = null;
            yield break;
        }

        Color original = sr != null ? sr.color : img.color;
        float maxAlpha = original.a;
        float minAlpha = 0.15f * maxAlpha;

        while (tutorialEnabled && currentStep == TutorialStep.HighlightAttackCard && attackCardHighlight != null)
        {
            if (!attackCardHighlight.activeInHierarchy)
            {
                yield return null;
                continue;
            }

            // Smoothly ease alpha up and down over time.
            float t = Mathf.PingPong(Time.time * 2f, 1f);              // 0..1..0
            float eased = t * t * (3f - 2f * t);                       // smoothstep
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, eased);

            Color c = original;
            c.a = alpha;
            if (sr != null) sr.color = c;
            if (img != null) img.color = c;

            yield return null;
        }

        // Restore original color when done.
        if (attackCardHighlight != null)
        {
            if (sr != null) sr.color = original;
            if (img != null) img.color = original;
        }

        attackFlashRoutine = null;
    }

    private System.Collections.IEnumerator FlashReadyHighlight()
    {
        if (readyHighlight == null)
        {
            readyFlashRoutine = null;
            yield break;
        }

        var sr = readyHighlight.GetComponent<SpriteRenderer>();
        var img = readyHighlight.GetComponent<Image>();
        if (sr == null && img == null)
        {
            readyFlashRoutine = null;
            yield break;
        }

        Color original = sr != null ? sr.color : img.color;
        float maxAlpha = original.a;
        float minAlpha = 0.15f * maxAlpha;

        while (tutorialEnabled && currentStep == TutorialStep.HighlightReady && readyHighlight != null)
        {
            if (!readyHighlight.activeInHierarchy)
            {
                yield return null;
                continue;
            }

            // Smoothly ease alpha up and down over time.
            float t = Mathf.PingPong(Time.time * 2f, 1f);              // 0..1..0
            float eased = t * t * (3f - 2f * t);                       // smoothstep
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, eased);

            Color c = original;
            c.a = alpha;
            if (sr != null) sr.color = c;
            if (img != null) img.color = c;

            yield return null;
        }

        // Restore original color when done.
        if (readyHighlight != null)
        {
            if (sr != null) sr.color = original;
            if (img != null) img.color = original;
        }

        readyFlashRoutine = null;
    }

    private System.Collections.IEnumerator FlashOutgoingDamageArrowHighlight()
    {
        if (outgoingDamageArrowHighlight == null)
        {
            outgoingDamageArrowFlashRoutine = null;
            yield break;
        }

        var sr = outgoingDamageArrowHighlight.GetComponent<SpriteRenderer>();
        var img = outgoingDamageArrowHighlight.GetComponent<Image>();
        if (sr == null && img == null)
        {
            outgoingDamageArrowFlashRoutine = null;
            yield break;
        }

        Color original = sr != null ? sr.color : img.color;
        float maxAlpha = original.a;
        float minAlpha = 0.15f * maxAlpha;

        while (tutorialEnabled && currentStep == TutorialStep.HighlightAp && apPanelSubStep == 0 && outgoingDamageArrowHighlight != null)
        {
            if (!outgoingDamageArrowHighlight.activeInHierarchy)
            {
                yield return null;
                continue;
            }

            float t = Mathf.PingPong(Time.time * 2f, 1f);
            float eased = t * t * (3f - 2f * t);
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, eased);

            Color c = original;
            c.a = alpha;
            if (sr != null) sr.color = c;
            if (img != null) img.color = c;

            yield return null;
        }

        if (outgoingDamageArrowHighlight != null)
        {
            if (sr != null) sr.color = original;
            if (img != null) img.color = original;
        }

        outgoingDamageArrowFlashRoutine = null;
    }

    private System.Collections.IEnumerator FlashApArrowHighlight()
    {
        if (apArrowHighlight == null)
        {
            apArrowFlashRoutine = null;
            yield break;
        }

        var sr = apArrowHighlight.GetComponent<SpriteRenderer>();
        var img = apArrowHighlight.GetComponent<Image>();
        if (sr == null && img == null)
        {
            apArrowFlashRoutine = null;
            yield break;
        }

        Color original = sr != null ? sr.color : img.color;
        float maxAlpha = original.a;
        float minAlpha = 0.15f * maxAlpha;

        while (tutorialEnabled && currentStep == TutorialStep.HighlightAp && apArrowHighlight != null)
        {
            if (!apArrowHighlight.activeInHierarchy)
            {
                yield return null;
                continue;
            }

            // Smoothly ease alpha up and down over time.
            float t = Mathf.PingPong(Time.time * 2f, 1f);              // 0..1..0
            float eased = t * t * (3f - 2f * t);                       // smoothstep
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, eased);

            Color c = original;
            c.a = alpha;
            if (sr != null) sr.color = c;
            if (img != null) img.color = c;

            yield return null;
        }

        // Restore original color when done.
        if (apArrowHighlight != null)
        {
            if (sr != null) sr.color = original;
            if (img != null) img.color = original;
        }

        apArrowFlashRoutine = null;
    }

    private System.Collections.IEnumerator FlashUndoArrowHighlight()
    {
        if (undoArrowHighlight == null)
        {
            undoArrowFlashRoutine = null;
            yield break;
        }

        var sr = undoArrowHighlight.GetComponent<SpriteRenderer>();
        var img = undoArrowHighlight.GetComponent<Image>();
        if (sr == null && img == null)
        {
            undoArrowFlashRoutine = null;
            yield break;
        }

        Color original = sr != null ? sr.color : img.color;
        float maxAlpha = original.a;
        float minAlpha = 0.15f * maxAlpha;

        while (tutorialEnabled && currentStep == TutorialStep.UndoTip && undoArrowHighlight != null)
        {
            if (!undoArrowHighlight.activeInHierarchy)
            {
                yield return null;
                continue;
            }

            float t = Mathf.PingPong(Time.time * 2f, 1f);
            float eased = t * t * (3f - 2f * t);
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, eased);

            Color c = original;
            c.a = alpha;
            if (sr != null) sr.color = c;
            if (img != null) img.color = c;

            yield return null;
        }

        if (undoArrowHighlight != null)
        {
            if (sr != null) sr.color = original;
            if (img != null) img.color = original;
        }

        undoArrowFlashRoutine = null;
    }

    /// <summary>
    /// Called by DeckClickable when the player successfully clicks the deck.
    /// </summary>
    public void NotifyDeckClicked()
    {
        if (!tutorialEnabled) return;
        if (currentStep != TutorialStep.HighlightDeck) return;

        BeginHandCardStep();
    }

    /// <summary>
    /// Called by CardDraggable when a card has been placed on the player board.
    /// </summary>
    public void NotifyCardPlacedOnPlayerBoardFromHand(GameObject cardObject, bool fromPlayerHand)
    {
        if (!tutorialEnabled) return;
        if (currentStep != TutorialStep.HighlightHandCard) return;
        if (!fromPlayerHand) return;
        BeginAttackCardStep();
    }

    /// <summary>
    /// Called by CardDraggable when the player taps a card on their board to attack.
    /// </summary>
    public void NotifyBoardCardTappedForAttack()
    {
        if (!tutorialEnabled) return;
        if (currentStep != TutorialStep.HighlightAttackCard) return;
        BeginApStep();
    }

    /// <summary>
    /// Called on left-click while AP panel is active. Advances: damage → AP → undo → close.
    /// </summary>
    public void NotifyApOverlayClicked()
    {
        if (!tutorialEnabled) return;

        if (currentStep == TutorialStep.HighlightAp)
        {
            if (apPanelSubStep == 0)
            {
                // First click: hide damage text and arrow, show AP explanation and AP arrow.
                apPanelSubStep = 1;
                if (outgoingDamageArrowHighlight != null)
                {
                    SetHighlight(outgoingDamageArrowHighlight, false);
                }
                if (damageOutgoingTextTutorial != null)
                {
                    damageOutgoingTextTutorial.gameObject.SetActive(false);
                }
                if (apTutorialText != null)
                {
                    apTutorialText.gameObject.SetActive(true);
                    apTutorialText.text = "Action Points (AP) are used to play cards and to attack. Manually drawing a card, playing a card or attacking costs 1 AP.";
                }
                if (apArrowHighlight != null)
                {
                    SetHighlight(apArrowHighlight, true);
                    if (apArrowFlashRoutine == null)
                    {
                        apArrowFlashRoutine = StartCoroutine(FlashApArrowHighlight());
                    }
                }
                return;
            }

            if (apPanelSubStep == 1)
            {
                // Second click: switch to Undo explanation, show undo arrow.
                if (apArrowHighlight != null)
                {
                    SetHighlight(apArrowHighlight, false);
                }
                if (apTutorialText != null)
                {
                    apTutorialText.gameObject.SetActive(false);
                }

                currentStep = TutorialStep.UndoTip;

                if (undoArrowHighlight != null)
                {
                    SetHighlight(undoArrowHighlight, true);
                    if (undoArrowFlashRoutine == null)
                    {
                        undoArrowFlashRoutine = StartCoroutine(FlashUndoArrowHighlight());
                    }
                }
                if (undoTutorialText != null)
                {
                    undoTutorialText.gameObject.SetActive(true);
                    undoTutorialText.text = "Misplaced a card? Use Undo to return your most recent played card to your hand and refund its cost.";
                }
                return;
            }
        }

        // Third click (or when already on UndoTip): hide AP panel and go to Ready step.
        if (currentStep == TutorialStep.UndoTip)
        {
            if (apInfoPanel != null)
            {
                apInfoPanel.SetActive(false);
            }
            if (undoArrowHighlight != null)
            {
                SetHighlight(undoArrowHighlight, false);
            }
            if (undoTutorialText != null)
            {
                undoTutorialText.gameObject.SetActive(false);
            }

            BeginReadyStep();
        }
    }

    /// <summary>
    /// Called by GameManagerUI when the player clicks Ready.
    /// </summary>
    public void NotifyReadyClicked()
    {
        if (!tutorialEnabled) return;
        if (currentStep != TutorialStep.HighlightReady) return;
        CompleteTutorial();
    }

    /// <summary>
    /// Called by GameManagerUI whenever the phase switches to Planning.
    /// </summary>
    public void NotifyPlanningPhaseStarted()
    {
        if (!tutorialEnabled) return;
        if (hasShownMergeTip) return;

        // Skip the very first Planning phase; show tip on the next one.
        if (!hasSeenFirstPlanningPhase)
        {
            hasSeenFirstPlanningPhase = true;
            return;
        }

        BeginMergeTipStep();
    }
}

