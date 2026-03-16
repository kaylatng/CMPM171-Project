using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Button that toggles global audio mute and switches between audio-on and audio-muted sprite images.
/// Assign the two sprites in the Inspector (e.g. audio.png and audio_mute.png from _Art/UI).
/// </summary>
[RequireComponent(typeof(Button))]
public class AudioMuteButton : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite audioOnSprite;
    [SerializeField] private Sprite audioMutedSprite;

    [Header("Optional")]
    [Tooltip("If not set, uses Image on this GameObject.")]
    [SerializeField] private Image targetImage;

    private Button _button;
    private Image _image;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _image = targetImage != null ? targetImage : GetComponent<Image>();

        if (_image == null)
            Debug.LogWarning("AudioMuteButton: No Image found. Add an Image or assign Target Image.", this);
    }

    private void Start()
    {
        AudioMuteManager.ApplyMute();
        UpdateSprite();

        if (_button != null)
            _button.onClick.AddListener(OnClick);
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        AudioMuteManager.ToggleMute();
        UpdateSprite();
    }

    private void UpdateSprite()
    {
        if (_image == null) return;

        bool muted = AudioMuteManager.IsMuted;
        if (muted && audioMutedSprite != null)
            _image.sprite = audioMutedSprite;
        else if (!muted && audioOnSprite != null)
            _image.sprite = audioOnSprite;
    }
}
