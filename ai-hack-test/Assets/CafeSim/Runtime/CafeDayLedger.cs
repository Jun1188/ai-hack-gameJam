using System;
using System.Collections.Generic;
using UnityEngine;

namespace CafeSim
{
    // One finished order on today's receipt: a drink handed over, or a customer who gave up waiting.
    public sealed class CafeLedgerEntry
    {
        public int visitId, paid, tip, stars, asks;
        public string personaId, personaName, label;
        public bool success, timedOut, guessed;
        public float serviceSeconds;
    }

    [Serializable] public sealed class CafeDrinkPrice { public string menuId; public int price; }

    // Everything the closing receipt shows, recorded from the session's dialogue events as the day is played.
    // An order succeeds when the drink is close enough that the customer keeps it (similarity >= .7, the same
    // line the queue uses for "similar"); anything less is a failed order, paid nothing, and the cup is thrown away.
    public sealed class CafeDayLedger
    {
        public const float SuccessSimilarity = .7f;

        public readonly List<CafeLedgerEntry> Orders = new List<CafeLedgerEntry>();
        readonly Dictionary<int, int> asksByVisit = new Dictionary<int, int>();

        public int Questions { get; private set; }   // 되물은 횟수: every line the player typed to a customer
        public int Guesses { get; private set; }     // 넘겨짚은 말: a wrong drink served without asking a single question
        public int WastedCups { get; private set; }
        public int Successes { get; private set; }
        public int Failures => Orders.Count - Successes;
        public int Sales { get; private set; }
        public int Tips { get; private set; }
        public int WasteCost { get; private set; }
        public int Profit => Sales + Tips - WasteCost;
        public float SuccessRate => Orders.Count == 0 ? 0 : Successes / (float)Orders.Count;

        public float AverageServiceSeconds
        {
            get
            {
                if (Orders.Count == 0) return 0;
                float sum = 0;
                foreach (var o in Orders) sum += o.serviceSeconds;
                return sum / Orders.Count;
            }
        }

        public void Ask(CafeVisit visit)
        {
            Questions++;
            asksByVisit[visit.Id] = AsksFor(visit) + 1;
        }

        public int AsksFor(CafeVisit visit) => asksByVisit.TryGetValue(visit.Id, out int n) ? n : 0;

        public CafeLedgerEntry Served(CafeVisit visit, int price, int tipPerStar, int cupCost)
        {
            bool success = visit.Accuracy >= SuccessSimilarity;
            int stars = visit.Reaction?.stars ?? 0;
            var entry = NewEntry(visit);
            entry.success = success;
            entry.stars = stars;
            entry.paid = success ? price : 0;
            entry.tip = success ? Mathf.Max(0, stars - 3) * tipPerStar : 0;
            entry.guessed = !success && entry.asks == 0;
            entry.serviceSeconds = Mathf.Max(0, visit.ResolvedAt - visit.OrderedAt);
            if (success) Successes++;
            else { WastedCups++; WasteCost += cupCost; }
            if (entry.guessed) Guesses++;
            Sales += entry.paid;
            Tips += entry.tip;
            Orders.Add(entry);
            return entry;
        }

        public CafeLedgerEntry TimedOut(CafeVisit visit)
        {
            var entry = NewEntry(visit);
            entry.timedOut = true;
            entry.serviceSeconds = visit.ActiveWait;
            Orders.Add(entry);
            return entry;
        }

        CafeLedgerEntry NewEntry(CafeVisit visit) => new CafeLedgerEntry
        {
            visitId = visit.Id,
            personaId = visit.Person?.id,
            personaName = visit.Person?.name,
            label = CafeQueueSim.Label(visit.RequestedName, visit.Order?.temperature),
            asks = AsksFor(visit)
        };

        // The customer served successfully most often today; ties go to whoever was served first.
        public bool BestCustomer(out string personaId, out string name, out int successes)
        {
            personaId = name = null;
            successes = 0;
            var counts = new Dictionary<string, int>();
            foreach (var o in Orders)
            {
                if (!o.success || string.IsNullOrEmpty(o.personaId)) continue;
                counts.TryGetValue(o.personaId, out int n);
                counts[o.personaId] = ++n;
                if (n > successes) { successes = n; personaId = o.personaId; name = o.personaName; }
            }
            return personaId != null;
        }

        public string Grade
        {
            get
            {
                if (Orders.Count == 0) return "-";
                float rate = SuccessRate;
                int level = rate >= .95f && Guesses == 0 && Orders.Count >= 5 ? 0 : rate >= .8f ? 1 : rate >= .6f ? 2 : rate >= .4f ? 3 : 4;
                if (Guesses >= 3) level = Mathf.Min(4, level + 1);   // guessing instead of asking costs a grade
                return new[] { "S", "A", "B", "C", "D" }[level];
            }
        }

        public string Quote
        {
            get
            {
                int timeouts = 0;
                foreach (var o in Orders) if (o.timedOut) timeouts++;
                if (Orders.Count == 0) return "오늘은 손님이 한 명도 없었네…";
                if (Guesses >= 2 && Guesses * 2 >= Failures) return "짐작하지 말고 한 번만 더 물어봐 주지.";
                if (timeouts >= 2 && timeouts * 2 >= Failures) return "기다리다 목이 빠지는 줄 알았어요.";
                if (SuccessRate >= .8f && Questions >= Successes) return "말을 끝까지 들어주는 알바생이네.";
                if (SuccessRate >= .8f) return "손이 참 빠른 알바생이네.";
                if (SuccessRate >= .5f) return "조금만 더 귀 기울여 주면 좋겠어.";
                return "내일은 주문부터 차근차근 들어 봐요.";
            }
        }
    }
}
