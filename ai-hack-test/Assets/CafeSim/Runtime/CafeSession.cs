using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CafeSim
{
 public enum CafePhase { Loading, Arriving, Ordering, Choosing, Reacting, Reaction, Rating, Leaving, Error }
 public sealed class CafeSession : MonoBehaviour
 {
  public TextAsset menuData,personaData; public LocalCafeModel model;
  public Transform customer,entrance,servicePoint; public Renderer customerBody; public GameObject servedCup;
  public float reactionReadSeconds=3.5f,ratingReadSeconds=5f;
  public CafeQueueSettings queueSettings=new CafeQueueSettings();
  public bool mergedCafe;
  // Inside of the cafe door. Merged customers queue toward it and enter/leave through it; entrance is the street outside.
  public Transform doorway;
  public event Action<CafeVisit,string,string> DialogueOutput;
  public readonly List<string> DialogueLog=new List<string>();
  public MenuTable Menu {get;private set;}
  public Persona Person=>Selected?.Person; public CustomerOrder Order=>Selected?.Order;
  public CustomerReaction Reaction=>Selected?.Reaction; public CafePhase Phase {get;private set;}
  public string Error {get;private set;} public string ServedLabel {get;private set;}
  public int CustomerNumber=>Selected?.Id??0;
  public readonly List<string> Chat=new List<string>(); public int ChatRevision {get;private set;}
  public CafeQueueSim Queue {get;private set;} public CafeVisit Selected {get;private set;}
  public bool ChatBusy {get;private set;}
  public string ChatStatus {get;private set;}="";
  public string ChatError {get;private set;}
  public string LastReplySource {get;private set;}
  public float LastReplySeconds {get;private set;}
  public bool CanChat=>Selected!=null&&((!mergedCafe&&Selected.State==VisitState.Inactive)||Selected.State==VisitState.Active);
  readonly HashSet<int> closedChats=new HashSet<int>();
  public bool ChatClosed=>Selected!=null&&closedChats.Contains(Selected.Id);
  public CafeConversation Conversation {get;private set;}
  Coroutine chatRoutine,bootRoutine; CafeVisit chattingWith;
  readonly Dictionary<int,Transform> bodies=new Dictionary<int,Transform>();
  readonly Dictionary<int,float> ratingDue=new Dictionary<int,float>();
  readonly List<CafeVisit> awaitingRating=new List<CafeVisit>();
  readonly HashSet<int> departed=new HashSet<int>();
  float lastTime,selectedUntil;
  void Start(){RestartModel();}
  public void RestartModel()
  {
   try {
    CancelChat();if(bootRoutine!=null)StopCoroutine(bootRoutine);bootRoutine=null;
    model?.Shutdown();foreach(var t in bodies.Values)if(t!=null)Destroy(t.gameObject);bodies.Clear();
    ratingDue.Clear();awaitingRating.Clear();departed.Clear();Chat.Clear();DialogueLog.Clear();Selected=null;Error=null;ServedLabel=null;
    if(menuData==null||personaData==null||customer==null||servicePoint==null||entrance==null)throw new InvalidOperationException("씬 참조 누락");
    Menu=JsonUtility.FromJson<MenuTable>(menuData.text);
    var people=JsonUtility.FromJson<PersonaTable>(personaData.text);
    Queue=new CafeQueueSim(people.items,Menu,new CafeKnowledge(),queueSettings,Environment.TickCount);
    Conversation=new CafeConversation();closedChats.Clear();ChatError=null;ChatStatus="";
    if(model==null)model=GetComponent<LocalCafeModel>()??gameObject.AddComponent<LocalCafeModel>();
    bootRoutine=StartCoroutine(model.Boot());
    Queue.Output+=OnOutput;customer.gameObject.SetActive(false);if(servedCup!=null)servedCup.SetActive(false);
    Phase=CafePhase.Arriving;lastTime=Time.realtimeSinceStartup;Queue.Tick(0);RefreshSelection();
    Debug.Log("[Cafe] READY | inactive-capacity="+queueSettings.inactiveCapacity+" off-menu="+queueSettings.offMenuChance+" | local retrieval + prepared sentence patterns");
   } catch(Exception e){Error=e.Message;Phase=CafePhase.Error;Debug.LogError("[Cafe] DATA_ERROR | "+e.Message);}
  }
  void Update()
  {
   if(Queue==null||Error!=null)return;
   float now=Time.realtimeSinceStartup,dt=Mathf.Max(0,now-lastTime);lastTime=now;Queue.Tick(dt);
   for(int i=awaitingRating.Count-1;i>=0;i--){var c=awaitingRating[i];if(Queue.Now<ratingDue[c.Id])continue;awaitingRating.RemoveAt(i);ratingDue.Remove(c.Id);Queue.PublishRating(c);}
   RefreshSelection();UpdateBodies();
   if(ChatBusy&&(!CanChat||Selected!=chattingWith))CancelChat();
  }
  void OnOutput(CafeVisit c,string kind,string line)
  {
   if(kind=="ORDER"){if(chattingWith==c)CancelChat();closedChats.Remove(c.Id);}
   RecordDialogue(c,kind,line);
   var flags=new List<string>(c.Parameters());flags.Sort();
   Debug.Log("[Cafe] "+kind+" | t="+Queue.Now.ToString("F2")+" customer="+c.Id+" persona="+c.Person.id+" "+c.Person.name+" inactive="+c.InactiveWait.ToString("F1")+" active="+c.ActiveWait.ToString("F1")+" flags="+string.Join(",",flags)+" | "+line);
   if(Selected==c)SyncChat();if(kind=="RATING"&&Selected==c){Phase=CafePhase.Rating;selectedUntil=Queue.Now+ratingReadSeconds;}
  }
  void RefreshSelection()
  {
   if(Selected!=null&&(Selected.State==VisitState.Active||Selected.State==VisitState.Inactive)){Phase=Selected.State==VisitState.Active?CafePhase.Choosing:CafePhase.Ordering;return;}
   if(Selected!=null&&Queue.Now<selectedUntil)return;
   Queue.FillVacancy();
   if(Queue.Active.Count>0)SelectCustomer(Queue.Active[0].Id);else if(Queue.Inactive.Count>0)SelectCustomer(Queue.Inactive[0].Id);else {Selected=null;Phase=CafePhase.Arriving;}
  }
  public void SelectCustomer(int id)
  {
   var c=Queue?.Active.Find(v=>v.Id==id)??Queue?.Inactive.Find(v=>v.Id==id)??Queue?.Completed.Find(v=>v.Id==id&&v.RatingPublished);
   if(c==null||(mergedCafe&&c.State==VisitState.Inactive))return;if(c!=Selected){CancelChat();ChatError=null;}Selected=c;
   if((c.State==VisitState.Inactive||c.State==VisitState.Active)&&string.IsNullOrEmpty(c.LastThought))
   {
    float wait=c.State==VisitState.Inactive?c.InactiveWait:c.ActiveWait;
    c.LastThought=Queue.Knowledge.Retrieve(c,"chatter",wait,true);
    if(!string.IsNullOrEmpty(c.LastThought)){AddChat(c,"CHATTER",c.LastThought);c.LastChatterAt=Queue.Now;}
   }
   SyncChat();Phase=c.State==VisitState.Active?CafePhase.Choosing:c.State==VisitState.Inactive?CafePhase.Ordering:CafePhase.Rating;
   selectedUntil=Queue.Now+ratingReadSeconds;if(servedCup!=null)servedCup.SetActive(c.State==VisitState.Received);
  }
  void SyncChat()
  {
   Chat.Clear();
   if(Selected!=null)foreach(var entry in Selected.History){int split=entry.IndexOf('\n');if(split<0)continue;
    string kind=entry.Substring(0,split),line=entry.Substring(split+1);
    if(kind=="ARRIVING")continue;
    if(kind=="SERVED"){int detail=line.IndexOf(" | similarity=");if(detail>=0)line=line.Substring(0,detail);}
    string label=kind=="USER"?"나":kind=="REPLY"?"손님 답변":kind=="NOTICE"?"안내":kind=="ORDER"?"주문":kind=="CHATTER"?"잡담":kind=="SERVED"?"제공":kind=="REACTION"?"반응":kind=="RATING"?"후기":kind;
    Chat.Add(label+"\n"+line);
   }ChatRevision++;
  }
  public bool Serve(string menuId,string temperature)
  {
   if(Selected==null||Selected.State!=VisitState.Active)return false;CancelChat();var c=Selected;if(!Queue.Serve(c.Id,menuId,temperature))return false;
   ServedLabel=c.ServedName+" · "+temperature;Phase=CafePhase.Reaction;if(servedCup!=null)servedCup.SetActive(true);
   awaitingRating.Add(c);ratingDue[c.Id]=Queue.Now+reactionReadSeconds;selectedUntil=Queue.Now+reactionReadSeconds+ratingReadSeconds;
   return true;
  }
  public Transform BodyOf(CafeVisit c)=>c!=null&&bodies.TryGetValue(c.Id,out var body)?body:null;
  void RecordDialogue(CafeVisit c,string kind,string line)
  {
   if(kind=="ARRIVING")return;
   DialogueLog.Add("#"+c.Id+" "+c.Person.name+" · "+kind+"\n"+line);DialogueOutput?.Invoke(c,kind,line);
  }
  void AddChat(CafeVisit c,string kind,string text)
  {
   // Player text and generated replies stay in memory, never in the shared queue event log.
   c.History.Add(kind+"\n"+text);RecordDialogue(c,kind,text);
   if(Selected==c)SyncChat();
  }
  public bool SendChat(string input)
  {
   input=input?.Trim();if(ChatBusy||!CanChat||string.IsNullOrWhiteSpace(input)||input.Length>300)return false;
   ChatError=null;LastReplySource=null;
   var c=Selected;
   if(Conversation.TryNewMenuReply(c,input,Menu,out var menuReply))
   {
    closedChats.Remove(c.Id);AddChat(c,"USER",input);AddChat(c,"REPLY",menuReply);
    LastReplySource="new-menu-table";LastReplySeconds=0;ChatStatus="답변 완료";return true;
   }
   if(ChatClosed)return false;
   if(Conversation.IsOffTopic(c,input))
   {
    AddChat(c,"USER",input);AddChat(c,"REPLY",Conversation.Closing(c));closedChats.Add(c.Id);
    LastReplySource="closing";LastReplySeconds=0;ChatStatus="대화를 마쳤어요";return true;
   }
   string prompt=Conversation.Prompt(c,input,Queue.Knowledge);
   bool direct=Conversation.TryDirect(c,input,out var reply);
   bool cached=!direct&&Conversation.TryCached(prompt,out reply);
   if(!direct&&!cached&&(model==null||!model.Ready))
   {ChatError=model?.Error??"로컬 모델을 준비하는 중입니다. 잠시 후 전송하거나 모델 재시작을 눌러 주세요.";return false;}
   AddChat(c,"USER",input);
   if(direct||cached){AddChat(c,"REPLY",reply);LastReplySource=direct?"database":"cache";LastReplySeconds=0;ChatStatus="답변 완료";return true;}
   ChatBusy=true;chattingWith=c;ChatStatus="손님이 답변을 생각하고 있어요…";
   chatRoutine=StartCoroutine(GenerateChat(c,prompt));return true;
  }
  public void ReopenChat(){if(Selected!=null)closedChats.Remove(Selected.Id);ChatStatus="";ChatError=null;}
  IEnumerator GenerateChat(CafeVisit c,string prompt)
  {
   float start=Time.realtimeSinceStartup;string json=null,error=null;
   yield return model.Generate(prompt,x=>json=x,x=>error=x,CafeConversation.SystemPrompt,192);
   if(chattingWith!=c)yield break;
   LastReplySeconds=Time.realtimeSinceStartup-start;
   if(Selected==c&&CanChat)
   {
    if(error==null&&CafeConversation.Parse(json,out var speech,out bool endConversation))
    {
     if(endConversation){speech=Conversation.Closing(c);closedChats.Add(c.Id);LastReplySource="closing";ChatStatus="대화를 마쳤어요";}
     else {speech=Queue.Knowledge.MaskMenuNames(speech);Conversation.Remember(prompt,speech);LastReplySource=model.UseApi?"api":"model";ChatStatus="답변 완료";}
     AddChat(c,"REPLY",speech);
    }
    else {ChatError=error??"답변 형식이 올바르지 않습니다. 문장을 조금 바꾸어 다시 전송해 주세요.";ChatStatus="답변을 다시 요청할 수 있어요";AddChat(c,"NOTICE",ChatError);}
   }
   ChatBusy=false;chattingWith=null;chatRoutine=null;
  }
  public void CancelChat()
  {
   var c=chattingWith;chattingWith=null;
   if(chatRoutine!=null)StopCoroutine(chatRoutine);chatRoutine=null;model?.CancelRequest();
   if(ChatBusy&&c!=null)AddChat(c,"NOTICE","답변 요청을 중단했습니다.");
   ChatBusy=false;ChatStatus="";
  }
  public void RebootChatModel()
  {CancelChat();if(bootRoutine!=null)StopCoroutine(bootRoutine);ChatError=null;bootRoutine=StartCoroutine(model.Boot());}
  void UpdateBodies()
  {
   var visible=new List<CafeVisit>();visible.AddRange(Queue.Active);visible.AddRange(Queue.Inactive);
   // Finished customers keep their body until they have walked out of the door.
   if(mergedCafe)foreach(var c in Queue.Completed)if(!departed.Contains(c.Id)&&bodies.ContainsKey(c.Id))visible.Add(c);
   if(!mergedCafe&&Selected!=null&&!visible.Contains(Selected))visible.Insert(0,Selected);var keep=new HashSet<int>();
   for(int i=0;i<visible.Count&&i<12;i++){
    var c=visible[i];keep.Add(c.Id);
    if(!bodies.TryGetValue(c.Id,out var body)||body==null){body=Instantiate(customer,entrance.position,entrance.rotation);body.name="Customer "+c.Id+" "+c.Person.name;body.gameObject.SetActive(true);bodies[c.Id]=body;
     foreach(var r in body.GetComponentsInChildren<Renderer>())if(r.name=="Body")r.material.color=Color.HSVToRGB((c.Id*.137f)%1,.35f,.78f);}
    if(!mergedCafe)
    {
     Vector3 spot=c==Selected?servicePoint.position:new Vector3(c.State==VisitState.Inactive?-3.7f:3.7f,servicePoint.position.y,2.3f+(i%5)*.8f);
     body.position=Vector3.MoveTowards(body.position,spot,1.7f*Time.unscaledDeltaTime);body.rotation=servicePoint.rotation;continue;
    }
    bool leaving=c.State!=VisitState.Active&&c.State!=VisitState.Inactive&&(c.RatingPublished||c.State==VisitState.TimedOut);
    Vector3 target=c.State==VisitState.Active?servicePoint.position:c.State==VisitState.Inactive?QueueSpot(Queue.Inactive.IndexOf(c)):
     leaving?entrance.position:servicePoint.position+servicePoint.right*.8f;
    Vector3 from=body.position;
    body.position=Vector3.MoveTowards(from,ThroughDoor(from,target),1.7f*Time.unscaledDeltaTime);
    Vector3 walk=body.position-from;walk.y=0;
    Quaternion facing=walk.sqrMagnitude>1e-8f?Quaternion.LookRotation(walk):servicePoint.rotation;
    body.rotation=Quaternion.RotateTowards(body.rotation,facing,360*Time.unscaledDeltaTime);
    if(leaving&&(body.position-entrance.position).sqrMagnitude<.0025f)departed.Add(c.Id);
   }
   foreach(var id in new List<int>(bodies.Keys))if(!keep.Contains(id)){Destroy(bodies[id].gameObject);bodies.Remove(id);}
  }
  Vector3 QueueSpot(int index)
  {
   Vector3 line=doorway!=null?doorway.position-servicePoint.position:-servicePoint.forward*5;line.y=0;
   return servicePoint.position+line.normalized*Mathf.Min(1.05f*(index+1),line.magnitude);
  }
  // Walls have no openings for the placeholder bodies to path around, so any trip between
  // the street and the room is routed through the inside of the door first.
  Vector3 ThroughDoor(Vector3 from,Vector3 target)
  {
   if(doorway==null)return target;
   Vector3 outward=entrance.position-doorway.position;outward.y=0;outward.Normalize();
   bool fromOutside=Vector3.Dot(from-doorway.position,outward)>.02f,targetOutside=Vector3.Dot(target-doorway.position,outward)>.02f;
   Vector3 gap=from-doorway.position;gap.y=0;
   return fromOutside!=targetOutside&&gap.sqrMagnitude>.0025f?doorway.position:target;
  }
  public void Retry(){RestartModel();}
  public void SkipCustomer(){if(Queue?.Active.Count>0)SelectCustomer(Queue.Active[0].Id);}
  void OnDisable(){CancelChat();if(bootRoutine!=null)StopCoroutine(bootRoutine);bootRoutine=null;model?.Shutdown();}
  void OnDestroy(){foreach(var t in bodies.Values)if(t!=null)Destroy(t.gameObject);}
 }
}
