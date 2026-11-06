using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CafeSim
{
    public static class CafeCustomerCatalogChecks
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }

        // Editor-only contract check; never starts Play or adds anything to a scene.
        [UnityEditor.MenuItem("Tools/Cafe/Check Customer Asset Catalog")]
        public static void Run()
        {
            var people = JsonUtility.FromJson<PersonaTable>(File.ReadAllText("Assets/CafeSim/Data/personas.json")).items;
            var elder = CafeCustomerNames.Describe("elder_male_cardigan_cane", people);
            Check(elder.personaIds.Length == 1 && elder.personaIds[0] == "cus_elder", "Elder mapping");
            var trainer = CafeCustomerNames.Describe("trainer", people);
            Check(trainer.personaIds.Length == 1 && trainer.personaIds[0] == "cus_kim_gyeran", "Trainer asset maps only Kim Gyeran branch");
            var miku = CafeCustomerNames.Describe("miku", people);
            Check(miku.personaIds.Length == 1 && miku.personaIds[0] == "cus_miku", "Miku asset maps only Miku branch");
            Check(CafeCustomerNames.Describe("중년_여성_블라우스", people).personaIds[0] == "cus_jeolla", "Korean filename");
            Check(CafeCustomerNames.Describe("Character01", people).personaIds.Length == 0, "Unknown filename must stay pending");
            Check(CafeCustomerNames.Describe("young_male_female", people).personaIds.Length == 0, "Conflicting filename");
            Check(CafeCustomerNames.Describe("cus_elder_cus_child", people).personaIds.Length == 0, "Conflicting explicit IDs");
            Check(CafeCustomerNames.Describe("cus_blind_V2", people).personaIds[0] == "cus_blind", "Explicit persona ID");
            Check(miku.description.Contains("통통한 미쿠"), "Filename description");

            Directory.CreateDirectory(CafeCustomerCatalogImport.DropPath);
            AssetDatabase.Refresh();
            string fixture = CafeCustomerCatalogImport.DropPath.TrimEnd('/') + "/__CatalogCheck_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder(CafeCustomerCatalogImport.DropPath.TrimEnd('/'), Path.GetFileName(fixture));
            GameObject instance = null;
            var previewScene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CafeSim/Resources/CafeCustomers/Black/Model.fbx");
                Check(source != null, "Existing fallback model must exist");
                instance = (GameObject)PrefabUtility.InstantiatePrefab(source, previewScene);
                var animator = instance.GetComponentInChildren<Animator>() ?? instance.AddComponent<Animator>();
                var controller = AnimatorController.CreateAnimatorControllerAtPath(fixture + "/Fixture.controller");
                controller.AddParameter("Walking", AnimatorControllerParameterType.Bool);
                var idle = controller.layers[0].stateMachine.AddState("Idle");
                var walk = controller.layers[0].stateMachine.AddState("Walk");
                idle.motion = CafeCustomerCatalog.Clip("CafeCustomers/Black/Idle", FirstClip("Assets/CafeSim/Resources/CafeCustomers/Black/Idle.fbx"));
                walk.motion = CafeCustomerCatalog.Clip("CafeCustomers/Black/Walk", FirstClip("Assets/CafeSim/Resources/CafeCustomers/Black/Walk.fbx"));
                var toWalk = idle.AddTransition(walk); toWalk.hasExitTime = false; toWalk.AddCondition(AnimatorConditionMode.If, 0, "Walking");
                var toIdle = walk.AddTransition(idle); toIdle.hasExitTime = false; toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Walking");
                animator.runtimeAnimatorController = controller;
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                Check(shader != null, "Fixture shader");
                var material = new Material(shader);
                AssetDatabase.CreateAsset(material, fixture + "/Fixture.mat");
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
                string readyPath = fixture + "/cus_elder.prefab";
                PrefabUtility.SaveAsPrefabAsset(instance, readyPath);
                animator.runtimeAnimatorController = null;
                PrefabUtility.SaveAsPrefabAsset(instance, fixture + "/cus_child.prefab");
                AssetDatabase.SaveAssets();
                CafeCustomerCatalogImport.Rebuild();
                var table = ReadCatalog();
                var ready = Array.Find(table.items, e => e.source == readyPath);
                Check(ready != null && ready.status == "ready" && ready.controllerParameter == "Walking", "Prepared prefab must reach ready catalog");
                var pending = Array.Find(table.items, e => e.source == fixture + "/cus_child.prefab");
                Check(pending != null && pending.status == "review" && pending.issues.Length > 0, "Unanimated model must not become ready");
                var chosen = CafeCustomerCatalog.Choose("cus_elder", new System.Random(1));
                Check(chosen != null && chosen.personaIds.Length == 1, "Runtime selection from generated catalog");
                Check(Resources.Load<GameObject>(ready.resource) != null, "Generated resource path must load");
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(previewScene);
                AssetDatabase.DeleteAsset(fixture);
                CafeCustomerCatalogImport.Rebuild();
            }
            Check(!Array.Exists(ReadCatalog().items, e => e.source.Contains("/__CatalogCheck_")), "Deleted assets must leave catalog");
            string result = "PASS filename descriptions, persona matching/conflicts, prepared-prefab import, missing-animation rejection, Resources loading, runtime selection and deleted-asset cleanup. No Play/build.";
            File.WriteAllText("../.utmp/cafe-customer-assets-check.txt", result);
            Debug.Log("[Cafe] " + result);
        }
        static CustomerAssetTable ReadCatalog() => JsonUtility.FromJson<CustomerAssetTable>(File.ReadAllText("Assets/CafeSim/Resources/CafeCustomers/CustomerCatalog.json"));
        static string FirstClip(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal)) return clip.name;
            throw new Exception("Fixture clip missing: " + path);
        }
    }
}
