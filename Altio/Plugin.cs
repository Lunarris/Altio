using Altio.Dialogue;
using Altio.Lights;
using Altio.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Altio;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/altio";

    public Configuration Configuration { get; }
    public LightService Lights { get; }
    public DialogueService Dialogue { get; }

    public readonly WindowSystem WindowSystem = new("Altio");
    private readonly MainWindow mainWindow;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (Configuration.Scripts.Count == 0)
            Configuration.Scripts.Add(new DialogueScript());
        if (Configuration.Version < 2)
        {
            foreach (var script in Configuration.Scripts)
                script.Lines.RemoveAll(l => l is { Speaker: "Speaker", Text: "New line..." });
            Configuration.Version = 2;
            Configuration.Save();
        }

        Lights = new LightService(SigScanner, Framework, ClientState);
        Dialogue = new DialogueService(PluginInterface, Framework, Configuration);

        mainWindow = new MainWindow(this);
        WindowSystem.AddWindow(mainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Altio (GPose only).",
        });

        PluginInterface.UiBuilder.DisableGposeUiHide = true;
        PluginInterface.UiBuilder.Draw += DrawUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleMainUi;

        wasInGPose = ClientState.IsGPosing;
        Framework.Update += OnFrameworkUpdate;
    }

    private bool wasInGPose;

    private void OnFrameworkUpdate(IFramework framework)
    {
        var inGPose = ClientState.IsGPosing;
        if (inGPose != wasInGPose)
            mainWindow.IsOpen = inGPose;
        else if (!inGPose && mainWindow.IsOpen)
            mainWindow.IsOpen = false;
        wasInGPose = inGPose;
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= DrawUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleMainUi;
        CommandManager.RemoveHandler(CommandName);

        WindowSystem.RemoveAllWindows();

        Dialogue.Dispose();
        Lights.Dispose();
    }

    private void OnCommand(string command, string args) => ToggleMainUi();

    public void ToggleMainUi()
    {
        if (!ClientState.IsGPosing)
        {
            ChatGui.PrintError("Altio only works in GPose.");
            return;
        }
        mainWindow.Toggle();
    }

    private void DrawUi()
    {
        Theme.Update(Configuration);
        WindowSystem.Draw();
    }
}
