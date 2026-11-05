using System;
using GrandpaRobot.Data;
using GrandpaRobot.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UIElements;

namespace GrandpaRobot.Editor
{
    public static class PresentationChecks
    {
        static void Require(bool condition,string message){if(!condition)throw new Exception("PRESENTATION_CHECK_FAILED: "+message);}
        public static void Run()
        {
            foreach(var aspect in new[]{16f/9,4f/3,9f/16})
            {
                var cameraObject=new GameObject("Editor camera boundary check");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.aspect=aspect;
                var rig=cameraObject.AddComponent<StageCamera>();rig.view=camera;rig.ResetStage(30,26,1.2f);rig.Apply();float size=camera.orthographicSize;
                var plane=new Plane(Vector3.up,Vector3.zero);
                for(int yaw=0;yaw<360;yaw+=5)
                {
                    rig.azimuth=yaw;rig.Apply();Require(Mathf.Abs(camera.orthographicSize-size)<.0001f,"rotation never changes zoom");
                    foreach(float x in new[]{0f,1f})foreach(float y in new[]{0f,1f})
                    {
                        var ray=camera.ViewportPointToRay(new Vector3(x,y));Require(plane.Raycast(ray,out var distance),"camera intersects ground");
                        var point=ray.GetPoint(distance);Require(Mathf.Abs(point.x)<15&&Mathf.Abs(point.z)<13,"full orbit stays inside map");
                    }
                }
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
            var tree=AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>("Assets/UI/WellHud.uxml").CloneTree();
            var input=tree.Q<UnityEngine.UIElements.TextField>("prompt");
            Require(input!=null,"UI Toolkit input exists");
            input.value="곳간을 복구해 주세요";Require(input.value=="곳간을 복구해 주세요","Korean Unicode value roundtrip");
            var catalog=AssetDatabase.LoadAssetAtPath<AssetCatalog>("Assets/Data/PrototypeCatalog.asset");
            var instance=UnityEngine.Object.Instantiate(catalog.Prefab("grandfather"));
            var graph=PlayableGraph.Create("Editor-only character deformation check");
            var idleMesh=new Mesh();var walkMesh=new Mesh();
            try
            {
                var driver=instance.GetComponent<GrandfatherLocomotion>();var animator=instance.GetComponentInChildren<Animator>();
                Require(driver&&driver.controller&&driver.animator==animator,"runtime locomotion binding saved in prefab");
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.enabled=true;
                var renderer=instance.GetComponentInChildren<SkinnedMeshRenderer>();
                Require(renderer.sharedMesh.uv.Length>0,"original UV coordinates preserved");
                Require(renderer.sharedMaterial.GetTexture("_BaseMap"),"provided GRAND color texture assigned");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var controller=AnimatorControllerPlayable.Create(graph,driver.controller);controller.SetLayerWeight(0,1);
                AnimationPlayableOutput.Create(graph,"Character",animator).SetSourcePlayable(controller);graph.Play();
                controller.Play("Base Layer.Idle",0,0);
                for(int i=0;i<12;i++)graph.Evaluate(.05f);
                Require(controller.GetCurrentAnimatorStateInfo(0).IsName("Idle"),"Idle state evaluates");
                renderer.BakeMesh(idleMesh);
                var arm=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);var hand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
                var armAngle=Vector3.Angle(hand.position-arm.position,-animator.transform.up);
                Require(armAngle<75,"Idle deforms the arms away from T-pose (angle="+armAngle+")");
                controller.SetTrigger("Walk");for(int i=0;i<14;i++)graph.Evaluate(.05f);
                Require(controller.GetCurrentAnimatorStateInfo(0).IsName("Walk"),"Walk trigger transitions");
                renderer.BakeMesh(walkMesh);float change=0;var idle=idleMesh.vertices;var walk=walkMesh.vertices;
                for(int i=0;i<idle.Length;i++)change=Mathf.Max(change,(idle[i]-walk[i]).magnitude);
                Require(change>.01f,"Walk changes actual skinned vertices");
                controller.SetTrigger("Idle");for(int i=0;i<12;i++)graph.Evaluate(.05f);
                Require(controller.GetCurrentAnimatorStateInfo(0).IsName("Idle"),"Idle trigger returns to idle");
                Debug.Log("PRESENTATION_CHECKS_OK: fixed-zoom full camera orbit; UI Toolkit/Korean value; UV/color; Idle/Walk deformation; armAngle="+armAngle+" vertexDelta="+change);
            }
            finally {graph.Destroy();UnityEngine.Object.DestroyImmediate(instance);UnityEngine.Object.DestroyImmediate(idleMesh);UnityEngine.Object.DestroyImmediate(walkMesh);}
        }
    }
}
