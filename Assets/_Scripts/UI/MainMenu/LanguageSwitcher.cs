using UnityEngine;
using TMPro;

public class LanguageSwitcher : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI buttonText; // Drag 'Text (TMP)' here
    
    [Header("Settings")]
    public string[] languages = { "English", "Español" };
    private int currentIndex = 0;

    void Start()
    {
        // Load saved language or default to 0 (English)
        currentIndex = PlayerPrefs.GetInt("SelectedLanguage", 0);
        LocalizationManager.SetLanguage(currentIndex);
        UpdateVisuals();
    }

    // This is the function you link to the Button's OnClick
    public void CycleLanguage()
    {
        currentIndex = (currentIndex + 1) % languages.Length;

        LocalizationManager.SetLanguage(currentIndex);
        UpdateVisuals();
        
        Debug.Log("Language changed to: " + languages[currentIndex]);
    }

    void UpdateVisuals()
    {
        if (buttonText != null)
        {
            buttonText.text = languages[currentIndex];
        }
    }
}