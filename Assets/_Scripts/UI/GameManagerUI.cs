using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;

public class GameManagerUI : MonoBehaviour
{
	public static GameManagerUI Instance;

	[Header("UI Elements")]
	[SerializeField] private Button readyBtn;
	[SerializeField] private TextMeshProUGUI readyBtnText;
	[SerializeField] private TextMeshProUGUI phaseText;

	[Header("Phase Panel")]
	[SerializeField] private GameObject phasePanel;
	[SerializeField] private TextMeshProUGUI phasePanelText;
	[SerializeField, Tooltip("If > 0, phase panel will auto-hide after this many seconds.")]
	private float phasePanelAutoHideSeconds = 0f;
	private Coroutine phasePanelHideRoutine;
	
	[Header("Resource Display")]
	[SerializeField] private TextMeshProUGUI apText;
	[SerializeField] private TextMeshProUGUI manaText;
	[SerializeField] private TextMeshProUGUI hpText;
	
	[Header("Opponent Status")]
	[SerializeField] private TextMeshProUGUI opponentStatusText;
	[SerializeField] private GameObject opponentReadyIndicator;
	[SerializeField] private TextMeshProUGUI opponentHpText;

	[Header("Round Display")]
	[SerializeField] private TextMeshProUGUI roundText;

	[Header("Reset")]
	[SerializeField] private Button resetBtn;

	[Header("Game Over")]
	[SerializeField] private GameObject gameOverPanel;
	[SerializeField] private TextMeshProUGUI gameOverText;

	private PlayerNetwork localPlayer;

	private void Awake()
	{
		if (Instance == null) Instance = this;
		else Destroy(gameObject);
	}

	private void Start()
	{
		if (readyBtn != null)
		{
			readyBtn.onClick.AddListener(OnReadyButtonClicked);
		}
		if (resetBtn != null)
		{
			resetBtn.onClick.AddListener(OnResetButtonClicked);
		}

		// subscribe to game manager phase changes
		if (GameManager.Instance != null)
		{
			GameManager.Instance.CurrentPhase.OnValueChanged += OnPhaseChanged;
			GameManager.Instance.CurrentRound.OnValueChanged += OnRoundChanged;
			UpdatePhaseUI(GameManager.Instance.CurrentPhase.Value);
			UpdateRoundUI(GameManager.Instance.CurrentRound.Value);
		}

		// initialize UI
		UpdateReadyButton(false);
		if (phasePanel != null)
		{
			// Ensure it starts visible only if we have a phase to show; UpdatePhaseUI will toggle.
			phasePanel.SetActive(false);
		}
		if (opponentReadyIndicator != null)
		{
			opponentReadyIndicator.SetActive(false);
		}
		if (gameOverPanel != null)
		{
			gameOverPanel.SetActive(false);
		}
	}
	
	private void Update()
	{
		// find local player if we haven't yet, and update resource display (with pending attack deduction in Planning)
		if (localPlayer == null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient) {
			var localClient = NetworkManager.Singleton.LocalClient;
			if (localClient != null && localClient.PlayerObject != null)
			{
				localPlayer = localClient.PlayerObject.GetComponent<PlayerNetwork>();
				if (localPlayer != null)
				{
					Debug.Log("GAME MANAGER UI || Found local player");
					ApplyResourceDisplay(localPlayer);
				}
			}
		}
		else if (localPlayer != null)
		{
			ApplyResourceDisplay(localPlayer);
		}

		// check opponent ready status
		CheckOpponentStatus();
	}

