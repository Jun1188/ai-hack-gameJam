using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CafeSim.Editor
{
    public static class CafeSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/test-serv.unity";
        const string ArtPath = "Assets/CafeSim/Materials";

        [UnityEditor.MenuItem("Cafe Simulator/Create test-serv scene")]
        public static void Create()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("test-serv already exists. Preserve scene edits; remove explicitly before regeneration.");
            Directory.CreateDirectory(ArtPath);
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.72f, .77f, .78f);
                var wood = Material("Walnut", new Color(.31f, .18f, .115f));
                var cream = Material("Linen", new Color(.86f, .81f, .68f));
                var sage = Material("Sage", new Color(.24f, .40f, .35f));
                var dark = Material("Charcoal", new Color(.075f, .1f, .1f));
                var skin = Material("CustomerSkin", new Color(.78f, .57f, .43f));
                var shirt = Material("CustomerShirt", new Color(.35f, .48f, .63f));
                var gold = Material("Brass", new Color(.8f, .55f, .25f));
                Box("Floor", new Vector3(0,-.15f,1), new Vector3(12,.3f,12), wood);
                Box("Back wall", new Vector3(0,2,-4.9f), new Vector3(12,4,.2f), sage);
                Box("Left wall", new Vector3(-5.9f,2,1), new Vector3(.2f,4,12), cream);
                Box("Right wall", new Vector3(5.9f,2,1), new Vector3(.2f,4,12), cream);
                // Door opening on the customer's right: visible arrival and departure path.
                Box("Front left", new Vector3(-1.5f,2,6.9f), new Vector3(9,4,.2f), sage);
                Box("Front right", new Vector3(5.5f,2,6.9f), new Vector3(1,4,.2f), sage);
                Box("Door lintel", new Vector3(4,3.65f,6.9f), new Vector3(2, .7f,.2f), sage);
                var boundary = new GameObject("Door player boundary");
                boundary.transform.position = new Vector3(4,1.5f,6.95f);
                boundary.AddComponent<BoxCollider>().size = new Vector3(2,3,.15f);
                Box("Counter base", new Vector3(0,.5f,1.1f), new Vector3(5.8f,1, .9f), sage);
                Box("Counter top", new Vector3(0,1.05f,1.1f), new Vector3(6,.15f,1.15f), wood);
                Box("Serving tray", new Vector3(0,1.15f,1.1f), new Vector3(.65f,.045f,.5f), gold);
                Box("Back worktop", new Vector3(0,.9f,-4.2f), new Vector3(8,.12f,.9f), wood);
                Box("Coffee machine", new Vector3(-1.8f,1.28f,-4.2f), new Vector3(.9f,.7f,.6f), dark);
                Box("Menu board", new Vector3(1.4f,2.4f,-4.75f), new Vector3(3,1.5f,.1f), dark);
                var label = new GameObject("Cafe sign").AddComponent<TextMesh>();
                label.text = "SLOW CUP\nLOCAL CAFE"; label.fontSize = 44; label.characterSize = .065f;
                label.anchor = TextAnchor.MiddleCenter; label.color = new Color(.98f,.85f,.56f);
                label.transform.position = new Vector3(1.4f,2.4f,-4.65f); label.transform.rotation = Quaternion.Euler(0,180,0);
                for (int i = 0; i < 4; i++)
                    Box("Shelf cup " + i, new Vector3(-.5f + i * .4f,1.1f,-4.2f), new Vector3(.16f,.25f,.16f), cream);
                var sun = new GameObject("Warm daylight").AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.5f;
                sun.color = new Color(1,.91f,.76f); sun.transform.rotation = Quaternion.Euler(48,-35,0);

                var player = new GameObject("Player (controls only)");
                player.transform.position = new Vector3(0,1.02f,-1.4f);
                var capsule = player.AddComponent<CapsuleCollider>(); capsule.height = 1.8f; capsule.radius = .28f;
                var body = player.AddComponent<Rigidbody>(); body.mass = 70;
                var friction = new PhysicsMaterial("PlayerFrictionless") { staticFriction = 0, dynamicFriction = 0, frictionCombine = PhysicsMaterialCombine.Minimum };
                AssetDatabase.CreateAsset(friction, ArtPath + "/PlayerFrictionless.physicMaterial"); capsule.sharedMaterial = friction;
                var controls = player.AddComponent<CafeFirstPersonController>();
                var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(player.transform, false); cameraObject.transform.localPosition = new Vector3(0,.64f,0);
                var camera = cameraObject.AddComponent<Camera>(); camera.fieldOfView = 65; camera.nearClipPlane = .05f; camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.46f,.59f,.61f);
                cameraObject.AddComponent<AudioListener>(); controls.eye = cameraObject.transform;
                cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

                var customer = new GameObject("Customer (placeholder)");
                var torso = Primitive("Body", PrimitiveType.Capsule, new Vector3(0,.8f,0), new Vector3(.55f,.65f,.55f), shirt, customer.transform, false);
                Primitive("Head", PrimitiveType.Sphere, new Vector3(0,1.6f,0), new Vector3(.4f,.43f,.4f), skin, customer.transform, false);
                for (int i = -1; i <= 1; i += 2)
                    Primitive("Eye", PrimitiveType.Sphere, new Vector3(.08f*i,1.65f,.18f), new Vector3(.045f,.045f,.035f), dark, customer.transform, false);
                var entrance = new GameObject("Customer entrance").transform; entrance.position = new Vector3(4,0,6.6f);
                var service = new GameObject("Customer service point").transform; service.position = new Vector3(0,0,2.5f); service.rotation = Quaternion.Euler(0,180,0);
                customer.transform.position = service.position; customer.transform.rotation = service.rotation;
                var cup = Primitive("Served drink", PrimitiveType.Cylinder, new Vector3(0,1.3f,1.1f), new Vector3(.2f,.14f,.2f), cream, null, false);
                cup.SetActive(false);

                var root = new GameObject("Cafe test session");
                var model = root.AddComponent<LocalCafeModel>();
                var session = root.AddComponent<CafeSession>();
                session.menuData = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/CafeSim/Data/menu.json");
                session.personaData = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/CafeSim/Data/personas.json");
                session.model = model; session.customer = customer.transform; session.customerBody = torso.GetComponent<Renderer>();
                session.entrance = entrance; session.servicePoint = service; session.servedCup = cup;
                var hud = root.AddComponent<CafeHud>(); hud.session = session; hud.player = controls;
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("Cafe scene created: " + ScenePath + ". Build scene settings were preserved.");
            }
            finally
            {
                if (!Application.isBatchMode) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
        static Material Material(string name, Color color)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            AssetDatabase.CreateAsset(m, ArtPath + "/" + name + ".mat"); return m;
        }
        static GameObject Box(string name, Vector3 position, Vector3 scale, Material material) =>
            Primitive(name, PrimitiveType.Cube, position, scale, material, null, true);
        static GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Transform parent, bool collision)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
    }

    // A future Windows build containing test-serv cannot silently omit its tested model.
    public sealed class CafeLocalBuildPayload : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        [Serializable] sealed class PayloadFile { public string path, sha256; public long bytes; }
        [Serializable] sealed class PayloadManifest { public bool normalTextPassed; public PayloadFile[] files; }
        public int callbackOrder => 50;
        static bool IncludesCafe()
        {
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled && scene.path == CafeSceneBuilder.ScenePath) return true;
            // Explicit BuildPlayerOptions can use a different scene list; prebuild report covers this below.
            return false;
        }
        public void OnPreprocessBuild(BuildReport report)
        {
            // Payload is always included for Windows builds of this cafe branch.
            if (report.summary.platform != BuildTarget.StandaloneWindows64)
            {
                if (IncludesCafe()) throw new BuildFailedException("Cafe local inference supports Windows x64 only.");
                return;
            }
            var root = LocalCafeModel.PayloadPath;
            if (!File.Exists(Path.Combine(root,"manifest.json")) || !File.Exists(Path.Combine(root,"model.gguf")) ||
                !File.Exists(Path.Combine(root,"engine/llama-server.exe")))
                throw new BuildFailedException("Test and prepare the local model with tools/prepare-cafe-local.py before building.");
            ValidatePayload(root);
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64) return;
            string output = report.summary.outputPath;
            string target = Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + "_Data/StreamingAssets/CafeLocal");
            CopyTree(LocalCafeModel.PayloadPath, target);
            Debug.Log("Included tested cafe inference runtime + model: " + target);
        }
        static void CopyTree(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source))
                if (!file.EndsWith(".partial", StringComparison.OrdinalIgnoreCase)) File.Copy(file, Path.Combine(target,Path.GetFileName(file)),true);
            foreach (string folder in Directory.GetDirectories(source)) CopyTree(folder,Path.Combine(target,Path.GetFileName(folder)));
        }
        public static void ValidatePayload(string root)
        {
            var manifest = JsonUtility.FromJson<PayloadManifest>(File.ReadAllText(Path.Combine(root,"manifest.json")));
            if (manifest == null || !manifest.normalTextPassed || manifest.files == null || manifest.files.Length < 5)
                throw new BuildFailedException("No verified cafe inference payload manifest.");
            foreach (var entry in manifest.files)
            {
                string path = Path.GetFullPath(Path.Combine(root,entry.path));
                if (!path.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new BuildFailedException("Invalid inference payload path.");
                if (!File.Exists(path) || new FileInfo(path).Length != entry.bytes)
                    throw new BuildFailedException("Missing/incomplete cafe payload file: " + entry.path);
                using (var input = File.OpenRead(path))
                using (var sha = SHA256.Create())
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant();
                    if (hash != entry.sha256) throw new BuildFailedException("Cafe payload hash mismatch: " + entry.path);
                }
            }
        }
    }
}
