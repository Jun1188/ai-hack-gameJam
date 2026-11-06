using System;
using System.Collections.Generic;
using System.Linq;

namespace CafeCoworker
{
    public enum Worker { Player, Barista }
    public enum WorkState { Waiting, Ready, Moving, Working, Done, Cancelled }
    [Serializable] public sealed class WorkSpec
    {
        public string id, label, station, owner;
        public string[] after = Array.Empty<string>();
        public float seconds = 3;
    }
    [Serializable] public sealed class RecipeSpec { public string id, name; public WorkSpec[] tasks; }
    public sealed class WorkItem
    {
        public int OrderId { get; }
        public WorkSpec Spec { get; }
        public Worker Owner { get; }
        public WorkState State { get; internal set; }
        public double ReadyAt { get; internal set; }
        public double ReminderAt { get; internal set; }
        public float Progress { get; internal set; }
        public string Blocked { get; internal set; } = "";
        internal WorkItem(int order, WorkSpec spec)
        { OrderId = order; Spec = spec; Owner = spec.owner == "barista" ? Worker.Barista : Worker.Player; }
        public bool Finished => State == WorkState.Done || State == WorkState.Cancelled;
    }
    public sealed class WorkOrder
    {
        public int Id { get; }
        public RecipeSpec Recipe { get; }
        public List<WorkItem> Tasks { get; }
        public bool Cancelled { get; internal set; }
        public bool Done => !Cancelled && Tasks.All(t => t.State == WorkState.Done);
        internal WorkOrder(int id, RecipeSpec recipe)
        { Id = id; Recipe = recipe; Tasks = recipe.tasks.Select(t => new WorkItem(id, t)).ToList(); }
    }
    // All time is supplied by the scene; no global clock, model, UI or Unity dependency.
    public sealed class WorkScheduler
    {
        readonly List<WorkOrder> orders = new List<WorkOrder>();
        readonly Dictionary<string, WorkItem> reservations = new Dictionary<string, WorkItem>();
        int nextId = 1;
        public IReadOnlyList<WorkOrder> Orders => orders;
        public event Action<WorkItem> Completed;
        public WorkOrder Add(RecipeSpec recipe, double now)
        {
            Validate(recipe);
            var order = new WorkOrder(nextId++, recipe);
            orders.Add(order); Refresh(now); return order;
        }
        public static void Validate(RecipeSpec recipe)
        {
            if (recipe == null || recipe.tasks == null || recipe.tasks.Length == 0) throw new ArgumentException("Empty recipe");
            var ids = new HashSet<string>();
            foreach (var t in recipe.tasks)
                if (t == null || string.IsNullOrEmpty(t.id) || !ids.Add(t.id) || string.IsNullOrEmpty(t.station) ||
                    (t.owner != "player" && t.owner != "barista") || t.seconds <= 0 || float.IsNaN(t.seconds) || float.IsInfinity(t.seconds))
                    throw new ArgumentException("Invalid task");
            var done = new HashSet<string>();
            for (int i = 0; i < recipe.tasks.Length; i++)
                foreach (var t in recipe.tasks)
                    if ((t.after ?? Array.Empty<string>()).All(done.Contains)) done.Add(t.id);
            if (done.Count != ids.Count) throw new ArgumentException("Missing prerequisite or cycle");
        }
        public void Refresh(double now)
        {
            foreach (var o in orders.Where(x => !x.Cancelled))
                foreach (var t in o.Tasks.Where(x => x.State == WorkState.Waiting))
                    if ((t.Spec.after ?? Array.Empty<string>()).All(id => o.Tasks.Any(x => x.Spec.id == id && x.State == WorkState.Done)))
                    { t.State = WorkState.Ready; t.ReadyAt = now; t.ReminderAt = now; }
        }
        public WorkItem Next(Worker owner) => orders.Where(o => !o.Cancelled).SelectMany(o => o.Tasks)
            .FirstOrDefault(t => t.Owner == owner && (t.State == WorkState.Ready || t.State == WorkState.Moving || t.State == WorkState.Working));
        public bool IsAvailable(WorkItem task) => Contains(task) && !task.Finished &&
            (!reservations.TryGetValue(task.Spec.station, out var current) || current == task);
        public bool Claim(WorkItem task, bool moving)
        {
            if (task == null || task.State != WorkState.Ready || !IsAvailable(task)) return false;
            reservations[task.Spec.station] = task;
            task.State = moving ? WorkState.Moving : WorkState.Working;
            return true;
        }
        public void Arrive(WorkItem task)
        { if (Contains(task) && task.State == WorkState.Moving) { task.State = WorkState.Working; task.Blocked = ""; } }
        public void Block(WorkItem task, string reason, double now)
        {
            if (!Contains(task) || task.Finished) return;
            task.Blocked = reason ?? "";
            // A blocked station is not evidence that the player ignored their task.
            if (!string.IsNullOrEmpty(reason)) task.ReminderAt = now;
        }
        public bool Advance(WorkItem task, float delta, double now)
        {
            if (!Contains(task) || task.State != WorkState.Working || delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return false;
            task.Progress = Math.Min(task.Spec.seconds, task.Progress + delta);
            task.Blocked = ""; task.ReminderAt = now;
            if (task.Progress < task.Spec.seconds) return false;
            task.State = WorkState.Done; Release(task); Refresh(now); Completed?.Invoke(task); return true;
        }
        public WorkItem Reminder(double now, double delay, double cooldown)
        {
            var t = Next(Worker.Player);
            if (t == null || (t.State != WorkState.Ready && t.State != WorkState.Working) || !string.IsNullOrEmpty(t.Blocked) || !IsAvailable(t)) return null;
            if (now - t.ReadyAt < delay || now - t.ReminderAt < cooldown) return null;
            t.ReminderAt = now; return t;
        }
        public void Cancel(int id)
        {
            var o = orders.FirstOrDefault(x => x.Id == id);
            if (o == null) return;
            o.Cancelled = true;
            foreach (var t in o.Tasks.Where(x => !x.Finished)) { t.State = WorkState.Cancelled; Release(t); }
        }
        public void Reset()
        { foreach (var o in orders.ToArray()) Cancel(o.Id); orders.Clear(); reservations.Clear(); }
        bool Contains(WorkItem task) => task != null && orders.Any(o => !o.Cancelled && o.Tasks.Contains(task));
        void Release(WorkItem task)
        { if (reservations.TryGetValue(task.Spec.station, out var t) && t == task) reservations.Remove(task.Spec.station); }
    }
}
