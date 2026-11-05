using UnityEngine;

[DisallowMultipleComponent]
public sealed class PorchSwingMotion : MonoBehaviour
{
    [SerializeField, Range(0f, 8f)] private float swayDegrees = 2.4f;
    [SerializeField, Min(0.1f)] private float periodSeconds = 5.2f;

    private Quaternion restRotation;
    private float startTime;

    private void OnEnable()
    {
        restRotation = transform.localRotation;
        startTime = Time.time;
    }

    private void Update()
    {
        float phase = (Time.time - startTime) * (2f * Mathf.PI / periodSeconds);
        transform.localRotation = restRotation * Quaternion.AngleAxis(swayDegrees * Mathf.Sin(phase), Vector3.right);
    }

    private void OnDisable()
    {
        transform.localRotation = restRotation;
    }
}
