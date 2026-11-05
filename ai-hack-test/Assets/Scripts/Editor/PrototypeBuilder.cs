using System;
using System.IO;
using System.Linq;
using GrandpaRobot.Data;
using GrandpaRobot.Game;
using GrandpaRobot.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEditor.Animations;
using UnityEngine;

namespace GrandpaRobot.Editor
{
    public static partial class PrototypeBuilder
    {
        const string CatalogPath="Assets/Data/PrototypeCatalog.asset";
        public const string ScenePath="Assets/Scenes/WellPrototype.unity";
        [MenuItem("Grandpa Robot/Generate Prototype")]
        public static void Generate()
        {
            foreach(var path in new[]{"Assets/Art/Materials","Assets/Prefabs/Prototype","Assets/Art/Audio","Assets/Art/Fonts"})Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
            var catalog=AssetDatabase.LoadAssetAtPath<AssetCatalog>(CatalogPath);
            if(!catalog){catalog=ScriptableObject.CreateInstance<AssetCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath);}
            var colors=new[]{("ground",new Color(.67f,.73f,.65f)),("stone",new Color(.66f,.68f,.66f)),("stone_light",new Color(.84f,.85f,.8f)),
                ("wood",new Color(.59f,.43f,.28f)),("dark",new Color(.12f,.2f,.23f)),("node",new Color(.37f,.66f,.67f)),
                ("robot",new Color(.73f,.87f,.9f)),("cloth",new Color(.9f,.87f,.77f)),("skin",new Color(.91f,.71f,.56f)),("hat",new Color(.64f,.5f,.32f)),
                ("dirt",new Color(.66f,.56f,.4f)),("grass",new Color(.37f,.49f,.29f)),("rock",new Color(.49f,.53f,.48f)),
                ("ghost",new Color(.35f,1,.85f,.38f))};
            catalog.materials=colors.Select(x=>new MaterialEntry{id=x.Item1,asset=Material(x.Item1,x.Item2)}).ToArray();
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name="Whitebox cube";
            var cubePrefab=PrefabUtility.SaveAsPrefabAsset(cube,"Assets/Prefabs/Prototype/Cube.prefab");UnityEngine.Object.DestroyImmediate(cube);
            var pulley=GameObject.CreatePrimitive(PrimitiveType.Cube);pulley.name="Pulley placeholder";
            var pointer=GameObject.CreatePrimitive(PrimitiveType.Cube);pointer.transform.SetParent(pulley.transform);pointer.transform.localPosition=new Vector3(0,.65f,.32f);pointer.transform.localScale=new Vector3(.4f,.4f,.3f);
            var pulleyPrefab=PrefabUtility.SaveAsPrefabAsset(pulley,"Assets/Prefabs/Prototype/Pulley.prefab");UnityEngine.Object.DestroyImmediate(pulley);
            catalog.prefabs=new[]{new PrefabEntry{id="cube",asset=cubePrefab},new PrefabEntry{id="pulley",asset=pulleyPrefab},new PrefabEntry{id="house",asset=CreateHouse()},new PrefabEntry{id="grandfather",asset=ImportGrandfather()}};
            catalog.prefabs=catalog.prefabs.Concat(CreateAssemblyPrefabs()).ToArray();
            catalog.sounds=new[]{new AudioEntry{id="ambience",asset=Tone("Ambience",110,2,.02f)},new AudioEntry{id="place",asset=Tone("Place",330,.12f,.18f)}};
            catalog.objectDefinitions=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/objects.json");
            catalog.stages=new[]{"house_01","well_01","well_fixture"}.Select(x=>AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/"+x+".json")).ToArray();
            // Redistributable font included with Unity's installed search service package.
            var fontSource=Directory.GetFiles("Library/PackageCache","*.ttf",SearchOption.AllDirectories).FirstOrDefault(p=>Path.GetFileName(p).Equals("NotoSansKR-Regular.ttf",StringComparison.OrdinalIgnoreCase));
            // Windows Korean system font stays local: runtime uses it dynamically if no bundled font exists.
            if(fontSource!=null){File.Copy(fontSource,"Assets/Art/Fonts/NotoSansKR-Regular.ttf",true);AssetDatabase.ImportAsset("Assets/Art/Fonts/NotoSansKR-Regular.ttf");catalog.uiFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/NotoSansKR-Regular.ttf");}
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
            foreach(var stage in catalog.stages)WellGame.ValidateData(JsonUtility.FromJson<StageDefinition>(stage.text),catalog.Library(),catalog);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Stage Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.22f,.24f);
            camera.gameObject.AddComponent<AudioListener>();var rig=camera.gameObject.AddComponent<StageCamera>();rig.view=camera;rig.Apply();
            var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.transform.rotation=Quaternion.Euler(55,-35,0);
            var root=new GameObject("Prototype Systems");var game=root.AddComponent<WellGame>();game.catalog=catalog;
            root.AddComponent<WellHud>().game=game;
            ConfigureHud(game);game.RefreshEditorPreview();
            EditorSceneManager.SaveScene(scene,ScenePath);
            PlayerSettings.productName="Grandpa and Robot - Well Prototype";
            PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=800;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.runInBackground=true;
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();
            PrototypeChecks.Run();
            Debug.Log("PROTOTYPE_GENERATED: "+ScenePath);
        }
        static Material Material(string name,Color color)
        {
            var path="Assets/Art/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find(name=="ghost"?"GrandpaRobot/MemoryGhost":"GrandpaRobot/WhiteboxToon"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color);EditorUtility.SetDirty(mat);return mat;
        }
        static AudioClip Tone(string name,float hz,float seconds,float volume)
        {
            var path="Assets/Art/Audio/"+name+".wav";
            using(var writer=new BinaryWriter(File.Open(path,FileMode.Create)))
            {
                int count=(int)(22050*seconds);writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(22050);writer.Write(44100);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
                for(int i=0;i<count;i++)writer.Write((short)(Math.Sin(i*hz*Math.PI*2/22050)*volume*short.MaxValue*Math.Min(1,(count-i)/1000f)));
            }
            AssetDatabase.ImportAsset(path);return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        static GameObject ImportGrandfather()
        {
            const string modelPath="Assets/Art/Models/Grandfather/grand_9.fbx";
            AssetDatabase.ImportAsset(modelPath);
            var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.importAnimation=true;importer.animationType=ModelImporterAnimationType.Human;
            importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.optimizeGameObjects=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if(!source)throw new InvalidOperationException("할아버지 FBX를 읽을 수 없습니다.");
            var root=new GameObject("Grandfather");var visual=(GameObject)PrefabUtility.InstantiatePrefab(source);visual.transform.SetParent(root.transform,false);
            PrefabUtility.UnpackPrefabInstance(visual,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var animator=visual.GetComponent<Animator>();
            if(!animator||!animator.avatar||!animator.avatar.isValid||!animator.avatar.isHuman)throw new InvalidOperationException("할아버지 휴머노이드 아바타 매핑 실패");
            animator.runtimeAnimatorController=CreateLocomotion();animator.applyRootMotion=false;
            animator.enabled=true;animator.speed=1;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var locomotion=root.AddComponent<GrandfatherLocomotion>();locomotion.animator=animator;locomotion.controller=animator.runtimeAnimatorController;
            var renderers=visual.GetComponentsInChildren<Renderer>();
            foreach(var skinned in visual.GetComponentsInChildren<SkinnedMeshRenderer>())skinned.updateWhenOffscreen=true;
            if(renderers.Length==0)throw new InvalidOperationException("할아버지 FBX에 렌더러가 없습니다.");
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            float scale=1.9f/Mathf.Max(.001f,bounds.size.y);visual.transform.localScale*=scale;
            visual.transform.localPosition=new Vector3(-bounds.center.x*scale,-bounds.min.y*scale,-bounds.center.z*scale);
            foreach(var collider in visual.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
            // Keep the original UVs and preserve later user-authored material choices.
            var shared=new System.Collections.Generic.Dictionary<Material,Material>();
            foreach(var renderer in renderers)
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    var original=materials[i];
                    if(original && !shared.ContainsKey(original))
                    {
                        var color=original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):original.HasProperty("_Color")?original.color:new Color(.8f,.74f,.63f);
                        string path="Assets/Art/Materials/Grandfather_Lit_"+shared.Count+".mat";
                        var toon=AssetDatabase.LoadAssetAtPath<Material>(path);
                        if(!toon){toon=new Material(Shader.Find("Universal Render Pipeline/Lit"));toon.SetColor("_BaseColor",color);AssetDatabase.CreateAsset(toon,path);}
                        var texture=original.HasProperty("_BaseMap")?original.GetTexture("_BaseMap"):original.HasProperty("_MainTex")?original.GetTexture("_MainTex"):null;
                        if(texture&&!toon.GetTexture("_BaseMap"))toon.SetTexture("_BaseMap",texture);
                        BindGrandfatherTextures(toon);
                        EditorUtility.SetDirty(toon);
                        shared[original]=toon;
                    }
                    materials[i]=original?shared[original]:Material("Grandfather_default",new Color(.8f,.74f,.63f));
                }
                renderer.sharedMaterials=materials;
            }
            AttachCane(animator);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/Prototype/Grandfather.prefab");
            Debug.Log("GRANDFATHER_IMPORTED: renderers="+renderers.Length+" originalBounds="+bounds.size+" scale="+scale);
            var mesh=visual.GetComponentInChildren<SkinnedMeshRenderer>();
            Debug.Log("GRANDFATHER_RIG: human="+animator.avatar.isHuman+" valid="+animator.avatar.isValid+" bones="+mesh.bones.Length+" uv="+mesh.sharedMesh.uv.Length+" clips="+AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Count());
            UnityEngine.Object.DestroyImmediate(root);return prefab;
        }
        static RuntimeAnimatorController CreateLocomotion()
        {
            Directory.CreateDirectory("Assets/Art/Animation");AssetDatabase.Refresh();
            AnimationClip ImportClip(string name)
            {
                string path="Assets/Art/Models/Grandfather/Animations/"+name+".fbx";
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                if(importer==null)throw new InvalidOperationException("애니메이션 FBX 없음: "+path);
                importer.importAnimation=true;importer.animationType=ModelImporterAnimationType.Human;
                // Mixamo exports have different wrapper/finger hierarchies. Retarget humanoid
                // motion from each valid source avatar instead of copying an incompatible rig.
                importer.sourceAvatar=null;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
                var clips=importer.defaultClipAnimations;
                foreach(var clip in clips)
                {
                    clip.name=name;clip.loopTime=true;clip.loopPose=true;
                    clip.lockRootRotation=true;clip.lockRootHeightY=true;clip.lockRootPositionXZ=true;
                }
                importer.clipAnimations=clips;importer.SaveAndReimport();
                var asset=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(x=>!x.name.StartsWith("__preview__"));
                if(!asset)throw new InvalidOperationException("사용 가능한 애니메이션 클립 없음: "+path);
                Debug.Log("GRANDFATHER_CLIP: "+name+" duration="+asset.length+" loop="+asset.isLooping);
                return asset;
            }
            var idle=ImportClip("Idle");var walk=ImportClip("Walk");
            const string controllerPath="Assets/Art/Animation/Grandfather.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var layers=controller.layers;layers[0].defaultWeight=1;controller.layers=layers;
            var machine=controller.layers[0].stateMachine;
            foreach(var state in machine.states)machine.RemoveState(state.state);
            controller.parameters=new[]{new AnimatorControllerParameter{name="Walk",type=AnimatorControllerParameterType.Trigger},new AnimatorControllerParameter{name="Idle",type=AnimatorControllerParameterType.Trigger},new AnimatorControllerParameter{name="WalkRate",type=AnimatorControllerParameterType.Float,defaultFloat=1}};
            var idleState=machine.AddState("Idle");idleState.motion=idle;
            var walkState=machine.AddState("Walk");walkState.motion=walk;machine.defaultState=idleState;
            walkState.speedParameter="WalkRate";walkState.speedParameterActive=true;
            var start=idleState.AddTransition(walkState);start.hasExitTime=false;start.duration=.15f;start.AddCondition(AnimatorConditionMode.If,0,"Walk");
            var stop=walkState.AddTransition(idleState);stop.hasExitTime=false;stop.duration=.15f;stop.AddCondition(AnimatorConditionMode.If,0,"Idle");
            EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();return controller;
        }
        public static void RefreshGrandfather()
        {
            var cat=AssetDatabase.LoadAssetAtPath<AssetCatalog>(CatalogPath);
            Array.Find(cat.prefabs,x=>x.id=="grandfather").asset=ImportGrandfather();
            EditorUtility.SetDirty(cat);AssetDatabase.SaveAssets();PrototypeChecks.Run();
            PresentationChecks.Run();
        }
        public static void RefreshAndBuild(){RefreshGrandfather();Build();}
        public static void GenerateAndBuild(){Generate();Build();}
        static GameObject CreateHouse()
        {
            var root=new GameObject("Storm damaged shed");
            void Part(string name,Vector3 p,Vector3 scale,string material,float tilt=0)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root.transform,false);
                go.transform.localPosition=p;go.transform.localScale=scale;go.transform.localRotation=Quaternion.Euler(0,0,tilt);
                go.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/"+material+".mat");
            }
            Part("Foundation",new Vector3(0,-.44f,0),new Vector3(1,.1f,1),"rock");
            Part("Walls",new Vector3(0,-.06f,0),new Vector3(.88f,.68f,.86f),"cloth");
            Part("Roof left",new Vector3(-.23f,.36f,0),new Vector3(.62f,.06f,1.08f),"wood",32);
            Part("Roof right",new Vector3(.23f,.36f,0),new Vector3(.62f,.06f,1.08f),"wood",-32);
            Part("Door",new Vector3(0,-.15f,-.44f),new Vector3(.2f,.48f,.025f),"dark");
            Part("Window",new Vector3(.29f,.02f,-.44f),new Vector3(.17f,.18f,.025f),"node");
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/Prototype/Shed.prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
        }
        public static void Build()
        {
            if(!File.Exists(ScenePath))Generate();else PrototypeChecks.Run();
            string output="Builds/Windows/GrandpaRobot.exe";
            var args=Environment.GetCommandLineArgs();for(int i=0;i<args.Length-1;i++)if(args[i]=="-buildOutput")output=args[i+1];
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Windows build failed: "+report.summary.result);
            Debug.Log("PROTOTYPE_BUILD_OK: "+report.summary.totalSize);
        }
    }
}
