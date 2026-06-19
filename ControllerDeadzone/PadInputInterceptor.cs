using System.Runtime.InteropServices;
using System.Threading;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Input;

namespace ControllerDeadzone;

public unsafe sealed class PadInputInterceptor : IDisposable
{
    private static readonly TimeSpan MaxHookRetryDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan InputErrorLogInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan HookShutdownWait = TimeSpan.FromSeconds(1);
    private readonly Configuration configuration;
    private readonly IGameInteropProvider interopProvider;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly object hookGate = new();
    private Hook<PadPollDelegate>? padPollHook;
    private DateTime nextHookAttemptUtc;
    private DateTime nextRemapErrorLogUtc;
    private DateTime nextCaptureErrorLogUtc;
    private int failedHookAttempts;
    private int activePollDetours;
    private int shutdownStarted;
    private string? lastHookFailureMessage;
    private bool disposed;

    public PadInputInterceptor(
        Configuration configuration,
        IGameInteropProvider interopProvider,
        IFramework framework,
        IPluginLog log)
    {
        this.configuration = configuration;
        this.interopProvider = interopProvider;
        this.framework = framework;
        this.log = log;

        this.framework.Update += this.OnFrameworkUpdate;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint PadPollDelegate(PadDeviceInterface* padDeviceInterface);

    public bool IsHooked => this.padPollHook is { IsEnabled: true };

    public string StatusText
    {
        get
        {
            if (this.IsHooked)
                return "input hook active";

            if (this.lastHookFailureMessage != null && DateTime.UtcNow < this.nextHookAttemptUtc)
            {
                var retrySeconds = Math.Max(0d, (this.nextHookAttemptUtc - DateTime.UtcNow).TotalSeconds);
                return $"hook failed; retrying in {retrySeconds:0}s";
            }

            return "waiting for controller input";
        }
    }

    public string? LastHookFailureMessage => this.lastHookFailureMessage;

    public GamepadSnapshot LastOriginalInput { get; private set; } = GamepadSnapshot.Empty;

    public GamepadSnapshot LastRemappedInput { get; private set; } = GamepadSnapshot.Empty;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.shutdownStarted, 1) != 0)
            return;

        this.disposed = true;
        this.framework.Update -= this.OnFrameworkUpdate;

        Hook<PadPollDelegate>? hook;
        lock (this.hookGate)
        {
            hook = this.padPollHook;
        }

        if (hook != null)
        {
            this.log.Information("Shutting down PadDeviceInterface.Poll hook.");
            this.WaitForActiveDetours("before disabling");

            try
            {
                hook.Disable();
            }
            catch (Exception ex)
            {
                this.log.Verbose(ex, "Failed to disable PadDeviceInterface.Poll hook during shutdown.");
            }

            this.WaitForActiveDetours("before disposing");

            try
            {
                hook.Dispose();
            }
            catch (Exception ex)
            {
                this.log.Verbose(ex, "Failed to dispose PadDeviceInterface.Poll hook during shutdown.");
            }

            this.log.Information("Disposed PadDeviceInterface.Poll hook.");

            lock (this.hookGate)
            {
                if (ReferenceEquals(this.padPollHook, hook))
                    this.padPollHook = null;
            }
        }

        this.LastOriginalInput = GamepadSnapshot.Empty;
        this.LastRemappedInput = GamepadSnapshot.Empty;
    }

    private void WaitForActiveDetours(string stage)
    {
        if (SpinWait.SpinUntil(
                () => Volatile.Read(ref this.activePollDetours) == 0,
                HookShutdownWait))
            return;

        this.log.Verbose("Timed out waiting for active PadDeviceInterface.Poll detours {Stage}.", stage);
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (this.disposed)
            return;

        if (this.padPollHook == null && DateTime.UtcNow >= this.nextHookAttemptUtc)
            this.TryInstallHook();

        this.CaptureCurrentInput();
    }

    private void TryInstallHook()
    {
        lock (this.hookGate)
        {
            if (this.disposed || Volatile.Read(ref this.shutdownStarted) != 0)
                return;

            if (this.padPollHook != null)
                return;

            Hook<PadPollDelegate>? hook = null;
            try
            {
                if (!TryGetPadDevice(out _, out var padDeviceInterface))
                    return;

                var pollAddress = GetPollAddress(padDeviceInterface);
                if (pollAddress == 0)
                    return;

                hook = this.interopProvider.HookFromAddress<PadPollDelegate>(pollAddress, this.PadPollDetour);
                this.padPollHook = hook;
                hook.Enable();

                this.failedHookAttempts = 0;
                this.lastHookFailureMessage = null;
                this.nextHookAttemptUtc = DateTime.MinValue;
                this.log.Information("Hooked PadDeviceInterface.Poll at {Address:X}.", pollAddress);
            }
            catch (Exception ex)
            {
                hook?.Dispose();
                if (ReferenceEquals(this.padPollHook, hook))
                    this.padPollHook = null;

                var retryDelay = this.GetNextHookRetryDelay();
                this.nextHookAttemptUtc = DateTime.UtcNow + retryDelay;
                this.lastHookFailureMessage = ex.Message;
                this.log.Error(ex, "Failed to hook PadDeviceInterface.Poll. Retrying in {RetryDelaySeconds:0.0}s.", retryDelay.TotalSeconds);
            }
        }
    }

    private nint PadPollDetour(PadDeviceInterface* padDeviceInterface)
    {
        Interlocked.Increment(ref this.activePollDetours);
        nint result = 0;

        try
        {
            var hook = this.padPollHook;
            if (hook == null)
                return 0;

            try
            {
                result = hook.Original(padDeviceInterface);
            }
            catch (Exception ex)
            {
                if (ShouldLogNow(ref this.nextRemapErrorLogUtc, InputErrorLogInterval))
                    this.log.Verbose(ex, "Failed to call original PadDeviceInterface.Poll.");

                return 0;
            }

            if (Volatile.Read(ref this.shutdownStarted) != 0)
                return result;

            if (padDeviceInterface == null)
                return result;

            var padDevice = (PadDevice*)padDeviceInterface;
            ref var input = ref padDevice->GamepadInputData;

            this.LastOriginalInput = GamepadSnapshot.From(input);

            if (this.configuration.LeftStick.Enabled || this.configuration.RightStick.Enabled)
                this.ApplyDeadzone(ref input);

            this.LastRemappedInput = GamepadSnapshot.From(input);
            return result;
        }
        catch (Exception ex)
        {
            if (ShouldLogNow(ref this.nextRemapErrorLogUtc, InputErrorLogInterval))
                this.log.Verbose(ex, "Failed to remap controller input.");

            return result;
        }
        finally
        {
            Interlocked.Decrement(ref this.activePollDetours);
        }
    }

    private void CaptureCurrentInput()
    {
        try
        {
            if (!TryGetPadDevice(out var padDevice, out _))
                return;

            var snapshot = GamepadSnapshot.From(padDevice->GamepadInputData);
            if (!this.IsHooked)
                this.LastOriginalInput = snapshot;

            this.LastRemappedInput = snapshot;
        }
        catch (Exception ex)
        {
            if (ShouldLogNow(ref this.nextCaptureErrorLogUtc, InputErrorLogInterval))
                this.log.Verbose(ex, "Failed to capture current controller input.");
        }
    }

    private void ApplyDeadzone(ref GamepadInputData input)
    {
        var left = this.configuration.LeftStick.Enabled
            ? DeadzoneProcessor.Apply(new StickAxes(input.LeftStickX, input.LeftStickY), this.configuration.LeftStick)
            : new StickAxes(input.LeftStickX, input.LeftStickY);
        var right = this.configuration.RightStick.Enabled
            ? DeadzoneProcessor.Apply(new StickAxes(input.RightStickX, input.RightStickY), this.configuration.RightStick)
            : new StickAxes(input.RightStickX, input.RightStickY);

        input.LeftStickX = (int)left.X;
        input.LeftStickY = (int)left.Y;
        input.RightStickX = (int)right.X;
        input.RightStickY = (int)right.Y;

        UpdateDirectionalFields(ref input);
    }

    private static void UpdateDirectionalFields(ref GamepadInputData input)
    {
        input.LeftStickLeft = Positive(input.LeftStickX);
        input.LeftStickRight = Negative(input.LeftStickX);
        input.LeftStickUp = Positive(input.LeftStickY);
        input.LeftStickDown = Negative(input.LeftStickY);

        input.RightStickLeft = Positive(input.RightStickX);
        input.RightStickRight = Negative(input.RightStickX);
        input.RightStickUp = Positive(input.RightStickY);
        input.RightStickDown = Negative(input.RightStickY);
    }

    private static float Positive(int value) => Math.Clamp(value / GamepadConstants.StickLimit, 0f, 1f);

    private static float Negative(int value) => Math.Clamp(-value / GamepadConstants.StickLimit, 0f, 1f);

    private static bool TryGetPadDevice(out PadDevice* padDevice, out PadDeviceInterface* padDeviceInterface)
    {
        padDevice = null;
        padDeviceInterface = null;

        var manager = InputDeviceManager.Instance();
        if (manager == null || manager->PadDevice == null)
            return false;

        padDevice = manager->PadDevice;
        padDeviceInterface = (PadDeviceInterface*)padDevice;
        return true;
    }

    private static nint GetPollAddress(PadDeviceInterface* padDeviceInterface)
    {
        if (padDeviceInterface == null || padDeviceInterface->VirtualTable == null)
            return 0;

        return (nint)padDeviceInterface->VirtualTable->Poll;
    }

    private TimeSpan GetNextHookRetryDelay()
    {
        var exponent = Math.Min(this.failedHookAttempts++, 5);
        var seconds = Math.Min(Math.Pow(2, exponent), MaxHookRetryDelay.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }

    private static bool ShouldLogNow(ref DateTime nextLogUtc, TimeSpan interval)
    {
        var now = DateTime.UtcNow;
        if (now < nextLogUtc)
            return false;

        nextLogUtc = now + interval;
        return true;
    }
}
