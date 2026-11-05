using System.Collections.Generic;
using UnityEngine;

internal sealed class StorehouseParts
{
    internal sealed class Piece
    {
        internal string Name;
        internal int Stage;
        internal Transform Model;
        internal Vector3 Socket;
        internal Vector3 Pin;
        internal Vector3 Supply;
        internal int Turns;
        internal int TurnPeriod = 4;
        internal bool Installed;
    }

    private readonly Transform root;
    private readonly Material[] wood;
    private readonly Material stone, roof, metal;
    internal readonly List<Piece> All = new();

    internal StorehouseParts(Transform root, Material[] wood, Material stone, Material roof, Material metal)
    {
        this.root = root;
        this.wood = wood;
        this.stone = stone;
        this.roof = roof;
        this.metal = metal;
    }

    internal void Build()
    {
        for (int row = 0; row < 3; row++)
        for (int side = 0; side < 2; side++)
        {
            int index = row * 2 + side;
            Piece block = Add($"기초 블록 {index + 1}", 0, new Vector3(side == 0 ? -1.59f : 1.59f, .15f, (row - 1) * 1.1f));
            Box(block.Model, "Stone footing", Vector3.zero, new Vector3(.55f, .3f, .49f), stone);
            Box(block.Model, "Chipped stone cap", Vector3.up * .124f, new Vector3(.48f, .065f, .44f), stone);
            block.Installed = index != 0 && index != 5;
            block.Supply = block.Socket + (index == 0 ? new Vector3(.4f, 0f, -1.16f) : new Vector3(1.2f, 0f, -.27f));
        }

        Piece floorFrame = Add("바닥 틀", 1, new Vector3(0f, .38f, 0f));
        for (int sign = -1; sign <= 1; sign += 2)
        {
            Box(floorFrame.Model, "Long sill", new Vector3(0, 0, sign * 1.35f), new Vector3(3.8f, .17f, .15f), wood[1]);
            Box(floorFrame.Model, "End sill", new Vector3(sign * 1.83f, 0, 0), new Vector3(.14f, .17f, 2.58f), wood[1]);
        }

        for (int i = 0; i < 4; i++)
        {
            Piece joist = Add($"장선 {i + 1}", 2, new Vector3((i - 1.5f) * .6096f, .39f, 0f));
            Box(joist.Model, "Floor joist", Vector3.zero, new Vector3(.11f, .15f, 2.57f), wood[0]);
        }

        Piece deck = Add("바닥판", 3, new Vector3(0f, .5f, 0f));
        for (int i = 0; i < 13; i++)
            Box(deck.Model, "Floorboard", new Vector3(0, 0, (i - 6) * .216f), new Vector3(3.83f, .09f, .213f), wood[i % 3]);

        Wall("뒷벽", 4, new Vector3(0f, 1.53f, 1.34f), 3.8f, 0, false, false);
        Wall("창문 벽", 4, new Vector3(0f, 1.53f, -1.34f), 3.8f, 180, true, false);
        Wall("옆벽", 5, new Vector3(-1.82f, 1.53f, 0f), 2.57f, 90, false, false);
        Wall("문 벽", 5, new Vector3(1.82f, 1.53f, 0f), 2.57f, -90, false, true);

        Piece railA = Add("긴 윗틀", 6, new Vector3(0f, 2.58f, 0f));
        Piece railB = Add("짧은 윗틀", 6, new Vector3(0f, 2.66f, 0f));
        foreach (int sign in new[] { -1, 1 })
        {
            Box(railA.Model, "Long top plate", new Vector3(0f, 0f, sign * 1.35f), new Vector3(3.87f, .075f, .15f), wood[1]);
            Box(railB.Model, "Cross cap", new Vector3(sign * 1.82f, 0f, 0f), new Vector3(.16f, .075f, 2.83f), wood[1]);
            Box(railB.Model, "Lapped corner", new Vector3(sign * 1.49f, 0f, sign * 1.35f),
                new Vector3(.56f, .075f, .15f), wood[1]);
        }

        for (int i = 0; i < 5; i++)
        {
            Piece truss = Add($"서까래 {i + 1}", 7, new Vector3((i - 2) * .9f, 2.72f, 0f),
                pin: new Vector3(0f, .68f, -.28f));
            Beam(truss.Model, "Rafter", new Vector3(0f, 0f, -1.52f), new Vector3(0f, .82f, 0f), .115f, wood[0]);
            Beam(truss.Model, "Rafter", new Vector3(0f, .82f, 0f), new Vector3(0f, 0f, 1.52f), .115f, wood[0]);
            Box(truss.Model, "Collar tie", new Vector3(0f, .04f, 0f), new Vector3(.105f, .1f, 2.67f), wood[1]);
        }

        for (int side = -1; side <= 1; side += 2)
        {
            Piece panel = Add(side < 0 ? "앞 지붕판" : "뒤 지붕판", 8,
                new Vector3(0f, 3.17f, side * .78f));
            Transform slope = Group(panel.Model, "Roof slope", Vector3.zero);
            slope.localRotation = Quaternion.Euler(side * 28.4f, 0f, 0f);
            Box(slope, "Roof deck", Vector3.zero, new Vector3(4.16f, .09f, 1.78f), roof);
            for (int i = -5; i <= 5; i++)
                Box(slope, "Raised seam", new Vector3(i * .373f, .053f, 0f),
                    new Vector3(.023f, .03f, 1.77f), wood[1]);
        }

        Piece door = Add("문짝", 9, new Vector3(1.86f, 1.36f, 0f));
        for (int i = -2; i <= 2; i++)
            Box(door.Model, "Door plank", new Vector3(0f, 0f, i * .181f), new Vector3(.088f, 1.55f, .177f), wood[(i + 2) % 3]);
        Box(door.Model, "Cross brace", new Vector3(.055f, -.55f, 0f), new Vector3(.06f, .1f, .92f), wood[1]);
        Box(door.Model, "Cross brace", new Vector3(.055f, .53f, 0f), new Vector3(.06f, .1f, .92f), wood[1]);
        Box(door.Model, "Iron handle", new Vector3(.098f, 0f, -.29f), new Vector3(.08f, .11f, .055f), metal);
    }

