using UnityEngine;

namespace MapleCafe
{
    // TextMesh can repack the built-in font atlas as signs come into view.
    [ExecuteAlways]
    public sealed class CafeSignFonts : MonoBehaviour
    {
        [SerializeField] private Font font;
        [SerializeField] private Renderer[] signs;
        private MaterialPropertyBlock atlasProperties;

        private void OnEnable()
        {
            Font.textureRebuilt += RefreshAtlas;
            RefreshAtlas(font);
        }

        private void OnDisable() => Font.textureRebuilt -= RefreshAtlas;

        private void RefreshAtlas(Font rebuilt)
        {
            if (font == null || rebuilt != font || signs == null)
                return;
            Texture atlas = font.material.mainTexture;
            if (atlas == null)
                return;
            atlasProperties ??= new MaterialPropertyBlock();
            atlasProperties.SetTexture("_MainTex", atlas);
            foreach (Renderer sign in signs)
                if (sign != null)
                    sign.SetPropertyBlock(atlasProperties);
        }
    }
}
