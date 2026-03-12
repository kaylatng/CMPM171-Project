using UnityEngine;

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
    public float glintAlpha = 0.35f;
    public float glintDuration = 0.35f;
    public float glintStartX = -0.45f;
    public float glintEndX = 0.45f;
    public float glintY = 0f;
    public bool playGlintOncePerHover = true;

    [Header("Idle Wobble")]
    public bool idleWobble = true;
    public float wobbleDegrees = 3f;
    public float wobbleSpeed = 2.5f;
    public float wobbleRamp = 10f;

    // Other Variables
    private float wobbleWeight = 0f;

    private Vector3 rootScale0;
    private Vector3 tiltPos0;
    private Vector3 glintPos0;

    private bool hovering;
    private Vector2 p01 = new Vector2(0.5f, 0.5f);

    private bool glintPlaying = false;
    private float glintTimer = 0f;
    private bool glintPlayedThisHover = false;

    void Awake()
    {
        if (!hover) hover = GetComponent<CardHoverEffect>();
        if (!draggable) draggable = GetComponent<CardDraggable>();

        if (!cardRoot) cardRoot = transform;
        if (!tiltPivot) tiltPivot = cardRoot;

        rootScale0 = cardRoot.localScale;
        tiltPos0 = tiltPivot.localPosition;

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

        if (glintTransform != null && glintRenderer != null)
        {
            if (!playGlintOncePerHover || !glintPlayedThisHover)
            {
                glintPlaying = true;
                glintTimer = 0f;
                glintPlayedThisHover = true;

                glintTransform.localPosition = glintPos0 + new Vector3(glintStartX, glintY, 0f);
                glintTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
                SetGlintAlpha(glintAlpha);
            }
        }
    }

    void Exit()
    {
        hovering = false;
        glintPlaying = false;
        glintTimer = 0f;
        glintPlayedThisHover = false;

        if (glintTransform != null)
        {
            glintTransform.localPosition = glintPos0;
            glintTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
        }

        SetGlintAlpha(0f);
    }

    void Move01(Vector2 v) => p01 = v;

    void Update()
    {
        bool active = hovering
            && (draggable == null || !draggable.IsDragging)
            && (!onlyWhenOnBoard || draggable == null || draggable.IsOnBoard);

        Vector2 p = (p01 * 2f) - Vector2.one;

        float tScale = active ? hoverScale : 1f;
        Vector3 targetScale = rootScale0 * tScale;

        Vector3 targetTiltPos = tiltPos0 + (active ? new Vector3(0f, liftWorld, 0f) : Vector3.zero);

        float targetWeight = (active && idleWobble) ? 1f : 0f;
        wobbleWeight = Mathf.Lerp(wobbleWeight, targetWeight, Time.deltaTime * wobbleRamp);

        float wobbleX = Mathf.Sin(Time.time * wobbleSpeed) * wobbleDegrees * wobbleWeight;
        float wobbleY = Mathf.Cos(Time.time * wobbleSpeed * 0.9f) * wobbleDegrees * 0.7f * wobbleWeight;

        float tiltX = active ? (-p.y * tiltMaxDeg + wobbleX) : 0f;
        float tiltY = active ? ( p.x * tiltMaxDeg + wobbleY) : 0f;

        Quaternion targetRot = Quaternion.Euler(tiltX, tiltY, 0f);

        cardRoot.localScale = Vector3.Lerp(cardRoot.localScale, targetScale, Time.deltaTime * smooth);
        tiltPivot.localPosition = Vector3.Lerp(tiltPivot.localPosition, targetTiltPos, Time.deltaTime * smooth);
        tiltPivot.localRotation = Quaternion.Slerp(tiltPivot.localRotation, targetRot, Time.deltaTime * smooth);

        if (glintTransform != null && glintRenderer != null)
        {
            if (glintPlaying)
            {
                glintTimer += Time.deltaTime;
                float t = Mathf.Clamp01(glintTimer / glintDuration);

                float x = Mathf.Lerp(glintStartX, glintEndX, t);
                glintTransform.localPosition = glintPos0 + new Vector3(x, glintY, 0f);
                glintTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);

                float fade = 1f;
                if (t > 0.75f)
                    fade = Mathf.InverseLerp(1f, 0.75f, t);

                SetGlintAlpha(glintAlpha * fade);

                if (t >= 1f)
                {
                    glintPlaying = false;
                    SetGlintAlpha(0f);
                }
            }
            else
            {
                SetGlintAlpha(Mathf.Lerp(glintRenderer.color.a, 0f, Time.deltaTime * smooth));
            }
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
