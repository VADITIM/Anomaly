using System.Collections.Generic;
using Godot;

namespace Anomaly;

// Who says a narration line. Kio's portrait slides in from the left; every other speaker's from the right.
// A portrait is a SpriteFrames: a .tres is used as authored, a single texture becomes a one-frame "default"
// animation. Black-and-white line-art portraits (Portraits/Mono) are cut out on load: the paper inside the outline
// turns off-white and the silhouette gets a thin light edge, so the art reads on the black dialogue wash.
// A speaker without art simply has no portrait.
public sealed class Speaker
{
    public const string KIO = "kio";
    public const string MALDOR = "maldor";
    public const string VOICE = "voice";

    private const float TALK_FPS = 5f;
    private static readonly Color PAPER = new(0.9f, 0.88f, 0.84f);
    private static readonly Color INK = new(0.04f, 0.03f, 0.07f);
    private static readonly Color EDGE = new(0.92f, 0.9f, 0.86f);

    public string Id { get; init; }
    public string DisplayName { get; init; }
    public Color NameColor { get; init; } = UiFactory.Accent;
    public Color TextColor { get; init; } = UiFactory.TextColor;
    public string PortraitPath { get; init; }
    public float BlipPitch { get; init; } = 1f;

    public bool IsKio => Id == KIO;

    private SpriteFrames frames;
    private bool framesLoaded;

    public SpriteFrames Portrait
    {
        get
        {
            if (!framesLoaded)
            {
                framesLoaded = true;
                frames = BuildPortrait();
            }
            return frames;
        }
    }

    private SpriteFrames BuildPortrait()
    {
        if (PortraitPath == null)
            return null;
        if (PortraitPath.EndsWith(".tres"))
            return ResourceLoader.Exists(Art.ASSETS + PortraitPath) ? GD.Load<SpriteFrames>(Art.ASSETS + PortraitPath) : null;

        Texture2D texture = LoadTexture(PortraitPath);
        if (texture == null)
            return null;
        var single = new SpriteFrames();
        single.SetAnimationSpeed("default", TALK_FPS);
        single.AddFrame("default", texture);
        return single;
    }

    // A freshly copied PNG has no import until the editor scans it, so fall back to reading the file directly.
    private static Texture2D LoadTexture(string path)
    {
        string full = Art.ASSETS + path;
        Image image = ResourceLoader.Exists(full) ? GD.Load<Texture2D>(full).GetImage() : Image.LoadFromFile(full);
        if (image == null || image.IsEmpty())
            return null;
        if (path.StartsWith("Portraits/Mono/"))
            image = CutOut(image);
        return ImageTexture.CreateFromImage(image);
    }

    // The pack is ink on transparent. Transparent pixels reachable from the border are the background and stay
    // clear (with a 1px light edge where they touch the figure); transparent pixels enclosed by ink are paper.
    private static Image CutOut(Image source)
    {
        int w = source.GetWidth() + 2;
        int h = source.GetHeight() + 2;
        var solid = new bool[w * h];
        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
                solid[y * w + x] = source.GetPixel(x - 1, y - 1).A > 0.5f;

        var background = new bool[w * h];
        var stack = new Stack<int>();
        void Push(int x, int y)
        {
            int i = y * w + x;
            if (x < 0 || y < 0 || x >= w || y >= h || solid[i] || background[i])
                return;
            background[i] = true;
            stack.Push(i);
        }
        for (int x = 0; x < w; x++)
        {
            Push(x, 0);
            Push(x, h - 1);
        }
        for (int y = 0; y < h; y++)
        {
            Push(0, y);
            Push(w - 1, y);
        }
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            int x = i % w;
            int y = i / w;
            Push(x + 1, y);
            Push(x - 1, y);
            Push(x, y + 1);
            Push(x, y - 1);
        }

        var cut = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (!background[i])
                    cut.SetPixel(x, y, solid[i] ? INK : PAPER);
                else if ((x > 0 && !background[i - 1]) || (x < w - 1 && !background[i + 1]) || (y > 0 && !background[i - w]) || (y < h - 1 && !background[i + w]))
                    cut.SetPixel(x, y, EDGE);
                else
                    cut.SetPixel(x, y, new Color(0, 0, 0, 0));
            }
        return cut;
    }

    public static string Mono(int number) => $"Portraits/Mono/portrait_{number:00}.png";

    private static readonly Dictionary<string, Speaker> registry = new();

    static Speaker()
    {
        Register(new Speaker { Id = KIO, DisplayName = "Kio", NameColor = UiFactory.Accent, PortraitPath = Mono(3) });
        Register(new Speaker { Id = MALDOR, DisplayName = "Maldor", NameColor = new Color(1f, 0.72f, 0.42f), PortraitPath = "Entities/Maldor/portrait.png", BlipPitch = 0.75f });
        Register(new Speaker { Id = VOICE, DisplayName = "The Voice", NameColor = UiFactory.VoiceColor, TextColor = UiFactory.VoiceColor, BlipPitch = 0.6f });
    }

    public static void Register(Speaker speaker) => registry[speaker.Id] = speaker;

    // Unknown ids still speak, on the right, under their own id, so a missing entry never stalls a scene.
    public static Speaker Get(string id)
    {
        if (!registry.TryGetValue(id, out Speaker speaker))
            registry[id] = speaker = new Speaker { Id = id, DisplayName = id };
        return speaker;
    }
}
