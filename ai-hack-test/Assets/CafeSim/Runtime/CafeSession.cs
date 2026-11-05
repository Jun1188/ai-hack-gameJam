using System;
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
  public MenuTable Menu {get;private set;}
  public Persona Person=>Selected?.Person; public CustomerOrder Order=>Selected?.Order;
  public CustomerReaction Reaction=>Selected?.Reaction; public CafePhase Phase {get;private set;}
  public string Error {get;private set;} public string ServedLabel {get;private set;}
  public int CustomerNumber=>Selected?.Id??0;
  public readonly List<string> Chat=new List<string>(); public int ChatRevision {get;private set;}
  public CafeQueueSim Queue {get;private set;} public CafeVisit Selected {get;private set;}
  readonly Dictionary<int,Transform> bodies=new Dictionary<int,Transform>();
  readonly Dictionary<int,float> ratingDue=new Dictionary<int,float>();
  readonly List<CafeVisit> awaitingRating=new List<CafeVisit>();
  float lastTime,selectedUntil;
  void Start(){RestartModel();}
  public void RestartModel()
  {
   try {
    model?.Shutdown();foreach(var t in bodies.Values)if(t!=null)Destroy(t.gameObject);bodies.Clear();
    ratingDue.Clear();awaitingRating.Clear();Chat.Clear();Selected=null;Error=null;ServedLabel=null;
    if(menuData==null||personaData==null||customer==null||servicePoint==null||entrance==null)throw new InvalidOperationException("씬 참조 누락");
    Menu=JsonUtility.FromJson<MenuTable>(menuData.text);
    var people=JsonUtility.FromJson<PersonaTable>(personaData.text);
    Queue=new CafeQueueSim(people.items,Menu,new CafeKnowledge(),queueSettings,Environment.TickCount);
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
  }
  void OnOutput(CafeVisit c,string kind,string line)
  {
   var flags=new List<string>(c.Parameters());flags.Sort();
   Debug.Log("[Cafe] "+kind+" | t="+Queue.Now.ToString("F2")+" customer="+c.Id+" persona="+c.Person.id+" "+c.Person.name+" inactive="+c.InactiveWait.ToString("F1")+" active="+c.ActiveWait.ToString("F1")+" flags="+string.Join(",",flags)+" | "+line);
   if(Selected==c)SyncChat();if(kind=="RATING"&&Selected==c){Phase=CafePhase.Rating;selectedUntil=Queue.Now+ratingReadSeconds;}
  }
  void RefreshSelection()
  {
   if(Selected!=null&&(Selected.State==VisitState.Active||Selected.State==VisitState.Inactive)){Phase=Selected.State==VisitState.Active?CafePhase.Choosing:CafePhase.Ordering;return;}
   if(Selected!=null&&Queue.Now<selectedUntil)return;
   if(Queue.Active.Count>0)SelectCustomer(Queue.Active[0].Id);else if(Queue.Inactive.Count>0)SelectCustomer(Queue.Inactive[0].Id);else {Selected=null;Phase=CafePhase.Arriving;}
  }
  public void SelectCustomer(int id)
  {
   var c=Queue?.Active.Find(v=>v.Id==id)??Queue?.Inactive.Find(v=>v.Id==id)??Queue?.Completed.Find(v=>v.Id==id&&v.RatingPublished);
   if(c==null)return;Selected=c;SyncChat();Phase=c.State==VisitState.Active?CafePhase.Choosing:c.State==VisitState.Inactive?CafePhase.Ordering:CafePhase.Rating;
   selectedUntil=Queue.Now+ratingReadSeconds;if(servedCup!=null)servedCup.SetActive(c.State==VisitState.Received);
  }
  void SyncChat()
  {
   Chat.Clear();
   if(Selected!=null)foreach(var entry in Selected.History){int split=entry.IndexOf('\n');if(split<0)continue;
    string kind=entry.Substring(0,split),line=entry.Substring(split+1);
    if(kind=="ARRIVING")continue;
    if(kind=="SERVED"){int detail=line.IndexOf(" | similarity=");if(detail>=0)line=line.Substring(0,detail);}
    string label=kind=="ORDER"?"주문":kind=="CHATTER"?"잡담":kind=="SERVED"?"제공":kind=="REACTION"?"반응":kind=="RATING"?"후기":kind;
    Chat.Add(label+"\n"+line);
   }ChatRevision++;
  }
  public void Serve(string menuId,string temperature)
  {
   if(Selected==null||Selected.State!=VisitState.Active)return;var c=Selected;if(!Queue.Serve(c.Id,menuId,temperature))return;
   ServedLabel=c.ServedName+" · "+temperature;Phase=CafePhase.Reaction;if(servedCup!=null)servedCup.SetActive(true);
   awaitingRating.Add(c);ratingDue[c.Id]=Queue.Now+reactionReadSeconds;selectedUntil=Queue.Now+reactionReadSeconds+ratingReadSeconds;
  }
  void UpdateBodies()
  {
   var visible=new List<CafeVisit>();visible.AddRange(Queue.Active);visible.AddRange(Queue.Inactive);
   if(Selected!=null&&!visible.Contains(Selected))visible.Insert(0,Selected);var keep=new HashSet<int>();
   for(int i=0;i<visible.Count&&i<12;i++){
    var c=visible[i];keep.Add(c.Id);
    if(!bodies.TryGetValue(c.Id,out var body)||body==null){body=Instantiate(customer,entrance.position,entrance.rotation);body.name="Customer "+c.Id+" "+c.Person.name;body.gameObject.SetActive(true);bodies[c.Id]=body;
     foreach(var r in body.GetComponentsInChildren<Renderer>())if(r.name=="Body")r.material.color=Color.HSVToRGB((c.Id*.137f)%1,.35f,.78f);}
    Vector3 target=c==Selected?servicePoint.position:new Vector3(c.State==VisitState.Inactive?-3.7f:3.7f,servicePoint.position.y,2.3f+(i%5)*.8f);
    body.position=Vector3.MoveTowards(body.position,target,1.7f*Time.unscaledDeltaTime);body.rotation=servicePoint.rotation;
   }
   foreach(var id in new List<int>(bodies.Keys))if(!keep.Contains(id)){Destroy(bodies[id].gameObject);bodies.Remove(id);}
  }
  public void Retry(){RestartModel();}
  public void SkipCustomer(){if(Queue?.Active.Count>0)SelectCustomer(Queue.Active[0].Id);}
  void OnDisable(){model?.Shutdown();}
  void OnDestroy(){foreach(var t in bodies.Values)if(t!=null)Destroy(t.gameObject);}
 }
}
