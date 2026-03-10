using UnityEngine;

public class LocalizationBootstrapper : MonoBehaviour
{
    [Tooltip("Translation table asset used for the whole game.")]
    public TranslationTable table;

    private void Awake()
    {
        if (table != null)
            LocalizationManager.SetTable(table, notify: false);

        // Ensure current language is applied to all listeners in this scene.
        LocalizationManager.SetLanguage(LocalizationManager.CurrentLanguageIndex, save: false, notify: true);
    }
}

