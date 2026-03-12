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

    private void Start()
    {
        FindLocalPlayer();
        FindOpponentPlayer();
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

        SetBar(hpFill, hp, maxHP);
        SetBar(opponentHpFill, opponentHp, maxHP);
        SetPips(ap);
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