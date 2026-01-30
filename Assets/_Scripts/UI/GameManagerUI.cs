using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;

public class GameManagerUI : MonoBehaviour {
	public static GameManagerUI Instance;

	[Header("UI Elements")]
	[SerializeField] private Button readyBtn;
	[SerializeField] private TextMeshProUGUI readyBtnText;
	[SerializeField] private TextMeshProUGUI phaseText;
	
	[Header("Resource Display")]
	[SerializeField] private TextMeshProUGUI apText;
	[SerializeField] private TextMeshProUGUI manaText;
	[SerializeField] private TextMeshProUGUI healthText;
	
	[Header("Opponent Status")]
	[SerializeField] private TextMeshProUGUI opponentStatusText;
	[SerializeField] private GameObject opponentReadyIndicator;

	[Header("Round Display")]
	[SerializeField] private TextMeshProUGUI roundText;

	private PlayerNetwork localPlayer;

	private void Awake() {
		if (Instance == null) Instance = this;
		else Destroy(gameObject);
	}

	private void Start() {
		if (readyBtn != null) {
			readyBtn.onClick.AddListener(OnReadyButtonClicked);
		}

		// subscribe to game manager phase changes
		if (GameManager.Instance != null) {
			GameManager.Instance.CurrentPhase.OnValueChanged += OnPhaseChanged;
			GameManager.Instance.CurrentRound.OnValueChanged += OnRoundChanged;
			UpdatePhaseUI(GameManager.Instance.CurrentPhase.Value);
			UpdateRoundUI(GameManager.Instance.CurrentRound.Value);
		}

		// initialize UI
		UpdateReadyButton(false);
		if (opponentReadyIndicator != null) {
			opponentReadyIndicator.SetActive(false);
		}
	}
	
	private void Update() {
		// find local player if we haven't yet
		if (localPlayer == null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient) {
			var localClient = NetworkManager.Singleton.LocalClient;
			if (localClient != null && localClient.PlayerObject != null) {
				localPlayer = localClient.PlayerObject.GetComponent<PlayerNetwork>();
				if (localPlayer != null) {
					Debug.Log("GAME MANAGER UI || Found local player");
					UpdateResourceUI(
						localPlayer.GetCurrentActionPoints(),
						localPlayer.GetCurrentMana(),
						localPlayer.GetCurrentHealth()
					);
				}
			}
		}

		// check opponent ready status
		CheckOpponentStatus();
	}

	private void OnReadyButtonClicked() {
		if (localPlayer != null) {
			// check if we can ready up
			if (GameManager.Instance == null || !GameManager.Instance.CanPlayCards()) {
				Debug.Log("GAME MANAGER UI || Cannot ready - not in Planning phase");
				return;
			}

			localPlayer.FinishTurn();
			UpdateReadyButton(true);
		}
	}

	private void UpdateReadyButton(bool isReady) {
		if (readyBtn == null) return;

		if (isReady) {
			readyBtn.interactable = false;
			if (readyBtnText != null) {
				readyBtnText.text = "READY ✓";
			}
		} else {
			readyBtn.interactable = true;
			if (readyBtnText != null) {
				readyBtnText.text = "READY";
			}
		}
	}

	private void OnPhaseChanged(GameManager.GamePhase oldPhase, GameManager.GamePhase newPhase) {
		UpdatePhaseUI(newPhase);

		// re-enable ready button when entering planning phase
		if (newPhase == GameManager.GamePhase.Planning) {
			UpdateReadyButton(false);
			
			if (opponentReadyIndicator != null) {
				opponentReadyIndicator.SetActive(false);
			}
		}

		// disable ready button during other phases
		if (newPhase != GameManager.GamePhase.Planning) {
			if (readyBtn != null) {
				readyBtn.interactable = false;
			}
		}
	}

	private void OnRoundChanged(int oldRound, int newRound) {
		UpdateRoundUI(newRound);
	}

	private void UpdatePhaseUI(GameManager.GamePhase phase) {
		if (phaseText == null) return;

		switch (phase) {
			case GameManager.GamePhase.ResourceGain:
				phaseText.text = "Phase: Resource Gain";
				phaseText.color = Color.cyan;
				break;
			case GameManager.GamePhase.Planning:
				phaseText.text = "Phase: Planning";
				phaseText.color = Color.green;
				break;
			case GameManager.GamePhase.Reveal:
				phaseText.text = "Phase: Reveal";
				phaseText.color = Color.yellow;
				break;
			case GameManager.GamePhase.Cleanup:
				phaseText.text = "Phase: Cleanup";
				phaseText.color = Color.white;
				break;
		}
	}

	private void UpdateRoundUI(int round) {
		if (roundText != null) {
			roundText.text = $"Round: {round}";
		}
	}

	private void CheckOpponentStatus() {
		if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient) return;

		// find opponent player
		foreach (var client in NetworkManager.Singleton.ConnectedClientsList) {
			if (client.ClientId != NetworkManager.Singleton.LocalClientId) {
				if (client.PlayerObject != null && client.PlayerObject.TryGetComponent<PlayerNetwork>(out var opponentPlayer)) {
					bool opponentReady = opponentPlayer.IsPlayerReady();
					
					if (opponentReadyIndicator != null) {
						opponentReadyIndicator.SetActive(opponentReady);
					}

					if (opponentStatusText != null) {
						opponentStatusText.text = opponentReady ? "Opponent: Ready ✓" : "Opponent: Planning...";
					}
					return;
				}
			}
		}

		// no opponent found
		if (opponentStatusText != null) {
			opponentStatusText.text = "Opponent: Not Connected";
		}
	}

	public void UpdateResourceUI(int ap, int mana, int health) {
		if (apText != null) {
			apText.text = $"AP: {ap}/5";
			
			// color code AP display
			if (ap <= 0) {
				apText.color = Color.red;
			} else if (ap <= 2) {
				apText.color = Color.yellow;
			} else {
				apText.color = Color.white;
			}
		}

		if (manaText != null) {
			manaText.text = $"Mana: {mana}";
		}

		if (healthText != null) {
			healthText.text = $"HP: {health}/20";
			
			// color code health display
			if (health <= 5) {
				healthText.color = Color.red;
			} else if (health <= 10) {
				healthText.color = Color.yellow;
			} else {
				healthText.color = Color.white;
			}
		}
	}

	private void OnDestroy() {
		if (GameManager.Instance != null) {
			GameManager.Instance.CurrentPhase.OnValueChanged -= OnPhaseChanged;
			GameManager.Instance.CurrentRound.OnValueChanged -= OnRoundChanged;
		}
	}
}