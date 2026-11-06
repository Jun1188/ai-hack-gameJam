using UnityEngine;

namespace CafeSim
{
    // Player options shared by the title screen and the in-game pause menu, saved in PlayerPrefs.
    public static class CafeSettings
    {
        const string VolumeKey="cafe.volume", SensitivityKey="cafe.mouseSensitivity";
        public const float MinSensitivity=.03f, MaxSensitivity=.4f, DefaultSensitivity=.13f;

        public static float Volume
        {
            get=>PlayerPrefs.GetFloat(VolumeKey,1);
            set{PlayerPrefs.SetFloat(VolumeKey,Mathf.Clamp01(value));AudioListener.volume=Mathf.Clamp01(value);}
        }
        public static float MouseSensitivity
        {
            get=>Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey,DefaultSensitivity),MinSensitivity,MaxSensitivity);
            set=>PlayerPrefs.SetFloat(SensitivityKey,Mathf.Clamp(value,MinSensitivity,MaxSensitivity));
        }

        // Volume applies from the first scene, so the title music already respects it.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()=>AudioListener.volume=Volume;
    }
}
