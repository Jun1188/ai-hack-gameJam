using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class CottageSceneBuilder
{
    private const string SwingArtPath = AssetFolder + "/Swing Art.asset";
    private const string EndingScenePath = "Assets/Scenes/CottageEnding.unity";
    private const string SwingName = "04 Pondside bench swing";
    private static readonly Vector3 SwingPosition = new Vector3(14.1f, 0f, -6.45f);
    private static readonly Vector3 SwingFacing = new Vector3(-0.36f, 0f, -0.93f).normalized;

    [MenuItem("Tools/Cottage/Add Bench Swing and Sunset Scene")]
    public static void AddSwingAndEnding()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || System.IO.File.Exists(EndingScenePath) ||
            scene.GetRootGameObjects().Any(obj => obj.name == SwingName))
        {
            Debug.LogWarning("Open Cottage.unity without the bench swing to generate the sunset scene.");
            return;
        }

        var builder = new Builder(SwingArtPath);
        builder.BuildSwing();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save the Cottage scene.");

        builder.ConfigureEnding();
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene, EndingScenePath, true))
            throw new InvalidOperationException("Could not save the Cottage ending scene.");

        EditorSceneManager.OpenScene(ScenePath);
    }

    private sealed partial class Builder
    {
        public void BuildSwing()
        {
            Transform frame = Group(SwingName);
            Material oak = Paint("Swing frame oak", "#65462F");
            Material honey = Paint("Swing seat cedar", "#A37248");
            Material edge = Paint("Swing edge cedar", "#865B39");
            Material metal = Paint("Swing weathered iron", "#565554", 0.37f);
            Material footing = Paint("Swing footing stone", "#9B9179");
            Material gravel = Paint("Swing compacted gravel", "#9C906E");

            Shape(frame, "Gravel under swing", PrimitiveType.Cylinder, V(0, 0.004f, 0),
                V(3.8f, 0.012f, 2.27f), gravel, false);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int end = -1; end <= 1; end += 2)
                {
                    Vector3 foot = V(side * 1.47f, 0.09f, end * 0.78f);
                    Box(frame, "Stone footing", foot, V(0.35f, 0.18f, 0.35f), footing);
                    TimberBeam(frame, "A-frame leg", foot, V(side * 1.45f, 2.25f, end * 0.025f),
                        0.17f, oak);
                }
                Box(frame, "Frame cross brace", V(side * 1.47f, 0.86f, 0),
                    V(0.13f, 0.13f, 1.07f), oak);
                TimberBeam(frame, "Top knee brace", V(side * 1.43f, 1.77f, 0),
                    V(side * 0.98f, 2.31f, 0), 0.1f, oak);
            }
            Box(frame, "Overhead crossbeam", V(0, 2.33f, 0), V(3.32f, 0.19f, 0.2f), oak);

            Transform pivot = Group("Swing hinge", frame);
            pivot.position = V(0, 2.22f, 0);
            pivot.gameObject.AddComponent<PorchSwingMotion>();
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 upper = V(side * 0.88f, 2.2f, 0);
                Shape(frame, "Iron hinge", PrimitiveType.Cylinder, upper + V(0, 0.028f, 0),
                    V(0.15f, 0.046f, 0.15f), metal, false, Quaternion.Euler(0, 0, 90));
                foreach (float z in new[] { -0.21f, 0.26f })
                {
                    Vector3 lower = V(side * 0.98f, 0.56f, z);
                    Vector3 cable = upper - lower;
                    Shape(pivot, "Suspension rod", PrimitiveType.Cylinder, (upper + lower) * 0.5f,
                        V(0.038f, cable.magnitude * 0.5f, 0.038f), metal, false,
                        Quaternion.FromToRotation(Vector3.up, cable));
                }
                Box(pivot, "Seat side rail", V(side * 0.96f, 0.46f, 0.035f), V(0.12f, 0.1f, 0.57f), edge);
                TimberBeam(pivot, "Backrest support", V(side * 0.88f, 0.47f, -0.24f),
                    V(side * 0.88f, 1.02f, -0.34f), 0.081f, edge);
                TimberBeam(pivot, "Armrest post", V(side * 1.01f, 0.49f, 0.24f),
                    V(side * 1.01f, 0.72f, 0.21f), 0.068f, edge);
                Box(pivot, "Armrest", V(side * 1.01f, 0.74f, -0.02f), V(0.13f, 0.065f, 0.54f), honey);
            }
            for (int i = 0; i < 5; i++)
                Box(pivot, "Seat slat", V(0, 0.51f, -0.21f + i * 0.119f),
                    V(2.03f, 0.065f, 0.109f), i % 2 == 0 ? honey : edge);
            for (int i = 0; i < 4; i++)
                Box(pivot, "Backrest slat", V(0, 0.66f + i * 0.102f, -0.247f - i * 0.018f),
                    V(1.99f, 0.092f, 0.062f), honey, true, Quaternion.Euler(-10, 0, 0));

            Transform seats = Group("Seat anchors", pivot);
            Anchor(seats, "grandfather_seat", V(-0.47f, 0.55f, 0.025f));
            Anchor(seats, "robot_seat", V(0.47f, 0.55f, 0.025f));
            frame.SetPositionAndRotation(SwingPosition, Quaternion.LookRotation(SwingFacing, Vector3.up));
        }

        private void TimberBeam(Transform parent, string name, Vector3 from, Vector3 to, float width,
            Material material)
        {
            Vector3 direction = to - from;
            Box(parent, name, (from + to) * 0.5f, V(width, direction.magnitude, width),
                material, true, Quaternion.FromToRotation(Vector3.up, direction));
        }

        public void ConfigureEnding()
        {
            Vector3 side = Vector3.Cross(Vector3.up, SwingFacing);
            Camera camera = Camera.main;
            camera.name = "Sunset Camera";
            camera.transform.position = SwingPosition - SwingFacing * 5.25f - side * 1.08f + V(0, 1.99f, 0);
            camera.transform.LookAt(SwingPosition + SwingFacing * 7.25f + V(0, 1.38f, 0));
            camera.fieldOfView = 42f;

            Light sun = RenderSettings.sun;
            Vector3 sunDirection = (SwingFacing - side * 0.34f + V(0, 0.12f, 0)).normalized;
            sun.name = "Setting sun";
            sun.transform.rotation = Quaternion.LookRotation(-sunDirection, Vector3.up);
            sun.color = new Color(1f, 0.67f, 0.36f);
            sun.intensity = 1.75f;
            RenderSettings.ambientSkyColor = new Color(0.67f, 0.58f, 0.57f);
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.43f, 0.41f);
            RenderSettings.ambientGroundColor = new Color(0.29f, 0.23f, 0.24f);
            RenderSettings.fogColor = new Color(0.77f, 0.57f, 0.49f);
            RenderSettings.fogDensity = 0.012f;

            var sky = new Material(Shader.Find("Cottage/Sunset Sky")) { name = "Sunset sky" };
            sky.SetVector("_SunDirection", new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0));
            SaveAsset(sky);
            RenderSettings.skybox = sky;

            // Leave the viewing corridor open in the ending composition.
            Transform farmyard = GameObject.Find(FarmyardRoot).transform;
            Transform daytimeVerge = farmyard.Find("Wild meadow and scattered rocks");
            daytimeVerge.gameObject.SetActive(false);
            timber = Paint("Barn structural oak", "#544237");
            stone = Palette("Creek stone", "#74796C", "#858477", "#9A9685", "#667365");
            leaves = Palette("Meadow foliage", "#466037", "#597C40", "#738C47", "#869C56", "#A5A65F");
            BuildWildVerges(Group("Sunset meadow"), false);

            Transform horizon = Group("Distant sunset hills");
            Material[] hillColors = Palette("Sunset hills", "#65535B", "#775C5E", "#806264");
            for (int i = 0; i < 13; i++)
            {
                float lateral = (i - 6) * 5.5f;
                Vector3 position = SwingPosition + SwingFacing * Range(34f, 46f) + side * lateral;
                position.y = Range(-1.7f, -0.85f);
                Scatter(PrimitiveType.Sphere, position, V(Range(11, 17), Range(4.8f, 8.4f), Range(9, 16)),
                    Quaternion.Euler(0, Range(0, 180), 0), Pick(hillColors));
            }
            Flush(horizon);
        }
    }
}
