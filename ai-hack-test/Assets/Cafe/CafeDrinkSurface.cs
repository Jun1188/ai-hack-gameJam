using UnityEngine;

namespace MapleCafe
{
    internal sealed class CafeDrinkSurface
    {
        internal enum Finish { Crema, Black, Rosetta, Vanilla, Condensed, Cocoa, Matcha, Strawberry, Yuja, Lemonade, Ssanghwa, Invalid }

        internal sealed class Drink
        {
            public readonly string Name;
            public readonly Color Color;
            public readonly Finish Pattern;

            public Drink(string name, Color color, Finish pattern)
            {
                Name = name;
                Color = color;
                Pattern = pattern;
            }
        }

        private static readonly Drink Espresso = new("에스프레소", new Color(.35f, .14f, .055f), Finish.Crema);
        private static readonly Drink Americano = new("아메리카노", new Color(.20f, .074f, .031f), Finish.Black);
        private static readonly Drink Latte = new("카페라떼", new Color(.66f, .36f, .17f), Finish.Rosetta);
        private static readonly Drink Vanilla = new("바닐라라떼", new Color(.74f, .47f, .24f), Finish.Vanilla);
        private static readonly Drink Condensed = new("연유라떼", new Color(.78f, .55f, .32f), Finish.Condensed);
        private static readonly Drink Cocoa = new("초코라떼", new Color(.45f, .22f, .12f), Finish.Cocoa);
        private static readonly Drink Matcha = new("녹차라떼", new Color(.37f, .49f, .19f), Finish.Matcha);
        private static readonly Drink Strawberry = new("딸기우유", new Color(.94f, .51f, .56f), Finish.Strawberry);
        private static readonly Drink Yuja = new("유자차", new Color(.86f, .53f, .12f), Finish.Yuja);
        private static readonly Drink Lemonade = new("레몬에이드", new Color(.95f, .75f, .28f), Finish.Lemonade);
        private static readonly Drink Ssanghwa = new("쌍화차", new Color(.31f, .14f, .075f), Finish.Ssanghwa);
        internal static readonly Drink Invalid = new(CafeSim.CafeQueueSim.FailedDrinkName, new Color(.58f, .11f, .83f), Finish.Invalid);
        private readonly MeshRenderer surface;
        private readonly Material material;
        private readonly Mesh mesh;
        private readonly MaterialPropertyBlock surfaceProperties = new();
        private readonly Texture2D[] textures = new Texture2D[(int)Finish.Invalid + 1];

        internal CafeDrinkSurface(Renderer liquid)
        {
            var disk = new GameObject("Drink surface pattern");
            disk.transform.SetParent(liquid.transform.parent, false);
            disk.transform.localPosition = liquid.transform.localPosition + Vector3.up * .0025f;
            disk.AddComponent<MeshFilter>().sharedMesh = mesh = CreateDisk(liquid.transform.localScale.x * .493f);
            surface = disk.AddComponent<MeshRenderer>();
            material = new Material(liquid.sharedMaterial);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .32f);
            surface.sharedMaterial = material;
            surface.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Clear();
        }

        internal static Drink Match(int contents)
        {
            const int shot = 1 << (int)CafeBrewStation.IngredientKind.Shot;
            const int water = 1 << (int)CafeBrewStation.IngredientKind.Water;
            const int milk = 1 << (int)CafeBrewStation.IngredientKind.Milk;
            const int vanilla = 1 << (int)CafeBrewStation.IngredientKind.Vanilla;
            const int condensed = 1 << (int)CafeBrewStation.IngredientKind.Condensed;
            const int cocoa = 1 << (int)CafeBrewStation.IngredientKind.Cocoa;
            const int matcha = 1 << (int)CafeBrewStation.IngredientKind.Matcha;
            const int strawberry = 1 << (int)CafeBrewStation.IngredientKind.Strawberry;
            const int yuja = 1 << (int)CafeBrewStation.IngredientKind.Yuja;
            const int lemon = 1 << (int)CafeBrewStation.IngredientKind.Lemon;
            const int soda = 1 << (int)CafeBrewStation.IngredientKind.Soda;
            const int ssanghwa = 1 << (int)CafeBrewStation.IngredientKind.Ssanghwa;
            const int nuts = 1 << (int)CafeBrewStation.IngredientKind.Nuts;
            return contents switch
            {
                shot => Espresso,
                (shot | water) => Americano,
                (shot | milk) => Latte,
                (shot | milk | vanilla) => Vanilla,
                (shot | milk | condensed) => Condensed,
                (milk | cocoa) => Cocoa,
                (milk | matcha) => Matcha,
                (milk | strawberry) => Strawberry,
                (water | yuja) => Yuja,
                (soda | lemon) => Lemonade,
                (water | ssanghwa | nuts) => Ssanghwa,
                _ => null
            };
        }

        internal static Drink MatchServing(int contents, bool cold)
        {
            var drink = Match(contents);
            if (cold) return drink == Espresso || drink == Ssanghwa ? null : drink;
            return drink == Strawberry || drink == Lemonade ? null : drink;
        }

        internal void Show(Drink drink)
        {
            if (drink == null) { Clear(); return; }
            int index = (int)drink.Pattern;
            textures[index] ??= CreatePattern(drink);
            surfaceProperties.SetTexture("_BaseMap", textures[index]);
            surface.SetPropertyBlock(surfaceProperties);
            surface.enabled = true;
        }

