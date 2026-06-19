using FFXIVClientStructs.FFXIV.Client.System.Input;

namespace ControllerDeadzone;

public readonly record struct StickAxes(float X, float Y);

public readonly record struct GamepadSnapshot(StickAxes LeftStick, StickAxes RightStick)
{
    public static readonly GamepadSnapshot Empty = new(new StickAxes(0, 0), new StickAxes(0, 0));

    public static GamepadSnapshot From(in GamepadInputData data)
    {
        return new GamepadSnapshot(
            new StickAxes(data.LeftStickX, data.LeftStickY),
            new StickAxes(data.RightStickX, data.RightStickY));
    }
}
