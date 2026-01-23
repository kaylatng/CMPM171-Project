using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;

public class GameManagerUI : MonoBehaviour {
	[SerializeField]private Button readyBtn;
	[SerializeField]private TextMeshProUGUI apText;
	[SerializeField]private TextMeshProUGUI phaseText;

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
		if (localPlayer == null && NetworkManager.Singleton.IsClient) {
			var localClient = NetworkManager.Singleton.LocalClient;
			if (localClient != null && localClient.PlayerObject != null) {
				localPlayer = localClient.PlayerObject.GetComponent<PlayerNetwork>();
			}
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
	}

	public void UpdateResourceUI(int ap, int hp) {
		// apText.text = $"AP: {ap}/5";
	}
}
