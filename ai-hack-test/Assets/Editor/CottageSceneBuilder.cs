using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class CottageSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Cottage.unity";
    private const string AssetFolder = "Assets/Cottage";
    private const string ArtPath = AssetFolder + "/Cottage Art.asset";

    [MenuItem("Tools/Cottage/Create Countryside Scene")]
    public static void Create()
    {
        if (System.IO.File.Exists(ScenePath))
        {
            Debug.LogWarning("Cottage.unity already exists. Open the existing scene to edit it.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        new Builder().Build();
    }

    private sealed partial class Builder
    {
        private readonly string artPath;
        private readonly System.Random random;

        public Builder(string artPath = ArtPath, int seed = 133)
        {
            this.artPath = artPath;
            random = new System.Random(seed);
        }
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly Dictionary<PrimitiveType, Mesh> primitives = new Dictionary<PrimitiveType, Mesh>();
        private readonly Dictionary<Material, List<CombineInstance>> decoration =
            new Dictionary<Material, List<CombineInstance>>();

        private Material mortar, timber, trim, glass, earth, grass, water;
        private Material[] stone, slate, leaves, flowers;
        private Transform repairPoints;

        public void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder("Assets", "Cottage");

            mortar = Paint("Lime mortar", "#9A8C73");
            timber = Paint("Weathered oak", "#68503B");
            trim = Paint("Ivory window frames", "#E2DFC7");
            glass = Paint("Blue window glass", "#43606C", 0.48f);
            earth = Paint("Ochre garden path", "#AE8C62");
            grass = Paint("Meadow green", "#6E8846");
            water = Paint("Irrigation water", "#588981", 0.65f);
            stone = Palette("Limestone", "#9E927E", "#BDB19C", "#ADA28A", "#C9BB9F", "#847F70");
            slate = Palette("Slate", "#465155", "#535D63", "#606B70", "#3E494F");
            leaves = Palette("Foliage", "#35512C", "#4F7134", "#668741", "#82994D", "#9BA853");
            flowers = Palette("Petals", "#9A78BD", "#D9A2BC", "#E7DCE1", "#C4749C", "#EAC679");

            BuildLandscape();
            BuildHouse();
            BuildYard();
            BuildLighting();
            new Builder(FarmyardArtPath, 276).BuildFarmyard(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save the Cottage scene.");

            Debug.Log("Cottage scene saved with " + repairPoints.childCount + " repair anchors.");
        }

        private void BuildHouse()
        {
            Transform house = Group("01 Cottage | 10.4 x 7.4 m");
            Transform shell = Group("Walls and foundations", house);
            Transform front = Group("South facade", shell);
            Box(shell, "Stone plinth", V(0, 0.13f, 0.1f), V(10.4f, 0.26f, 7.4f), stone[4]);
            Box(shell, "Interior floor", V(0, 0.29f, 0.1f), V(9.9f, 0.08f, 6.85f), timber);
            Wall(front, -5.2f, -0.82f, -3.5f);
            Wall(front, 0.82f, 5.2f, -3.5f);
            Box(front, "Lintel wall", V(0, 3.17f, -3.5f), V(1.65f, 1.4f, 0.36f), mortar);
            StoneRows(-0.82f, 0.82f, 2.47f, 3.86f, -3.7f);

            Box(shell, "North wall", V(0, 2.05f, 3.7f), V(10.4f, 3.6f, 0.34f), mortar);
            foreach (int side in new[] { -1, 1 })
            {
                Box(shell, "End wall", V(side * 5.03f, 2.05f, 0.1f), V(0.34f, 3.6f, 7.2f), mortar);
                EndGable(shell, side);
                for (int row = 0; row < 13; row++)
                {
                    for (float z = -3.55f + (row % 2) * 0.28f; z < 3.72f; z += 0.57f)
                        Scatter(PrimitiveType.Cube, V(side * 5.22f, 0.38f + row * 0.27f, z),
                            V(0.11f, Range(0.19f, 0.25f), 0.53f), Quaternion.identity, Pick(stone));
                }
            }
            for (int row = 0; row < 13; row++)
            {
                for (float x = -4.95f + (row % 2) * 0.25f; x < 5.16f; x += 0.53f)
                    Scatter(PrimitiveType.Cube, V(x, 0.38f + row * 0.27f, 3.88f),
                        V(0.49f, Range(0.19f, 0.25f), 0.11f), Quaternion.identity, Pick(stone));
            }

            Flush(shell);

            Transform entry = Group("Entry | Arched entry", house);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 8; i++)
                    Box(entry, "Jamb stone", V(side * 0.82f, 0.37f + i * 0.26f, -3.75f),
                        V(0.25f, 0.245f, 0.24f), stone[i % stone.Length]);
            for (int i = 0; i <= 12; i++)
            {
                float angle = i * 15f;
                float radians = angle * Mathf.Deg2Rad;
                Box(entry, "Arch voussoir", V(0.82f * Mathf.Cos(radians),
                    2.05f + 0.49f * Mathf.Sin(radians), -3.76f),
                    V(0.27f, 0.245f, 0.25f), stone[i % stone.Length], true,
                    Quaternion.Euler(0, 0, angle));
            }
            Box(entry, "Threshold", V(0, 0.14f, -3.93f), V(1.95f, 0.28f, 0.73f), stone[1]);
            Box(entry, "Open oak door", V(-0.67f, 1.25f, -2.87f), V(0.11f, 1.85f, 1.28f), timber,
                true, Quaternion.Euler(0, -12, 0));

            Transform roof = Group("Main roof", house);
            RoofSlope(roof, "South west slope", V(-3.55f, 6.15f, 0.1f), V(-3.55f, 3.85f, -4.06f),
                Vector3.right, 4.1f, false);
            RoofSlope(roof, "South slope behind gable", V(1.01f, 6.15f, 0.1f), V(1.01f, 4.12f, -3.57f),
                Vector3.right, 5.02f, false);
            RoofSlope(roof, "South east slope", V(4.56f, 6.15f, 0.1f), V(4.56f, 3.85f, -4.06f),
                Vector3.right, 2.08f, false);
            RoofSlope(roof, "North slope", V(0, 6.15f, 0.1f), V(0, 3.85f, 4.26f),
                Vector3.right, 11.2f, false);
            Box(roof, "Ridge cap", V(0, 6.22f, 0.1f), V(11.24f, 0.16f, 0.24f), slate[0]);

            Transform gable = Group("Projecting front gable", house);
            Box(gable, "Gable front", V(1.04f, 3.57f, -3.75f), V(4.7f, 1.43f, 0.3f), mortar);
            Triangle(gable, "Upper gable masonry", V(-1.31f, 4.26f, -3.92f),
                V(3.39f, 4.26f, -3.92f), V(1.04f, 6.7f, -3.92f), mortar);
            for (int row = 0; row < 9; row++)
            {
                float y = 4.32f + row * 0.26f;
                float halfWidth = 2.23f * (6.7f - y) / 2.44f;
                StoneRows(1.04f - halfWidth, 1.04f + halfWidth, y, y + 0.19f, -3.97f);
            }
            StoneRows(-1.3f, 3.38f, 2.94f, 4.23f, -3.94f);
            Flush(gable);
            RoofSlope(gable, "West gable slope", V(1.04f, 6.76f, -2.38f),
                V(-1.66f, 4.12f, -2.38f), Vector3.forward, 3.9f, false);
            RoofSlope(gable, "East gable slope", V(1.04f, 6.76f, -2.38f),
                V(3.74f, 4.12f, -2.38f), Vector3.forward, 3.9f, true);

            Chimney(house, -4.12f, 0.96f, 7.38f);
            Chimney(house, 3.95f, 1.62f, 6.73f);
            Window(house, "West casement", -3.18f, 1.77f, -3.76f, 1.42f, 1.42f);
            Window(house, "East casement", 2.8f, 1.75f, -3.76f, 1.42f, 1.45f);
            Window(house, "Attic casement", 1.05f, 4.96f, -4.02f, 1.17f, 1.4f);
            Box(house, "Loose east shutter", V(3.72f, 0.38f, -4.43f), V(0.43f, 0.12f, 1.38f), trim,
                true, Quaternion.Euler(0, -25, 0));
            Box(house, "East shutter frame", V(3.69f, 1.75f, -3.77f), V(0.08f, 1.44f, 0.13f), timber);

            repairPoints = Group("Repair anchors", house);
            Anchor(repairPoints, "cottage_roof_east", V(2.95f, 4.95f, -3.15f));
            Anchor(repairPoints, "cottage_shutter_east", V(3.72f, 1.75f, -3.84f));
            Anchor(repairPoints, "cottage_chimney_west", V(-4.12f, 7.19f, 0.96f));
            Box(house, "Loose slate on path edge", V(2.9f, 0.06f, -5.03f),
                V(0.46f, 0.07f, 0.56f), slate[2], true, Quaternion.Euler(0, 22, 0));

            Transform ivy = Group("Ivy on the facade", house);
            for (int i = 0; i < 330; i++)
            {
                float t = Range(0, 1);
                float x = Mathf.Lerp(-4.9f, 0.9f, t) + Range(-0.45f, 0.45f);
                float y = Mathf.Lerp(0.4f, 6.1f, t * t) + Range(-0.38f, 0.38f);
                float size = Range(0.1f, 0.29f);
                Scatter(PrimitiveType.Sphere, V(x, y, y > 3.05f ? -4.07f : -3.86f),
                    V(size, size * 0.9f, size * 0.55f), Quaternion.identity, Pick(leaves));
            }
            Flush(ivy);
        }

        private void Wall(Transform parent, float left, float right, float z)
        {
            Box(parent, "Wall masonry", V((left + right) * 0.5f, 2.05f, z),
                V(right - left, 3.6f, 0.36f), mortar);
            StoneRows(left, right, 0.28f, 3.85f, z - 0.2f);
        }

        private void StoneRows(float left, float right, float bottom, float top, float z)
        {
            for (int row = 0; bottom + row * 0.27f + 0.16f < top; row++)
            {
                for (float x = left; x < right - 0.09f;)
                {
                    float width = Mathf.Min(Range(0.25f, row % 2 == 0 ? 0.67f : 0.74f), right - x);
                    float y = bottom + 0.125f + row * 0.27f;
                    Scatter(PrimitiveType.Cube, V(x + width * 0.5f, y, z),
                        V(width - 0.025f, Range(0.185f, 0.24f), Range(0.085f, 0.14f)),
                        Quaternion.Euler(0, 0, Range(-4, 4)), Pick(stone));
                    x += width;
                }
            }
        }

        private void EndGable(Transform parent, int side)
        {
            float x = side * 5.15f;
            Triangle(parent, "End gable", V(x, 3.83f, -3.6f), V(x, 3.83f, 3.8f),
                V(x, 6.15f, 0.1f), mortar);
            for (int row = 0; row < 8; row++)
            {
                float y = 3.95f + row * 0.26f;
                float halfWidth = 3.64f * (6.15f - y) / 2.32f;
                for (float z = 0.1f - halfWidth + 0.18f; z < 0.1f + halfWidth - 0.14f; z += 0.39f)
                    Scatter(PrimitiveType.Cube, V(side * 5.19f, y, z),
                        V(0.1f, 0.22f, 0.36f), Quaternion.identity, Pick(stone));
            }
        }

        private void RoofSlope(Transform parent, string name, Vector3 ridge, Vector3 eave,
            Vector3 across, float width, bool damaged)
        {
            Vector3 slope = eave - ridge;
            Quaternion rotation = Quaternion.LookRotation(slope, Vector3.up);
            Transform section = Group(name, parent);
            Box(section, "Roof deck", (ridge + eave) * 0.5f, V(width, 0.15f, slope.magnitude),
                damaged ? timber : slate[0], true, rotation);
            Box(section, "Fascia", eave, V(width + 0.1f, 0.19f, 0.17f), timber, true, rotation);
            Vector3 up = rotation * Vector3.up;
            for (int row = 0; row < Mathf.CeilToInt(slope.magnitude / 0.25f); row++)
            {
                for (float x = -width * 0.5f; x < width * 0.5f - 0.04f;)
                {
                    float tile = Mathf.Min(x == -width * 0.5f && row % 2 != 0 ? 0.16f : 0.32f,
                        width * 0.5f - x);
                    if (!damaged || row < 8 || row > 12 || x < -1.46f || x > -0.21f)
                        Scatter(PrimitiveType.Cube, ridge + slope.normalized * (0.13f + row * 0.25f)
                            + across * (x + tile * 0.5f) + up * Range(0.092f, 0.119f),
                            V(tile - 0.013f, 0.048f, 0.27f), rotation, Pick(slate));
                    x += tile;
                }
            }
            Flush(section);
        }

        private void Chimney(Transform parent, float x, float z, float height)
        {
            Transform chimney = Group(x < 0 ? "West chimney" : "East chimney", parent);
            Box(chimney, "Stack", V(x, (4.5f + height) * 0.5f, z),
                V(0.83f, height - 4.5f, 0.81f), mortar);
            for (float y = 5.05f; y < height; y += 0.22f)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    Scatter(PrimitiveType.Cube, V(x, y, z + side * 0.42f), V(0.87f, 0.19f, 0.09f),
                        Quaternion.identity, Pick(stone));
                    Scatter(PrimitiveType.Cube, V(x + side * 0.43f, y, z), V(0.09f, 0.19f, 0.85f),
                        Quaternion.identity, Pick(stone));
                }
            }
            Box(chimney, "Cap", V(x, height, z), V(0.99f, 0.14f, 0.99f), stone[4]);
            Shape(chimney, "Clay pot", PrimitiveType.Cylinder, V(x, height + 0.24f, z),
                V(0.4f, 0.19f, 0.4f), earth);
            Flush(chimney);
        }

        private void Window(Transform parent, string name, float x, float y, float z, float width, float height)
        {
            Transform window = Group(name, parent);
            Box(window, "Stone surround", V(x, y, z), V(width + 0.18f, height + 0.17f, 0.18f), stone[1]);
            Box(window, "Glazing", V(x, y, z - 0.105f), V(width, height, 0.06f), glass);
            for (int i = 0; i <= 4; i++)
                Box(window, "Mullion", V(x + width * (i / 4f - 0.5f), y, z - 0.15f),
                    V(i % 4 == 0 ? 0.095f : 0.045f, height, 0.07f), trim, false);
            for (int i = 0; i <= 3; i++)
                Box(window, "Rail", V(x, y + height * (i / 3f - 0.5f), z - 0.15f),
                    V(width, i % 3 == 0 ? 0.09f : 0.045f, 0.07f), trim, false);
            Box(window, "Sill", V(x, y - height * 0.5f - 0.07f, z - 0.14f),
                V(width + 0.29f, 0.14f, 0.37f), stone[1]);
        }

        private void BuildLandscape()
        {
            Transform land = Group("00 Meadow and hills");
            Box(land, "Walkable meadow", V(0, -0.32f, 5), V(130, 0.64f, 130), grass);
            for (int i = 0; i < 19; i++)
            {
                float angle = Mathf.Lerp(-1.46f, 1.5f, i / 18f);
                float distance = Range(37, 68);
                Scatter(PrimitiveType.Sphere, V(Mathf.Sin(angle) * distance, -1.9f,
                        9 + Mathf.Cos(angle) * distance), V(Range(18, 37), Range(6, 17), Range(15, 27)),
                    Quaternion.identity, Pick(leaves));
            }
            Flush(land);

            Transform grove = Group("Woodland", land);
            for (int i = 0; i < 42; i++)
            {
                float x = Range(-38, 37);
                float z = Range(8, 37);
                if (x > 8.5f && x < 23.8f && z < 14)
                    z += 7f;

                Tree(V(x, 0, z), Range(0.75f, 1.55f));
            }
            Tree(V(-14.5f, 0, 1.5f), 1.53f);
            Tree(V(22.7f, 0, 0.6f), 1.83f);
            Tree(V(24f, 0, 7), 1.35f);
            Tree(V(-13.6f, 0, 6.5f), 1.55f);
            Flush(grove);
        }

        private void Tree(Vector3 origin, float size)
        {
            Scatter(PrimitiveType.Cylinder, origin + V(0, 1.28f, 0) * size, V(0.38f, 1.3f, 0.36f) * size,
                Quaternion.Euler(4, 0, -5), timber);
            for (int i = 0; i < 15; i++)
            {
                Vector3 offset = V(Range(-1.4f, 1.4f), Range(2.65f, 4.65f), Range(-1.35f, 1.35f));
                Scatter(PrimitiveType.Sphere, origin + offset * size,
                    V(Range(1.5f, 2.65f), Range(1.25f, 2.4f), Range(1.45f, 2.45f)) * size,
                    Quaternion.Euler(0, Range(0, 360), Range(-22, 22)), Pick(leaves));
            }
        }

        private void BuildYard()
        {
            Transform yard = Group("02 Garden and farmyard");
            Transform path = Group("Flagstone walk", yard);
            for (int i = 0; i < 15; i++)
            {
                float z = -4.35f - i * 0.74f;
                float x = PathX(z);
                Scatter(PrimitiveType.Cylinder, V(x, 0.008f, z), V(2.45f, 0.009f, 1.75f),
                    Quaternion.identity, earth);
                for (int side = -1; side <= 1; side += 2)
                {
                    Scatter(PrimitiveType.Cylinder, V(x + side * 0.44f, 0.027f, z + Range(-0.17f, 0.17f)),
                        V(Range(0.66f, 0.88f), 0.019f, Range(0.45f, 0.75f)),
                        Quaternion.Euler(0, Range(0, 360), 0), Pick(stone));
                }
            }
            Flush(path);

            Transform garden = Group("Cottage flower borders", yard);
            for (int i = 0; i < 105; i++)
            {
                float z = Range(-13.2f, -4.3f);
                float x = PathX(z) + (i % 2 == 0 ? -1 : 1) * Range(1.45f, 4.8f);
                FlowerClump(V(x, 0, z), Pick(flowers));
            }
            for (int i = 0; i < 24; i++)
            {
                float x = i % 2 == 0 ? Range(-5.35f, -2.15f) : Range(1.6f, 5.45f);
                FlowerClump(V(x, 0, Range(-5.35f, -3.95f)), Pick(flowers));
            }
            Flush(garden);

            Transform fence = Group("Weathered picket fence", yard);
            Fence(fence, V(2.85f, 0, -10.9f), V(8.2f, 0, -7.32f));
            Fence(fence, V(8.2f, 0, -7.32f), V(8.2f, 0, -0.8f));
            Fence(fence, V(-9.2f, 0, -5.5f), V(-6.2f, 0, -9.15f));
            Flush(fence);

            BuildWell(yard);
            BuildStorehouse(yard);
            BuildPaddy(yard);
        }

        private static float PathX(float z) => 1.55f * Mathf.Sin((-z - 4.3f) * 0.29f);

        private void FlowerClump(Vector3 origin, Material petal)
        {
            for (int i = 0; i < 7; i++)
            {
                Vector3 root = origin + V(Range(-0.44f, 0.44f), 0, Range(-0.4f, 0.4f));
                float height = Range(0.24f, 0.83f);
                Scatter(PrimitiveType.Cylinder, root + V(0, height * 0.5f, 0),
                    V(0.025f, height * 0.5f, 0.025f), Quaternion.identity, leaves[1]);
                Scatter(PrimitiveType.Sphere, root + V(0.063f, height * 0.42f, 0),
                    V(0.23f, 0.065f, 0.11f), Quaternion.Euler(0, Range(0, 360), 22), Pick(leaves));
                Scatter(PrimitiveType.Sphere, root + V(0, height, 0),
                    V(Range(0.105f, 0.19f), Range(0.095f, 0.25f), Range(0.105f, 0.19f)),
                    Quaternion.identity, petal);
            }
        }

        private void Fence(Transform parent, Vector3 start, Vector3 end)
        {
            Vector3 span = end - start;
            Quaternion facing = Quaternion.LookRotation(Vector3.Cross(span, Vector3.up));
            int posts = Mathf.RoundToInt(span.magnitude / 0.19f);
            Material picket = Paint("Picket whitewash", "#C6C4AD");
            for (int i = 0; i <= posts; i++)
                Scatter(PrimitiveType.Cube, Vector3.Lerp(start, end, i / (float)posts) + V(0, 0.43f, 0),
                    V(0.116f, Range(0.78f, 0.98f), 0.085f),
                    facing * Quaternion.Euler(0, 0, Range(-5, 5)), picket);

            foreach (float height in new[] { 0.28f, 0.64f })
                Box(parent, "Fence rail", (start + end) * 0.5f + V(0, height, 0),
                    V(span.magnitude, 0.105f, 0.105f), picket, height < 0.5f, facing);
        }

        private void BuildWell(Transform yard)
        {
            Transform well = Group("Storm damaged well", yard);
            Vector3 center = V(-7.5f, 0, -2.1f);
            Shape(well, "Dark water", PrimitiveType.Cylinder, center + V(0, 0.055f, 0),
                V(1.77f, 0.047f, 1.77f), water);
            for (int row = 0; row < 3; row++)
            {
                for (int i = 0; i < 16; i++)
                {
                    if (row == 2 && i >= 10 && i <= 13)
                        continue;

                    float angle = (i + row * 0.5f) * 22.5f;
                    Quaternion turn = Quaternion.Euler(0, angle, 0);
                    Box(well, "Well stone", center + V(0, 0.14f + row * 0.26f, 0) + turn * V(0, 0, 0.9f),
                        V(0.37f, 0.245f, 0.29f), Pick(stone), true, turn);
                }
            }
            Box(well, "Standing pulley post", center + V(-1.02f, 1.07f, 0), V(0.17f, 2.14f, 0.18f), timber);
            Box(well, "Fallen pulley post", center + V(1.3f, 0.14f, 0.47f),
                V(0.17f, 0.18f, 2.05f), timber, true, Quaternion.Euler(0, 32, 0));
            Shape(well, "Pulley wheel", PrimitiveType.Cylinder, center + V(1.65f, 0.12f, -0.9f),
                V(0.32f, 0.052f, 0.32f), timber);
            Box(well, "Loose well stone", center + V(-0.75f, 0.1f, -1.34f),
                V(0.38f, 0.19f, 0.31f), stone[1], true, Quaternion.Euler(0, 24, 7));
            Anchor(repairPoints, "well_rim_gap", center + V(-0.78f, 0.65f, 0.38f));
            Anchor(repairPoints, "well_pulley", center + V(0, 2.05f, 0));
        }

        private void BuildStorehouse(Transform yard)
        {
            Transform shed = Group("Storm damaged timber storehouse", yard);
            Vector3 center = V(-12.5f, 0, -0.3f);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int end = -1; end <= 1; end += 2)
                {
                    Vector3 corner = center + V(1.45f * side, 0, 1.05f * end);
                    Box(shed, "Foundation stone", corner + V(0, 0.14f, 0), V(0.64f, 0.28f, 0.62f), Pick(stone));
                    if (side == 1 && end == -1)
                        Anchor(repairPoints, "storehouse_post_south_east", corner + V(0, 0.29f, 0));
                    else
                        Box(shed, "Standing post", corner + V(0, 1.37f, 0), V(0.19f, 2.23f, 0.2f), timber);
                }
            }
            Box(shed, "Floorboards", center + V(0, 0.38f, 0), V(3.23f, 0.15f, 2.47f), timber);
            Box(shed, "Fallen post", center + V(1.45f, 0.11f, -1.83f), V(2.23f, 0.19f, 0.2f),
                timber, true, Quaternion.Euler(0, 24, 0));
            RoofSlope(shed, "Standing roof section", center + V(0, 3.26f, 0),
                center + V(0, 2.48f, 1.38f), Vector3.right, 3.44f, false);
            for (int i = 0; i < 4; i++)
                Box(shed, "Exposed rafter", center + V(-1.4f + i * 0.73f, 2.9f, -0.62f),
                    V(0.13f, 0.14f, 1.58f), timber, true, Quaternion.Euler(-29, 0, 0));
        }

        private void BuildPaddy(Transform yard)
        {
            Transform field = Group("Rice terraces and irrigation", yard);
            for (int row = 0; row < 3; row++)
            {
                Vector3 center = V(15.2f + row * 1.45f, 0, 2f + row * 4.4f);
                Box(field, "Paddy water", center + V(0, 0.03f, 0), V(9.1f, 0.055f, 3.95f), water);
                Box(field, "Front embankment", center + V(0, 0.09f, -2), V(9.45f, 0.19f, 0.25f), leaves[2]);
                Box(field, "Rear embankment", center + V(0, 0.09f, 2), V(9.45f, 0.19f, 0.25f), leaves[2]);
                for (int x = 0; x < 18; x++)
                    for (int z = 0; z < 6; z++)
                        Scatter(PrimitiveType.Cube, center + V(-4.15f + x * 0.49f, 0.17f, -1.53f + z * 0.6f),
                            V(0.065f, Range(0.2f, 0.42f), 0.075f),
                            Quaternion.Euler(Range(-16, 16), 0, Range(-16, 16)), Pick(leaves));
            }
            Box(field, "Irrigation channel", V(10.22f, 0.032f, 6.7f), V(0.58f, 0.063f, 13.45f), water);
            Flush(field);
        }

        private void BuildLighting()
        {
            var sun = new GameObject("Afternoon sun", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.91f, 0.77f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(37, -46, 0);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.68f, 0.75f, 0.83f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.51f, 0.47f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.32f, 0.27f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.7f, 0.81f, 0.82f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.011f;

            var sky = new Material(Shader.Find("Skybox/Procedural")) { name = "Soft countryside sky" };
            sky.SetColor("_SkyTint", new Color(0.64f, 0.76f, 0.86f));
            sky.SetFloat("_AtmosphereThickness", 0.9f);
            sky.SetFloat("_Exposure", 1.15f);
            SaveAsset(sky);
            RenderSettings.skybox = sky;

            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = V(11.8f, 5.7f, -22.5f);
            camera.transform.LookAt(V(0.15f, 2.6f, 0.35f));
            camera.fieldOfView = 40;
            camera.farClipPlane = 140;

            var view = SceneView.lastActiveSceneView;
            if (view != null)
                view.LookAtDirect(V(0.15f, 2.6f, 0.35f), camera.transform.rotation, 15);
        }

        private void Triangle(Transform parent, string name, Vector3 a, Vector3 b, Vector3 c, Material material)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { a, b, c, a, c, b };
            mesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveAsset(mesh);
            var target = Group(name, parent);
            target.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            target.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void Scatter(PrimitiveType type, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            if (!primitives.TryGetValue(type, out Mesh mesh))
            {
                if (type == PrimitiveType.Sphere)
                {
                    mesh = FoliageSphere();
                }
                else
                {
                    var sample = GameObject.CreatePrimitive(type);
                    mesh = sample.GetComponent<MeshFilter>().sharedMesh;
                    Object.DestroyImmediate(sample);
                }
                primitives.Add(type, mesh);
            }
            if (!decoration.TryGetValue(material, out List<CombineInstance> pieces))
            {
                pieces = new List<CombineInstance>();
                decoration.Add(material, pieces);
            }
            pieces.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, rotation, scale) });
        }

        private static Mesh FoliageSphere()
        {
            const int segments = 12;
            const int rings = 6;
            var vertices = new List<Vector3> { Vector3.up * 0.5f };
            var triangles = new List<int>();

            for (int ring = 0; ring < rings; ring++)
            {
                float latitude = Mathf.PI * (ring + 1) / (rings + 1);
                for (int segment = 0; segment < segments; segment++)
                {
                    float longitude = 2 * Mathf.PI * segment / segments;
                    vertices.Add(V(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude),
                        Mathf.Sin(latitude) * Mathf.Sin(longitude)) * 0.5f);
                }
            }

            int bottom = vertices.Count;
            vertices.Add(Vector3.down * 0.5f);
            for (int segment = 0; segment < segments; segment++)
            {
                int current = 1 + segment;
                int next = 1 + (segment + 1) % segments;
                triangles.Add(0);
                triangles.Add(next);
                triangles.Add(current);

                for (int ring = 0; ring < rings - 1; ring++)
                {
                    int upper = current + ring * segments;
                    int upperNext = next + ring * segments;
                    triangles.Add(upper);
                    triangles.Add(upperNext);
                    triangles.Add(upper + segments);
                    triangles.Add(upperNext);
                    triangles.Add(upperNext + segments);
                    triangles.Add(upper + segments);
                }

                triangles.Add(bottom);
                triangles.Add(current + (rings - 1) * segments);
                triangles.Add(next + (rings - 1) * segments);
            }

            var mesh = new Mesh { name = "Low-poly foliage sphere" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        private void Flush(Transform parent)
        {
            foreach (var pair in decoration)
            {
                int vertices = 0;
                foreach (var piece in pair.Value)
                    vertices += piece.mesh.vertexCount;

                var mesh = new Mesh
                {
                    name = parent.name + " - " + pair.Key.name,
                    indexFormat = vertices > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
                };
                mesh.CombineMeshes(pair.Value.ToArray());
                SaveAsset(mesh);
                var target = Group(pair.Key.name, parent);
                target.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                target.gameObject.AddComponent<MeshRenderer>().sharedMaterial = pair.Key;
            }
            decoration.Clear();
        }

        private void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material,
            bool collider = true, Quaternion? rotation = null)
        {
            Shape(parent, name, PrimitiveType.Cube, position, scale, material, collider, rotation);
        }

        private void Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale,
            Material material, bool collider = true, Quaternion? rotation = null)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.SetPositionAndRotation(position, rotation ?? Quaternion.identity);
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider)
                Object.DestroyImmediate(obj.GetComponent<Collider>());
        }

        private Material[] Palette(string name, params string[] colors)
        {
            var palette = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                palette[i] = Paint(name + " " + (i + 1), colors[i]);

            return palette;
        }

        private Material Paint(string name, string hex, float smoothness = 0.12f)
        {
            if (materials.TryGetValue(name, out Material existing))
                return existing;

            ColorUtility.TryParseHtmlString(hex, out Color color);
            var material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            materials.Add(name, material);
            SaveAsset(material);
            return material;
        }

        private void SaveAsset(Object asset)
        {
            if (AssetDatabase.LoadMainAssetAtPath(artPath) == null)
                AssetDatabase.CreateAsset(asset, artPath);
            else
                AssetDatabase.AddObjectToAsset(asset, artPath);
        }

        private static Transform Group(string name, Transform parent = null)
        {
            var transform = new GameObject(name).transform;
            transform.SetParent(parent, false);
            return transform;
        }

        private static void Anchor(Transform parent, string id, Vector3 position)
        {
            Group(id, parent).position = position;
        }

        private float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        private Material Pick(Material[] palette) => palette[random.Next(palette.Length)];
        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    }
}
