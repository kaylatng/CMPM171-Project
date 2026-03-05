using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerNetwork : NetworkBehaviour {

	private NetworkVariable<PlayerData> playerData = new NetworkVariable<PlayerData>(
		new PlayerData {
			Health = 20, // changed from 20 to 5
			Mana = 0,
			ActionPoints = 5,
			IsReady = false,
			CardsInHandCount = 0,
			PlayerName = "Placeholder Name",
		}, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
	);

	public event Action<PlayerData> OnPlayerDataChanged;

	public int GetHP() { return playerData.Value.Health; }
	public int GetMana() { return playerData.Value.Mana; }
	public int GetAP() { return playerData.Value.ActionPoints; }

	public struct PlayerData : INetworkSerializable {
		public int Health;
		public int Mana;
		public int ActionPoints;
		public bool IsReady;
		public int CardsInHandCount;
		public FixedString128Bytes PlayerName;
		public FixedList32Bytes<int> HandCardIds;
		public FixedList32Bytes<int> BoardCardIds;
		public FixedList32Bytes<int> BoardCardCharges; // attack charges per slot (same order as BoardCardIds)
		public FixedList32Bytes<int> BoardCardTiers;   // tier per slot (1 = base, 2, 3...)

		public void UpdateHandCount() {
			CardsInHandCount = HandCardIds.Length;
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
			serializer.SerializeValue(ref Health);
			serializer.SerializeValue(ref Mana);
			serializer.SerializeValue(ref ActionPoints);
			serializer.SerializeValue(ref IsReady);
			serializer.SerializeValue(ref CardsInHandCount);
			serializer.SerializeValue(ref PlayerName);
			
			// serialize hand cards
			if (serializer.IsReader) {
				int count = 0;
				serializer.SerializeValue(ref count);
				HandCardIds.Clear();
				for (int i = 0; i < count; i++) {
					int cardId = 0;
					serializer.SerializeValue(ref cardId);
					HandCardIds.Add(cardId);
				}
			} else {
				int count = HandCardIds.Length;
				serializer.SerializeValue(ref count);
				for (int i = 0; i < HandCardIds.Length; i++) {
					int cardId = HandCardIds[i];
					serializer.SerializeValue(ref cardId);
				}
			}

			// serialize BoardCardIds
			if (serializer.IsReader) {
				int boardCount = 0;
				serializer.SerializeValue(ref boardCount);
				BoardCardIds.Clear();
				for (int i = 0; i < boardCount; i++) {
					int cardId = 0;
					serializer.SerializeValue(ref cardId);
					BoardCardIds.Add(cardId);
				}
			} else {
				int boardCount = BoardCardIds.Length;
				serializer.SerializeValue(ref boardCount);
				for (int i = 0; i < BoardCardIds.Length; i++) {
					int cardId = BoardCardIds[i];
					serializer.SerializeValue(ref cardId);
				}
			}

			// serialize BoardCardCharges
			if (serializer.IsReader) {
				int chargeCount = 0;
				serializer.SerializeValue(ref chargeCount);
				BoardCardCharges.Clear();
				for (int i = 0; i < chargeCount; i++) {
					int ch = 0;
					serializer.SerializeValue(ref ch);
					BoardCardCharges.Add(ch);
				}
			} else {
				int chargeCount = BoardCardCharges.Length;
				serializer.SerializeValue(ref chargeCount);
				for (int i = 0; i < BoardCardCharges.Length; i++) {
					int ch = BoardCardCharges[i];
					serializer.SerializeValue(ref ch);
				}
			}

			// serialize BoardCardTiers
			if (serializer.IsReader) {
				int tierCount = 0;
				serializer.SerializeValue(ref tierCount);
				BoardCardTiers.Clear();
				for (int i = 0; i < tierCount; i++) {
					int tier = 1;
					serializer.SerializeValue(ref tier);
					BoardCardTiers.Add(tier);
				}
			} else {
				int tierCount = BoardCardTiers.Length;
				serializer.SerializeValue(ref tierCount);
				for (int i = 0; i < BoardCardTiers.Length; i++) {
					int tier = BoardCardTiers[i];
					serializer.SerializeValue(ref tier);
				}
			}
		}
	}

	public override void OnNetworkSpawn() {
		playerData.OnValueChanged += (PlayerData previousValue, PlayerData newValue) => {
			string idListString = "";
			CardLibrary library = CardManager.Instance.GetCardLibrary();

			for (int i = 0; i < newValue.HandCardIds.Length; i++) {
				int poolId = newValue.HandCardIds[i];
				int assetId = library.GetMappedAssetID(poolId);

				idListString += newValue.HandCardIds[i].ToString();
				idListString += $" (Asset ID: {assetId})";
				
				if (i < newValue.HandCardIds.Length - 1) {
					idListString += ", ";
				}
			}
			Debug.Log($"Player {OwnerClientId} | HP: {newValue.Health} | Mana: {newValue.Mana} | AP: {newValue.ActionPoints} | Ready: {newValue.IsReady} | Hand: [{idListString}]");
		};
		// Debug.Log(OwnerClientId + "; " + newValue.Health + "; " + newValue.IsReady + "; " + newValue.PlayerName + "; Cards in hand: " + newValue.CardsInHandCount);
		// 	OnPlayerDataChanged?.Invoke(newValue);

		// notify UI to update when local player data changes
		if (IsOwner) {
			playerData.OnValueChanged += OnLocalPlayerDataChanged;
		}
	}

	private void OnLocalPlayerDataChanged(PlayerData previousValue, PlayerData newValue) {
		// update UI with new values (pending attack deduction applied so display matches intent during Planning)
		if (GameManagerUI.Instance != null) {
			GameManagerUI.Instance.OnServerResourceUpdate(newValue.ActionPoints, newValue.Mana, newValue.Health);
		}
	}

	// Forwarder for external subscribers; NetworkVariable.OnValueChanged uses a different delegate type than Action<,>.
	private Action<PlayerData, PlayerData> dataChangedHandler;
	private void ForwardDataChanged(PlayerData previous, PlayerData next) => dataChangedHandler?.Invoke(previous, next);

	/// <summary>Subscribe to this player's replicated data changes (e.g. so opponent can sync board from our BoardCardIds).</summary>
	public void SubscribeToDataChanged(Action<PlayerData, PlayerData> handler) {
		dataChangedHandler = handler;
		playerData.OnValueChanged += ForwardDataChanged;
	}

	/// <summary>Unsubscribe from data changes (e.g. when BoardManager is destroyed).</summary>
	public void UnsubscribeFromDataChanged(Action<PlayerData, PlayerData> handler) {
		if (dataChangedHandler == handler) {
			playerData.OnValueChanged -= ForwardDataChanged;
			dataChangedHandler = null;
		}
	}

	public bool IsPlayerReady() {
		return playerData.Value.IsReady;
	}

	public void FinishTurn() {
		if (!IsOwner) return;

		// can only ready during planning phase
		if (GameManager.Instance != null && !GameManager.Instance.CanPlayCards()) {
			Debug.Log("PLAYER NETWORK || Cannot ready - not in Planning phase");
			return;
		}

		SetReadyServerRpc(true);
	}

	private void Update() {
		if (!IsOwner) return;

		// debug keys
		if (Keyboard.current.tKey.wasPressedThisFrame) {
			UpdatePlayerStateServerRpc();
		}

		if (Keyboard.current.dKey.wasPressedThisFrame) {
			RequestCardDrawServerRpc();
		}
	}

	public void StartNewTurnServer() {
		if (!IsServer) return;
		PlayerData data = playerData.Value;
		data.ActionPoints = 5; // reset to 5 AP
		data.Mana += 1; // gain 1 mana
		data.IsReady = false; // reset ready status
		playerData.Value = data;

		Debug.Log($"PLAYER NETWORK || Player {OwnerClientId} - New turn: 5 AP, {data.Mana} Mana");
	}

	/// <summary>Server only. Resets this player to initial game state (HP, Mana, AP, ready, hand, board).</summary>
	public void ResetPlayerStateServer() {
		if (!IsServer) return;
		PlayerData data = new PlayerData {
			Health = 20,
			Mana = 0,
			ActionPoints = 5,
			IsReady = false,
			CardsInHandCount = 0,
			PlayerName = playerData.Value.PlayerName,
			HandCardIds = default,
			BoardCardIds = default,
			BoardCardCharges = default,
			BoardCardTiers = default,
		};
		playerData.Value = data;
		Debug.Log($"PLAYER NETWORK || Player {OwnerClientId} reset to initial state");
	}

	// bypass the phase check for automatic draw at start of turn
	public void ExecuteDrawServer(bool isFree) {
		if (!IsServer) return;

		if (playerData.Value.CardsInHandCount >= 5) {
			Debug.Log($"PLAYER NETWORK || Player {OwnerClientId} hand is full (5/5)");
			return;
		}

		int drawnCardId = DeckManager.Instance.DrawCard();
		if (drawnCardId == -1) {
			Debug.Log($"PLAYER NETWORK || Deck is empty!");
			return;
		}

		PlayerData data = playerData.Value;
		
		data.HandCardIds.Add(drawnCardId);
		data.UpdateHandCount();

		// only charge AP if it's a manual draw (not the free blind draw)
		if (!isFree) {
			data.ActionPoints--;
			Debug.Log($"PLAYER NETWORK || Player {OwnerClientId} drew card {drawnCardId} (cost 1 AP, {data.ActionPoints} remaining)");
		} else {
			Debug.Log($"PLAYER NETWORK || Player {OwnerClientId} drew card {drawnCardId} (free blind draw)");
		}

		playerData.Value = data;

		SendDrawRpcs(drawnCardId);
	}

	private void SendDrawRpcs(int cardId) {
		if (!IsServer) return;

		// send the actual card to the player who drew it
		ClientRpcParams drawerParams = new ClientRpcParams {
			Send = new ClientRpcSendParams {
				TargetClientIds = new ulong[] { OwnerClientId }
			}
		};
		ReceiveCardClientRpc(cardId, true, drawerParams);

		// send a hidden card to the opponent
		ulong opponentId = GetOpponentId(OwnerClientId);
		if (opponentId != OwnerClientId) {
			ClientRpcParams othersParams = new ClientRpcParams {
				Send = new ClientRpcSendParams {
					TargetClientIds = new ulong[] { opponentId }
				}
			};
			ReceiveCardClientRpc(-1, false, othersParams);
		}
	}

	[ServerRpc]
	private void UpdatePlayerStateServerRpc(ServerRpcParams serverRpcParams = default) {
		Debug.Log("UpdatePlayerStateServerRpc " + OwnerClientId + "; " + serverRpcParams.Receive.SenderClientId);
		var senderId = serverRpcParams.Receive.SenderClientId;
		PlayerData data = playerData.Value;

		data.Health -= 1;
		data.IsReady = !data.IsReady;
		data.PlayerName = (senderId == 0) ? "Host" : "Client";
		
		playerData.Value = data;

		NotifyPlayerPokedClientRpc(senderId);
	}

	[ServerRpc]
	public void RequestCardDrawServerRpc(ServerRpcParams serverRpcParams = default) {
		Debug.Log($"PLAYER NETWORK || RequestCardDrawServerRpc from Player {OwnerClientId}");
		
		// 1. PHASE CHECK - can only draw during planning phase
		if (GameManager.Instance == null || GameManager.Instance.CurrentPhase.Value != GameManager.GamePhase.Planning) {
			Debug.Log("PLAYER NETWORK || Cannot draw - not in Planning phase");
			return;
		}

		// 2. AP CHECK - must have at least 1 AP
		if (playerData.Value.ActionPoints <= 0) {
			Debug.Log($"PLAYER NETWORK || Player {OwnerClientId} - Not enough AP! ({playerData.Value.ActionPoints}/5)");
			return;
		}

		// 3. HAND SIZE CHECK - can't exceed 5 cards
		if (playerData.Value.CardsInHandCount >= 5) {
			Debug.Log($"PLAYER NETWORK || Player {OwnerClientId} - Hand full! (5/5)");
			return;
		}

		// all checks passed - execute the draw (costs 1 AP)
		ExecuteDrawServer(isFree: false);
	}

	[ServerRpc]
	private void SetReadyServerRpc(bool readyStatus) {
		PlayerData data = playerData.Value;
		
		data.IsReady = readyStatus;
		playerData.Value = data;

		Debug.Log($"PLAYER NETWORK || Player {OwnerClientId} ready status: {readyStatus}");

		// notify game manager to check if both players are ready
		if (GameManager.Instance != null) {
			GameManager.Instance.CheckPlayersReadyServerRpc();
		}
	}

	[ServerRpc]
	public void RequestAttackServerRpc(int slotIndex, ServerRpcParams serverRpcParams = default) {
		if (!IsServer) return;

		// 1. PHASE CHECK - can only attack during planning phase
		if (GameManager.Instance == null || GameManager.Instance.CurrentPhase.Value != GameManager.GamePhase.Planning) {
			Debug.Log("PLAYER NETWORK || Cannot attack - not in Planning phase");
			return;
		}

		// 2. AP CHECK
		if (playerData.Value.ActionPoints <= 0) {
			Debug.Log($"PLAYER NETWORK || Player {OwnerClientId} - Not enough AP to attack!");
			return;
		}

		// 3. MANA CHECK (attack costs 1 Mana)
		if (playerData.Value.Mana < 1) {
			Debug.Log($"PLAYER NETWORK || Player {OwnerClientId} - Not enough Mana to attack!");
			return;
		}

		// 4. SLOT VALIDATION
		if (slotIndex < 0 || slotIndex >= 3) {
			Debug.Log($"PLAYER NETWORK || Invalid slot index for attack: {slotIndex}");
			return;
		}

		PlayerData data = playerData.Value;

		// Ensure board lists have 3 slots
		while (data.BoardCardIds.Length < 3) data.BoardCardIds.Add(-1);
		while (data.BoardCardCharges.Length < 3) data.BoardCardCharges.Add(0);

		if (data.BoardCardIds[slotIndex] == -1 || data.BoardCardCharges[slotIndex] <= 0) {
			Debug.Log($"PLAYER NETWORK || No card or no attack charges in slot {slotIndex}");
			return;
		}

		data.ActionPoints--;
		data.Mana--;
		playerData.Value = data;

		if (GameManager.Instance != null) {
			GameManager.Instance.RecordAttackIntent(OwnerClientId, slotIndex);
		}

		NotifyAttackScheduledClientRpc(OwnerClientId, slotIndex);
		Debug.Log($"PLAYER NETWORK || Player {OwnerClientId} scheduled attack with slot {slotIndex}");
	}

	[ClientRpc]
	private void NotifyAttackScheduledClientRpc(ulong attackerClientId, int slotIndex) {
		if (BoardManager.Instance != null) {
			BoardManager.Instance.OnAttackScheduled(attackerClientId, slotIndex);
		}
	}

	[ServerRpc]
	public void RemoveCardFromHandServerRpc(int cardId) {
		PlayerData data = playerData.Value;

		if (data.HandCardIds.Contains(cardId)) {
			data.HandCardIds.Remove(cardId);
			data.UpdateHandCount();
			playerData.Value = data;

			Debug.Log($"PLAYER NETWORK || Removed card {cardId} from Player {OwnerClientId}'s hand");
		}
	}

	[ServerRpc]
	public void PlayCardToSlotServerRpc(int cardId, int slotIndex, ServerRpcParams serverRpcParams = default) {
		if (!IsServer) return;

		Debug.Log($"PLAYER NETWORK || PlayCardToSlotServerRpc: Card {cardId} to slot {slotIndex}");

		// 1. PHASE CHECK - can only play cards during planning phase
		if (GameManager.Instance == null || GameManager.Instance.CurrentPhase.Value != GameManager.GamePhase.Planning) {
			Debug.Log("PLAYER NETWORK || Cannot play card - not in Planning phase");
			return;
		}

		// 2. AP CHECK - must have at least 1 AP to play a card
		if (playerData.Value.ActionPoints <= 0) {
			Debug.Log($"PLAYER NETWORK || Player {OwnerClientId} - Not enough AP to play card!");
			return;
		}

		PlayerData data = playerData.Value;

		// 3. HAND CHECK - card must be in player's hand
		if (!data.HandCardIds.Contains(cardId)) {
			Debug.Log($"PLAYER NETWORK || Card {cardId} not in player's hand");
			return;
		}
		
		// 4. SLOT VALIDATION
		if (slotIndex < 0 || slotIndex >= 3) {
			Debug.Log($"PLAYER NETWORK || Invalid slot index: {slotIndex}");
			return;
		}

		// Mana is only spent when a card is tapped to attack (ScheduleAttackServerRpc), not when playing to board.

		// DEDUCT 1 ACTION POINT for playing the card
		data.ActionPoints--;
		Debug.Log($"PLAYER NETWORK || Spent 1 AP. {data.ActionPoints} remaining");

		// remove card from hand
		data.HandCardIds.Remove(cardId);
		data.UpdateHandCount();

		// ensure BoardCardIds / charges / tiers have 3 slots
		while (data.BoardCardIds.Length < 3) {
			data.BoardCardIds.Add(-1);
		}
		while (data.BoardCardCharges.Length < 3) {
			data.BoardCardCharges.Add(0);
		}
		while (data.BoardCardTiers.Length < 3) {
			data.BoardCardTiers.Add(1);
		}

		// if slot already has a card, return that card to hand (no extra AP cost)
		if (data.BoardCardIds[slotIndex] != -1) {
			int replacedCardId = data.BoardCardIds[slotIndex];
			data.HandCardIds.Add(replacedCardId);
			data.UpdateHandCount();
			Debug.Log($"PLAYER NETWORK || Swapped card {replacedCardId} back to hand");

			ClientRpcParams playerParams = new ClientRpcParams {
				Send = new ClientRpcSendParams {
					TargetClientIds = new ulong[] { OwnerClientId }
				}
			};
			ReturnCardToHandClientRpc(replacedCardId, playerParams);
		}

		// place new card in slot and set attack charges (default 1 if asset has 0)
		int maxCharges = 1;
		CardLibrary library = CardManager.Instance?.GetCardLibrary();
		if (library != null) {
			CardData cardData = library.GetTierOneAssetFromPool(cardId);
			if (cardData != null && cardData.maxCharges > 0) maxCharges = cardData.maxCharges;
		}
		data.BoardCardIds[slotIndex] = cardId;
		data.BoardCardCharges[slotIndex] = maxCharges;
		data.BoardCardTiers[slotIndex] = 1; // newly played card starts at base tier
		playerData.Value = data;

		Debug.Log($"PLAYER NETWORK || Player {OwnerClientId} played card {cardId} to slot {slotIndex}");

		// Opponent board is updated only from replicated BoardCardIds (BoardManager.SyncOpponentBoardFromServerState),
		// so we do not send NotifyOpponentCardPlayedClientRpc here — that caused wrong counts (e.g. 1 card shown as 2, or 2 as 1).
	}

	[ClientRpc]
	private void ReturnCardToHandClientRpc(int cardId, ClientRpcParams clientRpcParams = default) {
		// find the card on the board and return it to hand
		if (BoardManager.Instance != null) {
			// for (int i = 0; i < 3; i++) {
			// 	GameObject card = BoardManager.Instance.GetCardInSlot(i, true);
			// 	if (card != null) {
			// 		CardVisual visual = card.GetComponent<CardVisual>();
			// 		if (visual != null && visual.CardID == cardId) {
			// 			BoardManager.Instance.ReturnCardToHand(card);
			// 			break;
			// 		}
			// 	}
			// }

			List<GameObject> boardCards = BoardManager.Instance.GetBoardCards(true);
			
			foreach (GameObject card in boardCards) {
					if (card != null) {
							CardVisual visual = card.GetComponent<CardVisual>();
							if (visual != null && visual.CardID == cardId) {
									BoardManager.Instance.ReturnCardToHand(card);
									break;
							}
					}
			}
		}
	}

	[ClientRpc]
	private void NotifyPlayerPokedClientRpc(ulong playerId) {
		if (playerId == NetworkManager.Singleton.LocalClientId) {
			Debug.Log("T pressed, server acknowledged");
		} else {
			Debug.Log($"playerId: {playerId} pressed T");
		}
	}

	[ClientRpc]
	private void NotifyOpponentCardPlayedClientRpc(int cardId, int slotIndex, ClientRpcParams clientRpcParams = default) {
		// check if this client is the player who played the card
		if (NetworkManager.Singleton.LocalClientId == OwnerClientId) {
			Debug.Log($"PLAYER NETWORK || Skipping - this is the player who played the card");
			return; // skip - we're the player who played it
		}

		Debug.Log($"PLAYER NETWORK || NotifyOpponentCardPlayedClientRpc - CardID: {cardId}, SlotIndex: {slotIndex}");

		// place opponent card on board
		if (BoardManager.Instance != null) {
			BoardManager.Instance.PlaceOpponentCard(cardId, slotIndex);
		}

		Debug.Log($"PLAYER NETWORK || Opponent played card {cardId} to slot {slotIndex}");
	}

	// PUBLIC GETTERS for other systems to access player data
	public int GetCurrentHealth() {
		return playerData.Value.Health;
	}

	public int GetCurrentMana() {
		return playerData.Value.Mana;
	}

	public int GetCurrentActionPoints() {
		return playerData.Value.ActionPoints;
	}

	public FixedList32Bytes<int> GetBoardCards() {
		return playerData.Value.BoardCardIds;
	}

	/// <summary>Server only. Apply damage to this player (the defender).</summary>
	public void ApplyDamageServer(int damage) {
		if (!IsServer) return;
		PlayerData data = playerData.Value;
		data.Health = Mathf.Max(0, data.Health - damage);
		playerData.Value = data;
		Debug.Log($"PLAYER NETWORK || Player {OwnerClientId} took {damage} damage. HP: {data.Health}");
	}

	/// <summary>Server only. Decrement attack charge in slot; returns new charge count.</summary>
	public int DecrementBoardChargeServer(int slotIndex) {
		if (!IsServer) return 0;
		PlayerData data = playerData.Value;
		while (data.BoardCardCharges.Length <= slotIndex) data.BoardCardCharges.Add(0);
		int ch = Mathf.Max(0, data.BoardCardCharges[slotIndex] - 1);
		data.BoardCardCharges[slotIndex] = ch;
		playerData.Value = data;
		return ch;
	}

	/// <summary>Server only. Remove card from board slot (clear id and charges).</summary>
	public void RemoveBoardCardServer(int slotIndex) {
		if (!IsServer) return;
		PlayerData data = playerData.Value;
		while (data.BoardCardIds.Length <= slotIndex) data.BoardCardIds.Add(-1);
		while (data.BoardCardCharges.Length <= slotIndex) data.BoardCardCharges.Add(0);
		while (data.BoardCardTiers.Length <= slotIndex) data.BoardCardTiers.Add(1);
		data.BoardCardIds[slotIndex] = -1;
		data.BoardCardCharges[slotIndex] = 0;
		data.BoardCardTiers[slotIndex] = 1;
		playerData.Value = data;
		Debug.Log($"PLAYER NETWORK || Player {OwnerClientId} slot {slotIndex} card removed (out of charges)");
	}

	/// <summary>Server only. Upgrade card tier and refresh its max charges in a board slot.</summary>
	[ServerRpc]
	public void UpgradeBoardCardTierServerRpc(int slotIndex, int newTier, int newMaxCharges) {
		if (!IsServer) return;
		if (slotIndex < 0 || slotIndex >= 3) return;

		PlayerData data = playerData.Value;
		while (data.BoardCardIds.Length <= slotIndex) data.BoardCardIds.Add(-1);
		while (data.BoardCardCharges.Length <= slotIndex) data.BoardCardCharges.Add(0);
		while (data.BoardCardTiers.Length <= slotIndex) data.BoardCardTiers.Add(1);

		// Only upgrade if there is actually a card in this slot
		if (data.BoardCardIds[slotIndex] == -1) return;

		int clampedTier = Mathf.Clamp(newTier, 1, 3);
		data.BoardCardTiers[slotIndex] = clampedTier;
		if (newMaxCharges > 0) {
			data.BoardCardCharges[slotIndex] = newMaxCharges;
		}

		playerData.Value = data;
		Debug.Log($"PLAYER NETWORK || Upgraded slot {slotIndex} to tier {clampedTier}, maxCharges {newMaxCharges}");
	}

	/// <summary>Get current tier of a board card on the server (1 if unset).</summary>
	public int GetBoardCardTier(int slotIndex) {
		var tiers = playerData.Value.BoardCardTiers;
		if (slotIndex < 0 || slotIndex >= tiers.Length) return 1;
		int t = tiers[slotIndex];
		return (t <= 0) ? 1 : t;
	}

	[ClientRpc]
	private void ReceiveCardClientRpc(int cardId, bool isMyCard, ClientRpcParams clientRpcParams = default) {
		if (CardManager.Instance != null) {
			CardManager.Instance.SpawnCard(cardId, isMyCard);
		} else {
			Debug.LogError("PLAYER NETWORK || CardManager.Instance is null - cannot spawn card visual");
		}
	}

	private ulong GetOpponentId(ulong drawerId) {
		foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds) {
			if (clientId != drawerId) return clientId;
		}
		return drawerId;
	}

	public override void OnNetworkDespawn() {
		if (IsOwner) {
			playerData.OnValueChanged -= OnLocalPlayerDataChanged;
		}
	}
}