using System;
using System.Drawing;
using System.Numerics;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;
using KamiToolKit.UiOverlay;

namespace Altio.Dialogue;

public enum BalloonBackground
{
    Say,
    Party,
    Tell,
    Alliance,
    Yell,
    Shout,
    FreeCompany,
    Linkshell,
    CrossWorldLinkshell,
    Novice,
    PvP,
}

public enum BalloonColor
{
    Default,
    Lime,
    Orange,
    Violet,
    SkyBlue,
    Clay,
    LightJeans,
    GrassGreen,
    Gray,
    Pink,
    DarkJeans,
    Green,
    Purple,
    Brown,
    CloudyBlue,
    RoyalPurple,
}

public sealed class BalloonNode : OverlayNode
{
    private const string Texture = "ui/uld/MiniTalkPlayer_hr1.tex";

    private readonly SimpleNineGridNode background;
    private readonly SimpleNineGridNode gradient;
    private readonly SimpleImageNode arrow;
    private readonly TextNode body;

    public override OverlayLayer OverlayLayer => OverlayLayer.BehindUserInterface;
    public override bool HideWithNativeUi => false;
    public override bool HideWithUiToggled => false;

    public BalloonBackground Background = BalloonBackground.Say;
    public BalloonColor BubbleColor = BalloonColor.Default;
    public string Text = string.Empty;
    public bool ArrowVisible = true;
    public float ArrowX = 49.0f;
    public uint FontSize = 14;

    public BalloonNode()
    {
        background = new SimpleNineGridNode
        {
            TexturePath = Texture,
            TextureSize = new Vector2(200.0f, 90.0f),
            TextureCoordinates = CoordinatesForBackground(),
            Size = new Vector2(200.0f, 90.0f),
            TopOffset = 51.0f,
            BottomOffset = 37.0f,
            LeftOffset = 162.0f,
            RightOffset = 36.0f,
        };

        gradient = new SimpleNineGridNode
        {
            TexturePath = Texture,
            TextureSize = new Vector2(200.0f, 90.0f),
            TextureCoordinates = CoordinatesForGradient(),
            MultiplyColor = ColorForGradient(),
            Size = new Vector2(200.0f, 90.0f),
            TopOffset = 51.0f,
            BottomOffset = 37.0f,
            LeftOffset = 162.0f,
            RightOffset = 36.0f,
        };

        arrow = new SimpleImageNode
        {
            TexturePath = Texture,
            TextureSize = new Vector2(32.0f, 32.0f),
            TextureCoordinates = new Vector2(0, 992.0f),
            Position = new Vector2(49, 70),
            Size = new Vector2(32.0f, 32.0f),
        };

        body = new TextNode
        {
            Size = new Vector2(151.0f, 17.0f),
            Position = new Vector2(24.0f, 43.0f),
            TextColor = KnownColor.Black.Vector(),
            FontType = FontType.Axis,
            TextFlags = TextFlags.Ellipsis,
            AlignmentType = AlignmentType.Center,
            FontSize = FontSize,
            LineSpacing = 14,
        };

        background.AttachNode(this);
        gradient.AttachNode(this);
        arrow.AttachNode(this);
        body.AttachNode(this);
    }

    protected override void OnUpdate() { }

    public void Refresh()
    {
        body.String = Text;
        body.FontSize = FontSize;
        background.TextureCoordinates = CoordinatesForBackground();
        gradient.TextureCoordinates = CoordinatesForGradient();
        gradient.MultiplyColor = ColorForGradient();
        arrow.IsVisible = ArrowVisible;
        if (ArrowVisible)
            arrow.Position = new Vector2(Math.Clamp(ArrowX, 32.0f, 130.0f), 70.0f);
    }

    private Vector2 CoordinatesForBackground() => new(0, 90 * (int)Background);

    private Vector2 CoordinatesForGradient() => new(200, 90 * (int)Background);

    private Vector3 ColorForGradient() => BubbleColor switch
    {
        BalloonColor.Lime => new Vector3(74, 74, 0),
        BalloonColor.Orange => new Vector3(87, 60, 28),
        BalloonColor.Violet => new Vector3(76, 48, 63),
        BalloonColor.SkyBlue => new Vector3(39, 70, 78),
        BalloonColor.Clay => new Vector3(72, 40, 22),
        BalloonColor.LightJeans => new Vector3(43, 58, 62),
        BalloonColor.GrassGreen => new Vector3(47, 62, 11),
        BalloonColor.Gray => new Vector3(50, 50, 50),
        BalloonColor.Pink => new Vector3(78, 50, 50),
        BalloonColor.DarkJeans => new Vector3(27, 39, 51),
        BalloonColor.Green => new Vector3(36, 58, 36),
        BalloonColor.Purple => new Vector3(40, 32, 46),
        BalloonColor.Brown => new Vector3(54, 44, 26),
        BalloonColor.CloudyBlue => new Vector3(40, 63, 80),
        BalloonColor.RoyalPurple => new Vector3(51, 29, 41),
        _ => new Vector3(83, 76, 58),
    } / 100.0f;
}
