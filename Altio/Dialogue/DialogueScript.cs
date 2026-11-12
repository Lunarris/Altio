using System;
using System.Collections.Generic;
using System.Numerics;

namespace Altio.Dialogue;

public enum LineStyle
{
    Talk,
    Balloon,
}

[Serializable]
public class DialogueLine
{
    public LineStyle Style = LineStyle.Talk;
    public string Speaker = string.Empty;
    public string Text = string.Empty;
    public uint FontSize = 14;

    public TalkBackground TalkBackground = TalkBackground.Basic;
    public TalkCursor TalkCursor = TalkCursor.Pin;

    public BalloonBackground BalloonBackground = BalloonBackground.Say;
    public BalloonColor BalloonColor = BalloonColor.Default;
    public bool BalloonArrow = true;
    public float BalloonArrowX = 49.0f;

    public DialogueLine Clone() => (DialogueLine)MemberwiseClone();
}

[Serializable]
public class DialogueScript
{
    public string Name = "New script";
    public List<DialogueLine> Lines = [];

    public Vector2 TalkPosition = new(600.0f, 600.0f);
    public float TalkScale = 1.0f;
    public Vector2 BalloonPosition = new(800.0f, 300.0f);
    public float BalloonScale = 1.0f;

    public DialogueScript Clone(string name)
    {
        var copy = (DialogueScript)MemberwiseClone();
        copy.Name = name;
        copy.Lines = Lines.ConvertAll(l => l.Clone());
        return copy;
    }
}
