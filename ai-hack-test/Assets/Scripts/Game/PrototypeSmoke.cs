using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GrandpaRobot.Game
{
    // Explicit opt-in built-player acceptance runner. Never runs in normal gameplay.
    public sealed class PrototypeSmoke : MonoBehaviour
    {
        WellGame game;
        string failure;
        int checks;
        void Check(bool ok,string message){checks++;if(!ok){failure=message;Debug.LogError("SMOKE FAILED: "+message);}}
        public IEnumerator Run()
        {
            game=GetComponent<WellGame>();game.Testing=true;
            game.LoadStage(1);
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            // Native keyboard may already have been disabled before Start in a hidden window.
            var testKeyboard=InputSystem.AddDevice<Keyboard>("PrototypeTestKeyboard");
            InputSystem.EnableDevice(testKeyboard);testKeyboard.MakeCurrent();
            yield return null;
            Check(!game.State.Solved,"initial state");
            var origin=game.Grandfather.position;
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.W));
            yield return new WaitForSeconds(.3f);
            var animator=game.Grandfather.GetComponentInChildren<Animator>();
            Check(animator&&animator.avatar.isHuman&&animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"),"humanoid walk animation during movement");
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());yield return null;
            Check(Vector3.Distance(origin,game.Grandfather.position)>.2f,"WASD movement");
            game.ToggleChat();origin=game.Grandfather.position;
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.W,Key.R));
            yield return new WaitForSeconds(.2f);
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());yield return null;
            Check(Vector3.Distance(origin,game.Grandfather.position)<.01f,"chat blocks movement");game.CloseChat();
            yield return new WaitForSeconds(.3f);
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")&&!animator.applyRootMotion,"idle and code-driven root position");
            var cane=game.Grandfather.GetComponentInChildren<GrandpaRobot.Presentation.WalkingCane>();
            var caneRenderers=cane.GetComponentsInChildren<Renderer>();var caneBounds=caneRenderers[0].bounds;
            foreach(var renderer in caneRenderers)caneBounds.Encapsulate(renderer.bounds);
            Check(caneBounds.size.y>caneBounds.size.x&&caneBounds.size.y>caneBounds.size.z,"walking cane stays upright");
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.R));yield return null;
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());yield return new WaitForSeconds(.5f);
            Check(Mathf.Abs(game.CameraRig.azimuth-135)<1,"R rotates camera by 90 degrees");
            string previous=game.RunId;game.NextFixture();yield return null;
            Check(game.RunId!=previous&&game.Objects.Count==game.Stage.objects.Length,"stage reconstruction");game.LoadStage(1);yield return null;
            Check(game.Objects.Values.All(x=>x.transform.localScale.x>0&&x.transform.localScale.y>0&&x.transform.localScale.z>0),"all placed and stocked objects have visible scale");
            var json=JsonUtility.ToJson(game.Snapshot());Check(!json.Contains("goals")&&!json.Contains("prerequisites"),"hidden answers omitted");
            Check(game.ValidateResponse(new RobotResponse{status="plan",actions=new[]{new RobotAction{type="delete",objectId="B1"}}})!=null,"reject unauthorized action");
            Check(game.ValidateResponse(new RobotResponse{status="plan",actions=new[]{new RobotAction{type="place",objectId="B1",targetId="NOPE"}}})!=null,"reject unknown node");
            foreach(var aspect in new[]{16f/10,16f/9,4f/3,9f/16})
            {
                game.CameraRig.view.aspect=aspect;
                foreach(var zoom in new[]{.78f,1f})
                {
                game.CameraRig.zoom=zoom;
                for(int angle=0;angle<360;angle+=15)
                {
                    game.CameraRig.azimuth=angle;game.CameraRig.Apply();
                    foreach(float x in new[]{0f,1f})foreach(float y in new[]{0f,1f})
                    {
                        var ray=game.CameraRig.view.ViewportPointToRay(new Vector3(x,y,0));
                        var ground=new Plane(Vector3.up,Vector3.zero);
                        var hit=ground.Raycast(ray,out var distance);var p=ray.GetPoint(distance);
                        Check(hit&&Mathf.Abs(p.x)<game.Stage.width/2&&Mathf.Abs(p.z)<game.Stage.depth/2,"viewport stays inside stage "+aspect+" "+angle);
                    }
                }
                }
            }
            game.CameraRig.view.ResetAspect();game.CameraRig.ResetStage(game.Stage.width,game.Stage.depth);
            // Through the same HTTP/fallback and command runner used by the input UI.
            game.Submit("B1을 N1에 놓아");var deadline=Time.realtimeSinceStartup+40;
            while(game.Busy&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(!game.Busy&&game.State.At("N1",0)?.Id=="B1","HTTP natural language command");
            for(int i=1;i<=4;i++)
            {
                if(i>1)yield return Move("B"+i,"N"+i);
                yield return Move("T"+i,"N"+i);
                Check(game.State.CanPick("B"+i)!=null,"support removal blocked "+i);
            }
            yield return Move("S1","N5");yield return Move("S2","N6");yield return Move("P1","N7");
            Check(!game.State.Solved,"wrong rotation remains incomplete");
            yield return game.Execute(new[]{new RobotAction{type="rotate",objectId="P1",rotation=90}});
            Check(game.State.Solved,"full puzzle solved: "+game.Message);
            game.Recall();yield return null;
            Check(game.Recalling,"memory activation");
            Capture("well-completed.png");
            yield return new WaitForSeconds(1);
            var old=game.RunId;game.Submit("T1을 A1에 놓아");game.Cancel();game.Restart();yield return null;
            Check(!game.Busy&&game.RunId!=old&&!game.State.Solved&&game.State.HeldId==null,"cancel and reset");
            Check(FindObjectsByType<WellGame>().Length==1,"no duplicate systems");
            yield return new WaitForSeconds(.5f);
            Capture("well-start.png");
            var camera=game.CameraRig.view;var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;var oldSize=camera.orthographicSize;
            camera.transform.position=game.Grandfather.position+new Vector3(2.6f,2.1f,3.5f);camera.transform.LookAt(game.Grandfather.position+Vector3.up*.95f);camera.orthographicSize=1.35f;
            Capture("grandfather-preview.png");camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.orthographicSize=oldSize;
            game.LoadStage(0);yield return null;
            Check(game.State.Items.Count==26&&game.Stage.phases.Length==10,"spreadsheet house: 26 parts and 10 phases");
            Check(game.State.Completed==4,"four foundation blocks initially aligned");
            Check(game.State.CanPick("K5")!=null,"foundation uses push, not carry");
            Capture("house-start.png");
            foreach(var goal in game.Stage.goals)
            {
                if(game.State.GoalMet(goal))continue;
                var item=game.State.Items.Values.FirstOrDefault(x=>x.Kind==goal.kind&&x.NodeId==null&&!x.Carried);
                if(item==null){Check(false,"available part for "+goal.nodeId);break;}
                if(item.PushOnly)yield return game.Execute(new[]{new RobotAction{type="push",objectId=item.Id,targetId=goal.nodeId}});
                else
                {
                    if(goal.rotation>=0)yield return game.Execute(new[]{new RobotAction{type="rotate",objectId=item.Id,rotation=goal.rotation}});
                    yield return Move(item.Id,goal.nodeId);
                    if(goal.adjusted)yield return game.Execute(new[]{new RobotAction{type="adjust",objectId=item.Id,targetId=goal.nodeId}});
                }
                Check(game.State.GoalMet(goal),"house assembly "+goal.nodeId+": "+game.Message);
                if(failure!=null)break;
            }
            Check(game.State.Solved,"house fully assembled");Capture("house-completed.png");
            game.Restart();yield return null;
            yield return new WaitForSeconds(1);
            var report="{\"passed\":"+(failure==null?"true":"false")+",\"checks\":"+checks+",\"failure\":\""+(failure??"").Replace("\"","'")+"\",\"provider\":\""+game.Provider+"\"}";
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../selftest.json")),report);
            Debug.Log("PROTOTYPE_SMOKE: "+report);Application.Quit(failure==null?0:1);
        }
        IEnumerator Move(string id,string node)
        {
            yield return game.Execute(new[]{new RobotAction{type="pick_up",objectId=id},new RobotAction{type="place",objectId=id,targetId=node}});
            Check(game.State.Items[id].NodeId==node,"executed placement "+id+" -> "+node+": "+game.Message);
        }
        void Capture(string filename)
        {
            // An explicit URP render works even when the automated player window is hidden.
            // Captures the 3D world; IMGUI is intentionally not part of this render target.
            var rt=RenderTexture.GetTemporary(1280,800,24,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;
            RenderPipeline.SubmitRenderRequest(game.CameraRig.view,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            RenderTexture.active=rt;
            var texture=new Texture2D(1280,800,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,800),0,0);texture.Apply();
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath,"../"+filename)),texture.EncodeToPNG());
            Check(texture.GetPixel(640,400)!=Color.magenta,"render target is not missing-shader pink");
            Destroy(texture);RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);
        }
    }
}
