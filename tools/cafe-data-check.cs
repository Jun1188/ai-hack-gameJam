using System;
using System.IO;
using UnityEngine;
public static class CafeDataCheck
{
 static void Check(bool x,string reason){if(!x)throw new Exception(reason);}
 public static string Main()
 {
  var menu=JsonUtility.FromJson<CafeSim.MenuTable>(File.ReadAllText("Assets/CafeSim/Data/menu.json"));
  var people=JsonUtility.FromJson<CafeSim.PersonaTable>(File.ReadAllText("Assets/CafeSim/Data/personas.json"));
  var kb=new CafeSim.CafeKnowledge();Check(people.items.Length==18&&Array.FindAll(people.items,p=>string.IsNullOrEmpty(p.parentPersonaId)).Length==16&&menu.items.Length==11,"16 originals + 2 branches");
  Check(kb.Weights.Length==198&&kb.Situations.Length==37,"Original and branch relation counts");
  var settings=new CafeSim.CafeQueueSettings{arrivalSeconds=10000,orderSeconds=10000,activeTimeoutSeconds=10000,inactiveCapacity=3};
  int checkedSpeech=0;
  foreach(var p in people.items)
  {
   Check(p.hotPreference>=0&&p.hotPreference<=1&&!string.IsNullOrEmpty(p.communicationPoint),"Missing imported traits");
   var q=new CafeSim.CafeQueueSim(new[]{p},menu,kb,settings,48);
   for(int v=0;v<4;v++)
   {
    var c=q.Admit(false);q.AcceptOrder();
    Check(kb.Weight(c,c.Order.menuId)>0,"Zero-weight order selected");
    Check(!kb.ContainsForbidden(c.Order.speech),"Menu name leaked in order: "+c.Order.speech);
    Check(c.Situation!=null&&c.Situation.personaId==p.id,"Situation foreign key");
    Check(c.Order.speech.Length<400,"Oversized customer speech");
    string menuId=c.Order.menuId,temp=c.Order.temperature;
    Check(q.Serve(c.Id,menuId,temp),"Serve failed");
    q.PublishRating(c);
    foreach(string line in c.History)if(!line.StartsWith("SERVED")&&!line.StartsWith("ARRIVING"))Check(!kb.ContainsForbidden(line),"Forbidden surface form leaked");
    checkedSpeech++;
   }
  }
  foreach(var p in people.items)
  {
   var q=new CafeSim.CafeQueueSim(new[]{p},menu,kb,settings,8);var c=q.Admit(true);q.AcceptOrder();
   Check(c.Order.menuId.StartsWith("request_"),"Off-menu target missing");
   float low=2,high=-1;string best=null,bestTemp=null;
   foreach(var m in menu.items)foreach(var temp in new[]{"HOT","ICE"})if(m.Supports(temp)){
    float s=kb.Similarity(c.Order.menuId,c.Order.temperature,m.id,temp);low=Mathf.Min(low,s);if(s>high){high=s;best=m.id;bestTemp=temp;}}
   Check(high>low,"Similarity has no discrimination");q.Serve(c.Id,best,bestTemp);q.PublishRating(c);
   Check(c.Reaction.stars>=1&&c.Reaction.stars<=4,"Substitution is not exact or timeout");
   Check(!kb.ContainsForbidden(c.Reaction.speech)&&!kb.ContainsForbidden(c.Reaction.reason),"Forbidden customer response");
   Check(!c.Reaction.speech.Contains("그 음료"),"Feedback should describe the actual tastes, not erase both names");
  }
  string result="PASS 16 original personas + 2 branches, 11 menus, 198 weights, 37 situations; "+checkedSpeech+" normal orders + 18 off-menu substitutions, no menu-name leakage.";
  File.WriteAllText("../.utmp/cafe-data-check.txt",result);return result;
 }
}
