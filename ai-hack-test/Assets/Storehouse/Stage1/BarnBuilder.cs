using System.Collections.Generic;
using UnityEngine;

// Builds every interactive object of the Stage 1 barn from primitives:
// the barn parts (8 assembly steps), the event props (substitute poles, hidden item) and their slots.
internal sealed class BarnBuilder
{
    internal readonly List<BarnItem> Items = new();
    internal readonly List<BarnSlot> Slots = new();

    private readonly Transform root;     // assembly origin; slot positions are local to it
    private readonly Transform looseRoot;
    private readonly Material[] wood;
    private readonly Material stone, roof, metal;

    internal BarnBuilder(Transform root, Transform looseRoot, Material[] wood, Material stone, Material roof, Material metal)
    {
        this.root = root;
        this.looseRoot = looseRoot;
        this.wood = wood;
        this.stone = stone;
        this.roof = roof;
        this.metal = metal;
    }

    internal void Build()
    {
        BuildBlocks();
        BuildFloor();
        BuildWalls();
        BuildRoof();
        BuildDoor();
        BuildProps();
    }

    // Step 1: six footing stones, two pushed out of place by the storm.
    private void BuildBlocks()
    {
        for (int row = 0; row < 3; row++)
        for (int side = 0; side < 2; side++)
        {
            int index = row * 2 + side;
            var local = new Vector3(side == 0 ? -1.59f : 1.59f, .15f, (row - 1) * 1.1f);
            BarnSlot slot = Slot($"slot_block_{index + 1}", $"기초 블록 자리 {index + 1}", 1, local);
            BarnItem block = Item($"block_{index + 1}", $"기초 블록 {index + 1}", BarnKind.Block, "stone", "heavy");
            block.Heavy = true;
            Box(block.Model, "Stone footing", Vector3.zero, new Vector3(.55f, .3f, .49f), stone);
            Box(block.Model, "Chipped stone cap", Vector3.up * .124f, new Vector3(.48f, .065f, .44f), stone);
            slot.Accepts = item => item.Kind == BarnKind.Block ? null : "여긴 기초 블록 자리예요.";
            bool displaced = index == 0 || index == 5;
            Vector3 offset = index == 0 ? new Vector3(.4f, 0f, -1.36f) : new Vector3(1.35f, 0f, -.27f);
            block.LoosePosition = slot.Position + offset;
            block.LooseRotation = Quaternion.Euler(0f, index == 0 ? 17f : -11f, 0f);
            if (!displaced)
                Fill(slot, block);
        }
    }

