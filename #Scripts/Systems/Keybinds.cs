using Godot;
using System.Collections.Generic;

public partial class Keybinds : Node
{
	public const string UiCancel = "ui_cancel";
	public const string ToggleEscape = "Toggle Escape";
	public const string Jump = "jump";
	public const string Dodge = "dodge";
	public const string Heal = "heal";
	public const string Heavy = "heavy";
	public const string Attack = "attack";
	public const string MoveUp = "up";
	public const string MoveDown = "down";
	public const string MoveLeft = "left";
	public const string MoveRight = "right";
	public const string Special = "special";
	public const string Interact = "interact";
	public const string UiWeaponPanel = "ui_weapon_panel";
	public const string FocusToggle = "focus_toggle";

	public static readonly string[] ActionOrder =
	{
		UiCancel,
		ToggleEscape,
		Jump,
		Dodge,
		Attack,
		Heavy,
		Heal,
		MoveUp,
		MoveDown,
		MoveLeft,
		MoveRight,
		Special,
		Interact,
		UiWeaponPanel,
		FocusToggle
	};

	private static readonly Dictionary<string, string> ActionLabels = new()
	{
		[UiCancel] = "Cancel / Back",
		[ToggleEscape] = "Toggle Escape",
		[Jump] = "Jump",
		[Dodge] = "Dodge",
		[Attack] = "Attack",
		[Heavy] = "Heavy Attack",
		[Heal] = "Heal",
		[MoveUp] = "Move Up",
		[MoveDown] = "Move Down",
		[MoveLeft] = "Move Left",
		[MoveRight] = "Move Right",
		[Special] = "Special",
		[Interact] = "Interact",
		[UiWeaponPanel] = "Weapon Panel",
		[FocusToggle] = "Toggle Focus Mode"
	};

	private static readonly Dictionary<string, List<InputEvent>> DefaultBindings = new();
	private const string SavePath = "user://keybinds.cfg";
	private const string SaveSection = "input";
	private static bool _defaultsCaptured;

	public override void _Ready()
	{
		EnsureInitialized();
		LoadBindings();
	}

	public static void EnsureInitialized()
	{
		CaptureDefaultBindings();
	}

	public static string GetActionLabel(string action)
	{
		return ActionLabels.TryGetValue(action, out string label) ? label : action;
	}

	public static string GetBindingText(string action)
	{
		if (!InputMap.HasAction(action))
			return "Unbound";

		Godot.Collections.Array<InputEvent> events = InputMap.ActionGetEvents(action);
		if (events.Count == 0)
			return "Unbound";

		List<string> parts = new();
		foreach (InputEvent inputEvent in events)
		{
			string text = inputEvent.AsText();
			if (!string.IsNullOrWhiteSpace(text))
				parts.Add(text);
		}

		return parts.Count > 0 ? string.Join(", ", parts) : "Unbound";
	}

	public static void SetActionBinding(string action, InputEvent binding)
	{
		if (binding == null || !InputMap.HasAction(action))
			return;

		EnsureInitialized();
		RemoveMatchingBindingFromOtherActions(action, binding);
		InputMap.ActionEraseEvents(action);
		InputMap.ActionAddEvent(action, CloneEvent(binding));
		SaveBindings();
	}

	public static void ResetToDefaults()
	{
		EnsureInitialized();
		RestoreDefaultBindings();
		SaveBindings();
	}

	public static void LoadBindings()
	{
		EnsureInitialized();
		RestoreDefaultBindings();

		ConfigFile config = new();
		if (config.Load(SavePath) != Error.Ok)
			return;

		foreach (string action in ActionOrder)
		{
			if (!InputMap.HasAction(action) || !config.HasSectionKey(SaveSection, action))
				continue;

			InputMap.ActionEraseEvents(action);
			Godot.Collections.Array savedEvents = config.GetValue(SaveSection, action, new Godot.Collections.Array()).AsGodotArray();
			if (savedEvents == null)
				continue;

			foreach (Variant savedEvent in savedEvents)
			{
				InputEvent loadedEvent = DeserializeEvent(savedEvent.AsGodotDictionary());
				if (loadedEvent != null)
					InputMap.ActionAddEvent(action, loadedEvent);
			}
		}
	}

	public static void SaveBindings()
	{
		ConfigFile config = new();

		foreach (string action in ActionOrder)
		{
			Godot.Collections.Array serializedEvents = new();
			if (InputMap.HasAction(action))
			{
				foreach (InputEvent inputEvent in InputMap.ActionGetEvents(action))
					serializedEvents.Add(SerializeEvent(inputEvent));
			}

			config.SetValue(SaveSection, action, serializedEvents);
		}

		config.Save(SavePath);
	}

	private static void CaptureDefaultBindings()
	{
		if (_defaultsCaptured)
			return;

		DefaultBindings.Clear();

		foreach (string action in ActionOrder)
		{
			List<InputEvent> events = new();
			if (InputMap.HasAction(action))
			{
				foreach (InputEvent inputEvent in InputMap.ActionGetEvents(action))
					events.Add(CloneEvent(inputEvent));
			}

			DefaultBindings[action] = events;
		}

		_defaultsCaptured = true;
	}

	private static void RestoreDefaultBindings()
	{
		foreach (string action in ActionOrder)
		{
			if (!InputMap.HasAction(action))
				continue;

			InputMap.ActionEraseEvents(action);
			if (!DefaultBindings.TryGetValue(action, out List<InputEvent> events))
				continue;

			foreach (InputEvent inputEvent in events)
				InputMap.ActionAddEvent(action, CloneEvent(inputEvent));
		}
	}

