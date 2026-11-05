using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CafeSim;
using UnityEditor;
using UnityEngine;

public static class CafePersonaBranchCheck
{
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    public static string Main()
    {
        Check(!EditorApplication.isPlaying, "Run this check outside Play");
        var people = JsonUtility.FromJson<PersonaTable>(File.ReadAllText("Assets/CafeSim/Data/personas.json"));
        var menu = JsonUtility.FromJson<MenuTable>(File.ReadAllText("Assets/CafeSim/Data/menu.json"));
        Check(people.items.Length == 18 && people.items.Count(p => string.IsNullOrEmpty(p.parentPersonaId)) == 16, "16 originals + 2 branches");
        var miku = people.items.Single(p => p.id == "cus_miku");
        var egg = people.items.Single(p => p.id == "cus_kim_gyeran");
        var parent = people.items.Single(p => p.id == "cus_trainer");
        Check(parent.gender.Contains("청년 여성") && parent.name == "트레이너", "Original trainer restored");
        Check(egg.gender.Contains("중년 남성") && miku.gender.Contains("청년 여성"), "Branch identity");
        var kb = new CafeKnowledge(); var conversation = new CafeConversation();
        CafeCustomerCatalog.Reload();
        foreach (string id in new[] { "cus_west", "cus_china", "cus_barista", "cus_trainer", "cus_chungcheong", "cus_gangwon", "cus_blind" })
            Check(CafeCustomerCatalog.Choose(id, new System.Random(4)) == null, "Branch appearance leaked to original: " + id);
        var output = new System.Collections.Generic.List<string>();
        var preview = new PreviewRenderUtility();
        try
        {
            var context = new GameObject("Branch check"); preview.AddSingleGO(context);
            var session = context.AddComponent<CafeSession>(); session.entrance = context.transform;
            foreach (var p in new[] { miku, egg })
            {
                var q = new CafeQueueSim(new[] { p }, menu, kb, new CafeQueueSettings(), 31);
                var visit = q.Admit(false); q.AcceptOrder();
                var originalVisit = new CafeVisit { Person = people.items.Single(x => x.id == p.parentPersonaId), State = VisitState.Active, VisitNumber = 1 };
                foreach (string slot in new[] { "opener", "ending", "match", "wrong", "good", "bad", "chatter" })
                    Check(kb.Retrieve(visit, slot, 0) != kb.Retrieve(originalVisit, slot, 0), "Inherited parent dialogue in branch: " + p.id + "/" + slot);
                visit.LastThought = kb.Retrieve(visit, "chatter", 0);
                Check(conversation.TryDirect(visit, "더 자세히 들려주세요", out var reply), "Branch follow-up missing");
                Check(CafePrompts.ValidSpeech(reply), "Invalid branch follow-up");
                Check(CafeCustomerCatalog.Choose(p.id, new System.Random(4))?.personaIds.Single() == p.id, "Exclusive branch model");
                var spawn = typeof(CafeCustomerAppearance).GetMethod("Spawn", BindingFlags.Static | BindingFlags.NonPublic);
                var root = (Transform)spawn.Invoke(null, new object[] { session, visit, new System.Random(4) }); preview.AddSingleGO(root.gameObject);
                var grip = typeof(CafeCustomerAppearance).GetMethod("PrepareMugGrip", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(root.GetComponent<CafeCustomerAppearance>(), null);
                Check(grip != null, "Branch mug grip missing");
                output.Add(p.name + " 주문: " + visit.Order.speech + "\n후속: " + reply);
                UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
        }
        finally { preview.Cleanup(); }
        // Isolated in-memory collection: never write or replace the user's saved progress.
        var collection = (CafeCollection)Activator.CreateInstance(typeof(CafeCollection), true);
        Check(collection.Customers.Count == 18 && collection.RequiredCount == 16, "Optional notebook entries");
        foreach (var c in collection.Customers) collection.Record(c.id).state = c.optional ? "unseen" : "regular";
        Check(collection.AllRegular, "Optional branches changed original ending requirement");
        collection.Record("cus_trainer").state = "unseen";
        collection.Record("cus_kim_gyeran").state = "regular";
        Check(!collection.AllRegular, "Branch progress must not replace original progress");
        Check(new CafeDialogue().PlanPrompt(people).Contains("cus_miku"), "Legacy vocabulary compatibility");
        output.Add("PASS originals restored, independent branch dialogue/models/records, 16-person ending preserved; no Play/build.");
        string result = string.Join("\n", output);
        File.WriteAllText("../.utmp/cafe-persona-branch-check.txt", result);
        return result;
    }
}
