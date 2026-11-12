using System.Drawing;
using System.Numerics;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;
using KamiToolKit.UiOverlay;

namespace Altio.Dialogue;

public enum TalkBackground
{
    Basic = 0,
    Thought = 1,
    Echo = 2,
    Computer = 3,
    Yell = 4,
    Parchment = 5,
    Dragonspeak = 6,
    Linkpearl = 7,
    Narration = 8,
}

public enum TalkCursor
{
    None = 0,
    Pin = 1,
    Loop = 2,
}

public sealed unsafe class TalkNode : OverlayNode
{
    private const string BasicTexture = "ui/uld/Talk_Basic_hr1.tex";
    private const string OtherTexture = "ui/uld/Talk_Other_hr1.tex";
    private const string FrameTexture = "ui/uld/Talk_hr1.tex";

    private readonly SimpleImageNode background;
    private readonly NineGridNode speakerPlate;
    private readonly SimpleImageNode cursor;
    private readonly TextNode body;
    private readonly TextNode speaker;

    public override OverlayLayer OverlayLayer => OverlayLayer.BehindUserInterface;
    public override bool HideWithNativeUi => false;
    public override bool HideWithUiToggled => false;

    public TalkBackground Background = TalkBackground.Basic;
    public TalkCursor Cursor = TalkCursor.Pin;
    public string Speaker = string.Empty;
    public string Text = string.Empty;
    public uint FontSize = 14;

    public TalkNode()
    {
        background = new SimpleImageNode
        {
            Size = new Vector2(544.0f, 144.0f),
            WrapMode = WrapMode.Stretch,
            Scale = new Vector2(1.25f),
            TexturePath = TextureForBackground(),
            TextureCoordinates = CoordinatesForBackground(),
            TextureSize = new Vector2(544.0f, 144.0f),
        };

        speakerPlate = new NineGridNode
        {
            Size = new Vector2(288.0f, 36.0f),
            Position = new Vector2(18.0f, 0.0f),
            Scale = new Vector2(1.25f),
            TopOffset = 0.0f,
            LeftOffset = 50.0f,
            RightOffset = 1.0f,
            BottomOffset = 0.0f,
        };
        speakerPlate.AddPart(new Part
        {
            TexturePath = FrameTexture,
            TextureCoordinates = Vector2.Zero,
            Size = new Vector2(288.0f, 36.0f),
            Id = 0,
        });

        cursor = new SimpleImageNode
        {
            Size = new Vector2(18.0f, 24.0f),
            Position = new Vector2(614.0f, 104.0f),
            WrapMode = WrapMode.Tile,
            TexturePath = FrameTexture,
            TextureCoordinates = CoordinatesForCursor(),
            TextureSize = new Vector2(16.0f, 24.0f),
        };

        body = new TextNode
        {
            Size = new Vector2(556.0f, 90.0f),
            Position = new Vector2(62.0f, 42.0f),
            TextColor = TextColorForBackground(),
            FontType = FontType.Axis,
            TextFlags = TextFlags.WordWrap | TextFlags.MultiLine | TextFlags.OverflowHidden,
            FontSize = FontSize,
            LineSpacing = 18,
        };

        speaker = new TextNode
        {
            Size = new Vector2(300.0f, 36.0f),
            Position = new Vector2(60.0f, 2.0f),
            TextColor = KnownColor.White.Vector(),
            TextOutlineColor = KnownColor.Black.Vector(),
            FontType = FontType.Axis,
            FontSize = 18,
            AlignmentType = AlignmentType.Left,
            TextFlags = TextFlags.Edge | TextFlags.Ellipsis,
        };

        background.AttachNode(this);
        speakerPlate.AttachNode(this);
        cursor.AttachNode(this);
        body.AttachNode(this);
        speaker.AttachNode(this);
    }

    protected override void OnUpdate() { }

    public void Refresh()
    {
        body.String = Text;
        body.TextColor = TextColorForBackground();
        body.FontSize = FontSize;
        speaker.String = Speaker;
        speakerPlate.IsVisible = Background != TalkBackground.Narration && !string.IsNullOrEmpty(Speaker);
        speaker.IsVisible = speakerPlate.IsVisible;

        cursor.TextureCoordinates = CoordinatesForCursor();
        cursor.IsVisible = Cursor != TalkCursor.None;

        background.TexturePath = TextureForBackground();
        background.TextureCoordinates = CoordinatesForBackground();
    }

    private string TextureForBackground() => (int)Background <= 2 ? BasicTexture : OtherTexture;

    private Vector4 TextColorForBackground()
        => Background is TalkBackground.Echo or TalkBackground.Computer or TalkBackground.Narration
            ? KnownColor.White.Vector()
            : KnownColor.Black.Vector();

    private Vector2 CoordinatesForBackground() => Background switch
    {
        TalkBackground.Basic or TalkBackground.Computer => new Vector2(0, 0),
        TalkBackground.Thought or TalkBackground.Yell => new Vector2(0, 144),
        TalkBackground.Echo or TalkBackground.Parchment => new Vector2(0, 288),
        TalkBackground.Dragonspeak => new Vector2(0, 432),
        TalkBackground.Linkpearl => new Vector2(0, 576),
        TalkBackground.Narration => new Vector2(0, 720),
        _ => Vector2.Zero,
    };

    private Vector2 CoordinatesForCursor() => Cursor switch
    {
        TalkCursor.Pin => new Vector2(288, 0),
        TalkCursor.Loop => new Vector2(306, 0),
        _ => Vector2.Zero,
    };
}
