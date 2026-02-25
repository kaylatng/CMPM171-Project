using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PlayerResourcesUI : MonoBehaviour
{
    [Header("Bars (Fill Images only)")]
    [SerializeField] private Image hpFill;
    [SerializeField] private Image manaFill;

    [Header("Pips (in order)")]
    [SerializeField] private List<Image> apPips = new List<Image>();

    [Header("Max values (match your game rules)")]
    [SerializeField] private int maxHP = 20;
    [SerializeField] private int maxMana = 10;
    [SerializeField] private int maxAP = 5;

    private PlayerNetwork localPlayer;

    private void Start()
    {
        FindLocalPlayer();
        UpdateAll(); 
    }

    private void Update()
    {
        
        if (localPlayer == null)
        {
            FindLocalPlayer();
            return;
        }

        UpdateAll();
    }

    private void FindLocalPlayer()
    {
        // Find the local client's player object
        if (Unity.Netcode.NetworkManager.Singleton == null) return;
        var localObj = Unity.Netcode.NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (localObj == null) return;

        localPlayer = localObj.GetComponent<PlayerNetwork>();
    }

    private void UpdateAll()
    {
        if (localPlayer == null) return;

        int hp = localPlayer.GetCurrentHealth();
        int mana = localPlayer.GetCurrentMana();
        int ap = localPlayer.GetCurrentActionPoints();

        SetBar(hpFill, hp, maxHP);
        SetBar(manaFill, mana, maxMana);
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