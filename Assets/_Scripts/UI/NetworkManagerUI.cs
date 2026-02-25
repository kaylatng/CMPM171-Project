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
	}

	private void OnServerClicked()
	{
		HideHostIp();
		NetworkManager.Singleton.StartServer();
	}

	private void OnHostClicked()
	{
		NetworkManager.Singleton.StartHost();
		ShowAndFetchHostIp();
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

		NetworkManager.Singleton.StartClient();
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
}
