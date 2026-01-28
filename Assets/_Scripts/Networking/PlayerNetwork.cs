using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerNetwork : NetworkBehaviour {

	[SerializeField] private GameObject cardPrefab;
	private Transform playerHandZone;
	private Transform opponentHandZone;


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

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
			serializer.SerializeValue(ref Health);
			serializer.SerializeValue(ref Mana);
			serializer.SerializeValue(ref ActionPoints);
			serializer.SerializeValue(ref IsReady);
			serializer.SerializeValue(ref CardsInHandCount);
			serializer.SerializeValue(ref PlayerName);
		}
	}

	public override void OnNetworkSpawn() {
		
		GameObject pZone = GameObject.Find("PlayerHandZone");
		GameObject oZone = GameObject.Find("OpponentHandZone");

		if (pZone != null) playerHandZone = pZone.transform;
		if (oZone != null) opponentHandZone = oZone.transform;

		AssignHandZones();
		
		playerData.OnValueChanged += (PlayerData previousValue, PlayerData newValue) => {
			Debug.Log(OwnerClientId + "; " + newValue.Health + "; " + newValue.IsReady + "; " + newValue.PlayerName + "; Cards in hand: " + newValue.CardsInHandCount);
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
		data.CardsInHandCount++;
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
	private void RequestCardDrawServerRpc(ServerRpcParams serverRpcParams = default) {
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

	[ClientRpc]
	private void NotifyPlayerPokedClientRpc(ulong playerId) {
		if (playerId == NetworkManager.Singleton.LocalClientId) {
			Debug.Log("T pressed, server acknowledged");
		} else {
			Debug.Log($"playerId: {playerId} pressed T");
		}
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
			// FIX THIS LINE
			newCard.GetComponent<CardVisual>().Initialize(cardId);
		} else {
			// Opponent's card: set to a hidden card visual
			newCard.GetComponent<SpriteRenderer>().color = Color.red;
		}
	}

	private ulong GetOpponentId(ulong drawerId) {
		foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds) {
			if (clientId != drawerId) return clientId;
		}
		return drawerId;
	}

	private void AssignHandZones() {
		if (playerHandZone == null) {
			GameObject pZone = GameObject.Find("PlayerHandZone");
			if (pZone != null) playerHandZone = pZone.transform;
		}

		if (opponentHandZone == null) {
			GameObject oZone = GameObject.Find("OpponentHandZone");
			if (oZone != null) opponentHandZone = oZone.transform;
		}
	}
}
