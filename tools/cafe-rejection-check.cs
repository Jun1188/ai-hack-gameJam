using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
public static class CafeRejectionCheck
{
 public static string Main()
 {
  var menu=JsonUtility.FromJson<CafeSim.MenuTable>(File.ReadAllText("Assets/CafeSim/Data/menu.json"));
  var people=JsonUtility.FromJson<CafeSim.PersonaTable>(File.ReadAllText("Assets/CafeSim/Data/personas.json"));
  var kb=new CafeSim.CafeKnowledge();var contrasts=new HashSet<string>();var exits=new HashSet<string>();var report=new System.Text.StringBuilder();
  foreach(var p in people.items)
  {
   var config=new CafeSim.CafeQueueSettings{arrivalSeconds=10000,orderSeconds=10000,activeTimeoutSeconds=200};
   var q=new CafeSim.CafeQueueSim(new[]{p},menu,kb,config,4);q.Tick(0);var c=q.Active[0];
   // Hold facts constant to verify differences come from this persona's lexical relation.
   c.Order.menuId="menu_choco";c.Order.temperature="ICE";c.RequestedName="초코라떼";c.OffMenu=false;
   q.Serve(c.Id,"menu_americano","HOT");
   if(!contrasts.Add(c.Reaction.speech))throw new Exception("Duplicate refusal voice: "+p.id);
   if(kb.ContainsForbidden(c.Reaction.speech)||c.Reaction.speech.Contains("similarity="))throw new Exception("Technical/name leakage");
   report.AppendLine(p.id+": "+c.Reaction.speech);
   var t=new CafeSim.CafeQueueSim(new[]{p},menu,kb,new CafeSim.CafeQueueSettings{arrivalSeconds=10000,orderSeconds=10000,activeTimeoutSeconds=1},4);
   t.Tick(0);var timed=t.Active[0];t.Tick(2);
   if(timed.Reaction?.stars!=0||!exits.Add(timed.Reaction.reason))throw new Exception("Duplicate/invalid timeout voice: "+p.id);
  }
  string result="PASS 16 distinct refusals under identical facts and 16 distinct timeout reviews.\n"+report;
  File.WriteAllText("../.utmp/cafe-rejection-check.txt",result);return result;
 }
}
