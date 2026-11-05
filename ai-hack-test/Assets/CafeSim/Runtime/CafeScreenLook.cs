using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CafeSim
{
    // Turns on the full-screen cafe look for the scene this component lives in:
    //  - colour grade, vignette and grain: Hidden/CafeSim/ScreenLook, run by the Full Screen Pass feature
    //    on PC_Renderer (other scenes keep strength 0, where the pass leaves the image unchanged);
    //  - lamp/window glow: URP Bloom on glowVolume, which is smooth because it blurs over several resolutions.
    [ExecuteAlways]
    public sealed class CafeScreenLook : MonoBehaviour
    {
        [Range(0, 1)] public float strength = 1f;
        [Range(0, 1)] public float warmth = .55f;
        [Range(0, 2)] public float saturation = 1.08f;
        [Range(.5f, 1.5f)] public float contrast = 1.06f;
        [Tooltip("Bloom intensity on the glow volume.")]
        [Range(0, 3)] public float glow = 1.3f;
        [Tooltip("Brightness where the glow starts. Lower lets walls and tables lit by the lamps glow too.")]
        [Range(.3f, 1.5f)] public float glowThreshold = .7f;
        [Range(0, 1)] public float vignette = .55f;
        [Range(0, 1)] public float grain = .25f;
        [Tooltip("Global volume whose profile has a Bloom override (Cafe Look Volume).")]
        public Volume glowVolume;

        static readonly int Strength = Shader.PropertyToID("_CafeLookStrength");
        static readonly int Warmth = Shader.PropertyToID("_CafeLookWarmth");
        static readonly int Saturation = Shader.PropertyToID("_CafeLookSaturation");
        static readonly int Contrast = Shader.PropertyToID("_CafeLookContrast");
        static readonly int Vignette = Shader.PropertyToID("_CafeLookVignette");
        static readonly int Grain = Shader.PropertyToID("_CafeLookGrain");

        Bloom bloom;

        void OnEnable() => Apply();
        void Update() => Apply();
        void OnValidate() => Apply();
        void OnDisable()
        {
            Shader.SetGlobalFloat(Strength, 0f);
            if (glowVolume != null) glowVolume.weight = 0f;
        }

        void Apply()
        {
            float s = isActiveAndEnabled ? strength : 0f;
            Shader.SetGlobalFloat(Strength, s);
            Shader.SetGlobalFloat(Warmth, warmth);
            Shader.SetGlobalFloat(Saturation, saturation);
            Shader.SetGlobalFloat(Contrast, contrast);
            Shader.SetGlobalFloat(Vignette, vignette);
            Shader.SetGlobalFloat(Grain, grain);
            ApplyGlow(s);
        }

        void ApplyGlow(float s)
        {
            if (glowVolume == null) return;
            glowVolume.weight = s;
            // At runtime edit a per-scene copy of the profile; in the editor the asset keeps its saved values.
            if (!Application.isPlaying) return;
            if (bloom == null && !glowVolume.profile.TryGet(out bloom)) return;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = glow;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = glowThreshold;
        }
    }
}
