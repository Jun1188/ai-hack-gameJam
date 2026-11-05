using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
public static class CafeDialogueCheck
{
    public static string Main()
    {
        var people=JsonUtility.FromJson<CafeSim.PersonaTable>(File.ReadAllText("Assets/CafeSim/Data/personas.json"));
        var menu=JsonUtility.FromJson<CafeSim.MenuTable>(File.ReadAllText("Assets/CafeSim/Data/menu.json"));
        var dialogue=new CafeSim.CafeDialogue();
        var timer=Stopwatch.StartNew(); int count=0;
        var examples=new System.Text.StringBuilder();
        for(int p=0;p<people.items.Length;p++) for(int visit=0;visit<2;visit++)
        {
            dialogue.Prepare(people.items[p],p,visit,menu);
            var order=dialogue.Order;
            var selected=Array.Find(menu.items,m=>m.id==order.menuId);
            if(!CafeSim.CafePrompts.Prefers(people.items[p],order.menuId)||!selected.Supports(order.temperature)) throw new Exception("Invalid order");
            if(!string.IsNullOrEmpty(people.items[p].preferredTemperature)&&order.temperature!=people.items[p].preferredTemperature) throw new Exception("Temperature preference");
            if(visit==0) examples.AppendLine(people.items[p].name+": "+order.speech);
            foreach(var served in menu.items) foreach(var temp in new[]{"HOT","ICE"})
            {
                if(!served.Supports(temp))continue;
                var r=dialogue.ForDelivery(served.id,temp);
                bool exact=served.id==order.menuId&&temp==order.temperature;
                if((r.stars==5)!=exact||r.stars<1||r.stars>5)throw new Exception("Rating mismatch");
                if(!exact&&(!r.speech.Contains(selected.name)||!r.speech.Contains(served.name)))throw new Exception("Missing facts");
                if(!CafeSim.CafePrompts.ValidSpeech(r.speech)||!CafeSim.CafePrompts.ValidSpeech(r.reason))throw new Exception("Invalid speech");
                count++;
            }
        }
        timer.Stop();
        string result="PASS: "+count+" deliveries across 9 personas and 2 variants; preparation+validation "+timer.Elapsed.TotalMilliseconds.ToString("F2")+"ms\n"+examples;
        File.WriteAllText("../.utmp/cafe-dialogue-check.txt",result);
        return result;
    }
}
