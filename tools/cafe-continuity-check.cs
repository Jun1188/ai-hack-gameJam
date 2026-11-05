var menu=UnityEngine.JsonUtility.FromJson<CafeSim.MenuTable>(System.IO.File.ReadAllText("Assets/CafeSim/Data/menu.json"));
var people=UnityEngine.JsonUtility.FromJson<CafeSim.PersonaTable>(System.IO.File.ReadAllText("Assets/CafeSim/Data/personas.json"));
var kb=new CafeSim.CafeKnowledge();
var q=new CafeSim.CafeQueueSim(people.items,menu,kb,new CafeSim.CafeQueueSettings{arrivalSeconds=10000,orderSeconds=60,activeTimeoutSeconds=10000},17);
q.Tick(0);var first=q.Active[0];
var root=new UnityEngine.GameObject("Isolated cafe continuity check");root.SetActive(false);
try
{
 var s=root.AddComponent<CafeSim.CafeSession>();
 typeof(CafeSim.CafeSession).GetProperty("Queue").SetValue(s,q);
 typeof(CafeSim.CafeSession).GetProperty("Menu").SetValue(s,menu);
 var refresh=typeof(CafeSim.CafeSession).GetMethod("RefreshSelection",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
 s.SelectCustomer(first.Id);s.Serve("menu_latte","HOT");q.PublishRating(first);
 if(q.Active.Count!=0||q.Inactive.Count!=0)throw new System.Exception("Expected an empty cafe after first service");
 q.Tick(s.reactionReadSeconds+s.ratingReadSeconds-.25f);refresh.Invoke(s,null);
 if(s.Selected!=first||q.Active.Count!=0)throw new System.Exception("Review reading period was skipped");
 q.Tick(.25f);refresh.Invoke(s,null);
 if(s.Selected==first||s.Selected?.Id!=2||!s.CanChat||q.Active.Count!=1||q.Now>=60)
  throw new System.Exception("Next customer must be selected and able to chat before periodic arrival timer");
 if(q.FillVacancy()!=null||q.Active.Count!=1)throw new System.Exception("Vacancy refill duplicated an active customer");
 q.Tick(1);if(q.Active.Count!=1||q.Inactive.Count!=0)throw new System.Exception("Periodic arrival was not rescheduled after refill");
}
finally{UnityEngine.Object.DestroyImmediate(root);}
var expired=new CafeSim.CafeQueueSim(people.items,menu,kb,new CafeSim.CafeQueueSettings{arrivalSeconds=10000,activeTimeoutSeconds=1},4);
expired.Tick(0);expired.Active[0].Situation=null;expired.Tick(1);
if(expired.FillVacancy()?.State!=CafeSim.VisitState.Active)throw new System.Exception("Empty timeout queue did not recover");
return "PASS session-level first service -> review hold -> second arrival/selection/chat; no duplicate refill; periodic timer reset; timeout recovery. No Play/model calls used.";
