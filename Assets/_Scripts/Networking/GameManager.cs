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

	public override void OnNetworkSpawn() {
		if (IsServer) {
		CurrentPhase.OnValueChanged += OnPhaseChanged;

		if (CurrentPhase.Value == GamePhase.ResourceGain) {
			StartCoroutine(ProcessResourceGain());
		}
		}
	}

	private void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase) {
		if (!IsServer) return;
		if (newPhase == GamePhase.ResourceGain) {
		StartCoroutine(ProcessResourceGain());
		}
	}

	private System.Collections.IEnumerator ProcessResourceGain() {
		Debug.Log("GAME MANAGER || Resource Gain Phase - Waiting for players");
		while (NetworkManager.Singleton.ConnectedClientsList.Count < 2) {
			yield return new WaitForSeconds(0.5f);
		}

		Debug.Log("GAME MANAGER || Resource Gain Phase - Both players connected");
		yield return new WaitForSeconds(0.5f);

		// blind draw 1 card (automatic, 1 AP NOT used)
		// and 1 mana charge given to player
		foreach (var client in NetworkManager.Singleton.ConnectedClientsList) {
		if (client.PlayerObject.TryGetComponent<PlayerNetwork>(out var player)) {
			player.StartNewTurnServer(); // reset ap, add mana
			player.ExecuteDrawServer(isFree: true);
		}
		}

		yield return new WaitForSeconds(1.0f);
		CurrentPhase.Value = GamePhase.Planning; // move to planning
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

