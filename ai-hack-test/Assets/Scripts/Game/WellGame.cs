using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GrandpaRobot.Data;
using GrandpaRobot.Sim;
using GrandpaRobot.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandpaRobot.Game
{
    public sealed class WellGame : MonoBehaviour
    {
        public AssetCatalog catalog;
        public PuzzleState State {get;private set;}
        public StageDefinition Stage {get;private set;}
        public string RunId {get;private set;}
        public bool Busy {get;private set;}
        public bool ChatOpen {get;private set;}
        public string Message {get;private set;}="Q로 옛 우물의 모습을 떠올린 뒤, E로 로봇에게 설명해 주세요.";
        public string Provider {get;private set;}="Codex CLI · 연결 대기";
        public bool Testing;
        public IReadOnlyDictionary<string,GameObject> Objects=>objects;
        public Transform Robot=>robot;
        public Transform Grandfather=>grandfather;
        public StageCamera CameraRig=>cameraRig;
        readonly Dictionary<string,GameObject> objects=new Dictionary<string,GameObject>();
        readonly Dictionary<string,GameObject> ghosts=new Dictionary<string,GameObject>();
        readonly Dictionary<string,Vector3> labels=new Dictionary<string,Vector3>();
        readonly List<Bounds> scenery=new List<Bounds>();
        readonly List<Renderer> nodeMarkers=new List<Renderer>();
        Transform stageRoot,grandfather,robot;
        Transform environmentRoot,partsRoot,nodesRoot,memoryRoot,charactersRoot;
        Animator grandfatherAnimator;
        GrandfatherLocomotion grandfatherLocomotion;
        bool wasWalking;
        Vector3 grandfatherVelocity;
        public float grandfatherWalkSpeed=1.35f;
        StageCamera cameraRig;
        IRobotCommandProvider provider;
        Coroutine work;
        string pendingId;
        float recallUntil;
        int stageIndex;
        public float Speed=>Testing?24:3.5f;
        public bool Recalling=>Time.time<recallUntil;
        public IReadOnlyDictionary<string,Vector3> Labels=>labels;
        public bool IsHouse=>Stage!=null&&Stage.id=="house_01";
        public string PhaseText
        {
            get
            {
                if(Stage?.phases==null)return "배치 · 쌓기 · 방향";
                if(State.HasRepair&&!State.CleanupComplete)return "1 / 3 · 무너진 곳간 정리 · 잔해 "+State.ClearedCount+" / "+Stage.repair.debrisIds.Length;
                if(State.HasRepair&&!State.ArrangementComplete)return "2 / 3 · 재료의 위치·형태 맞추기 · "+State.ArrangedCount+" / "+Stage.repair.arrangementGoals.Length;
                var phase=Stage.phases.FirstOrDefault(p=>!p.nodes.All(State.NodeSolved));
                return phase==null?"농장의 곳간 복구 완료":"3 / 3 · 곳간 재건 · "+phase.name+" · "+phase.hint;
            }
        }

        void Start()
        {
            provider=new BridgeCommandProvider();
            cameraRig=Camera.main.GetComponent<StageCamera>();
            LoadStage(0);
        }
        public void LoadStage(int index)
        {
            Cancel();
            foreach(var child in transform.Cast<Transform>().ToArray())
            {
                if(!child.name.StartsWith("Stage · "))continue;
                child.gameObject.SetActive(false);
                if(Application.isPlaying)Destroy(child.gameObject);else DestroyImmediate(child.gameObject);
            }
            objects.Clear();ghosts.Clear();labels.Clear();scenery.Clear();nodeMarkers.Clear();
            stageIndex=index; Stage=catalog.Stage(index); ValidateData(Stage,catalog.Library(),catalog);
            State=new PuzzleState(Stage,catalog.Library()); RunId=Guid.NewGuid().ToString("N");
            stageRoot=new GameObject("Stage · "+Stage.id).transform;stageRoot.SetParent(transform);
            Transform Group(string name){var group=new GameObject(name).transform;group.SetParent(stageRoot,false);return group;}
            environmentRoot=Group("01 Environment · 농장 환경");partsRoot=Group("02 Parts · 잔해와 조립 부품");
            nodesRoot=Group("03 Nodes · 배치 지점");memoryRoot=Group("04 Memory · 회상 단서");charactersRoot=Group("05 Characters · 할아버지와 로봇");
            var floor=Primitive("Ground",PrimitiveType.Cube,new Vector3(0,-.18f,0),new Vector3(Stage.width,.35f,Stage.depth),catalog.Material(Stage.backgroundMaterialId));
            if(!IsHouse)
            {
                Primitive("Well cavity",PrimitiveType.Cylinder,new Vector3(0,.06f,0),new Vector3(1.8f,.08f,1.8f),catalog.Material("dark"));
                scenery.Add(new Bounds(Vector3.zero,new Vector3(1.3f,.15f,1.3f)));
            }
            var defs=catalog.Library().objects.ToDictionary(x=>x.id);
            foreach(var instance in Stage.objects)
            {
                var def=defs[instance.definitionId];
                var go=Instantiate(catalog.Prefab(def.prefabId),def.movable?partsRoot:environmentRoot);go.name=instance.id+" · "+def.name;
                go.transform.position=instance.position.Vector;
                var tilt=instance.visualEuler;
                go.transform.rotation=Quaternion.Euler(tilt?.x??0,instance.rotation,tilt?.z??0);
                var factor=instance.scale;
                go.transform.localScale=factor==null||factor.x<=0||factor.y<=0||factor.z<=0?def.size.Vector:Vector3.Scale(def.size.Vector,factor.Vector);
                if(!def.preservePrefabMaterials)foreach(var r in go.GetComponentsInChildren<Renderer>())r.sharedMaterial=catalog.Material(def.materialId);
                objects.Add(instance.id,go);
                if(def.movable) labels[instance.id]=go.transform.position;
                else if(def.blocksMovement)scenery.Add(new Bounds(go.transform.position,go.transform.localScale));
            }
            foreach(var node in Stage.nodes)
            {
                var marker=Primitive(node.id,PrimitiveType.Cylinder,new Vector3(node.position.x,.02f,node.position.z),new Vector3(.65f,.025f,.65f),catalog.Material("node"));
                marker.transform.SetParent(nodesRoot,true);nodeMarkers.Add(marker.GetComponent<Renderer>());
                labels[node.id]=node.position.Vector+Vector3.up*.15f;
            }
            foreach(var goal in Stage.goals)
            {
                var node=State.Nodes[goal.nodeId];var def=defs.Values.First(x=>x.kind==goal.kind);
                var ghost=Instantiate(catalog.Prefab(def.prefabId),memoryRoot);ghost.name="Memory · "+goal.nodeId+" / "+goal.level;
                ghost.transform.position=PlacementPosition(node,goal.level,def.size.y);
                ghost.transform.rotation=Quaternion.Euler(0,Mathf.Max(0,goal.rotation),0);ghost.transform.localScale=def.size.Vector*1.03f;
                foreach(var r in ghost.GetComponentsInChildren<Renderer>())r.sharedMaterial=catalog.Material("ghost");
                foreach(var c in ghost.GetComponentsInChildren<Collider>())RemoveComponent(c);
                ghost.SetActive(false);ghosts.Add(goal.nodeId+":"+goal.level,ghost);
            }
            if(State.HasRepair)
            {
                var memoryHeights=new Dictionary<string,float>();
                foreach(var goal in Stage.repair.arrangementGoals)
                {
                    var node=State.Nodes[goal.nodeId];var def=defs.Values.First(x=>x.kind==goal.kind);
                    var ghost=Instantiate(catalog.Prefab(def.prefabId),memoryRoot);ghost.name="Sorting memory · "+goal.nodeId;
                    var pose=StorageRotation(def,Mathf.Max(0,goal.rotation));var scale=def.size.Vector*node.displayScale;
                    ghost.transform.localScale=scale;ghost.transform.rotation=pose;
                    var height=RotatedSize(scale,pose).y;
                    float bottom=memoryHeights.TryGetValue(node.id,out var accumulated)?accumulated:0;
                    ghost.transform.position=node.position.Vector+Vector3.up*(bottom+height/2);
                    memoryHeights[node.id]=bottom+height+.025f;
                    foreach(var r in ghost.GetComponentsInChildren<Renderer>())r.sharedMaterial=catalog.Material("ghost");
                    foreach(var c in ghost.GetComponentsInChildren<Collider>())RemoveComponent(c);
                    ghost.SetActive(false);ghosts.Add("sort/"+goal.nodeId+":"+goal.level,ghost);
                }
            }
            grandfather=Character("Grandfather",Stage.grandfatherSpawn.Vector,false);
            grandfatherAnimator=grandfather.GetComponentInChildren<Animator>();
            grandfatherLocomotion=grandfather.GetComponentInChildren<GrandfatherLocomotion>();
            wasWalking=false;
            grandfatherVelocity=Vector3.zero;
            robot=Character("Robot",Stage.robotSpawn.Vector,true);
            cameraRig.ResetStage(Stage.width,Stage.depth,IsHouse?1.2f:.35f);recallUntil=0;ChatOpen=false;
            Message=IsHouse?"농장의 곳간이 무너졌어요. 잔해를 C1~C5에 정리 → 분류 노드의 위치·형태 맞추기 → 곳간 재건 순서로 복구해 주세요.":"돌 테두리 → 지지대 → 도르래. Q로 회상하고 E로 설명해 주세요.";
            var audio=stageRoot.gameObject.AddComponent<AudioSource>();audio.clip=catalog.Sound(Stage.soundId);audio.playOnAwake=false;audio.loop=true;audio.volume=.07f;if(Application.isPlaying)audio.Play();
        }
        static void RemoveComponent(Component component){if(Application.isPlaying)Destroy(component);else DestroyImmediate(component);}
        public void RefreshEditorPreview()
        {
            if(Application.isPlaying)return;
            cameraRig=Camera.main.GetComponent<StageCamera>();LoadStage(0);cameraRig.Apply();
            foreach(var marker in nodeMarkers)marker.enabled=NodeVisible(marker.gameObject.name);
        }
        public static void ValidateData(StageDefinition s,ObjectLibrary lib,AssetCatalog cat)
        {
            if(s==null||s.width<6||s.depth<6||s.objects==null||s.nodes==null||s.goals==null||s.goals.Length==0)throw new InvalidOperationException("잘못된 스테이지 정의");
            var defs=lib.objects.ToDictionary(x=>x.id); var ids=new HashSet<string>();
            foreach(var d in defs.Values)
                if(!cat.Prefab(d.prefabId)||!cat.Material(d.materialId)||d.size==null||d.size.x<=0||d.size.y<=0||d.size.z<=0)throw new InvalidOperationException("객체 에셋/크기 오류: "+d.id);
            foreach(var obj in s.objects)
                if(!ids.Add(obj.id)||!defs.ContainsKey(obj.definitionId)||obj.position==null)throw new InvalidOperationException("객체 ID/참조 오류: "+obj.id);
            foreach(var n in s.nodes)
                if(!ids.Add(n.id)||n.capacity<1||n.position==null||Mathf.Abs(n.position.x)>s.width/2-1||Mathf.Abs(n.position.z)>s.depth/2-1)throw new InvalidOperationException("노드 오류: "+n.id);
            var nodes=s.nodes.ToDictionary(x=>x.id);
            foreach(var n in s.nodes)
                if(n.requiresNodes!=null && n.requiresNodes.Any(x=>!nodes.ContainsKey(x)))throw new InvalidOperationException("받침 노드 참조 오류");
            foreach(var g in s.goals)
                if(!nodes.ContainsKey(g.nodeId)||g.level<0||g.level>=nodes[g.nodeId].capacity||!defs.Values.Any(x=>x.kind==g.kind)||
                    (g.prerequisites!=null && g.prerequisites.Any(x=>!nodes.ContainsKey(x))))throw new InvalidOperationException("퍼즐 조건 참조 오류");
            if(s.objects.Any(o=>!string.IsNullOrEmpty(o.initialNodeId)&&!nodes.ContainsKey(o.initialNodeId)))throw new InvalidOperationException("초기 배치 노드 참조 오류");
            if(s.repair?.debrisIds!=null&&s.repair.debrisIds.Length>0)
            {
                if(s.repair.debrisIds.Distinct().Count()!=s.repair.debrisIds.Length||s.repair.debrisIds.Any(id=>!s.objects.Any(o=>o.id==id&&defs[o.definitionId].movable&&!defs[o.definitionId].pushOnly)))throw new InvalidOperationException("잔해 ID/재료 참조 오류");
                if(s.repair.temporaryNodes==null||s.repair.temporaryNodes.Any(id=>!nodes.ContainsKey(id)||nodes[id].role!="temporary"))throw new InvalidOperationException("임시 정리장 참조 오류");
                if(s.repair.arrangementGoals==null||s.repair.arrangementGoals.Length!=s.repair.debrisIds.Length)throw new InvalidOperationException("재료 배치 목표 개수 오류");
                foreach(var goal in s.repair.arrangementGoals)
                    if(!nodes.ContainsKey(goal.nodeId)||nodes[goal.nodeId].role!="sorting"||goal.level<0||goal.level>=nodes[goal.nodeId].capacity||!defs.Values.Any(d=>d.kind==goal.kind))throw new InvalidOperationException("분류 노드/형태 목표 참조 오류");
            }
            if(!cat.Material(s.backgroundMaterialId)||!cat.Sound(s.soundId))throw new InvalidOperationException("스테이지 에셋 참조 오류");
        }
        GameObject Primitive(string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(environmentRoot?environmentRoot:stageRoot);go.transform.position=position;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        Transform Character(string name,Vector3 position,bool isRobot)
        {
            var root=new GameObject(name).transform;root.SetParent(charactersRoot);root.position=position;
            if(!isRobot && catalog.Prefab("grandfather"))
            {
                var visual=Instantiate(catalog.Prefab("grandfather"),root);visual.name="Grandfather visual · grand_9";
                return root;
            }
            void Part(string n,PrimitiveType t,Vector3 p,Vector3 scale,string material)
            {
                var go=Primitive(n,t,position+p,scale,catalog.Material(material));go.transform.SetParent(root);RemoveComponent(go.GetComponent<Collider>());
            }
            Part("Body",isRobot?PrimitiveType.Capsule:PrimitiveType.Cube,new Vector3(0,.85f,0),new Vector3(.55f,.55f,.4f),isRobot?"robot":"cloth");
            Part("Head",PrimitiveType.Sphere,new Vector3(0,1.55f,0),new Vector3(.6f,.5f,.5f),isRobot?"robot":"skin");
            Part("Left arm",PrimitiveType.Cube,new Vector3(-.4f,.9f,0),new Vector3(.18f,.7f,.18f),isRobot?"robot":"cloth");
            Part("Right arm",PrimitiveType.Cube,new Vector3(.4f,.9f,0),new Vector3(.18f,.7f,.18f),isRobot?"robot":"cloth");
            Part("Left leg",PrimitiveType.Cube,new Vector3(-.16f,.25f,0),new Vector3(.2f,.5f,.25f),"dark");
            Part("Right leg",PrimitiveType.Cube,new Vector3(.16f,.25f,0),new Vector3(.2f,.5f,.25f),"dark");
            if(isRobot)Part("Visor",PrimitiveType.Cube,new Vector3(0,1.55f,-.24f),new Vector3(.42f,.17f,.05f),"dark");
            else
            {
                Part("Hat brim",PrimitiveType.Cylinder,new Vector3(0,1.82f,0),new Vector3(.9f,.04f,.8f),"hat");
                Part("Hat",PrimitiveType.Cylinder,new Vector3(0,1.96f,0),new Vector3(.55f,.12f,.5f),"hat");
                Part("Moustache",PrimitiveType.Cube,new Vector3(0,1.44f,-.24f),new Vector3(.3f,.08f,.06f),"cloth");
            }
            return root;
        }
        public void Recall() {if(!Recalling){recallUntil=Time.time+3;Message=State.HasRepair&&!State.CleanupComplete?"먼저 건물 터의 목재 잔해를 임시 정리장 C1~C5로 옮겨 주세요.":"회상은 할아버지에게만 보입니다. 노드의 위치·종류·쌓는 순서·방향을 기억하세요.";}}
        public void ToggleChat() {ChatOpen=!ChatOpen;}
        public void CloseChat() {ChatOpen=false;}
        public void Restart() {LoadStage(stageIndex);}
        public void NextFixture() {LoadStage((stageIndex+1)%catalog.stages.Length);}
        void Update()
        {
            if(State==null)return;
            var currentPhase=IsHouse?Stage.phases.FirstOrDefault(p=>!p.nodes.All(State.NodeSolved)):null;
            foreach(var pair in ghosts)
            {
                bool show=Recalling;
                if(State.HasRepair&&!State.CleanupComplete)show=false;
                else if(State.HasRepair&&!State.ArrangementComplete)show&=pair.Key.StartsWith("sort/");
                else show&=!pair.Key.StartsWith("sort/")&&(!IsHouse||currentPhase==null||currentPhase.nodes.Contains(pair.Key.Split(':')[0]));
                pair.Value.SetActive(show);
            }
            foreach(var marker in nodeMarkers)
            {
                marker.enabled=NodeVisible(marker.gameObject.name);
                marker.sharedMaterial=catalog.Material(Recalling?"ghost":"node");
            }
            var k=Keyboard.current; if(k==null)return;
            bool moving=false;
            var previousPosition=grandfather.position;
            if(!ChatOpen)
            {
                if(k.eKey.wasPressedThisFrame&&!Busy)ToggleChat();
                if(k.qKey.wasPressedThisFrame)Recall();
                var input=new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
                var forward=Vector3.ProjectOnPlane(cameraRig.transform.forward,Vector3.up).normalized;
                var right=Vector3.ProjectOnPlane(cameraRig.transform.right,Vector3.up).normalized;
                var desired=ChatOpen?Vector3.zero:(right*input.x+forward*input.y).normalized*grandfatherWalkSpeed;
                grandfatherVelocity=Vector3.MoveTowards(grandfatherVelocity,desired,Time.deltaTime*5);
                var movement=grandfatherVelocity*Time.deltaTime;
                var next=grandfather.position+movement;
                if(!Blocked(next,null,true)){grandfather.position=next;moving=movement.sqrMagnitude>.00001f;}
                if(moving)grandfather.rotation=Quaternion.RotateTowards(grandfather.rotation,Quaternion.LookRotation(movement),240*Time.deltaTime);
            }
            else grandfatherVelocity=Vector3.zero;
            if(grandfatherLocomotion)grandfatherLocomotion.SetSpeed(Vector3.Distance(previousPosition,grandfather.position)/Mathf.Max(.0001f,Time.deltaTime));
            else if(grandfatherAnimator&&moving!=wasWalking)
            {
                grandfatherAnimator.ResetTrigger(moving?"Idle":"Walk");
                grandfatherAnimator.SetTrigger(moving?"Walk":"Idle");
            }
            wasWalking=moving;
            cameraRig.inputBlocked=ChatOpen;
            if(State.HeldId!=null)
            {
                var held=objects[State.HeldId];held.transform.position=robot.position+new Vector3(0,1.3f,-.65f);
            }
            foreach(var pair in objects)if(labels.ContainsKey(pair.Key))labels[pair.Key]=pair.Value.transform.position+Vector3.up*.7f;
        }
        bool Blocked(Vector3 position,string ignore=null,bool player=false)
        {
            if(Mathf.Abs(position.x)>Stage.width/2-.5f||Mathf.Abs(position.z)>Stage.depth/2-.5f)return true;
            foreach(var b in scenery)if(Inside(position,b,.3f))return true;
            foreach(var pair in State.Items)
            {
                if(pair.Key==ignore||pair.Value.Carried)continue;
                var go=objects[pair.Key];if(go.transform.position.y>2)continue;
                if(Inside(position,ItemBounds(pair.Key,go.transform.position,pair.Value.Rotation),.25f))return true;
            }
            return false;
        }
        static bool Inside(Vector3 p,Bounds b,float padding) => Mathf.Abs(p.x-b.center.x)<b.extents.x+padding && Mathf.Abs(p.z-b.center.z)<b.extents.z+padding;
        Bounds ItemBounds(string id,Vector3 position,int rotation)
        {
            var renderers=objects[id].GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            var size=bounds.size;bounds.size=new Vector3(Mathf.Max(.005f,size.x-.025f),Mathf.Max(.005f,size.y-.025f),Mathf.Max(.005f,size.z-.025f));return bounds;
        }
        static Vector3 RotatedSize(Vector3 size,Quaternion q)
        {
            var x=q*Vector3.right*size.x;var y=q*Vector3.up*size.y;var z=q*Vector3.forward*size.z;
            return new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
        }
        static bool IsStorage(NodeDefinition n)=>n.role=="temporary"||n.role=="sorting";
        static Quaternion StorageRotation(ObjectDefinition d,int yaw)=>Quaternion.Euler(d.storageEuler?.x??0,yaw,d.storageEuler?.z??0);
        Vector3 PlacementSize(string id,NodeDefinition n)=>ObjectDef(id).size.Vector*(IsStorage(n)?n.displayScale:1);
        Quaternion PlacementRotation(string id,NodeDefinition n,int yaw)=>IsStorage(n)?StorageRotation(ObjectDef(id),yaw):Quaternion.Euler(0,yaw,0);
        ObjectDefinition ObjectDef(string id)=>catalog.Library().objects.First(x=>x.id==Stage.objects.First(o=>o.id==id).definitionId);
        string SpatialError(string id,Vector3 position,int rotation,string destination=null,Vector3? candidateSize=null)
        {
            var size=candidateSize??objects[id].transform.localScale;
            var pose=destination==null?Quaternion.Euler(0,rotation,0):PlacementRotation(id,State.Nodes[destination],rotation);
            size=RotatedSize(size,pose);
            var bounds=new Bounds(position,size-Vector3.one*.025f);
            bool structural=destination!=null&&State.Nodes[destination].allowStructuralOverlap;
            if(bounds.min.x < -Stage.width/2 || bounds.max.x > Stage.width/2 || bounds.min.z < -Stage.depth/2 || bounds.max.z > Stage.depth/2)
                return "물체가 맵 밖으로 나가요. 방향이나 임시 노드를 바꿔 주세요.";
            foreach(var pair in State.Items)
                if(pair.Key!=id&&!pair.Value.Carried&&!(structural&&pair.Value.NodeId!=null)&&bounds.Intersects(ItemBounds(pair.Key,objects[pair.Key].transform.position,pair.Value.Rotation)))
                    return "다른 물체와 겹쳐요. 주변을 치우거나 다른 방향으로 놓아 주세요.";
            if(scenery.Any(bounds.Intersects))return "배경 구조물과 겹치는 위치입니다.";
            return null;
        }
        Vector3 PlacementPosition(NodeDefinition n,int level,float height)
        {
            float bottom=n.position.y;
            if(IsStorage(n))
            {
                for(int i=0;i<level;i++)
                {
                    var below=State.At(n.id,i);if(below==null)continue;
                    var go=objects[below.Id];var pose=PlacementRotation(below.Id,n,below.Rotation);
                    bottom=Mathf.Max(bottom,go.transform.position.y+RotatedSize(go.transform.localScale,pose).y/2+.025f);
                }
            }
            else bottom+=level*n.layerHeight;
            return new Vector3(n.position.x,bottom+height/2,n.position.z);
        }
        public RobotRequest NewRequest(string prompt)=>new RobotRequest {requestId=Guid.NewGuid().ToString("N"),stageRunId=RunId,prompt=prompt,snapshot=Snapshot()};
        public bool NodeVisible(string id)
        {
            if(!State.HasRepair)return true;
            var role=State.Nodes[id].role;
            return State.ArrangementComplete||role=="temporary"||(State.CleanupComplete&&role=="sorting");
        }
        public PublicSnapshot Snapshot()
        {
            var defs=catalog.Library().objects.ToDictionary(x=>x.id);
            return new PublicSnapshot {
                objects=Stage.objects.Where(x=>State.Items.ContainsKey(x.id)).Select(x=>{
                    var item=State.Items[x.id];return new VisibleObject {id=x.id,name=defs[x.definitionId].name,kind=item.Kind,nodeId=item.NodeId,
                        level=item.Level,rotation=item.Rotation,carried=item.Carried,pushOnly=item.PushOnly};}).ToArray(),
                nodes=Stage.nodes.Where(n=>NodeVisible(n.id)).Select(n=>new VisibleNode{id=n.id,name=n.name,capacity=n.capacity,count=State.Count(n.id)}).ToArray(),
                allowedActions=new[]{"move_to","pick_up","place","rotate","say","push","adjust"}};
        }
        public void Submit(string prompt)
        {
            if(Busy||string.IsNullOrWhiteSpace(prompt)||prompt.Length>1000)return;
            ChatOpen=false;Busy=true;Message="로봇이 생각 중이에요…";
            var request=NewRequest(prompt);pendingId=request.requestId;work=StartCoroutine(RequestAndExecute(request));
        }
        IEnumerator RequestAndExecute(RobotRequest request)
        {
            RobotResponse response=null;yield return provider.Interpret(request,r=>response=r);
            if(request.stageRunId!=RunId||pendingId!=request.requestId)yield break;
            if(response==null||response.requestId!=request.requestId||response.stageRunId!=RunId){Finish("이전 또는 잘못된 응답을 폐기했습니다.");yield break;}
            Provider=response.provider??"Codex CLI";
            if(response.status=="clarify"){Finish(response.message);yield break;}
            var error=ValidateResponse(response);
            if(error!=null){Finish(error);yield break;}
            Message=response.message;yield return Execute(response.actions);
            Busy=false;work=null;pendingId=null;
        }
        public string ValidateResponse(RobotResponse r)
        {
            if(r.status!="plan"||r.actions==null||r.actions.Length==0||r.actions.Length>12)return "행동 계획 형식 또는 개수가 잘못되었습니다.";
            foreach(var a in r.actions)
            {
                if(a==null)return "빈 행동입니다.";
                if(a.type=="say") {if(a.text==null||a.text.Length>500)return "대사가 너무 깁니다.";continue;}
                if(a.type=="move_to") {if(!State.Nodes.ContainsKey(a.targetId??"")&&!State.Items.ContainsKey(a.targetId??""))return "이동 대상 ID가 없습니다.";continue;}
                if(!new[]{"pick_up","place","rotate","push","adjust"}.Contains(a.type)||!State.Items.ContainsKey(a.objectId??""))return "허용하지 않은 행동 또는 물체 ID입니다.";
                if(new[]{"place","push","adjust"}.Contains(a.type)&&!State.Nodes.ContainsKey(a.targetId??""))return "배치 노드 ID가 없습니다.";
                if(a.type=="rotate"&&(a.rotation<0||a.rotation>=360||a.rotation%90!=0))return "잘못된 회전 각도입니다.";
            }
            return null;
        }
        void Finish(string message){Message=message;Busy=false;pendingId=null;work=null;}
        public void Cancel()
        {
            provider?.Cancel();if(work!=null)StopCoroutine(work);work=null;Busy=false;pendingId=null;
            Message="중단했어요. 이미 옮긴 물체와 들고 있는 물체는 그대로입니다.";
        }
        public IEnumerator Execute(RobotAction[] actions)
        {
            int previousPhase=State.RepairPhase;
            foreach(var action in actions)
            {
                string error=null;
                if(action.type=="say") {Message=action.text;continue;}
                if(action.type=="pick_up")error=State.CanPick(action.objectId);
                if(action.type=="place")error=State.CanPlace(action.objectId,action.targetId);
                if(action.type=="push")error=State.CanPush(action.objectId,action.targetId);
                if(error==null&&action.type=="place")
                {
                    var item=State.Items[action.objectId];var go=objects[item.Id];
                    var destinationNode=State.Nodes[action.targetId];var size=PlacementSize(item.Id,destinationNode);
                    var height=RotatedSize(size,PlacementRotation(item.Id,destinationNode,item.Rotation)).y;
                    error=SpatialError(item.Id,PlacementPosition(destinationNode,State.Count(action.targetId),height),item.Rotation,action.targetId,size);
                }
                if(error==null&&action.type=="rotate"&&State.HeldId!=action.objectId)
                    error=SpatialError(action.objectId,objects[action.objectId].transform.position,action.rotation,State.Items[action.objectId].NodeId);
                if(error!=null){Message=error;yield break;}
                var target=action.type=="move_to"?action.targetId:action.type=="place"||action.type=="adjust"?action.targetId:action.objectId;
                var position=State.Nodes.TryGetValue(target??"",out var node)?node.position.Vector:objects[target].transform.position;
                if(!(action.type=="rotate"&&State.HeldId==action.objectId))
                {
                    float reach=node==null?1.25f:Mathf.Max(1.25f,node.interactionRadius);
                    if(node==null&&objects.ContainsKey(target))
                    {
                        var footprint=ItemBounds(target,objects[target].transform.position,0);
                        reach=Mathf.Max(reach,Mathf.Max(footprint.extents.x,footprint.extents.z)+.45f);
                    }
                    if(node==null&&State.Items.TryGetValue(target,out var targetItem)&&targetItem.NodeId!=null)reach=Mathf.Max(reach,State.Nodes[targetItem.NodeId].interactionRadius);
                    var path=GridNavigation.Find(robot.position,position,Stage.width,Stage.depth,p=>Blocked(p,action.type=="pick_up"||action.type=="push"?action.objectId:null),reach);
                    if(path==null){Message="그곳까지 갈 수 없어요. 주변 물체를 먼저 치워 주세요.";yield break;}
                    foreach(var point in path)
                        while(Vector3.Distance(robot.position,point)>.01f) {robot.position=Vector3.MoveTowards(robot.position,point,Speed*Time.deltaTime);yield return null;}
                }
                if(action.type=="pick_up")error=State.Pick(action.objectId);
                else if(action.type=="place")
                {
                    error=State.Place(action.objectId,action.targetId);
                    if(error==null)
                    {
                        var item=State.Items[action.objectId];var go=objects[item.Id];
                        var destinationNode=State.Nodes[item.NodeId];
                        go.transform.localScale=PlacementSize(item.Id,destinationNode);
                        go.transform.rotation=PlacementRotation(item.Id,destinationNode,item.Rotation);
                        go.transform.position=PlacementPosition(destinationNode,item.Level,RotatedSize(go.transform.localScale,go.transform.rotation).y);
                        var definitionId=Stage.objects.First(x=>x.id==item.Id).definitionId;
                        var soundId=catalog.Library().objects.First(x=>x.id==definitionId).soundId;
                        if(catalog.Sound(soundId))AudioSource.PlayClipAtPoint(catalog.Sound(soundId),go.transform.position,.15f);
                    }
                }
                else if(action.type=="rotate")
                {
                    error=State.Rotate(action.objectId,action.rotation);
                    if(error==null)
                    {
                        var item=State.Items[action.objectId];var go=objects[action.objectId];
                        go.transform.rotation=item.NodeId==null?Quaternion.Euler(0,action.rotation,0):PlacementRotation(item.Id,State.Nodes[item.NodeId],action.rotation);
                        if(item.NodeId!=null)go.transform.position=PlacementPosition(State.Nodes[item.NodeId],item.Level,RotatedSize(go.transform.localScale,go.transform.rotation).y);
                    }
                }
                else if(action.type=="push")
                {
                    var go=objects[action.objectId];var destination=PlacementPosition(State.Nodes[action.targetId],0,go.transform.localScale.y);
                    error=SpatialError(action.objectId,destination,State.Items[action.objectId].Rotation,action.targetId);
                    if(error==null)
                    {
                        while(Vector3.Distance(go.transform.position,destination)>.01f){go.transform.position=Vector3.MoveTowards(go.transform.position,destination,Speed*.5f*Time.deltaTime);yield return null;}
                        error=State.Push(action.objectId,action.targetId);
                    }
                }
                else if(action.type=="adjust")error=State.Adjust(action.objectId,action.targetId);
                if(error!=null){Message=error;yield break;}
                yield return new WaitForSeconds(Testing?.01f:.15f);
            }
            Message=State.Solved?(IsHouse?"농장의 잔해 정리와 재료 배치, 곳간 재건을 모두 마쳤어요!":"우물을 복원했어요! 할아버지: …고맙다. 함께하니 해냈구나."):
                State.HasRepair&&State.RepairPhase!=previousPhase?(State.ArrangementComplete?"재료 배치를 확인했어요. 이제 정리한 부품으로 곳간을 재건해 주세요.":"건물 터가 비었어요. Q로 분류 노드의 종류·방향·쌓는 형태를 확인해 주세요."):
                "부탁을 마쳤어요. 회상과 비교하고, 필요하면 다시 옮겨 주세요.";
        }
        void OnDestroy(){provider?.Cancel();}
    }
}
