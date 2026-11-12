using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace Altio.Windows;

public sealed partial class MainWindow : ThemedWindow
{
    private readonly Plugin plugin;

    private Configuration Config => plugin.Configuration;

    public MainWindow(Plugin plugin) : base("Altio##AltioMain")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(460, 420),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override bool DrawConditions() => Plugin.ClientState.IsGPosing;

    protected override void DrawContents()
    {
        using (var tabs = ImRaii.TabBar("##AltioTabs"))
        {
            if (tabs.Success)
            {
                using (var tab = ImRaii.TabItem("Dialogue"))
                    if (tab.Success)
                        DrawDialogueTab();
                using (var tab = ImRaii.TabItem("Lights"))
                    if (tab.Success)
                        DrawLightsTab();
                using (var tab = ImRaii.TabItem("Appearance"))
                    if (tab.Success)
                        DrawAppearanceTab();
            }
        }
    }

    private void DrawAppearanceTab()
    {
        SaveIf(ImGui.Checkbox("Use dark theme", ref Config.UseTheme));
        Tooltip("Off uses the standard Dalamud style.");

        using (ImRaii.Disabled(!Config.UseTheme))
        {
            Width(150);
            SaveIf(EnumCombo("Color scheme", ref Config.ThemeScheme));
        }
    }

    private const int GPoseFirstIndex = 200;

    private ICharacter[] actorCache = [];
    private DateTime actorCacheTime = DateTime.MinValue;

    private ICharacter[] CachedActors()
    {
        if (DateTime.UtcNow - actorCacheTime > TimeSpan.FromSeconds(1))
        {
            var actors = new List<ICharacter>();
            if (Plugin.ClientState.IsGPosing)
                for (var i = GPoseFirstIndex; i < Plugin.ObjectTable.Length; i++)
                    if (Plugin.ObjectTable[i] is ICharacter { Name.TextValue.Length: > 0 } actor)
                        actors.Add(actor);
            actorCache = actors.ToArray();
            actorCacheTime = DateTime.UtcNow;
        }
        return actorCache;
    }

    private void InvalidateActors() => actorCacheTime = DateTime.MinValue;

    private static bool EnumCombo<T>(string label, ref T value) where T : struct, Enum
    {
        var changed = false;
        using var combo = ImRaii.Combo(label, value.ToString());
        if (!combo.Success)
            return false;
        foreach (var option in Enum.GetValues<T>())
        {
            if (ImGui.Selectable(option.ToString(), option.Equals(value)))
            {
                value = option;
                changed = true;
            }
        }
        return changed;
    }

    private static void Disabled(string text) => ImGui.TextDisabled(text);

    private static void Width(float pixels) => ImGui.SetNextItemWidth(pixels * ImGuiHelpers.GlobalScale);

    private static void Tooltip(string text, bool whenDisabled = false)
    {
        if (ImGui.IsItemHovered(whenDisabled ? ImGuiHoveredFlags.AllowWhenDisabled : ImGuiHoveredFlags.None))
            ImGui.SetTooltip(text);
    }

    private static bool Done() => ImGui.IsItemDeactivatedAfterEdit();

    private void SaveWhenDone()
    {
        if (Done())
            Config.Save();
    }

    private void SaveIf(bool changed)
    {
        if (changed)
            Config.Save();
    }
}
