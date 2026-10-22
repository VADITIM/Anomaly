using System;
using System.Collections.Generic;
using Godot;

namespace Anomaly;

// Modal dialogue band: a black wash that fades in from the top, running the full width of the screen with no border.
// Each speaker's cut-out portrait slides in from their own edge on their turn (Kio from the left, everyone else from
// the right) and back out when someone else speaks. Strike / Interact / confirm finishes the line, or moves on once
// it is written; a line with choices ends in a short answer list (move + confirm, or click). Pause / cancel skips
// the rest of the scene.
public partial class NarrationBox : CanvasLayer
{
	private const float CHARS_PER_SECOND = 42f;
	private const float PUNCTUATION_HOLD = 0.18f;
	private const int BLIP_EVERY = 3;
	private const float FADE_TIME = 0.25f;
	private const float SLIDE_TIME = 0.28f;
	private const float INPUT_GUARD = 0.15f;
	private const float AUTOPILOT_HOLD = 0.8f;

	private const float EDGE = 6f;
	private const float PORTRAIT_BOB = 2f;
	private const float CHOICE_ROW = 12f;

	private sealed class Slot
	{
		public Node2D Node;
		public AnimatedSprite2D Sprite;
		public bool Left;
		public Speaker Shown;
		public Speaker Wanted;
		public float T;
		public float Width;
		public float Scale;
	}

	private readonly Queue<NarrationLine> queue = new();
	private readonly List<Button> choiceButtons = new();
	private readonly Slot left = new() { Left = true };
	private readonly Slot right = new();
	private Action onFinished;
	private Conversation conversation;
	private string nextNode;

	private Control wash;
	private Label nameLabel;
	private Label textLabel;
	private DiamondMark nextHint;
	private VBoxContainer choiceList;
	private float choiceBase;
	private float choiceWidth;

	private NarrationLine current;
	private float typed;
	private float hold;
	private float fade;
	private float guard;
	private float time;
	private float autoAdvance = -1f;
	private bool closing;

	public bool IsOpen => Visible;
	private bool Typing => textLabel.VisibleCharacters >= 0 && textLabel.VisibleCharacters < textLabel.GetTotalCharacterCount();
	private bool Choosing => choiceList.Visible;

	// The look lives in NarrationBox.tscn, laid out as it appears in game. The scene also carries sample portraits,
	// text and answers for the editor; Ready hides or removes them, since the game fills them in per line.
	public override void _Ready()
	{
		ViewScale.Fit(this);
		Visible = false;

		wash = GetNode<Control>("Root/Wash");
		wash.Modulate = new Color(1, 1, 1, 0);
		BuildSlot(left, "Root/LeftSlot");
		BuildSlot(right, "Root/RightSlot");

		nameLabel = GetNode<Label>("Root/NameLabel");
		nameLabel.Text = "";
		textLabel = GetNode<Label>("Root/TextLabel");
		textLabel.Text = "";
		nextHint = GetNode<DiamondMark>("Root/NextHint");
		nextHint.Visible = false;

		// The sample answers show where the list sits with three rows; its bottom edge is what stays put.
		choiceList = GetNode<VBoxContainer>("Root/ChoiceList");
		choiceBase = choiceList.Position.Y + choiceList.GetChildCount() * CHOICE_ROW;
		choiceWidth = choiceList.Size.X;
		foreach (Node sample in choiceList.GetChildren())
			sample.Free();
		choiceList.Visible = false;
	}

	private void BuildSlot(Slot slot, string path)
	{
		slot.Node = GetNode<Node2D>(path);
		slot.Node.Visible = false;
		slot.Sprite = slot.Node.GetNode<AnimatedSprite2D>("Sprite");
		slot.Sprite.SpriteFrames = null;
		slot.Scale = slot.Sprite.Scale.X;
		slot.Width = 64f * slot.Scale;
	}

	// Plays a scene. Lines queued while one is already running join the end of it; onFinished runs once it closes.
	public void Play(IEnumerable<NarrationLine> lines, Action finished = null)
	{
		foreach (NarrationLine line in lines)
			queue.Enqueue(line);
		if (finished != null)
			onFinished += finished;
		if (Visible || queue.Count == 0)
			return;
		Open();
		NextLine();
	}

	public void Play(params NarrationLine[] lines) => Play(lines, null);

	// Plays a branching conversation from its start node; choices pick the next node until one has none.
	public void Play(Conversation script, Action finished = null)
	{
		if (Visible)
			return;
		conversation = script;
		nextNode = script.Start;
		if (finished != null)
			onFinished += finished;
		Open();
		NextLine();
	}

	public void Say(string speakerId, string text, Action finished = null) => Play(new[] { new NarrationLine(speakerId, text) }, finished);

	private void Open()
	{
		Visible = true;
		closing = false;
		GetTree().Paused = true;
		guard = INPUT_GUARD;
		nameLabel.Text = "";
		textLabel.Text = "";
	}

