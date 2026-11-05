using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class CottageSceneBuilder
{
    private const string OutskirtsArtPath = AssetFolder + "/Outskirts Art.asset";
    private const string OutskirtsPrefabPath = AssetFolder + "/Cottage Outskirts.prefab";
    private const string OutskirtsName = "06 Orchard, field and meadow";

    [MenuItem("Tools/Cottage/Add Outskirts and Free Camera")]
    public static void AddOutskirtsAndCamera()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.OpenScene(ScenePath);
        if (scene.GetRootGameObjects().Any(obj => obj.name == OutskirtsName))
        {
            Debug.Log("The outskirts are already in the Cottage scene.");
            return;
        }

        var builder = new Builder(OutskirtsArtPath, 518);
        GameObject outskirts = builder.BuildOutskirts();
        AssetDatabase.SaveAssets();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(outskirts, OutskirtsPrefabPath);
        if (prefab == null)
            throw new InvalidOperationException("Could not save the Cottage outskirts prefab.");

        Object.DestroyImmediate(outskirts);
        PrefabUtility.InstantiatePrefab(prefab, scene).name = OutskirtsName;
        Camera.main.gameObject.AddComponent<QuarterViewCameraPan>();
        AssetDatabase.SaveAssets();
        Builder.SaveOutskirtsScene(scene);

        Scene ending = EditorSceneManager.OpenScene(EndingScenePath);
        PrefabUtility.InstantiatePrefab(prefab, ending).name = OutskirtsName;
        Builder.SaveOutskirtsScene(ending);
        EditorSceneManager.OpenScene(ScenePath);
    }

    private sealed partial class Builder
    {
        public GameObject BuildOutskirts()
        {
            Transform root = Group(OutskirtsName);
            timber = Paint("Orchard bark", "#695341");
            earth = Paint("Outskirts footpath", "#A08E65");
            stone = Palette("Fieldstone", "#6B7060", "#858878", "#9E9981", "#79796A");
            leaves = Palette("Outskirts foliage", "#47603C", "#607B43", "#71864B", "#869553", "#9AA161");
            Material[] bloom = Palette("Meadow flowers", "#E3CF90", "#E6DEBD", "#A69ABA");
            Material fruit = Paint("Orchard apples", "#AD6854");

            Transform orchard = Group("Left orchard and stone boundary", root);
            for (int row = 0; row < 2; row++)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 position = V(-24.4f + row * 4.4f + Range(-0.35f, 0.35f), 0,
                        -10.4f + i * 3.2f + Range(-0.4f, 0.4f));
                    float height = Range(0.64f, 0.93f);
                    Tree(position, height);
                    for (int apple = 0; apple < 11; apple++)
                        Scatter(PrimitiveType.Sphere, position + V(Range(-1, 1), Range(2.45f, 3.75f), Range(-1, 1)) * height,
                            V(0.22f, 0.21f, 0.22f), Quaternion.identity, fruit);
                }
            }

            StoneWall(V(-19.4f, 0, -2.3f), V(-17.5f, 0, -9.5f));
            StoneWall(V(-16.7f, 0, -12.7f), V(-15.3f, 0, -18.4f));
            MeadowPatch(V(-20.5f, 0, -14.0f), 3.5f, 2.8f, 30, bloom);
            MeadowPatch(V(-22.7f, 0, -6.0f), 4.4f, 4.1f, 34, bloom);
            Flush(orchard);

            Transform meadow = Group("Front lane and wildflower verges", root);
            Mesh lane = MakeYardPath("Lane into the village", 1.92f,
                V(-0.45f, 0.004f, -15.65f), V(-0.8f, 0.004f, -18.25f), V(-2.6f, 0.004f, -21.9f),
                V(-3.5f, 0.004f, -25.2f), V(-6.3f, 0.004f, -30.1f), V(-9.2f, 0.004f, -34.3f));
            PlacePath(meadow, lane, earth);
            MeadowPatch(V(-10.8f, 0, -21.5f), 3.1f, 2.2f, 34, bloom);
            MeadowPatch(V(-6.4f, 0, -24.9f), 2.1f, 3.7f, 32, bloom);
            MeadowPatch(V(1.25f, 0, -25.7f), 2.8f, 3.2f, 28, bloom);
            MeadowPatch(V(6.45f, 0, -22.9f), 2.9f, 2.6f, 35, bloom);
            MeadowPatch(V(12.8f, 0, -19.15f), 3.2f, 2.7f, 35, bloom);
            MeadowPatch(V(18.55f, 0, -15.9f), 2.4f, 2.4f, 25, bloom);
            MeadowPatch(V(23.0f, 0, -10.9f), 2.2f, 3.5f, 30, bloom);
            StoneWall(V(-11.85f, 0, -19.9f), V(-7.3f, 0, -21.55f), 0.63f);
            StoneWall(V(8.65f, 0, -23.45f), V(13.7f, 0, -20.8f), 0.62f);
            Boulder(V(-10.5f, 0, -24.25f), 1.65f);
            Boulder(V(4.45f, 0, -23.6f), 1.46f);
            Boulder(V(16.0f, 0, -17.1f), 1.3f);
            Boulder(V(22.1f, 0, -12.15f), 1.4f);
            Tree(V(-12.1f, 0, -23.0f), 0.9f);
            Tree(V(22.9f, 0, -13.5f), 0.85f);
            Tree(V(24.8f, 0, -6.5f), 1.04f);
            Flush(meadow);

            Transform crops = Group("Kitchen garden beside the rice fields", root);
            Material soil = Paint("Kitchen garden tilled soil", "#80644C");
            Material cabbage = Paint("Kitchen garden leafy crops", "#8A9B54");
            Vector3 plot = V(7.1f, 0, 10.8f);
            for (int row = 0; row < 5; row++)
            {
                float x = (row - 2) * 0.84f;
                Scatter(PrimitiveType.Sphere, plot + V(x, 0.045f, 0), V(0.74f, 0.23f, 5.55f),
                    Quaternion.identity, soil);
                for (int i = 0; i < 11; i++)
                {
                    Vector3 crop = plot + V(x + Range(-0.09f, 0.09f), 0.2f, -2.38f + i * 0.48f);
                    Scatter(PrimitiveType.Sphere, crop, V(0.32f, 0.26f, 0.31f), Quaternion.identity,
                        i % 3 == 0 ? cabbage : leaves[2]);
                }
            }
            StoneWall(plot + V(-2.68f, 0, 3.08f), plot + V(2.68f, 0, 3.08f), 0.56f);
            MeadowPatch(V(10.4f, 0, 15.3f), 3.2f, 1.55f, 25, bloom);
            Flush(crops);
            return root.gameObject;
        }

        private void MeadowPatch(Vector3 center, float radiusX, float radiusZ, int count, Material[] bloom)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Range(0, 2 * Mathf.PI);
                float radius = Mathf.Sqrt(Range(0, 1));
                Vector3 point = center + V(Mathf.Cos(angle) * radiusX, 0, Mathf.Sin(angle) * radiusZ) * radius;
                float height = Range(0.19f, 0.53f);
                GrassTuft(point, height, false);
                if (i % 3 == 0)
                    Scatter(PrimitiveType.Sphere, point + V(0, height * 0.85f, 0), V(0.15f, 0.09f, 0.14f),
                        Quaternion.identity, Pick(bloom));
            }
        }

        private void StoneWall(Vector3 start, Vector3 end, float height = 0.81f)
        {
            Vector3 direction = end - start;
            int count = Mathf.CeilToInt(direction.magnitude / 0.56f);
            Quaternion rotation = Quaternion.LookRotation(direction);
            for (int i = 0; i < count; i++)
            {
                for (int row = 0; row < 3; row++)
                {
                    float t = (i + (row % 2 == 0 ? 0.43f : 0.57f)) / count;
                    Vector3 position = Vector3.Lerp(start, end, t) + V(0, height * (row + 0.47f) / 3f, 0);
                    Scatter(PrimitiveType.Sphere, position, V(Range(0.39f, 0.52f), height * 0.44f, Range(0.57f, 0.68f)),
                        rotation * Quaternion.Euler(Range(-8, 8), Range(-10, 10), 0), Pick(stone));
                }
            }
        }

        public static void SaveOutskirtsScene(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save " + scene.path);
        }
    }
}
