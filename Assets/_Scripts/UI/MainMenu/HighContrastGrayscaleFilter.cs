using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Simple toggle for a high‑contrast grayscale post‑processing effect.
/// 
/// Usage:
/// - Create a Global Volume in your main menu scene.
/// - Add a ColorAdjustments override with Saturation = -100 and adjust Contrast to taste.
/// - Drag that Volume into the volume field on this component.
/// - Hook your UI Toggle's OnValueChanged(bool) to SetEnabledFromToggle.
/// </summary>
public class HighContrastGrayscaleFilter : MonoBehaviour
{
    [Header("Post‑processing volume with grayscale settings")]
    [SerializeField] private Volume grayscaleVolume;

    private void Awake()
    {
        // Make sure we start in a known state (off by default)
        if (grayscaleVolume != null)
        {
            grayscaleVolume.enabled = false;
            grayscaleVolume.weight = 0f;
        }
    }

    /// <summary>
    /// Enable or disable the grayscale effect from code.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        if (grayscaleVolume == null) return;

        grayscaleVolume.enabled = enabled;
        grayscaleVolume.weight = enabled ? 1f : 0f;
    }

    /// <summary>
    /// Helper for UI Toggle (bool) callbacks.
    /// </summary>
    public void SetEnabledFromToggle(bool isOn)
    {
        // Some UI setups never send 'false' correctly; instead of trusting isOn,
        // just flip the current state of the volume.
        bool newEnabled = grayscaleVolume == null ? isOn : !grayscaleVolume.enabled;
        SetEnabled(newEnabled);
    }
}

