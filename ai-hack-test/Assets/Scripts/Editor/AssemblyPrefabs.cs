using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GrandpaRobot.Data;
using UnityEditor;
using UnityEngine;

namespace GrandpaRobot.Editor
{
    public static partial class PrototypeBuilder
    {
        static PrefabEntry[] CreateAssemblyPrefabs()
        {
            var list=new List<PrefabEntry>();
            void Build(string id,Action<Transform> create)
            {
                var root=new GameObject(id);create(root.transform);
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/Prototype/"+id+".prefab");
                UnityEngine.Object.DestroyImmediate(root);list.Add(new PrefabEntry{id=id,asset=prefab});
            }
            void Bar(Transform parent,Vector3 position,Vector3 scale)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
                go.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/wood.mat");
            }
            void Line(Transform parent,Vector3 from,Vector3 to,float width)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(parent,false);
                go.transform.localPosition=(from+to)/2;go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,to-from);
                go.transform.localScale=new Vector3(1,(to-from).magnitude,width);
            }
            Build("floor_frame",root=>{
                foreach(float z in new[]{-.47f,.47f})Bar(root,new Vector3(0,0,z),new Vector3(1,1,.06f));
                foreach(float x in new[]{-.47f,.47f})Bar(root,new Vector3(x,0,0),new Vector3(.06f,1,.94f));
            });
            foreach(var style in new[]{"wall_plain","wall_window","wall_door"})
                Build(style,root=>{
                    Bar(root,new Vector3(0,.46f,0),new Vector3(1,.08f,1));
                    if(style!="wall_door")Bar(root,new Vector3(0,-.46f,0),new Vector3(1,.08f,1));
                    foreach(float x in new[]{-.47f,.47f})Bar(root,new Vector3(x,0,0),new Vector3(.055f,1,1));
                    if(style=="wall_plain")foreach(float x in new[]{-.24f,0,.24f})Bar(root,new Vector3(x,0,0),new Vector3(.04f,1,1));
                    else if(style=="wall_window")
                    {
                        foreach(float x in new[]{-.22f,.22f})Bar(root,new Vector3(x,0,0),new Vector3(.04f,1,1));
                        foreach(float y in new[]{-.1f,.3f})Bar(root,new Vector3(0,y,0),new Vector3(.45f,.06f,1));
                    }
                    else
                    {
                        foreach(float x in new[]{-.18f,.18f})Bar(root,new Vector3(x,-.04f,0),new Vector3(.045f,.92f,1));
                        Bar(root,new Vector3(0,.36f,0),new Vector3(.4f,.06f,1));
                        foreach(float x in new[]{-.33f,.33f})Bar(root,new Vector3(x,-.46f,0),new Vector3(.25f,.08f,1));
                    }
                });
            Build("top_frame",root=>{
                Bar(root,new Vector3(0,0,-.47f),new Vector3(1,1,.06f));Bar(root,new Vector3(-.47f,0,0),new Vector3(.06f,1,1));
            });
            Build("rafter_pair",root=>{
                Line(root,new Vector3(0,-.45f,-.5f),new Vector3(0,.45f,0),.08f);
                Line(root,new Vector3(0,.45f,0),new Vector3(0,-.45f,.5f),.08f);
                Bar(root,new Vector3(0,-.43f,0),new Vector3(.8f,.08f,1));
            });
            Build("roof_panel",root=>{
                const string meshPath="Assets/Art/Models/RoofPanel.asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(!mesh){mesh=new Mesh{name="Sloped roof board"};AssetDatabase.CreateAsset(mesh,meshPath);}
                mesh.vertices=new[]{new Vector3(-.5f,.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(.5f,-.42f,.5f),new Vector3(-.5f,-.42f,.5f),new Vector3(-.5f,.42f,-.5f),new Vector3(.5f,.42f,-.5f),new Vector3(.5f,-.5f,.5f),new Vector3(-.5f,-.5f,.5f)};
                mesh.triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
                mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
                root.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                root.gameObject.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/wood.mat");
            });
            return list.ToArray();
        }
        static void AttachCane(Animator animator)
        {
            const string path="Assets/Art/Models/Grandfather/cane_grand.fbx";
            if(!File.Exists(path))return;
            AssetDatabase.ImportAsset(path);var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!source)return;
            var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);if(!hand)return;
            animator.Rebind();animator.Update(0);
            var root=new GameObject("Walking cane · textured");
            var model=(GameObject)PrefabUtility.InstantiatePrefab(source);model.transform.SetParent(root.transform,false);
            var renderers=model.GetComponentsInChildren<Renderer>();if(renderers.Length==0){UnityEngine.Object.DestroyImmediate(root);return;}
            Bounds Bounds(){var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
            var bounds=Bounds();
            if(bounds.size.z>bounds.size.y&&bounds.size.z>bounds.size.x)model.transform.rotation=Quaternion.Euler(90,0,0);
            else if(bounds.size.x>bounds.size.y)model.transform.rotation=Quaternion.Euler(0,0,90);
            bounds=Bounds();float length=Mathf.Clamp(hand.position.y,.75f,1.05f);
            root.transform.localScale=Vector3.one*(length/Mathf.Max(.001f,bounds.size.y));
            bounds=Bounds();model.transform.position-=new Vector3(bounds.center.x,bounds.max.y-.03f,bounds.center.z);
            var matPath="Assets/Art/Materials/Cane_Lit.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,matPath);}
            var textureFolder=Path.GetDirectoryName(Directory.GetFiles("Assets/Art/Models/Grandfather/wooden walking cane 3d model","Color.jpg",SearchOption.AllDirectories).First());
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>((textureFolder+"/Color.jpg").Replace('\\','/')));
            var normalPath=(textureFolder+"/Normal.png").Replace('\\','/');var normalImporter=(TextureImporter)AssetImporter.GetAtPath(normalPath);
            if(normalImporter.textureType!=TextureImporterType.NormalMap){normalImporter.textureType=TextureImporterType.NormalMap;normalImporter.SaveAndReimport();}
            mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_Smoothness",.25f);mat.SetFloat("_Metallic",0);EditorUtility.SetDirty(mat);
            foreach(var renderer in renderers)renderer.sharedMaterial=mat;
            foreach(var collider in model.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/Prototype/Cane.prefab");
            // Keep the prop out of the animated bone hierarchy: Mixamo bone scaling can
            // shear a world-space upright prop. Follow the grip after animation instead.
            root.transform.SetParent(animator.transform.parent,true);root.transform.position=hand.position;
            var alignment=root.AddComponent<GrandpaRobot.Presentation.WalkingCane>();alignment.groundRoot=animator.transform.parent;alignment.grip=hand;alignment.referenceLength=length;
            Debug.Log("CANE_ATTACHED: length="+length+" textured="+(mat.GetTexture("_BaseMap")!=null));
        }
    }
}
