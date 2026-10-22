using System.Collections.Generic;
using Godot;

namespace Anomaly;

// Passing a plain string where Godot wants a StringName (an input action, a shader parameter, an animation) builds a new
// StringName on every call. Each one is a finalizable object, so per-frame calls pile up garbage and make every GC
// pause long enough to drop frames. Code that runs every frame looks its names up here instead.
public static class StringNames
{
    private static readonly Dictionary<string, StringName> cache = new();

    public static StringName Of(string name)
    {
        if (!cache.TryGetValue(name, out StringName stringName))
            cache[name] = stringName = new StringName(name);
        return stringName;
    }
}
