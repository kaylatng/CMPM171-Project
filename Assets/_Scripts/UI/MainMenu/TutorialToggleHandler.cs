using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach this to the tutorial checkbox Toggle in the MainMenu scene.
/// It records whether the player wants to see the in-game tutorial.
/// </summary>
public class TutorialToggleHandler : MonoBehaviour
{
    public static bool TutorialSelected { get; private set; }

    [SerializeField] private Toggle tutorialToggle;

    private void Awake()
    {
        if (tutorialToggle == null)
        {
            tutorialToggle = GetComponent<Toggle>();
        }

        if (tutorialToggle != null)
        {
            // Initialize state and subscribe to changes.
            OnToggleValueChanged(tutorialToggle.isOn);
            tutorialToggle.onValueChanged.AddListener(OnToggleValueChanged);
        }
    }

    private void OnDestroy()
    {
        if (tutorialToggle != null)
        {
            tutorialToggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        }
    }

    private void OnToggleValueChanged(bool isOn)
    {
        TutorialSelected = isOn;
    }
}

