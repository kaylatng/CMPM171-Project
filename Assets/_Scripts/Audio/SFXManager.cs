using UnityEngine;

public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance;

    [SerializeField] private AudioSource sfxSource;

[Header("Clips")]
[SerializeField] private AudioClip cardPlaceClip;
[SerializeField] private AudioClip cardUpgradeClip;
[SerializeField] private AudioClip cardAttackClip;
[SerializeField] private AudioClip buttonClickClip;
[SerializeField] private AudioClip noApBuzzerClip;

    private void Awake()
    {
        if (Instance == null)
        {
         Instance = this;
         DontDestroyOnLoad(gameObject);
         Debug.Log("SFXManager created");  
        } 
        else
        {
            Destroy(gameObject);
            return;
        }
    
        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
    }

    private void Play(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip);
    }

    public void PlayCardPlace()
    {
        Debug.Log("SFX, place card called ");
        Play(cardPlaceClip);
    }
    public void PlayCardUpgrade() => Play(cardUpgradeClip);
    public void PlayCardAttack() => Play(cardAttackClip);
    public void PlayButtonClick() => Play(buttonClickClip);
    public void PlayNoApBuzzer() => Play(noApBuzzerClip);
}