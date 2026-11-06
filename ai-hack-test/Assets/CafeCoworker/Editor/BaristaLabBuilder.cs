using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using CafeCoworker.Integration;

namespace CafeCoworker.Editor
{
    public static class BaristaLabBuilder
    {
        public const string Root = "Assets/CafeCoworker";
        public const string ScenePath = Root + "/Scenes/CafeBaristaLab.unity";
        const string Art = Root + "/Art";
        [MenuItem("Tools/Cafe Coworker/Create independent lab scene")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before creating the lab.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("Lab scene exists. Open it for editing; generation never overwrites it.");
            Directory.CreateDirectory(Root + "/Scenes"); Directory.CreateDirectory(Art + "/Generated");
            AssetDatabase.Refresh();
            ImportArt();
            if (!AssetDatabase.CopyAsset("Assets/Scenes/CafeGame.unity", ScenePath)) throw new IOException("Cannot fork cafe scene");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var roots = scene.GetRootGameObjects();
                var all = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                var playerRoot = all.First(t => t.GetComponent<CharacterController>() != null);
                var eyes = playerRoot.GetComponentInChildren<Camera>(true);
                // Keep the forked cafe geometry, lights and URP components, but remove its game state and UI.
                foreach (var canvas in roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).ToArray())
                    UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                foreach (var m in roots.Where(r=>r != null).SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray())
                {
                    if (m == null) continue;
                    var ns = m.GetType().Namespace ?? "";
                    if (!ns.StartsWith("UnityEngine.Rendering")) UnityEngine.Object.DestroyImmediate(m);
                }
                foreach (var doc in roots.Where(r=>r != null).SelectMany(r => r.GetComponentsInChildren<UIDocument>(true)).ToArray())
                    UnityEngine.Object.DestroyImmediate(doc);
                all.First(t => t != null && t.name == "Order counter").position += Vector3.back * .6f;
                foreach(var collider in roots.Where(r=>r!=null).SelectMany(r=>r.GetComponentsInChildren<Collider>(true)).ToArray())
                    if(collider.name.StartsWith("Serve target") || collider.name.EndsWith("interaction", StringComparison.OrdinalIgnoreCase))
                        UnityEngine.Object.DestroyImmediate(collider);
                var control = playerRoot.gameObject.AddComponent<CoopPlayer>(); control.eyes = eyes;
                eyes.transform.localPosition = new Vector3(0, 1.57f, 0);
                eyes.transform.localRotation = Quaternion.identity;
                playerRoot.SetPositionAndRotation(new Vector3(1.12f, .02f, 2.65f), Quaternion.Euler(0, 195, 0));