	private void OnResetButtonClicked()
	{
		if (GameManager.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
		{
			GameManager.Instance.RequestResetGameServerRpc();
		}
	}

	private void OnReadyButtonClicked()
	{
		if (localPlayer != null) {
			// check if we can ready up
			if (GameManager.Instance == null || !GameManager.Instance.CanPlayCards())
			{
				Debug.Log("GAME MANAGER UI || Cannot ready - not in Planning phase");
				return;
			}

			// Submit all toggled attack intents before marking ready (resources deducted on server when RPCs are processed)
			if (BoardManager.Instance != null)
				BoardManager.Instance.SubmitLocalAttackIntents();

			localPlayer.FinishTurn();
			UpdateReadyButton(true);
		}
	}

	private void UpdateReadyButton(bool isReady)
	{
		if (readyBtn == null) return;

		if (isReady) {
			readyBtn.interactable = false;
			if (readyBtnText != null)
			{
				readyBtnText.text = "READY ✓";
			}
		} else {
			readyBtn.interactable = true;
			if (readyBtnText != null)
			{
				readyBtnText.text = "READY";
			}
			if (localPlayer != null) {
    			localPlayer.OnPlayerDataChanged += HandlePlayerDataChanged;
    			HandlePlayerDataChanged(default); // initial draw
			}
		}
	}

	private void OnPhaseChanged(GameManager.GamePhase oldPhase, GameManager.GamePhase newPhase)
	{
		UpdatePhaseUI(newPhase);

		// re-enable ready button when entering planning phase
		if (newPhase == GameManager.GamePhase.Planning)
		{
			UpdateReadyButton(false);
			
			if (opponentReadyIndicator != null)
			{
				opponentReadyIndicator.SetActive(false);
			}
		}

		// disable ready button during other phases
		if (newPhase != GameManager.GamePhase.Planning)
		{
			if (readyBtn != null)
			{
				readyBtn.interactable = false;
			}
		}
	}

	private void OnRoundChanged(int oldRound, int newRound)
	{
		UpdateRoundUI(newRound);
	}

	private void UpdatePhaseUI(GameManager.GamePhase phase)
	{
		// Prefer the new panel text, but keep the old field working if still used in-scene.
		var targetText = phasePanelText != null ? phasePanelText : phaseText;
		if (targetText == null) return;

		if (phasePanel != null)
		{
			phasePanel.SetActive(true);
		}

		if (phasePanelHideRoutine != null)
		{
			StopCoroutine(phasePanelHideRoutine);
			phasePanelHideRoutine = null;
		}

		switch (phase)
		{
			case GameManager.GamePhase.ResourceGain:
				targetText.text = "Phase: Resource Gain";
				targetText.color = Color.cyan;
				break;
			case GameManager.GamePhase.Planning:
				targetText.text = "Phase: Planning";
				targetText.color = Color.green;
				break;
			case GameManager.GamePhase.Reveal:
				targetText.text = "Phase: Reveal";
				targetText.color = Color.yellow;
				break;
			case GameManager.GamePhase.Cleanup:
				targetText.text = "Phase: Cleanup";
				targetText.color = Color.white;
				break;
		}

		// If you're using the panel, optionally auto-hide it.
		if (phasePanel != null && phasePanelAutoHideSeconds > 0f)
		{
			phasePanelHideRoutine = StartCoroutine(HidePhasePanelAfterDelay(phasePanelAutoHideSeconds));
		}
	}

	private System.Collections.IEnumerator HidePhasePanelAfterDelay(float seconds)
	{
		yield return new WaitForSeconds(seconds);
		if (phasePanel != null)
			phasePanel.SetActive(false);
		phasePanelHideRoutine = null;
	}

	private void UpdateRoundUI(int round)
	{
		if (roundText != null) {
			roundText.text = $"Round: {round}";
		}
	}

	private void CheckOpponentStatus()
	{
		if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient) return;

		// find opponent player
		foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
		{
			if (client.ClientId != NetworkManager.Singleton.LocalClientId) {
				if (client.PlayerObject != null && client.PlayerObject.TryGetComponent<PlayerNetwork>(out var opponentPlayer)) {
					bool opponentReady = opponentPlayer.IsPlayerReady();
					
					if (opponentReadyIndicator != null)
					{
						opponentReadyIndicator.SetActive(opponentReady);
					}

					if (opponentStatusText != null)
					{
						opponentStatusText.text = opponentReady ? "Opponent: Ready ✓" : "Opponent: Planning...";
					}

					// Update opponent HP display
					if (opponentHpText != null)
					{
						int oppHp = opponentPlayer.GetCurrentHealth();
						opponentHpText.text = $"Opponent HP: {oppHp}/20";
						if (oppHp <= 5)
							opponentHpText.color = Color.red;
						else if (oppHp <= 10)
							opponentHpText.color = Color.yellow;
						else
							opponentHpText.color = Color.white;
					}
					return;
				}
			}
		}

		// no opponent found
		if (opponentStatusText != null)
		{
			opponentStatusText.text = "Opponent: Not Connected";
		}
		if (opponentHpText != null)
		{
			opponentHpText.text = "Opponent HP: --";
			opponentHpText.color = Color.white;
		}
	}

