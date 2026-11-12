using System;
using System.Collections.Generic;
using Altio.Dialogue;
using Dalamud.Configuration;

namespace Altio;

public enum ThemeScheme
{
    Midnight,
    Slate,
    Ember,
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public List<DialogueScript> Scripts = [];
    public int ActiveScript;
    public float TypewriterSpeed = 40.0f;
    public bool HideAtEnd = true;
    public bool LoopScript;
    public bool UseTheme = false;
    public ThemeScheme ThemeScheme = ThemeScheme.Midnight;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
