using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class HighContrastGrayscaleImageEffect : MonoBehaviour
{
    [SerializeField] private Material grayscaleMaterial;
    [SerializeField] private bool effectEnabled = false;

    public void SetEnabled(bool enabled)
    {
        effectEnabled = enabled;
    }

    public void SetEnabledFromToggle(bool isOn)
    {
        SetEnabled(isOn);
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        bool hasMat = grayscaleMaterial != null;

        if (!effectEnabled || !hasMat)
        {
            Graphics.Blit(src, dest);
            return;
        }

        Graphics.Blit(src, dest, grayscaleMaterial);
    }
}

