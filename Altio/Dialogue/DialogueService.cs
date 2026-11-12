using System;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KamiToolKit;
using KamiToolKit.UiOverlay;

namespace Altio.Dialogue;

public sealed class DialogueService : IDisposable
{
    private readonly IFramework framework;
    private readonly Configuration config;

    private OverlayController? controller;
    private TalkNode? talk;
    private BalloonNode? balloon;
    private bool disposed;

    private readonly record struct NodeState(
        DialogueLine Line,
        LineStyle Style,
        int Shown,
        string Text,
        string Speaker,
        uint FontSize,
        TalkBackground TalkBackground,
        TalkCursor TalkCursor,
        BalloonBackground BalloonBackground,
        BalloonColor BalloonColor,
        bool BalloonArrow,
        float BalloonArrowX,
        float Scale);

    private NodeState? lastState;
    private float revealed;
    private int lastLineIndex = -1;
    private DialogueScript? lastScript;

    public bool Ready { get; private set; }
    public string? InitError { get; private set; }
    public bool Showing { get; private set; }
    public int LineIndex { get; private set; }
    public bool MoveMode { get; private set; }

    public DialogueScript? Script
        => config.Scripts.Count == 0 ? null : config.Scripts[Math.Clamp(config.ActiveScript, 0, config.Scripts.Count - 1)];

    public DialogueLine? CurrentLine
        => Script is { } s && LineIndex >= 0 && LineIndex < s.Lines.Count ? s.Lines[LineIndex] : null;

    private static bool InGPose => Plugin.ClientState.IsGPosing;
    private bool wasInGPose;

    public bool IsTyping => CurrentLine is { } line && revealed < line.Text.Length;

    public DialogueService(IDalamudPluginInterface pi, IFramework framework, Configuration config)
    {
        this.framework = framework;
        this.config = config;
        _ = InitializeAsync(pi);
    }

    private async Task InitializeAsync(IDalamudPluginInterface pi)
    {
        try
        {
            await KamiToolKitLibrary.InitializeAsync(pi);
            await framework.RunOnFrameworkThread(() =>
            {
                if (disposed)
                    return;
                controller = new OverlayController();
                talk = new TalkNode { Size = new Vector2(680.0f, 180.0f), IsVisible = false };
                balloon = new BalloonNode { Size = new Vector2(200.0f, 90.0f), IsVisible = false };
                controller.AddNode(talk);
                controller.AddNode(balloon);
                framework.Update += OnUpdate;
                Ready = true;
            });
        }
        catch (Exception ex)
        {
            InitError = ex.Message;
            Plugin.Log.Error(ex, "Dialogue overlay failed to start");
        }
    }

    public void Show()
    {
        if (!InGPose || Script is not { Lines.Count: > 0 })
            return;
        LineIndex = Math.Clamp(LineIndex, 0, Script.Lines.Count - 1);
        Showing = true;
        Restart();
    }

    public void Hide()
    {
        Showing = false;
        SetMoveMode(false);
    }

    public void Toggle()
    {
        if (Showing)
            Hide();
        else
            Show();
    }

    public void Next()
    {
        if (!InGPose)
            return;
        if (!Showing)
        {
            Show();
            return;
        }
        if (IsTyping)
        {
            revealed = CurrentLine!.Text.Length;
            return;
        }
        if (Script is null || LineIndex >= Script.Lines.Count - 1)
        {
            if (config.LoopScript)
            {
                LineIndex = 0;
                Restart();
            }
            else if (config.HideAtEnd)
                Hide();
            return;
        }
        LineIndex++;
        Restart();
    }

    public void Previous()
    {
        if (!InGPose || LineIndex <= 0)
            return;
        LineIndex--;
        Showing = true;
        Restart();
    }

    public void GoTo(int index)
    {
        if (!InGPose || Script is null || index < 0 || index >= Script.Lines.Count)
            return;
        LineIndex = index;
        Showing = true;
        Restart();
    }

