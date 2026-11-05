using UnityEngine;

namespace CafeSim
{
    [RequireComponent(typeof(Light))]
    public sealed class CafeDaylight : MonoBehaviour
    {
        [SerializeField] private CafeBusinessDay businessDay;

        private static readonly int SkyTint = Shader.PropertyToID("_SkyTint");
        private static readonly int Exposure = Shader.PropertyToID("_Exposure");
        private static readonly Color Morning = new(1f, .91f, .79f);
        private static readonly Color Noon = new(1f, .97f, .89f);
        private static readonly Color GoldenHour = new(1f, .73f, .45f);
        private static readonly Color Sunset = new(1f, .51f, .27f);
        private static readonly Color SunsetSky = new(.81f, .57f, .43f);

        private Light sun;
        private Material originalSky, sky;
        private Color originalTint;
        private float originalExposure, lastClock = -1f;

        private void Awake()
        {
            sun = GetComponent<Light>();
            originalSky = RenderSettings.skybox;
            if (originalSky != null && originalSky.HasProperty(SkyTint) && originalSky.HasProperty(Exposure))
            {
                sky = new Material(originalSky);
                originalTint = sky.GetColor(SkyTint);
                originalExposure = sky.GetFloat(Exposure);
                RenderSettings.skybox = sky;
            }
        }

        private void LateUpdate()
        {
            if (businessDay == null || businessDay.Clock == lastClock) return;
            lastClock = businessDay.Clock;
            float hour = lastClock / 3600f;
            float morning = Mathf.InverseLerp(9f, 12f, hour);
            float afternoon = Mathf.InverseLerp(12f, 17f, hour);
            float sunset = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(17f, 18f, hour));

            // The glass faces west (-X). The late rays travel east and up the street,
            // clearing the opposite roof and reaching through the upper window panes.
            float elevation = hour < 12f ? Mathf.Lerp(34f, 60f, morning)
                : hour < 17f ? Mathf.Lerp(60f, 24f, afternoon)
                : Mathf.Lerp(24f, 17f, sunset);
            float heading = hour < 17f ? Mathf.Lerp(208f, 65f, Mathf.InverseLerp(9f, 17f, hour))
                : Mathf.Lerp(65f, 52f, sunset);
            sun.transform.rotation = Quaternion.Euler(elevation, heading, 0f);

            sun.color = hour < 12f ? Color.Lerp(Morning, Noon, morning)
                : hour < 17f ? Color.Lerp(Noon, GoldenHour, afternoon * afternoon)
                : Color.Lerp(GoldenHour, Sunset, sunset);
            sun.intensity = hour < 12f ? Mathf.Lerp(1.45f, 1.9f, morning)
                : hour < 17f ? Mathf.Lerp(1.9f, 1.75f, afternoon)
                : Mathf.Lerp(1.75f, 1.3f, sunset);

            if (sky != null)
            {
                float warmth = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(16.5f, 18f, hour));
                sky.SetColor(SkyTint, Color.Lerp(originalTint, SunsetSky, warmth));
                sky.SetFloat(Exposure, originalExposure * Mathf.Lerp(1f, .76f, warmth));
            }
        }

        private void OnDestroy()
        {
            if (sky == null) return;
            if (RenderSettings.skybox == sky) RenderSettings.skybox = originalSky;
            Destroy(sky);
        }
    }
}
