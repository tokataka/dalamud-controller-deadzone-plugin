using System.Numerics;

namespace ControllerDeadzone;

public static class DeadzoneProcessor
{
    private const float Epsilon = 0.0001f;

    public static StickAxes Apply(StickAxes input, StickSettings settings)
    {
        settings.Normalize();

        var normalized = ClampToUnitCircle(new Vector2(input.X / GamepadConstants.StickLimit, input.Y / GamepadConstants.StickLimit));
        var output = settings.Type switch
        {
            DeadzoneType.Axial => ApplyAxial(normalized, settings),
            _ => ApplyRadial(normalized, settings),
        };

        output = ClampToUnitCircle(output);
        return new StickAxes(ToGameAxis(output.X), ToGameAxis(output.Y));
    }

    private static Vector2 ApplyRadial(Vector2 input, StickSettings settings)
    {
        var deadZone = new Vector2(
            AxisDeadzoneSettings.ToRatio(settings.X.DeadZone),
            AxisDeadzoneSettings.ToRatio(settings.Y.DeadZone));
        if (IsInsideDeadzoneEllipse(input, deadZone))
            return Vector2.Zero;

        var antiDeadZone = new Vector2(
            AxisDeadzoneSettings.ToRatio(settings.X.AntiDeadZone),
            AxisDeadzoneSettings.ToRatio(settings.Y.AntiDeadZone));
        var maxZone = new Vector2(
            AxisDeadzoneSettings.ToRatio(settings.X.MaxZone),
            AxisDeadzoneSettings.ToRatio(settings.Y.MaxZone));

        return ApplyRadialResponseCurve(input, deadZone, antiDeadZone, maxZone);
    }

    private static Vector2 ApplyAxial(Vector2 input, StickSettings settings)
    {
        var xResponse = AxialAxisResponse.From(settings.X);
        var yResponse = AxialAxisResponse.From(settings.Y);
        var output = new Vector2(
            MathF.CopySign(xResponse.ApplyMagnitude(MathF.Abs(input.X)), input.X),
            MathF.CopySign(yResponse.ApplyMagnitude(MathF.Abs(input.Y)), input.Y));

        var outputMagnitude = output.Length();
        if (outputMagnitude <= 0f)
            return output;

        var xMagnitude = MathF.Abs(input.X);
        var yMagnitude = MathF.Abs(input.Y);
        var xBoundaryInYDeadzone = GetAxisMaxBoundaryInOppositeDeadzone(xResponse, yResponse);
        var yBoundaryInXDeadzone = GetAxisMaxBoundaryInOppositeDeadzone(yResponse, xResponse);
        if ((yMagnitude <= yResponse.DeadZone && xMagnitude >= xBoundaryInYDeadzone)
            || (xMagnitude <= xResponse.DeadZone && yMagnitude >= yBoundaryInXDeadzone))
            return output / outputMagnitude;

        if (yMagnitude <= yResponse.DeadZone || xMagnitude <= xResponse.DeadZone)
            return output;

        var inputMagnitude = input.Length();
        if (inputMagnitude <= 0f)
            return output;

        var maxBoundaryRadius = FindAxialMaxBoundaryRadius(input, xResponse, yResponse, xBoundaryInYDeadzone, yBoundaryInXDeadzone);
        if (inputMagnitude >= maxBoundaryRadius)
            return output / outputMagnitude;

        return output;
    }

    private static bool IsInsideDeadzoneEllipse(Vector2 input, Vector2 deadZone)
    {
        if (deadZone == Vector2.Zero)
            return false;

        var x = GetEllipseComponent(input.X, deadZone.X);
        var y = GetEllipseComponent(input.Y, deadZone.Y);
        return (x * x) + (y * y) <= 1f;
    }

    private static float GetEllipseComponent(float value, float radius)
    {
        if (radius > 0f)
            return value / radius;

        return value == 0f ? 0f : float.PositiveInfinity;
    }

