using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple UI-only "greyscale" effect for Screen Space - Overlay canvases.
/// It tints specified UI graphics to a grey color when enabled.
/// </summary>
public class UIGreyscaleTinter : MonoBehaviour
{
    [SerializeField] private Graphic[] uiGraphics;
    [SerializeField] private Shadow[] shadowEffects;   // Outline and Shadow components
    [SerializeField] private Color greyscaleColor = Color.gray;

    private Color[] originalGraphicColors;
    private bool[] originalShadowEnabled;
    private bool isGreyscale;

    private void Awake()
    {
        if (uiGraphics != null)
        {
            originalGraphicColors = new Color[uiGraphics.Length];
            for (int i = 0; i < uiGraphics.Length; i++)
            {
                if (uiGraphics[i] != null)
                {
                    originalGraphicColors[i] = uiGraphics[i].color;
                }
            }
        }

        if (shadowEffects != null)
        {
            originalShadowEnabled = new bool[shadowEffects.Length];
            for (int i = 0; i < shadowEffects.Length; i++)
            {
                if (shadowEffects[i] != null)
                {
                    originalShadowEnabled[i] = shadowEffects[i].enabled;
                }
            }
        }

        // If the global grayscale is already enabled (from main menu),
        // start this UI in greyscale too so Scene and Game views match.
        if (HighContrastGrayscaleFilter.IsEnabled)
        {
            SetGreyscale(true);
        }
    }

    public void SetGreyscale(bool enabled)
    {
        isGreyscale = enabled;

        if (uiGraphics != null && originalGraphicColors != null)
        {
            for (int i = 0; i < uiGraphics.Length; i++)
            {
                if (uiGraphics[i] == null) continue;
                uiGraphics[i].color = enabled ? greyscaleColor : originalGraphicColors[i];
            }
        }

        if (shadowEffects != null && originalShadowEnabled != null)
        {
            for (int i = 0; i < shadowEffects.Length; i++)
            {
                if (shadowEffects[i] == null) continue;
                // When greyscale is on, hide shadows/outlines for a flatter look.
                // When off, restore their original enabled state.
                shadowEffects[i].enabled = enabled ? false : originalShadowEnabled[i];
            }
        }
    }

    /// <summary>
    /// Helper for a Toggle (bool) callback. You can hook your accessibility
    /// toggle directly to this.
    /// </summary>
    public void OnToggleChanged(bool _)
    {
        SetGreyscale(!isGreyscale);
    }
}

