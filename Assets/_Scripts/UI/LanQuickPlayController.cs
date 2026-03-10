using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Simple LAN "Quick Play" helper:
/// - First tries to discover an existing host on the same LAN via UDP broadcast.
/// - If a host is found within a short timeout, connects as a client to that host.
/// - If no host is found, starts a host locally and begins responding to discovery requests.
/// 
/// Attach this to a GameObject in the scene that also has access to NetworkManagerUI,
/// and wire your Quick Play button to call OnQuickPlay().
/// </summary>
public class LanQuickPlayController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkManagerUI networkManagerUI;

    [Header("LAN Discovery Settings")]
    [Tooltip("UDP port used for LAN discovery broadcasts. Does not need to match the game port.")]
    [SerializeField] private int discoveryPort = 47777;

    [Tooltip("How many seconds to wait for a host response before becoming the host.")]
    [SerializeField] private float discoveryTimeoutSeconds = 3f;

    [Tooltip("Discovery message identifier to avoid cross-talk with other apps on the network.")]
    [SerializeField] private string discoveryMessage = "CARDGAME_DISCOVERY_V1";

    private bool isAttemptInProgress;
    private UdpClient responderClient;

    /// <summary>
    /// Entry point for the Quick Play button.
    /// </summary>
    public void OnQuickPlay()
    {
        if (isAttemptInProgress)
        {
            Debug.Log("LAN QuickPlay already in progress.");
            return;
        }

        if (NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient))
        {
            Debug.LogWarning("LAN QuickPlay requested but NetworkManager is already running.");
            return;
        }

        StartCoroutine(QuickPlayRoutine());
    }

    private IEnumerator QuickPlayRoutine()
    {
        isAttemptInProgress = true;

        // 1. Try to discover an existing host on the LAN.
        string discoveredIp = null;
        yield return StartCoroutine(TryDiscoverHost(ip => discoveredIp = ip));

        if (!string.IsNullOrEmpty(discoveredIp))
        {
            Debug.Log($"LAN QuickPlay: Discovered host at {discoveredIp}, connecting as client.");
            if (networkManagerUI != null)
            {
                networkManagerUI.ConnectToIp(discoveredIp);
            }
            else
            {
                Debug.LogError("LanQuickPlayController: NetworkManagerUI reference is missing.");
            }
        }
        else
        {
            // 2. No host found, become the host and start responding to future discovery requests.
            Debug.Log("LAN QuickPlay: No host found, starting as host.");
            if (networkManagerUI != null)
            {
                networkManagerUI.OnHostClicked();
            }
            else
            {
                Debug.LogError("LanQuickPlayController: NetworkManagerUI reference is missing.");
            }

            // Start listening for discovery requests so other players can find us.
            StartResponder();
        }

        isAttemptInProgress = false;
    }

    private IEnumerator TryDiscoverHost(Action<string> onResult)
    {
        UdpClient client = null;
        IPEndPoint anyEndpoint = new IPEndPoint(IPAddress.Any, 0);
        string foundIp = null;

        // Set up socket and send the broadcast without any yields inside try/catch.
        try
        {
            client = new UdpClient();
            client.EnableBroadcast = true;
            client.Client.ReceiveTimeout = (int)(discoveryTimeoutSeconds * 1000f);

            byte[] requestBytes = Encoding.UTF8.GetBytes(discoveryMessage);
            IPEndPoint broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, discoveryPort);

            // Send discovery broadcast.
            client.Send(requestBytes, requestBytes.Length, broadcastEndpoint);
        }
        catch (SocketException ex)
        {
            Debug.LogWarning($"LAN QuickPlay discovery socket exception: {ex.Message}");
            if (client != null)
            {
                client.Close();
                client = null;
            }
            onResult?.Invoke(null);
            yield break;
        }

        // Now poll for responses with yields, outside of the try/catch.
        if (client != null)
        {
            float elapsed = 0f;
            while (elapsed < discoveryTimeoutSeconds)
            {
                if (client.Available > 0)
                {
                    byte[] responseBytes = client.Receive(ref anyEndpoint);
                    string response = Encoding.UTF8.GetString(responseBytes);

                    if (response == discoveryMessage)
                    {
                        foundIp = anyEndpoint.Address.ToString();
                        break;
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            client.Close();
        }

        onResult?.Invoke(foundIp);
    }

    private void StartResponder()
    {
        if (responderClient != null)
        {
            return; // already running
        }

        try
        {
            responderClient = new UdpClient(discoveryPort);
            responderClient.EnableBroadcast = true;
            responderClient.Client.Blocking = false;
            StartCoroutine(ResponderLoop());
        }
        catch (SocketException ex)
        {
            Debug.LogWarning($"LAN QuickPlay responder could not bind to port {discoveryPort}: {ex.Message}");
            if (responderClient != null)
            {
                responderClient.Close();
                responderClient = null;
            }
        }
    }

    private IEnumerator ResponderLoop()
    {
        IPEndPoint anyEndpoint = new IPEndPoint(IPAddress.Any, 0);
        byte[] responseBytes = Encoding.UTF8.GetBytes(discoveryMessage);

        while (responderClient != null &&
               NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsListening)
        {
            try
            {
                while (responderClient.Available > 0)
                {
                    byte[] requestBytes = responderClient.Receive(ref anyEndpoint);
                    string request = Encoding.UTF8.GetString(requestBytes);

                    if (request == discoveryMessage)
                    {
                        responderClient.Send(responseBytes, responseBytes.Length, anyEndpoint.Address.ToString(), discoveryPort);
                    }
                }
            }
            catch (SocketException ex)
            {
                // Non-blocking recv may throw when no data; ignore typical "WouldBlock" / timeout errors.
                Debug.LogWarning($"LAN QuickPlay responder socket exception: {ex.Message}");
            }

            yield return null;
        }

        if (responderClient != null)
        {
            responderClient.Close();
            responderClient = null;
        }
    }

    private void OnDestroy()
    {
        if (responderClient != null)
        {
            responderClient.Close();
            responderClient = null;
        }
    }
}