    private static Vector2 ApplyRadialResponseCurve(Vector2 input, Vector2 deadZone, Vector2 antiDeadZone, Vector2 maxZone)
    {
        var magnitude = input.Length();
        if (magnitude <= 0f)
            return input;

        var direction = input / magnitude;
        var deadZoneMagnitude = GetEllipseRadius(direction, deadZone);
        var antiDeadZoneMagnitude = GetEllipseRadius(direction, antiDeadZone);
        var maxZoneMagnitude = GetEllipseRadius(direction, maxZone);

        var outputMagnitude = RemapResponseCurve(magnitude, deadZoneMagnitude, antiDeadZoneMagnitude, maxZoneMagnitude);
        return direction * outputMagnitude;
    }

    private static float RemapResponseCurve(float magnitude, float deadZone, float antiDeadZone, float maxZone)
    {
        if (maxZone <= deadZone)
            return 1f;

        var normalized = Math.Clamp((magnitude - deadZone) / (maxZone - deadZone), 0f, 1f);
        return antiDeadZone + (normalized * (1f - antiDeadZone));
    }

    internal static float FindAxialMaxBoundaryRadius(Vector2 input, StickSettings settings)
    {
        var xResponse = AxialAxisResponse.From(settings.X);
        var yResponse = AxialAxisResponse.From(settings.Y);
        var xBoundaryInYDeadzone = GetAxisMaxBoundaryInOppositeDeadzone(xResponse, yResponse);
        var yBoundaryInXDeadzone = GetAxisMaxBoundaryInOppositeDeadzone(yResponse, xResponse);

        return FindAxialMaxBoundaryRadius(input, xResponse, yResponse, xBoundaryInYDeadzone, yBoundaryInXDeadzone);
    }

    private static float FindAxialMaxBoundaryRadius(
        Vector2 input,
        AxialAxisResponse xResponse,
        AxialAxisResponse yResponse,
        float xBoundaryInYDeadzone,
        float yBoundaryInXDeadzone)
    {
        var inputMagnitude = input.Length();
        if (inputMagnitude <= Epsilon)
            return 1f;

        var direction = input / inputMagnitude;
        var boundaryRadius = FindAxialExactMaxBoundaryRadius(direction, xResponse, yResponse);
        var xDirection = MathF.Abs(direction.X);
        var yDirection = MathF.Abs(direction.Y);

        if (xDirection > Epsilon)
        {
            var radiusAtXBoundary = xBoundaryInYDeadzone / xDirection;
            var yAtXBoundary = radiusAtXBoundary * yDirection;
            if (radiusAtXBoundary <= 1f + Epsilon && yAtXBoundary <= yResponse.DeadZone + Epsilon)
                boundaryRadius = MathF.Min(boundaryRadius, radiusAtXBoundary);
        }

        if (yDirection > Epsilon)
        {
            var radiusAtYBoundary = yBoundaryInXDeadzone / yDirection;
            var xAtYBoundary = radiusAtYBoundary * xDirection;
            if (radiusAtYBoundary <= 1f + Epsilon && xAtYBoundary <= xResponse.DeadZone + Epsilon)
                boundaryRadius = MathF.Min(boundaryRadius, radiusAtYBoundary);
        }

        return Math.Clamp(boundaryRadius, 0f, 1f);
    }

    private static float FindAxialExactMaxBoundaryRadius(Vector2 direction, AxialAxisResponse xResponse, AxialAxisResponse yResponse)
    {
        if (GetAxialPreClampOutputMagnitude(direction, xResponse, yResponse) < 1f)
            return 1f;

        var xDirection = MathF.Abs(direction.X);
        var yDirection = MathF.Abs(direction.Y);
        Span<float> breakpoints = stackalloc float[6];
        var breakpointCount = 0;

        AddBreakpoint(breakpoints, ref breakpointCount, 0f);
        AddBreakpoint(breakpoints, ref breakpointCount, 1f);
        AddAxisBreakpoints(breakpoints, ref breakpointCount, xResponse, xDirection);
        AddAxisBreakpoints(breakpoints, ref breakpointCount, yResponse, yDirection);
        SortBreakpoints(breakpoints[..breakpointCount]);

        for (var i = 0; i < breakpointCount - 1; i++)
        {
            var start = breakpoints[i];
            var end = breakpoints[i + 1];
            if (end - start <= Epsilon)
                continue;

            var sample = (start + end) * 0.5f;
            var xSegment = xResponse.GetRaySegment(xDirection, sample);
            var ySegment = yResponse.GetRaySegment(yDirection, sample);
            if (TryFindFirstRoot(xSegment, ySegment, start, end, out var root))
                return root;
        }

        return 1f;
    }