        internal void Clear() => surface.enabled = false;

        internal void Dispose()
        {
            foreach (var texture in textures) Object.Destroy(texture);
            Object.Destroy(mesh);
            Object.Destroy(material);
        }

        private static Mesh CreateDisk(float radius)
        {
            const int sides = 48;
            var vertices = new Vector3[sides + 1];
            var uv = new Vector2[sides + 1];
            var triangles = new int[sides * 3];
            uv[0] = new Vector2(.5f, .5f);
            for (int i = 0; i < sides; i++)
            {
                float angle = i * (2f * Mathf.PI / sides);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices[i + 1] = new Vector3(direction.x, 0f, direction.y) * radius;
                uv[i + 1] = uv[0] + direction * .5f;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % sides + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            var disk = new Mesh { name = "Coffee surface", vertices = vertices, uv = uv, triangles = triangles };
            disk.RecalculateNormals();
            return disk;
        }

        private static Texture2D CreatePattern(Drink drink)
        {
            const int size = 128;
            var result = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            var cream = new Color(.96f, .83f, .62f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + .5f) / size * 2f - 1f;
                float v = (y + .5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float angle = Mathf.Atan2(v, u);
                float grain = Mathf.Sin(29f * u + 3f * Mathf.Sin(23f * v)) * Mathf.Sin(26f * v) * .025f;
                float rim = Stripe(r - .88f - .019f * Mathf.Sin(11f * angle), .067f);
                float design = drink.Pattern switch
                {
                    Finish.Crema => .60f * rim + .21f * Stripe(r - .61f - .085f * Mathf.Sin(3f * angle), .08f),
                    Finish.Black => .22f * rim,
                    Finish.Rosetta => Rosetta(u, v),
                    Finish.Vanilla => r < .74f ? .76f * Stripe(Mathf.Sin(17f * r - 2f * angle), .27f) : .12f * rim,
                    Finish.Condensed => .78f * Stripe(r - .49f - .085f * Mathf.Cos(3f * angle), .072f)
                        + .24f * Stripe(r - .24f, .043f),
                    Finish.Matcha => Rosetta(u, v),
                    Finish.Lemonade => LemonSlice(u, v),
                    Finish.Yuja => Mathf.Max(Stripe(v - .19f - u * .26f, .032f) * Stripe(u + .09f, .45f),
                        Stripe(v + .12f + u * .20f, .023f) * Stripe(u - .18f, .39f)),
                    Finish.Invalid => .78f * Stripe(r - .55f - .14f * Mathf.Sin(5f * angle), .075f)
                        + .32f * Stripe(r - .79f, .058f),
                    _ => 0f
                };
                Color highlight = drink.Pattern == Finish.Invalid ? new Color(.97f, .49f, 1f) : cream;
                Color pixel = Color.Lerp(drink.Color * (1f + grain), highlight, design);
                if (drink.Pattern == Finish.Cocoa)
                    pixel = Color.Lerp(pixel, new Color(.25f, .095f, .042f), Spots(u, v, 8, .074f));
                if (drink.Pattern == Finish.Strawberry)
                    pixel = Color.Lerp(pixel, new Color(.78f, .15f, .25f),
                        Mathf.Max(Spots(u, v, 7, .056f), .55f * Stripe(r - .69f - .11f * Mathf.Sin(3f * angle), .063f)));
                if (drink.Pattern == Finish.Ssanghwa)
                    pixel = Color.Lerp(pixel, new Color(.81f, .59f, .30f), Spots(u, v, 5, .13f));
                pixels[y * size + x] = pixel;
                pixels[y * size + x].a = 1f;
            }
            result.SetPixels(pixels);
            result.Apply(false, true);
            return result;
        }

        private static float Spots(float x, float y, int count, float size)
        {
            float mask = 0f;
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.4f;
                float radius = .25f + (i % 3) * .17f;
                float dx = x - Mathf.Cos(angle) * radius;
                float dy = y - Mathf.Sin(angle) * radius;
                mask = Mathf.Max(mask, Stripe(Mathf.Sqrt(dx * dx + dy * dy * 1.6f), size));
            }
            return mask;
        }

        private static float LemonSlice(float x, float y)
        {
            float u = x - .23f;
            float v = y + .18f;
            float radius = Mathf.Sqrt(u * u + v * v);
            float rays = radius < .31f ? Stripe(Mathf.Sin(Mathf.Atan2(v, u) * 3f), .23f) * .85f : 0f;
            return Mathf.Max(Mathf.Max(rays, Stripe(radius - .34f, .047f)), Spots(x, y, 7, .036f) * .58f);
        }

        private static float Rosetta(float x, float y)
        {
            float mask = Mathf.Abs(y) < .64f ? Stripe(x, .029f) : 0f;
            for (int leaf = 0; leaf < 4; leaf++)
            {
                float width = .38f - .05f * leaf;
                float height = -.31f + leaf * .18f + Mathf.Abs(x) * .62f;
                float oval = Mathf.Pow((Mathf.Abs(x) - width * .53f) / (width * .59f), 2f)
                    + Mathf.Pow((y - height) / .066f, 2f);
                mask = Mathf.Max(mask, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.84f, 1.1f, oval)));
            }
            return mask;
        }

        private static float Stripe(float distance, float width)
            => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(width * .72f, width, Mathf.Abs(distance)));
    }
}
