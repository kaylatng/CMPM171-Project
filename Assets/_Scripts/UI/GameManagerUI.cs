using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;

public class GameManagerUI : MonoBehaviour {
	[SerializeField]private Button readyBtn;
	[SerializeField]private TextMeshProUGUI apText;
	[SerializeField]private TextMeshProUGUI phaseText;
	[SerializeField] private TextMeshProUGUI manaText;
	[SerializeField] private TextMeshProUGUI hpText;
	


	private PlayerNetwork localPlayer;

	private void Start() {
		readyBtn.onClick.AddListener(() => {
			if (localPlayer != null) {
				localPlayer.FinishTurn(); 
				readyBtn.interactable = false;
			}
		});

		if (GameManager.Instance != null) {
			GameManager.Instance.CurrentPhase.OnValueChanged += OnPhaseChanged;
			phaseText.text = $"Phase: {GameManager.Instance.CurrentPhase.Value}";
		}
	}
	
	private void Update() {
		if (localPlayer != null) return;
		if(NetworkManager.Singleton == null) return;
		if(!NetworkManager.Singleton.IsClient) return;

			var localClient = NetworkManager.Singleton.LocalClient;
			if (localClient != null && localClient.PlayerObject != null) {
				localPlayer = localClient.PlayerObject.GetComponent<PlayerNetwork>();
			}
			if (localPlayer != null) {
    			localPlayer.OnPlayerDataChanged += HandlePlayerDataChanged;
    			HandlePlayerDataChanged(default); // initial draw
			}

		}
		
	private void OnPhaseChanged(GameManager.GamePhase oldPhase, GameManager.GamePhase newPhase) {
		phaseText.text = $"Phase: {newPhase}";

		if (newPhase == GameManager.GamePhase.Planning) {
			readyBtn.interactable = true;
		}
	}

	private void OnDestroy() {
		if (GameManager.Instance != null) {
			GameManager.Instance.CurrentPhase.OnValueChanged -= OnPhaseChanged;
		}
		if (localPlayer != null)
    	localPlayer.OnPlayerDataChanged -= HandlePlayerDataChanged;

	}

	private void HandlePlayerDataChanged(PlayerNetwork.PlayerData data) {
    if (manaText != null) manaText.text = $"Mana: {localPlayer.GetMana()}";
    if (apText != null) apText.text = $"AP: {localPlayer.GetAP()}/5";
    if (hpText != null) hpText.text = $"HP: {localPlayer.GetHP()}";
}


	//public void UpdateResourceUI(int ap, int hp) {
		// apText.text = $"AP: {ap}/5";
	//}
}
