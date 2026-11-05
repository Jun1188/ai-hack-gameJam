using System;
using System.Collections.Generic;
using System.Linq;
using GrandpaRobot.Data;

namespace GrandpaRobot.Sim
{
    public sealed class ItemState
    {
        public string Id, Kind, NodeId;
        public int Level = -1, Rotation;
        public bool Carried, PushOnly, Aligned;
    }
    // Pure rules: no transforms, physics or model output determine the answer.
    public sealed class PuzzleState
    {
        public readonly StageDefinition Definition;
        public readonly Dictionary<string, ItemState> Items = new Dictionary<string, ItemState>();
        public readonly Dictionary<string, NodeDefinition> Nodes;
        public string HeldId { get; private set; }
        public bool CleanupComplete {get;private set;}
        public bool ArrangementComplete {get;private set;}
        public bool HasRepair => Definition.repair?.debrisIds!=null&&Definition.repair.debrisIds.Length>0;
        public int ClearedCount => !HasRepair?0:Definition.repair.debrisIds.Count(id=>Items[id].NodeId!=null&&!Items[id].Carried&&Nodes[Items[id].NodeId].role=="temporary");
        public int ArrangedCount => !HasRepair?0:Definition.repair.arrangementGoals.Count(GoalMet);
        public int RepairPhase => !CleanupComplete?0:!ArrangementComplete?1:Solved?3:2;
        public PuzzleState(StageDefinition stage, ObjectLibrary library)
        {
            Definition=stage;
            Nodes=stage.nodes.ToDictionary(x=>x.id);
            var defs=library.objects.ToDictionary(x=>x.id);
            foreach(var obj in stage.objects)
            {
                var def=defs[obj.definitionId];
                if(def.movable) Items.Add(obj.id,new ItemState { Id=obj.id, Kind=def.kind, Rotation=obj.rotation, PushOnly=def.pushOnly,
                    NodeId=string.IsNullOrEmpty(obj.initialNodeId)?null:obj.initialNodeId, Level=string.IsNullOrEmpty(obj.initialNodeId)?-1:0 });
            }
            CleanupComplete=ArrangementComplete=!HasRepair;
        }
        public ItemState At(string node,int level) => Items.Values.FirstOrDefault(x=>x.NodeId==node && x.Level==level && !x.Carried);
        public int Count(string node) => Items.Values.Count(x=>x.NodeId==node && !x.Carried);
        public bool GoalMet(GoalDefinition g)
        {
            var item=At(g.nodeId,g.level);
            return item!=null && item.Kind==g.kind && (g.rotation<0 || item.Rotation==g.rotation) && (!g.adjusted||item.Aligned);
        }
        public bool NodeSolved(string node) => Definition.goals.Any(x=>x.nodeId==node) && Definition.goals.Where(x=>x.nodeId==node).All(GoalMet);
        public bool Solved => ArrangementComplete&&Definition.goals.All(GoalMet);
        public int Completed => Definition.goals.Count(GoalMet);
        public string CanPick(string id)
        {
            if(!Items.TryGetValue(id,out var item)) return "옮길 수 있는 물체 ID가 아닙니다: "+id;
            if(item.PushOnly)return "이 기초 블록은 무거워요. 집기 대신 노드 쪽으로 밀어 주세요.";
            if(HeldId!=null) return "먼저 들고 있는 물체를 내려놓아 주세요.";
            if(item.NodeId!=null)
            {
                if(At(item.NodeId,item.Level+1)!=null) return "위의 물체를 먼저 옮겨 주세요.";
                if(Nodes.Values.Any(n=>n.requiresNodes!=null && n.requiresNodes.Contains(item.NodeId) && Count(n.id)>0))
                    return "이 물체가 받치는 위쪽 부품을 먼저 옮겨 주세요.";
            }
            return null;
        }
        public string Pick(string id)
        {
            var error=CanPick(id); if(error!=null) return error;
            var item=Items[id]; item.NodeId=null; item.Level=-1; item.Carried=true;item.Aligned=false; HeldId=id; return null;
        }
        public string CanPlace(string id,string nodeId)
        {
            if(HeldId!=id || !Items.ContainsKey(id)) return "그 물체를 들고 있지 않습니다.";
            if(!Nodes.TryGetValue(nodeId,out var node)) return "존재하지 않는 노드: "+nodeId;
            var gate=PlacementGate(node);if(gate!=null)return gate;
            if(Count(nodeId)>=node.capacity) return "그 노드에는 빈 공간이 없습니다.";
            if(node.requiresNodes!=null && node.requiresNodes.Any(x=>Count(x)==0)) return "먼저 받침 노드에 부품을 놓아 주세요.";
            // Assembly order is data-driven. Incorrect but supported kinds remain recoverable.
            foreach(var goal in Definition.goals.Where(x=>x.nodeId==nodeId))
                if(goal.prerequisites!=null && goal.prerequisites.Any(x=>!NodeSolved(x))) return "앞 단계의 구조가 아직 완성되지 않았습니다.";
            return null;
        }
        public string Place(string id,string nodeId)
        {
            var error=CanPlace(id,nodeId); if(error!=null) return error;
            var item=Items[id]; item.NodeId=nodeId; item.Level=Count(nodeId); item.Carried=false; HeldId=null;UpdateRepair(); return null;
        }
        public string Rotate(string id,int degrees)
        {
            if(!Items.TryGetValue(id,out var item)) return "존재하지 않는 물체: "+id;
            if(degrees%90!=0 || degrees<0 || degrees>=360) return "방향은 0, 90, 180, 270도 중 하나입니다.";
            item.Rotation=degrees;UpdateRepair(); return null;
        }
        public string CanPush(string id,string nodeId)
        {
            if(!Items.TryGetValue(id,out var item)||!item.PushOnly)return "밀 수 있는 기초 블록이 아닙니다.";
            if(HeldId!=null)return "들고 있는 물체를 먼저 내려놓아 주세요.";
            if(!Nodes.TryGetValue(nodeId,out var node)||node.position.y>.1f)return "기초 블록은 바닥의 노드로만 밀 수 있어요.";
            if(HasRepair&&!ArrangementComplete)return "잔해 정리와 재료 배치를 마친 다음 기초를 복구해 주세요.";
            if(Count(nodeId)>0)return "이미 물체가 있는 노드예요.";
            if(item.NodeId!=null&&Nodes.Values.Any(n=>n.requiresNodes!=null&&n.requiresNodes.Contains(item.NodeId)&&Count(n.id)>0))return "위에 조립된 부품을 먼저 내려 주세요.";
            return null;
        }
        public string Push(string id,string nodeId)
        {
            var error=CanPush(id,nodeId);if(error!=null)return error;
            var item=Items[id];item.NodeId=nodeId;item.Level=0;item.Aligned=false;UpdateRepair();return null;
        }
        public string Adjust(string id,string nodeId)
        {
            if(!Items.TryGetValue(id,out var item)||item.Carried||item.NodeId!=nodeId)return "먼저 그 노드에 부품을 올린 뒤 맞춰 주세요.";
            item.Aligned=true;UpdateRepair();return null;
        }
        string PlacementGate(NodeDefinition node)
        {
            if(!HasRepair)return null;
            if(node.role=="sorting"&&!CleanupComplete)return "먼저 무너진 목재를 모두 임시 정리장 C1~C5로 옮겨 건물 터를 비워 주세요.";
            if(node.role=="construction"&&!ArrangementComplete)return "재료를 분류 노드에 올바른 종류·방향·쌓는 순서로 배치한 뒤 곳간을 재건할 수 있어요.";
            return null;
        }
        void UpdateRepair()
        {
            var repair=Definition.repair;if(!HasRepair)return;
            if(!CleanupComplete&&ClearedCount==repair.debrisIds.Length)CleanupComplete=true;
            if(CleanupComplete&&!ArrangementComplete&&repair.arrangementGoals.All(GoalMet))ArrangementComplete=true;
        }
    }
}
