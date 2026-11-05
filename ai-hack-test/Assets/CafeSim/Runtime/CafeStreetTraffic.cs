using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CafeSim
{
    public sealed class CafeStreetTraffic : MonoBehaviour
    {
        [SerializeField] private CafeBusinessDay businessDay;
        [SerializeField] private MeshRenderer roadSurface, pavement;
        [SerializeField] private Transform streetTree;
        [SerializeField] private MeshRenderer[] oppositeFacades;
        [SerializeField] private AudioClip passingCarLoop;

        private const float DriveSpeed = 7.5f;
        private const float WheelRadius = .28f;
        private static readonly Color32[] CarColors =
        {
            new(48, 101, 164, 255), new(179, 58, 52, 255),
            new(65, 128, 78, 255), new(205, 159, 73, 255),
            new(215, 206, 189, 255), new(100, 75, 126, 255)
        };

        private readonly bool[] travelDirections = { true, true, false, false };
        private readonly List<Material> materials = new();
        private readonly List<Mesh> meshes = new();
        private Transform street, car;
        private Material paint;
        private AudioSource engine;
        private Mesh cube;
        private Bounds road;
        private int directionsRemaining, lastCarColor = -1;
        private float nextPassAt, passStartedAt, startZ, endZ, laneX;

        private void Start()
        {
            if (businessDay == null || businessDay.session == null || businessDay.session.doorway == null ||
                businessDay.session.entrance == null || roadSurface == null || pavement == null)
            {
                enabled = false;
                return;
            }

            road = roadSurface.bounds;
            cube = roadSurface.GetComponent<MeshFilter>().sharedMesh;
            street = new GameObject("Cafe frontage and traffic").transform;
            street.SetParent(transform, true); // Keep the generated dimensions in world metres.
            BuildFrontage();
            BuildStreetScenery();
            BuildCar();
            nextPassAt = businessDay.Clock + Random.Range(22f, 38f) * 60f;
        }

        private void Update()
        {
            if (businessDay.Paused || businessDay.Closed || CafeStartIris.Active) return;
            float clock = businessDay.Clock;
            if (!car.gameObject.activeSelf)
            {
                if (clock >= nextPassAt) StartPass(clock);
                return;
            }

            float elapsed = (clock - passStartedAt) / Mathf.Max(1f, businessDay.gameSecondsPerSecond);
            float distance = elapsed * DriveSpeed;
            if (distance >= Mathf.Abs(endZ - startZ))
            {
                engine.Stop();
                car.gameObject.SetActive(false);
                nextPassAt = clock + Random.Range(82f, 130f) * 60f;
                return;
            }

            float z = Mathf.MoveTowards(startZ, endZ, distance);
            car.position = new Vector3(laneX, road.max.y, z);
        }

        private void StartPass(float clock)
        {
            // Shuffle both directions into each group of four passes.
            if (directionsRemaining == 0) directionsRemaining = travelDirections.Length;
            int directionIndex = Random.Range(0, directionsRemaining);
            bool northbound = travelDirections[directionIndex];
            travelDirections[directionIndex] = travelDirections[--directionsRemaining];
            travelDirections[directionsRemaining] = northbound;

            laneX = road.center.x + road.extents.x * (northbound ? .5f : -.5f);
            startZ = northbound ? road.min.z + 2f : road.max.z - 2f;
            endZ = northbound ? road.max.z - 2f : road.min.z + 2f;
            car.SetPositionAndRotation(new Vector3(laneX, road.max.y, startZ),
                Quaternion.Euler(0f, northbound ? 0f : 180f, 0f));
            int color = Random.Range(0, CarColors.Length - (lastCarColor >= 0 ? 1 : 0));
            if (lastCarColor >= 0 && color >= lastCarColor) color++;
            paint.color = CarColors[color];
            lastCarColor = color;
            passStartedAt = clock;
            car.gameObject.SetActive(true);
            if (engine.clip != null) engine.Play();
        }

        private void BuildFrontage()
        {
            var session = businessDay.session;
            Bounds walk = pavement.bounds;
            float left = walk.max.x;
            float right = session.entrance.position.x + 4.8f;
            float near = session.doorway.position.z - .46f;
            float far = session.entrance.position.z - 1.35f;
            float centerX = (left + right) * .5f;
            float centerZ = (near + far) * .5f;
            float walkTop = walk.max.y;
            float walkWidth = right - left;
            float walkDepth = near - far;

            var entrySidewalk = Box("Entry sidewalk", street, pavement.sharedMaterial,
                new Vector3(centerX, walkTop - .08f, centerZ), new Vector3(walkWidth, .16f, walkDepth));
            entrySidewalk.AddComponent<BoxCollider>();
            Material joint = MakeMaterial("Paving joints", new Color32(125, 111, 92, 255));
            for (float x = left + .94f; x < right; x += .94f)
                Box("Paving joint", street, joint, new Vector3(x, walkTop + .002f, centerZ),
                    new Vector3(.014f, .003f, walkDepth), false);
            for (float z = far + .92f; z < near; z += .92f)
                Box("Paving joint", street, joint, new Vector3(centerX, walkTop + .002f, z),
                    new Vector3(walkWidth, .003f, .014f), false);

            Material curb = MakeMaterial("Sandstone curb", new Color32(178, 161, 133, 255));
            Box("Street curb", street, curb, new Vector3(road.max.x, walkTop - .048f, road.center.z),
                new Vector3(.15f, .15f, road.size.z));
            Box("Entry sidewalk edge", street, curb, new Vector3(centerX, walkTop - .032f, far),
                new Vector3(walkWidth, .10f, .13f));

            Material paintLine = MakeMaterial("Road marking paint", new Color32(216, 197, 149, 255));
            for (float z = road.min.z + 1.5f; z < road.max.z - 1f; z += 3.1f)
                Box("Center lane dash", street, paintLine, new Vector3(road.center.x, road.max.y + .004f, z),
                    new Vector3(.065f, .004f, 1.18f), false);
        }

        private void BuildStreetScenery()
        {
            var scenery = new GameObject("Window view | shops and trees").transform;
            scenery.SetParent(street, false);
            float ground = pavement.bounds.max.y;
            float oppositeCurb = road.min.x;
            float storefront = oppositeCurb - 1.85f;
            float pavementWidth = 1.9f;
            Box("Opposite sidewalk", scenery, pavement.sharedMaterial,
                new Vector3(oppositeCurb - pavementWidth * .5f, ground - .075f, road.center.z),
                new Vector3(pavementWidth, .15f, road.size.z));

            Material plaster = MakeMaterial("Warm shop plaster", new Color32(198, 166, 128, 255));
            Material brick = MakeMaterial("Terracotta shopfront", new Color32(160, 109, 87, 255));
            Material sage = MakeMaterial("Sage shopfront", new Color32(150, 162, 139, 255));
            Material trim = MakeMaterial("Limestone trim", new Color32(223, 197, 154, 255));
            Material roof = MakeMaterial("Weathered slate", new Color32(80, 76, 74, 255));
            Material glass = MakeMaterial("Shop window glass", new Color32(72, 90, 98, 255), .5f);
            Material wood = MakeMaterial("Dark shop joinery", new Color32(91, 65, 47, 255));
            Material metal = MakeMaterial("Street lamp iron", new Color32(64, 66, 61, 255));
            Material linen = MakeMaterial("Ivory lamp shades", new Color32(241, 211, 156, 255));

            var shopColors = new[] { plaster, brick, sage };
            // Dress the original facades directly across the windows and give them real depth.
            for (int i = 0; oppositeFacades != null && i < oppositeFacades.Length; i++)
            {
                MeshRenderer facade = oppositeFacades[i];
                if (facade == null) continue;
                Bounds bounds = facade.bounds;
                Box("Original shop rear walls", scenery, facade.sharedMaterial,
                    new Vector3(bounds.min.x - 1.45f, (ground + bounds.max.y) * .5f, bounds.center.z),
                    new Vector3(3f, bounds.max.y - ground, bounds.size.z));
                var shopfront = new GameObject("Original shop display").transform;
                shopfront.SetParent(scenery, false);
                shopfront.localPosition = new Vector3(bounds.max.x, ground, bounds.center.z);
                shopfront.localScale = new Vector3(.45f, 1f, bounds.size.z / 4.05f);
                BuildShopfront(shopfront, shopColors[i % shopColors.Length], trim, wood, glass);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < shopColors.Length; i++)
                {
                    float z = side * (10.1f + i * 4.35f);
                    float height = 3.18f + .14f * i;
                    var shop = new GameObject("Neighborhood shop").transform;
                    shop.SetParent(scenery, false);
                    shop.localPosition = new Vector3(storefront, ground, z);
                    Material walls = shopColors[(i + (side < 0 ? 1 : 0)) % shopColors.Length];
                    Material awning = shopColors[(i + (side < 0 ? 2 : 1)) % shopColors.Length];
                    BuildShop(shop, height, walls, awning, roof, trim, wood, glass);
                }
            }

            for (int i = 0; i < 5; i++)
            {
                float z = (i - 2) * 7.6f + 1.25f;
                float height = 3.4f + (i % 3) * .48f;
                float x = storefront - 7.4f - (i % 2) * 1.3f;
                Box("Distant townhouses", scenery, shopColors[i % shopColors.Length],
                    new Vector3(x, ground + height * .5f, z), new Vector3(3.5f, height, 6.9f));
                Box("Distant slate cornice", scenery, roof, new Vector3(x, ground + height, z),
                    new Vector3(3.65f, .15f, 7.02f));
            }

            if (streetTree != null)
            {
                var treeScale = streetTree.lossyScale;
                float nearTreesX = pavement.bounds.center.x;
                for (int side = -1; side <= 1; side += 2)
                {
                    PlantMaple(nearTreesX, side * 8.2f, .91f, side * 40f);
                    PlantMaple(nearTreesX, side * 15.6f, 1.02f, side * 95f);
                    PlantMaple(oppositeCurb - .74f, side * 12.45f, .82f, side * 120f);
                    PlantMaple(oppositeCurb - .74f, side * 20.55f, .93f, side * 175f);
                }

                void PlantMaple(float x, float z, float scale, float heading)
                {
                    Transform maple = Instantiate(streetTree, scenery, false);
                    maple.name = "Street maple";
                    maple.localPosition = new Vector3(x, ground, z);
                    maple.localRotation = Quaternion.Euler(0f, heading, 0f);
                    maple.localScale = treeScale * scale;
                }
            }

            float lampsX = road.max.x + .32f;
            foreach (float z in new[] { -19.1f, -6.1f, 6.7f, 18.7f })
            {
                Box("Lamp base", scenery, metal, new Vector3(lampsX, ground + .09f, z), new Vector3(.24f, .18f, .24f));
                Box("Lamp post", scenery, metal, new Vector3(lampsX, ground + 1.35f, z), new Vector3(.085f, 2.7f, .085f));
                Box("Lamp glass", scenery, linen, new Vector3(lampsX, ground + 2.79f, z), new Vector3(.19f, .26f, .19f));
                Box("Lamp cap", scenery, metal, new Vector3(lampsX, ground + 2.94f, z), new Vector3(.29f, .07f, .29f));
            }
        }

        private void BuildShop(Transform shop, float height, Material walls, Material awning, Material roof,
            Material trim, Material wood, Material glass)
        {
            Box("Shop walls", shop, walls, new Vector3(-1.68f, height * .5f, 0f), new Vector3(3.36f, height, 4.05f));
            Box("Slate parapet", shop, roof, new Vector3(-1.67f, height, 0f), new Vector3(3.51f, .16f, 4.18f));
            BuildShopfront(shop, awning, trim, wood, glass);
            foreach (float z in new[] { -.95f, .95f })
            {
                Box("Upper window surround", shop, trim, new Vector3(.026f, 2.56f, z), new Vector3(.08f, .77f, .94f));
                Box("Upper window glass", shop, glass, new Vector3(.073f, 2.56f, z), new Vector3(.028f, .63f, .80f));
                Box("Upper window mullion", shop, trim, new Vector3(.09f, 2.56f, z), new Vector3(.024f, .63f, .045f));
                Box("Window sill", shop, trim, new Vector3(.10f, 2.15f, z), new Vector3(.17f, .074f, 1.03f));
            }
        }

        private void BuildShopfront(Transform shop, Material awning, Material trim, Material wood, Material glass)
        {
            Box("Facade belt course", shop, trim, new Vector3(.012f, 1.92f, 0f), new Vector3(.10f, .095f, 4.03f));
            Box("Storefront frame", shop, wood, new Vector3(.019f, .91f, 0f), new Vector3(.08f, 1.63f, 3.55f));
            Box("Display window", shop, glass, new Vector3(.067f, 1.03f, -.54f), new Vector3(.03f, 1.04f, 2.12f));
            Box("Shop door glass", shop, glass, new Vector3(.067f, .96f, 1.15f), new Vector3(.03f, 1.22f, .76f));
            Box("Display sash", shop, trim, new Vector3(.085f, 1.03f, -.54f), new Vector3(.03f, .036f, 2.12f));
            Box("Door handle", shop, trim, new Vector3(.105f, .85f, .88f), new Vector3(.08f, .12f, .045f));

            var canopy = Box("Canvas awning", shop, awning, new Vector3(.26f, 1.79f, 0f), new Vector3(.56f, .067f, 3.66f));
            canopy.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            Box("Awning edge", shop, trim, new Vector3(.53f, 1.72f, 0f), new Vector3(.045f, .095f, 3.66f));
        }

        private void BuildCar()
        {
            car = new GameObject("Passing hatchback").transform;
            car.SetParent(street, false);
            paint = MakeMaterial("Hatchback paint", CarColors[0], .42f);
            Material glass = MakeMaterial("Blue gray car glass", new Color32(53, 73, 81, 255), .65f);
            Material rubber = MakeMaterial("Tire rubber", new Color32(37, 36, 35, 255));
            Material steel = MakeMaterial("Wheel caps", new Color32(173, 176, 171, 255), .32f);
            Material lamps = MakeMaterial("Headlamp lenses", new Color32(241, 218, 159, 255), .45f);
            Material tail = MakeMaterial("Tail lamp lenses", new Color32(148, 44, 42, 255), .4f);

            Shell("Lower body", paint, new Vector3(.62f, .28f, 1.54f), new Vector3(.73f, .60f, 1.72f));
            Shell("Fenders and hood", paint, new Vector3(.73f, .60f, 1.72f), new Vector3(.69f, .83f, 1.56f));
            Shell("Cabin windows", glass, new Vector3(.65f, .83f, 1.04f),
                new Vector3(.53f, 1.29f, .73f), -.19f);
            Shell("Roof", paint, new Vector3(.55f, 1.27f, .75f),
                new Vector3(.49f, 1.34f, .68f), -.19f);

            foreach (float side in new[] { -1f, 1f })
            {
                Box("Center window pillar", car, paint, new Vector3(side * .595f, 1.05f, -.18f),
                    new Vector3(.037f, .43f, .085f)).transform.localRotation = Quaternion.Euler(0f, 0f, side * 15f);
                foreach (float axle in new[] { -.99f, 1.02f })
                {
                    Wheel("Tire", rubber, new Vector3(side * .69f, WheelRadius, axle), WheelRadius, .17f);
                    Wheel("Wheel cap", steel, new Vector3(side * .785f, WheelRadius, axle), .16f, .025f);
                }
                Box("Headlamp", car, lamps, new Vector3(side * .43f, .68f, 1.65f), new Vector3(.27f, .13f, .08f));
                Box("Tail lamp", car, tail, new Vector3(side * .50f, .68f, -1.64f), new Vector3(.23f, .13f, .08f));
            }
            Box("Front bumper", car, rubber, new Vector3(0f, .43f, 1.62f), new Vector3(1.13f, .09f, .12f));
            Box("Rear bumper", car, rubber, new Vector3(0f, .43f, -1.62f), new Vector3(1.13f, .09f, .12f));

            engine = car.gameObject.AddComponent<AudioSource>();
            engine.clip = passingCarLoop;
            engine.playOnAwake = false;
            engine.loop = true;
            engine.spatialBlend = 1f;
            engine.rolloffMode = AudioRolloffMode.Linear;
            engine.minDistance = 2f;
            engine.maxDistance = 15f;
            engine.dopplerLevel = .25f;
            engine.volume = .12f;
            car.gameObject.SetActive(false);
        }

        private void Shell(string name, Material material, Vector3 lower, Vector3 upper, float zOffset = 0f)
        {
            var vertices = new Vector3[16];
            WriteRing(lower, 0);
            WriteRing(upper, 8);
            var triangles = new List<int>(84);
            for (int i = 0; i < 8; i++)
            {
                int next = (i + 1) % 8;
                triangles.AddRange(new[] { i, next, next + 8, i, next + 8, i + 8 });
            }
            for (int i = 1; i < 7; i++)
                triangles.AddRange(new[] { 0, i + 1, i, 8, i + 8, i + 9 });

            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            meshes.Add(mesh);
            AddMesh(name, car, mesh, material, Vector3.zero, Vector3.one);

            void WriteRing(Vector3 profile, int start)
            {
                float x = profile.x, y = profile.y, z = profile.z;
                const float bevel = .12f;
                vertices[start] = new Vector3(-x + bevel, y, zOffset - z);
                vertices[start + 1] = new Vector3(-x, y, zOffset - z + bevel);
                vertices[start + 2] = new Vector3(-x, y, zOffset + z - bevel);
                vertices[start + 3] = new Vector3(-x + bevel, y, zOffset + z);
                vertices[start + 4] = new Vector3(x - bevel, y, zOffset + z);
                vertices[start + 5] = new Vector3(x, y, zOffset + z - bevel);
                vertices[start + 6] = new Vector3(x, y, zOffset - z + bevel);
                vertices[start + 7] = new Vector3(x - bevel, y, zOffset - z);
            }
        }

        private void Wheel(string name, Material material, Vector3 position, float radius, float thickness)
        {
            var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = name;
            var collider = wheel.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            wheel.transform.SetParent(car, false);
            wheel.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, 0f, 90f));
            wheel.transform.localScale = new Vector3(radius * 2f, thickness * .5f, radius * 2f);
            wheel.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private GameObject Box(string name, Transform parent, Material material, Vector3 position, Vector3 size,
            bool castsShadows = true)
        {
            var box = AddMesh(name, parent, cube, material, position, size);
            if (!castsShadows) box.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return box;
        }

        private static GameObject AddMesh(string name, Transform parent, Mesh mesh, Material material,
            Vector3 position, Vector3 size)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        private Material MakeMaterial(string name, Color color, float smoothness = .15f)
        {
            var material = new Material(roadSurface.sharedMaterial) { name = name, color = color };
            material.SetFloat("_Smoothness", smoothness);
            materials.Add(material);
            return material;
        }

        private void OnDestroy()
        {
            foreach (var material in materials) Destroy(material);
            foreach (var mesh in meshes) Destroy(mesh);
        }
    }
}
