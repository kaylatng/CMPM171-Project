using UnityEngine;

/// <summary>
/// Global audio mute state. Persists across scenes and uses AudioListener to mute all game audio.
/// </summary>
public static class AudioMuteManager
{
    private const string PrefKey = "AudioMuted";

    /// <summary>True when all game audio is muted.</summary>
    public static bool IsMuted
    {
        get => PlayerPrefs.GetInt(PrefKey, 0) != 0;
        private set
        {
            PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
            PlayerPrefs.Save();
            ApplyMute();
        }
    }

    /// <summary>Apply current mute state to AudioListener. Call from any scene that has an AudioListener.</summary>
    public static void ApplyMute()
    {
        if (AudioListener.volume >= 0)
            AudioListener.volume = IsMuted ? 0f : 1f;
    }

    /// <summary>Toggle mute and return the new state (true = muted).</summary>
    public static bool ToggleMute()
    {
        IsMuted = !IsMuted;
        return IsMuted;
    }

    /// <summary>Set mute on or off.</summary>
    public static void SetMuted(bool muted)
    {
        IsMuted = muted;
    }
}
