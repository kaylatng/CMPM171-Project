// Direct IP: assign ipAddressInputField (and optionally portInputField) in the Inspector
// so the Client can connect to a host by IP. If left unassigned, Client uses 127.0.0.1:7778.
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class NetworkManagerUI : MonoBehaviour
{
	[SerializeField] private Button serverBtn;
	[SerializeField] private Button hostBtn;
	[SerializeField] private Button clientBtn;

	[Header("Host - Your IP (shown only when hosting)")]
	[Tooltip("Assign a TextMeshProUGUI to show the host's public IP after clicking Host.")]
	[SerializeField] private TextMeshProUGUI hostPublicIpText;

	[Header("Connection Status (optional)")]
	[Tooltip("Optional status label to show host/client connection state (e.g. 'Waiting for opponent...').")]
	[SerializeField] private TextMeshProUGUI statusText;

	[Header("Direct IP (optional - for Client)")]
	[Tooltip("Leave empty to use 127.0.0.1 (same machine). Set to host's IP for LAN/internet.")]
	[SerializeField] private InputField ipAddressInputField;
	[Tooltip("Leave 0 or empty to use default port 7778.")]
	[SerializeField] private InputField portInputField;

	private const ushort DefaultPort = 7778;

	private void Awake()
	{
		serverBtn.onClick.AddListener(OnServerClicked);
		hostBtn.onClick.AddListener(OnHostClicked);
		clientBtn.onClick.AddListener(OnClientClicked);
		if (hostPublicIpText != null)
			hostPublicIpText.gameObject.SetActive(false);

		if (statusText != null)
			statusText.text = "";

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

	private void OnServerClicked()
	{
		HideHostIp();
		ConfigureServerTransport();
		NetworkManager.Singleton.StartServer();
		SetStatus("Server running. Waiting for clients...");
	}

	private void OnHostClicked()
	{
		ConfigureServerTransport();
		bool success = NetworkManager.Singleton.StartHost();
		if (!success)
		{
			SetStatus("Failed to start host.");
			return;
		}

		ShowAndFetchHostIp();
		SetStatus("Hosting game. Waiting for opponent to join...");
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

	private void OnClientClicked()
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
		// so the "waiting for opponent" text is not left stale.
		if (NetworkManager.Singleton.IsHost)
		{
			if (clientId == NetworkManager.Singleton.LocalClientId)
			{
				SetStatus("Hosting game (local client connected). Waiting for opponent...");
			}
			else
			{
				SetStatus("Opponent connected. Starting game!");
			}
		}
		// Pure client (non-host): we successfully connected to the host.
		else if (!NetworkManager.Singleton.IsServer && clientId == NetworkManager.Singleton.LocalClientId)
		{
			SetStatus("Connected to host.");
		}
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