    private static float GetAxialPreClampOutputMagnitude(Vector2 input, AxialAxisResponse xResponse, AxialAxisResponse yResponse)
    {
        var x = xResponse.ApplyMagnitude(MathF.Abs(input.X));
        var y = yResponse.ApplyMagnitude(MathF.Abs(input.Y));

        return MathF.Sqrt((x * x) + (y * y));
    }

    private static float GetAxisMaxBoundaryInOppositeDeadzone(AxialAxisResponse axis, AxialAxisResponse oppositeAxis)
    {
        var targetOutputMagnitude = MathF.Sqrt(MathF.Max(0f, 1f - (oppositeAxis.AntiDeadZone * oppositeAxis.AntiDeadZone)));
        return axis.GetInputMagnitudeForOutput(targetOutputMagnitude);
    }

    private static void SortBreakpoints(Span<float> breakpoints)
    {
        for (var i = 1; i < breakpoints.Length; i++)
        {
            var value = breakpoints[i];
            var j = i - 1;
            while (j >= 0 && breakpoints[j] > value)
            {
                breakpoints[j + 1] = breakpoints[j];
                j--;
            }

            breakpoints[j + 1] = value;
        }
    }

    private static void AddAxisBreakpoints(
        Span<float> breakpoints,
        ref int breakpointCount,
        AxialAxisResponse axis,
        float directionMagnitude)
    {
        if (directionMagnitude <= Epsilon)
            return;

        AddBreakpoint(breakpoints, ref breakpointCount, axis.DeadZone / directionMagnitude);
        if (axis.MaxZone > axis.DeadZone)
            AddBreakpoint(breakpoints, ref breakpointCount, axis.MaxZone / directionMagnitude);
    }

    private static void AddBreakpoint(Span<float> breakpoints, ref int breakpointCount, float value)
    {
        if (!float.IsFinite(value) || value < -Epsilon || value > 1f + Epsilon)
            return;

        value = Math.Clamp(value, 0f, 1f);
        for (var i = 0; i < breakpointCount; i++)
        {
            if (MathF.Abs(breakpoints[i] - value) <= Epsilon)
                return;
        }

        breakpoints[breakpointCount++] = value;
    }

    private static bool TryFindFirstRoot(
        AxisRaySegment xSegment,
        AxisRaySegment ySegment,
        float start,
        float end,
        out float root)
    {
        if (GetMagnitudeSquared(xSegment, ySegment, start) >= 1f - Epsilon)
        {
            root = start;
            return true;
        }

        var a = (xSegment.Slope * xSegment.Slope) + (ySegment.Slope * ySegment.Slope);
        var b = 2f * ((xSegment.Slope * xSegment.Intercept) + (ySegment.Slope * ySegment.Intercept));
        var c = (xSegment.Intercept * xSegment.Intercept) + (ySegment.Intercept * ySegment.Intercept) - 1f;
        if (a <= Epsilon)
        {
            root = 0f;
            return false;
        }

        var discriminant = (b * b) - (4f * a * c);
        if (discriminant < -Epsilon)
        {
            root = 0f;
            return false;
        }

        discriminant = MathF.Max(0f, discriminant);
        var sqrtDiscriminant = MathF.Sqrt(discriminant);
        var denominator = 2f * a;
        var root1 = (-b - sqrtDiscriminant) / denominator;
        var root2 = (-b + sqrtDiscriminant) / denominator;

        if (IsInsideInterval(root1, start, end))
        {
            root = Math.Clamp(root1, start, end);
            return true;
        }

        if (IsInsideInterval(root2, start, end))
        {
            root = Math.Clamp(root2, start, end);
            return true;
        }

        root = 0f;
        return false;
    }