    private void BuildFloor()
    {
        // Step 2: the floor frame.
        BarnSlot frameSlot = Slot("slot_floor_frame", "바닥 틀 자리(기초 블록 위)", 2, new Vector3(0f, .38f, 0f));
        BarnItem frame = Item("floor_frame", "바닥 틀", BarnKind.FloorFrame, "wood", "big");
        for (int sign = -1; sign <= 1; sign += 2)
        {
            Box(frame.Model, "Long sill", new Vector3(0, 0, sign * 1.35f), new Vector3(3.8f, .17f, .15f), wood[1]);
            Box(frame.Model, "End sill", new Vector3(sign * 1.83f, 0, 0), new Vector3(.14f, .17f, 2.58f), wood[1]);
        }
        frameSlot.Accepts = item => item.Kind == BarnKind.FloorFrame ? null : "여긴 바닥 틀이 들어갈 자리예요.";
        frame.LoosePosition = new Vector3(6.75f, .09f, 1.25f);

        // Step 3: four joists, one of them snapped (event D).
        for (int i = 0; i < 4; i++)
        {
            BarnSlot slot = Slot($"slot_joist_{i + 1}", $"장선 자리 {i + 1}", 3, new Vector3((i - 1.5f) * .6096f, .39f, 0f));
            slot.Accepts = JoistRule;
            bool broken = i == 2;
            BarnItem joist = Item($"joist_{i + 1}", broken ? $"장선 {i + 1}(부러짐)" : $"장선 {i + 1}", BarnKind.Joist,
                broken ? new[] { "wood", "long", "broken", "fragile" } : new[] { "wood", "long", "sturdy", "straight" });
            if (broken)
            {
                Transform halfA = StorehouseParts.Group(joist.Model, "Snapped half", new Vector3(0f, 0f, -.66f));
                halfA.localRotation = Quaternion.Euler(-7f, 4f, 0f);
                Box(halfA, "Floor joist", Vector3.zero, new Vector3(.11f, .15f, 1.24f), wood[0]);
                Transform halfB = StorehouseParts.Group(joist.Model, "Snapped half", new Vector3(.03f, 0f, .67f));
                halfB.localRotation = Quaternion.Euler(8f, -6f, 0f);
                Box(halfB, "Floor joist", Vector3.zero, new Vector3(.11f, .15f, 1.22f), wood[0]);
                Box(joist.Model, "Splinters", new Vector3(.02f, .02f, 0f), new Vector3(.08f, .05f, .14f), wood[2]);
                joist.Note = "가운데가 부러져서 장선으로 못 씀";
            }
            else
                Box(joist.Model, "Floor joist", Vector3.zero, new Vector3(.11f, .15f, 2.57f), wood[0]);
            // Laid out along the workbench top.
            joist.LoosePosition = new Vector3(-4.1f + i * .26f, 1.04f, 0f);
        }

        // Step 4: the floor deck.
        BarnSlot deckSlot = Slot("slot_floor_deck", "바닥판 자리(장선 위)", 4, new Vector3(0f, .5f, 0f));
        BarnItem deck = Item("floor_deck", "바닥판", BarnKind.FloorDeck, "wood", "big", "flat");
        for (int i = 0; i < 13; i++)
            Box(deck.Model, "Floorboard", new Vector3(0, 0, (i - 6) * .216f), new Vector3(3.83f, .09f, .213f), wood[i % 3]);
        deckSlot.Accepts = item => item.Kind == BarnKind.FloorDeck ? null : "여긴 바닥판을 덮는 자리예요.";
        deck.LoosePosition = new Vector3(6.75f, .05f, -2.05f);
    }

    private static string JoistRule(BarnItem item)
    {
        if (item.Has("broken"))
            return "이건 부러져서 장선으로 못 써요… 다른 걸 찾아봐야 할 것 같아요.";
        if (item.Kind == BarnKind.Joist)
            return null;
        // Event D: a substitute needs to be long, sturdy and straight. The verdict is made here, not by the LLM.
        if (item.Has("long") && item.Has("sturdy") && item.Has("straight"))
            return null;
        if (!item.Has("long"))
            return $"{BarnText.Josa(item.Name, "은", "는")} 너무 짧아서 양쪽 틀에 안 닿아요.";
        if (!item.Has("sturdy"))
            return $"{BarnText.Josa(item.Name, "은", "는")} 너무 약해요. 밟으면 부러질 거예요.";
        return $"{BarnText.Josa(item.Name, "은", "는")} 곧지가 않아서 바닥이 울퉁불퉁해질 거예요.";
    }

    // Step 5: four framed wall panels (studs and top plate already joined).
    private void BuildWalls()
    {
        Wall("wall_back", "긴 벽(창 없음)", "slot_wall_back", "뒤쪽 긴 벽 자리", new Vector3(0f, 1.53f, 1.34f), 3.8f, 0,
            false, false, new Vector3(-.3f, .07f, -5.05f), true);
        Wall("wall_window", "창문 달린 긴 벽", "slot_wall_front", "앞쪽 긴 벽 자리(바람 드는 쪽)", new Vector3(0f, 1.53f, -1.34f),
            3.8f, 180, true, false, new Vector3(3.95f, .07f, -5.05f), true);
        Wall("wall_side", "짧은 벽(문 없음)", "slot_wall_west", "왼쪽 짧은 벽 자리(작업대 쪽)", new Vector3(-1.82f, 1.53f, 0f),
            2.57f, 90, false, false, new Vector3(7.6f, .07f, -5.05f), false);
        Wall("wall_door", "문 구멍 있는 짧은 벽", "slot_wall_east", "오른쪽 짧은 벽 자리(마당 쪽)", new Vector3(1.82f, 1.53f, 0f),
            2.57f, -90, false, true, new Vector3(-4.35f, .07f, -5.05f), false);
    }

