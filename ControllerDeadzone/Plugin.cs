using ControllerDeadzone.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ControllerDeadzone;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/pdeadzone";

    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

    [PluginService]
    internal static IFramework Framework { get; private set; } = null!;

    [PluginService]
    internal static IPluginLog Log { get; private set; } = null!;

    public Plugin()
    {
        this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.Configuration.Initialize(PluginInterface);

        this.PadInputInterceptor = new PadInputInterceptor(
            this.Configuration,
            GameInteropProvider,
            Framework,
            Log);

        this.ConfigWindow = new ConfigWindow(this);
        this.WindowSystem.AddWindow(this.ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open Controller Deadzone settings.",
        });

        PluginInterface.UiBuilder.Draw += this.WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += this.ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += this.ToggleConfigUi;
    }

    public Configuration Configuration { get; }

    public PadInputInterceptor PadInputInterceptor { get; }

    public WindowSystem WindowSystem { get; } = new("ControllerDeadzone");

    private ConfigWindow ConfigWindow { get; }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= this.WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= this.ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= this.ToggleConfigUi;

        CommandManager.RemoveHandler(CommandName);
        this.WindowSystem.RemoveAllWindows();
        this.ConfigWindow.Dispose();
        this.PadInputInterceptor.Dispose();
    }

    public void ToggleConfigUi()
    {
        this.ConfigWindow.Toggle();
    }

    private void OnCommand(string command, string arguments)
    {
        this.ToggleConfigUi();
    }
}
