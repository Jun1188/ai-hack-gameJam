using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CafeSim
{
    public static class CafeCustomerMvpBuilder
    {
        public const string ScenePath = "Assets/Scenes/CafeCustomerMvp.unity";

        [UnityEditor.MenuItem("Tools/Cafe/Create Customer MVP Scene")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Customer MVP only");
            var session = root.AddComponent<CafeSession>();
            session.menuData = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/CafeSim/Data/menu.json");
            session.personaData = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/CafeSim/Data/personas.json");
            session.model = root.AddComponent<LocalCafeModel>();
            session.mergedCafe = true;
            session.queueSettings = new CafeQueueSettings { singleOrderingCustomer = true, arrivalSeconds = 1000,
                orderSeconds = 1, activeTimeoutSeconds = 600, chatterSeconds = 10, offMenuChance = 0 };
            session.reactionReadSeconds = 2; session.ratingReadSeconds = 30;
            session.entrance = Marker("Arrival", new Vector3(2.5f, 0, 3.8f));
            session.servicePoint = Marker("Service point", new Vector3(2.5f, 0, 0));
            session.servicePoint.rotation = Quaternion.Euler(0, 180, 0);
            session.customer = Marker("Unused legacy customer", Vector3.zero);
            session.customer.gameObject.SetActive(false);
            var counter = root.AddComponent<CafeServeCounter>();
            counter.session = session;
            var target = new GameObject("Serving target").AddComponent<BoxCollider>();
            target.transform.position = new Vector3(2.5f, .9f, -.5f); target.size = new Vector3(1.3f, 1.8f, .5f);
            counter.serveTarget = target;
            var mvp = root.AddComponent<CafeCustomerMvp>(); mvp.session = session; mvp.counter = counter;
            mvp.allPersonas = session.personaData;
            mvp.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Cafe/Menu/NotoSansKR.ttf");
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.transform.position = new Vector3(-.5f, 1.65f, -5.5f);
            camera.transform.LookAt(new Vector3(.35f, 1f, .7f)); camera.fieldOfView = 40;
            camera.backgroundColor = new Color(.20f, .25f, .28f); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("Soft light").AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 1.4f; light.transform.rotation = Quaternion.Euler(42, -30, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.name = "MVP floor";
            floor.transform.position = new Vector3(2.5f, -.01f, 1.5f); floor.transform.localScale = Vector3.one * 1.5f;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }
        static Transform Marker(string name, Vector3 position)
        { var t = new GameObject(name).transform; t.position = position; return t; }
    }
}
