using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Stage 1 "곳간" (tutorial). The player (grandfather) talks to the child robot; the robot turns words into
// actions and this class judges every action against slot rules and step prerequisites.
// Split across partial files: commands (fast path / LLM / fallback), events (A–D), HUD.
public sealed partial class BarnStage : MonoBehaviour
{
    private static readonly string[] StepTasks =
    {
        "", "밀려난 기초 블록 2개를 제자리로 밀기", "바닥 틀을 기초 블록 위에 올리기", "장선 4개 깔기", "바닥판 덮기",
        "벽 4장 세우기 (긴 벽 → 짧은 벽)", "지붕 올리기", "지붕 못 박기", "문짝 달기"
    };
    private static readonly string[] StepLines =
    {
        "",
        "할아버지   기억 속 곳간을 떠올려 보자. 돌이 비어 있는 자리가 두 군데 보이지?",
        "할아버지   돌 여섯 개 위에 바닥 틀을 반듯하게 올려야 한다.",
        "할아버지   장선은 두 뼘 간격으로 고르게 놓는 게야.",
        "할아버지   이제 장선 위를 바닥판으로 덮자.",
        "할아버지   창은 바람 드는 쪽, 문은 마당 쪽이다. 긴 벽부터 세우거라.",
        "할아버지   벽이 다 섰으니 지붕을 얹자꾸나.",
        "할아버지   내가 판재를 잡고 있으마. 못을 박아라. 너무 세게는 말고.",
        "할아버지   마지막으로 마당 쪽에 문짝만 달면 된다."
    };

    [SerializeField] private Camera view;
    [SerializeField] private Material[] wood;
    [SerializeField] private Material stone, roof, metal, outline;
    [SerializeField] private Material robotShell, robotTrim, robotGlow;
    [SerializeField] private Light completionLamp;
    [SerializeField] private Transform grandfather;
    [SerializeField] private Vector3 robotStart = new(-1.3f, 0f, -3.5f);
    [SerializeField] private HiddenItemInfo hiddenItem = new();
    [SerializeField] private BarnClaudeClient claude = new();

    private readonly List<BarnItem> items = new();
    private readonly Dictionary<string, BarnItem> itemById = new();
    private readonly List<BarnSlot> slots = new();
    private readonly Dictionary<string, BarnSlot> slotById = new();
    private readonly Dictionary<Collider, BarnItem> itemByCollider = new();
    private readonly Dictionary<BarnSlot, Bounds> slotBounds = new();
    private readonly Queue<RobotAction> queue = new();
    private readonly List<BarnItem> wallOrder = new();

    private Transform assemblyRoot, looseRoot, recallRoot;
    private BarnRobot robot;
    private Behaviour grandfatherMovement;
    private Coroutine worker;
    private BarnItem pointed, pendingItem;
    private BarnSlot pendingSlot;
    private bool recallVisible = true, completed;

    public int Step { get; private set; } = 1;
    public bool Complete => completed;
    public BarnRobot Robot => robot;

    private void Start()
    {
        assemblyRoot = StorehouseParts.Group(transform, "Barn assembly", new Vector3(.92f, 0f, .12f));
        looseRoot = StorehouseParts.Group(transform, "Barn loose parts", Vector3.zero);
        recallRoot = StorehouseParts.Group(transform, "Recall overlay (grandfather's memory)", Vector3.zero);

        var builder = new BarnBuilder(assemblyRoot, looseRoot, wood, stone, roof, metal);
        builder.Build();
        items.AddRange(builder.Items);
        slots.AddRange(builder.Slots);
        foreach (BarnItem item in items)
            itemById[item.Id] = item;
        foreach (BarnSlot slot in slots)
            slotById[slot.Id] = slot;
        AssignPrerequisites();

        foreach (BarnItem item in items)
        {
            AddPointerCollider(item);
            if (item.Kind == BarnKind.Nail)
                continue;
            if (item.Installed)
                PlaceModel(item, item.Slot);
            else
                ApplyLoosePose(item);
            if (item.Hidden)
                SetVisible(item, false);
        }
        foreach (BarnItem nail in Nails())
            nail.Model.gameObject.SetActive(false);
        BuildRecallOverlay();
        CreateFloorColliders();

        robot = BarnRobot.Create(transform, robotStart, robotShell != null ? robotShell : metal,
            robotTrim != null ? robotTrim : metal, robotGlow != null ? robotGlow : roof);
        if (grandfather == null)
            grandfather = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (grandfather != null)
        {
            grandfatherMovement = grandfather.GetComponent<GrandfatherMovement>();
            robot.transform.rotation = Quaternion.LookRotation(Flat(grandfather.position - robot.transform.position));
        }
        if (view == null)
            view = Camera.main;
        if (completionLamp != null)
            completionLamp.enabled = false;

        Log("할아버지", "태풍에 곳간이 다 무너졌구나… 얘야, 우리 같이 다시 지어 보자.");
        RobotSay("네, 할아버지! 뭐부터 할까요? 말로 시켜 주세요!");
        if (!claude.Available)
            Log("시스템", $"Claude API 키가 없어 데모용 키워드 파서로 동작합니다. (ANTHROPIC_API_KEY 또는 {BarnClaudeClient.KeyFilePath})");
        RefreshStep();
    }