    private void Wall(string id, string name, string slotId, string slotName, Vector3 local, float length, float yaw,
        bool window, bool door, Vector3 loose, bool longWall)
    {
        BarnSlot slot = Slot(slotId, slotName, 5, local);
        var tags = new List<string> { "wood", "wall", longWall ? "long_wall" : "short_wall" };
        if (window) tags.Add("window");
        if (door) tags.Add("door_opening");
        BarnItem panel = Item(id, name, BarnKind.Wall, tags.ToArray());
        bool facing = window || door;
        panel.TurnPeriod = facing ? 4 : 2;
        panel.Turns = facing ? 2 : 0;   // the facing walls lie the wrong way round and need turning

        Transform frame = StorehouseParts.Group(panel.Model, "Framed wall", Vector3.zero);
        frame.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Box(frame, "Top stud plate", Vector3.up * .97f, new Vector3(length, .105f, .14f), wood[1]);
        if (door)
        {
            float sillWidth = (length - .97f) / 2f;
            foreach (int sign in new[] { -1, 1 })
                Box(frame, "Split door sill", new Vector3(sign * (.485f + sillWidth / 2f), -.94f, 0f),
                    new Vector3(sillWidth, .09f, .13f), wood[1]);
        }
        else
            Box(frame, "Bottom stud plate", Vector3.down * .94f, new Vector3(length, .09f, .13f), wood[1]);
        int bays = length > 3f ? 6 : 4;
        for (int i = 0; i <= bays; i++)
        {
            float x = Mathf.Lerp(-length / 2f + .07f, length / 2f - .07f, (float)i / bays);
            if (facing && i == bays / 2)
                continue;
            Box(frame, "Wall stud", new Vector3(x, .012f, 0f), new Vector3(.105f, 1.83f, .13f), wood[i % 3]);
        }
        if (window)
        {
            Box(frame, "Window header", new Vector3(0f, .65f, 0f), new Vector3(1.23f, .13f, .21f), wood[1]);
            Box(frame, "Window sill", new Vector3(0f, -.10f, 0f), new Vector3(1.23f, .12f, .23f), wood[1]);
            Box(frame, "Cripple stud", new Vector3(0f, -.5f, 0f), new Vector3(.095f, .74f, .13f), wood[0]);
            // Shutter on the outer face so the facing reads at a glance.
            Box(frame, "Window shutter", new Vector3(0f, .27f, -.12f), new Vector3(1.1f, .66f, .04f), wood[2]);
        }
        if (door)
            Box(frame, "Door header", new Vector3(0f, .64f, 0f), new Vector3(1.23f, .16f, .17f), wood[1]);

        // Lying flat on the floor: rotate the panel so it rests on its face, length along world X.
        panel.LoosePosition = loose;
        panel.LooseRotation = Quaternion.AngleAxis(90f, Vector3.right) * Quaternion.Euler(0f, -yaw, 0f);

        slot.Accepts = item =>
        {
            if (item.Kind != BarnKind.Wall)
                return "여긴 벽을 세우는 자리예요.";
            if (slotId == "slot_wall_front" && !item.Has("window"))
                return item.Has("long_wall") ? "할아버지가 창은 바람 드는 쪽이라고 했어요. 여긴 창문 벽 자리예요!" : "여긴 긴 벽 자리예요.";
            if (slotId == "slot_wall_back" && (item.Has("window") || !item.Has("long_wall")))
                return item.Has("window") ? "창은 바람 드는 앞쪽에 달아야 해요. 여기 말고요!" : "여긴 긴 벽 자리예요.";
            if (slotId == "slot_wall_east" && !item.Has("door_opening"))
                return item.Has("short_wall") ? "문은 마당 쪽이라고 했어요. 여긴 문 구멍 있는 벽 자리예요!" : "여긴 짧은 벽 자리예요.";
            if (slotId == "slot_wall_west" && (item.Has("door_opening") || !item.Has("short_wall")))
                return item.Has("door_opening") ? "문은 마당 쪽(오른쪽)이어야 해요." : "여긴 짧은 벽 자리예요.";
            if (!item.FacingOk)
                return item.Has("window") ? "창이 안쪽을 보고 있어요. 벽을 돌려야 할 것 같아요!"
                    : "문 구멍이 거꾸로예요. 벽을 돌려야 할 것 같아요!";
            return null;
        };
    }

