using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuRoot;   // container with Play / Options / Quit
    [SerializeField] private GameObject onlinePlayPanel; // your connection panel

    private void Awake()
    {
        // Start with main menu visible, panel hidden
        if (mainMenuRoot != null) mainMenuRoot.SetActive(true);
        if (onlinePlayPanel != null) onlinePlayPanel.SetActive(false);
    }

    public void OnPlayButton()
    {
        // Show the connection panel
        mainMenuRoot.SetActive(false);
        onlinePlayPanel.SetActive(true);
    }

    public void OnBackFromOnlinePlay()
    {
        // Go back to main menu
        onlinePlayPanel.SetActive(false);
        mainMenuRoot.SetActive(true);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}