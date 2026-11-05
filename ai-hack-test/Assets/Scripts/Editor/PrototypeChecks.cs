using System;
using System.Collections.Generic;
using System.Linq;
using GrandpaRobot.Data;
using GrandpaRobot.Game;
using GrandpaRobot.Sim;
using UnityEditor;
using UnityEngine;

namespace GrandpaRobot.Editor
{
    public static class PrototypeChecks
    {
        static int checks;
        static void Check(bool ok,string message){checks++;if(!ok)throw new Exception("CHECK FAILED: "+message);}
        public static void Run()
        {
            checks=0;
            var cat=AssetDatabase.LoadAssetAtPath<AssetCatalog>("Assets/Data/PrototypeCatalog.asset");
            foreach(var asset in cat.stages)
            {
                var stage=JsonUtility.FromJson<StageDefinition>(asset.text);WellGame.ValidateData(stage,cat.Library(),cat);
                if(stage.id=="house_01"){CheckHouse(stage,cat.Library());continue;}
                var s=new PuzzleState(stage,cat.Library());
                Check(!s.Solved,"starts unsolved");
                Check(s.Pick("unknown")!=null,"unknown object");
                Check(s.Pick("P1")==null,"pick pulley");
                Check(s.Place("P1","N7")!=null,"unsupported placement");
                Check(s.Place("P1","A1")==null,"parking");
                Check(s.Pick("T1")==null&&s.Place("T1","N1")==null,"wrong but supported placement accepted");
                Check(!s.NodeSolved("N1"),"wrong kind does not satisfy goal");
                Check(s.Pick("T1")==null&&s.Place("T1","A2")==null,"wrong placement recoverable");
                s=new PuzzleState(stage,cat.Library());
                for(int i=1;i<=4;i++)
                {
                    Check(s.Pick("B"+i)==null&&s.Place("B"+i,"N"+i)==null,"base stone");
                    Check(s.Pick("T"+i)==null&&s.Place("T"+i,"N"+i)==null,"top stone");
                    Check(s.CanPick("B"+i)!=null,"cannot remove supporting stone");
                }
                Check(s.Pick("S1")==null&&s.Place("S1","N5")==null,"support 1");
                Check(s.Pick("S2")==null&&s.Place("S2","N6")==null,"support 2");
                Check(s.Pick("P1")==null&&s.Place("P1","N7")==null,"pulley");
                Check(!s.Solved,"orientation matters");
                Check(s.Rotate("P1",45)!=null,"reject non-cardinal orientation");
                Check(s.Rotate("P1",90)==null&&s.Solved,"all goals solved");
                Check(s.CanPick("S1")!=null,"cannot remove pulley support");
                var request=new RobotRequest{requestId="test",stageRunId="run",prompt="B1을 N1에 놓아",snapshot=new PublicSnapshot{
                    objects=new[]{new VisibleObject{id="B1"}},nodes=new[]{new VisibleNode{id="N1"}}}};
                var fallback=FallbackCommandProvider.Parse(request,"test");
                Check(fallback.status=="plan"&&fallback.actions.Length==2,"fallback exact ID placement");
                request.prompt="알아서 해";Check(FallbackCommandProvider.Parse(request,"test").status=="clarify","ambiguous instruction");
                var publicJson=JsonUtility.ToJson(request.snapshot);
                Check(!publicJson.Contains("goals")&&!publicJson.Contains("prerequisites")&&!publicJson.Contains("ghost"),"no hidden data in snapshot DTO");
            }
            Check(GridNavigation.Find(Vector3.zero,new Vector3(2,0,0),8,8,p=>false)!=null,"open path");
            Check(GridNavigation.Find(Vector3.zero,new Vector3(2,0,0),8,8,p=>true)==null,"unreachable path");
            Check(cat.Prefab("grandfather").GetComponentsInChildren<Renderer>().Length>0,"imported grandfather renderers");
            var animator=cat.Prefab("grandfather").GetComponentInChildren<Animator>();
            Check(animator&&animator.avatar&&animator.avatar.isValid&&animator.avatar.isHuman,"valid humanoid mapping");
            Check(animator.runtimeAnimatorController&&animator.runtimeAnimatorController.animationClips.Any(x=>x.name=="Idle")&&animator.runtimeAnimatorController.animationClips.Any(x=>x.name=="Walk"),"idle and walk animations linked");
            Check(Shader.Find("GrandpaRobot/WhiteboxToon")!=null,"toon shader imported");
            Debug.Log("PROTOTYPE_CHECKS_OK: "+checks);
        }
        static void CheckHouse(StageDefinition stage,ObjectLibrary library)
        {
            var s=new PuzzleState(stage,library);
            Check(s.Items.Count==26&&stage.phases.Length==10,"house spreadsheet counts");
            Check(s.Completed==4,"house starts with four aligned foundations");
            Check(s.CanPick("K5")!=null,"foundation carry forbidden");
            Check(s.Pick("FR1")==null&&s.CanPlace("FR1","N7")!=null,"floor frame requires six foundations");
            s=PrepareRepair(stage,library);
            Check(s.Push("K5","N5")==null&&s.Push("K6","N6")==null,"restore displaced foundations");
            Check(s.Pick("R5")==null&&s.CanPlace("R5","N23")!=null,"center rafter requires prior ends");
            s=PrepareRepair(stage,library);
            foreach(var goal in stage.goals)
            {
                if(s.GoalMet(goal))continue;
                var item=s.Items.Values.Where(x=>x.Kind==goal.kind&&!x.Carried&&(x.NodeId==null||s.Nodes[x.NodeId].role!="construction")).OrderBy(x=>s.CanPick(x.Id)==null?0:1).First();
                FreeItem(s,item);
                if(item.PushOnly)Check(s.Push(item.Id,goal.nodeId)==null,"push foundation "+item.Id);
                else Check(s.Pick(item.Id)==null&&s.Place(item.Id,goal.nodeId)==null,"assemble "+goal.nodeId);
                if(goal.rotation>=0)Check(s.Rotate(item.Id,goal.rotation)==null,"orient "+goal.nodeId);
                if(goal.adjusted)
                {
                    Check(!s.NodeSolved(goal.nodeId),"upper frame needs explicit alignment");
                    Check(s.Adjust(item.Id,goal.nodeId)==null,"align upper frame");
                }
                Check(s.GoalMet(goal),"house goal "+goal.nodeId);
            }
            Check(s.Solved,"house all phases complete");
            Check(s.CanPush("K1","N5")!=null,"assembled structure prevents foundation removal");
        }
        static PuzzleState PrepareRepair(StageDefinition stage,ObjectLibrary library)
        {
            var s=new PuzzleState(stage,library);if(!s.HasRepair)return s;
            Check(!s.CleanupComplete&&!s.ArrangementComplete&&!s.Solved,"farm starts with blocked ruined structure");
            Check(s.Push("K5","N5")!=null,"foundation repair gated by clearing and sorting");
            Check(s.Pick("FR1")==null,"ruined frame can be picked");
            Check(s.CanPlace("FR1","SORT7")!=null&&s.CanPlace("FR1","N7")!=null,"sorting and construction locked before clearing");
            s=new PuzzleState(stage,library);
            var remaining=stage.repair.debrisIds.Select(id=>s.Items[id]).ToList();
            var order=new List<string>();
            foreach(var goal in stage.repair.arrangementGoals)
            {
                var item=remaining.First(x=>x.Kind==goal.kind);remaining.Remove(item);order.Add(item.Id);
            }
            order.Reverse();
            for(int i=0;i<order.Count;i++)Check(s.Pick(order[i])==null&&s.Place(order[i],stage.repair.temporaryNodes[i/5])==null,"clear same collapsed component "+order[i]);
            Check(s.CleanupComplete&&!s.ArrangementComplete&&s.ClearedCount==20,"clearing unlocks sorting only");
            Check(s.Push("K5","N5")!=null,"clearing alone does not unlock construction");
            int count=0;
            foreach(var goal in stage.repair.arrangementGoals)
            {
                var item=s.Items.Values.First(x=>x.Kind==goal.kind&&x.NodeId!=null&&s.Nodes[x.NodeId].role=="temporary"&&s.CanPick(x.Id)==null);
                Check(s.Pick(item.Id)==null&&s.Place(item.Id,goal.nodeId)==null,"sort reusable component "+goal.nodeId);
                if(count++==0)
                {
                    Check(s.Rotate(item.Id,90)==null&&!s.GoalMet(goal),"sorting checks orientation, not just a cleared floor");
                }
                Check(s.Rotate(item.Id,goal.rotation)==null&&s.GoalMet(goal),"sorting kind, stack level and orientation");
            }
            Check(s.ArrangementComplete&&s.ArrangedCount==20&&!s.Solved,"arrangement unlocks reconstruction without duplicating materials");
            Check(s.Items.Count==26,"no replacement materials spawned");
            return s;
        }
        static void FreeItem(PuzzleState s,ItemState item)
        {
            while(item.NodeId!=null&&s.At(item.NodeId,item.Level+1)!=null)
            {
                var top=s.Items.Values.Where(x=>x.NodeId==item.NodeId).OrderByDescending(x=>x.Level).First();
                var buffer=s.Definition.repair.temporaryNodes.First(id=>id!=item.NodeId&&s.Count(id)<s.Nodes[id].capacity);
                Check(s.Pick(top.Id)==null&&s.Place(top.Id,buffer)==null,"clear upper material before reusing lower part");
            }
        }
    }
}