	private void NextLine()
	{
		if (conversation != null)
		{
			if (!conversation.Has(nextNode))
			{
				Finish();
				return;
			}
			current = conversation[nextNode];
			nextNode = current.Next;
		}
		else if (queue.Count > 0)
			current = queue.Dequeue();
		else
		{
			Finish();
			return;
		}
		HideChoices();
		current.OnShow?.Invoke();
		Speaker speaker = current.Speaker;
		bool hasPortrait = speaker.Portrait != null;

		left.Wanted = speaker.IsKio && hasPortrait ? speaker : null;
		right.Wanted = !speaker.IsKio && hasPortrait ? speaker : null;

		nameLabel.Text = speaker.DisplayName;
		nameLabel.LabelSettings.FontColor = speaker.NameColor;
		textLabel.Text = current.Text;
		textLabel.LabelSettings.FontColor = speaker.TextColor;
		textLabel.VisibleCharacters = 0;
		typed = 0f;
		hold = 0f;
		nextHint.Visible = false;
		autoAdvance = -1f;
	}

	// Swaps the art once the old portrait is off screen. Pixel art is shown at a whole-number scale.
	private void Assign(Slot slot, Speaker speaker)
	{
		slot.Shown = speaker;
		SpriteFrames frames = speaker?.Portrait;
		slot.Sprite.SpriteFrames = frames;
		slot.Node.Visible = frames != null;
		if (frames == null)
			return;
		string animation = frames.HasAnimation("default") ? "default" : frames.GetAnimationNames()[0];
		Vector2 size = frames.GetFrameCount(animation) > 0 ? frames.GetFrameTexture(animation, 0).GetSize() : new Vector2(64, 64);
		slot.Width = size.X * slot.Scale;
		slot.Sprite.Position = new Vector2(0, ViewScale.DESIGN_SIZE.Y - size.Y * slot.Scale);
		slot.Sprite.Play(animation);
	}

	private void UpdateSlot(Slot slot, float dt)
	{
		if (slot.Wanted != slot.Shown)
		{
			slot.T = Mathf.MoveToward(slot.T, 0f, dt / SLIDE_TIME);
			if (slot.T <= 0f)
				Assign(slot, slot.Wanted);
		}
		else
			slot.T = Mathf.MoveToward(slot.T, slot.Shown != null ? 1f : 0f, dt / SLIDE_TIME);

		if (slot.Shown == null)
			return;
		float screen = ViewScale.DESIGN_SIZE.X;
		float rest = slot.Left ? EDGE : screen - EDGE - slot.Width;
		float away = slot.Left ? -slot.Width - 40f : screen + 40f;
		float eased = Motion.Power2Out(slot.T);
		slot.Node.Position = new Vector2(Mathf.Lerp(away, rest, eased), 0f);
		slot.Node.Modulate = new Color(1, 1, 1, Mathf.Clamp(slot.T * 2f, 0f, 1f));
		// The speaker nods along with their words, in whole-pixel steps to keep the art crisp.
		bool talking = !closing && slot.Wanted == slot.Shown && slot.T >= 1f && Typing && current.Speaker == slot.Shown;
		slot.Sprite.Offset = new Vector2(0, talking && (int)(time * 8f) % 2 == 0 ? -PORTRAIT_BOB / slot.Scale : 0f);
	}

	private void Advance()
	{
		if (Typing)
		{
			textLabel.VisibleCharacters = -1;
			OnLineWritten();
			return;
		}
		if (Choosing)
		{
			PickFocusedChoice();
			return;
		}
		Sfx.Play("ui_move", -8f, 0.9f);
		NextLine();
	}

	private void OnLineWritten()
	{
		if (current.HasChoices)
			ShowChoices();
		else
			nextHint.Visible = true;
		if (AutoPilot.IsRequested())
			autoAdvance = AUTOPILOT_HOLD;
	}

	private void ShowChoices()
	{
		HideChoices();
		foreach (NarrationChoice choice in current.Choices)
		{
			Button button = UiFactory.MenuButton(choice.Label);
			button.CustomMinimumSize = new Vector2(choiceWidth, CHOICE_ROW);
			button.AddThemeColorOverride("font_outline_color", UiFactory.Outline);
			button.AddThemeConstantOverride("outline_size", 3);
			button.MouseEntered += button.GrabFocus;
			button.FocusEntered += () => Sfx.Play("ui_move", -10f, 1.2f);
			string next = choice.Next;
			button.Pressed += () => Choose(next);
			choiceList.AddChild(button);
			choiceButtons.Add(button);
		}
		choiceList.Position = new Vector2(choiceList.Position.X, choiceBase - choiceButtons.Count * CHOICE_ROW);
		choiceList.Visible = true;
		choiceButtons[0].GrabFocus();
	}

	private void HideChoices()
	{
		choiceList.Visible = false;
		foreach (Button button in choiceButtons)
			button.QueueFree();
		choiceButtons.Clear();
	}

