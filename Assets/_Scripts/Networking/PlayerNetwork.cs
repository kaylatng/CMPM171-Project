using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerNetwork : NetworkBehaviour {

	// [SerializeField] private GameObject cardPrefab;
	// private Transform playerHandZone;
	// private Transform opponentHandZone;


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
		/*
		GameObject pZone = GameObject.Find("PlayerHandZone");
		GameObject oZone = GameObject.Find("OpponentHandZone");

		if (pZone != null) playerHandZone = pZone.transform;
		if (oZone != null) opponentHandZone = oZone.transform;

		AssignHandZones();
		*/
		
		// playerData.OnValueChanged += (PlayerData previousValue, PlayerData newValue) => {
		// 	Debug.Log(OwnerClientId + "; " + newValue.Health + "; " + newValue.IsReady + "; " + newValue.PlayerName + "; Cards in hand: " + newValue.HandCardIds);
		// };
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
			Debug.Log($"Player {OwnerClientId} | HP: {newValue.Health} | Hand Count: {newValue.CardsInHandCount} | IDs: [{idListString}]");
    };
	}

	public bool IsPlayerReady() {
		return playerData.Value.IsReady;
	}

	public void FinishTurn() {
    if (!IsOwner) return;

    SetReadyServerRpc(true);
	}

  private void Update() {
		if (!IsOwner) return;

		if (Keyboard.current.tKey.wasPressedThisFrame) {
			UpdatePlayerStateServerRpc();
			/*
			PlayerData data = playerData.Value;

			data.Health -= 1;
			data.IsReady = !data.IsReady;
			data.PlayerName = "Molly";

			// playerData.Value = new PlayerData {
			// 	Health = 10,
			// 	IsReady = true,
			// 	PlayerName = "Placeholder Name",
			// };

			playerData.Value = data;
			*/
		}

		if (Keyboard.current.dKey.wasPressedThisFrame) {
			RequestCardDrawServerRpc();
		}
	}

	public void StartNewTurnServer() {
		if (!IsServer) return;
		PlayerData data = playerData.Value;
		data.ActionPoints = 5;
		data.Mana += 1;
		data.IsReady = false; // Reset ready status for new phase
		playerData.Value = data;
	}

	// bypass the phase check for automatic draw
	public void ExecuteDrawServer(bool isFree) {
		if (!IsServer) return;

		if (playerData.Value.CardsInHandCount >= 5) return;

		int drawnCardId = DeckManager.Instance.DrawCard();
		if (drawnCardId == -1) return;

		PlayerData data = playerData.Value;
		
		data.HandCardIds.Add(drawnCardId);
		data.UpdateHandCount();
		// data.CardsInHandCount++;

		if (!isFree) data.ActionPoints--; // only charge AP if it's a manual draw
		playerData.Value = data;

		SendDrawRpcs(drawnCardId);
	}

	private void SendDrawRpcs(int cardId) {
		if (!IsServer) return;

    ClientRpcParams drawerParams = new ClientRpcParams {
			Send = new ClientRpcSendParams {
				TargetClientIds = new ulong[] { OwnerClientId }
			}
    };
		// Debug.Log("CardId Pulled: " + cardId);
    ReceiveCardClientRpc(cardId, true, drawerParams);

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
		Debug.Log("RequestCardDrawServerRpc " + OwnerClientId + "; " + serverRpcParams.Receive.SenderClientId);
		
		/**
		var senderId = serverRpcParams.Receive.SenderClientId;

		if (GameManager.Instance.CurrentPhase.Value != GameManager.GamePhase.Planning) return;
		
		if (playerData.Value.CardsInHandCount >= 5) {
			Debug.Log("Hand is full. Must use action point to discard.");
			return;
		}
		
		int drawnCardId = DeckManager.Instance.DrawCard();

		if (drawnCardId == -1) {
			Debug.Log("Deck is empty");
			return;
		}

		PlayerData data = playerData.Value;
		data.CardsInHandCount++;
		playerData.Value = data;

		// send card ID to drawing player
		ClientRpcParams drawerParams = new ClientRpcParams {
			Send = new ClientRpcSendParams {
				TargetClientIds = new ulong[] { senderId }
			}
		};
		ReceiveCardClientRpc(drawnCardId, true, drawerParams);

		// tell other player to show hidden card
		ulong opponentId = GetOpponentId(senderId);
		if (opponentId != senderId) {
			ClientRpcParams othersParams = new ClientRpcParams {
				Send = new ClientRpcSendParams {
					TargetClientIds = new ulong[] { opponentId }
				}
			};
			ReceiveCardClientRpc(-1, false, othersParams);
		}
		**/
		// 1. phase check
    if (GameManager.Instance.CurrentPhase.Value != GameManager.GamePhase.Planning) return;

    // 2. ap check
    if (playerData.Value.ActionPoints <= 0) {
			Debug.Log("Not enough AP!");
			return;
    }

    // 3. hand size check
    if (playerData.Value.CardsInHandCount >= 5) {
			Debug.Log("Hand full!");
			return;
    }

    // execute (false means it's not a free draw)
    ExecuteDrawServer(isFree: false);
	}

	[ServerRpc]
	private void SetReadyServerRpc(bool readyStatus) {
		PlayerData data = playerData.Value;
		
		data.IsReady = readyStatus;
		playerData.Value = data;

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

			Debug.Log($"Server: Removed card {cardId} from Player {OwnerClientId}'s hand");
		}
	}

	[ServerRpc]
	public void PlayCardToSlotServerRpc(int cardId, int slotIndex, ServerRpcParams serverRpcParams = default) {
		if (!IsServer) return;

		Debug.Log($"PLAYER NETWORK || ServerRpc received: Card {cardId} to slot {slotIndex}");

		if (GameManager.Instance.CurrentPhase.Value != GameManager.GamePhase.Planning) {
			Debug.Log("PLAYER NETWORK || Cannot play card - not in Planning phase");
			return;
		}

		PlayerData data = playerData.Value;
		string handCards = "";
    for (int i = 0; i < data.HandCardIds.Length; i++)
    {
        handCards += data.HandCardIds[i] + ", ";
    }
    Debug.Log($"PLAYER NETWORK || Current hand: [{handCards}]");

		if (!data.HandCardIds.Contains(cardId)) {
			Debug.Log($"PLAYER NETWORK || Card {cardId} not in player's hand");
			return;
		}

		/*
		CardLibrary library = CardManager.Instance?.GetCardLibrary();
		if (library != null) {
			CardData cardData = library.GetCardByID(cardId);
			if (cardData != null) {
				// check mana cost
				if (data.Mana < cardData.manaCost) {
					Debug.Log($"PLAYER NETWORK || Not enough mana. Need {cardData.manaCost}, have {data.Mana}");
					return;
				}

				// subtract mana
				data.Mana -= cardData.manaCost;
			}
		}
		*/
		
		// validate slot index
		if (slotIndex < 0 || slotIndex >= 3) {
			Debug.Log($"PLAYER NETWORK || Invalid slot index: {slotIndex}");
			return;
		}

		// remove card from hand
		data.HandCardIds.Remove(cardId);
		data.UpdateHandCount();

		// ensure BoardCardIds has 3 slots (initialize with -1 for empty)
		while (data.BoardCardIds.Length < 3) {
			data.BoardCardIds.Add(-1);
		}

		// if slot already has a card, return that card to hand
		if (data.BoardCardIds[slotIndex] != -1) {
			int replacedCardId = data.BoardCardIds[slotIndex];
			data.HandCardIds.Add(replacedCardId);
			data.UpdateHandCount();
			Debug.Log($"PLAYER NETWORK || Swapped card {replacedCardId} back to hand");

			ClientRpcParams playerParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        };
        ReturnCardToHandClientRpc(replacedCardId, playerParams);
		}

		// place new card in slot
		data.BoardCardIds[slotIndex] = cardId;
		playerData.Value = data;

		Debug.Log($"PLAYER NETWORK || Player {OwnerClientId} played card {cardId} to slot {slotIndex}");

		// notify opponent to show card on their board
		ulong opponentId = GetOpponentId(OwnerClientId);
    if (opponentId != OwnerClientId)
    {
        ClientRpcParams opponentParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { opponentId }
            }
        };
        NotifyOpponentCardPlayedClientRpc(cardId, slotIndex, opponentParams);
    }
	}

	[ClientRpc]
	private void ReturnCardToHandClientRpc(int cardId, ClientRpcParams clientRpcParams = default)
	{
			// find the card on the board and return it to hand
			if (BoardManager.Instance != null)
			{
					for (int i = 0; i < 3; i++)
					{
							GameObject card = BoardManager.Instance.GetCardInSlot(i, true);
							if (card != null)
							{
									CardVisual visual = card.GetComponent<CardVisual>();
									if (visual != null && visual.CardID == cardId)
									{
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
		// only process for opponent (not the player who played it)
		if (IsOwner) return;

		if (BoardManager.Instance != null) {
			BoardManager.Instance.PlaceOpponentCard(cardId, slotIndex);
		}

		Debug.Log($"PLAYER NETWORK || Opponent played card {cardId} to slot {slotIndex}");
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
		/*
		if (cardId >= 60 && cardId <= 65 ) {
			Debug.Log("Drew modifier card");
		} else {
			Debug.Log($"Drew spell ID: {cardId}");
		}
		*/

		// MOVED TO CardManager
		/*
		if (playerHandZone == null) {
			GameObject pZone = GameObject.Find("PlayerHandZone");
			if (pZone != null) playerHandZone = pZone.transform;
    }
    if (opponentHandZone == null) {
			GameObject oZone = GameObject.Find("OpponentHandZone");
			if (oZone != null) opponentHandZone = oZone.transform;
    }

		Transform targetZone = isMyCard ? playerHandZone : opponentHandZone;
		if (targetZone == null) {
			targetZone = GameObject.Find(isMyCard ? "PlayerHandZone" : "OpponentHandZone").transform;
		}

		GameObject newCard = Instantiate(cardPrefab, targetZone);
		newCard.transform.SetParent(targetZone, false);

		SpriteRenderer sr = newCard.GetComponent<SpriteRenderer>();
    	if (sr == null) sr = newCard.GetComponentInChildren<SpriteRenderer>();
		
		if (isMyCard) {
			newCard.GetComponent<CardVisual>().Initialize(cardId);
		} else {
			// Opponent's card: set to a hidden card visual
			newCard.GetComponent<SpriteRenderer>().color = Color.red;
		}
		*/

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

	// private void AssignHandZones() {
	// 	if (playerHandZone == null) {
	// 		GameObject pZone = GameObject.Find("PlayerHandZone");
	// 		if (pZone != null) playerHandZone = pZone.transform;
	// 	}

	// 	if (opponentHandZone == null) {
	// 		GameObject oZone = GameObject.Find("OpponentHandZone");
	// 		if (oZone != null) opponentHandZone = oZone.transform;
	// 	}
	// }
}
