using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CafeSim.Editor
{
    public static class CafeReceiptSunsetPreviewBuilder
    {
        public const string ScenePath="Assets/Scenes/CafeReceiptSunsetTest.unity";

        [UnityEditor.MenuItem("Cafe/검증/정산·노을 부분 씬 열기")]
        public static void Open()
        {
            if(!System.IO.File.Exists(ScenePath))Create();
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
        }

        public static string Create()
        {
            if(System.IO.File.Exists(ScenePath))return ScenePath;
            if(!AssetDatabase.CopyAsset("Assets/Scenes/CafeGame.unity",ScenePath))throw new System.InvalidOperationException("Cannot create preview scene");
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            CafeHud hud=null;CafeSession session=null;CafeBusinessDay day=null;CafeDaylight daylight=null;
            foreach(var root in scene.GetRootGameObjects())
            {
                hud??=root.GetComponentInChildren<CafeHud>(true);session??=root.GetComponentInChildren<CafeSession>(true);
                day??=root.GetComponentInChildren<CafeBusinessDay>(true);daylight??=root.GetComponentInChildren<CafeDaylight>(true);
                foreach(var c in root.GetComponentsInChildren<LocalCafeModel>(true))c.enabled=false;
                foreach(var c in root.GetComponentsInChildren<MapleCafe.BaristaView>(true))c.enabled=false;
                foreach(var c in root.GetComponentsInChildren<MapleCafe.CafeBrewStation>(true))c.enabled=false;
                foreach(var c in root.GetComponentsInChildren<MapleCafe.CafeMenuBook>(true))c.enabled=false;
                foreach(var c in root.GetComponentsInChildren<CafeStreetTraffic>(true))c.enabled=false;
            }
            session.enabled=false;day.enabled=false;
            var go=new GameObject("Receipt and 18h sunset partial preview");SceneManager.MoveGameObjectToScene(go,scene);
            var preview=go.AddComponent<CafeReceiptSunsetPreview>();preview.hud=hud;preview.session=session;preview.day=day;preview.daylight=daylight;
            preview.view=CafeReceiptSunsetPreview.View.Receipt;preview.previewHour=18;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorSceneManager.CloseScene(scene,true);
            return ScenePath;
        }
    }
}