	private static void RemoveMatchingBindingFromOtherActions(string currentAction, InputEvent binding)
	{
		foreach (string action in ActionOrder)
		{
			if (action == currentAction || !InputMap.HasAction(action))
				continue;

			Godot.Collections.Array<InputEvent> events = InputMap.ActionGetEvents(action);
			foreach (InputEvent existingEvent in events)
			{
				if (EventsMatch(existingEvent, binding))
					InputMap.ActionEraseEvent(action, existingEvent);
			}
		}
	}

	private static bool EventsMatch(InputEvent existingEvent, InputEvent candidateEvent)
	{
		if (existingEvent == null || candidateEvent == null)
			return false;

		if (existingEvent.GetType() != candidateEvent.GetType())
			return false;

		if (existingEvent is InputEventKey existingKey && candidateEvent is InputEventKey candidateKey)
			return existingKey.PhysicalKeycode == candidateKey.PhysicalKeycode && existingKey.Keycode == candidateKey.Keycode;

		if (existingEvent is InputEventMouseButton existingMouse && candidateEvent is InputEventMouseButton candidateMouse)
			return existingMouse.ButtonIndex == candidateMouse.ButtonIndex;

		if (existingEvent is InputEventJoypadButton existingJoypadButton && candidateEvent is InputEventJoypadButton candidateJoypadButton)
			return existingJoypadButton.ButtonIndex == candidateJoypadButton.ButtonIndex;

		if (existingEvent is InputEventJoypadMotion existingMotion && candidateEvent is InputEventJoypadMotion candidateMotion)
			return existingMotion.Axis == candidateMotion.Axis && Mathf.IsEqualApprox(existingMotion.AxisValue, candidateMotion.AxisValue);

		return existingEvent.AsText() == candidateEvent.AsText();
	}

	private static InputEvent CloneEvent(InputEvent inputEvent)
	{
		return inputEvent?.Duplicate() as InputEvent;
	}

	private static Godot.Collections.Dictionary SerializeEvent(InputEvent inputEvent)
	{
		Godot.Collections.Dictionary data = new();

		switch (inputEvent)
		{
			case InputEventKey keyEvent:
				data["type"] = "key";
				data["physical_keycode"] = (long)keyEvent.PhysicalKeycode;
				data["keycode"] = (long)keyEvent.Keycode;
				data["shift_pressed"] = keyEvent.ShiftPressed;
				data["alt_pressed"] = keyEvent.AltPressed;
				data["ctrl_pressed"] = keyEvent.CtrlPressed;
				data["meta_pressed"] = keyEvent.MetaPressed;
				data["location"] = (long)keyEvent.Location;
				break;
			case InputEventMouseButton mouseButtonEvent:
				data["type"] = "mouse_button";
				data["button_index"] = (long)mouseButtonEvent.ButtonIndex;
				data["device"] = mouseButtonEvent.Device;
				data["double_click"] = mouseButtonEvent.DoubleClick;
				data["factor"] = mouseButtonEvent.Factor;
				break;
			case InputEventJoypadButton joypadButtonEvent:
				data["type"] = "joypad_button";
				data["button_index"] = (long)joypadButtonEvent.ButtonIndex;
				data["device"] = joypadButtonEvent.Device;
				break;
			case InputEventJoypadMotion joypadMotionEvent:
				data["type"] = "joypad_motion";
				data["axis"] = (long)joypadMotionEvent.Axis;
				data["axis_value"] = joypadMotionEvent.AxisValue;
				data["device"] = joypadMotionEvent.Device;
				break;
			default:
				data["type"] = "unknown";
				data["text"] = inputEvent.AsText();
				break;
		}

		return data;
	}

	private static InputEvent DeserializeEvent(Godot.Collections.Dictionary data)
	{
		if (data == null || !data.ContainsKey("type"))
			return null;

		string type = data["type"].AsString();

		switch (type)
		{
			case "key":
				return new InputEventKey
				{
					PhysicalKeycode = (Key)(long)data.GetValueOrDefault("physical_keycode", 0L),
					Keycode = (Key)(long)data.GetValueOrDefault("keycode", 0L),
					ShiftPressed = data.GetValueOrDefault("shift_pressed", false).AsBool(),
					AltPressed = data.GetValueOrDefault("alt_pressed", false).AsBool(),
					CtrlPressed = data.GetValueOrDefault("ctrl_pressed", false).AsBool(),
					MetaPressed = data.GetValueOrDefault("meta_pressed", false).AsBool(),
					Location = (KeyLocation)(long)data.GetValueOrDefault("location", 0L)
				};
			case "mouse_button":
				return new InputEventMouseButton
				{
					ButtonIndex = (MouseButton)(long)data.GetValueOrDefault("button_index", 0L),
					Device = (int)data.GetValueOrDefault("device", -1),
					DoubleClick = data.GetValueOrDefault("double_click", false).AsBool(),
					Factor = (float)data.GetValueOrDefault("factor", 1.0)
				};
			case "joypad_button":
				return new InputEventJoypadButton
				{
					ButtonIndex = (JoyButton)(long)data.GetValueOrDefault("button_index", 0L),
					Device = (int)data.GetValueOrDefault("device", -1)
				};
			case "joypad_motion":
				return new InputEventJoypadMotion
				{
					Axis = (JoyAxis)(long)data.GetValueOrDefault("axis", 0L),
					AxisValue = (float)data.GetValueOrDefault("axis_value", 0.0),
					Device = (int)data.GetValueOrDefault("device", -1)
				};
			default:
				return null;
		}
	}
}