using System;
using System.Numerics;
using Altio.Lights;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using RenderLight = FFXIVClientStructs.FFXIV.Client.Graphics.Render.Light;

namespace Altio.Windows;

public sealed unsafe partial class MainWindow
{
    private static readonly (string Name, Vector3 Rgb)[] LightPresets =
    [
        ("Candle", new Vector3(1.00f, 0.58f, 0.25f)),
        ("Warm", new Vector3(1.00f, 0.80f, 0.60f)),
        ("Daylight", new Vector3(1.00f, 0.97f, 0.92f)),
        ("Moonlight", new Vector3(0.62f, 0.72f, 1.00f)),
        ("Aether", new Vector3(0.45f, 0.90f, 1.00f)),
        ("Crimson", new Vector3(1.00f, 0.20f, 0.25f)),
    ];

    private static readonly (LightShape Shape, string Name)[] LightShapes =
    [
        (LightShape.PointLight, "Point"),
        (LightShape.SpotLight, "Spot"),
        (LightShape.FlatLight, "Area"),
        (LightShape.WorldLight, "World (outdoors only)"),
    ];

    private AltioLight? goboTarget;
    private string goboSearch = string.Empty;
    private string goboCustomPath = string.Empty;

    private void DrawLightsTab()
    {
        var lights = plugin.Lights;
        if (!Plugin.ClientState.IsGPosing)
        {
            Disabled("Enter GPose to edit lights.");
            return;
        }

        lights.ScanScene();
        Disabled("Create, move and aim lights in Brio or the GPose menu. Edit everything else here.");

        if (lights.Lights.Count == 0)
            Disabled("No lights yet. Add one in Brio or the GPose menu.");
        for (var i = 0; i < lights.Lights.Count; i++)
        {
            var light = lights.Lights[i];
            if (!light.IsValid)
                continue;
            using var id = ImRaii.PushId(i);
            DrawLight(light);
        }
    }

    private void DrawLight(AltioLight light)
    {
        var lights = plugin.Lights;
        var render = light.Render;

        var visible = light.Visible;
        if (ImGui.Checkbox("##Visible", ref visible))
            light.Visible = visible;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Visible");
        ImGui.SameLine();

        if (!ImGui.CollapsingHeader($"{light.Name}###Header"))
            return;

        using var indent = ImRaii.PushIndent();

        var dirty = false;

        var shapeName = Array.Find(LightShapes, s => s.Shape == render->LightShape).Name ?? render->LightShape.ToString();
        ImGui.SetNextItemWidth(180 * ImGuiHelpers.GlobalScale);
        using (var combo = ImRaii.Combo("Type", shapeName))
        {
            if (combo.Success)
                foreach (var (shape, name) in LightShapes)
                    if (ImGui.Selectable(name, shape == render->LightShape))
                        lights.SetShape(light, shape);
        }

        var rgb = light.Rgb;
        if (ImGui.ColorEdit3("Colour", ref rgb, ImGuiColorEditFlags.Hdr | ImGuiColorEditFlags.NoInputs)) { light.Rgb = rgb; dirty = true; }
        ImGui.SameLine();
        foreach (var (name, presetRgb) in LightPresets)
        {
            if (ImGui.ColorButton($"{name}##Preset", new Vector4(presetRgb, 1)))
            {
                light.Rgb = presetRgb;
                dirty = true;
            }
            ImGui.SameLine();
        }
        ImGui.NewLine();

        var intensity = render->Intensity;
        if (ImGui.DragFloat("Intensity", ref intensity, 0.01f, 0, 100, "%.2f")) { render->Intensity = intensity; dirty = true; }
        dirty |= ImGui.DragFloat("Range", ref render->Range, 0.1f, 0, 1000, "%.1f");
        ImGui.SetNextItemWidth(180 * ImGuiHelpers.GlobalScale);
        dirty |= EnumCombo("Falloff type", ref render->FalloffType);
        dirty |= ImGui.DragFloat("Falloff", ref render->FalloffFactor, 0.01f, 0, 100, "%.2f");

        if (render->LightShape == LightShape.SpotLight)
        {
            dirty |= ImGui.SliderFloat("Cone angle", ref render->SpotLightAngleDegrees, 0, 180, "%.0f°");
            dirty |= ImGui.SliderFloat("Edge softness", ref render->AngularFalloffDegrees, 0, 180, "%.0f°");
        }
        else if (render->LightShape == LightShape.FlatLight)
        {
            Vector2 skew = render->FlatLightSkewAngleDegrees;
            if (ImGui.SliderFloat2("Area skew", ref skew, -90, 90, "%.0f°")) { render->FlatLightSkewAngleDegrees = skew; dirty = true; }
            dirty |= ImGui.SliderFloat("Edge softness", ref render->AngularFalloffDegrees, 0, 180, "%.0f°");
        }

        DrawGoboRow(light);

        if (ImGui.TreeNode("Shadows and reflections"))
        {
            dirty |= FlagBox(render, "Reflections", LightFlags.SpecularHighlights);
            ImGui.SameLine();
            dirty |= FlagBox(render, "Dynamic shadows", LightFlags.DynamicShadows);
            dirty |= FlagBox(render, "Character shadows", LightFlags.CharacterShadows);
            ImGui.SameLine();
            dirty |= FlagBox(render, "Object shadows", LightFlags.ObjectShadows);

            dirty |= ImGui.DragFloat("Shadow near", ref render->ShadowPlaneNear, 0.01f, 0, 100, "%.2f");
            dirty |= ImGui.DragFloat("Shadow far", ref render->ShadowPlaneFar, 0.1f, 0, 1000, "%.1f");
            dirty |= ImGui.DragFloat("Character shadow range", ref render->CharacterShadowRange, 0.1f, 0, 1000, "%.1f");
            ImGui.TreePop();
        }

        if (dirty)
            lights.Apply(light);
    }

