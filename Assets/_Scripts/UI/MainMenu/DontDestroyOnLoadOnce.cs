using UnityEngine;

/// <summary>
/// Keeps a single instance of this GameObject alive across scene loads.
/// Attach this to your AccessibilityGrayscale object (the one with the
/// Global Volume + HighContrastGrayscaleFilter).
/// </summary>
public class DontDestroyOnLoadOnce : MonoBehaviour
{
    private static DontDestroyOnLoadOnce instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}

