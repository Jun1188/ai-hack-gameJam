using System;
using GrandpaRobot.Game;
using GrandpaRobot.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace GrandpaRobot.Editor
{
    public static partial class PrototypeBuilder
    {
        [MenuItem("Grandpa Robot/Refresh HUD and Stage Preview")]
        public static void RefreshInteraction()
        {
            RefreshGrandfather();
            AssetDatabase.Refresh();
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=ScenePath)scene=EditorSceneManager.OpenScene(ScenePath);
            var game=UnityEngine.Object.FindAnyObjectByType<WellGame>();
            if(!game)throw new InvalidOperationException("Prototype scene has no WellGame");
            ConfigureHud(game);
            game.RefreshEditorPreview();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            var tree=game.GetComponent<UIDocument>().visualTreeAsset.CloneTree();
            var input=tree.Q<TextField>("prompt");
            if(input==null||tree.Q<Button>("send")==null)throw new InvalidOperationException("HUD required controls missing");
            input.value="무너진 곳간의 목재를 C1에 옮겨 주세요";
            if(input.value!="무너진 곳간의 목재를 C1에 옮겨 주세요")throw new InvalidOperationException("Unicode input roundtrip failed");
            if(game.transform.Find("Stage · house_01/02 Parts · 잔해와 조립 부품")==null)throw new InvalidOperationException("Hierarchy preview missing");
            Debug.Log("INTERACTION_REPAIR_OK: UI Toolkit HUD/TextField + Korean Unicode roundtrip; persisted grouped farm preview; no Play Mode or player build");
        }
        static void ConfigureHud(WellGame game)
        {
            var doc=game.GetComponent<UIDocument>();if(!doc)doc=game.gameObject.AddComponent<UIDocument>();
            const string settingsPath="Assets/UI/WellHudPanel.asset";
            var settings=AssetDatabase.LoadAssetAtPath<PanelSettings>(settingsPath);
            if(!settings){settings=ScriptableObject.CreateInstance<PanelSettings>();AssetDatabase.CreateAsset(settings,settingsPath);}
            settings.scaleMode=PanelScaleMode.ScaleWithScreenSize;settings.referenceResolution=new Vector2Int(1100,720);
            settings.screenMatchMode=PanelScreenMatchMode.MatchWidthOrHeight;settings.match=.5f;
            doc.panelSettings=settings;doc.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/WellHud.uxml");
            if(!doc.visualTreeAsset)throw new InvalidOperationException("HUD UXML import failed");
            doc.sortingOrder=10;EditorUtility.SetDirty(settings);EditorUtility.SetDirty(doc);
            var hud=game.GetComponent<WellHud>();hud.game=game;EditorUtility.SetDirty(hud);
        }
    }
}
