using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// DEBUG MODE - Just bypass everything and test card movement
/// NO NETWORK CONNECTION NEEDED - Works completely standalone
/// Compatible with BOTH old and new Input System
/// Attach this to any GameObject and it will automatically let you drag cards without any checks
/// </summary>
public class CardDebugger : MonoBehaviour
{
    [Header("Spawn Test Cards")]
    #if ENABLE_INPUT_SYSTEM
    [SerializeField] private Key spawnKey = Key.Space;
    #else
    [SerializeField] private KeyCode spawnKey = KeyCode.Space;
    #endif
    [SerializeField] private int cardsToSpawn = 5;

    [Header("Auto-Setup")]
    [SerializeField] private bool disableNetworkManagerUI = true;
    [SerializeField] private bool disableGameManager = true;

    private void Start()
    {
        Debug.Log("=== CARD DEBUGGER ===");
        Debug.Log("Press SPACE to spawn test cards");
        Debug.Log("NO NETWORK CONNECTION NEEDED!");
        Debug.Log("All phase/network/resource checks are DISABLED");
        Debug.Log("Just drag and drop cards freely!");
        Debug.Log("Children in scene after spawn: " + FindObjectsByType<CardDraggable>(FindObjectsSortMode.None).Length);
        
        // Disable network UI so you don't accidentally click it
        if (disableNetworkManagerUI)
        {
            DisableNetworkUI();
        }

        // Disable GameManager phase checks
        if (disableGameManager)
        {
            DisableGameManagerChecks();
        }
        
        // Auto-bypass on start
        BypassAllChecks();
    }

    private void Update()
    {
        // Check for spawn key press using appropriate Input System
        bool spawnPressed = false;
        
        #if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current[spawnKey].wasPressedThisFrame)
        {
            spawnPressed = true;
        }
        #else
        if (Input.GetKeyDown(spawnKey))
        {
            spawnPressed = true;
        }
        #endif

        if (spawnPressed)
        {
            SpawnCards();
        }

        // Keep bypass active every frame (in case new cards spawn)
        BypassAllChecks();
    }

    private void SpawnCards()
    {
        if (CardManager.Instance == null)
        {
            Debug.LogError("CardManager not found! Make sure CardManager exists in your scene.");
            return;
        }

        for (int i = 0; i < cardsToSpawn; i++)
        {
            CardManager.Instance.SpawnCard(i, true);
        }

        Debug.Log($"Spawned {cardsToSpawn} test cards - DRAG THEM AROUND!");
    }

    private void BypassAllChecks()
    {
        // Find all card draggables and force bypass mode
        CardDraggable[] cards = FindObjectsByType<CardDraggable>(FindObjectsSortMode.None);
        
        foreach (CardDraggable card in cards)
        {
            // Use reflection to bypass the checks
            var field = card.GetType().GetField("skipNetworkChecks", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                field.SetValue(card, true);
            }
        }
    }

    private void DisableNetworkUI()
    {
        // Find and disable NetworkManagerUI so you can't accidentally click Host/Client/Server
        NetworkManagerUI networkUI = FindFirstObjectByType<NetworkManagerUI>();
        if (networkUI != null)
        {
            networkUI.gameObject.SetActive(false);
            Debug.Log("Disabled NetworkManagerUI - You don't need to connect!");
        }
    }

    private void DisableGameManagerChecks()
    {
        // Disable GameManager if it exists so it doesn't interfere
        GameManager gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
        {
            gameManager.enabled = false;
            Debug.Log("Disabled GameManager - No phase checks!");
        }
    }

    private void OnGUI()
    {
        // Show helpful instructions
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 16;
        style.normal.textColor = Color.green;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;

        // GUI.Box(new Rect(Screen.width / 2 - 200, 20, 400, 60), 
        //     "NO CONNECTION NEEDED\nPress SPACE to spawn cards!", 
        //     style);
    }
}