    // Step 6 + 7: the roof as one chunk (top plates, rafters, roof boards) and three nail points on it.
    private void BuildRoof()
    {
        var roofLocal = new Vector3(0f, 2.6f, 0f);
        BarnSlot slot = Slot("slot_roof", "지붕 자리(벽 위)", 6, roofLocal);
        BarnItem chunk = Item("roof", "지붕(서까래+지붕판)", BarnKind.Roof, "wood", "big");
        foreach (int sign in new[] { -1, 1 })
        {
            Box(chunk.Model, "Long top plate", new Vector3(0f, -.02f, sign * 1.35f), new Vector3(3.87f, .075f, .15f), wood[1]);
            Box(chunk.Model, "Cross cap", new Vector3(sign * 1.82f, .06f, 0f), new Vector3(.16f, .075f, 2.83f), wood[1]);
        }
        for (int i = 0; i < 5; i++)
        {
            Transform truss = StorehouseParts.Group(chunk.Model, "Rafter pair", new Vector3((i - 2) * .9f, .12f, 0f));
            StorehouseParts.Beam(truss, "Rafter", new Vector3(0f, 0f, -1.52f), new Vector3(0f, .82f, 0f), .115f, wood[0]);
            StorehouseParts.Beam(truss, "Rafter", new Vector3(0f, .82f, 0f), new Vector3(0f, 0f, 1.52f), .115f, wood[0]);
            Box(truss, "Collar tie", new Vector3(0f, .04f, 0f), new Vector3(.105f, .1f, 2.67f), wood[1]);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            Transform slope = StorehouseParts.Group(chunk.Model, "Roof slope", new Vector3(0f, .57f, side * .78f));
            slope.localRotation = Quaternion.Euler(side * 28.4f, 0f, 0f);
            Box(slope, "Roof deck", Vector3.zero, new Vector3(4.16f, .09f, 1.78f), roof);
            for (int i = -5; i <= 5; i++)
                Box(slope, "Raised seam", new Vector3(i * .373f, .053f, 0f), new Vector3(.023f, .03f, 1.77f), wood[1]);
        }
        slot.Accepts = item => item.Kind == BarnKind.Roof ? null : "여긴 지붕을 올리는 자리예요.";
        chunk.LoosePosition = new Vector3(-1.2f, .1f, -8.5f);

        for (int i = 0; i < 3; i++)
        {
            BarnItem nail = Item($"nail_{i + 1}", $"못 자리 {i + 1}", BarnKind.Nail, "nail");
            nail.Model.SetParent(chunk.Model, false);
            // Points sit on the front slope, where grandfather holds the board.
            nail.Model.localPosition = new Vector3((i - 1) * 1.25f, .66f, -.72f);
            nail.Model.localRotation = Quaternion.Euler(-28.4f, 0f, 0f);
            Box(nail.Model, "Nail head", new Vector3(0f, .2f, 0f), new Vector3(.07f, .02f, .07f), metal);
            Box(nail.Model, "Nail shank", new Vector3(0f, .1f, 0f), new Vector3(.025f, .2f, .025f), metal);
            nail.Hidden = true;
        }
    }

    // Step 8: the door leaf hangs in the yard-side wall.
    private void BuildDoor()
    {
        BarnSlot slot = Slot("slot_door", "문짝 자리(마당 쪽 벽)", 8, new Vector3(1.86f, 1.36f, 0f));
        BarnItem door = Item("door", "문짝", BarnKind.Door, "wood", "flat");
        for (int i = -2; i <= 2; i++)
            Box(door.Model, "Door plank", new Vector3(0f, 0f, i * .181f), new Vector3(.088f, 1.55f, .177f), wood[(i + 2) % 3]);
        Box(door.Model, "Cross brace", new Vector3(.055f, -.55f, 0f), new Vector3(.06f, .1f, .92f), wood[1]);
        Box(door.Model, "Cross brace", new Vector3(.055f, .53f, 0f), new Vector3(.06f, .1f, .92f), wood[1]);
        Box(door.Model, "Iron handle", new Vector3(.098f, 0f, -.29f), new Vector3(.08f, .11f, .055f), metal);
        slot.Accepts = item => item.Kind == BarnKind.Door ? null : "여긴 문짝을 다는 자리예요.";
        door.LoosePosition = new Vector3(-3.55f, 1.01f, 2.05f);
        door.LooseRotation = Quaternion.Euler(0f, 0f, 90f);
    }

