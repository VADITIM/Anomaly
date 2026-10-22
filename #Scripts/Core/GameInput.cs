using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Anomaly;

public static class GameInput
{
    public const string MOVE_LEFT = "move_left";
    public const string MOVE_RIGHT = "move_right";
    public const string MOVE_UP = "move_up";
    public const string MOVE_DOWN = "move_down";
    public const string AIM_LEFT = "aim_left";
    public const string AIM_RIGHT = "aim_right";
    public const string AIM_UP = "aim_up";
    public const string AIM_DOWN = "aim_down";
    public const string ATTACK = "attack";
    public const string SPECIAL = "special";
    public const string DASH = "dash";
    public const string JUMP = "jump";
    public const string HEAL = "heal";
    public const string AETHERIAC = "aetheriac";
    public const string INTERACT = "interact";
    public const string PAUSE = "pause";
    public const string MAP = "map";
    public const string CAMERA_UNLOCK = "camera_unlock";
    public const string DEBUG_GOD_MODE = "debug_god_mode";

    public static readonly string[] REBINDABLE =
    {
        MOVE_UP, MOVE_DOWN, MOVE_LEFT, MOVE_RIGHT, ATTACK, SPECIAL, DASH, JUMP, HEAL, AETHERIAC, INTERACT, CAMERA_UNLOCK,
    };

    private static readonly Dictionary<string, string> DISPLAY_NAMES = new()
    {
        [MOVE_UP] = "Move up",
        [MOVE_DOWN] = "Move down",
        [MOVE_LEFT] = "Move left",
        [MOVE_RIGHT] = "Move right",
        [ATTACK] = "Strike",
        [SPECIAL] = "Arc special",
        [DASH] = "Dash",
        [JUMP] = "Jump",
        [HEAL] = "Healing Mark",
        [AETHERIAC] = "Aetheriac State",
        [INTERACT] = "Interact",
        [CAMERA_UNLOCK] = "Unlock camera",
    };

    private static readonly Dictionary<string, string[]> defaults = new();

    public static bool UsingGamepad { get; private set; }
    public static Vector2? AimOverride { get; set; }

    private static bool registered;

    public static void Register()
    {
        if (registered)
            return;
        registered = true;

        AddKeys(MOVE_LEFT, Key.A, Key.Left);
        AddKeys(MOVE_RIGHT, Key.D, Key.Right);
        AddKeys(MOVE_UP, Key.W, Key.Up);
        AddKeys(MOVE_DOWN, Key.S, Key.Down);
        AddAxis(MOVE_LEFT, JoyAxis.LeftX, -1f);
        AddAxis(MOVE_RIGHT, JoyAxis.LeftX, 1f);
        AddAxis(MOVE_UP, JoyAxis.LeftY, -1f);
        AddAxis(MOVE_DOWN, JoyAxis.LeftY, 1f);
        AddAxis(AIM_LEFT, JoyAxis.RightX, -1f);
        AddAxis(AIM_RIGHT, JoyAxis.RightX, 1f);
        AddAxis(AIM_UP, JoyAxis.RightY, -1f);
        AddAxis(AIM_DOWN, JoyAxis.RightY, 1f);

        AddMouse(ATTACK, MouseButton.Left);
        AddKeys(ATTACK, Key.J);
        AddJoy(ATTACK, JoyButton.X);

        AddMouse(SPECIAL, MouseButton.Right);
        AddKeys(SPECIAL, Key.K);
        AddJoy(SPECIAL, JoyButton.Y);

        AddKeys(DASH, Key.Space);
        AddJoy(DASH, JoyButton.B);

        AddKeys(JUMP, Key.Shift, Key.C);
        AddJoy(JUMP, JoyButton.A);

        AddKeys(HEAL, Key.Q);
        AddJoy(HEAL, JoyButton.LeftShoulder);

        AddKeys(AETHERIAC, Key.R);
        AddJoy(AETHERIAC, JoyButton.RightShoulder);

        AddKeys(INTERACT, Key.E);
        AddJoy(INTERACT, JoyButton.DpadUp);

        AddKeys(PAUSE, Key.Escape);
        AddJoy(PAUSE, JoyButton.Start);

        AddKeys(MAP, Key.Tab);
        AddJoy(MAP, JoyButton.Back);

        // Physical keys follow the US layout: this is the key labelled Y on a German QWERTZ board.
        AddKeys(CAMERA_UNLOCK, Key.Z);
        AddJoy(CAMERA_UNLOCK, JoyButton.RightStick);

        AddKeys(DEBUG_GOD_MODE, Key.F1);

        foreach (string action in REBINDABLE)
            defaults[action] = BindingCodes(action);
    }

    public static string DisplayName(string action) => DISPLAY_NAMES.TryGetValue(action, out string name) ? name : action;