	/// <summary>Apply resource display: in Planning phase, subtract pending attack count from AP/Mana for real-time feedback.</summary>
	private void ApplyResourceDisplay(PlayerNetwork player)
	{
		int ap = player.GetCurrentActionPoints();
		int mana = player.GetCurrentMana();
		int health = player.GetCurrentHealth();
		int pending = GetPendingAttackDeduction();
		UpdateResourceUI(ap - pending, mana - pending, health);
	}

	/// <summary>Called when server pushes new player data; applies pending attack deduction so display matches intent.</summary>
	public void OnServerResourceUpdate(int ap, int mana, int health)
	{
		int pending = GetPendingAttackDeduction();
		UpdateResourceUI(ap - pending, mana - pending, health);
	}

	private int GetPendingAttackDeduction()
	{
		if (GameManager.Instance == null || !GameManager.Instance.CanPlayCards() || BoardManager.Instance == null)
			return 0;
		return BoardManager.Instance.GetLocalPendingAttackCount();
	}

	public void UpdateResourceUI(int ap, int mana, int health)
	{
		if (apText != null)
		{
			apText.text = $"AP: {ap}/5";
			
			// color code AP display
			if (ap <= 0)
			{
				apText.color = Color.red;
			} else if (ap <= 2)
			{
				apText.color = Color.yellow;
			} else
			{
				apText.color = Color.white;
			}
		}

		if (manaText != null)
		{
			manaText.text = $"Mana: {mana}";
		}

		if (hpText != null) {
			hpText.text = $"HP: {health}/20";
			if (health <= 5)
				hpText.color = Color.red;
			else if (health <= 10)
				hpText.color = Color.yellow;
			else
				hpText.color = Color.white;
		}
	}

	private void OnDestroy()
	{
		if (GameManager.Instance != null)
		{
			GameManager.Instance.CurrentPhase.OnValueChanged -= OnPhaseChanged;
			GameManager.Instance.CurrentRound.OnValueChanged -= OnRoundChanged;
		}
		if (localPlayer != null)
    	localPlayer.OnPlayerDataChanged -= HandlePlayerDataChanged;

	}

	private void HandlePlayerDataChanged(PlayerNetwork.PlayerData data) {
		if (manaText != null) manaText.text = $"Mana: {localPlayer.GetMana()}";
		if (apText != null) apText.text = $"AP: {localPlayer.GetAP()}/5";
		if (hpText != null) hpText.text = $"HP: {localPlayer.GetHP()}";
	}

	public void ShowGameOver(bool isWin)
	{
		if (gameOverPanel != null)
		{
			gameOverPanel.SetActive(true);
		}
		if (gameOverText != null)
		{
			gameOverText.text = isWin ? "YOU WIN!" : "YOU LOSE";
			gameOverText.color = isWin ? Color.green : Color.red;
		}

		if (readyBtn != null)
		{
			readyBtn.interactable = false;
		}
	}

	public void HideGameOver()
	{
		if (gameOverPanel != null)
		{
			gameOverPanel.SetActive(false);
		}
	}


	//public void UpdateResourceUI(int ap, int hp) {
		// apText.text = $"AP: {ap}/5";
	//}
}
