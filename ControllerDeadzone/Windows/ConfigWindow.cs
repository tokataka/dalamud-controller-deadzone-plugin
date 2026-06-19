using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ControllerDeadzone.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private static readonly string[] DeadzoneTypes = ["Radial", "Axial"];
    private static readonly Vector2 FixedWindowSize = new(620, 560);
    private const float TypeComboWidth = 120f;
    private const int AxisSettingRows = 6;
    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin)
        : base("Controller Deadzone###ControllerDeadzoneConfig")
    {
        this.plugin = plugin;
        this.Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = FixedWindowSize,
            MaximumSize = FixedWindowSize,
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var configuration = this.plugin.Configuration;
        var padInputInterceptor = this.plugin.PadInputInterceptor;

        var statusColor = padInputInterceptor.IsHooked
            ? new Vector4(0.35f, 0.85f, 0.45f, 1f)
            : padInputInterceptor.LastHookFailureMessage != null
                ? new Vector4(0.95f, 0.35f, 0.35f, 1f)
                : new Vector4(0.65f, 0.68f, 0.72f, 1f);
        ImGui.TextColored(statusColor, padInputInterceptor.StatusText);
        if (padInputInterceptor.LastHookFailureMessage is { } failureMessage && ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(failureMessage);
            ImGui.EndTooltip();
        }

        ImGui.Separator();

        this.DrawStickPanel(
            "Left stick",
            "LS",
            configuration.LeftStick,
            padInputInterceptor.LastOriginalInput.LeftStick,
            padInputInterceptor.LastRemappedInput.LeftStick);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        this.DrawStickPanel(
            "Right stick",
            "RS",
            configuration.RightStick,
            padInputInterceptor.LastOriginalInput.RightStick,
            padInputInterceptor.LastRemappedInput.RightStick);
    }

    private void DrawStickPanel(
        string label,
        string id,
        StickSettings settings,
        StickAxes original,
        StickAxes remapped)
    {
        settings.Normalize();

        if (ImGui.BeginTable($"{id}Layout", 2, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Visualizer", ImGuiTableColumnFlags.WidthFixed, GetVisualizerSize());
            ImGui.TableSetupColumn("Controls", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextColumn();
            this.DrawStickHeader(id, label, settings);
            DrawStickVisualizer($"{id}Visualizer", settings, original, remapped);
            ImGui.TableNextColumn();
            if (settings.Enabled)
            {
                AddVerticalControlOffset();
                this.DrawStickControls(id, settings);
            }

            ImGui.EndTable();
        }
    }

    private void DrawStickHeader(string id, string label, StickSettings settings)
    {
        var enabled = settings.Enabled;
        if (ImGui.Checkbox($"{label}##{id}Enabled", ref enabled))
        {
            settings.Enabled = enabled;
            this.plugin.Configuration.Save();
        }
    }

    private void DrawStickControls(string id, StickSettings settings)
    {
        var type = (int)settings.Type;
        ImGui.SetNextItemWidth(TypeComboWidth);
        if (ImGui.Combo($"##{id}Type", ref type, DeadzoneTypes, DeadzoneTypes.Length))
        {
            settings.Type = (DeadzoneType)type;
            this.plugin.Configuration.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button($"Reset##{id}Reset"))
        {
            settings.ResetToDefaults();
            this.plugin.Configuration.Save();
        }

        this.DrawAxisSettings(id, settings);
    }

    private static void AddVerticalControlOffset()
    {
        var style = ImGui.GetStyle();
        var availableHeight = ImGui.GetTextLineHeightWithSpacing() + GetVisualizerSize();
        var controlsHeight = ImGui.GetFrameHeight()
                             + style.ItemSpacing.Y
                             + (ImGui.GetFrameHeightWithSpacing() * AxisSettingRows);
        var offset = MathF.Max(0f, (availableHeight - controlsHeight) * 0.5f);
        if (offset > 0f)
            ImGui.Dummy(new Vector2(1f, offset));
    }

    private void DrawAxisSettings(string id, StickSettings settings)
    {
        if (!ImGui.BeginTable($"{id}AxisSettings", 2, ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Anti-Dead Y").X + 8f);
        ImGui.TableSetupColumn("Slider", ImGuiTableColumnFlags.WidthStretch);

        this.DrawAxisSettingRow("Dead Zone X", $"{id}DeadX", settings.X, static axis => axis.DeadZone, static (axis, value) => axis.DeadZone = value, maximum: AxisDeadzoneSettings.ToRatio(settings.X.MaxZone - 1));
        this.DrawAxisSettingRow("Dead Zone Y", $"{id}DeadY", settings.Y, static axis => axis.DeadZone, static (axis, value) => axis.DeadZone = value, maximum: AxisDeadzoneSettings.ToRatio(settings.Y.MaxZone - 1));
        this.DrawAxisSettingRow("Max Zone X", $"{id}MaxX", settings.X, static axis => axis.MaxZone, static (axis, value) => axis.MaxZone = value, AxisDeadzoneSettings.ToRatio(settings.X.DeadZone + 1));
        this.DrawAxisSettingRow("Max Zone Y", $"{id}MaxY", settings.Y, static axis => axis.MaxZone, static (axis, value) => axis.MaxZone = value, AxisDeadzoneSettings.ToRatio(settings.Y.DeadZone + 1));
        this.DrawAxisSettingRow("Anti-Dead X", $"{id}AntiX", settings.X, static axis => axis.AntiDeadZone, static (axis, value) => axis.AntiDeadZone = value);
        this.DrawAxisSettingRow("Anti-Dead Y", $"{id}AntiY", settings.Y, static axis => axis.AntiDeadZone, static (axis, value) => axis.AntiDeadZone = value);

        ImGui.EndTable();
    }

    private void DrawAxisSettingRow(
        string label,
        string id,
        AxisDeadzoneSettings settings,
        Func<AxisDeadzoneSettings, int> getValue,
        Action<AxisDeadzoneSettings, int> setValue,
        float minimum = 0f,
        float maximum = 1f)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(label);
        ImGui.TableNextColumn();

        var value = AxisDeadzoneSettings.ToRatio(getValue(settings));
        var minimumLabel = FormatRatio(minimum);
        var maximumLabel = FormatRatio(maximum);
        var style = ImGui.GetStyle();
        var labelColor = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var minimumLabelWidth = ImGui.CalcTextSize(minimumLabel).X;
        var maximumLabelWidth = ImGui.CalcTextSize(maximumLabel).X;
        var sliderWidth = MathF.Max(
            ImGui.GetFrameHeight() * 2f,
            availableWidth - minimumLabelWidth - maximumLabelWidth - (style.ItemSpacing.X * 2f));

        ImGui.TextColored(labelColor, minimumLabel);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(sliderWidth);
        if (ImGui.SliderFloat($"##{id}", ref value, minimum, maximum, "%.2f"))
        {
            setValue(settings, AxisDeadzoneSettings.ClampPercent((int)MathF.Round(value * GamepadConstants.PercentMax)));
            settings.Normalize();
        }

        var shouldSave = ImGui.IsItemDeactivatedAfterEdit();

        ImGui.SameLine();
        ImGui.TextColored(labelColor, maximumLabel);

        if (shouldSave)
            this.plugin.Configuration.Save();

        static string FormatRatio(float value)
        {
            return value.ToString("0.00");
        }
    }

    private static void DrawStickVisualizer(
        string id,
        StickSettings settings,
        StickAxes original,
        StickAxes remapped)
    {
        var scale = ImGui.GetIO().FontGlobalScale;
        var size = MathF.Min(ImGui.GetContentRegionAvail().X, GetVisualizerSize());
        size = MathF.Max(size, 120f * scale);
        var canvasSize = new Vector2(size, size);

        ImGui.InvisibleButton(id, canvasSize);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var center = (min + max) * 0.5f;
        var radius = (max.X - min.X) * 0.5f - 8f * scale;
        var drawList = ImGui.GetWindowDrawList();

        var frameColor = ImGui.GetColorU32(new Vector4(0.45f, 0.47f, 0.50f, 1f));
        var gridColor = ImGui.GetColorU32(new Vector4(0.28f, 0.30f, 0.34f, 1f));
        var deadzoneColor = ImGui.GetColorU32(new Vector4(0.50f, 0.52f, 0.56f, 0.35f));
        var antiDeadzoneFillColor = ImGui.GetColorU32(new Vector4(0.95f, 0.66f, 0.22f, 0.16f));
        var maxZoneFillColor = ImGui.GetColorU32(new Vector4(0.72f, 0.34f, 0.95f, 0.16f));
        var originalColor = ImGui.GetColorU32(settings.Enabled ? OriginalPointColor : DisabledPointColor);
        var originalBorderColor = ImGui.GetColorU32(settings.Enabled ? OriginalPointBorderColor : DisabledPointBorderColor);
        var remappedColor = ImGui.GetColorU32(settings.Enabled ? RemappedPointColor : DisabledPointColor);
        var remappedBorderColor = ImGui.GetColorU32(settings.Enabled ? RemappedPointBorderColor : DisabledPointBorderColor);

        drawList.AddRect(min, max, frameColor);
        drawList.AddLine(new Vector2(center.X, min.Y + 6f), new Vector2(center.X, max.Y - 6f), gridColor);
        drawList.AddLine(new Vector2(min.X + 6f, center.Y), new Vector2(max.X - 6f, center.Y), gridColor);
        drawList.AddCircle(center, radius, gridColor, 64);

        if (settings.Enabled)
        {
            var deadzoneSize = new Vector2(
                radius * AxisDeadzoneSettings.ToRatio(settings.X.DeadZone),
                radius * AxisDeadzoneSettings.ToRatio(settings.Y.DeadZone));
            var antiDeadzoneSize = new Vector2(
                radius * AxisDeadzoneSettings.ToRatio(settings.X.AntiDeadZone),
                radius * AxisDeadzoneSettings.ToRatio(settings.Y.AntiDeadZone));
            var maxZoneSize = new Vector2(
                radius * AxisDeadzoneSettings.ToRatio(settings.X.MaxZone),
                radius * AxisDeadzoneSettings.ToRatio(settings.Y.MaxZone));

            if (settings.Type == DeadzoneType.Radial)
            {
                AddRadialMaxZonePixels(drawList, center, radius, maxZoneSize, maxZoneFillColor);
                AddEllipseFilled(drawList, center, deadzoneSize, deadzoneColor, 48);
                AddEllipseFilled(drawList, center, antiDeadzoneSize, antiDeadzoneFillColor, 48);
            }
            else
            {
                AddAxialMaxZonePixels(drawList, center, radius, settings, maxZoneFillColor);
                AddAxialDeadzoneAxes(drawList, center, radius, deadzoneSize, deadzoneColor);
                AddAxialDeadzoneAxes(drawList, center, radius, antiDeadzoneSize, antiDeadzoneFillColor);
            }
        }

        DrawPoint(original, originalColor, originalBorderColor, 3.2f * scale);
        DrawPoint(remapped, remappedColor, remappedBorderColor, 3.8f * scale);
        DrawInputValueOverlay();

        void DrawPoint(StickAxes axes, uint fillColor, uint borderColor, float pointRadius)
        {
            var normalized = new Vector2(
                Math.Clamp(axes.X / GamepadConstants.StickLimit, -1f, 1f),
                Math.Clamp(-axes.Y / GamepadConstants.StickLimit, -1f, 1f));
            var point = center + normalized * radius;
            drawList.AddCircleFilled(point, pointRadius, fillColor, 20);
            drawList.AddCircle(point, pointRadius, borderColor, 20, Math.Max(1f, 1.2f * scale));
        }

        void DrawInputValueOverlay()
        {
            var lineHeight = ImGui.GetTextLineHeight();
            var y = max.Y - (lineHeight * 2f) - 8f * scale;
            var disabledText = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];
            var originalText = settings.Enabled ? OriginalTextColor : DisabledPointBorderColor;
            var remappedText = settings.Enabled ? RemappedTextColor : disabledText;
            var originalValue = FormatCoordinates(original);
            var remappedValue = FormatCoordinates(remapped);

            if (!settings.Enabled)
            {
                AddRightAlignedTextWithShadow(y + lineHeight, originalText, originalValue);
                return;
            }

            AddRightAlignedTextWithShadow(y, originalText, originalValue);
            y += lineHeight;
            AddRightAlignedTextWithShadow(y, remappedText, remappedValue);
        }

        void AddRightAlignedTextWithShadow(float y, Vector4 color, string text)
        {
            var position = new Vector2(max.X - 9f * scale - ImGui.CalcTextSize(text).X, y);
            AddTextWithShadow(position, color, text);
        }

        static string FormatCoordinates(StickAxes axes)
        {
            return $"({axes.X:+0;-0;0},{axes.Y:+0;-0;0})";
        }

        void AddTextWithShadow(Vector2 position, Vector4 color, string text)
        {
            drawList.AddText(position + Vector2.One, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.75f)), text);
            drawList.AddText(position, ImGui.GetColorU32(color), text);
        }
    }

    private static Vector4 OriginalPointColor => new(0.25f, 0.72f, 1f, 1f);

    private static Vector4 OriginalPointBorderColor => new(0.80f, 0.93f, 1f, 1f);

    private static Vector4 OriginalTextColor => new(0.45f, 0.78f, 1f, 1f);

    private static Vector4 RemappedPointColor => new(0.96f, 0.25f, 0.20f, 1f);

    private static Vector4 RemappedPointBorderColor => new(1f, 0.68f, 0.62f, 1f);

    private static Vector4 RemappedTextColor => new(1f, 0.48f, 0.42f, 1f);

    private static Vector4 DisabledPointColor => new(0.88f, 0.88f, 0.88f, 0.85f);

    private static Vector4 DisabledPointBorderColor => new(1f, 1f, 1f, 0.95f);

    private static float GetVisualizerSize()
    {
        return 210f * ImGui.GetIO().FontGlobalScale;
    }

    private static void AddEllipseFilled(ImDrawListPtr drawList, Vector2 center, Vector2 radius, uint color, int segments)
    {
        if (radius.X <= 0f || radius.Y <= 0f)
            return;

        drawList.PathClear();
        for (var i = 0; i < segments; i++)
        {
            var angle = MathF.Tau * i / segments;
            drawList.PathLineTo(center + new Vector2(MathF.Cos(angle) * radius.X, MathF.Sin(angle) * radius.Y));
        }

        drawList.PathFillConvex(color);
    }

    private static void AddRadialMaxZonePixels(
        ImDrawListPtr drawList,
        Vector2 center,
        float outerRadius,
        Vector2 maxZoneRadius,
        uint color)
    {
        if (outerRadius <= 0f || maxZoneRadius.X <= 0f || maxZoneRadius.Y <= 0f)
            return;

        AddPixelMask(drawList, center, outerRadius, color, offset =>
        {
            var distance = offset.Length();
            if (distance <= 0f)
                return false;

            var innerRadius = GetEllipseRadius(maxZoneRadius, offset / distance);
            return distance > innerRadius;
        });
    }

    private static float GetEllipseRadius(Vector2 radius, Vector2 direction)
    {
        if (radius.X <= 0f || radius.Y <= 0f)
            return 0f;

        var denominator = (direction.X * direction.X / (radius.X * radius.X))
                          + (direction.Y * direction.Y / (radius.Y * radius.Y));
        return denominator <= 0f ? 0f : 1f / MathF.Sqrt(denominator);
    }

    private static void AddAxialMaxZonePixels(
        ImDrawListPtr drawList,
        Vector2 center,
        float radius,
        StickSettings settings,
        uint color)
    {
        if (radius <= 0f)
            return;

        AddPixelMask(drawList, center, radius, color, offset =>
        {
            var distance = offset.Length();
            if (distance <= 0f)
                return false;

            var boundary = DeadzoneProcessor.FindAxialMaxBoundaryRadius(offset / distance, settings) * radius;
            return distance > boundary;
        });
    }

    private static void AddPixelMask(
        ImDrawListPtr drawList,
        Vector2 center,
        float radius,
        uint color,
        Func<Vector2, bool> shouldFill)
    {
        var minX = (int)MathF.Floor(center.X - radius);
        var maxX = (int)MathF.Ceiling(center.X + radius);
        var minY = (int)MathF.Floor(center.Y - radius);
        var maxY = (int)MathF.Ceiling(center.Y + radius);
        var radiusSquared = radius * radius;

        for (var y = minY; y < maxY; y++)
        {
            int? runStart = null;
            for (var x = minX; x < maxX; x++)
            {
                var sample = new Vector2(x + 0.5f, y + 0.5f);
                var offset = sample - center;
                var fill = offset.LengthSquared() < radiusSquared && shouldFill(offset);

                if (fill)
                {
                    runStart ??= x;
                    continue;
                }

                if (runStart != null)
                {
                    drawList.AddRectFilled(new Vector2(runStart.Value, y), new Vector2(x, y + 1), color);
                    runStart = null;
                }
            }

            if (runStart != null)
                drawList.AddRectFilled(new Vector2(runStart.Value, y), new Vector2(maxX, y + 1), color);
        }
    }

    private static void AddAxialDeadzoneAxes(ImDrawListPtr drawList, Vector2 center, float radius, Vector2 deadzoneSize, uint color)
    {
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);

        if (deadzoneSize.Y > 0f)
        {
            drawList.AddRectFilled(
                new Vector2(min.X, center.Y - deadzoneSize.Y),
                new Vector2(max.X, center.Y + deadzoneSize.Y),
                color);
        }

        if (deadzoneSize.X > 0f)
        {
            drawList.AddRectFilled(
                new Vector2(center.X - deadzoneSize.X, min.Y),
                new Vector2(center.X + deadzoneSize.X, max.Y),
                color);
        }
    }

}
