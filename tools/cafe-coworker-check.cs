using CafeCoworker;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

var results = new List<string>();
void Check(bool condition, string name) { if (!condition) throw new Exception(name); results.Add(name); }
var tables = JsonUtility.FromJson<BaristaTables>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/CafeCoworker/Data/BaristaTables.json").text);
Check(tables.profile.reminderDelay>=60&&tables.profile.reminderCooldown>=60,"coworker reminders wait at least one minute");
foreach (var recipe in tables.recipes)
{
    var scheduler = new WorkScheduler(); var order = scheduler.Add(recipe, 0);
    var prep = scheduler.Next(Worker.Player); var cups = scheduler.Next(Worker.Barista);
    Check(prep.Spec.id == "prep" && cups.Spec.id == "cup", recipe.id + ": divided initial work");
    Check(order.Tasks.First(t=>t.Spec.id=="brew").State == WorkState.Waiting, recipe.id + ": brewing requires preparation");
    Check(scheduler.Claim(cups, true) && !scheduler.Claim(cups,true), recipe.id + ": no double claim");
    scheduler.Arrive(cups); scheduler.Advance(cups,100,2);
    Check(scheduler.Next(Worker.Barista)==null, recipe.id + ": wait for player prerequisite");
    Check(scheduler.Reminder(29,25,28)==prep && scheduler.Reminder(30,25,28)==null, recipe.id + ": reminder and cooldown");
    scheduler.Block(prep,"occupied",31);
    Check(scheduler.Reminder(90,25,28)==null, recipe.id + ": no blame while blocked");
    scheduler.Block(prep,"",91); scheduler.Claim(prep,false); scheduler.Advance(prep,.5f,92);
    Check(scheduler.Reminder(100,25,28)==null && scheduler.Reminder(121,25,28)==prep, recipe.id + ": progress defers reminder, stalled partial work reminds");
    scheduler.Advance(prep,100,122);
    while (!order.Done)
    {
        var next = scheduler.Next(Worker.Barista) ?? scheduler.Next(Worker.Player);
        Check(next != null, recipe.id + ": remaining work executable");
        scheduler.Claim(next, next.Owner==Worker.Barista);
        scheduler.Arrive(next);
        scheduler.Advance(next,100,130);
    }
    Check(order.Tasks.All(t=>t.State==WorkState.Done), recipe.id + ": complete order");
    var cancelled = scheduler.Add(recipe,140); var task=scheduler.Next(Worker.Barista);
    scheduler.Claim(task,true); scheduler.Cancel(cancelled.Id);
    Check(!scheduler.Advance(task,100,200) && scheduler.Next(Worker.Barista)==null, recipe.id + ": cancelled work cannot finish late");
    scheduler.Reset();
    Check(scheduler.Orders.Count==0, recipe.id + ": reset");
}
var broken = new RecipeSpec { id="bad",tasks=new[]{new WorkSpec{id="a",label="a",station="x",owner="player",after=new[]{"b"},seconds=1},new WorkSpec{id="b",label="b",station="x",owner="barista",after=new[]{"a"},seconds=1}}};
bool rejected=false;try{WorkScheduler.Validate(broken);}catch(ArgumentException){rejected=true;}
Check(rejected,"cyclic dependency rejected");
var overlapRecipe=new RecipeSpec{id="shared",tasks=new[]{new WorkSpec{id="a",station="same",owner="player",seconds=1},new WorkSpec{id="b",station="same",owner="barista",seconds=1}}};
var overlap=new WorkScheduler();var shared=overlap.Add(overlapRecipe,0);
Check(overlap.Claim(shared.Tasks[0],false)&&!overlap.Claim(shared.Tasks[1],true),"station reservation excludes second worker");
overlap.Cancel(shared.Id);
Check(!overlap.IsAvailable(shared.Tasks[0]),"cancelled reservation not executable");

