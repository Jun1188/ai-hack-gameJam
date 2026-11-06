using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CafeCoworker
{
    [DefaultExecutionOrder(-80)]
    public sealed class CoopSession : MonoBehaviour
    {
        public TextAsset tablesAsset;
        public CoopPlayer player;
        public BezierWorkMotor barista;
        public WorkStation[] stations;
        public BaristaDialogue dialogue;
        public GuestDialogue guests;
        public Transform cup;
        public Renderer cupContents;
        public WorkScheduler Scheduler { get; private set; }
        public BaristaTables Tables { get; private set; }
        public WorkItem StaffTask { get; private set; }
        public string Notice { get; private set; } = "입구의 주문 벨을 누르면 함께 일할 주문을 받을 수 있어요.";
        public bool Paused { get; set; }
        public float Clock { get; private set; }
        public bool CanTalk { get; private set; }
        public bool CanSeeStaff { get; private set; }
        public string BubbleText { get; private set; } = "";
        WorkItem reminderTask;
        float bubbleViewedSeconds;
        public bool CanWork { get; private set; }
        public string WorkHint { get; private set; } = "";
        public int Revision { get; private set; }
        float nextArrival = 8, hintUntil, nextHintAllowed;
        bool previouslyTalkable;
        public bool TalkDiscovery => CanTalk && Clock < hintUntil;
        public event Action Changed;
        Transform cupRest;
        Transform carryGrip;
        Transform carryHand;
        Vector3 initialStaff, initialPlayer;
        Quaternion initialPlayerRotation;
        void Awake()
        {
            Tables = JsonUtility.FromJson<BaristaTables>(tablesAsset.text);
            foreach (var recipe in Tables.recipes)
            {
                WorkScheduler.Validate(recipe);
                foreach(var t in recipe.tasks)
                    if (Station(t.station) == null) throw new InvalidOperationException("Missing station: " + t.station);
            }
            Scheduler = new WorkScheduler(); Scheduler.Completed += OnCompleted;
            dialogue.Initialize(Tables); dialogue.WorkContext = DescribeWork;
            initialStaff = barista.transform.position; initialPlayer = player.transform.position;
            initialPlayerRotation = player.transform.rotation;
            if (cup != null) { cupRest = new GameObject("Cup home").transform; cupRest.SetParent(transform); cupRest.position = cup.position; cup.gameObject.SetActive(false); }
            if(cupContents!=null)cupContents.enabled=false;
            carryGrip = new GameObject("Staff cup grip").transform;
            carryGrip.SetParent(barista.transform, false); carryGrip.localPosition = new Vector3(.22f, 1.05f, .34f);
            dialogue.Say(dialogue.Pick("greeting"));
        }
        void Update()
        {
            if (Paused) return;
            Clock += Time.deltaTime;
            if (Clock >= nextArrival && Scheduler.Orders.All(o => o.Done || o.Cancelled))
            { NewOrder(); nextArrival = float.PositiveInfinity; }
            Scheduler.Refresh(Clock);
            UpdateStaff();
            UpdatePlayerWork();
            UpdateTalk();
            if (!player.MenuOpen)
            {
                var reminder = Scheduler.Reminder(Clock, Mathf.Max(60,Tables.profile.reminderDelay), Mathf.Max(60,Tables.profile.reminderCooldown));
                if (reminder != null)
                {
                    BubbleText=dialogue.Pick("reminder").Replace("{task}",reminder.Spec.label).Replace("{station}",Station(reminder.Spec.station).displayName);
                    reminderTask=reminder;bubbleViewedSeconds=0;dialogue.Say(BubbleText);
                }
            }
            if(reminderTask!=null&&(reminderTask.Finished||Scheduler.Next(Worker.Player)!=reminderTask))
            {BubbleText="";reminderTask=null;}
            if(CanSeeStaff&&!player.MenuOpen&&!string.IsNullOrEmpty(BubbleText))
            {bubbleViewedSeconds+=Time.deltaTime;if(bubbleViewedSeconds>=10)BubbleText="";}
        }
        void UpdateStaff()
        {
            if (StaffTask != null && StaffTask.Finished) { StaffTask = null; barista.Stop(); }
            if (StaffTask == null)
            {
                var next = Scheduler.Next(Worker.Barista);
                if (next != null && Scheduler.Claim(next, true))
                {
                    StaffTask = next; var station = Station(next.Spec.station);
                    barista.SetDestination(station.Position, station.Facing);
                    Notice = Tables.profile.displayName + " → " + station.displayName + " · " + next.Spec.label;
                    Touch();
                }
            }
            if (StaffTask == null) return;
            if (!string.IsNullOrEmpty(barista.Status) && !barista.Arrived) Scheduler.Block(StaffTask, barista.Status, Clock);
            if (StaffTask.State == WorkState.Moving && barista.Arrived)
            { Scheduler.Arrive(StaffTask); Touch(); }
            if (StaffTask.State == WorkState.Working)
            {
                var station = Station(StaffTask.Spec.station);
                // If a player crowds the station, stop work rather than executing through their body.
                var d = player.transform.position - barista.transform.position; d.y = 0;
                if (d.magnitude < .75f) { Scheduler.Block(StaffTask, "작업할 공간이 필요해요", Clock); return; }
                Scheduler.Block(StaffTask, "", Clock);
                if (cup != null && StaffTask.Spec.id == "brew")
                { cup.gameObject.SetActive(true); cup.SetParent(station.workSurface, false); cup.localPosition = Vector3.zero; }
                if (cup != null && StaffTask.Spec.id == "deliver")
                { cup.SetParent(station.workSurface, false); cup.localPosition = Vector3.zero; }
                Scheduler.Advance(StaffTask, Time.deltaTime, Clock);
            }
        }
        void UpdatePlayerWork()
        {
            CanWork = false; WorkHint = "";
            var task = Scheduler.Next(Worker.Player);
            if (task == null) return;
            var station = Station(task.Spec.station);
            Vector3 surface = station.workSurface.position;
            Vector3 from = player.eyes.transform.position;
            Vector3 delta = surface - from;
            var floorDelta = player.transform.position - station.Position; floorDelta.y = 0;
            bool at = floorDelta.magnitude < 1.5f && Vector3.Dot(player.eyes.transform.forward, delta.normalized) > .75f &&
                !Physics.Linecast(from, surface - delta.normalized * .12f, out var hit, ~0, QueryTriggerInteraction.Ignore);
            // A counter surface at the ray endpoint is allowed.
            if (!at && floorDelta.magnitude < 1.5f && Vector3.Dot(player.eyes.transform.forward, delta.normalized) > .75f)
                at = Physics.Raycast(from, delta.normalized, out hit, delta.magnitude + .15f, ~0, QueryTriggerInteraction.Ignore) &&
                     (hit.transform == station.transform || hit.transform.IsChildOf(station.transform));
            if (!Scheduler.IsAvailable(task)) { Scheduler.Block(task, "동료가 같은 작업대를 사용 중이에요", Clock); return; }
            Scheduler.Block(task, "", Clock);
            CanWork = at && !player.MenuOpen;
            WorkHint = "F 누르기 유지 · " + station.displayName + "에서 " + task.Spec.label;
            if (!Application.isFocused||!CanWork || Keyboard.current?.fKey.isPressed != true) return;
            WorkAtAimedStation(Time.deltaTime);
        }
        public bool WorkAtAimedStation(float seconds)
        {
            var task=Scheduler.Next(Worker.Player);
            if(!CanWork||task==null||player.MenuOpen)return false;
            if (task.State == WorkState.Ready) { Scheduler.Claim(task, false); Touch(); }
            return Scheduler.Advance(task,seconds,Clock);
        }
        void UpdateTalk()
        {
            Vector3 target = barista.transform.position + Vector3.up * 1.25f;
            Vector3 delta = target - player.eyes.transform.position;
            Vector3 feet = barista.transform.position - player.transform.position; feet.y = 0;
            float dot = Vector3.Dot(player.eyes.transform.forward, delta.normalized);
            float threshold = previouslyTalkable ? Tables.profile.lookDot - .04f : Tables.profile.lookDot;
            bool visible = !Physics.Raycast(player.eyes.transform.position, delta.normalized, out var hit,
                delta.magnitude, ~0, QueryTriggerInteraction.Ignore) || hit.transform.IsChildOf(barista.transform);
            Vector3 viewport=player.eyes.WorldToViewportPoint(target);
            CanSeeStaff=visible&&dot>.88f&&viewport.z>0&&viewport.x>0&&viewport.x<1&&viewport.y>0&&viewport.y<1;
            CanTalk = feet.magnitude < Tables.profile.talkDistance + (previouslyTalkable ? .25f : 0) &&
                dot >= threshold && visible;
            if (CanTalk && !previouslyTalkable && Clock >= nextHintAllowed)
            { hintUntil = Clock + 3; nextHintAllowed = Clock + 18; }
            previouslyTalkable = CanTalk;
        }
        public WorkStation Station(string id) => stations.FirstOrDefault(s => s.stationId == id);
        void LateUpdate()
        {
            if(carryGrip==null||barista.animator==null)return;
            if(carryHand==null)carryHand=barista.animator.GetBoneTransform(HumanBodyBones.RightHand);
            if(carryHand!=null)
            {
                carryGrip.position=carryHand.position+barista.transform.forward*.07f+Vector3.up*.08f;
                carryGrip.rotation=Quaternion.identity;
            }
        }
        public void NewOrder()
        {
            if (Scheduler.Orders.Any(o => !o.Done && !o.Cancelled)) { Notice = "진행 중인 주문을 먼저 마쳐 주세요."; Touch(); return; }
            int n = Scheduler.Orders.Count;
            var recipe = Tables.recipes[n % Tables.recipes.Length];
            var order = Scheduler.Add(recipe, Clock);
            guests.BeginOrder(recipe.name);
            if(cupContents!=null)cupContents.enabled=false;
            Notice = "주문 " + order.Id + " · " + recipe.name + " — 각자 맡은 일을 확인해 주세요.";
            dialogue.Say(dialogue.Pick("assignment")); Touch();
        }
        void OnCompleted(WorkItem item)
        {
            if (item.Spec.id == "cup" && cup != null) { cup.gameObject.SetActive(true); cup.SetParent(carryGrip, false); cup.localPosition = Vector3.zero; }
            if (item.Spec.id == "brew")
            {
                if (cupContents != null) cupContents.enabled = true;
                if (cup != null) { cup.SetParent(carryGrip, false); cup.localPosition = Vector3.zero; }
                dialogue.Say(dialogue.Pick("ready"));
            }
            var order = Scheduler.Orders.First(x => x.Id == item.OrderId);
            if (order.Done)
            {
                guests.Finish(false);
                dialogue.Say(dialogue.Pick("complete"));
                Notice = order.Recipe.name + " 서빙 완료 · 다음 주문은 벨로 받을 수 있어요.";
                nextArrival = Clock + 18;
                if (cup != null) cup.gameObject.SetActive(false);
            }
            Touch();
        }
        public void CancelOrder()
        {
            var order = Scheduler.Orders.FirstOrDefault(o => !o.Done && !o.Cancelled);
            if (order == null) return;
            Scheduler.Cancel(order.Id); StaffTask = null; barista.Stop(); dialogue.Cancel();
            BubbleText="";reminderTask=null;
            if (cup != null) cup.gameObject.SetActive(false);
            guests.Finish(true);
            Notice = "주문을 취소했어요. 다음 주문은 벨로 받을 수 있어요."; nextArrival = Clock + 12; Touch();
        }
        public void ResetDay()
        {
            CancelOrder(); Scheduler.Reset(); guests.ResetRecords(); dialogue.ResetConversation();
            Clock = 0; nextArrival = 8; previouslyTalkable = false; nextHintAllowed = 0;
            BubbleText="";reminderTask=null;
            barista.Stop(); var cc = barista.GetComponent<CharacterController>(); cc.enabled = false;
            barista.transform.position = initialStaff; cc.enabled = true;
            var pc = player.GetComponent<CharacterController>(); pc.enabled = false;
            player.transform.SetPositionAndRotation(initialPlayer, initialPlayerRotation); pc.enabled = true;
            Notice = "새로 시작했어요. 잠시 후 첫 주문이 들어와요.";
            dialogue.Say(dialogue.Pick("greeting")); Touch();
        }
        public string DescribeWork()
        {
            var p = Scheduler.Next(Worker.Player); var b = Scheduler.Next(Worker.Barista);
            string Task(WorkItem t) => t == null ? "지금 맡은 작업 없음" :
                Station(t.Spec.station).displayName + "에서 " + t.Spec.label +
                (t.State == WorkState.Working ? " 진행 중" : t.State == WorkState.Moving ? "하러 이동 중" : " 대기 중") +
                (string.IsNullOrEmpty(t.Blocked) ? "" : " · " + t.Blocked);
            return "플레이어: " + Task(p) + "\n" + Tables.profile.displayName + ": " + Task(b);
        }
        public void Touch() { Revision++; Changed?.Invoke(); }
        void OnDestroy() { if (Scheduler != null) Scheduler.Completed -= OnCompleted; }
    }
}
