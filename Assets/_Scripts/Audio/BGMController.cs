using UnityEngine;

public class BGMController : MonoBehaviour
{
    public AudioSource source;

    public AudioClip introClip;
    public AudioClip loopClip;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(PlayMusic());
    }

    System.Collections.IEnumerator PlayMusic()
    {
        // play intro
        source.clip = introClip;
        source.loop = false;
        source.Play();

        // wait until intro finishes
        yield return new WaitForSeconds(introClip.length);

        // play looping music
        source.clip = loopClip;
        source.loop = true;
        source.Play();
    }
}