    private Piece Add(string name, int stage, Vector3 socket, int turns = 0, Vector3? pin = null)
    {
        var piece = new Piece
        {
            Name = name, Stage = stage, Model = Group(root, name, socket),
            Socket = root.TransformPoint(socket), Turns = turns,
            Pin = root.TransformPoint(socket + (pin ?? Vector3.zero))
        };
        All.Add(piece);
        return piece;
    }

    private void Wall(string name, int stage, Vector3 position, float length, float yaw, bool window, bool door)
    {
        bool hasFacing = window || door;
        Piece panel = Add(name, stage, position, hasFacing ? 3 : 0);
        panel.TurnPeriod = hasFacing ? 4 : 2;
        Transform frame = Group(panel.Model, "Framed wall", Vector3.zero);
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
            if ((window || door) && i == bays / 2)
                continue;
            Box(frame, "Wall stud", new Vector3(x, .012f, 0f), new Vector3(.105f, 1.83f, .13f), wood[i % 3]);
        }
        if (window)
        {
            Box(frame, "Window header", new Vector3(0f, .65f, 0f), new Vector3(1.23f, .13f, .21f), wood[1]);
            Box(frame, "Window sill", new Vector3(0f, -.10f, 0f), new Vector3(1.23f, .12f, .23f), wood[1]);
            Box(frame, "Cripple stud", new Vector3(0f, -.5f, 0f), new Vector3(.095f, .74f, .13f), wood[0]);
        }
        if (door)
            Box(frame, "Door header", new Vector3(0f, .64f, 0f), new Vector3(1.23f, .16f, .17f), wood[1]);
    }

    internal static Transform Group(Transform parent, string name, Vector3 point)
    {
        var group = new GameObject(name).transform;
        group.SetParent(parent, false);
        group.localPosition = point;
        return group;
    }

    internal static Transform Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = size;
        var collider = box.GetComponent<Collider>();
        collider.enabled = false;
        if (Application.isPlaying) Object.Destroy(collider);
        else Object.DestroyImmediate(collider);
        box.GetComponent<Renderer>().sharedMaterial = material;
        return box.transform;
    }

    internal static void Beam(Transform parent, string name, Vector3 from, Vector3 to, float width, Material material)
    {
        Vector3 line = to - from;
        Box(parent, name, (from + to) * .5f, new Vector3(width, line.magnitude, width), material)
            .localRotation = Quaternion.FromToRotation(Vector3.up, line);
    }
}

