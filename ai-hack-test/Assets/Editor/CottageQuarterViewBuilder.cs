using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class CottageSceneBuilder
{
    private const string LayoutArtPath = AssetFolder + "/Quarter View Layout.asset";
    private const string LayoutRoot = "05 Front yard paths";

    [MenuItem("Tools/Cottage/Arrange Quarter View Yard")]
    public static void ArrangeQuarterViewYard()
    {
        if (!System.IO.File.Exists(ScenePath) || !System.IO.File.Exists(EndingScenePath))
        {
            Debug.LogWarning("Both Cottage scenes are required to arrange the yard.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var builder = new Builder(LayoutArtPath);
        builder.ArrangeYardScenes();
    }

    private sealed partial class Builder
    {
        public void ArrangeYardScenes()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            if (scene.GetRootGameObjects().Any(obj => obj.name == LayoutRoot))
            {
                Debug.Log("The quarter-view yard is already arranged.");
                return;
            }

            Material gravel = Paint("Front yard compacted earth", "#A29370");
            Material track = Paint("Worn gravel tracks", "#B09C79");
            Mesh driveway = MakeYardPath("Driveway to equipment barn", 2.4f,
                V(0, 0.006f, -15.1f), V(-4.7f, 0.006f, -15.55f), V(-9.6f, 0.006f, -15.35f),
                V(-11.65f, 0.006f, -14.15f), V(-11.65f, 0.006f, -12.65f));
            Mesh westWalk = MakeYardPath("Walk to the well", 1.25f,
                V(-4.45f, 0.007f, -15.51f), V(-5.08f, 0.007f, -13.75f), V(-5.42f, 0.007f, -11.3f),
                V(-5.15f, 0.007f, -9.1f));
            Mesh pondWalk = MakeYardPath("Walk around the garden to the pond", 1.16f,
                V(0.45f, 0.006f, -15.05f), V(3.25f, 0.006f, -14.33f), V(6.45f, 0.006f, -12.56f),
                V(7.1f, 0.006f, -10.26f), V(8.45f, 0.006f, -8.42f), V(9.43f, 0.006f, -6.3f),
                V(10.3f, 0.006f, -6.5f), V(11.1f, 0.006f, -6.75f));
            AssetDatabase.SaveAssets();

            ArrangeYard(scene, driveway, westWalk, pondWalk, gravel, track);
            Camera camera = Camera.main;
            camera.transform.position = V(12.0f, 16.0f, -22.2f);
            camera.transform.LookAt(V(0.2f, 0.2f, -4.9f));
            camera.orthographic = true;
            camera.orthographicSize = 13.6f;
            RenderSettings.fogDensity = 0.0075f;
            SaveLayoutScene(scene);

            Scene ending = EditorSceneManager.OpenScene(EndingScenePath);
            ArrangeYard(ending, driveway, westWalk, pondWalk, gravel, track);
            Camera.main.transform.position += SwingYardOffset;
            SaveLayoutScene(ending);
            EditorSceneManager.OpenScene(ScenePath);
        }

        private static readonly Vector3 SwingYardOffset = new Vector3(-2.35f, 0, 0.35f);

        private static void ArrangeYard(Scene scene, Mesh driveway, Mesh westWalk, Mesh pondWalk,
            Material gravel, Material track)
        {
            Transform garden = Root(scene, "02 Garden and farmyard");
            Transform farmyard = Root(scene, FarmyardRoot);
            Transform anchors = Root(scene, "01 Cottage | 10.4 x 7.4 m").Find("Repair anchors");

            MoveFacility(garden.Find("Storm damaged well"), V(2.35f, 0, -5.95f),
                anchors.Find("well_rim_gap"), anchors.Find("well_pulley"));
            MoveFacility(garden.Find("Storm damaged timber storehouse"), V(2.0f, 0, -2.48f),
                anchors.Find("storehouse_post_south_east"));
            MoveFacility(farmyard.Find("Equipment barn | 5.8 x 5.6 m"), V(-18.8f, 0, -16.5f));
            Object.DestroyImmediate(farmyard.Find("Barn approach").gameObject);
            MoveFacility(Root(scene, SwingName), SwingYardOffset);

            Transform paths = Group(LayoutRoot);
            PlacePath(paths, driveway, track);
            PlacePath(paths, westWalk, gravel);
            PlacePath(paths, pondWalk, gravel);
        }

        private static Transform Root(Scene scene, string name) =>
            scene.GetRootGameObjects().Single(obj => obj.name == name).transform;

        private static void MoveFacility(Transform facility, Vector3 offset, params Transform[] anchors)
        {
            facility.position += offset;
            foreach (Transform anchor in anchors)
                anchor.position += offset;
        }

        private Mesh MakeYardPath(string name, float width, params Vector3[] points)
        {
            const int samplesPerSegment = 10;
            int samples = (points.Length - 1) * samplesPerSegment + 1;
            var centers = new Vector3[samples];
            for (int i = 0; i < samples; i++)
            {
                int segment = Mathf.Min(i / samplesPerSegment, points.Length - 2);
                float t = (i - segment * samplesPerSegment) / (float)samplesPerSegment;
                Vector3 a = points[Mathf.Max(0, segment - 1)];
                Vector3 b = points[segment];
                Vector3 c = points[segment + 1];
                Vector3 d = points[Mathf.Min(points.Length - 1, segment + 2)];
                centers[i] = 0.5f * (2 * b + (-a + c) * t
                    + (2 * a - 5 * b + 4 * c - d) * (t * t)
                    + (-a + 3 * b - 3 * c + d) * (t * t * t));
            }

            var vertices = new Vector3[samples * 2];
            var triangles = new int[(samples - 1) * 6];
            for (int i = 0; i < samples; i++)
            {
                Vector3 forward = (centers[Mathf.Min(i + 1, samples - 1)]
                    - centers[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, forward);
                float halfWidth = width * (0.48f + 0.05f * Mathf.Sin(i * 0.81f));
                vertices[i * 2] = centers[i] - side * halfWidth;
                vertices[i * 2 + 1] = centers[i] + side * halfWidth;
                if (i == samples - 1)
                    continue;

                int vertex = i * 2;
                int triangle = i * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }

            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            SaveAsset(mesh);
            return mesh;
        }

        private static void PlacePath(Transform parent, Mesh mesh, Material material)
        {
            Transform path = Group(mesh.name, parent);
            path.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            path.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void SaveLayoutScene(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save the new yard layout in " + scene.path);
        }
    }
}
