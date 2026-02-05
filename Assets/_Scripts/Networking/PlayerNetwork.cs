using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerNetwork : NetworkBehaviour {

	private NetworkVariable<PlayerData> playerData = new NetworkVariable<PlayerData>(
		new PlayerData {
			Health = 20,
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
		// update UI with new values
		if (GameManagerUI.Instance != null) {
			GameManagerUI.Instance.UpdateResourceUI(newValue.ActionPoints, newValue.Mana, newValue.Health);
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

		// 5. MANA CHECK - verify player can afford the card
		CardLibrary library = CardManager.Instance?.GetCardLibrary();
		if (library != null) {
			CardData cardData = library.GetTierOneAssetFromPool(cardId);
			if (cardData != null && cardData.manaCost > data.Mana) {
				Debug.Log($"PLAYER NETWORK || Not enough mana! Card costs {cardData.manaCost}, player has {data.Mana}");
				return;
			}
			
			// DEDUCT MANA COST
			if (cardData != null && cardData.manaCost > 0) {
				data.Mana -= cardData.manaCost;
				Debug.Log($"PLAYER NETWORK || Spent {cardData.manaCost} mana. {data.Mana} remaining");
			}
		}

		// DEDUCT 1 ACTION POINT for playing the card
		data.ActionPoints--;
		Debug.Log($"PLAYER NETWORK || Spent 1 AP. {data.ActionPoints} remaining");

		// remove card from hand
		data.HandCardIds.Remove(cardId);
		data.UpdateHandCount();

		// ensure BoardCardIds has 3 slots (initialize with -1 for empty)
		while (data.BoardCardIds.Length < 3) {
			data.BoardCardIds.Add(-1);
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

		// place new card in slot
		data.BoardCardIds[slotIndex] = cardId;
		playerData.Value = data;

		Debug.Log($"PLAYER NETWORK || Player {OwnerClientId} played card {cardId} to slot {slotIndex}");

		// notify opponent to show card back on their board (not revealed yet)
		ulong opponentId = GetOpponentId(OwnerClientId);
		if (opponentId != OwnerClientId) {
			ClientRpcParams opponentParams = new ClientRpcParams {
				Send = new ClientRpcSendParams {
					TargetClientIds = new ulong[] { opponentId }
				}
			};
			NotifyOpponentCardPlayedClientRpc(cardId, slotIndex, opponentParams);
		}
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