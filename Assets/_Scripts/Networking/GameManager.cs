using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour {
  public static GameManager Instance;

  public enum GamePhase { ResourceGain, Planning, Reveal, Cleanup }
  public NetworkVariable<GamePhase> CurrentPhase = new NetworkVariable<GamePhase>(GamePhase.ResourceGain);

  private void Awake() {
    if (Instance == null) Instance = this;
    else Destroy(gameObject);
  }

 [ServerRpc]
  public void CheckPlayersReadyServerRpc() {
    if (!IsServer) return;

    int readyCount = 0;
    
    foreach (var client in NetworkManager.Singleton.ConnectedClientsList) {
      if (client.PlayerObject.TryGetComponent<PlayerNetwork>(out var player)) {
        if (player.IsPlayerReady()) {
          readyCount++;
        }
      }
    }

    if (readyCount >= 2 && CurrentPhase.Value == GamePhase.Planning) {
      CurrentPhase.Value = GamePhase.Reveal;
      StartRevealPhase();
    }
  }

  private void StartRevealPhase() {
    Debug.Log("Both players ready. Enter Reveal Phase.");
  }
}

