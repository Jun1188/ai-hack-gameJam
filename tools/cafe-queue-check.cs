using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
public static class CafeQueueCheck
{
 static void Check(bool test,string message){if(!test)throw new Exception(message);}
 public static string Main()
 {
  var menu=JsonUtility.FromJson<CafeSim.MenuTable>(File.ReadAllText("Assets/CafeSim/Data/menu.json"));
  var people=JsonUtility.FromJson<CafeSim.PersonaTable>(File.ReadAllText("Assets/CafeSim/Data/personas.json"));
  var kb=new CafeSim.CafeKnowledge();
  var settings=new CafeSim.CafeQueueSettings{inactiveCapacity=2,arrivalSeconds=10000,orderSeconds=10000,activeTimeoutSeconds=60,chatterSeconds=5,inactivePenaltyPerSecond=.025f,activePenaltyPerSecond=.035f};
  var q=new CafeSim.CafeQueueSim(people.items,menu,kb,settings,17);
  var events=new List<string>();q.Output+=(c,k,t)=>events.Add(k+" #"+c.Id+" "+t);
  q.Tick(0);var second=q.Admit(false);Check(q.Inactive.Count==2,"Admission setup");
  Check(q.Admit()==null&&q.Inactive.Count==2,"Inactive capacity must not grow");
  float before=q.WaitingSatisfaction(second);q.Tick(10);Check(q.WaitingSatisfaction(second)<before,"Inactive satisfaction must decline");
  Check(events.Exists(e=>e.StartsWith("CHATTER")),"Waiting chatter without input");
  var a=q.AcceptOrder();var b=q.AcceptOrder();a.Situation=null;b.Situation=null;Check(q.Inactive.Count==0&&q.Active.Count==2,"FIFO transfer to active");
  Check(a.InactiveEntries==1&&a.ActiveEntries==1&&a.Parameters().Contains("visited_active"),"Queue feature activation");
  for(int i=0;i<6;i++){q.Admit(false);q.AcceptOrder().Situation=null;}Check(q.Active.Count>settings.inactiveCapacity,"Active has no inactive capacity limit");
  q.Tick(59.75f);Check(q.Active.Contains(a),"Must not leave before deadline");q.Tick(.25f);
  Check(a.Reaction.stars==0&&a.State==CafeSim.VisitState.TimedOut&&!q.Active.Contains(a),"Deadline timeout zero review and removal");
  Check(!q.Serve(a.Id,"menu_latte","HOT"),"Service at/after deadline must fail");
  Check(!string.IsNullOrEmpty(a.Reaction.reason)&&!a.Parameters().Contains("received"),"Timeout review must not invent receipt");
  Check(events.Exists(e=>e.StartsWith("RATING")),"Timeout logged");
  // Accuracy + both independently recorded waits influence served reviews.
  var servedSim=new CafeSim.CafeQueueSim(people.items,menu,kb,settings,99);servedSim.Tick(0);var c=servedSim.AcceptOrder();
  var exact=Array.Find(menu.items,m=>m.id==c.Order.menuId);
  // Seed may create an off-menu customer: make request deterministic for this exact-service boundary test.
  c.OffMenu=false;c.Order.menuId="menu_latte";c.Order.temperature="HOT";c.RequestedName="카페라떼";
  Check(servedSim.Serve(c.Id,"menu_latte","HOT")&&c.Reaction.stars==5,"Immediate exact receipt is five stars");
  Check(!servedSim.Serve(c.Id,"menu_latte","HOT"),"Double serving blocked");
  servedSim.PublishRating(c);int eventCount=c.History.Count;servedSim.PublishRating(c);Check(c.History.Count==eventCount,"Publish rating exactly once");
  var delayed=new CafeSim.CafeQueueSim(people.items,menu,kb,settings,9);delayed.Tick(0);delayed.Tick(30);var d=delayed.AcceptOrder();delayed.Tick(20);
  d.Order.menuId="menu_latte";d.Order.temperature="HOT";d.RequestedName="카페라떼";d.OffMenu=false;
  delayed.Serve(d.Id,"menu_latte","HOT");Check(d.Reaction.stars<c.Reaction.stars&&d.Reaction.stars>=1,"Both waits lower served rating, never zero");
  Check(d.Reaction.reason.Contains("30초")&&d.Reaction.reason.Contains("20초"),"Review uses true wait durations");
  float mochaLatte=kb.Similarity("request_mocha","HOT","menu_latte","HOT"),mochaTea=kb.Similarity("request_mocha","HOT","menu_ssanghwa","HOT");
  Check(mochaLatte>mochaTea,"Off-menu similarity must distinguish ingredients");
  int cases=0;
  foreach(var r in kb.Requests)foreach(var m in menu.items)foreach(var t in new[]{"HOT","ICE"})if(m.Supports(t)){
   float sim=kb.Similarity(r.id,r.temperature,m.id,t);Check(sim>=0&&sim<1,"Similarity bounds");cases++;
  }
  Check(new CafeSim.CafeQueueSettings().offMenuChance==.05f,"Off-menu probability is five percent");
  Check(new CafeSim.CafeQueueSettings().arrivalSeconds>=60&&new CafeSim.CafeQueueSettings().orderSeconds>=60,"Default queue cadence must be at least a minute");
  // Deterministic empirical coverage of both randomized request paths, without asserting exact frequency.
  var randomSim=new CafeSim.CafeQueueSim(people.items,menu,kb,new CafeSim.CafeQueueSettings{inactiveCapacity=1000},82);int off=0;
  for(int i=0;i<1000;i++){var v=randomSim.Admit();if(v.OffMenu)off++;}
  Check(off>20&&off<85,"Five percent sampling outside broad expected range");
  string result="PASS queue cap, FIFO, independent waits, autonomous chatter, deadline zero/removal, duplicate service, rating idempotence, similarity "+cases+" cases; off-menu sample="+off+"/1000\n"+string.Join("\n",events);
  File.WriteAllText("../.utmp/cafe-queue-check.txt",result);return result;
 }
}
