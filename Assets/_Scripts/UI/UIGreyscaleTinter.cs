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
    [SerializeField] private Color greyscaleColor = Color.gray;

    private Color[] originalColors;
    private bool isGreyscale;

    private void Awake()
    {
        if (uiGraphics == null) return;

        originalColors = new Color[uiGraphics.Length];
        for (int i = 0; i < uiGraphics.Length; i++)
        {
            if (uiGraphics[i] != null)
            {
                originalColors[i] = uiGraphics[i].color;
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

        if (uiGraphics == null || originalColors == null) return;

        for (int i = 0; i < uiGraphics.Length; i++)
        {
            if (uiGraphics[i] == null) continue;
            uiGraphics[i].color = enabled ? greyscaleColor : originalColors[i];
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

