using UnityEngine;

namespace MapleCafe
{
    internal sealed class CafeFloatingIce
    {
        private readonly Transform root;
        private readonly Transform surface;
        private readonly Transform[] cubes = new Transform[3];
        private readonly Renderer[] renderers = new Renderer[3];
        private readonly MaterialPropertyBlock tint = new();
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private readonly float spread;

        internal CafeFloatingIce(Transform parent, Transform liquidSurface, Material material, float spread)
        {
            surface = liquidSurface;
            this.spread = spread;
            root = new GameObject("Floating ice").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < cubes.Length; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Ice cube";
                cube.transform.SetParent(root, false);
                cube.transform.localScale = new Vector3(.027f + i * .003f, .024f + i * .002f, .029f);
                var collider = cube.GetComponent<Collider>();
                collider.enabled = false;
                Object.Destroy(collider);
                var renderer = cube.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[i] = renderer;
                cubes[i] = cube.transform;
            }
            SetVisible(false);
        }

        internal void SetVisible(bool visible) => root.gameObject.SetActive(visible);

        internal void SetDrinkColor(Color color)
        {
            tint.SetColor(BaseColor, Color.Lerp(color, Color.white, .45f));
            foreach (var renderer in renderers) renderer.SetPropertyBlock(tint);
        }

        internal void Animate(float time)
        {
            if (!root.gameObject.activeSelf) return;
            root.position = surface.position;
            for (int i = 0; i < cubes.Length; i++)
            {
                float phase = i * 2.0944f;
                float angle = phase + Mathf.Sin(time * .85f + phase) * .17f;
                cubes[i].localPosition = new Vector3(
                    Mathf.Cos(angle) * spread,
                    .011f + Mathf.Sin(time * 2.2f + phase) * .004f,
                    Mathf.Sin(angle) * spread);
                cubes[i].localRotation = Quaternion.Euler(11f + Mathf.Sin(time * 1.7f + phase) * 7f,
                    i * 61f + Mathf.Sin(time + phase) * 9f, 10f);
            }
        }
    }
}
