using UnityEngine;
using Unity.Netcode;
using UnityEngine.EventSystems;

public class DeckClickable : MonoBehaviour, IPointerClickHandler
{
	private SpriteRenderer sr;
	[SerializeField] private Color hoverColor = Color.gray;
	private Color originalColor;

	private void Start()
	{
		sr = GetComponent<SpriteRenderer>();
		originalColor = sr.color;
	}
	
	public void OnPointerClick(PointerEventData eventData)
	{
		if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient) return;
		if (NetworkManager.Singleton.LocalClient == null || NetworkManager.Singleton.LocalClient.PlayerObject == null) return;

		var player = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerNetwork>();
		if (player == null) return;

		// Once the player has clicked Ready, the deck should no longer respond to clicks this turn.
		if (player.IsPlayerReady())
		{
			Debug.Log("DECK CLICKABLE || Click blocked - player is Ready");
			return;
		}

		player.RequestCardDrawServerRpc();

		// Inform tutorial (if active) that the deck has been clicked.
		if (UITutorialController.Instance != null)
		{
			UITutorialController.Instance.NotifyDeckClicked();
		}
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		sr.color = hoverColor;
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		sr.color = originalColor;
	}
}