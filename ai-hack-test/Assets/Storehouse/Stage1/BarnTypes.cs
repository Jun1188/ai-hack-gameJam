using System;
using System.Collections.Generic;
using UnityEngine;

// Data shared by the Stage 1 barn systems. The LLM only ever sees ids, names, states and tags;
// every rule (slot acceptance, prerequisites, event outcomes) is decided by BarnStage.

public enum BarnKind
{
    Block, FloorFrame, Joist, FloorDeck, Wall, Roof, Nail, Door, Prop, Hidden
}

public sealed class BarnItem
{
    public string Id;
    public string Name;
    public BarnKind Kind;
    public readonly HashSet<string> Tags = new();
    public Transform Model;
    public Vector3 LoosePosition;
    public Quaternion LooseRotation = Quaternion.identity;
    public BarnSlot Slot;
    public int Turns;
    public int TurnPeriod = 1;
    public bool Hidden;
    public bool Heavy;
    public string Note;

    public bool Installed => Slot != null;
    public bool Has(string tag) => Tags.Contains(tag);
    public bool FacingOk => TurnPeriod <= 1 || Turns % TurnPeriod == 0;

    public Bounds WorldBounds()
    {
        var renderers = Model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(Model.position, Vector3.one * .3f);
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer surface in renderers)
            bounds.Encapsulate(surface.bounds);
        return bounds;
    }
}

public sealed class BarnSlot
{
    public string Id;
    public string Name;
    public int Step;
    public Vector3 Position;
    public Quaternion Rotation = Quaternion.identity;
    public BarnItem Filled;
    public Func<BarnItem, string> Accepts;   // null when the item fits, otherwise a hint for the robot to say
    public Func<string> Prerequisite;        // null when the slot is open, otherwise a hint
    public Transform Recall;
}

[Serializable]
public sealed class RobotAction
{
    public string type = "";
    public string target = "";
    public string slot = "";
    public string direction = "";
    public string text = "";

    public static RobotAction Make(string type, string target = "", string slot = "", string direction = "",
        string text = "") => new() { type = type, target = target, slot = slot, direction = direction, text = text };

    public override string ToString() => $"{type}({target}{(slot != "" ? "→" + slot : "")}{direction}{text})";
}

[Serializable]
public sealed class RobotPlan
{
    public RobotAction[] actions = Array.Empty<RobotAction>();
    public string say = "";
}

[Serializable]
public sealed class HiddenItemInfo
{
    [Tooltip("Placeholder: the real identity of the object under the floor is still undecided.")]
    public string identity = "낡은 양철 도시락통";
    [Tooltip("Words that count as a correct guess when the player says them.")]
    public string[] answers = { "도시락", "양철통", "밥통", "찬합" };
    public string material = "얇은 금속(양철). 두드리면 통통 소리가 남";
    public string color = "빨간 칠이 군데군데 벗겨졌고, 벗겨진 곳은 갈색으로 녹슬었음";
    public string size = "로봇 두 손바닥 정도. 들어 보면 가벼움";
    public string shape = "모서리가 둥근 납작한 네모 상자. 위에 뚜껑이 있고 옆에 걸쇠가 있음";
    public string feature = "흔들면 안에서 달그락 소리가 남. 뚜껑에 꽃 그림이 희미하게 남아 있음";
    [TextArea]
    public string story = "그건… 네 할머니가 새참을 담아 내오던 도시락통이구나. 태풍 때 여기까지 굴러왔던 게로구나.";
}

public static class BarnText
{
    public static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        var builder = new System.Text.StringBuilder(value.Length + 8);
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (c < 0x20) builder.Append("\\u").Append(((int)c).ToString("x4"));
                    else builder.Append(c);
                    break;
            }
        }
        return builder.ToString();
    }

    public static string Quote(string value) => "\"" + Escape(value) + "\"";

    // Removes spaces and punctuation so short spoken commands ("멈춰!!", "더 세게~") compare reliably.
    public static string Compact(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
            if (!char.IsWhiteSpace(c) && !char.IsPunctuation(c) && !char.IsSymbol(c))
                builder.Append(c);
        return builder.ToString();
    }

    // Korean particle that matches the last syllable: Josa("빗자루", "은", "는") -> "빗자루는".
    // For 으로/로 a final ㄹ also takes "로".
    public static string Josa(string word, string withFinal, string withoutFinal)
    {
        if (string.IsNullOrEmpty(word))
            return word;
        char last = word[^1];
        if (last < 0xAC00 || last > 0xD7A3)
            return word + withoutFinal;
        int final = (last - 0xAC00) % 28;
        bool useWith = final != 0 && !(withFinal == "으로" && final == 8);
        return word + (useWith ? withFinal : withoutFinal);
    }

    public static bool ContainsAny(string text, params string[] words)
    {
        foreach (string word in words)
            if (text.Contains(word))
                return true;
        return false;
    }
}