    // Event D substitutes (tags decide the verdict) and the event C hidden object.
    private void BuildProps()
    {
        BarnItem pole = Item("prop_pole", "빨랫대 장대", BarnKind.Prop, "wood", "long", "sturdy", "straight");
        Box(pole.Model, "Laundry pole", Vector3.zero, new Vector3(.1f, .1f, 2.62f), wood[1]);
        Box(pole.Model, "Rope mark", new Vector3(0f, 0f, .9f), new Vector3(.11f, .11f, .05f), wood[2]);
        pole.LoosePosition = new Vector3(-5.45f, .05f, 2.3f);
        pole.Note = "빨래 널던 굵고 단단한 나무 장대";

        BarnItem broom = Item("prop_broom", "빗자루", BarnKind.Prop, "long", "fragile", "light");
        Box(broom.Model, "Broom handle", Vector3.zero, new Vector3(.04f, .04f, 1.35f), wood[0]);
        Box(broom.Model, "Broom head", new Vector3(0f, 0f, .78f), new Vector3(.32f, .07f, .26f), wood[2]);
        broom.LoosePosition = new Vector3(-5.35f, .04f, -.9f);
        broom.LooseRotation = Quaternion.Euler(0f, 18f, 0f);
        broom.Note = "가는 대나무 자루. 휘청거림";

        // Design call: the ladder is long and sturdy but its rungs stick out, so it is not "straight".
        BarnItem ladder = Item("prop_ladder", "사다리 한쪽", BarnKind.Prop, "wood", "long", "sturdy", "rungs");
        foreach (int sign in new[] { -1, 1 })
            Box(ladder.Model, "Ladder rail", new Vector3(sign * .2f, 0f, 0f), new Vector3(.06f, 2.5f, .07f), wood[0]);
        for (int i = 0; i < 7; i++)
            Box(ladder.Model, "Ladder rung", new Vector3(0f, -1.05f + i * .35f, 0f), new Vector3(.46f, .045f, .045f), wood[1]);
        ladder.LoosePosition = new Vector3(-1.9f, 1.23f, 4.82f);
        ladder.LooseRotation = Quaternion.Euler(-12f, 0f, 0f);
        ladder.Note = "튀어나온 가로대가 달린 사다리. 한쪽 다리만 뜯어 쓰기 어려움";

        BarnItem hidden = Item("item_hidden", "장선 사이의 물건", BarnKind.Hidden, "small");
        Box(hidden.Model, "Tin box", Vector3.zero, new Vector3(.3f, .1f, .2f), roof);
        Box(hidden.Model, "Tin lid", new Vector3(0f, .055f, 0f), new Vector3(.31f, .015f, .21f), metal);
        hidden.LoosePosition = root.TransformPoint(new Vector3(.3f, .31f, .55f));
        hidden.LooseRotation = Quaternion.Euler(0f, 23f, 4f);
        hidden.Hidden = true;
    }

    private BarnItem Item(string id, string name, BarnKind kind, params string[] tags)
    {
        var item = new BarnItem { Id = id, Name = name, Kind = kind, Model = StorehouseParts.Group(looseRoot, name, Vector3.zero) };
        item.Model.name = $"{name} | {id}";
        foreach (string tag in tags)
            item.Tags.Add(tag);
        Items.Add(item);
        return item;
    }

    private BarnSlot Slot(string id, string name, int step, Vector3 local)
    {
        var slot = new BarnSlot { Id = id, Name = name, Step = step, Position = root.TransformPoint(local), Rotation = root.rotation };
        Slots.Add(slot);
        return slot;
    }

    private static void Fill(BarnSlot slot, BarnItem item)
    {
        slot.Filled = item;
        item.Slot = slot;
    }

    private static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        => StorehouseParts.Box(parent, name, position, size, material);
}
