namespace ControllerDeadzone;

[Serializable]
public sealed class AxisDeadzoneSettings
{
    public const int DefaultDeadZone = 15;

    public const int DefaultAntiDeadZone = 0;

    public const int DefaultMaxZone = GamepadConstants.PercentMax;

    public int DeadZone { get; set; } = DefaultDeadZone;

    public int AntiDeadZone { get; set; } = DefaultAntiDeadZone;

    public int MaxZone { get; set; } = DefaultMaxZone;

    public void ResetToDefaults()
    {
        this.DeadZone = DefaultDeadZone;
        this.AntiDeadZone = DefaultAntiDeadZone;
        this.MaxZone = DefaultMaxZone;
    }

    public void Normalize()
    {
        this.DeadZone = Math.Clamp(this.DeadZone, GamepadConstants.PercentMin, GamepadConstants.PercentMax - 1);
        this.AntiDeadZone = ClampPercent(this.AntiDeadZone);
        this.MaxZone = Math.Clamp(this.MaxZone, this.DeadZone + 1, GamepadConstants.PercentMax);
    }

    public static int ClampPercent(int value)
    {
        return Math.Clamp(value, GamepadConstants.PercentMin, GamepadConstants.PercentMax);
    }

    public static float ToRatio(int value)
    {
        return ClampPercent(value) / (float)GamepadConstants.PercentMax;
    }
}
