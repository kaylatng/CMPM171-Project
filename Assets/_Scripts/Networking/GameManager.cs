using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
	public static GameManager Instance;

	public enum GamePhase { ResourceGain, Planning, Reveal, Cleanup }
	public NetworkVariable<GamePhase> CurrentPhase = new NetworkVariable<GamePhase>(GamePhase.ResourceGain);
	public NetworkVariable<int> CurrentRound = new NetworkVariable<int>(0);

	[Header("Phase Timings")]
	[SerializeField] private float revealPhaseDuration = 3f;
	[SerializeField] private float cleanupPhaseDuration = 2f;

	private void Awake()
	{
		if (Instance == null) Instance = this;
		else Destroy(gameObject);
	}

	public override void OnNetworkSpawn()
	{
		if (IsServer)
		{
			CurrentPhase.OnValueChanged += OnPhaseChanged;

			if (CurrentPhase.Value == GamePhase.ResourceGain)
			{
				StartCoroutine(ProcessResourceGain());
			}
		}

		// clients also listen to phase changes for UI updates
		if (IsClient && !IsServer)
		{
			CurrentPhase.OnValueChanged += OnPhaseChangedClient;
		}
	}

	private void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase)
	{
		if (!IsServer) return;

		Debug.Log($"GAME MANAGER || Phase changed: {oldPhase} -> {newPhase}");

		// automatically process each phase on the server
		switch (newPhase)
		{
			case GamePhase.ResourceGain:
				StartCoroutine(ProcessResourceGain());
				break;
			case GamePhase.Planning:
				// planning phase is player-driven, just wait for both ready
				break;
			case GamePhase.Reveal:
				StartCoroutine(ProcessReveal());
				break;
			case GamePhase.Cleanup:
				StartCoroutine(ProcessCleanup());
				break;
		}
	}

	private void OnPhaseChangedClient(GamePhase oldPhase, GamePhase newPhase)
	{
		Debug.Log($"GAME MANAGER || Client sees phase change: {oldPhase} -> {newPhase}");
	}

	private System.Collections.IEnumerator ProcessResourceGain()
	{
		Debug.Log("GAME MANAGER || === RESOURCE GAIN PHASE ===");
		
		// wait for both players to connect
		while (NetworkManager.Singleton.ConnectedClientsList.Count < 2)
		{
			yield return new WaitForSeconds(0.5f);
		}

		Debug.Log("GAME MANAGER || Both players connected");
		CurrentRound.Value++;
		yield return new WaitForSeconds(0.5f);

		// give each player resources for the new turn
		foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
		{
			if (client.PlayerObject.TryGetComponent<PlayerNetwork>(out var player))
			{
				player.StartNewTurnServer(); // reset AP to 5, add +1 mana, set IsReady = false
				player.ExecuteDrawServer(isFree: true); // blind draw 1 card (doesn't cost AP)
			}
		}

		Debug.Log($"GAME MANAGER || Round {CurrentRound.Value} - Resources distributed");
		yield return new WaitForSeconds(1.0f);

		// move to planning phase
		CurrentPhase.Value = GamePhase.Planning;
		NotifyPlanningPhaseStartClientRpc();
	}

	[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
	public void CheckPlayersReadyServerRpc()
	{
		if (!IsServer) return;

		// only check ready status during planning phase
		if (CurrentPhase.Value != GamePhase.Planning) return;

		int readyCount = 0;
		
		foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
		{
			if (client.PlayerObject.TryGetComponent<PlayerNetwork>(out var player))
			{
				if (player.IsPlayerReady()) {
					readyCount++;
				}
			}
		}

		Debug.Log($"GAME MANAGER || Ready check: {readyCount}/2 players ready");

		// both players ready - move to reveal phase
		if (readyCount >= 2)
		{
			CurrentPhase.Value = GamePhase.Reveal;
		}
	}

	private System.Collections.IEnumerator ProcessReveal()
	{
		Debug.Log("GAME MANAGER || === REVEAL PHASE ===");
		
		// reveal all cards on both boards
		RevealBoardsClientRpc();

		// TODO: Process attacks here in the future
		// For now, just show the boards for a few seconds

		yield return new WaitForSeconds(revealPhaseDuration);

		// move to cleanup phase
		CurrentPhase.Value = GamePhase.Cleanup;
	}

	private System.Collections.IEnumerator ProcessCleanup()
	{
		Debug.Log("GAME MANAGER || === CLEANUP PHASE ===");

		// check for exhausted cards (cards with 0 charges)
		CleanupExhaustedCardsClientRpc();

		// check for win condition
		bool gameEnded = CheckWinCondition();
		
		if (gameEnded)
		{
			Debug.Log("GAME MANAGER || Game ended!");
			yield break;
		}

		yield return new WaitForSeconds(cleanupPhaseDuration);

		// move to next round's resource gain phase
		CurrentPhase.Value = GamePhase.ResourceGain;
	}

	private bool CheckWinCondition()
	{
		// check if any player has HP <= 0
		foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
		{
			if (client.PlayerObject.TryGetComponent<PlayerNetwork>(out var player))
			{
				if (player.GetCurrentHealth() <= 0) {
					// TODO: Implement proper win/loss handling
					Debug.Log($"GAME MANAGER || Player {player.OwnerClientId} has been defeated!");
					return true;
				}
			}
		}
		return false;
	}

	[ClientRpc]
	private void NotifyPlanningPhaseStartClientRpc()
	{
		Debug.Log("GAME MANAGER || === PLANNING PHASE === Take your actions!");
	}

	[ClientRpc]
	private void RevealBoardsClientRpc()
	{
		Debug.Log("GAME MANAGER || Revealing boards...");
		// TODO: Trigger visual effects, reveal opponent cards, etc.
		
		// Could add board reveal logic here
		if (BoardManager.Instance != null) {
			// BoardManager.Instance.RevealAllCards();
		}
	}

	[ClientRpc]
	private void CleanupExhaustedCardsClientRpc()
	{
		Debug.Log("GAME MANAGER || Cleaning up exhausted cards...");
		
		// TODO: Remove cards with 0 charges from the board
		if (BoardManager.Instance != null) {
			// BoardManager.Instance.RemoveExhaustedCards();
		}
	}

	public bool CanPlayCards()
	{
		return CurrentPhase.Value == GamePhase.Planning;
	}

	public bool CanDrawCards()
	{
		return CurrentPhase.Value == GamePhase.Planning;
	}

	public bool CanAttack()
	{
		return CurrentPhase.Value == GamePhase.Planning;
	}

	public override void OnNetworkDespawn()
	{
		if (IsServer)
		{
			CurrentPhase.OnValueChanged -= OnPhaseChanged;
		}
		if (IsClient && !IsServer)
		{
			CurrentPhase.OnValueChanged -= OnPhaseChangedClient;
		}
	}
}