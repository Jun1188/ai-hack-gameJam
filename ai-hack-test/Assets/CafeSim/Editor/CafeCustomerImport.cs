using System.IO;
using UnityEditor;

namespace CafeSim
{
    public sealed class CafeCustomerImport : AssetPostprocessor
    {
        const string CustomerPath = "Assets/CafeSim/Resources/CafeCustomers/";

        public override uint GetVersion() => 2;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(CustomerPath, System.StringComparison.Ordinal)) return;

            if (assetPath.StartsWith(CafeCustomerCatalogImport.DropPath, System.StringComparison.Ordinal)) return;

            var importer = (ModelImporter)assetImporter;
            bool isMotion = Path.GetFileNameWithoutExtension(assetPath) is "Idle" or "Walk";
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.preserveHierarchy = true;
            importer.optimizeGameObjects = false;
            importer.importAnimation = isMotion;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importNormals = Path.GetFileNameWithoutExtension(assetPath) == "Female"
                ? ModelImporterNormals.Calculate : ModelImporterNormals.Import;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(CustomerPath, System.StringComparison.Ordinal)) return;
            if (assetPath.StartsWith(CafeCustomerCatalogImport.DropPath, System.StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = true;
                clip.loopPose = true;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
            }
            if (clips.Length > 0) importer.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(CustomerPath, System.StringComparison.Ordinal)) return;
            if (assetPath.StartsWith(CafeCustomerCatalogImport.DropPath, System.StringComparison.Ordinal)) return;

            var importer = (TextureImporter)assetImporter;
            string texture = Path.GetFileNameWithoutExtension(assetPath);
            importer.textureType = texture == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = texture == "Color";
            importer.maxTextureSize = texture == "ClothRegions" ? 1024 : 2048;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        }
    }
}
