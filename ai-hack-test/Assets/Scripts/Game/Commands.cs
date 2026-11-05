using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace GrandpaRobot.Game
{
    [Serializable] public sealed class RobotAction { public string type, objectId, targetId, text; public int rotation; }
    [Serializable] public sealed class RobotResponse
    {
        public string requestId, stageRunId, status, message, provider;
        public RobotAction[] actions;
    }
    [Serializable] public sealed class VisibleObject { public string id, name, kind, nodeId; public int level, rotation; public bool carried, pushOnly; }
    [Serializable] public sealed class VisibleNode { public string id, name; public int capacity, count; }
    [Serializable] public sealed class PublicSnapshot { public VisibleObject[] objects; public VisibleNode[] nodes; public string[] allowedActions; }
    [Serializable] public sealed class RobotRequest { public string requestId, stageRunId, prompt; public PublicSnapshot snapshot; }
    public interface IRobotCommandProvider { IEnumerator Interpret(RobotRequest request,Action<RobotResponse> complete); void Cancel(); }
    public sealed class BridgeCommandProvider : IRobotCommandProvider
    {
        UnityWebRequest active;
        public IEnumerator Interpret(RobotRequest request,Action<RobotResponse> complete)
        {
            using(var web=new UnityWebRequest("http://127.0.0.1:8765/interpret","POST"))
            {
                active=web; web.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(request)));
                web.downloadHandler=new DownloadHandlerBuffer(); web.SetRequestHeader("Content-Type","application/json"); web.timeout=33;
                yield return web.SendWebRequest();
                RobotResponse result=null;
                if(web.result==UnityWebRequest.Result.Success)
                    try { result=JsonUtility.FromJson<RobotResponse>(web.downloadHandler.text); } catch(ArgumentException) { }
                active=null;
                if(result==null) result=FallbackCommandProvider.Parse(request,"중계 연결 실패 — 대체 파서");
                complete(result);
            }
        }
        public void Cancel() { active?.Abort(); active=null; }
    }
    public sealed class FallbackCommandProvider : IRobotCommandProvider
    {
        public IEnumerator Interpret(RobotRequest request,Action<RobotResponse> complete) { complete(Parse(request,"대체 파서")); yield break; }
        public void Cancel() { }
        public static RobotResponse Parse(RobotRequest r,string reason)
        {
            var result=new RobotResponse {requestId=r.requestId,stageRunId=r.stageRunId,status="clarify",provider=reason,
                message="물체와 노드 ID를 알려 주세요. 예: B1을 N1에 놓아 / P1을 90도로 돌려",actions=Array.Empty<RobotAction>()};
            var p=r.prompt.ToUpperInvariant();
            var objects=Array.FindAll(r.snapshot.objects,x=>Regex.IsMatch(p,@"(?<![A-Z0-9_])"+Regex.Escape(x.id.ToUpperInvariant())+@"(?![A-Z0-9_])"));
            var nodes=Array.FindAll(r.snapshot.nodes,x=>Regex.IsMatch(p,@"(?<![A-Z0-9_])"+Regex.Escape(x.id.ToUpperInvariant())+@"(?![A-Z0-9_])"));
            var angle=Regex.Match(p,@"(?<!\d)(270|180|90|0)\s*도");
            if(objects.Length==1&&nodes.Length==1&&(p.Contains("밀")||p.Contains("조정")||p.Contains("맞춰")))
                result.actions=new[]{new RobotAction{type=p.Contains("밀")?"push":"adjust",objectId=objects[0].id,targetId=nodes[0].id}};
            else if(objects.Length>1&&objects.Length==nodes.Length&&objects.Length<=6&&(p.Contains("놓")||p.Contains("깔")||p.Contains("배치")))
            {
                Array.Sort(objects,(a,b)=>p.IndexOf(a.id,StringComparison.Ordinal).CompareTo(p.IndexOf(b.id,StringComparison.Ordinal)));
                Array.Sort(nodes,(a,b)=>p.IndexOf(a.id,StringComparison.Ordinal).CompareTo(p.IndexOf(b.id,StringComparison.Ordinal)));
                var list=new System.Collections.Generic.List<RobotAction>();
                for(int i=0;i<objects.Length;i++){if(!objects[i].carried)list.Add(new RobotAction{type="pick_up",objectId=objects[i].id});list.Add(new RobotAction{type="place",objectId=objects[i].id,targetId=nodes[i].id});}
                result.actions=list.ToArray();
            }
            else if(objects.Length==1 && angle.Success && (p.Contains("돌")||p.Contains("회전")))
            {
                result.actions=new[]{new RobotAction{type="rotate",objectId=objects[0].id,rotation=int.Parse(angle.Groups[1].Value)}};
            }
            else if(objects.Length==1 && nodes.Length==1 && (p.Contains("놓")||p.Contains("옮")||p.Contains("배치")||p.Contains("쌓")))
                result.actions=objects[0].carried ? new[]{new RobotAction{type="place",objectId=objects[0].id,targetId=nodes[0].id}} :
                    new[]{new RobotAction{type="pick_up",objectId=objects[0].id},new RobotAction{type="place",objectId=objects[0].id,targetId=nodes[0].id}};
            else if(objects.Length==1 && nodes.Length==0 && (p.Contains("집어")||p.Contains("들어")))
                result.actions=new[]{new RobotAction{type="pick_up",objectId=objects[0].id}};
            else if(nodes.Length==1 && objects.Length==0 && (p.Contains("이동")||p.Contains("가줘")||p.Contains("가 줘")))
                result.actions=new[]{new RobotAction{type="move_to",targetId=nodes[0].id}};
            if(result.actions.Length>0) {result.status="plan";result.message="알겠어요. 말씀하신 순서로 해 볼게요.";}
            return result;
        }
    }
}
