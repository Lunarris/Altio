using System;
using System.Linq;
using System.Numerics;
using Altio.Dialogue;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Altio.Windows;

public sealed partial class MainWindow
{
    private static readonly uint[] FontSizes = [8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24];

    private void DrawDialogueTab()
    {
        var dialogue = plugin.Dialogue;
        if (!dialogue.Ready)
        {
            Disabled(dialogue.InitError is null ? "Starting the dialogue overlay..." : $"Dialogue overlay failed: {dialogue.InitError}");
            return;
        }

        DrawScriptPicker();
        var script = dialogue.Script;
        if (script == null)
            return;

        ImGui.Separator();
        DrawPlayback(script);
        ImGui.Separator();
        DrawLineList(script);
        ImGui.Separator();
        DrawLineEditor();
    }

    private void SwitchScript(int index)
    {
        Config.ActiveScript = Math.Clamp(index, 0, Config.Scripts.Count - 1);
        Config.Save();
        plugin.Dialogue.ScriptChanged();
    }

    private void DrawScriptPicker()
    {
        var active = Math.Clamp(Config.ActiveScript, 0, Config.Scripts.Count - 1);
        var script = Config.Scripts[active];

        Width(220);
        using (var combo = ImRaii.Combo("##Script", script.Name))
        {
            if (combo.Success)
                for (var i = 0; i < Config.Scripts.Count; i++)
                    if (ImGui.Selectable($"{Config.Scripts[i].Name}##{i}", i == active) && i != active)
                        SwitchScript(i);
        }

        ImGui.SameLine();
        if (ImGuiComponents.IconButton("##AddScript", FontAwesomeIcon.Plus))
        {
            Config.Scripts.Add(new DialogueScript { Name = $"Script {Config.Scripts.Count + 1}" });
            SwitchScript(Config.Scripts.Count - 1);
        }
        Tooltip("New script");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton("##CopyScript", FontAwesomeIcon.Copy))
        {
            Config.Scripts.Add(script.Clone($"{script.Name} copy"));
            SwitchScript(Config.Scripts.Count - 1);
        }
        Tooltip("Duplicate script");

        ImGui.SameLine();
        using (ImRaii.Disabled(Config.Scripts.Count <= 1 || !ImGui.GetIO().KeyCtrl))
        {
            if (ImGuiComponents.IconButton("##DeleteScript", FontAwesomeIcon.Trash))
            {
                Config.Scripts.RemoveAt(active);
                SwitchScript(active);
            }
        }
        Tooltip("Delete script (hold Ctrl)", true);