var path="Assets/CafeCoworker/Scenes/CafeBaristaLab.unity";
var scene=SceneManager.GetSceneByPath(path);
bool wasOpen=scene.IsValid()&&scene.isLoaded;
if(!wasOpen) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
try
{
    var objects=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
    var session=objects.Select(t=>t.GetComponent<CoopSession>()).First(x=>x!=null);
    Check(objects.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))==0,"scene has no missing scripts");
    Check(session.player!=null&&session.player.eyes!=null&&session.barista!=null&&session.dialogue!=null&&session.tablesAsset!=null,"scene composition references");
    Check(session.guests!=null&&session.guests.profilesAsset!=null,"independent guest profile snapshot bound");
    Check(session.stations.Length==4&&session.stations.All(s=>s!=null&&s.approach!=null&&s.workSurface!=null),"four persisted task locations");
    Check(!objects.SelectMany(t=>t.GetComponents<MonoBehaviour>()).Any(m=>m!=null&&(m.GetType().FullName=="CafeSim.CafeSession"||m.GetType().FullName=="CafeSim.CafeHud")),"no main cafe session or HUD in fork");
    Physics.SyncTransforms();
    foreach(var station in session.stations)
    {
        var blocked=Physics.OverlapCapsule(station.Position+Vector3.up*.4f,station.Position+Vector3.up*1.4f,.34f,~0,QueryTriggerInteraction.Ignore)
            .Where(c=>!c.transform.IsChildOf(session.player.transform)&&!c.transform.IsChildOf(session.barista.transform)).ToArray();
        Check(blocked.Length==0,"static clearance: "+station.stationId);
    }
    var staff=session.barista.transform.position;
    Check(!Physics.OverlapCapsule(staff+Vector3.up*.4f,staff+Vector3.up*1.4f,.34f,~0,QueryTriggerInteraction.Ignore).Any(c=>!c.transform.IsChildOf(session.barista.transform)),"staff spawn clear of scenery and player");
    var player=session.player.GetComponent<CharacterController>();
    bool wasEnabled=player.enabled;player.enabled=false;
    try {
        foreach(var target in session.stations.Where(s=>s.stationId!="prep"))
        {
            var workRoute=session.barista.Plan(staff,target.Position);
            Check(workRoute.Count>0,"route to "+target.stationId);
            Vector3 prior=staff;
            bool safe=true;
            foreach(var p in workRoute){safe &= session.barista.Clear(prior,p);prior=p;}
            Check(safe,"all curve segments clear: "+target.stationId);
            Check(Vector3.Distance(workRoute.Last(),target.Position)<.02f,"route ends at assigned work location "+target.stationId);
        }
        var route=session.barista.Plan(session.Station("cups").Position,session.Station("brew").Position);
        Check(route.Count>0,"cups to brewing route");
        var blocker=new GameObject("Temporary dynamic-player clearance check");
        SceneManager.MoveGameObjectToScene(blocker,scene);
        try
        {
            var capsule=blocker.AddComponent<CapsuleCollider>();capsule.height=1.75f;capsule.radius=.3f;capsule.center=Vector3.up*.875f;
            blocker.transform.position=Vector3.Lerp(session.Station("cups").Position,session.Station("brew").Position,.5f);
            Physics.SyncTransforms();
            Check(!session.barista.Clear(session.Station("cups").Position,session.Station("brew").Position),"player entering a path blocks its old direct segment");
            var detour=session.barista.Plan(session.Station("cups").Position,session.Station("brew").Position);
            Check(detour.Count>0,"replan finds a route around the player");
            bool safe=true;Vector3 prev=session.Station("cups").Position;
            foreach(var point in detour){safe &= session.barista.Clear(prev,point);prev=point;}
            Check(safe,"bezier detour preserves player clearance");
        }
        finally{UnityEngine.Object.DestroyImmediate(blocker);Physics.SyncTransforms();}
    } finally {player.enabled=wasEnabled;}
    var animator=session.barista.animator;
    Check(animator!=null&&animator.avatar!=null&&animator.avatar.isHuman&&animator.runtimeAnimatorController!=null,"humanoid and animation controller bound");
    Check(animator.runtimeAnimatorController.animationClips.All(c=>c!=null&&c.humanMotion&&c.length>.5f),"humanoid animation clips imported");
    Check(objects.SelectMany(t=>t.GetComponents<UnityEngine.UIElements.UIDocument>()).Single().visualTreeAsset!=null,"UI tree bound");
}
finally{if(!wasOpen)EditorSceneManager.CloseScene(scene,true);}
return new{scope="Edit-mode logic, persisted references and static physics only; no Play or build",checks=results.Distinct().ToArray()};
