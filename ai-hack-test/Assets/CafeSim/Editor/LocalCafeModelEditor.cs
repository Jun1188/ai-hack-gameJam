using UnityEditor;
using UnityEngine;

namespace CafeSim.Editor
{
    [CustomEditor(typeof(LocalCafeModel))]
    public sealed class LocalCafeModelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            CafeApiKeyInspector.Draw((LocalCafeModel)target);
        }
    }

    // Both the title and in-game Inspectors edit the same session-only override.
    internal static class CafeApiKeyInspector
    {
        internal static void Draw(LocalCafeModel model = null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor API 키", EditorStyles.boldLabel);
            var values = CafeApiRuntimeSettings.Load();
            bool changed = false;
            bool enabled = values.directInput;
            bool next = EditorGUILayout.Toggle("API 키 직접 입력", enabled);
            if (next != enabled) { values.directInput = next; changed = true; }
            if (next)
            {
                string key = values.apiKey ?? "";
                string edited = EditorGUILayout.PasswordField("API 키", key);
                if (edited != key) { values.apiKey = edited.Trim(); changed = true; }
                if (string.IsNullOrWhiteSpace(edited))
                    EditorGUILayout.HelpBox("키를 입력해 주세요. 비어 있으면 로컬 모델/자동 답변을 사용합니다.", MessageType.Info);
                if (GUILayout.Button("입력한 키 지우기")) { values.apiKey = ""; changed = true; }
            }
            if (changed) CafeApiRuntimeSettings.Save(values);
            EditorGUILayout.HelpBox("MainMenu와 CafeGame이 같은 설정을 사용합니다. 토글과 입력한 키는 이 PC에 보관되고 빌드에도 포함됩니다. 키 설정 파일은 Git에서 제외됩니다. 토글을 끄면 환경변수/기존 로컬 파일을 사용합니다.", MessageType.None);
            if (model == null)
                EditorGUILayout.HelpBox("여기서 입력한 키는 게임 시작 후 CafeGame의 API 연결에 적용됩니다.", MessageType.Info);
            else if (Application.isPlaying && GUILayout.Button("API 키 적용 · 응답 연결 재시작"))
            {
                // A key previously entered through the in-game settings must not shadow this edit.
                model.runtimeApiKey = null;
                var session = model.GetComponent<CafeSession>();
                if (session != null) session.RebootChatModel();
                else model.StartCoroutine(model.Boot());
            }
        }
    }
}
