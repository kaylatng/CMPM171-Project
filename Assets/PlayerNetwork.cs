using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

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
		playerData.OnValueChanged += (PlayerData previousValue, PlayerData newValue) => {
			Debug.Log(OwnerClientId + "; " + newValue.Health + "; " + newValue.IsReady + "; " + newValue.PlayerName + "; Cards in hand: " + newValue.CardsInHandCount);
		};
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
		int drawnCardId = DeckManager.Instance.DrawCard();

		if (drawnCardId == -1) {
			Debug.Log("Deck is empty");
			return;
		}

		PlayerData data = playerData.Value;
		data.CardsInHandCount++;
		playerData.Value = data;

		ClientRpcParams targetedParams = new ClientRpcParams {
			Send = new ClientRpcSendParams {
				TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
			}
		};
		ReceiveCardClientRpc(drawnCardId, targetedParams);
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
	private void ReceiveCardClientRpc(int cardId, ClientRpcParams clientRpcParams = default) {
		if (cardId >= 60 && cardId <= 65 ) {
			Debug.Log("Drew modifier card");
		} else {
			Debug.Log($"Drew spell ID: {cardId}");
		}
	}
}