        Width(220);
        ImGui.InputText("Name##ScriptName", ref script.Name, 64);
        SaveWhenDone();
    }

    private void DrawPlayback(DialogueScript script)
    {
        var dialogue = plugin.Dialogue;

        using (ImRaii.Disabled(script.Lines.Count == 0))
        {
            using (ImRaii.Disabled(dialogue.LineIndex <= 0))
                if (ImGuiComponents.IconButton("##Prev", FontAwesomeIcon.StepBackward))
                    dialogue.Previous();
            ImGui.SameLine();
            if (ImGui.Button(dialogue.Showing ? "Hide" : "Show", new Vector2(70 * ImGuiHelpers.GlobalScale, 0)))
                dialogue.Toggle();
            ImGui.SameLine();
            if (ImGuiComponents.IconButton("##Next", FontAwesomeIcon.StepForward))
                dialogue.Next();
            ImGui.SameLine();
            ImGui.TextUnformatted($"Line {Math.Min(dialogue.LineIndex + 1, script.Lines.Count)} / {script.Lines.Count}");

            ImGui.SameLine();
            var move = dialogue.MoveMode;
            if (ImGui.Checkbox("Drag to place", ref move))
            {
                if (move && !dialogue.Showing)
                    dialogue.Show();
                dialogue.SetMoveMode(move);
            }
            Tooltip("Drag the dialogue boxes on screen. Untick to keep the spot.");
        }

        Width(160);
        ImGui.SliderFloat("Text speed", ref Config.TypewriterSpeed, 0, 120, Config.TypewriterSpeed <= 0 ? "Instant" : "%.0f chars/s");
        SaveWhenDone();
        ImGui.SameLine();
        SaveIf(ImGui.Checkbox("Hide after last line", ref Config.HideAtEnd));

        ImGui.SameLine();
        SaveIf(ImGui.Checkbox("Loop", ref Config.LoopScript));
        Tooltip("After the last line, Next goes back to the first");

        Width(160);
        ImGui.SliderFloat("Box size", ref script.TalkScale, 0.5f, 2.0f, "%.2fx");
        SaveWhenDone();
        ImGui.SameLine();
        Width(160);
        ImGui.SliderFloat("Bubble size", ref script.BalloonScale, 0.5f, 3.0f, "%.2fx");
        SaveWhenDone();

    }

    private void DrawLineList(DialogueScript script)
    {
        var dialogue = plugin.Dialogue;
        int? moveFrom = null, moveTo = null, remove = null;

        using (var child = ImRaii.Child("##Lines", new Vector2(0, 150 * ImGuiHelpers.GlobalScale), true))
        {
            if (child.Success)
            {
                var buttons = 3 * (ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.X);
                for (var i = 0; i < script.Lines.Count; i++)
                {
                    var line = script.Lines[i];
                    using var id = ImRaii.PushId(i);

                    var label = line.Style == LineStyle.Talk && !string.IsNullOrEmpty(line.Speaker)
                        ? $"{i + 1}. {line.Speaker}: {Preview(line.Text)}"
                        : $"{i + 1}. ({line.Style}) {Preview(line.Text)}";

                    if (ImGui.Selectable(label, i == dialogue.LineIndex, ImGuiSelectableFlags.None, new Vector2(ImGui.GetContentRegionAvail().X - buttons, 0)))
                        dialogue.GoTo(i);

                    ImGui.SameLine();
                    using (ImRaii.Disabled(i == 0))
                        if (ImGuiComponents.IconButton("##Up", FontAwesomeIcon.ArrowUp)) { moveFrom = i; moveTo = i - 1; }
                    ImGui.SameLine();
                    using (ImRaii.Disabled(i == script.Lines.Count - 1))
                        if (ImGuiComponents.IconButton("##Down", FontAwesomeIcon.ArrowDown)) { moveFrom = i; moveTo = i + 1; }
                    ImGui.SameLine();
                    if (ImGuiComponents.IconButton("##Remove", FontAwesomeIcon.Times)) remove = i;
                }
            }
        }

        if (moveFrom is { } from && moveTo is { } to)
        {
            (script.Lines[from], script.Lines[to]) = (script.Lines[to], script.Lines[from]);
            Config.Save();
            if (dialogue.LineIndex == from)
                dialogue.GoTo(to);
        }
        if (remove is { } r)
        {
            script.Lines.RemoveAt(r);
            Config.Save();
            if (script.Lines.Count == 0)
                dialogue.Hide();
            else
                dialogue.GoTo(Math.Min(dialogue.LineIndex, script.Lines.Count - 1));
        }

        if (script.Lines.Count == 0)
            Disabled("No lines yet. Add one to start.");
        if (ImGui.Button("Add line"))
        {
            var template = dialogue.CurrentLine?.Clone() ?? new DialogueLine();
            template.Text = string.Empty;
            var at = Math.Clamp(dialogue.LineIndex + 1, 0, script.Lines.Count);
            script.Lines.Insert(at, template);
            Config.Save();
            dialogue.GoTo(at);
        }
    }

    private static string Preview(string text)
    {
        var flat = text.Replace('\n', ' ');
        return flat.Length > 48 ? flat[..48] + "..." : flat;
    }

    private string[] speakerNames = [];
    private DateTime speakerNamesTime = DateTime.MinValue;

    private string[] GPoseActorNames()
    {
        if (DateTime.UtcNow - speakerNamesTime < TimeSpan.FromSeconds(1))
            return speakerNames;
        speakerNamesTime = DateTime.UtcNow;

        speakerNames = CachedActors().Select(a => a.Name.TextValue).Distinct().ToArray();
        return speakerNames;
    }

    private bool DrawSpeakerPicker(DialogueLine line)
    {
        var picked = false;
        using (ImRaii.Disabled(!Plugin.ClientState.IsGPosing))
        {
            if (ImGuiComponents.IconButton("##SpeakerPick", FontAwesomeIcon.Users))
            {
                speakerNamesTime = DateTime.MinValue;
                InvalidateActors();
                ImGui.OpenPopup("##SpeakerPopup");
            }
        }
        Tooltip(Plugin.ClientState.IsGPosing ? "Pick a GPose actor's name" : "Actor names are available in GPose", true);

        using var popup = ImRaii.Popup("##SpeakerPopup");
        if (!popup.Success)
            return false;

        var names = GPoseActorNames();
        if (names.Length == 0)
            Disabled("No actors found in GPose.");
        foreach (var name in names)
        {
            if (ImGui.Selectable(name, name == line.Speaker))
            {
                line.Speaker = name;
                picked = true;
            }
        }
        return picked;
    }

    private void DrawLineEditor()
    {
        var dialogue = plugin.Dialogue;
        if (dialogue.CurrentLine is not { } line)
            return;

        var changed = false;
        var commit = false;

        Width(140);
        commit |= EnumCombo("Style", ref line.Style);

        ImGui.SameLine();
        Width(80);
        using (var combo = ImRaii.Combo("Font size", line.FontSize.ToString()))
        {
            if (combo.Success)
                foreach (var size in FontSizes)
                    if (ImGui.Selectable(size.ToString(), size == line.FontSize)) { line.FontSize = size; commit = true; }
        }


        if (line.Style == LineStyle.Talk)
        {
            Width(220);
            changed |= ImGui.InputTextWithHint("##Speaker", "Name shown on the box", ref line.Speaker, 64);
            commit |= Done();
            ImGui.SameLine();
            commit |= DrawSpeakerPicker(line);
            ImGui.SameLine();
            ImGui.TextUnformatted("Speaker");

            Width(140);
            commit |= EnumCombo("Box##TalkBg", ref line.TalkBackground);
            ImGui.SameLine();
            Width(100);
            commit |= EnumCombo("Prompt", ref line.TalkCursor);
        }
        else
        {
            Width(140);
            commit |= EnumCombo("Bubble##BalloonBg", ref line.BalloonBackground);
            ImGui.SameLine();
            Width(120);
            commit |= EnumCombo("Colour", ref line.BalloonColor);

            commit |= ImGui.Checkbox("Tail", ref line.BalloonArrow);
            if (line.BalloonArrow)
            {
                ImGui.SameLine();
                Width(160);
                changed |= ImGui.SliderFloat("Tail position", ref line.BalloonArrowX, 32.0f, 130.0f, "%.0f");
                commit |= Done();
            }
        }

        var boxHeight = Math.Max(70 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().Y - ImGui.GetStyle().ItemSpacing.Y);
        changed |= ImGui.InputTextMultiline("##LineText", ref line.Text, 1024, new Vector2(-1, boxHeight));
        if (line.Text.Length == 0 && !ImGui.IsItemActive())
        {
            var min = ImGui.GetItemRectMin() + ImGui.GetStyle().FramePadding;
            ImGui.GetWindowDrawList().AddText(min, ImGui.GetColorU32(ImGuiCol.TextDisabled), "Type the line here");
        }
        commit |= Done();

        if (changed || commit)
            dialogue.LineEdited();
        SaveIf(commit);
    }
}
