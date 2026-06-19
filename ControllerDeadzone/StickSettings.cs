namespace ControllerDeadzone;

[Serializable]
public sealed class StickSettings
{
    public bool Enabled { get; set; } = true;

    public DeadzoneType Type { get; set; } = DeadzoneType.Radial;

    public AxisDeadzoneSettings X { get; set; } = new();

    public AxisDeadzoneSettings Y { get; set; } = new();

    public float Deadzone { get; set; } = -1f;

    public void ResetToDefaults()
    {
        this.Type = DeadzoneType.Radial;
        this.X ??= new AxisDeadzoneSettings();
        this.Y ??= new AxisDeadzoneSettings();
        this.X.ResetToDefaults();
        this.Y.ResetToDefaults();
        this.Deadzone = -1f;
    }

    public void Normalize()
    {
        if (!Enum.IsDefined(this.Type))
            this.Type = DeadzoneType.Radial;

        this.X ??= new AxisDeadzoneSettings();
        this.Y ??= new AxisDeadzoneSettings();

        if (this.Deadzone >= 0f)
        {
            var migratedDeadZone = AxisDeadzoneSettings.ClampPercent((int)MathF.Round(this.Deadzone * GamepadConstants.PercentMax));
            this.X.DeadZone = migratedDeadZone;
            this.Y.DeadZone = migratedDeadZone;
            this.Deadzone = -1f;
        }

        this.X.Normalize();
        this.Y.Normalize();
    }

    public bool ShouldSerializeDeadzone()
    {
        return false;
    }
}
