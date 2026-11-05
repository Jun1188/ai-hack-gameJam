using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GrandpaRobot.Editor
{
    public static partial class PrototypeBuilder
    {
        static void BindGrandfatherTextures(Material material)
        {
            const string root="Assets/Art/Models/Grandfather/GRAND";
            if(!Directory.Exists(root))return;
            var files=Directory.GetFiles(root,"*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).ToArray();
            var colorPath=files.FirstOrDefault(p=>Path.GetFileName(p)=="Color.jpg");
            if(colorPath==null)return;
            var color=AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath);
            if(!color)return;
            bool initial=material.GetTexture("_BaseMap")!=color;
            material.SetTexture("_BaseMap",color);
            if(initial)material.SetColor("_BaseColor",Color.white);
            var normalPath=files.FirstOrDefault(p=>Path.GetFileName(p)=="Normal.png");
            if(normalPath!=null)
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(normalPath);
                if(importer.textureType!=TextureImporterType.NormalMap){importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();}
                material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));material.EnableKeyword("_NORMALMAP");
            }
            var metalPath=files.FirstOrDefault(p=>p.EndsWith("_metallic.JPEG"));
            var roughPath=files.FirstOrDefault(p=>p.EndsWith("_roughness.JPEG"));
            if(metalPath!=null&&roughPath!=null)
            {
                var mi=(TextureImporter)AssetImporter.GetAtPath(metalPath);var ri=(TextureImporter)AssetImporter.GetAtPath(roughPath);
                bool mr=mi.isReadable,rr=ri.isReadable;
                Texture2D packed=null;
                try
                {
                    mi.isReadable=true;mi.sRGBTexture=false;mi.SaveAndReimport();
                    ri.isReadable=true;ri.sRGBTexture=false;ri.SaveAndReimport();
                    var metal=AssetDatabase.LoadAssetAtPath<Texture2D>(metalPath);var rough=AssetDatabase.LoadAssetAtPath<Texture2D>(roughPath);
                    int width=Mathf.Min(1024,metal.width),height=Mathf.Min(1024,metal.height);
                    var metals=metal.GetPixels32();var roughs=rough.GetPixels32();var pixels=new Color32[width*height];
                    for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                    {
                        var m=metals[(y*metal.height/height)*metal.width+x*metal.width/width];
                        var r=roughs[(y*rough.height/height)*rough.width+x*rough.width/width];
                        pixels[y*width+x]=new Color32(m.r,0,0,(byte)(255-r.r));
                    }
                    packed=new Texture2D(width,height,TextureFormat.RGBA32,false,true);packed.SetPixels32(pixels);packed.Apply();
                    const string packedPath="Assets/Art/Materials/Grandfather_MetallicSmoothness.png";
                    File.WriteAllBytes(packedPath,packed.EncodeToPNG());AssetDatabase.ImportAsset(packedPath);
                    var pi=(TextureImporter)AssetImporter.GetAtPath(packedPath);pi.sRGBTexture=false;pi.alphaSource=TextureImporterAlphaSource.FromInput;pi.SaveAndReimport();
                    material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));
                    material.SetFloat("_Smoothness",1);material.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                finally
                {
                    if(packed)UnityEngine.Object.DestroyImmediate(packed);
                    mi.isReadable=mr;mi.SaveAndReimport();ri.isReadable=rr;ri.SaveAndReimport();
                }
            }
            EditorUtility.SetDirty(material);Debug.Log("GRANDFATHER_TEXTURES_BOUND: "+colorPath);
        }
    }
}
