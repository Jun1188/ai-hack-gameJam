using System;
using UnityEngine;

namespace CafeSim
{
    // Local build configuration. This credential asset is deliberately excluded from Git.
    public static class CafeApiRuntimeSettings
    {
        public const string ResourceName = "CafeApiRuntimeSettings";
        public const string AssetPath = "Assets/CafeSim/Resources/CafeApiRuntimeSettings.json";
        [Serializable] public sealed class Values { public bool directInput; public string apiKey = ""; }

        public static Values Load()
        {
            string json = null;
#if UNITY_EDITOR
            string path = System.IO.Path.Combine(Application.dataPath, "CafeSim/Resources/CafeApiRuntimeSettings.json");
            if (System.IO.File.Exists(path)) json = System.IO.File.ReadAllText(path);
            else return new Values {
                directInput = UnityEditor.SessionState.GetBool(LocalCafeModel.InspectorKeyEnabled, false),
                apiKey = UnityEditor.SessionState.GetString(LocalCafeModel.InspectorKeyValue, "")
            };
#else
            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset != null) json = asset.text;
#endif
            try { return string.IsNullOrWhiteSpace(json) ? new Values() : JsonUtility.FromJson<Values>(json) ?? new Values(); }
            catch (ArgumentException) { return new Values(); }
        }

#if UNITY_EDITOR
        public static void Save(Values values)
        {
            values.apiKey = values.apiKey?.Trim() ?? "";
            string path = System.IO.Path.Combine(Application.dataPath, "CafeSim/Resources/CafeApiRuntimeSettings.json");
            System.IO.File.WriteAllText(path, JsonUtility.ToJson(values));
            UnityEditor.SessionState.SetBool(LocalCafeModel.InspectorKeyEnabled, values.directInput);
            UnityEditor.SessionState.SetString(LocalCafeModel.InspectorKeyValue, values.apiKey);
            UnityEditor.AssetDatabase.ImportAsset(AssetPath);
        }
#endif
    }
}