	private void PickFocusedChoice()
	{
		foreach (Button button in choiceButtons)
			if (button.HasFocus())
			{
				button.EmitSignal(BaseButton.SignalName.Pressed);
				return;
			}
		choiceButtons[0].EmitSignal(BaseButton.SignalName.Pressed);
	}

	private void Choose(string next)
	{
		if (guard > 0f || !Choosing || closing)
			return;
		Sfx.Play("ui_confirm", -6f);
		HideChoices();
		nextNode = next;
		NextLine();
	}

	private void MoveChoice(int step)
	{
		for (int i = 0; i < choiceButtons.Count; i++)
			if (choiceButtons[i].HasFocus())
			{
				choiceButtons[(i + step + choiceButtons.Count) % choiceButtons.Count].GrabFocus();
				return;
			}
		choiceButtons[0].GrabFocus();
	}

	// Closing waits for the portraits to slide out and the wash to fade before the game resumes.
	private void Finish()
	{
		if (closing)
			return;
		closing = true;
		queue.Clear();
		conversation = null;
		nextNode = null;
		HideChoices();
		left.Wanted = null;
		right.Wanted = null;
		autoAdvance = -1f;
		nextHint.Visible = false;
	}

	private void Complete()
	{
		Visible = false;
		closing = false;
		GetTree().Paused = false;
		Action finished = onFinished;
		onFinished = null;
		finished?.Invoke();
	}

	public override void _Process(double delta)
	{
		if (!Visible)
			return;
		float dt = Motion.Unscaled(delta);
		time += dt;
		guard -= dt;

		fade = Mathf.MoveToward(fade, closing ? 0f : 1f, dt / FADE_TIME);
		wash.Modulate = new Color(1, 1, 1, fade);
		nameLabel.Modulate = textLabel.Modulate = new Color(1, 1, 1, closing ? fade : 1f);
		UpdateSlot(left, dt);
		UpdateSlot(right, dt);

		if (closing)
		{
			if (fade <= 0f && left.Shown == null && right.Shown == null)
				Complete();
			return;
		}

		if (Typing)
			TypeOut(dt);
		// Autopilot only presses actions, which never reach _UnhandledInput, so scripted runs page through on their own.
		if (autoAdvance > 0f && (autoAdvance -= dt) <= 0f)
			Advance();
		nextHint.Modulate = new Color(1, 1, 1, 0.55f + 0.45f * Mathf.Sin(time * 6f));
	}

	// Characters arrive at a steady rate, holding briefly after sentence punctuation; a soft blip marks the rhythm.
	private void TypeOut(float dt)
	{
		if (hold > 0f)
		{
			hold -= dt;
			return;
		}
		typed += dt * CHARS_PER_SECOND;
		int total = textLabel.GetTotalCharacterCount();
		while (textLabel.VisibleCharacters < Mathf.Min((int)typed, total))
		{
			int shown = textLabel.VisibleCharacters + 1;
			textLabel.VisibleCharacters = shown;
			char c = current.Text[Mathf.Min(shown, current.Text.Length) - 1];
			if (shown % BLIP_EVERY == 0 && !char.IsWhiteSpace(c))
				Sfx.Play("ui_move", -18f, current.Speaker.BlipPitch * 1.4f, 0.12f);
			if (shown < total && c is '.' or ',' or '!' or '?' or ';' or ':' or '—')
			{
				hold = c == ',' ? PUNCTUATION_HOLD * 0.5f : PUNCTUATION_HOLD;
				typed = shown;
				return;
			}
		}
		if (textLabel.VisibleCharacters >= total)
		{
			textLabel.VisibleCharacters = -1;
			OnLineWritten();
		}
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (!Visible)
			return;
		if (closing)
		{
			GetViewport().SetInputAsHandled();
			return;
		}
		if (Choosing && guard <= 0f && !inputEvent.IsEcho())
		{
			bool up = inputEvent.IsActionPressed(GameInput.MOVE_UP);
			bool down = inputEvent.IsActionPressed(GameInput.MOVE_DOWN);
			if (up || down)
			{
				GetViewport().SetInputAsHandled();
				MoveChoice(up ? -1 : 1);
				return;
			}
		}
		bool advance = inputEvent.IsActionPressed(GameInput.ATTACK) || inputEvent.IsActionPressed(GameInput.INTERACT) || inputEvent.IsActionPressed("ui_accept")
			|| inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && !Choosing;
		bool skip = inputEvent.IsActionPressed(GameInput.PAUSE) || inputEvent.IsActionPressed("ui_cancel");
		if (!advance && !skip)
			return;
		GetViewport().SetInputAsHandled();
		if (guard > 0f || inputEvent.IsEcho())
			return;
		if (skip)
		{
			Sfx.Play("ui_move", -6f, 0.7f);
			Finish();
		}
		else
			Advance();
	}
}
