using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Netcode;

public class PlayerResourcesUI : MonoBehaviour
{
    [Header("Bars (Fill Images only)")]
    [SerializeField] private Image hpFill;
    [SerializeField] private Image opponentHpFill;

    [Header("Pips (in order)")]
    [SerializeField] private List<Image> apPips = new List<Image>();

    [Header("Max values (match your game rules)")]
    [SerializeField] private int maxHP = 10;
    [SerializeField] private int maxAP = 5;

    private PlayerNetwork localPlayer;
    private PlayerNetwork opponentPlayer;

    // cached HP values to detect damage events
    private int lastLocalHp = int.MinValue;
    private int lastOpponentHp = int.MinValue;

    // shake state
    private RectTransform hpFillRect;
    private RectTransform opponentHpFillRect;
    private Vector2 hpFillRestPos;
    private Vector2 opponentHpFillRestPos;
    private bool hpRestPosCached;
    private bool opponentHpRestPosCached;
    private Coroutine hpShakeRoutine;
    private Coroutine opponentHpShakeRoutine;

    private void Start()
    {
        FindLocalPlayer();
        FindOpponentPlayer();

        // ensure bars are configured to drain from right to left
        ConfigureHpFill(hpFill);
        ConfigureHpFill(opponentHpFill);

        // cache rects and rest positions for shake
        CacheHpRects();

        UpdateAll(); 
    }

    private void Update()
    {
        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        if (opponentPlayer == null)
        {
            FindOpponentPlayer();
        }

        if (localPlayer == null)
        {
            return;
        }

        UpdateAll();
    }

    private void ConfigureHpFill(Image fill)
    {
        if (fill == null) return;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        // Left origin so bar drains from left to right as HP changes
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
    }

    private void CacheHpRects()
    {
        if (hpFill != null && hpFillRect == null)
        {
            hpFillRect = hpFill.rectTransform;
            hpFillRestPos = hpFillRect.anchoredPosition;
            hpRestPosCached = true;
        }

        if (opponentHpFill != null && opponentHpFillRect == null)
        {
            opponentHpFillRect = opponentHpFill.rectTransform;
            opponentHpFillRestPos = opponentHpFillRect.anchoredPosition;
            opponentHpRestPosCached = true;
        }
    }

    private void FindLocalPlayer()
    {
        // Find the local client's player object
        if (NetworkManager.Singleton == null) return;
        var localObj = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (localObj == null) return;

        localPlayer = localObj.GetComponent<PlayerNetwork>();
    }

    private void FindOpponentPlayer()
    {
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.IsClient) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId == NetworkManager.Singleton.LocalClientId)
                continue;

            if (client.PlayerObject != null && client.PlayerObject.TryGetComponent<PlayerNetwork>(out var opponent))
            {
                opponentPlayer = opponent;
                break;
            }
        }
    }

    private void UpdateAll()
    {
        if (localPlayer == null) return;

        int hp = localPlayer.GetCurrentHealth();
        int ap = localPlayer.GetCurrentActionPoints();
        int opponentHp = opponentPlayer != null ? opponentPlayer.GetCurrentHealth() : 0;

        // detect damage and shake appropriate bar(s)
        if (lastLocalHp != int.MinValue && hp < lastLocalHp)
        {
            StartHpShake(true);
        }
        if (opponentPlayer != null && lastOpponentHp != int.MinValue && opponentHp < lastOpponentHp)
        {
            StartHpShake(false);
        }

        lastLocalHp = hp;
        lastOpponentHp = opponentHp;

        SetBar(hpFill, hp, maxHP);
        SetBar(opponentHpFill, opponentHp, maxHP);
        SetPips(ap);
    }

    private void StartHpShake(bool isLocal)
    {
        CacheHpRects();

        if (isLocal)
        {
            if (hpFillRect == null || !hpRestPosCached) return;

            if (hpShakeRoutine != null)
            {
                StopCoroutine(hpShakeRoutine);
                hpShakeRoutine = null;
            }

            hpShakeRoutine = StartCoroutine(ShakeRectTransform(
                hpFillRect,
                hpFillRestPos,
                0.15f,
                8f
            ));
        }
        else
        {
            if (opponentHpFillRect == null || !opponentHpRestPosCached) return;

            if (opponentHpShakeRoutine != null)
            {
                StopCoroutine(opponentHpShakeRoutine);
                opponentHpShakeRoutine = null;
            }

            opponentHpShakeRoutine = StartCoroutine(ShakeRectTransform(
                opponentHpFillRect,
                opponentHpFillRestPos,
                0.15f,
                8f
            ));
        }
    }

    private System.Collections.IEnumerator ShakeRectTransform(RectTransform rect, Vector2 restPos, float duration, float magnitude)
    {
        if (rect == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float damper = 1f - t; // fade out over time

            float offsetX = (Random.value * 2f - 1f) * magnitude * damper;
            float offsetY = (Random.value * 2f - 1f) * magnitude * damper;

            rect.anchoredPosition = restPos + new Vector2(offsetX, offsetY);
            yield return null;
        }

        rect.anchoredPosition = restPos;
    }

    private void SetBar(Image fill, int current, int max)
    {
        if (fill == null) return;
        if (max <= 0) { fill.fillAmount = 0f; return; }

        float pct = Mathf.Clamp01((float)current / max);
        fill.fillAmount = pct;
    }

    private void SetPips(int currentAP)
    {
        currentAP = Mathf.Clamp(currentAP, 0, maxAP);

        for (int i = 0; i < apPips.Count; i++)
        {
            if (apPips[i] == null) continue;
            apPips[i].enabled = i < currentAP; 
        }
    }
}