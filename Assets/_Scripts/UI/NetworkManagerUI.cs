// Direct IP: assign ipAddressInputField (and optionally portInputField) in the Inspector
// so the Client can connect to a host by IP. If left unassigned, Client uses 127.0.0.1:7778.
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class NetworkManagerUI : MonoBehaviour
{
	[SerializeField] private Button serverBtn;
	[SerializeField] private Button hostBtn;
	[SerializeField] private Button clientBtn;

	[Header("Scenes")]
	[SerializeField] private string gameSceneName = "MainGame";

	[Header("Host - Your IP (shown only when hosting)")]
	[Tooltip("Assign a TextMeshProUGUI to show the host's public IP after clicking Host.")]
	[SerializeField] private TextMeshProUGUI hostPublicIpText;

	[Header("Connection Status (optional)")]
	[Tooltip("Optional status label to show host/client connection state (e.g. 'Waiting for opponent...').")]
	[SerializeField] private TextMeshProUGUI statusText;

	[Tooltip("Optional LocalizedTMPText attached to the status label, so we can switch keys at runtime.")]
	[SerializeField] private LocalizedTMPText statusLocalized;

	[Tooltip("Spinner object (e.g. rotating '|') shown while waiting for opponent.")]
	[SerializeField] private GameObject waitingSpinner;

	[Header("Direct IP (optional - for Client)")]
	[Tooltip("Leave empty to use 127.0.0.1 (same machine). Set to host's IP for LAN/internet.")]
	[SerializeField] private TMP_InputField ipAddressInputField;
	[Tooltip("Leave 0 or empty to use default port 7778.")]
	[SerializeField] private TMP_InputField portInputField;

	private const ushort DefaultPort = 7778;

	private const string QuickplayDescriptionKey = "ui/menu/quickplayDescription";
	private const string QuickplayWaitingKey = "ui/menu/quickplayWaiting";

	// Tracks whether the host has already triggered the game start scene load.
	private bool hasStartedGame = false;

	private void Awake()
	{
		if (serverBtn != null)
			serverBtn.onClick.AddListener(OnServerClicked);

		if (hostBtn != null)
			hostBtn.onClick.AddListener(OnHostClicked);

		if (clientBtn != null)
			clientBtn.onClick.AddListener(OnClientClicked);
		if (hostPublicIpText != null)
			hostPublicIpText.gameObject.SetActive(false);

		if (statusText != null)
			statusText.text = "Find an opponent instantly.";

		if (NetworkManager.Singleton != null)
		{
			NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
			NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
		}
	}

	private void OnDestroy()
	{
		if (NetworkManager.Singleton != null)
		{
			NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
			NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
		}
	}

	public void OnServerClicked()
	{
		HideHostIp();
		ConfigureServerTransport();
		NetworkManager.Singleton.StartServer();
		SetStatus("Server running. Waiting for clients...");
	}

	public void OnHostClicked()
	{
		ConfigureServerTransport();
		bool success = NetworkManager.Singleton.StartHost();
		if (!success)
		{
			SetStatus("Failed to start host.");
			return;
		}

		hasStartedGame = false;

		ShowAndFetchHostIp();
		SetWaitingStatus();
	}

	private void HideHostIp()
	{
		if (hostPublicIpText != null)
			hostPublicIpText.gameObject.SetActive(false);
	}

	private void ShowAndFetchHostIp()
	{
		if (hostPublicIpText == null) return;
		hostPublicIpText.gameObject.SetActive(true);
		hostPublicIpText.text = "Getting IP...";
		StartCoroutine(FetchAndShowPublicIP());
	}

	private IEnumerator FetchAndShowPublicIP()
	{
		if (hostPublicIpText == null) yield break;
		using (var req = UnityWebRequest.Get("https://api.ipify.org"))
		{
			yield return req.SendWebRequest();
			if (hostPublicIpText == null) yield break;
			if (req.result == UnityWebRequest.Result.Success)
				hostPublicIpText.text = "Your IP: " + req.downloadHandler.text.Trim();
			else
				hostPublicIpText.text = "Could not get IP";
		}
	}

	public void OnClientClicked()
	{
		string ip = GetClientAddress();
		ushort port = GetClientPort();

		var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
		if (transport != null)
			transport.SetConnectionData(ip, port);

		SetStatus($"Connecting to {ip}:{port}...");

		bool success = NetworkManager.Singleton.StartClient();
		if (!success)
		{
			SetStatus("Failed to start client.");
		}
		else
		{
			// client started; connection result will be reflected via callbacks
		}
	}

	/// <summary>
	/// Connect as a client to a specific IPv4 address (used by LAN Quick Play).
	/// </summary>
	/// <param name="ip">Host IPv4 address (e.g. 192.168.1.10).</param>
	public void ConnectToIp(string ip)
	{
		if (ipAddressInputField != null)
		{
			ipAddressInputField.text = ip;
		}

		OnClientClicked();
	}

	private string GetClientAddress()
	{
		if (ipAddressInputField == null || string.IsNullOrWhiteSpace(ipAddressInputField.text))
			return "127.0.0.1";
		return ipAddressInputField.text.Trim();
	}

	private ushort GetClientPort()
	{
		if (portInputField == null || string.IsNullOrWhiteSpace(portInputField.text))
			return DefaultPort;
		return ushort.TryParse(portInputField.text.Trim(), out ushort p) ? p : DefaultPort;
	}

	/// <summary>
	/// Configure the Unity Transport for server/host to listen on all interfaces using the chosen port.
	/// </summary>
	private void ConfigureServerTransport()
	{
		var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
		if (transport == null) return;

		// Use the same port field as the client, or default if none provided.
		ushort port = GetClientPort();
		// Listen on all interfaces (0.0.0.0) so external clients can reach us when port-forwarded.
		transport.SetConnectionData("0.0.0.0", port);
	}

	private void HandleClientConnected(ulong clientId)
	{
		if (statusText == null || NetworkManager.Singleton == null) return;

		// Host: update when any client connects (including our own local client on host),
		if (NetworkManager.Singleton.IsHost)
		{
			if (clientId == NetworkManager.Singleton.LocalClientId)
			{
				// Local host connected; still waiting for an opponent.
				SetWaitingStatus();
			}
			else
			{
				// A remote client connected - status will be updated when Update() detects 2 clients.
				SetStatus("Opponent connected. Starting game!");
			}
		}
		// Pure client (non-host): we successfully connected to the host.
		else if (!NetworkManager.Singleton.IsServer && clientId == NetworkManager.Singleton.LocalClientId)
		{
			SetStatus("Connected to host.");
		}
	}

	/// <summary>
	/// Cancel any active host/client session and clear status/IP UI.
	/// Hook this to a Cancel/Back button alongside your own menu navigation.
	/// </summary>
	public void CancelNetworking()
	{
		if (NetworkManager.Singleton != null &&
		    (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient))
		{
			NetworkManager.Singleton.Shutdown();
		}

		hasStartedGame = false;

		SetStatus("Find an opponent instantly.");
		HideHostIp();

		// Restore the default quickplay description key so the panel text is correct next time it opens.
		ApplyStatusKey(QuickplayDescriptionKey);

		if (waitingSpinner != null)
			waitingSpinner.SetActive(false);
	}

	private void SetWaitingStatus()
	{
		if (waitingSpinner != null)
			waitingSpinner.SetActive(true);

		// Prefer localized key-based status if possible.
		if (!ApplyStatusKey(QuickplayWaitingKey))
		{
			// Fallback English string if localization isn't wired.
			SetStatus("Hosting game. Waiting for opponent.");
		}
	}

	private void Update()
	{
		if (NetworkManager.Singleton == null) return;

		// Only the host decides when to start the game.
		if (!NetworkManager.Singleton.IsHost) return;

		if (hasStartedGame) return;

		int connectedCount = NetworkManager.Singleton.ConnectedClientsList.Count;
		// host (server) + 1 client = 2 connected clients
		if (connectedCount >= 2)
		{
			hasStartedGame = true;
			SetStatus("Opponent connected. Starting game!");

			if (waitingSpinner != null)
				waitingSpinner.SetActive(false);

			if (!string.IsNullOrWhiteSpace(gameSceneName) &&
			    NetworkManager.Singleton.SceneManager != null)
			{
				NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
			}
			else
			{
				Debug.LogWarning("NETWORK UI || Cannot load game scene: SceneManager is null or gameSceneName is empty.");
			}
		}
	}

	/// <summary>
	/// Try to switch the LocalizedTMPText key on the status label and immediately apply the translated value.
	/// Returns true if a localized value was applied.
	/// </summary>
	private bool ApplyStatusKey(string key)
	{
		if (statusLocalized == null || statusText == null)
			return false;

		statusLocalized.key = key;

		if (LocalizationManager.TryGet(key, LocalizationManager.CurrentLanguageIndex, out var value))
		{
			statusText.text = value;
			return true;
		}

		return false;
	}

	private void HandleClientDisconnected(ulong clientId)
	{
		if (statusText == null || NetworkManager.Singleton == null) return;

		// Client lost connection to host.
		if (!NetworkManager.Singleton.IsServer && clientId == NetworkManager.Singleton.LocalClientId)
		{
			SetStatus("Disconnected from host.");
		}
		// Host lost the connected opponent.
		else if (NetworkManager.Singleton.IsHost && clientId != NetworkManager.Singleton.LocalClientId)
		{
			SetStatus("Opponent disconnected.");
		}
	}

	private void SetStatus(string message)
	{
		if (statusText == null) return;
		statusText.text = message;
	}
}
