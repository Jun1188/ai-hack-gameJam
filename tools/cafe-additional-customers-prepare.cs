// Run with unity command script eval --file <absolute path>. Does not enter Play Mode.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

if (EditorApplication.isPlayingOrWillChangePlaymode)
    throw new InvalidOperationException("Stop Play Mode before preparing customer assets.");
const string root = "Assets/CafeSim/Resources/CafeCustomers/Incoming/";
foreach (var id in new[] { "cus_pangyo", "cus_student", "cus_gyeongsang" })
{
    string folder = root + id + "/";
    foreach (string file in Directory.GetFiles(folder).Where(p => !p.EndsWith(".meta", StringComparison.Ordinal)))
        AssetDatabase.ImportAsset(file.Replace('\\', '/'), ImportAssetOptions.ForceSynchronousImport);
    foreach (var texture in new[] { "Color.jpg", "Normal.png", "Metallic.jpg", "Roughness.jpg" })
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(folder + texture);
        importer.textureType = texture == "Normal.png" ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = texture == "Color.jpg";
        importer.maxTextureSize = 2048;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }
    typeof(CafeSim.CafeCustomerDelivery).GetMethod("PrepareFamily", BindingFlags.NonPublic | BindingFlags.Static)
        .Invoke(null, new object[] { id, folder, "Model.fbx" });
}
AssetDatabase.SaveAssets();
CafeSim.CafeCustomerCatalogImport.Rebuild();
var table = JsonUtility.FromJson<CafeSim.CustomerAssetTable>(File.ReadAllText(root + "../CustomerCatalog.json"));
foreach (var id in new[] { "cus_pangyo", "cus_student", "cus_gyeongsang" })
{
    var entry = table.items.Single(e => e.personaIds.Length == 1 && e.personaIds[0] == id);
    if (entry.status != "ready") throw new InvalidOperationException(id + ": " + string.Join(", ", entry.issues));
}
return "All three dedicated customer appearances are ready.";
