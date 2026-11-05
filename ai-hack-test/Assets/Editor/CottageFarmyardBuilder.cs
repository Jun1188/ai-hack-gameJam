using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class CottageSceneBuilder
{
    private const string FarmyardArtPath = AssetFolder + "/Farmyard Art.asset";
    private const string FarmyardRoot = "03 Equipment barn and pond";

    [MenuItem("Tools/Cottage/Add Equipment Barn and Pond")]
    public static void AddFarmyard()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogWarning("Open Cottage.unity before adding the farmyard.");
            return;
        }

        if (scene.GetRootGameObjects().Any(obj => obj.name == FarmyardRoot))
        {
            Debug.Log("This Cottage scene already contains the farmyard.");
            return;
        }

        new Builder(FarmyardArtPath, 276).BuildFarmyard(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save the updated Cottage scene.");
    }

    private sealed partial class Builder
    {
        public void BuildFarmyard(Scene scene)
        {
            Transform meadow = scene.GetRootGameObjects().Single(obj => obj.name == "00 Meadow and hills").transform;
            Transform originalGround = meadow.Find("Walkable meadow");
            if (originalGround == null)
                throw new InvalidOperationException("The original Cottage meadow is required to build the pond.");

            grass = originalGround.GetComponent<Renderer>().sharedMaterial;
            timber = Paint("Barn structural oak", "#544237");
            earth = Paint("Barn gravel", "#A28B6D");
            stone = Palette("Creek stone", "#74796C", "#858477", "#9A9685", "#667365");
            leaves = Palette("Meadow foliage", "#466037", "#597C40", "#738C47", "#869C56", "#A5A65F");

            Transform farmyard = Group(FarmyardRoot);
            BuildEquipmentBarn(farmyard);
            BuildPond(farmyard, meadow, originalGround);
            BuildWildVerges(farmyard);

            Camera camera = scene.GetRootGameObjects().Select(obj => obj.GetComponent<Camera>())
                .FirstOrDefault(component => component != null && component.CompareTag("MainCamera"));
            if (camera != null)
            {
                camera.transform.position = V(12.2f, 9.3f, -28.8f);
                camera.transform.LookAt(V(2.6f, 1.65f, -0.8f));
                camera.fieldOfView = 43f;
            }
        }

        private void BuildEquipmentBarn(Transform parent)
        {
            Transform barn = Group("Equipment barn | 5.8 x 5.6 m", parent);
            Vector3 origin = V(7.15f, 0f, 6.7f);
            Material[] boards = Palette("Barn siding", "#80604A", "#8F6A4E", "#9A7556", "#765743");
            Material[] roof = Palette("Aged red roof", "#865748", "#95604C", "#A46C54", "#764B40");
            Material hay = Paint("Dry straw", "#B49B59");
            Material steel = Paint("Tool iron", "#555E5D");
            Material pale = Paint("Tool handles", "#B18C5E");

            Box(barn, "Gravel slab", origin + V(0, -0.025f, 0), V(5.8f, 0.12f, 5.6f), earth);
            Box(barn, "Rear wall", origin + V(0, 1.56f, 2.75f), V(5.7f, 3.05f, 0.2f), timber);
            foreach (int side in new[] { -1, 1 })
            {
                Box(barn, "Side wall", origin + V(side * 2.84f, 1.56f, 0), V(0.16f, 3.05f, 5.6f), timber);
                Box(barn, "Front side panel", origin + V(side * 2.35f, 1.55f, -2.75f),
                    V(1.08f, 3.02f, 0.19f), timber);
                Box(barn, "Entrance post", origin + V(side * 1.91f, 1.52f, -2.84f),
                    V(0.22f, 3.04f, 0.26f), timber);
                Box(barn, "Post footing", origin + V(side * 1.91f, 0.12f, -2.85f),
                    V(0.33f, 0.24f, 0.35f), stone[1]);

                for (int i = 0; i < 25; i++)
                {
                    float along = -2.68f + i * 0.224f;
                    Scatter(PrimitiveType.Cube, origin + V(side * 2.94f, 1.57f, along),
                        V(0.055f, 3f, 0.212f), Quaternion.identity, Pick(boards));
                }

                for (int i = 0; i < 5; i++)
                    Scatter(PrimitiveType.Cube, origin + V(side * (1.88f + i * 0.21f), 1.57f, -2.865f),
                        V(0.201f, 3f, 0.055f), Quaternion.identity, Pick(boards));
            }

            Box(barn, "Clear entrance beam", origin + V(0, 3.03f, -2.84f),
                V(5.81f, 0.22f, 0.27f), timber);
            foreach (int end in new[] { -1, 1 })
            {
                Triangle(barn, "Timber gable", origin + V(-2.87f, 3.03f, end * 2.86f),
                    origin + V(2.87f, 3.03f, end * 2.86f), origin + V(0, 4.36f, end * 2.86f), boards[0]);
                for (float x = -2.68f; x <= 2.7f; x += 0.21f)
                {
                    float height = 1.24f * (1 - Mathf.Abs(x) / 2.87f);
                    Scatter(PrimitiveType.Cube, origin + V(x, 3.12f + height * 0.5f, end * 2.88f),
                        V(0.198f, height, 0.05f), Quaternion.identity, Pick(boards));
                }
            }
            Flush(barn);

            slate = roof;
            Vector3 ridge = origin + V(0, 4.38f, 0);
            RoofSlope(barn, "West roof", ridge, origin + V(-3.16f, 2.95f, 0), Vector3.forward, 6.28f, false);
            RoofSlope(barn, "East roof", ridge, origin + V(3.16f, 2.95f, 0), Vector3.forward, 6.28f, false);
            Box(barn, "Ridge cap", ridge + V(0, 0.11f, 0), V(0.23f, 0.15f, 6.32f), roof[3]);

            Transform storage = Group("Equipment and storage", barn);
            Box(storage, "Tool rack", origin + V(-2.62f, 1.28f, -0.65f), V(0.16f, 0.17f, 1.54f), timber);
            for (int i = 0; i < 3; i++)
            {
                Vector3 tool = origin + V(-2.47f, 0.83f, -1.12f + i * 0.38f);
                Box(storage, "Tool handle", tool, V(0.044f, 1.25f, 0.045f), pale, false);
                Box(storage, "Iron tool head", tool + V(0, -0.55f, 0),
                    V(0.065f, i == 1 ? 0.11f : 0.21f, 0.24f), steel, false);
            }
            for (int i = 0; i < 3; i++)
                Box(storage, "Straw bale", origin + V(2.15f, 0.31f + i / 2 * 0.53f, 0.95f + i % 2 * 0.79f),
                    V(0.9f, 0.51f, 0.72f), hay);
            Box(storage, "Back workbench", origin + V(-1.41f, 0.91f, 2.26f), V(1.92f, 0.14f, 0.7f), boards[1]);
            for (int side = -1; side <= 1; side += 2)
                Box(storage, "Workbench leg", origin + V(-1.41f + side * 0.75f, 0.45f, 2.26f),
                    V(0.16f, 0.86f, 0.53f), timber);
            Box(storage, "Storage crate", origin + V(-2.2f, 0.36f, 1.2f), V(0.65f, 0.59f, 0.74f), boards[2]);

            Transform drive = Group("Barn approach", parent);
            for (int i = 0; i < 12; i++)
            {
                float t = i / 11f;
                Vector3 position = Vector3.Lerp(V(6.0f, 0, -4.7f), origin + V(0, 0, -2.65f), t);
                for (int side = -1; side <= 1; side += 2)
                    Scatter(PrimitiveType.Cylinder, position + V(side * 0.72f, 0.002f, 0),
                        V(Range(0.51f, 0.84f), 0.006f, 1.48f), Quaternion.identity, earth);
            }
            Flush(drive);
        }

        private void BuildPond(Transform parent, Transform meadow, Transform originalGround)
        {
            Transform pond = Group("Pond and wetland", parent);
            Vector3 center = V(12.15f, 0, -11.2f);
            const float halfWidth = 4.9f;
            const float halfDepth = 4.2f;
            const float waterHeight = -0.42f;
            float left = center.x - halfWidth;
            float right = center.x + halfWidth;
            float front = center.z - halfDepth;
            float rear = center.z + halfDepth;

            Vector3 minimum = originalGround.position - originalGround.localScale * 0.5f;
            Vector3 maximum = originalGround.position + originalGround.localScale * 0.5f;
            GroundStrip(meadow, minimum.x, left, minimum.z, maximum.z, minimum.y);
            GroundStrip(meadow, right, maximum.x, minimum.z, maximum.z, minimum.y);
            GroundStrip(meadow, left, right, minimum.z, front, minimum.y);
            GroundStrip(meadow, left, right, rear, maximum.z, minimum.y);
            Object.DestroyImmediate(originalGround.gameObject);

            var angles = new SortedSet<float>();
            for (int i = 0; i < 56; i++)
                angles.Add(2 * Mathf.PI * i / 56);
            foreach (int x in new[] { -1, 1 })
                foreach (int z in new[] { -1, 1 })
                    angles.Add(Mathf.Repeat(Mathf.Atan2(z * halfDepth, x * halfWidth), 2 * Mathf.PI));

            var boundary = new List<Vector3>();
            var shore = new List<Vector3>();
            var shallow = new List<Vector3>();
            var bottom = new List<Vector3>();
            var waterline = new List<Vector3>();
            foreach (float angle in angles)
            {
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                float rim = 1f + 0.06f * Mathf.Sin(3 * angle + 0.4f) + 0.05f * Mathf.Cos(5 * angle);
                float border = 1 / Mathf.Max(Mathf.Abs(x) / halfWidth, Mathf.Abs(z) / halfDepth);
                Vector3 shoreOffset = V(x * 4.28f * rim, 0, z * 3.57f * rim);
                boundary.Add(center + V(x * border, 0, z * border));
                shore.Add(center + shoreOffset);
                shallow.Add(center + shoreOffset * 0.84f + V(0, -0.6f, 0));
                bottom.Add(center + shoreOffset * 0.16f + V(0, -0.92f, 0));
                waterline.Add(center + shoreOffset * 0.886f + V(0, waterHeight, 0));
            }

            Material bank = Paint("Wet clay bank", "#776853");
            Material pondWater = Paint("Pond blue green", "#467C7F", 0.8f);
            Surface(pond, "Meadow lip", boundary, shore, grass, true);
            Surface(pond, "Sloped clay banks", shore, shallow, bank, true);
            Surface(pond, "Underwater bank", shallow, bottom, bank, true);
            var centerFloor = Enumerable.Repeat(center + V(0, -0.92f, 0), bottom.Count).ToList();
            Surface(pond, "Pond bottom", bottom, centerFloor, bank, true);
            var centerWater = Enumerable.Repeat(center + V(0, waterHeight, 0), waterline.Count).ToList();
            Surface(pond, "Water surface", waterline, centerWater, pondWater, false);

            Material ripple = Paint("Water highlights", "#75A7A3", 0.72f);
            Material lily = Paint("Floating leaves", "#5A813F");
            for (int i = 0; i < 24; i++)
            {
                Vector3 position = center + V(Range(-2.85f, 2.8f), waterHeight + 0.008f, Range(-1.85f, 1.85f));
                Scatter(PrimitiveType.Cylinder, position, V(Range(0.2f, 0.92f), 0.005f, Range(0.022f, 0.05f)),
                    Quaternion.Euler(0, -15, 0), ripple);
            }
            for (int i = 0; i < 20; i++)
            {
                Vector3 position = center + V(Range(-2.42f, -1.25f), waterHeight + 0.013f, Range(-1.8f, -0.28f));
                Scatter(PrimitiveType.Cylinder, position, V(Range(0.16f, 0.3f), 0.013f, Range(0.15f, 0.28f)),
                    Quaternion.identity, lily);
            }
            for (int i = 0; i < shore.Count; i += 2)
            {
                Vector3 point = shore[i];
                if (i % 6 != 0)
                    GrassTuft(Vector3.Lerp(point, shallow[i], 0.18f), Range(0.45f, 1.0f), true);
                if (i % 4 == 0)
                    Boulder(point, Range(0.38f, 1.0f));
            }
            Flush(pond);
        }

        private void GroundStrip(Transform meadow, float left, float right, float front, float rear, float bottom)
        {
            Box(meadow, "Meadow around pond", V((left + right) * 0.5f, bottom * 0.5f, (front + rear) * 0.5f),
                V(right - left, -bottom, rear - front), grass);
        }

        private void Surface(Transform parent, string name, List<Vector3> outside, List<Vector3> inside,
            Material material, bool collision)
        {
            int count = outside.Count;
            var vertices = new List<Vector3>(outside);
            vertices.AddRange(inside);
            var triangles = new List<int>();
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                triangles.Add(i);
                triangles.Add(i + count);
                triangles.Add(next);
                if (inside[i] != inside[next])
                {
                    triangles.Add(next);
                    triangles.Add(i + count);
                    triangles.Add(next + count);
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            SaveAsset(mesh);
            Transform surface = Group(name, parent);
            surface.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            surface.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collision)
                surface.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private void BuildWildVerges(Transform parent, bool includePondTrees = true)
        {
            Transform verge = Group("Wild meadow and scattered rocks", parent);
            Material[] blooms = Palette("Wildflowers", "#E7DDB3", "#C8B36A", "#A298BB");
            foreach (Vector3 patch in new[] { V(-8.6f, 0, -10.1f), V(-12.4f, 0, -5.3f),
                V(6.4f, 0, -14.0f), V(7.1f, 0, -8.0f), V(11.7f, 0, -5.8f), V(16.3f, 0, -5.7f),
                V(19.3f, 0, -9.6f), V(1.9f, 0, 8.2f), V(-5.3f, 0, 6.5f) })
            {
                for (int i = 0; i < 27; i++)
                {
                    Vector3 position = patch + V(Range(-1.35f, 1.35f), 0, Range(-0.9f, 0.9f));
                    float height = Range(0.15f, 0.45f);
                    GrassTuft(position, height, false);
                    if (i % 4 == 0)
                        Scatter(PrimitiveType.Sphere, position + V(0, height * 0.9f, 0),
                            V(0.14f, 0.07f, 0.13f), Quaternion.identity, Pick(blooms));
                }
                Boulder(patch + V(0.65f, 0, 0.43f), Range(0.42f, 0.94f));
            }
            foreach (Vector3 position in new[] { V(-8.4f, 0, -7.6f), V(-6.8f, 0, 4.6f),
                V(13.8f, 0, -5.1f), V(17.5f, 0, -6.8f), V(17.9f, 0, -11.3f) })
                if (includePondTrees || position.x < 0)
                    Tree(position, Range(0.8f, 1.24f));
            Flush(verge);
        }

        private void GrassTuft(Vector3 root, float height, bool reeds)
        {
            for (int blade = 0; blade < 5; blade++)
            {
                Vector3 offset = V(Range(-0.17f, 0.17f), 0, Range(-0.16f, 0.16f));
                float length = height * Range(0.57f, 1.0f);
                Quaternion rotation = Quaternion.Euler(Range(-17, 17), Range(0, 360), Range(-21, 21));
                Vector3 tip = root + offset + rotation * V(0, length, 0);
                Scatter(PrimitiveType.Cube, (root + offset + tip) * 0.5f,
                    V(reeds ? 0.032f : 0.046f, length, 0.019f), rotation, Pick(leaves));
                if (reeds && blade % 2 == 0)
                    Scatter(PrimitiveType.Sphere, tip, V(0.065f, 0.16f, 0.065f), rotation, timber);
            }
        }

        private void Boulder(Vector3 point, float size)
        {
            for (int i = 0; i < 3; i++)
            {
                float scale = i == 0 ? size : size * 0.56f;
                Scatter(PrimitiveType.Sphere, point + V(i * scale * 0.77f, scale * 0.19f, i * scale * 0.25f),
                    V(scale * 1.49f, scale * 0.76f, scale * 1.12f),
                    Quaternion.Euler(Range(-19, 19), Range(0, 360), Range(-18, 18)), Pick(stone));
            }
        }
    }
}
