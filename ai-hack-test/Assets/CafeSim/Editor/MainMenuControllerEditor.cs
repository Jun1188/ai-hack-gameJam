using UnityEditor;

namespace CafeSim.Editor
{
    [CustomEditor(typeof(global::MainMenuController))]
    public sealed class MainMenuControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            CafeApiKeyInspector.Draw();
        }
    }
}