    private void AssignPrerequisites()
    {
        foreach (BarnSlot slot in slots)
        {
            slot.Prerequisite = slot.Step switch
            {
                2 => () => AllFilled(1) ? null : "기초 블록이 아직 다 제자리가 아니에요. 그것부터 해야 해요!",
                3 => () => SlotFilled("slot_floor_frame") ? null : "바닥 틀이 먼저 올라가야 장선을 깔 수 있어요.",
                4 => () => !AllFilled(3) ? "장선이 아직 다 안 깔렸어요."
                    : !hiddenDone ? "잠깐요! 장선 사이에 뭐가 있어요. 그거 먼저 봐야 해요." : null,
                5 => WallPrerequisite(slot),
                6 => () => AllFilled(5) ? null : "벽 네 장이 다 서야 지붕을 올릴 수 있어요.",
                8 => () => AllFilled(5) ? null : "벽이 다 서야 문짝을 달 수 있어요.",
                _ => () => null
            };
        }
    }

    private System.Func<string> WallPrerequisite(BarnSlot slot)
    {
        bool shortWall = slot.Id == "slot_wall_west" || slot.Id == "slot_wall_east";
        return () =>
        {
            if (!SlotFilled("slot_floor_deck"))
                return "바닥판을 먼저 덮어야 벽을 세울 수 있어요.";
            if (shortWall && (!SlotFilled("slot_wall_back") || !SlotFilled("slot_wall_front")))
                return "할아버지가 긴 벽부터 세우라고 했어요!";
            return null;
        };
    }

    private void Update()
    {
        if (items.Count == 0 || StorehouseTransition.Busy)
            return;
        UpdateEvents(Time.deltaTime);
        if (worker == null && queue.Count > 0)
            worker = StartCoroutine(RunQueue());
        if (grandfatherMovement != null)
            grandfatherMovement.enabled = !chatFocused;

        Keyboard keys = Keyboard.current;
        if (keys == null || chatFocused)
            return;
        if (keys.hKey.wasPressedThisFrame)
            SetRecall(!recallVisible);
        if (keys.escapeKey.wasPressedThisFrame || (completed && keys.fKey.wasPressedThisFrame))
            StorehouseTransition.ReturnToCottage();
        UpdateVoiceKey(keys);
        UpdatePointer();
    }

    // ----- Pointing (mouse click stands in for grandfather's cane) -----

