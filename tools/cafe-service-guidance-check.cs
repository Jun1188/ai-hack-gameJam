using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CafeSim;
using MapleCafe;
using UnityEditor;
using UnityEngine;

// Explicit Editor check, never attached to a scene and never starts Play or builds a player.
public static class CafeServiceGuidanceCheck
{
    sealed class ArrivalRandom : System.Random
    {
        readonly double roll; readonly int pick;
        public ArrivalRandom(double roll, int pick = 0) { this.roll = roll; this.pick = pick; }
        public override double NextDouble() => roll;
        public override int Next(int maxValue) => pick % maxValue;
    }
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    public static string Main()
    {
        Check(!EditorApplication.isPlaying, "Run outside Play with CafeGame open");
        var station = UnityEngine.Object.FindAnyObjectByType<CafeBrewStation>();
        Check(station != null, "Open CafeGame first");
        var menu = JsonUtility.FromJson<MenuTable>(File.ReadAllText("Assets/CafeSim/Data/menu.json"));
        var people = JsonUtility.FromJson<PersonaTable>(File.ReadAllText("Assets/CafeSim/Data/personas.json"));
        var kb = new CafeKnowledge();
        var choose = typeof(CafeQueueSim).GetMethod("ChoosePersona", BindingFlags.Static | BindingFlags.NonPublic);
        var miku = people.items.Single(p => p.id == "cus_miku");
        Persona Arrival(Persona[] cast, double roll, int pick = 0) => (Persona)choose.Invoke(null, new object[] { cast, new ArrivalRandom(roll, pick) });
        Check(Arrival(people.items, 0) == miku && Arrival(people.items, .049999) == miku, "5 percent Miku interval");
        Check(Arrival(people.items, .05) != miku && Arrival(people.items, .999999) != miku, "Miku interval upper boundary");
        var ordinary = people.items.Where(p => p.id != "cus_miku").ToArray();
        for (int i = 0; i < ordinary.Length; i++) Check(Arrival(people.items, .5, i) == ordinary[i], "Other arrivals remain selectable");
        Check(Arrival(new[] { miku }, .99) == miku && Arrival(ordinary, 0).id != "cus_miku", "MVP selection and casts without Miku");
        foreach (var person in people.items)
        foreach (bool offMenu in new[] { false, true })
        foreach (string temp in new[] { "HOT", "ICE" })
        {
            var q = new CafeQueueSim(new[] { person }, menu, kb, new CafeQueueSettings(), 81);
            var c = q.Admit(offMenu); q.AcceptOrder();
            Check(!q.Serve(c.Id, "unknown_menu", temp) && c.State == VisitState.Active, "Unknown drink accepted");
            Check(q.Serve(c.Id, CafeQueueSim.FailedDrinkId, temp), "Failed drink rejected " + person.id);
            Check(c.Reaction.stars == 1 && c.Accuracy == 0 && c.ServedName == CafeQueueSim.FailedDrinkName, "Failed drink rating/identity");
            Check(!q.Serve(c.Id, CafeQueueSim.FailedDrinkId, temp), "Double serve");
            q.PublishRating(c); Check(c.RatingPublished, "Missing failed rating");
            var ledger = new CafeDayLedger(); var entry = ledger.Served(c, 5000, 500, 1600);
            Check(!entry.success && entry.stars == 1 && entry.paid == 0 && entry.tip == 0 && ledger.WastedCups == 1, "Failed receipt policy");
        }
        var expired = new CafeQueueSim(new[] { people.items[0] }, menu, kb,
            new CafeQueueSettings { exactTimeout = true, activeTimeoutSeconds = 1 }, 1);
        var late = expired.Admit(); expired.AcceptOrder(); expired.Tick(1);
        Check(!expired.Serve(late.Id, CafeQueueSim.FailedDrinkId, "HOT") && late.Reaction.stars == 0, "Timeout still takes precedence");

        var type = station.GetType();
        string[] fields = { "heldIngredient", "shotPrep", "hasIce", "iceWaterReady", "portions", "blendIngredients", "holdingBlend", "holdingMug", "finishedDrink", "busy" };
        var baseline = fields.ToDictionary(n => n, n => type.GetField(n, Private).GetValue(station));
        var ingredientArray = (Array)type.GetField("ingredients", Private).GetValue(station);
        object Ingredient(string label) => ingredientArray.Cast<object>().Single(i => (string)i.GetType().GetField("label").GetValue(i) == label);
        int Bit(string label) { var i = Ingredient(label); return 1 << (int)i.GetType().GetField("kind").GetValue(i); }
        void Set(string field, object value)
        {
            var f = type.GetField(field, Private);
            f.SetValue(station, f.FieldType.IsEnum ? Enum.ToObject(f.FieldType, value) : value);
        }
        // Domain reload can retain private runtime fields in the Editor; each fixture starts empty.
        void Reset()
        {
            foreach (string name in fields)
            {
                var field = type.GetField(name, Private);
                field.SetValue(station, field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null);
            }
        }
        string Step(CafeSim.MenuItem item, bool ice, string expectedTarget)
        {
            var args = new object[] { item, ice, null, null };
            Check((bool)type.GetMethod("TryGetRecipeGuidance", Private).Invoke(station, args), "Guidance unavailable");
            var target = (Collider)args[2];
            if (expectedTarget != null) Check(target == (Collider)type.GetField(expectedTarget, Private).GetValue(station), "Wrong target: " + args[3]);
            Check(!string.IsNullOrWhiteSpace((string)args[3]), "Missing action text");
            return (string)args[3];
        }
        try
        {
            foreach (var item in menu.items)
            {
                Reset();
                var recipe = item.recipe.Split(new[] { " + " }, StringSplitOptions.None);
                int mask = recipe.Aggregate(0, (v, label) => v | Bit(label));
                var surface = type.Assembly.GetType("MapleCafe.CafeDrinkSurface");
                foreach (bool ice in new[] { false, true })
                {
                    if (!item.Supports(ice ? "ICE" : "HOT")) continue;
                    var drink = surface.GetMethod("MatchServing", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { mask, ice });
                    Check(drink != null && (string)drink.GetType().GetField("Name").GetValue(drink) == item.name, "Card recipe disagrees with brew " + item.id);
                    Set("hasIce", ice); Set("iceWaterReady", ice); Set("blendIngredients", mask);
                    Step(item, ice, "mixingTarget");
                }
            }
            Reset();
            var espresso = menu.items.Single(i => i.id == "menu_espresso");
            Step(espresso, false, "beanTarget"); Set("shotPrep", 1);
            Step(espresso, false, "grinderTarget"); Set("shotPrep", 2);
            Step(espresso, false, "espressoTarget"); Set("shotPrep", 0); Set("heldIngredient", Ingredient("에스프레소 샷"));
            Step(espresso, false, "mixingTarget"); Set("heldIngredient", null); Set("blendIngredients", Bit("에스프레소 샷"));
            Step(espresso, false, "mixingTarget"); Set("holdingBlend", true);
            Step(espresso, false, "mugTarget"); Set("holdingBlend", false); Set("finishedDrink", CafeQueueSim.FailedDrinkName);
            Step(espresso, false, "mugTarget");
            Reset();
            var latte = menu.items.Single(i => i.id == "menu_latte");
            Check(Step(latte, true, null).Contains("얼음"), "ICE first");
            Set("hasIce", true); Check(Step(latte, true, null).Contains("물"), "ICE water base");
            Set("iceWaterReady", true); Step(latte, true, "beanTarget");
            Set("blendIngredients", Bit("레몬청")); Step(latte, true, "sinkTarget");
        }
        finally { foreach (var kv in baseline) Set(kv.Key, kv.Value); }
        string result = "PASS: Miku 5% arrival interval, other arrivals and direct MVP selection; failed serving and 1-star receipts for every persona, off-menu and both temperatures; unknown/double/late serving rejected; card recipes match brewing; shot preparation, ICE base, blending, mug and wrong-mixture recovery guidance. No main-scene Play/build.";
        File.WriteAllText("../.utmp/cafe-service-guidance-check.txt", result);
        return result;
    }
}
