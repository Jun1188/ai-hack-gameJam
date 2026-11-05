using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CafeSim
{
    // ----- Static data (Resources/CafeCollection/collection.json, from 카페_손님_데이터테이블) -----

    [Serializable] public sealed class CollectionCustomerInfo { public string id, name, group, tag, style, greeting; public bool optional; }
    [Serializable] public sealed class CollectionPhraseInfo { public string customer, text, line, meaning; public string[] attrs; }
    [Serializable] sealed class CollectionTable { public CollectionCustomerInfo[] customers; public CollectionPhraseInfo[] phrases; }

    // ----- Saved progress (persistentDataPath/cafe_collection.json, kept across days and sessions) -----

    [Serializable] public sealed class CustomerRecord
    {
        public string id, state = "unseen";            // unseen / met / regular
        public int visits, satisfied, regularDay;
        public string lastMenu, lastTemp, regularLine;
        public List<string> situationsHeard = new List<string>();
        public List<string> satisfiedMenus = new List<string>();
        public bool newRegular;                        // stamp animation pending on the next notebook open
    }

    [Serializable] public sealed class PhraseRecord
    {
        public string id, text, meaning, attr, customer, menu, temp, tag;
        public int day;
        public bool byPlayer, isNew;
    }

    [Serializable] sealed class CollectionSave
    {
        public List<CustomerRecord> customers = new List<CustomerRecord>();
        public List<PhraseRecord> phrases = new List<PhraseRecord>();
        public List<string> stamps = new List<string>();
        public int cleanDays, lastScoredDay;
        public int nextPhraseId = 1;
        // Ending (ending_spec.md): pending once all 16 are regulars at a closing receipt, cleared after it is seen.
        public bool endingPending, endingCleared;
        public int endingDay;
    }

    // 도감 (단골 장부 + 동네 사전), following collection_system_spec.md.
    //  - 만남: served once, success or not.  단골: satisfied on two different visits.
    //  - 만족: the right drink (exact, or close for an off-menu request) and at least 3 stars (mood >= 0).
    //  - 들었던 사정: recorded when the customer actually says their situation of the day.
    //  - 동네 사전: phrases from the persona phrase table that the customer said during a satisfied visit,
    //    kept only when they describe the drink that was actually ordered (max 3 per visit, no duplicates,
    //    no forbidden words). The blind customer instead records the player's own description.
    public sealed class CafeCollection
    {
        public const int RegularVisitsNeeded = 2;
        const int PhrasesPerVisit = 3;
        static readonly string[] Profanity = { "씨발", "시발", "병신", "좆", "새끼", "fuck", "shit" };

        static CafeCollection instance;
        public static CafeCollection Instance => instance ??= new CafeCollection();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatic() => instance = null;

        public readonly List<CollectionCustomerInfo> Customers = new List<CollectionCustomerInfo>();
        readonly List<CollectionPhraseInfo> lexicon = new List<CollectionPhraseInfo>();
        readonly Dictionary<string, MenuFact> facts = new Dictionary<string, MenuFact>();
        readonly Dictionary<string, List<VisitSituation>> situations = new Dictionary<string, List<VisitSituation>>();
        readonly Dictionary<string, string> twists = new Dictionary<string, string>();
        readonly Dictionary<string, string> menuNames = new Dictionary<string, string>();
        CollectionSave save;

        // Per-visit working state; never saved.
        readonly Dictionary<int, List<CollectionPhraseInfo>> heardPhrases = new Dictionary<int, List<CollectionPhraseInfo>>();
        readonly Dictionary<int, List<string>> playerLines = new Dictionary<int, List<string>>();
        readonly HashSet<int> resolved = new HashSet<int>();
        public readonly List<PhraseRecord> LearnedToday = new List<PhraseRecord>();

        static string SavePath => Path.Combine(Application.persistentDataPath, "cafe_collection.json");

        CafeCollection()
        {
            var table = JsonUtility.FromJson<CollectionTable>(Resources.Load<TextAsset>("CafeCollection/collection")?.text ?? "{}");
            if (table?.customers != null) Customers.AddRange(table.customers);
            if (table?.phrases != null) lexicon.AddRange(table.phrases);
            var factRows = Resources.Load<TextAsset>("CafeKnowledge/menuFacts");
            if (factRows != null) foreach (var f in JsonUtility.FromJson<KnowledgeRows<MenuFact>>(factRows.text).items) facts[f.id] = f;
            var situationRows = Resources.Load<TextAsset>("CafeKnowledge/situations");
            if (situationRows != null)
                foreach (var s in JsonUtility.FromJson<KnowledgeRows<VisitSituation>>(situationRows.text).items)
                {
                    if (!situations.TryGetValue(s.personaId, out var list)) situations[s.personaId] = list = new List<VisitSituation>();
                    list.Add(s);
                }
            Load();
        }

        // Persona twists and menu display names come from the scene's own data tables.
        public void Bind(PersonaTable people, MenuTable menu)
        {
            if (people?.items != null) foreach (var p in people.items) twists[p.id] = p.twist;
            if (menu?.items != null) foreach (var m in menu.items) menuNames[m.id] = m.name;
        }

        public void BeginDay() { LearnedToday.Clear(); heardPhrases.Clear(); playerLines.Clear(); resolved.Clear(); }

        // ----- Queries for the notebook -----

        public CustomerRecord Record(string id)
        {
            var r = save.customers.Find(c => c.id == id);
            if (r == null) { r = new CustomerRecord { id = id }; save.customers.Add(r); }
            return r;
        }
        public CollectionCustomerInfo Info(string id) => Customers.Find(c => c.id == id);
        public IReadOnlyList<PhraseRecord> Phrases => save.phrases;
        public IReadOnlyList<string> Stamps => save.stamps;
        public int RegularCount { get { int n = 0; foreach (var c in Customers) if (Record(c.id).state == "regular") n++; return n; } }
        public int RequiredCount => Customers.FindAll(c => !c.optional).Count;
        public int RequiredRegularCount => Customers.FindAll(c => !c.optional && Record(c.id).state == "regular").Count;
        public bool AllRegular => RequiredCount > 0 && RequiredRegularCount == RequiredCount;
        public bool HasNews => save.phrases.Exists(p => p.isNew) || save.customers.Exists(c => c.newRegular);
        public string Twist(string id) => twists.TryGetValue(id, out var t) ? t : "";
        public List<VisitSituation> SituationsOf(string id) => situations.TryGetValue(id, out var list) ? list : new List<VisitSituation>();
        public string DrinkLabel(string menuId, string temp)
        {
            if (string.IsNullOrEmpty(menuId)) return "-";
            string name = menuNames.TryGetValue(menuId, out var n) ? n : menuId;
            return CafeQueueSim.Label(name, temp);
        }

        public void MarkSeen()
        {
            bool changed = false;
            foreach (var p in save.phrases) if (p.isNew) { p.isNew = false; changed = true; }
            foreach (var c in save.customers) if (c.newRegular) { c.newRegular = false; changed = true; }
            if (changed) Save();
        }

        // ----- Order line additions (called by CafeQueueSim when a customer orders) -----

        // A regular greets the barista first, then one or two persona phrases that describe the order.
        public string OrderLines(CafeVisit c, out string greeting)
        {
            greeting = Record(c.Person.id).state == "regular" ? Info(c.Person.id)?.greeting : null;
            if (!facts.TryGetValue(c.Order.menuId, out var fact)) return "";
            var matching = lexicon.FindAll(p => p.customer == c.Person.id && Matches(p.attrs, fact, c.Order.temperature));
            if (matching.Count == 0) return "";
            // Prefer one taste phrase and one temperature phrase; rotate by visit so repeats vary.
            var taste = matching.FindAll(p => !IsTemperature(p.attrs));
            var temp = matching.FindAll(p => IsTemperature(p.attrs));
            var lines = new List<string>();
            if (taste.Count > 0) lines.Add(taste[(c.VisitNumber + c.Id) % taste.Count].line);
            if (temp.Count > 0 && (lines.Count == 0 || (c.Id + c.VisitNumber) % 3 != 0)) lines.Add(temp[(c.VisitNumber + c.Id) % temp.Count].line);
            return string.Join(" ", lines);
        }

        static bool IsTemperature(string[] attrs) => Array.TrueForAll(attrs, a => a == "hot" || a == "ice");

        // ----- Recording (called by CafeBusinessDay for every dialogue event) -----

        public void Observe(CafeVisit c, string kind, string line, CafeKnowledge knowledge, int day)
        {
            if (c?.Person == null) return;
            switch (kind)
            {
                case "ORDER":
                case "CHATTER":
                case "REPLY":
                case "REACTION":
                    NoteCustomerLine(c, line);
                    break;
                case "USER":
                    if (!playerLines.TryGetValue(c.Id, out var said)) playerLines[c.Id] = said = new List<string>();
                    said.Add(line);
                    break;
                case "SERVED":
                    Resolve(c, true, knowledge, day);
                    break;
                case "RATING":
                    if (c.State == VisitState.TimedOut) Resolve(c, false, knowledge, day);
                    break;
            }
        }

        void NoteCustomerLine(CafeVisit c, string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            // The situation of the day counts as heard once the customer has actually said it.
            if (c.Situation != null && !string.IsNullOrEmpty(c.Situation.spokenContext) && line.Contains(c.Situation.spokenContext))
            {
                var r = Record(c.Person.id);
                if (!r.situationsHeard.Contains(c.Situation.id)) { r.situationsHeard.Add(c.Situation.id); Save(); }
            }
            foreach (var p in lexicon)
            {
                if (p.customer != c.Person.id || line.IndexOf(p.text, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!heardPhrases.TryGetValue(c.Id, out var list)) heardPhrases[c.Id] = list = new List<CollectionPhraseInfo>();
                if (!list.Contains(p)) list.Add(p);
            }
        }

        void Resolve(CafeVisit c, bool served, CafeKnowledge knowledge, int day)
        {
            if (!resolved.Add(c.Id)) return;   // one result per visit
            var r = Record(c.Person.id);
            r.visits++;
            if (r.state == "unseen") r.state = "met";
            if (served) { r.lastMenu = facts.ContainsKey(c.Order.menuId) ? c.Order.menuId : MenuIdByName(c.ServedName); r.lastTemp = c.Order.temperature; }

            bool rightDrink = served && (c.Accuracy >= .999f || (c.OffMenu && c.Accuracy >= CafeDayLedger.SuccessSimilarity));
            bool satisfied = rightDrink && (c.Reaction?.stars ?? 0) >= 3;
            if (satisfied)
            {
                r.satisfied++;
                string menuId = facts.ContainsKey(c.Order.menuId) ? c.Order.menuId : MenuIdByName(c.ServedName);
                if (!string.IsNullOrEmpty(menuId)) r.satisfiedMenus.Add(menuId);
                if (r.state != "regular" && r.satisfied >= RegularVisitsNeeded)
                {
                    r.state = "regular";
                    r.regularDay = day;
                    r.newRegular = true;
                    r.regularLine = c.Reaction?.speech;
                    CheckGroupStamps();
                }
            }
            if (rightDrink) RegisterPhrases(c, knowledge, day);
            Save();
        }

        string MenuIdByName(string name)
        {
            foreach (var pair in menuNames) if (pair.Value == name) return pair.Key;
            return null;
        }

        void RegisterPhrases(CafeVisit c, CafeKnowledge knowledge, int day)
        {
            string menuId = facts.ContainsKey(c.Order.menuId) ? c.Order.menuId : MenuIdByName(c.ServedName);
            if (menuId == null || !facts.TryGetValue(menuId, out var fact)) return;
            string temp = c.Order.temperature;
            var info = Info(c.Person.id);
            int added = 0;

            if (c.Person.id == "cus_blind")
            {
                // Role reversal: keep the player's description that led the customer to this drink.
                if (playerLines.TryGetValue(c.Id, out var said))
                    for (int i = said.Count - 1; i >= 0 && added < 1; i--)
                    {
                        string clause = DescriptiveClause(said[i], fact, temp, out string attr);
                        if (clause == null || !Safe(clause, knowledge)) continue;
                        if (AddPhrase(clause, DescribeAttrs(fact, temp), attr, c, menuId, temp, "기타", day, true)) added++;
                    }
                return;
            }

            if (!heardPhrases.TryGetValue(c.Id, out var heard)) return;
            foreach (var p in heard)
            {
                if (added >= PhrasesPerVisit) break;
                if (!Matches(p.attrs, fact, temp) || !Safe(p.text, knowledge)) continue;   // attribute mismatch is discarded
                if (AddPhrase(p.text, p.meaning, string.Join(",", p.attrs), c, menuId, temp, info?.tag ?? "기타", day, false)) added++;
            }
        }

        bool AddPhrase(string text, string meaning, string attr, CafeVisit c, string menuId, string temp, string tag, int day, bool byPlayer)
        {
            // Same customer type with the same text or the same meaning is a duplicate.
            if (save.phrases.Exists(p => p.customer == c.Person.id && (p.text == text || p.meaning == meaning))) return false;
            var record = new PhraseRecord
            {
                id = "ph_" + save.nextPhraseId++.ToString("0000"), text = text, meaning = meaning, attr = attr,
                customer = c.Person.id, menu = menuId, temp = temp, tag = tag, day = day, byPlayer = byPlayer, isNew = true
            };
            save.phrases.Add(record);
            LearnedToday.Add(record);
            return true;
        }

        static bool Safe(string text, CafeKnowledge knowledge)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (knowledge != null && knowledge.ContainsForbidden(text)) return false;
            foreach (var bad in Profanity) if (text.IndexOf(bad, StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return true;
        }

        // ----- Day end: hidden stamp "편견 없는 카페" (three days in a row without guessing) -----

        public void EndDay(int day, CafeDayLedger ledger)
        {
            if (day == save.lastScoredDay || ledger.Orders.Count == 0) return;
            save.lastScoredDay = day;
            save.cleanDays = ledger.Guesses == 0 ? save.cleanDays + 1 : 0;
            if (save.cleanDays >= 3 && !save.stamps.Contains("no_presumption")) save.stamps.Add("no_presumption");
            Save();
        }

        // Every customer in a group (of two or more) is a regular: that group's stamp.
        void CheckGroupStamps()
        {
            var groups = new Dictionary<string, (bool all, int count)>();
            foreach (var c in Customers)
            {
                if (c.optional) continue;
                bool regular = Record(c.id).state == "regular";
                groups[c.group] = groups.TryGetValue(c.group, out var g) ? (g.all && regular, g.count + 1) : (regular, 1);
            }
            foreach (var pair in groups)
            {
                string stamp = "group_" + pair.Key;
                if (pair.Value.all && pair.Value.count >= 2 && !save.stamps.Contains(stamp)) save.stamps.Add(stamp);
            }
        }

        public static string StampName(string stamp)
        {
            switch (stamp)
            {
                case "no_presumption": return "편견 없는 카페";
                case "group_지역": return "팔도 도장";
                case "group_외국인": return "지구촌 도장";
                case "group_세대": return "세대 도장";
                case "group_직업": return "일터 도장";
                default: return stamp.Replace("group_", "") + " 도장";
            }
        }

        // ----- Drink attributes (from 주문 속성) -----

        public static bool Matches(string[] attrs, MenuFact f, string temp)
        {
            if (attrs == null || attrs.Length == 0) return false;
            foreach (var a in attrs) if (!Has(a, f, temp)) return false;
            return true;
        }

        public static bool Has(string attr, MenuFact f, string temp)
        {
            bool coffee = f.coffee != null && f.coffee.Contains("있음");
            bool milk = f.milk != null && f.milk.Contains("있음");
            string flavor = f.flavor ?? "";
            bool bitter = (coffee && !milk) || flavor.Contains("쌉");
            bool sour = flavor.Contains("새콤") || flavor.Contains("시큼");
            switch (attr)
            {
                case "sweet": return f.sweetness >= 2;
                case "unsweet": return f.sweetness == 0;
                case "lowsweet": return f.sweetness <= 1;
                case "bitter": return bitter;
                case "nonbitter": return !bitter;
                case "milk": return milk;
                case "coffee": return coffee;
                case "nocoffee": return !coffee;
                case "strong": return f.coffee != null && f.coffee.Contains("진함");
                case "sour": return sour;
                case "nonsour": return !sour;
                case "fizzy": return f.carbonation != null && f.carbonation.Contains("있음");
                case "green": return f.color != null && f.color.Contains("초록");
                case "pink": return f.color != null && f.color.Contains("분홍");
                case "choco": return flavor.Contains("초콜릿");
                case "vanilla": return f.id == "menu_vanilla";
                case "lemon": return f.id == "menu_lemonade";
                case "herbal": return flavor.Contains("한약");
                case "traditional": return f.id == "menu_ssanghwa" || f.id == "menu_yuja";
                case "hot": return temp == "HOT";
                case "ice": return temp == "ICE";
                default: return false;
            }
        }

        // Player wording that describes a drink, for the blind customer's role reversal.
        static readonly (string attr, string[] words)[] PlayerWords =
        {
            ("sweet", new[] { "달콤", "달달", "단맛", "달아", "달고" }), ("bitter", new[] { "쓴", "씁쓸", "쌉" }),
            ("milk", new[] { "우유", "고소", "부드러" }), ("coffee", new[] { "커피", "카페인" }),
            ("sour", new[] { "새콤", "상큼", "시큼", "신맛" }), ("fizzy", new[] { "탄산", "톡 쏘", "톡쏘", "청량" }),
            ("hot", new[] { "따뜻", "뜨거", "따끈" }), ("ice", new[] { "차갑", "시원", "차가" }),
            ("choco", new[] { "초콜릿", "초코" }), ("green", new[] { "녹차", "초록" }), ("pink", new[] { "분홍", "딸기" }),
            ("herbal", new[] { "한약", "구수", "약재" }), ("traditional", new[] { "전통", "유자" })
        };

        static string DescriptiveClause(string said, MenuFact fact, string temp, out string attr)
        {
            attr = null;
            foreach (var clause in said.Split(new[] { '.', '!', '?', ',', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string text = clause.Trim();
                foreach (var (a, words) in PlayerWords)
                {
                    if (!Has(a, fact, temp)) continue;
                    foreach (var w in words)
                        if (text.Contains(w))
                        {
                            attr = a;
                            return text.Length > 28 ? text.Substring(0, 28) + "…" : text;
                        }
                }
            }
            return null;
        }

        public static string DescribeAttrs(MenuFact f, string temp)
        {
            var parts = new List<string>();
            if (Has("sour", f, temp)) parts.Add("새콤한");
            else if (Has("sweet", f, temp)) parts.Add("달콤한");
            if (Has("bitter", f, temp) && !Has("sour", f, temp)) parts.Add("쌉쌀한");
            if (Has("fizzy", f, temp)) parts.Add("탄산");
            else if (Has("milk", f, temp)) parts.Add("우유");
            parts.Add(Has("coffee", f, temp) ? "커피" : "음료");
            return string.Join(" ", parts);
        }

        // 좋아하는 맛: summarised from every drink the customer was satisfied with.
        public string FavouriteTaste(CustomerRecord r)
        {
            if (r.satisfiedMenus.Count == 0) return "-";
            int sweet = 0, bitter = 0, milk = 0, sour = 0, fizzy = 0, herbal = 0, coffee = 0;
            foreach (var id in r.satisfiedMenus)
            {
                if (!facts.TryGetValue(id, out var f)) continue;
                if (Has("sweet", f, null)) sweet++;
                if (Has("bitter", f, null)) bitter++;
                if (Has("milk", f, null)) milk++;
                if (Has("sour", f, null)) sour++;
                if (Has("fizzy", f, null)) fizzy++;
                if (Has("herbal", f, null)) herbal++;
                if (Has("coffee", f, null)) coffee++;
            }
            int half = Mathf.Max(1, (r.satisfiedMenus.Count + 1) / 2);
            if (sour >= half && sweet >= half) return fizzy >= half ? "새콤하고 톡 쏘는 맛" : "새콤달콤한 맛";
            if (sweet >= half && milk >= half) return "달고 부드러운 맛";
            if (herbal >= half) return "구수하고 쌉쌀한 맛";
            if (bitter >= half && coffee >= half) return "쓰고 진한 커피 맛";
            if (sweet >= half) return "달콤한 맛";
            if (milk >= half) return "고소하고 부드러운 맛";
            if (sour >= half) return "새콤한 맛";
            if (bitter >= half) return "쌉쌀한 맛";
            return "그날그날 다른 맛";
        }

        public static string AttrLabel(string attr)
        {
            if (string.IsNullOrEmpty(attr)) return "취향";
            string first = attr.Split(',')[0];
            switch (first)
            {
                case "sweet": case "unsweet": case "lowsweet": return "단맛";
                case "bitter": case "nonbitter": return "쓴맛";
                case "hot": case "ice": return "온도";
                case "milk": return "우유";
                case "coffee": case "nocoffee": case "strong": return "커피";
                case "sour": case "nonsour": return "새콤한 맛";
                case "fizzy": return "탄산";
                case "green": case "pink": return "색";
                default: return "맛";
            }
        }

        // ----- Persistence -----

        void Load()
        {
            try
            {
                save = File.Exists(SavePath) ? JsonUtility.FromJson<CollectionSave>(File.ReadAllText(SavePath)) : null;
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Cafe] 도감 저장 파일을 읽지 못해 새로 시작합니다: " + error.Message);
                save = null;
            }
            save ??= new CollectionSave();
        }

        void Save()
        {
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(save, true)); }
            catch (Exception error) { Debug.LogWarning("[Cafe] 도감 저장 실패: " + error.Message); }
        }

        // ----- Ending -----

        public bool EndingPending => save.endingPending;
        public bool EndingCleared => save.endingCleared;
        public int EndingDay => save.endingDay;
        // The final day's receipt calls for the ending (all regulars, not seen yet).
        public bool EndingDue => AllRegular && !save.endingCleared;

        // Judged when the closing receipt is dismissed; saved at once so quitting still leads to the ending.
        public void MarkEndingPending(int morningDay)
        {
            if (!EndingDue || save.endingPending) return;
            save.endingPending = true;
            save.endingDay = morningDay;
            Save();
        }

        public void MarkEndingSeen()
        {
            save.endingPending = false;
            save.endingCleared = true;
            Save();
        }

        // For testing: wipes all progress.
        public void ResetProgress()
        {
            save = new CollectionSave();
            LearnedToday.Clear();
            Save();
        }
    }
}