    public void ScriptChanged()
    {
        LineIndex = 0;
        Restart();
    }

    public void LineEdited() => revealed = CurrentLine?.Text.Length ?? 0;

    private void Restart() => revealed = config.TypewriterSpeed <= 0 ? float.MaxValue : 0;

    private void Reset()
    {
        Hide();
        LineIndex = 0;
        revealed = 0;
        lastLineIndex = -1;
    }

    public void SetMoveMode(bool enabled)
    {
        if (enabled && !InGPose)
            return;
        if (MoveMode == enabled)
            return;
        MoveMode = enabled;
        if (talk != null)
            talk.EnableMoving = enabled;
        if (balloon != null)
            balloon.EnableMoving = enabled;
        if (!enabled)
            StorePositions();
    }

    private void StorePositions()
    {
        if (Script is not { } script || talk == null || balloon == null)
            return;
        if (script.TalkPosition == talk.Position && script.BalloonPosition == balloon.Position)
            return;
        script.TalkPosition = talk.Position;
        script.BalloonPosition = balloon.Position;
        config.Save();
    }

    private void OnUpdate(IFramework fw)
    {
        if (talk == null || balloon == null)
            return;

        var inGPose = InGPose;
        if (wasInGPose && !inGPose)
            Reset();
        wasInGPose = inGPose;

        var line = Showing ? CurrentLine : null;
        if (line == null)
        {
            if (lastState != null)
            {
                talk.IsVisible = false;
                balloon.IsVisible = false;
                lastState = null;
            }
            return;
        }

        var script = Script!;
        if (!ReferenceEquals(script, lastScript) || LineIndex != lastLineIndex)
        {
            lastScript = script;
            lastLineIndex = LineIndex;
            if (!MoveMode)
            {
                talk.Position = script.TalkPosition;
                balloon.Position = script.BalloonPosition;
            }
        }

        if (revealed < line.Text.Length)
            revealed += (float)fw.UpdateDelta.TotalSeconds * config.TypewriterSpeed;
        var shownLength = Math.Min((int)revealed, line.Text.Length);
        var cursor = shownLength < line.Text.Length ? TalkCursor.None : line.TalkCursor;
        var state = new NodeState(
            line, line.Style, shownLength, line.Text, line.Speaker, line.FontSize,
            line.TalkBackground, cursor, line.BalloonBackground, line.BalloonColor,
            line.BalloonArrow, line.BalloonArrowX,
            line.Style == LineStyle.Talk ? script.TalkScale : script.BalloonScale);
        if (lastState == state)
            return;
        lastState = state;

        var shown = shownLength >= line.Text.Length ? line.Text : line.Text[..shownLength];

        if (line.Style == LineStyle.Talk)
        {
            balloon.IsVisible = false;
            talk.IsVisible = true;
            talk.Scale = new Vector2(script.TalkScale);
            talk.Speaker = line.Speaker;
            talk.Text = shown;
            talk.FontSize = line.FontSize;
            talk.Background = line.TalkBackground;
            talk.Cursor = cursor;
            talk.Refresh();
        }
        else
        {
            talk.IsVisible = false;
            balloon.IsVisible = true;
            balloon.Scale = new Vector2(script.BalloonScale);
            balloon.Text = shown;
            balloon.FontSize = line.FontSize;
            balloon.Background = line.BalloonBackground;
            balloon.BubbleColor = line.BalloonColor;
            balloon.ArrowVisible = line.BalloonArrow;
            balloon.ArrowX = line.BalloonArrowX;
            balloon.Refresh();
        }
    }

    public void Dispose()
    {
        disposed = true;
        if (!Ready)
            return;

        framework.Update -= OnUpdate;
        StorePositions();

        void Teardown()
        {
            controller?.Dispose();
            KamiToolKitLibrary.Dispose();
        }

        if (framework.IsInFrameworkUpdateThread)
            Teardown();
        else
            framework.RunOnFrameworkThread(Teardown).Wait();
    }
}