    private void UpdatePointer()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || view == null || !mouse.leftButton.wasPressedThisFrame)
            return;
        Vector2 position = mouse.position.ReadValue();
        if (PointerOverHud(new Vector2(position.x, Screen.height - position.y)))
            return;
        Ray ray = view.ScreenPointToRay(position);
        RaycastHit[] hits = Physics.RaycastAll(ray, 200f, ~0, QueryTriggerInteraction.Collide);
        float nearest = float.PositiveInfinity;
        foreach (RaycastHit hit in hits)
            if (itemByCollider.ContainsKey(hit.collider) && IsPointable(itemByCollider[hit.collider]))
                nearest = Mathf.Min(nearest, hit.distance);
        BarnItem best = null;
        float smallest = float.PositiveInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (!itemByCollider.TryGetValue(hit.collider, out BarnItem item) || !IsPointable(item) || hit.distance > nearest + 1.2f)
                continue;
            Vector3 size = hit.collider.bounds.size;
            float volume = size.x * size.y * size.z;
            if (volume < smallest)
            {
                smallest = volume;
                best = item;
            }
        }
        pointed = best;
    }

    private bool IsPointable(BarnItem item) => item.Model.gameObject.activeInHierarchy
        && (!item.Hidden || (item.Kind == BarnKind.Hidden && hiddenActive));

    private void AddPointerCollider(BarnItem item)
    {
        var bounds = new Bounds();
        bool first = true;
        Matrix4x4 toLocal = item.Model.worldToLocalMatrix;
        foreach (Renderer surface in item.Model.GetComponentsInChildren<Renderer>(true))
        {
            if (item.Kind == BarnKind.Roof && surface.transform.parent.name.Contains("| nail_"))
                continue;
            Matrix4x4 local = toLocal * surface.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = local.MultiplyPoint3x4(new Vector3((corner & 1) - .5f, ((corner >> 1) & 1) - .5f,
                    ((corner >> 2) & 1) - .5f));
                if (first) bounds = new Bounds(point, Vector3.zero);
                else bounds.Encapsulate(point);
                first = false;
            }
        }
        var box = item.Model.gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = bounds.center;
        box.size = Vector3.Max(bounds.size, Vector3.one * .12f);
        itemByCollider[box] = item;
    }

    private void CreateFloorColliders()
    {
        var floor = new GameObject("Walkable floor (stage collider)").transform;
        floor.SetParent(transform, false);
        void Block(Vector3 center, Vector3 size)
        {
            var box = floor.gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }
        Block(new Vector3(1.6f, -.1f, -3.5f), new Vector3(16f, .2f, 17.5f));
        Block(new Vector3(-6.4f, 1.5f, -3.5f), new Vector3(.5f, 3f, 17.5f));
        Block(new Vector3(1.6f, 1.5f, 5.4f), new Vector3(16f, 3f, .5f));
        Block(new Vector3(9.6f, 1.5f, -3.5f), new Vector3(.5f, 3f, 17.5f));
        Block(new Vector3(1.6f, 1.5f, -12.2f), new Vector3(16f, 3f, .5f));
        // Keep grandfather off the workbench and out of the barn footprint.
        Block(new Vector3(-3.55f, .5f, 0f), new Vector3(1.66f, 1f, 5.84f));
    }

    // ----- Recall overlay: the finished barn as grandfather remembers it -----

    private void BuildRecallOverlay()
    {
        foreach (BarnSlot slot in slots)
        {
            BarnItem model = items.Find(item => item.Kind != BarnKind.Prop && FitsIgnoringFacing(slot, item));
            if (model == null)
                continue;
            Transform ghost = Instantiate(model.Model, recallRoot);
            ghost.name = slot.Name + " | recall";
            ghost.SetPositionAndRotation(slot.Position, slot.Rotation);
            ghost.localScale = Vector3.one;
            foreach (Collider part in ghost.GetComponentsInChildren<Collider>(true))
                Destroy(part);
            foreach (Transform child in ghost.GetComponentsInChildren<Transform>(true))
                child.gameObject.SetActive(true);
            foreach (Renderer surface in ghost.GetComponentsInChildren<Renderer>(true))
            {
                surface.enabled = true;
                surface.sharedMaterial = outline;
                surface.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (Transform nail in ghost)
                if (nail.name.Contains("nail_"))
                    nail.gameObject.SetActive(false);
            slot.Recall = ghost;
            var renderers = ghost.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(slot.Position, Vector3.one * .4f);
            foreach (Renderer surface in renderers)
                bounds.Encapsulate(surface.bounds);
            slotBounds[slot] = bounds;
        }
        SetRecall(recallVisible);
    }

    private static bool FitsIgnoringFacing(BarnSlot slot, BarnItem item)
    {
        int turns = item.Turns;
        item.Turns = 0;
        bool fits = slot.Accepts(item) == null;
        item.Turns = turns;
        return fits;
    }

    private void SetRecall(bool visible)
    {
        recallVisible = visible;
        foreach (BarnSlot slot in slots)
            if (slot.Recall != null)
                slot.Recall.gameObject.SetActive(visible && slot.Filled == null && !completed);
    }

    // ----- Step bookkeeping -----

    private IEnumerable<BarnItem> Nails()
    {
        foreach (BarnItem item in items)
            if (item.Kind == BarnKind.Nail)
                yield return item;
    }

    private bool SlotFilled(string id) => slotById[id].Filled != null;

    private bool AllFilled(int step)
    {
        foreach (BarnSlot slot in slots)
            if (slot.Step == step && slot.Filled == null)
                return false;
        return true;
    }

    private void RefreshStep()
    {
        int step = 1;
        while (step <= 8 && StepDone(step))
            step++;
        if (step != Step)
        {
            Step = step;
            OnStepEntered(step);
        }
        Step = step;
        if (step > 8 && !completed)
            CompleteStage();
    }

    private bool StepDone(int step) => step switch
    {
        3 => AllFilled(3),
        4 => SlotFilled("slot_floor_deck"),
        7 => nailsDone >= 3,
        _ => AllFilled(step)
    };

    private void CompleteStage()
    {
        completed = true;
        SetRecall(false);
        if (completionLamp != null)
            completionLamp.enabled = true;
        Log("할아버지", "허허, 이제 곡식을 비바람에서 지킬 수 있겠구나. 고맙다, 얘야.");
        RobotSay("우와! 우리가 곳간을 다 지었어요!");
    }

    // ----- Robot action queue -----

    private sealed class Outcome
    {
        public bool Failed;
    }

    private IEnumerator RunQueue()
    {
        while (queue.Count > 0)
        {
            RobotAction action = queue.Dequeue();
            var outcome = new Outcome();
            yield return Perform(action, outcome);
            if (outcome.Failed)
            {
                queue.Clear();
                break;
            }
        }
        worker = null;
    }

    // "멈춰": abort whatever the robot is doing and leave every object in a consistent place.
    private void StopRobot(bool announce)
    {
        queue.Clear();
        if (worker != null)
        {
            StopCoroutine(worker);
            worker = null;
        }
        if (pendingItem != null && pendingSlot != null)
            Install(pendingItem, pendingSlot);
        pendingItem = null;
        pendingSlot = null;
        if (pushing != null)
        {
            pushing.LoosePosition = pushing.Model.position;
            pushing = null;
        }
        if (hammering)
            EndHammer();
        robot.SetBracing(false);
        holding = null;
        if (robot.transform.position.y > .01f)
            StartCoroutine(robot.Hop(Flat(robot.transform.position) + Vector3.back * .6f, .6f, .4f));
        if (announce)
            RobotSay("네! 멈췄어요!");
    }

    private IEnumerator Perform(RobotAction action, Outcome outcome)
    {
        switch (action.type)
        {
            case "say":
                RobotSay(action.text);
                yield return new WaitForSeconds(.5f);
                break;
            case "move_to":
                yield return DoMoveTo(action, outcome);
                break;
            case "push":
                yield return DoPush(action, outcome);
                break;
            case "pick_up":
                yield return DoPickUp(Find(action.target), outcome);
                break;
            case "place":
                yield return DoPlace(action, outcome);
                break;
            case "rotate":
                yield return DoRotate(action, outcome);
                break;
            case "hold":
                yield return DoHold(action, outcome);
                break;
            case "hammer":
                yield return DoHammer(action, outcome);
                break;
            default:
                Fail(outcome, "그건 제가 할 줄 모르는 일이에요.");
                break;
        }
    }

    private void Fail(Outcome outcome, string line)
    {
        outcome.Failed = true;
        RobotSay(line);
    }

    private BarnItem Find(string id) => !string.IsNullOrEmpty(id) && itemById.TryGetValue(id, out BarnItem item) ? item : null;

    private BarnSlot FindSlot(string id) => !string.IsNullOrEmpty(id) && slotById.TryGetValue(id, out BarnSlot slot) ? slot : null;

    private IEnumerator DoMoveTo(RobotAction action, Outcome outcome)
    {
        BarnItem item = Find(action.target);
        BarnSlot slot = FindSlot(action.target) ?? FindSlot(action.slot);
        if (action.target == "grandfather" && grandfather != null)
            yield return Walk(Approach(new Bounds(grandfather.position + Vector3.up * .8f, new Vector3(.6f, 1.6f, .6f))));
        else if (item != null && Visible(item))
            yield return Walk(Approach(item.WorldBounds()));
        else if (slot != null)
            yield return Walk(ApproachSlot(slot));
        else
            Fail(outcome, "어디로 가라는 건지 잘 모르겠어요.");
    }

    private IEnumerator DoPush(RobotAction action, Outcome outcome)
    {
        BarnItem block = Find(action.target);
        if (block == null || block.Kind != BarnKind.Block)
        {
            if (block != null && !block.Heavy)
            {
                yield return DoPlace(action, outcome);
                yield break;
            }
            Fail(outcome, "밀 수 있는 건 기초 블록뿐이에요.");
            yield break;
        }
        if (block.Installed)
        {
            Fail(outcome, $"{BarnText.Josa(block.Name, "은", "는")} 이미 제자리에 있어요.");
            yield break;
        }
        BarnSlot slot = FindSlot(action.slot);
        if (slot == null || slot.Filled != null || slot.Accepts(block) != null)
            slot = NearestOpenSlot(block, block.Model.position);
        if (slot == null)
        {
            Fail(outcome, "블록을 놓을 빈자리가 없어요.");
            yield break;
        }
        if (Busy(outcome))
            yield break;

        Vector3 from = block.Model.position;
        Vector3 direction = Flat(slot.Position - from).normalized;
        yield return Walk(Flat(from) - direction * .62f);
        yield return robot.FaceTowards(slot.Position);
        RobotSay("영차, 영차!");
        pushing = block;
        Vector3 start = block.Model.position;
        float distance = Vector3.Distance(Flat(start), Flat(slot.Position));
        float duration = Mathf.Max(.5f, distance / 1.1f);
        Quaternion startRotation = block.Model.rotation;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            block.Model.position = Vector3.Lerp(start, slot.Position, k);
            block.Model.rotation = Quaternion.Slerp(startRotation, slot.Rotation, k);
            robot.transform.position = Flat(block.Model.position) - direction * .62f;
            yield return null;
        }
        pushing = null;
        Install(block, slot);
        RobotSay("쿵! 제자리에 들어갔어요.");
    }

    private IEnumerator DoPickUp(BarnItem item, Outcome outcome)
    {
        if (item == null || !Visible(item))
        {
            Fail(outcome, "그게 어디 있는지 모르겠어요. 손으로 가리켜 줄래요?");
            yield break;
        }
        if (item.Kind == BarnKind.Nail)
        {
            Fail(outcome, "못은 집는 게 아니라 망치로 박는 거예요!");
            yield break;
        }
        if (item.Heavy)
        {
            Fail(outcome, "으으, 너무 무거워서 못 들어요. 밀어서 옮길 수는 있어요!");
            yield break;
        }
        if (item.Installed)
        {
            Fail(outcome, $"{BarnText.Josa(item.Name, "은", "는")} 벌써 끼워져 있어요.");
            yield break;
        }
        if (robot.Carrying == item)
            yield break;
        if (Busy(outcome))
            yield break;
        if (robot.Carrying != null)
            DropCarried();

        yield return Walk(Approach(item.WorldBounds()));
        yield return robot.FaceTowards(item.Model.position);
        if (item.Kind == BarnKind.Hidden)
            SetVisible(item, true);
        robot.PickUp(item);
        Vector3 start = item.Model.position;
        Vector3 scale = item.Model.localScale;
        Quaternion rotation = item.Model.rotation;
        Quaternion carried = Quaternion.LookRotation(robot.transform.forward) * TurnRotation(item);
        float targetScale = item.Kind == BarnKind.Hidden ? 1f : BarnRobot.CarryScale;
        for (float t = 0f; t < .3f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / .3f);
            item.Model.position = Vector3.Lerp(start, robot.CarryPosition, k);
            item.Model.localScale = Vector3.Lerp(scale, Vector3.one * targetScale, k);
            item.Model.rotation = Quaternion.Slerp(rotation, carried, k);
            yield return null;
        }
        item.Model.localPosition = Vector3.zero;
        item.Model.localScale = Vector3.one * targetScale;
        if (item.Kind == BarnKind.Hidden)
            yield return DeliverHiddenItem();
    }

    private IEnumerator DoPlace(RobotAction action, Outcome outcome)
    {
        BarnItem item = Find(action.target) ?? robot.Carrying;
        if (item == null)
        {
            Fail(outcome, "뭘 놓으라는 건지 모르겠어요.");
            yield break;
        }
        if (item.Installed)
        {
            Fail(outcome, $"{BarnText.Josa(item.Name, "은", "는")} 벌써 제자리에 있어요.");
            yield break;
        }
        if (item.Kind == BarnKind.Block)
        {
            yield return DoPush(action, outcome);
            yield break;
        }
        BarnSlot slot = FindSlot(action.slot);
        if (slot == null || slot.Filled != null)
            slot = BestSlotFor(item) ?? slot;
        if (slot == null)
        {
            Fail(outcome, $"{BarnText.Josa(item.Name, "을", "를")} 어디에 놓아야 할지 모르겠어요.");
            yield break;
        }
        string issue = slot.Filled != null ? $"{slot.Name}엔 벌써 {BarnText.Josa(slot.Filled.Name, "이", "가")} 있어요."
            : slot.Accepts(item) ?? slot.Prerequisite();
        if (issue != null)
        {
            Fail(outcome, issue);
            yield break;
        }
        if (Busy(outcome))
            yield break;

        if (robot.Carrying != item)
        {
            yield return DoPickUp(item, outcome);
            if (outcome.Failed)
                yield break;
        }
        yield return Walk(ApproachSlot(slot));
        yield return robot.FaceTowards(slot.Position);
        // Re-check: the rules may have changed while walking (e.g. a gust started).
        issue = slot.Filled != null ? $"{slot.Name}엔 벌써 다른 게 있어요." : slot.Accepts(item) ?? slot.Prerequisite();
        if (issue != null || Busy(outcome))
        {
            if (issue != null)
                Fail(outcome, issue);
            yield break;
        }

        robot.Release(looseRoot);
        pendingItem = item;
        pendingSlot = slot;
        Vector3 start = item.Model.position;
        Vector3 scale = item.Model.localScale;
        Quaternion rotation = item.Model.rotation;
        const float duration = .55f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            item.Model.position = Vector3.Lerp(start, slot.Position, k) + Vector3.up * (Mathf.Sin(Mathf.PI * k) * .6f);
            item.Model.localScale = Vector3.Lerp(scale, Vector3.one, k);
            item.Model.rotation = Quaternion.Slerp(rotation, slot.Rotation, k);
            yield return null;
        }
        pendingItem = null;
        pendingSlot = null;
        Install(item, slot);
        if (item.Kind == BarnKind.Prop)
            RobotSay($"{BarnText.Josa(item.Name, "으로", "로")} 장선 대신 끼웠어요! 튼튼해요!");
    }

    private IEnumerator DoRotate(RobotAction action, Outcome outcome)
    {
        BarnItem item = Find(action.target) ?? robot.Carrying ?? (pointed != null && pointed.Kind == BarnKind.Wall ? pointed : null);
        if (item == null)
        {
            Fail(outcome, "뭘 돌리라는 거예요?");
            yield break;
        }
        if (item.Installed)
        {
            Fail(outcome, "이미 세운 건 못 돌려요.");
            yield break;
        }
        if (Busy(outcome))
            yield break;
        if (robot.Carrying != item)
        {
            yield return Walk(Approach(item.WorldBounds()));
            yield return robot.FaceTowards(item.Model.position);
        }
        int delta = action.direction switch
        {
            "left" => -1, "right" => 1, "around" => 2,
            _ => item.TurnPeriod == 4 ? 2 : 1
        };
        Quaternion before = item.Model.rotation;
        item.Turns = ((item.Turns + delta) % 4 + 4) % 4;
        Quaternion after = robot.Carrying == item
            ? Quaternion.LookRotation(robot.transform.forward) * TurnRotation(item)
            : TurnRotation(item) * item.LooseRotation;
        for (float t = 0f; t < .4f; t += Time.deltaTime)
        {
            item.Model.rotation = Quaternion.Slerp(before, after, Mathf.SmoothStep(0f, 1f, t / .4f));
            yield return null;
        }
        item.Model.rotation = after;
        if (item.TurnPeriod > 1)
            RobotSay(item.FacingOk ? "빙글! 이제 방향이 맞는 것 같아요." : "빙글! 음… 아직 방향이 이상한 것 같아요?");
    }

    // ----- Placement helpers -----

    private bool Busy(Outcome outcome)
    {
        if (gustActive)
        {
            Fail(outcome, "지금은 벽이 흔들려서 다른 걸 할 수 없어요! 벽을 잡아야 해요!");
            return true;
        }
        if (hammering)
        {
            Fail(outcome, "지금 망치질 중이에요! 먼저 멈춰 달라고 해 주세요.");
            return true;
        }
        return false;
    }

    private BarnSlot BestSlotFor(BarnItem item)
    {
        // Best: fits and is open now. Then: right slot but the part needs turning (so the hint is about facing,
        // not about the wrong place). Then: right slot whose prerequisite is missing. Last: same family of slot.
        BarnSlot fitsLater = null, needsTurn = null, family = null;
        foreach (BarnSlot slot in slots)
        {
            if (slot.Filled != null)
                continue;
            string rule = slot.Accepts(item);
            if (rule == null && slot.Prerequisite() == null)
                return slot;
            if (rule == null)
                fitsLater ??= slot;
            else if (FitsIgnoringFacing(slot, item))
                needsTurn ??= slot;
            else if (SameFamily(slot, item))
                family ??= slot;
        }
        return needsTurn ?? fitsLater ?? family;
    }

    private static bool SameFamily(BarnSlot slot, BarnItem item) => item.Kind switch
    {
        BarnKind.Joist or BarnKind.Prop => slot.Step == 3,
        BarnKind.Wall => slot.Step == 5,
        _ => false
    };

    private BarnSlot NearestOpenSlot(BarnItem item, Vector3 from)
    {
        BarnSlot best = null;
        float nearest = float.PositiveInfinity;
        foreach (BarnSlot slot in slots)
        {
            if (slot.Filled != null || slot.Accepts(item) != null)
                continue;
            float distance = (slot.Position - from).sqrMagnitude;
            if (distance < nearest)
            {
                nearest = distance;
                best = slot;
            }
        }
        return best;
    }

    private void Install(BarnItem item, BarnSlot slot)
    {
        if (robot.Carrying == item)
            robot.Release(assemblyRoot);
        slot.Filled = item;
        if (pointed == item)
            pointed = null;
        item.Slot = slot;
        item.Turns = 0;
        PlaceModel(item, slot);
        if (slot.Recall != null)
            slot.Recall.gameObject.SetActive(false);
        OnInstalled(item, slot);
        RefreshStep();
    }

    private void Uninstall(BarnItem item)
    {
        if (item.Slot == null)
            return;
        item.Slot.Filled = null;
        if (item.Slot.Recall != null)
            item.Slot.Recall.gameObject.SetActive(recallVisible);
        item.Slot = null;
        RefreshStep();
    }

    private void PlaceModel(BarnItem item, BarnSlot slot)
    {
        item.Model.SetParent(assemblyRoot, true);
        item.Model.SetPositionAndRotation(slot.Position, slot.Rotation);
        item.Model.localScale = Vector3.one;
    }

    private void ApplyLoosePose(BarnItem item)
    {
        item.Model.SetParent(looseRoot, true);
        item.Model.SetPositionAndRotation(item.LoosePosition, TurnRotation(item) * item.LooseRotation);
        item.Model.localScale = Vector3.one;
    }

    private static Quaternion TurnRotation(BarnItem item)
        => item.TurnPeriod > 1 ? Quaternion.Euler(0f, item.Turns * 90f, 0f) : Quaternion.identity;

    private void DropCarried()
    {
        BarnItem item = robot.Release(looseRoot);
        if (item == null)
            return;
        // Items from the workbench end up on the floor; everything else keeps its resting height.
        item.LoosePosition = Flat(robot.transform.position + robot.transform.forward * .9f)
            + Vector3.up * (item.LoosePosition.y > .5f ? .06f : item.LoosePosition.y);
        ApplyLoosePose(item);
    }

    private bool Visible(BarnItem item) => item.Model.gameObject.activeInHierarchy
        && (!item.Hidden || (item.Kind == BarnKind.Hidden && hiddenActive));

    private static void SetVisible(BarnItem item, bool visible)
    {
        foreach (Renderer surface in item.Model.GetComponentsInChildren<Renderer>(true))
            surface.enabled = visible;
        item.Hidden = !visible;
    }

    private Bounds SlotBounds(BarnSlot slot)
        => slotBounds.TryGetValue(slot, out Bounds bounds) ? bounds : new Bounds(slot.Position, Vector3.one * .5f);

    // A standing point just outside the given bounds, on the side facing the robot.
    // Walls, roof and door sit on the barn itself, so stand outside the footprint rather than inside it.
    private Vector3 ApproachSlot(BarnSlot slot)
    {
        if (slot.Step < 5)
            return Approach(SlotBounds(slot));
        Vector3 center = Flat(assemblyRoot.position);
        Vector3 outward = Flat(slot.Position) - center;
        if (outward.sqrMagnitude < .04f)
            outward = Flat(robot.transform.position) - center;
        if (outward.sqrMagnitude < .04f)
            outward = Vector3.back;
        outward.Normalize();
        var footprint = new Bounds(center, new Vector3(4.3f, 1f, 3.2f));
        footprint.IntersectRay(new Ray(center + outward * 10f, -outward), out float entry);
        Vector3 spot = center + outward * (10f - entry + .6f);
        return new Vector3(Mathf.Clamp(spot.x, -5.7f, 9.2f), 0f, Mathf.Clamp(spot.z, -11.8f, 4.8f));
    }

    private Vector3 Approach(Bounds bounds)
    {
        Vector3 from = robot.transform.position;
        Vector3 closest = bounds.ClosestPoint(new Vector3(from.x, bounds.center.y, from.z));
        Vector3 away = Flat(from - closest);
        if (away.sqrMagnitude < .0004f)
            away = Flat(from - bounds.center);
        if (away.sqrMagnitude < .0004f)
            away = Vector3.back;
        Vector3 spot = Flat(closest) + away.normalized * .5f;
        return new Vector3(Mathf.Clamp(spot.x, -5.7f, 9.2f), 0f, Mathf.Clamp(spot.z, -11.8f, 4.8f));
    }

    // The workbench is the one solid obstacle the robot crosses often; walk around its ends instead of through it.
    private static readonly Bounds Workbench = new(new Vector3(-3.55f, .5f, 0f), new Vector3(2.3f, 1f, 6.5f));

    private IEnumerator Walk(Vector3 destination)
    {
        Vector3 from = Flat(robot.transform.position) + Vector3.up * .5f;
        Vector3 to = Flat(destination) + Vector3.up * .5f;
        Vector3 path = to - from;
        if (path.sqrMagnitude > .01f && !Workbench.Contains(from) && !Workbench.Contains(to)
            && Workbench.IntersectRay(new Ray(from, path.normalized), out float hit) && hit < path.magnitude)
        {
            float north = Workbench.max.z + .3f;
            float south = Workbench.min.z - .3f;
            float end = Mathf.Abs(from.z - north) + Mathf.Abs(to.z - north) < Mathf.Abs(from.z - south) + Mathf.Abs(to.z - south)
                ? north : south;
            yield return robot.MoveTo(new Vector3(from.x, 0f, end));
            yield return robot.MoveTo(new Vector3(to.x, 0f, end));
        }
        yield return robot.MoveTo(destination);
    }

    private static Vector3 Flat(Vector3 value) => new(value.x, 0f, value.z);
}
