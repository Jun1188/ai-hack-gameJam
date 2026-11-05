using System;
using System.Collections.Generic;
using UnityEngine;

namespace CafeSim
{
    [Serializable] public sealed class CafeVocabularyTable { public int version; public CafeVocabulary[] items; }
    [Serializable] public sealed class CafeVocabulary
    {
        public string id;
        public string[] orderOpen, orderClose, matched, correction, ratingGood, ratingBad;
    }
    [Serializable] public sealed class CafePlanTable { public int[] choices; }

    // Model selects bounded vocabulary variants ahead of time. Facts never come from free text.
    public sealed class CafeDialogue
    {
        readonly CafeVocabularyTable vocabulary;
        readonly CafePlanTable plans;
        readonly Dictionary<string, CustomerReaction> reactions = new Dictionary<string, CustomerReaction>();
        public CustomerOrder Order { get; private set; }
        public string Source { get; private set; }
        public CafeDialogue()
        {
            var asset = Resources.Load<TextAsset>("CafeVocabulary");
            if (asset == null) throw new InvalidOperationException("CafeVocabulary table missing");
            vocabulary = JsonUtility.FromJson<CafeVocabularyTable>(asset.text);
            var prepared = Resources.Load<TextAsset>("CafePlans");
            plans = prepared == null ? new CafePlanTable() : JsonUtility.FromJson<CafePlanTable>(prepared.text);
            Source = prepared == null ? "어휘 테이블 기본 변형" : "로컬 모델 사전 선택";
        }
        public string PlanPrompt(PersonaTable people)
        {
            var prompt = new System.Text.StringBuilder("각 손님의 상황과 말투에 어울리는 어휘 변형 번호 0 또는 1을 순서대로 고른다. JSON만 반환: {\"choices\":[0,1,0,1,0,1,0,1,0]}\n");
            foreach (var person in people.items)
            {
                var lex = Array.Find(vocabulary.items,x => x.id == person.id);
                prompt.Append(person.id).Append(' ').Append(person.setting).Append(' ').Append(person.voice)
                    .Append(" 0=").Append(lex.orderOpen[0]).Append(" 1=").Append(lex.orderOpen[Math.Min(1,lex.orderOpen.Length-1)]).AppendLine();
            }
            return prompt.ToString();
        }
        public bool AcceptPlans(string json, int count)
        {
            try
            {
                var candidate = JsonUtility.FromJson<CafePlanTable>(json);
                if (candidate?.choices == null || candidate.choices.Length != count || Array.Exists(candidate.choices, x => x < 0 || x > 1)) return false;
                plans.choices = (int[])candidate.choices.Clone(); Source = "로컬 모델 사전 선택"; return true;
            }
            catch (Exception) { return false; }
        }
        static string Pick(string[] words, int index)
        {
            if (words == null || words.Length == 0) throw new InvalidOperationException("Empty persona vocabulary");
            return words[index % words.Length];
        }
        public void Prepare(Persona person, int personaIndex, int visit, MenuTable menu)
        {
            var lex = Array.Find(vocabulary.items, x => x.id == person.id);
            if (lex == null) throw new InvalidOperationException("Missing persona vocabulary: " + person.id);
            int choice = plans.choices != null && personaIndex < plans.choices.Length ? plans.choices[personaIndex] : 0;
            int variant = (choice + visit) % 2;
            var candidates = CafePrompts.OrderMenu(person, menu).items;
            if (candidates.Length == 0) throw new InvalidOperationException("No persona order candidates");
            var selected = candidates[(choice + visit) % candidates.Length];
            string temperature = selected.ice && (!selected.hot || variant == 1) ? "ICE" : "HOT";
            string tempWord = temperature == "ICE" ? "아이스" : "따뜻한";
            Order = new CustomerOrder { menuId = selected.id, temperature = temperature,
                speech = Pick(lex.orderOpen,variant) + " " + tempWord + " " + selected.name + " 한 잔" + Pick(lex.orderClose,variant) };
            reactions.Clear();
            // All 18 possible deliveries are resolved before this customer enters.
            foreach (var served in menu.items)
                foreach (var temp in new[] { "HOT", "ICE" })
                {
                    if (!served.Supports(temp)) continue;
                    bool sameMenu = served.id == Order.menuId, sameTemp = temp == Order.temperature;
                    bool exact = sameMenu && sameTemp;
                    string fact = exact ? "" : sameMenu
                        ? "주문은 " + tempWord + " " + selected.name + "였는데, 받은 건 " + Label(served.name,temp) + "네요. "
                        : "제가 부탁한 건 " + Label(selected.name,temperature) + "인데, " + Label(served.name,temp) + "가 나왔네요. ";
                    reactions[Key(served.id,temp)] = new CustomerReaction {
                        speech = exact ? Pick(lex.matched,variant) : fact + Pick(lex.correction,variant),
                        stars = exact ? 5 : sameMenu ? 4 : sameTemp ? (CafePrompts.Prefers(person,served.id) ? 3 : 2) : 1,
                        reason = exact ? Pick(lex.ratingGood,variant) : Pick(lex.ratingBad,variant) };
                }
        }
        static string Label(string name,string temp) => (temp == "HOT" ? "따뜻한 " : "아이스 ") + name;
        static string Key(string menu,string temp) => menu + ":" + temp;
        public CustomerReaction ForDelivery(string menu,string temp) => reactions[Key(menu,temp)];
    }
}
