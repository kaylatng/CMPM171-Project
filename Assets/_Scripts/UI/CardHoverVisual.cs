using UnityEngine;
using System.IO;

public class CardHover25DVisual : MonoBehaviour
{
    [Header("Hook")]
    public CardHoverEffect hover;
    public CardDraggable draggable;

    [Header("Transforms")]
    public Transform cardRoot;   // scale + lift
    public Transform tiltPivot;  // rotate

    [Header("Motion")]
    public float hoverScale = 1.08f;
    public float liftWorld = 0.15f;
    public float tiltMaxDeg = 12f;
    public float smooth = 14f;

    [Header("Only hover when on board")]
    public bool onlyWhenOnBoard = true;

    [Header("Glint")]
    public Transform glintTransform;
    public SpriteRenderer glintRenderer;
    public float glintTravel = 0.8f;
    public float glintSpeed = 1.2f;
    public float glintAlpha = 0.35f;

    [Header("Idle Wobble")]
    public bool idleWobble = true;
    public float wobbleDegrees = 3f;
    public float wobbleSpeed = 2.5f;
    public float wobbleRamp = 10f;

    private float wobbleWeight = 0f;

    Vector3 pos0;
    Vector3 scale0;

    bool hovering;
    Vector2 p01 = new Vector2(0.5f, 0.5f);
    float glintT;
    Vector3 glintPos0;

    void Awake()
    {
        if (!hover) hover = GetComponent<CardHoverEffect>();
        if (!draggable) draggable = GetComponent<CardDraggable>();

        if (!cardRoot) cardRoot = transform;
        if (!tiltPivot) tiltPivot = cardRoot;

        pos0 = cardRoot.localPosition;
        scale0 = cardRoot.localScale;

        if (glintRenderer == null && glintTransform != null)
            glintRenderer = glintTransform.GetComponent<SpriteRenderer>();

        if (glintTransform != null)
            glintPos0 = glintTransform.localPosition;

        if (glintRenderer != null)
            SetGlintAlpha(0f);
    }

    void OnEnable()
    {
        if (!hover) return;
        hover.OnHoverEnter += Enter;
        hover.OnHoverExit += Exit;
        hover.OnHoverMove01 += Move01;
    }

    void OnDisable()
    {
        if (!hover) return;
        hover.OnHoverEnter -= Enter;
        hover.OnHoverExit -= Exit;
        hover.OnHoverMove01 -= Move01;
    }

    void Enter()
    {
        if (draggable != null && draggable.IsDragging) return;
        if (onlyWhenOnBoard && draggable != null && !draggable.IsOnBoard) return;

        hovering = true;
    }

    void Exit()
    {
        hovering = false;
    }

    void Move01(Vector2 v) => p01 = v;

    void Update()
    {
        bool active = hovering
            && (draggable == null || !draggable.IsDragging)
            && (!onlyWhenOnBoard || draggable == null || draggable.IsOnBoard);

        // When we're not actively hovering this frame, treat the current
        // local position as the new baseline so external layout (e.g.,
        // BoardSlot / BoardManager) can fully control where the card lives.
        if (!active)
        {
            pos0 = cardRoot.localPosition;
        }

        Vector2 p = (p01 * 2f) - Vector2.one;

        float tScale = active ? hoverScale : 1f;
        Vector3 targetScale = scale0 * tScale;
        Vector3 targetPos = pos0 + (active ? new Vector3(0f, liftWorld, 0f) : Vector3.zero);

        float targetWeight = (active && idleWobble) ? 1f : 0f;
        wobbleWeight = Mathf.Lerp(wobbleWeight, targetWeight, Time.deltaTime * wobbleRamp);

        float wobbleX = Mathf.Sin(Time.time * wobbleSpeed) * wobbleDegrees * wobbleWeight;
        float wobbleY = Mathf.Cos(Time.time * wobbleSpeed * 0.9f) * wobbleDegrees * 0.7f * wobbleWeight;

        float tiltX = active ? (-p.y * tiltMaxDeg + wobbleX) : 0f;
        float tiltY = active ? ( p.x * tiltMaxDeg + wobbleY) : 0f;

        Quaternion targetRot = Quaternion.Euler(tiltX, tiltY, 0f);

        cardRoot.localScale = Vector3.Lerp(cardRoot.localScale, targetScale, Time.deltaTime * smooth);
        cardRoot.localPosition = Vector3.Lerp(cardRoot.localPosition, targetPos, Time.deltaTime * smooth);
        tiltPivot.localRotation = Quaternion.Slerp(tiltPivot.localRotation, targetRot, Time.deltaTime * smooth);

        if (glintTransform != null && glintRenderer != null)
        {
            if (active) glintT += Time.deltaTime * glintSpeed;
            else glintT = Mathf.Lerp(glintT, 0f, Time.deltaTime * smooth);

            float sweep = Mathf.PingPong(glintT, 1f);
            float x = Mathf.Lerp(-glintTravel * 0.5f, glintTravel * 0.5f, sweep);

            glintTransform.localRotation = Quaternion.Euler(0, 0, -p.x * 10f);
            glintTransform.localPosition = glintPos0 + new Vector3(x, 0f, 0f);

            float a = active ? glintAlpha : 0f;
            SetGlintAlpha(Mathf.Lerp(glintRenderer.color.a, a, Time.deltaTime * smooth));
        }
    }

    void SetGlintAlpha(float a)
    {
        if (glintRenderer == null) return;
        var c = glintRenderer.color;
        c.a = a;
        glintRenderer.color = c;
    }
}
