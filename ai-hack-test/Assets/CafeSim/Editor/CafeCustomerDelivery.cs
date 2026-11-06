using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CafeSim
{
    // Explicit asset preparation for approved Downloads archives. Never edits a scene.
    public static class CafeCustomerDelivery
    {
        const string Base = "Assets/CafeSim/Resources/CafeCustomers/";
        static readonly string[][] Families =
        {
            new[] { "young_female_miku", "Miku" },
            new[] { "young_male_black", "Black" },
            new[] { "middle_male_trainer", "Incoming/middle_male_trainer" },
            new[] { "child", "Incoming/child" },
            new[] { "cus_pangyo", "Incoming/cus_pangyo" },
            new[] { "cus_student", "Incoming/cus_student" },
            new[] { "cus_gyeongsang", "Incoming/cus_gyeongsang" },
            new[] { "shared_adult_female", "Black", "Female.fbx" }
        };

        [UnityEditor.MenuItem("Tools/Cafe/Prepare Downloaded Customer Models")]
        public static void Prepare()
        {
            foreach (var family in Families) PrepareFamily(family[0], Base + family[1] + "/", family.Length > 2 ? family[2] : "Model.fbx");
            AssetDatabase.SaveAssets();
            CafeCustomerCatalogImport.Rebuild();
        }

        static void PrepareFamily(string name, string source, string modelFile = "Model.fbx")
        {
            string folder = Base + "Incoming/" + name;
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Base + "Incoming", name);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source + modelFile);
            if (prefab == null) throw new InvalidOperationException("Customer source missing: " + source);
            var idle = Clip(source + "Idle.fbx");
            var walk = Clip(File.Exists(folder + "/Walk.fbx") ? folder + "/Walk.fbx" : source + "Walk.fbx");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Base + "CustomerSurface.shader");
            string materialPath = folder + "/Outfit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
            material.shader = shader;
            material.SetFloat("_Recolor", 0f);
            foreach (var pair in new[] { new[] { "_BaseMap", "Color" }, new[] { "_BumpMap", "Normal" }, new[] { "_MetallicMap", "Metallic" }, new[] { "_RoughnessMap", "Roughness" } })
            {
                string path = new[] { ".jpg", ".png", ".JPEG" }.Select(ext => source + pair[1] + ext).FirstOrDefault(File.Exists);
                if (path == null) throw new InvalidOperationException("Missing texture: " + source + pair[1]);
                material.SetTexture(pair[0], AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            }
            EditorUtility.SetDirty(material);

            string controllerPath = folder + "/Locomotion.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                controller.AddParameter("Walking", AnimatorControllerParameterType.Bool);
                var sm = controller.layers[0].stateMachine;
                var idleState = sm.AddState("Idle"); var walkState = sm.AddState("Walk");
                sm.defaultState = idleState;
                var toWalk = idleState.AddTransition(walkState); toWalk.hasExitTime = false; toWalk.duration = .18f;
                toWalk.AddCondition(AnimatorConditionMode.If, 0f, "Walking");
                var toIdle = walkState.AddTransition(idleState); toIdle.hasExitTime = false; toIdle.duration = .18f;
                toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Walking");
            }
            foreach (var state in controller.layers[0].stateMachine.states)
            {
                if (state.state.name == "Idle") state.state.motion = idle;
                if (state.state.name == "Walk") state.state.motion = walk;
                EditorUtility.SetDirty(state.state);
            }
            EditorUtility.SetDirty(controller);
            var previewScene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, previewScene);
            try
            {
                instance.name = name;
                var animator = instance.GetComponentInChildren<Animator>() ?? instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                PrefabUtility.SaveAsPrefabAsset(instance, folder + "/Appearance.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(previewScene); }
        }

        static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .First(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
    }
}
