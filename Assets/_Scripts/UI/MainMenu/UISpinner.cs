using UnityEngine;

public class UiSpinner : MonoBehaviour
{
    [SerializeField] private float stepDegrees = 90f;      // how much to rotate each step
    [SerializeField] private float stepDelaySeconds = 0.15f; // delay between steps

    private float _timer;

    private void OnEnable()
    {
        _timer = 0f;
    }

    private void Update()
    {
        _timer += Time.unscaledDeltaTime;
        if (_timer < stepDelaySeconds)
            return;

        _timer = 0f;
        transform.Rotate(0f, 0f, -stepDegrees);
    }
}