                var root = new GameObject("Coworker lab | independent composition");
                var dialogue = root.AddComponent<BaristaDialogue>();
                var session = root.AddComponent<CoopSession>();
                var guests = root.AddComponent<GuestDialogue>();
                guests.profilesAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/Data/GuestProfiles.json");
                session.guests=guests;
                session.player = control; session.dialogue = dialogue;
                session.tablesAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/BaristaTables.json");
                var host=root.AddComponent<ExistingCafeTransportHost>();
                var staffTransport=root.AddComponent<ExistingCafeDialogueAdapter>();staffTransport.staffChannel=dialogue;staffTransport.host=host;
                var guestTransport=new GameObject("Guest dialogue transport").AddComponent<ExistingCafeDialogueAdapter>();
                guestTransport.transform.SetParent(root.transform);guestTransport.guestChannel=guests;guestTransport.host=host;
                var staff = new GameObject("서온 | coworker");
                staff.transform.position = new Vector3(2.95f, .02f, 2.3f);
                var cc = staff.AddComponent<CharacterController>();
                cc.height = 1.72f; cc.center = new Vector3(0, .86f, 0); cc.radius = .28f; cc.skinWidth = .025f;
                cc.stepOffset = .16f; cc.minMoveDistance = 0;
                var appearance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/barista.fbx"), staff.transform);
                appearance.name = "Barista model"; appearance.transform.localPosition = Vector3.zero;
                var renderers = appearance.GetComponentsInChildren<Renderer>(true);
                Bounds bounds = renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
                float factor = 1.72f / Mathf.Max(.01f, bounds.size.y);
                appearance.transform.localScale *= factor;
                // Re-measure after scale, then put the model's lowest point at the controller feet.
                bounds = renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
                appearance.transform.position += Vector3.up * (staff.transform.position.y - bounds.min.y);
                var material = MakeMaterial("Barista apron", new Color(.8f,.7f,.5f));
                var color = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/barista_image/Color.jpg");
                material.SetTexture("_BaseMap", color); material.SetColor("_BaseColor", Color.white);
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/barista_image/Normal.png");
                material.SetTexture("_BumpMap", normal); material.EnableKeyword("_NORMALMAP"); material.SetFloat("_Smoothness", .23f);
                foreach (var r in renderers) r.sharedMaterial = material;
                var animator = appearance.GetComponent<Animator>() ?? appearance.AddComponent<Animator>();
                animator.runtimeAnimatorController = CreateAnimator(); animator.applyRootMotion = false;
                var motor = staff.AddComponent<BezierWorkMotor>(); motor.animator = animator; motor.visual = appearance.transform;
                motor.walkBounds = new Bounds(new Vector3(1.4f, 1.2f, 2.4f), new Vector3(4, 3, 1.8f));
                session.barista = motor;
                playerRoot.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(staff.transform.position-playerRoot.position,Vector3.up));
                staff.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(playerRoot.position-staff.transform.position,Vector3.up));
                session.stations = new[] {
                    Station("prep", "시럽 옆 준비대", new Vector3(1.54f,.02f,2.75f), 0, new Vector3(1.54f,1.02f,3.48f)),
                    Station("cups", "컵 선반", new Vector3(2.65f,.02f,2.72f), 0, new Vector3(2.27f,1.8f,3.84f)),
                    Station("brew", "추출기", new Vector3(.29f,.02f,2.72f), 0, new Vector3(.29f,1.12f,3.48f)),
                    Station("serve", "앞쪽 전달대", new Vector3(.63f,.02f,1.95f), 180, new Vector3(.63f,1.08f,1.04f))
                };
                session.cup = CreateCup(MakeMaterial("Cup ceramic", new Color(.95f,.89f,.73f)), session.stations[2].workSurface);
                session.cupContents = session.cup.Find("Coffee").GetComponent<Renderer>();
                var arrowRoot = new GameObject("Current player work arrow");
                var arrow = arrowRoot.AddComponent<WorkArrow>(); arrow.session = session;
                arrow.arrow = MakeArrow(MakeMaterial("Work marker", new Color(1,.78f,.18f))).transform;
                arrow.arrow.SetParent(arrowRoot.transform);
                var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Cafe/Menu/NotoSansKR.ttf");
                var uiRoot = new GameObject("Coworker interface");
                var document = uiRoot.AddComponent<UIDocument>();
                var panel = ScriptableObject.CreateInstance<PanelSettings>();
                // Copy theme settings, never edit the existing shared asset.
                EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/CafeSim/Resources/CafeChatPanelSettings.asset"), panel);
                panel.name = "CoworkerPanel"; panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1280,720); panel.match = .5f;
                AssetDatabase.CreateAsset(panel, Root + "/UI/CoworkerPanel.asset");
                document.panelSettings = panel;
                document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "/UI/CoopHud.uxml");
                var hud = uiRoot.AddComponent<CoopHud>(); hud.session = session; hud.font = font;
                var settings=root.AddComponent<CoworkerModelSettings>();settings.hud=hud;settings.session=session;settings.host=host;settings.font=font;
                CreateCustomer(font);
                Physics.SyncTransforms();
                foreach (var station in session.stations)
                    if (station.approach == null || station.workSurface == null) throw new InvalidOperationException("Unbound station");
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
        static WorkStation Station(string id, string label, Vector3 pos, float yaw, Vector3 surface)
        {
            var obj = new GameObject(label); var s = obj.AddComponent<WorkStation>();
            s.stationId = id; s.displayName = label;
            s.approach = new GameObject("Approach | " + id).transform; s.approach.SetParent(obj.transform);
            s.approach.SetPositionAndRotation(pos, Quaternion.Euler(0,yaw,0));
            s.workSurface = new GameObject("Work surface | " + id).transform; s.workSurface.SetParent(obj.transform); s.workSurface.position = surface;
            return s;
        }
        static Material MakeMaterial(string name, Color color)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.name = name;
            m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness", .2f);
            AssetDatabase.CreateAsset(m, Art + "/Generated/" + name + ".mat"); return m;
        }
        static RuntimeAnimatorController CreateAnimator()
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(Art + "/Generated/Barista.controller");
            controller.AddParameter("Walking", AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            var idle = machine.AddState("Idle"); idle.motion = Clip(Art + "/Standing Idle.fbx");
            var walk = machine.AddState("Walk"); walk.motion = Clip(Art + "/Walking.fbx");
            if (idle.motion == null || walk.motion == null) throw new InvalidOperationException("Missing barista animation");
            machine.defaultState = idle;
            var toWalk = idle.AddTransition(walk); toWalk.hasExitTime = false; toWalk.duration = .18f;
            toWalk.AddCondition(AnimatorConditionMode.If, 0, "Walking");
            var toIdle = walk.AddTransition(idle); toIdle.hasExitTime = false; toIdle.duration = .2f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Walking");
            return controller;
        }
        static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__"));
        static void ImportArt()
        {
            var model = (ModelImporter)AssetImporter.GetAtPath(Art + "/barista.fbx");
            model.animationType = ModelImporterAnimationType.Human; model.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            model.materialImportMode = ModelImporterMaterialImportMode.None; model.SaveAndReimport();
            var avatar = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/barista.fbx").GetComponent<Animator>()?.avatar;
            if (avatar == null || !avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("Barista humanoid avatar is invalid");
            foreach (string file in new[]{"Standing Idle.fbx","Walking.fbx"})
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(Art + "/" + file);
                importer.animationType = ModelImporterAnimationType.Human; importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null; importer.importAnimation = true;
                importer.clipAnimations = Array.Empty<ModelImporterClipAnimation>();
                importer.SaveAndReimport();
                var clips = importer.defaultClipAnimations;
                foreach (var c in clips) { c.loopTime = true; c.lockRootPositionXZ = true; c.lockRootHeightY = true; c.lockRootRotation = true; }
                importer.clipAnimations = clips; importer.SaveAndReimport();
            }
            var normal = (TextureImporter)AssetImporter.GetAtPath(Art + "/barista_image/Normal.png");
            normal.textureType = TextureImporterType.NormalMap; normal.SaveAndReimport();
        }
        static Transform CreateCup(Material ceramic, Transform surface)
        {
            var cup = GameObject.CreatePrimitive(PrimitiveType.Cylinder); cup.name = "Shared order mug";
            UnityEngine.Object.DestroyImmediate(cup.GetComponent<Collider>()); cup.GetComponent<Renderer>().sharedMaterial = ceramic;
            cup.transform.position = surface.position; cup.transform.localScale = new Vector3(.13f,.1f,.13f);
            var coffee = GameObject.CreatePrimitive(PrimitiveType.Cylinder); coffee.name = "Coffee"; coffee.transform.SetParent(cup.transform,false);
            coffee.transform.localPosition = new Vector3(0,.96f,0); coffee.transform.localScale = new Vector3(.85f,.03f,.85f);
            UnityEngine.Object.DestroyImmediate(coffee.GetComponent<Collider>());
            coffee.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Coffee", new Color(.17f,.075f,.025f));
            return cup.transform;
        }
        static GameObject MakeArrow(Material material)
        {
            material.shader=Shader.Find("Universal Render Pipeline/Unlit");
            material.SetColor("_BaseColor",new Color(.8f,.5f,.12f));
            var go = new GameObject("Down arrow | tip pivot");
            var mesh = new Mesh { name = "Downward work arrow" };
            mesh.vertices = new[]{new Vector3(0,0,0),new Vector3(-.16f,.18f,0),new Vector3(-.055f,.18f,0),
                new Vector3(-.055f,.44f,0),new Vector3(.055f,.44f,0),new Vector3(.055f,.18f,0),new Vector3(.16f,.18f,0)};
            mesh.triangles = new[]{0,1,6,0,6,1,2,3,4,2,4,5,2,4,3,2,5,4};
            mesh.RecalculateNormals(); AssetDatabase.CreateAsset(mesh, Art + "/Generated/WorkArrow.asset");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            return go;
        }
        static void CreateCustomer(Font font)
        {
            // A neutral scene-local customer marker; the existing customer MVP and its data are untouched.
            var person = GameObject.CreatePrimitive(PrimitiveType.Capsule); person.name = "Lab customer";
            person.transform.position = new Vector3(.63f,.88f,-.05f); person.transform.localScale = new Vector3(.55f,.88f,.55f);
            person.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Guest",new Color(.37f,.48f,.48f));
            var label = new GameObject("Customer label").AddComponent<TextMesh>();
            label.text = "손님"; label.font = font; label.characterSize = .04f; label.fontSize = 40;
            label.anchor = TextAnchor.MiddleCenter; label.transform.position = new Vector3(.63f,1.96f,-.05f);
            label.transform.rotation = Quaternion.Euler(0,180,0);
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
    }
}