    private void DrawGoboRow(AltioLight light)
    {
        var lights = plugin.Lights;
        if (!lights.GobosReady)
        {
            Disabled("Light textures are unavailable (signatures not found).");
            return;
        }

        using (ImRaii.Disabled(!light.SupportsGobo))
        {
            if (ImGuiComponents.IconButton("##Gobo", FontAwesomeIcon.Image))
            {
                goboTarget = light;
                goboSearch = string.Empty;
                ImGui.OpenPopup("##GoboPicker");
            }
            ImGui.SameLine();
            using (ImRaii.Disabled(light.Gobo == null && !light.HasTexture))
                if (ImGuiComponents.IconButton("##ClearGobo", FontAwesomeIcon.Times))
                {
                    lights.ClearGobo(light);
                    lights.Apply(light);
                }
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Remove texture");
        ImGui.SameLine();
        ImGui.TextUnformatted(light.SupportsGobo
            ? $"Texture: {light.Gobo?.Name ?? (light.HasTexture ? "set elsewhere" : "none")}"
            : "Texture: spot and area lights only");

        DrawGoboPopup();
    }

    private void DrawGoboPopup()
    {
        using var popup = ImRaii.Popup("##GoboPicker");
        if (!popup.Success || goboTarget is not { IsValid: true } light)
            return;

        var listSize = new Vector2(420, 360) * ImGuiHelpers.GlobalScale;
        ImGui.SetNextItemWidth(listSize.X);
        ImGui.InputTextWithHint("##GoboSearch", "Search textures", ref goboSearch, 64);

        var thumb = new Vector2(48 * ImGuiHelpers.GlobalScale);
        using (var child = ImRaii.Child("##GoboList", listSize, true))
        {
            if (child.Success)
            {
                foreach (var gobo in Gobos.All)
                {
                    if (goboSearch.Length > 0 && !gobo.Name.Contains(goboSearch, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (GoboEntry(gobo, thumb, ReferenceEquals(light.Gobo, gobo)))
                    {
                        plugin.Lights.SetGobo(light, gobo);
                        ImGui.CloseCurrentPopup();
                    }
                }
            }
        }

        var applyWidth = 70 * ImGuiHelpers.GlobalScale;
        ImGui.SetNextItemWidth(listSize.X - applyWidth - ImGui.GetStyle().ItemSpacing.X);
        ImGui.InputTextWithHint("##GoboPath", "Any game .tex path", ref goboCustomPath, 256);
        ImGui.SameLine();
        using (ImRaii.Disabled(!goboCustomPath.EndsWith(".tex", StringComparison.OrdinalIgnoreCase)))
        {
            if (ImGui.Button("Apply", new Vector2(applyWidth, 0)))
            {
                plugin.Lights.SetGobo(light, new Gobo(goboCustomPath, goboCustomPath));
                ImGui.CloseCurrentPopup();
            }
        }
    }

    private static bool GoboEntry(Gobo gobo, Vector2 thumb, bool selected)
    {
        var start = ImGui.GetCursorPos();
        var clicked = ImGui.Selectable($"##{gobo.Path}", selected, ImGuiSelectableFlags.None, new Vector2(0, thumb.Y));
        if (!ImGui.IsItemVisible())
            return clicked;

        ImGui.SetCursorPos(start);
        var texture = Plugin.TextureProvider.GetFromGame(gobo.Path).GetWrapOrEmpty();
        ImGui.Image(texture.Handle, thumb);
        ImGui.SameLine();
        using (ImRaii.Group())
        {
            ImGui.TextUnformatted(gobo.Name);
            ImGui.TextDisabled(gobo.Path);
        }
        return clicked;
    }

    private static bool FlagBox(RenderLight* render, string label, LightFlags flag)
    {
        var on = render->LightFlags.HasFlag(flag);
        if (!ImGui.Checkbox(label, ref on))
            return false;
        render->LightFlags = on ? render->LightFlags | flag : render->LightFlags & ~flag;
        return true;
    }
}
