using System;
using System.Collections.Generic;
using Godot;

namespace Anomaly;

// Placeholder NPCs for the sanctuary: line-art portraits from the free portrait pack, scripts to exercise the narration box.
// Two have yes/no choices, two are plain exchanges. Replace names and lines once the real cast is written.
public static class Dialogues
{
    public const string SCRIBE = "scribe";
    public const string WAYFARER = "wayfarer";
    public const string WATCHER = "watcher";
    public const string DRIFTER = "drifter";

    public sealed record NpcDef(string Id, string DisplayName, Color Tint, Func<Conversation> Script);

    private static readonly Dictionary<string, NpcDef> npcs = new();

    static Dialogues()
    {
        Speaker.Register(new Speaker { Id = SCRIBE, DisplayName = "Scribe", NameColor = new Color(0.95f, 0.85f, 0.55f), PortraitPath = Speaker.Mono(13) });
        Speaker.Register(new Speaker { Id = WAYFARER, DisplayName = "Wayfarer", NameColor = new Color(0.9f, 0.6f, 0.65f), PortraitPath = Speaker.Mono(14), BlipPitch = 1.2f });
        Speaker.Register(new Speaker { Id = WATCHER, DisplayName = "Watcher", NameColor = new Color(0.65f, 0.85f, 0.7f), PortraitPath = Speaker.Mono(7), BlipPitch = 1.1f });
        Speaker.Register(new Speaker { Id = DRIFTER, DisplayName = "Drifter", NameColor = new Color(0.75f, 0.75f, 0.9f), PortraitPath = Speaker.Mono(9), BlipPitch = 0.9f });

        Add(new NpcDef(SCRIBE, "Scribe", new Color(1f, 0.92f, 0.7f), ScribeTalk));
        Add(new NpcDef(WAYFARER, "Wayfarer", new Color(1f, 0.75f, 0.8f), WayfarerTalk));
        Add(new NpcDef(WATCHER, "Watcher", new Color(0.75f, 1f, 0.85f), WatcherTalk));
        Add(new NpcDef(DRIFTER, "Drifter", new Color(0.8f, 0.8f, 1f), DrifterTalk));
    }

    private static void Add(NpcDef def) => npcs[def.Id] = def;

    public static NpcDef Npc(string id) => npcs.TryGetValue(id ?? "", out NpcDef def) ? def : npcs[SCRIBE];

    private static NarrationChoice Yes(string next) => new("Yes", next);
    private static NarrationChoice No(string next) => new("No", next);

    // Choices, two endings.
    private static Conversation ScribeTalk() => new Conversation("greet")
        .Say("greet", SCRIBE, "Ah, a Hunter. Mind a question while the light holds?", "ask")
        .Ask("ask", SCRIBE, "Do you keep a record of what you cleanse?", Yes("yes"), No("no"))
        .Say("yes", Speaker.KIO, "Every one. It feels like the least I owe them.", "yes_reply")
        .Say("yes_reply", SCRIBE, "Then you are kinder than the last. Write it down; Akasa forgets quickly.")
        .Say("no", Speaker.KIO, "Why would I? They don't remember me.", "no_reply")
        .Say("no_reply", SCRIBE, "Hm. Perhaps that is its own mercy. Go on, Hunter.");

    // No choices, expressions only.
    private static Conversation WayfarerTalk() => new Conversation("a")
        .Say("a", WAYFARER, "You came from the forge? Maldor never lets anyone touch his anvil.", "b")
        .Say("b", Speaker.KIO, "He let me.", "c")
        .Say("c", WAYFARER, "Then he sees something in you. Be careful what he sees.", "d")
        .Say("d", WAYFARER, "...The Fractures hum at night. Don't answer them.");

    // Choices, a different shape: the refusal still ends kindly.
    private static Conversation WatcherTalk() => new Conversation("greet")
        .Say("greet", WATCHER, "I watch the ash corridor. Something nests past it.", "ask")
        .Ask("ask", WATCHER, "Will you clear it out?", Yes("yes"), No("no"))
        .Say("yes", Speaker.KIO, "I'll handle it.", "yes_reply")
        .Say("yes_reply", WATCHER, "Then I'll sleep tonight. Keep to the lanterns; they thin the dark.")
        .Say("no", Speaker.KIO, "Not yet. I need to grow stronger first.", "no_reply")
        .Say("no_reply", WATCHER, "Wise. Dead Hunters clear nothing.");

    // No choices, Kio answers.
    private static Conversation DrifterTalk() => new Conversation("a")
        .Say("a", DRIFTER, "Don't mind me. I'm only counting the stars that are left.", "b")
        .Say("b", Speaker.KIO, "How many?", "c")
        .Say("c", DRIFTER, "Fewer each night.");
}
