using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CafeSim
{
    // Only the new drop folder participates; the existing Black/Student/Miku import stays intact.
    [InitializeOnLoad]
    public sealed class CafeCustomerCatalogImport : AssetPostprocessor
    {
        public const string DropPath = "Assets/CafeSim/Resources/CafeCustomers/Incoming/";
        const string CatalogPath = "Assets/CafeSim/Resources/CafeCustomers/CustomerCatalog.json";
        const string PersonaPath = "Assets/CafeSim/Data/personas.json";
        static bool queued;
        static CafeCustomerCatalogImport() { Schedule(); }
        static void Schedule()
        {
            if (queued) return;
            queued = true;
            EditorApplication.delayCall += () => { queued = false; if (!EditorApplication.isPlayingOrWillChangePlaymode) Rebuild(); };
        }
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
        {
            foreach (var paths in new[] { imported, deleted, moved, oldPaths })
                if (Array.Exists(paths, p => p.StartsWith(DropPath, StringComparison.Ordinal) || p == PersonaPath)) { Schedule(); break; }
        }

        internal static bool MotionFile(string path) => Array.Exists(new[] { "idle", "walk" }, motion =>
            string.Equals(Path.GetFileNameWithoutExtension(path), motion, StringComparison.OrdinalIgnoreCase));

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(DropPath, StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            // Attempt standard humanoid retargeting once; later artist rig settings are retained.
            if (!importer.importSettingsMissing) return;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.preserveHierarchy = true;
            importer.optimizeGameObjects = false;
            importer.importCameras = false;
            importer.importLights = false;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(DropPath, StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            if (importer.clipAnimations.Length != 0) return;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
                if (MotionFile(assetPath) || clip.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    clip.name.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    clip.loopTime = clip.loopPose = true;
                    clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
                }
            if (clips.Length > 0) importer.clipAnimations = clips;
        }

        [UnityEditor.MenuItem("Tools/Cafe/Rebuild Customer Asset Catalog")]
        public static void Rebuild()
        {
            if (!File.Exists(PersonaPath)) return;
            var people = JsonUtility.FromJson<PersonaTable>(File.ReadAllText(PersonaPath)).items;
            var entries = new List<CustomerAssetEntry>();
            if (Directory.Exists(DropPath))
            {
                var paths = Directory.GetFiles(DropPath, "*", SearchOption.AllDirectories);
                Array.Sort(paths, StringComparer.Ordinal);
                foreach (string diskPath in paths)
                {
                    string path = diskPath.Replace('\\', '/');
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext != ".prefab" && ext != ".fbx") continue;
                    if (MotionFile(path)) continue;
                    if (ext == ".fbx" && File.Exists(Path.GetDirectoryName(path) + "/Appearance.prefab")) continue;
                    // A prepared prefab beside its source FBX owns that visual, avoiding duplicate draws.
                    if (ext == ".fbx" && File.Exists(Path.ChangeExtension(path, ".prefab"))) continue;
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (string.Equals(name, "Model", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Appearance", StringComparison.OrdinalIgnoreCase)) name = Path.GetFileName(Path.GetDirectoryName(path));
                    var entry = CafeCustomerNames.Describe(name, people);
                    entry.source = path;
                    entry.resource = Resource(path);
                    Validate(entry, path);
                    entries.Add(entry);
                }
            }
            string json = JsonUtility.ToJson(new CustomerAssetTable { items = entries.ToArray() }, true) + "\n";
            if (File.Exists(CatalogPath) && File.ReadAllText(CatalogPath) == json) return;
            File.WriteAllText(CatalogPath, json);
            AssetDatabase.ImportAsset(CatalogPath);
            CafeCustomerCatalog.Reload();
            Debug.Log("[Cafe] Customer catalog: " + entries.FindAll(e => e.status == "ready").Count + " ready / " + entries.Count + " models. Review " + CatalogPath);
        }

        static void Validate(CustomerAssetEntry entry, string path)
        {
            var issues = new List<string>();
            if (entry.personaIds.Length == 0) issues.Add(entry.mappingReason);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var renderers = model == null ? Array.Empty<Renderer>() : model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) issues.Add("모델 Renderer 없음/임포트 대기");
            foreach (var renderer in renderers)
                if (renderer.sharedMaterials.Length == 0 || Array.Exists(renderer.sharedMaterials, m => m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader"))
                    issues.Add("머티리얼/셰이더 연결 확인 필요");
            var animator = model == null ? null : model.GetComponentInChildren<Animator>(true);
            if (animator == null) issues.Add("Animator가 필요합니다.");
            if (model != null && model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0) issues.Add("전신 SkinnedMeshRenderer 필요");
            if (animator != null && (animator.avatar == null || !animator.avatar.isHuman) &&
                !Array.Exists(model.GetComponentsInChildren<Transform>(true), t => t.name.EndsWith("RightHand", StringComparison.Ordinal)))
                issues.Add("컵을 잡을 Humanoid 오른손 또는 RightHand 본 필요");
            // Prepared controllers must expose locomotion input; otherwise use two validated clips.
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                if (animator.runtimeAnimatorController.animationClips.Length < 2) issues.Add("Controller에 대기·걷기 애니메이션 연결 필요");
                RuntimeAnimatorController runtime = animator.runtimeAnimatorController;
                while (runtime is AnimatorOverrideController overrides) runtime = overrides.runtimeAnimatorController;
                var controller = runtime as UnityEditor.Animations.AnimatorController;
                foreach (var parameter in controller == null ? Array.Empty<AnimatorControllerParameter>() : controller.parameters)
                    if (parameter.name == "Walking" && parameter.type == AnimatorControllerParameterType.Bool ||
                        parameter.name == "Speed" && parameter.type == AnimatorControllerParameterType.Float)
                    { entry.controllerParameter = parameter.name; break; }
                if (string.IsNullOrEmpty(entry.controllerParameter)) issues.Add("Animator Controller에 Walking(bool) 또는 Speed(float) 필요");
            }
            else
            {
                FindClip(path, "idle", out entry.idleResource, out entry.idleClip);
                FindClip(path, "walk", out entry.walkResource, out entry.walkClip);
                if (entry.idleClip == null || entry.walkClip == null) issues.Add("Idle/Walk 클립 필요 (모델 내 또는 같은 폴더의 Idle.fbx, Walk.fbx)");
                foreach (var motion in new[] { new[] { entry.idleResource, entry.idleClip }, new[] { entry.walkResource, entry.walkClip } })
                {
                    var clip = CafeCustomerCatalog.Clip(motion[0], motion[1]);
                    if (clip == null) continue;
                    if (clip.legacy || clip.length <= 0f) issues.Add("Mecanim용 유효한 클립 필요: " + clip.name);
                    if (motion[0] != entry.resource && !clip.humanMotion) issues.Add("별도 클립의 Humanoid 매핑 필요: " + clip.name);
                }
                if (animator != null && (animator.avatar == null || !animator.avatar.isValid)) issues.Add("유효한 Avatar 필요");
                if (animator != null && (entry.idleResource != entry.resource || entry.walkResource != entry.resource) &&
                    (animator.avatar == null || !animator.avatar.isHuman)) issues.Add("별도 FBX 애니메이션 공유에는 Humanoid Avatar 필요");
            }
            entry.issues = issues.ToArray();
            entry.status = issues.Count == 0 ? "ready" : "review";
        }

        static void FindClip(string modelPath, string motion, out string resource, out string clipName)
        {
            resource = clipName = null;
            string folder = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            foreach (var path in new[] { modelPath, folder + "/" + (motion == "idle" ? "Idle.fbx" : "Walk.fbx") })
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                        (MotionFile(path) || clip.name.IndexOf(motion, StringComparison.OrdinalIgnoreCase) >= 0))
                    { resource = Resource(path); clipName = clip.name; return; }
        }
        static string Resource(string path) => Path.ChangeExtension(path.Substring(path.IndexOf("/Resources/", StringComparison.Ordinal) + 11), null);
    }
}