    // Keyboard and mouse bindings are rebindable and stored as "k:<physical keycode>" or "m:<button>". Gamepad stays fixed.
    public static string[] BindingCodes(string action) =>
        InputMap.ActionGetEvents(action).Select(Encode).Where(code => code != null).ToArray();

    public static void ApplyBindingCodes(string action, string[] codes)
    {
        if (!InputMap.HasAction(action))
            return;
        ClearKeyboardMouse(action);
        foreach (string code in codes)
            if (Decode(code) is InputEvent inputEvent)
                InputMap.ActionAddEvent(action, inputEvent);
    }

    // The new binding replaces this action's keyboard/mouse set and is taken away from any action that had it.
    public static void Rebind(string action, InputEvent inputEvent)
    {
        string code = Encode(inputEvent);
        if (code == null)
            return;
        foreach (string other in REBINDABLE)
            if (other != action && BindingCodes(other).Contains(code))
                ApplyBindingCodes(other, BindingCodes(other).Where(existing => existing != code).ToArray());
        ApplyBindingCodes(action, new[] { code });
    }

    public static void ResetBindings()
    {
        foreach ((string action, string[] codes) in defaults)
            ApplyBindingCodes(action, codes);
    }

    public static string Describe(string action)
    {
        string[] names = InputMap.HasAction(action) ? BindingCodes(action).Select(code => Label(Decode(code))).ToArray() : System.Array.Empty<string>();
        return names.Length == 0 ? "—" : string.Join(" / ", names);
    }

    // First binding only, for compact hints like "WASD".
    public static string Primary(string action) => Describe(action).Split(" / ")[0];

    public static bool IsBindable(InputEvent inputEvent) => Encode(inputEvent) != null;

    private static void ClearKeyboardMouse(string action)
    {
        foreach (InputEvent existing in InputMap.ActionGetEvents(action))
            if (existing is InputEventKey or InputEventMouseButton)
                InputMap.ActionEraseEvent(action, existing);
    }

    private static string Encode(InputEvent inputEvent) => inputEvent switch
    {
        InputEventKey key => $"k:{(long)(key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode)}",
        InputEventMouseButton mouse => $"m:{(long)mouse.ButtonIndex}",
        _ => null,
    };

    private static InputEvent Decode(string code)
    {
        if (code == null || code.Length < 3 || !long.TryParse(code.Substring(2), out long value))
            return null;
        return code[0] switch
        {
            'k' => new InputEventKey { PhysicalKeycode = (Key)value },
            'm' => new InputEventMouseButton { ButtonIndex = (MouseButton)value },
            _ => null,
        };
    }

    private static string Label(InputEvent inputEvent) => inputEvent switch
    {
        InputEventKey key => OS.GetKeycodeString(DisplayServer.KeyboardGetKeycodeFromPhysical(key.PhysicalKeycode)).ToUpper(),
        InputEventMouseButton { ButtonIndex: MouseButton.Left } => "LMB",
        InputEventMouseButton { ButtonIndex: MouseButton.Right } => "RMB",
        InputEventMouseButton { ButtonIndex: MouseButton.Middle } => "MMB",
        InputEventMouseButton mouse => $"MOUSE {(int)mouse.ButtonIndex}",
        _ => "?",
    };

    public static void TrackDevice(InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case InputEventJoypadButton:
                UsingGamepad = true;
                break;
            case InputEventJoypadMotion motion when Mathf.Abs(motion.AxisValue) > 0.35f:
                UsingGamepad = true;
                break;
            case InputEventMouseMotion mouseMotion when mouseMotion.Relative.LengthSquared() > 4f:
            case InputEventMouseButton:
            case InputEventKey:
                UsingGamepad = false;
                break;
        }
    }

    public static Vector2 MoveVector() => Input.GetVector(StringNames.Of(MOVE_LEFT), StringNames.Of(MOVE_RIGHT), StringNames.Of(MOVE_UP), StringNames.Of(MOVE_DOWN), 0.2f);

    public static Vector2 AimStick() => Input.GetVector(StringNames.Of(AIM_LEFT), StringNames.Of(AIM_RIGHT), StringNames.Of(AIM_UP), StringNames.Of(AIM_DOWN), 0.3f);

    private static void EnsureAction(string action)
    {
        if (!InputMap.HasAction(action))
            InputMap.AddAction(action, 0.2f);
    }

    private static void AddKeys(string action, params Key[] keys)
    {
        EnsureAction(action);
        foreach (Key key in keys)
            InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }

    private static void AddMouse(string action, MouseButton button)
    {
        EnsureAction(action);
        InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = button });
    }

    private static void AddJoy(string action, JoyButton button)
    {
        EnsureAction(action);
        InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = button });
    }

    private static void AddAxis(string action, JoyAxis axis, float direction)
    {
        EnsureAction(action);
        InputMap.ActionAddEvent(action, new InputEventJoypadMotion { Axis = axis, AxisValue = direction });
    }
}
