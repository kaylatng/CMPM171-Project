using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
	[SerializeField] private float phasePanelEnterDuration = 0.35f;
	[SerializeField] private float phasePanelExitDuration = 0.35f;
	[SerializeField] private float phaseTextEnterDuration = 0.25f;
	[SerializeField] private float phaseTextExitDuration = 0.25f;
	[SerializeField, Tooltip("Extra distance beyond the screen edge (pixels).")]
	private float phasePanelOffscreenPadding = 60f;
	
	[Header("Debug (Phase Panel)")]
	[SerializeField] private bool enablePhasePanelDebugToggle = false;
#if ENABLE_INPUT_SYSTEM
	[SerializeField] private Key phasePanelDebugToggleKey = Key.Space;
#else
	[SerializeField] private KeyCode phasePanelDebugToggleKey = KeyCode.Space;
#endif
	[SerializeField, Tooltip("How long the panel stays on-screen in the debug one-shot animation.")]
	private float phasePanelDebugHoldSeconds = 0.75f;
	private Coroutine phasePanelDebugRoutine;

	private Coroutine phasePanelAutoHideRoutine;
	private Coroutine phasePanelTweenRoutine;
	private Coroutine phaseTextTweenRoutine;
	private RectTransform phasePanelRect;
	private RectTransform phaseCanvasRect;
	private Vector2 phasePanelRestAnchoredPos;
	private bool phasePanelRestPosCached;
	
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
			CachePhasePanelRectsIfNeeded();
			// Ensure it starts hidden; UpdatePhaseUI will animate it in.
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

		if (enablePhasePanelDebugToggle)
		{
#if ENABLE_INPUT_SYSTEM
			var kb = Keyboard.current;
			if (kb != null && kb[phasePanelDebugToggleKey].wasPressedThisFrame)
				PlayPhasePanelDebugOnce();
#else
			if (Input.GetKeyDown(phasePanelDebugToggleKey))
				PlayPhasePanelDebugOnce();
#endif
		}
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
			PlayPhasePanelEnter();
		}

		if (phasePanelAutoHideRoutine != null)
		{
			StopCoroutine(phasePanelAutoHideRoutine);
			phasePanelAutoHideRoutine = null;
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
			phasePanelAutoHideRoutine = StartCoroutine(HidePhasePanelAfterDelay(phasePanelAutoHideSeconds));
		}
	}

	private System.Collections.IEnumerator HidePhasePanelAfterDelay(float seconds)
	{
		yield return new WaitForSeconds(seconds);
		PlayPhasePanelExit();
		phasePanelAutoHideRoutine = null;
	}
	
	private void PlayPhasePanelDebugOnce()
	{
		if (phasePanel == null) return;
		if (phasePanelDebugRoutine != null)
		{
			StopCoroutine(phasePanelDebugRoutine);
			phasePanelDebugRoutine = null;
		}

		if (phasePanelAutoHideRoutine != null)
		{
			StopCoroutine(phasePanelAutoHideRoutine);
			phasePanelAutoHideRoutine = null;
		}

		// Ensure we have something visible on the panel in debug mode.
		if (phasePanelText != null)
		{
			phasePanelText.text = "Phase";
			phasePanelText.color = Color.white;
		}
		else if (phaseText != null)
		{
			phaseText.text = "Phase";
			phaseText.color = Color.white;
		}
		
		phasePanelDebugRoutine = StartCoroutine(PlayPhasePanelDebugSequence());
	}

	private System.Collections.IEnumerator PlayPhasePanelDebugSequence()
	{
		PlayPhasePanelEnter();
		yield return new WaitForSeconds(Mathf.Max(0.01f, phasePanelEnterDuration));
		yield return new WaitForSeconds(Mathf.Max(0f, phasePanelDebugHoldSeconds));
		PlayPhasePanelExit();
		yield return new WaitForSeconds(Mathf.Max(0.01f, phasePanelExitDuration));
		phasePanelDebugRoutine = null;
	}

	private void CachePhasePanelRectsIfNeeded()
	{
		if (phasePanel == null) return;
		if (phasePanelRect == null)
			phasePanelRect = phasePanel.GetComponent<RectTransform>();
		if (phasePanelRect == null) return;

		if (!phasePanelRestPosCached)
		{
			phasePanelRestAnchoredPos = phasePanelRect.anchoredPosition;
			phasePanelRestPosCached = true;
		}

		if (phaseCanvasRect == null)
		{
			var canvas = phasePanelRect.GetComponentInParent<Canvas>();
			if (canvas != null)
				phaseCanvasRect = canvas.transform as RectTransform;
		}
	}

	private float GetCanvasWidth()
	{
		if (phaseCanvasRect != null)
			return phaseCanvasRect.rect.width;
		return Screen.width;
	}

	private Vector2 GetOffscreenLeftPos()
	{
		CachePhasePanelRectsIfNeeded();
		if (phasePanelRect == null) return Vector2.zero;
		float canvasWidth = GetCanvasWidth();
		float panelWidth = phasePanelRect.rect.width;
		float offset = (canvasWidth * 0.5f) + (panelWidth * 0.5f) + phasePanelOffscreenPadding;
		return new Vector2(phasePanelRestAnchoredPos.x - offset, phasePanelRestAnchoredPos.y);
	}

	private Vector2 GetOffscreenRightPos()
	{
		CachePhasePanelRectsIfNeeded();
		if (phasePanelRect == null) return Vector2.zero;
		float canvasWidth = GetCanvasWidth();
		float panelWidth = phasePanelRect.rect.width;
		float offset = (canvasWidth * 0.5f) + (panelWidth * 0.5f) + phasePanelOffscreenPadding;
		return new Vector2(phasePanelRestAnchoredPos.x + offset, phasePanelRestAnchoredPos.y);
	}

	private void PlayPhasePanelEnter()
	{
		if (phasePanel == null) return;
		CachePhasePanelRectsIfNeeded();
		if (phasePanelRect == null) return;

		phasePanel.SetActive(true);

		if (phasePanelTweenRoutine != null)
			StopCoroutine(phasePanelTweenRoutine);
		if (phaseTextTweenRoutine != null)
			StopCoroutine(phaseTextTweenRoutine);

		phasePanelRect.anchoredPosition = GetOffscreenLeftPos();
		phasePanelTweenRoutine = StartCoroutine(TweenAnchoredPos(
			phasePanelRect,
			GetOffscreenLeftPos(),
			phasePanelRestAnchoredPos,
			Mathf.Max(0.01f, phasePanelEnterDuration),
			EaseOutCubic,
			onComplete: () => phasePanelTweenRoutine = null
		));

		if (phasePanelText != null)
		{
			var rt = phasePanelText.rectTransform;
			rt.localScale = new Vector3(1f, 1f, 1f);
			phaseTextTweenRoutine = StartCoroutine(TweenScale(
				rt,
				new Vector3(4f, 0.3f, 1f),
				Vector3.one,
				Mathf.Max(0.01f, phaseTextEnterDuration),
				EaseOutCubic,
				onComplete: () => phaseTextTweenRoutine = null
			));
		}
	}

	private void PlayPhasePanelExit()
	{
		if (phasePanel == null) return;
		CachePhasePanelRectsIfNeeded();
		if (phasePanelRect == null) return;

		if (phasePanelTweenRoutine != null)
			StopCoroutine(phasePanelTweenRoutine);
		if (phaseTextTweenRoutine != null)
			StopCoroutine(phaseTextTweenRoutine);

		Vector2 startPos = phasePanelRect.anchoredPosition;
		Vector2 endPos = GetOffscreenRightPos();

		phasePanelTweenRoutine = StartCoroutine(TweenAnchoredPos(
			phasePanelRect,
			startPos,
			endPos,
			Mathf.Max(0.01f, phasePanelExitDuration),
			EaseInCubic,
			onComplete: () =>
			{
				phasePanelTweenRoutine = null;
				if (phasePanel != null)
					phasePanel.SetActive(false);
			}
		));

		if (phasePanelText != null)
		{
			var rt = phasePanelText.rectTransform;
			Vector3 startScale = rt.localScale;
			// Exit: stretch wide and squash short.
			Vector3 endScale = new Vector3(4f, 0.3f, 1f);
			phaseTextTweenRoutine = StartCoroutine(TweenScale(
				rt,
				startScale,
				endScale,
				Mathf.Max(0.01f, phaseTextExitDuration),
				EaseInCubic,
				onComplete: () => phaseTextTweenRoutine = null
			));
		}
	}

	private static System.Collections.IEnumerator TweenAnchoredPos(
		RectTransform rect,
		Vector2 from,
		Vector2 to,
		float duration,
		System.Func<float, float> easing,
		System.Action onComplete)
	{
		if (rect == null) yield break;
		float t = 0f;
		while (t < duration)
		{
			t += Time.deltaTime;
			float u = Mathf.Clamp01(t / duration);
			float e = easing != null ? easing(u) : u;
			rect.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
			yield return null;
		}
		rect.anchoredPosition = to;
		onComplete?.Invoke();
	}

	private static System.Collections.IEnumerator TweenScale(
		Transform tr,
		Vector3 from,
		Vector3 to,
		float duration,
		System.Func<float, float> easing,
		System.Action onComplete)
	{
		if (tr == null) yield break;
		float t = 0f;
		while (t < duration)
		{
			t += Time.deltaTime;
			float u = Mathf.Clamp01(t / duration);
			float e = easing != null ? easing(u) : u;
			tr.localScale = Vector3.LerpUnclamped(from, to, e);
			yield return null;
		}
		tr.localScale = to;
		onComplete?.Invoke();
	}

	private static float EaseOutCubic(float t)
	{
		t = Mathf.Clamp01(t);
		float p = 1f - t;
		return 1f - (p * p * p);
	}

	private static float EaseInCubic(float t)
	{
		t = Mathf.Clamp01(t);
		return t * t * t;
	}

	// A gentle overshoot for the "squash to normal" text entrance.
	private static float EaseOutBack(float t)
	{
		t = Mathf.Clamp01(t);
		const float c1 = 1.70158f;
		const float c3 = c1 + 1f;
		float p = t - 1f;
		return 1f + c3 * (p * p * p) + c1 * (p * p);
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
		// Pending attacks now only cost AP; mana is not required.
		UpdateResourceUI(ap - pending, mana, health);
	}

	/// <summary>Called when server pushes new player data; applies pending attack deduction so display matches intent.</summary>
	public void OnServerResourceUpdate(int ap, int mana, int health)
	{
		int pending = GetPendingAttackDeduction();
		// Pending attacks now only cost AP; mana is not required.
		UpdateResourceUI(ap - pending, mana, health);
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