    private static float GetMagnitudeSquared(AxisRaySegment xSegment, AxisRaySegment ySegment, float radius)
    {
        var x = xSegment.Evaluate(radius);
        var y = ySegment.Evaluate(radius);
        return (x * x) + (y * y);
    }

    private static bool IsInsideInterval(float value, float start, float end)
    {
        return value >= start - Epsilon && value <= end + Epsilon;
    }

    private static float GetEllipseRadius(Vector2 direction, Vector2 radius)
    {
        if (radius == Vector2.Zero)
            return 0f;

        var x = GetEllipseComponent(direction.X, radius.X);
        var y = GetEllipseComponent(direction.Y, radius.Y);
        var denominator = MathF.Sqrt((x * x) + (y * y));
        return denominator > 0f ? 1f / denominator : 0f;
    }

    private static Vector2 ClampToUnitCircle(Vector2 value)
    {
        var magnitude = value.Length();
        if (magnitude <= 1f)
            return value;

        return value / magnitude;
    }

    private static float ToGameAxis(float value)
    {
        return MathF.Round(value * GamepadConstants.StickLimit);
    }

    private readonly record struct AxialAxisResponse(float DeadZone, float AntiDeadZone, float MaxZone)
    {
        public static AxialAxisResponse From(AxisDeadzoneSettings settings)
        {
            return new AxialAxisResponse(
                AxisDeadzoneSettings.ToRatio(settings.DeadZone),
                AxisDeadzoneSettings.ToRatio(settings.AntiDeadZone),
                AxisDeadzoneSettings.ToRatio(settings.MaxZone));
        }

        public float ApplyMagnitude(float magnitude)
        {
            if (magnitude <= this.DeadZone)
                return 0f;

            if (this.MaxZone <= this.DeadZone)
                return 1f;

            var normalized = Math.Clamp((magnitude - this.DeadZone) / (this.MaxZone - this.DeadZone), 0f, 1f);
            return this.AntiDeadZone + (normalized * (1f - this.AntiDeadZone));
        }

        public float GetInputMagnitudeForOutput(float outputMagnitude)
        {
            if (this.MaxZone <= this.DeadZone || outputMagnitude <= this.AntiDeadZone)
                return this.DeadZone;

            if (outputMagnitude >= 1f)
                return this.MaxZone;

            var normalized = (outputMagnitude - this.AntiDeadZone) / (1f - this.AntiDeadZone);
            return this.DeadZone + (Math.Clamp(normalized, 0f, 1f) * (this.MaxZone - this.DeadZone));
        }

        public AxisRaySegment GetRaySegment(float directionMagnitude, float radius)
        {
            if (directionMagnitude <= 0f)
                return AxisRaySegment.Zero;

            var inputMagnitude = directionMagnitude * radius;
            if (inputMagnitude <= this.DeadZone)
                return AxisRaySegment.Zero;

            if (this.MaxZone <= this.DeadZone || inputMagnitude >= this.MaxZone)
                return AxisRaySegment.One;

            var slope = (1f - this.AntiDeadZone) / (this.MaxZone - this.DeadZone);
            return new AxisRaySegment(
                slope * directionMagnitude,
                this.AntiDeadZone - (slope * this.DeadZone));
        }
    }

    private readonly record struct AxisRaySegment(float Slope, float Intercept)
    {
        public static AxisRaySegment Zero => new(0f, 0f);

        public static AxisRaySegment One => new(0f, 1f);

        public float Evaluate(float radius)
        {
            return (this.Slope * radius) + this.Intercept;
        }
    }
}
