using System;
using System.Collections.Generic;

namespace Anomaly;

// One answer the player can give at the end of a line; Next is the node it leads to (null ends the conversation).
public readonly record struct NarrationChoice(string Label, string Next);

// One beat of a narration scene: who speaks and what they say. Animation optionally picks a portrait clip
// (e.g. "angry") on portraits that are authored as SpriteFrames. Inside a Conversation, Id names the node, Next the node that
// follows and Choices replace Next with a question; OnShow runs when the line appears.
public readonly record struct NarrationLine(
    string SpeakerId,
    string Text,
    string Animation = null,
    string Id = null,
    string Next = null,
    NarrationChoice[] Choices = null,
    Action OnShow = null)
{
    public Speaker Speaker => Speaker.Get(SpeakerId);
    public bool HasChoices => Choices is { Length: > 0 };

    public static NarrationLine Kio(string text, string animation = null) => new(Speaker.KIO, text, animation);
}

// A branching exchange: named lines linked by Next and Choices. Build it fluently, then hand it to NarrationBox.
public sealed class Conversation
{
    private readonly Dictionary<string, NarrationLine> nodes = new();

    public string Start { get; }

    public Conversation(string start) => Start = start;

    public NarrationLine this[string id] => nodes[id];

    public bool Has(string id) => id != null && nodes.ContainsKey(id);

    public Conversation Say(string id, string speaker, string text, string next = null, string animation = null, Action onShow = null)
    {
        nodes[id] = new NarrationLine(speaker, text, animation, id, next, null, onShow);
        return this;
    }

    public Conversation Ask(string id, string speaker, string text, params NarrationChoice[] choices)
    {
        nodes[id] = new NarrationLine(speaker, text, null, id, null, choices);
        return this;
    }
}
