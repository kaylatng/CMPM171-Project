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

	// Attack intents recorded during planning (attackerClientId, slotIndex). Cleared when processing reveal.
	private List<(ulong attackerId, int slotIndex)> attackIntents = new List<(ulong, int)>();

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

	/// <summary>Server only. Records that this player's card in slot will attack during reveal.</summary>
	public void RecordAttackIntent(ulong attackerClientId, int slotIndex)
	{
		if (!IsServer) return;
		attackIntents.Add((attackerClientId, slotIndex));
	}

	private System.Collections.IEnumerator ProcessReveal()
	{
		Debug.Log("GAME MANAGER || === REVEAL PHASE ===");
		
		// reveal all cards on both boards (client clears tilts, then flip + merge)
		RevealBoardsClientRpc();

		// Wait for flip and upgrade/merge to complete before processing attacks (upgrades first, then attack animations)
		yield return new WaitForSeconds(3f);

		// Process attacks: apply damage, decrement charges, then send data to clients (opponent attacks first, one by one)
		CardLibrary library = CardManager.Instance != null ? CardManager.Instance.GetCardLibrary() : null;
		foreach (var (attackerId, slotIndex) in attackIntents)
		{
			if (!GetPlayer(attackerId, out PlayerNetwork attacker) || !GetOtherPlayer(attackerId, out PlayerNetwork defender))
				continue;

			var boardIds = attacker.GetBoardCards();
			if (slotIndex >= boardIds.Length) continue;
			int cardId = boardIds[slotIndex];
			if (cardId == -1) continue;

			int damage = 1;
			if (library != null)
			{
				CardData cardData = library.GetTierOneAssetFromPool(cardId);
				if (cardData != null) damage = cardData.attackDamage;
			}

			defender.ApplyDamageServer(damage);
			int newCharges = attacker.DecrementBoardChargeServer(slotIndex);
			bool removeCard = newCharges <= 0;
			if (removeCard)
				attacker.RemoveBoardCardServer(slotIndex);

			ProcessAttackDataClientRpc(attackerId, slotIndex, damage, newCharges, removeCard);
		}
		attackIntents.Clear();
		ProcessAttacksPlayClientRpc();

		yield return new WaitForSeconds(revealPhaseDuration);

		// move to cleanup phase
		CurrentPhase.Value = GamePhase.Cleanup;
	}

	private bool GetPlayer(ulong clientId, out PlayerNetwork player)
	{
		player = null;
		if (NetworkManager.Singleton == null || !NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
			return false;
		return client.PlayerObject.TryGetComponent(out player);
	}

	private bool GetOtherPlayer(ulong excludeId, out PlayerNetwork other)
	{
		other = null;
		foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
		{
			if (id == excludeId) continue;
			if (GetPlayer(id, out other)) return true;
		}
		return false;
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
		if (NetworkManager.Singleton == null) return false;

		// find any player with HP <= 0
		ulong loserId = ulong.MaxValue;
		foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
		{
			if (client.PlayerObject != null && client.PlayerObject.TryGetComponent<PlayerNetwork>(out var player))
			{
				if (player.GetCurrentHealth() <= 0)
				{
					loserId = client.ClientId;
					Debug.Log($"GAME MANAGER || Player {loserId} has been defeated!");
					break;
				}
			}
		}

		if (loserId == ulong.MaxValue)
			return false;

		// winner is the other connected client (for 1v1)
		ulong winnerId = loserId;
		foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
		{
			if (id != loserId)
			{
				winnerId = id;
				break;
			}
		}

		Debug.Log($"GAME MANAGER || Game over. Winner: {winnerId}, Loser: {loserId}");
		GameOverClientRpc(winnerId, loserId);
		return true;
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
		if (BoardManager.Instance != null)
		{
			BoardManager.Instance.StartRevealSequence();
		}
	}

	[ClientRpc]
	private void ProcessAttackDataClientRpc(ulong attackerClientId, int slotIndex, int damageDealt, int chargesRemaining, bool removeCard)
	{
		if (BoardManager.Instance != null)
		{
			BoardManager.Instance.ReceiveAttackData(attackerClientId, slotIndex, damageDealt, chargesRemaining, removeCard);
		}
	}

	[ClientRpc]
	private void ProcessAttacksPlayClientRpc()
	{
		if (BoardManager.Instance != null)
		{
			BoardManager.Instance.PlayAttacksSequence();
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

	[ClientRpc]
	private void GameOverClientRpc(ulong winnerClientId, ulong loserClientId)
	{
		if (GameManagerUI.Instance == null || NetworkManager.Singleton == null)
			return;

		ulong localId = NetworkManager.Singleton.LocalClientId;
		if (localId == winnerClientId)
		{
			GameManagerUI.Instance.ShowGameOver(true);
		}
		else if (localId == loserClientId)
		{
			GameManagerUI.Instance.ShowGameOver(false);
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

	/// <summary>Any client can request a full game reset. Keeps host and client connected; resets all stats and board.</summary>
	[ServerRpc(RequireOwnership = false)]
	public void RequestResetGameServerRpc(ServerRpcParams serverRpcParams = default)
	{
		if (!IsServer) return;

		Debug.Log("GAME MANAGER || Reset requested - resetting game to beginning");
		StopAllCoroutines();
		attackIntents.Clear();
		CurrentRound.Value = 0;
		CurrentPhase.Value = GamePhase.ResourceGain;

		if (DeckManager.Instance != null)
			DeckManager.Instance.ResetDeck();

		foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
		{
			if (client.PlayerObject != null && client.PlayerObject.TryGetComponent<PlayerNetwork>(out var player))
				player.ResetPlayerStateServer();
		}

		ResetGameVisualsClientRpc();
		StartCoroutine(ProcessResourceGain());
	}

	[ClientRpc]
	private void ResetGameVisualsClientRpc()
	{
		if (BoardManager.Instance != null)
			BoardManager.Instance.ClearAllBoardsForReset();
		if (CardManager.Instance != null)
		{
			CardManager.Instance.ClearHandZone(true);
			CardManager.Instance.ClearHandZone(false);
		}
		if (GameManagerUI.Instance != null)
		{
			GameManagerUI.Instance.HideGameOver();
		}
		Debug.Log("GAME MANAGER || Client: boards and hands cleared");
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