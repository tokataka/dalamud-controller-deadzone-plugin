using Dalamud.Configuration;
using Dalamud.Plugin;

namespace ControllerDeadzone;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public StickSettings LeftStick { get; set; } = new();

    public StickSettings RightStick { get; set; } = new();

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        this.Normalize();
    }

    public void Save()
    {
        this.Normalize();
        this.pluginInterface?.SavePluginConfig(this);
    }

    private void Normalize()
    {
        this.LeftStick ??= new StickSettings();
        this.RightStick ??= new StickSettings();
        this.LeftStick.Normalize();
        this.RightStick.Normalize();
    }
}
