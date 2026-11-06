using System;
using UnityEngine;

namespace CafeCoworker
{
    [Serializable] public sealed class BaristaProfile
    {
        public string id, displayName, role, personality, voice;
        public string[] interests;
        public float reminderDelay = 60, reminderCooldown = 60, talkDistance = 2.2f, lookDot = .92f;
    }
    [Serializable] public sealed class DialogueTopic { public string id, title; public string[] keywords, lines; }
    [Serializable] public sealed class BaristaTables
    { public BaristaProfile profile; public DialogueTopic[] topics; public RecipeSpec[] recipes; }
}
