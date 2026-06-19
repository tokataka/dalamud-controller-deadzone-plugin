using ControllerDeadzone;

var tests = new (string Name, Action Test)[]
{
    ("Axial deadzone zeroes each axis independently", AxialDeadzoneZeroesEachAxis),
    ("Axial output remaps from deadzone to maximum", AxialOutputRemapsFromDeadzoneToMaximum),
    ("Axial anti-deadzone remaps from deadzone to maximum", AxialAntiDeadzoneRemapsToMaximum),
    ("Axial max zone remaps to maximum early", AxialMaxZoneRemapsToMaximumEarly),
    ("Axial max zone combines with anti-deadzone", AxialMaxZoneCombinesWithAntiDeadzone),
    ("Axial anti-deadzone max boundary extends through opposite deadzone", AxialAntiDeadzoneMaxBoundaryExtendsThroughOppositeDeadzone),
    ("Axial anti-deadzone output stays inside unit circle", AxialAntiDeadzoneOutputStaysInsideUnitCircle),
    ("Axial anti-deadzone below deadzone still remaps response", AxialAntiDeadzoneBelowDeadzoneStillRemapsResponse),
    ("Radial deadzone zeroes inside circular zone", RadialDeadzoneZeroesInsideCircle),
    ("Radial output remaps from deadzone to maximum", RadialOutputRemapsFromDeadzoneToMaximum),
    ("Radial anti-deadzone remaps along input direction", RadialAntiDeadzoneRemapsAlongDirection),
    ("Radial max zone remaps along input direction", RadialMaxZoneRemapsAlongDirection),
    ("Radial anti-deadzone uses ellipse radius along direction", RadialAntiDeadzoneUsesEllipseRadiusAlongDirection),
    ("Radial anti-deadzone output stays inside unit circle", RadialAntiDeadzoneOutputStaysInsideUnitCircle),
    ("Radial anti-deadzone below deadzone still remaps response", RadialAntiDeadzoneBelowDeadzoneStillRemapsResponse),
    ("Settings clamp percentage values", SettingsClampPercentageValues),
    ("Settings keep max zone above dead zone", SettingsKeepMaxZoneAboveDeadZone),
    ("Stick reset restores default response", StickResetRestoresDefaultResponse),
};

foreach (var (name, test) in tests)
{
    test();
    Console.WriteLine($"PASS {name}");
}

static void AxialDeadzoneZeroesEachAxis()
{
    var settings = NewSettings(DeadzoneType.Axial, deadZone: 20, antiDeadZone: 0);
    var output = DeadzoneProcessor.Apply(new StickAxes(19, -19), settings);

    AssertEqual(0, output.X);
    AssertEqual(0, output.Y);
}

static void AxialOutputRemapsFromDeadzoneToMaximum()
{
    var settings = NewSettings(DeadzoneType.Axial, deadZone: 20, antiDeadZone: 0);
    var output = DeadzoneProcessor.Apply(new StickAxes(50, -30), settings);

    AssertEqual(38, output.X);
    AssertEqual(-13, output.Y);
}

static void AxialAntiDeadzoneRemapsToMaximum()
{
    var settings = NewSettings(DeadzoneType.Axial, deadZone: 20, antiDeadZone: 40);
    var output = DeadzoneProcessor.Apply(new StickAxes(60, 0), settings);

    AssertEqual(70, output.X);
    AssertEqual(0, output.Y);
}

static void AxialMaxZoneRemapsToMaximumEarly()
{
    var settings = NewSettings(DeadzoneType.Axial, deadZone: 20, antiDeadZone: 0, maxZone: 80);
    var output = DeadzoneProcessor.Apply(new StickAxes(50, 0), settings);
    var saturated = DeadzoneProcessor.Apply(new StickAxes(90, 0), settings);

    AssertEqual(50, output.X);
    AssertEqual(0, output.Y);
    AssertEqual(99, saturated.X);
    AssertEqual(0, saturated.Y);
}

static void AxialMaxZoneCombinesWithAntiDeadzone()
{
    var settings = NewSettings(DeadzoneType.Axial, deadZone: 20, antiDeadZone: 40, maxZone: 80);
    var output = DeadzoneProcessor.Apply(new StickAxes(50, 0), settings);

    AssertEqual(70, output.X);
    AssertEqual(0, output.Y);
}

static void AxialAntiDeadzoneMaxBoundaryExtendsThroughOppositeDeadzone()
{
    var settings = new StickSettings
    {
        Type = DeadzoneType.Axial,
        X = new AxisDeadzoneSettings { DeadZone = 20, AntiDeadZone = 0, MaxZone = 80 },
        Y = new AxisDeadzoneSettings { DeadZone = 10, AntiDeadZone = 20, MaxZone = 90 },
    };

    var belowJoin = DeadzoneProcessor.Apply(new StickAxes(77, 5), settings);
    var pastJoin = DeadzoneProcessor.Apply(new StickAxes(79, 5), settings);

    AssertEqual(95, belowJoin.X);
    AssertEqual(0, belowJoin.Y);
    AssertEqual(99, pastJoin.X);
    AssertEqual(0, pastJoin.Y);
}

static void AxialAntiDeadzoneOutputStaysInsideUnitCircle()
{
    var settings = NewSettings(DeadzoneType.Axial, deadZone: 20, antiDeadZone: 90);
    var output = DeadzoneProcessor.Apply(new StickAxes(70, 70), settings);

    AssertLengthAtMost(GamepadConstants.StickLimit, output);
}

static void AxialAntiDeadzoneBelowDeadzoneStillRemapsResponse()
{
    var settings = NewSettings(DeadzoneType.Axial, deadZone: 40, antiDeadZone: 20);
    var output = DeadzoneProcessor.Apply(new StickAxes(60, -70), settings);

    AssertEqual(47, output.X);
    AssertEqual(-60, output.Y);
}

static void RadialDeadzoneZeroesInsideCircle()
{
    var settings = NewSettings(DeadzoneType.Radial, deadZone: 20, antiDeadZone: 0);
    var output = DeadzoneProcessor.Apply(new StickAxes(14, 14), settings);

    AssertEqual(0, output.X);
    AssertEqual(0, output.Y);
}

static void RadialOutputRemapsFromDeadzoneToMaximum()
{
    var settings = NewSettings(DeadzoneType.Radial, deadZone: 20, antiDeadZone: 0);
    var output = DeadzoneProcessor.Apply(new StickAxes(30, 40), settings);

    AssertEqual(23, output.X);
    AssertEqual(30, output.Y);
}

static void RadialAntiDeadzoneRemapsAlongDirection()
{
    var settings = NewSettings(DeadzoneType.Radial, deadZone: 20, antiDeadZone: 40);
    var output = DeadzoneProcessor.Apply(new StickAxes(60, 0), settings);

    AssertEqual(70, output.X);
    AssertEqual(0, output.Y);
}

static void RadialMaxZoneRemapsAlongDirection()
{
    var settings = NewSettings(DeadzoneType.Radial, deadZone: 20, antiDeadZone: 0, maxZone: 80);
    var output = DeadzoneProcessor.Apply(new StickAxes(50, 0), settings);

    AssertEqual(50, output.X);
    AssertEqual(0, output.Y);
}

static void RadialAntiDeadzoneUsesEllipseRadiusAlongDirection()
{
    var settings = new StickSettings
    {
        Type = DeadzoneType.Radial,
        X = new AxisDeadzoneSettings { DeadZone = 20, AntiDeadZone = 40 },
        Y = new AxisDeadzoneSettings { DeadZone = 40, AntiDeadZone = 80 },
    };
    var output = DeadzoneProcessor.Apply(new StickAxes(60, 60), settings);

    AssertEqual(63, output.X);
    AssertEqual(63, output.Y);
}

static void RadialAntiDeadzoneOutputStaysInsideUnitCircle()
{
    var settings = NewSettings(DeadzoneType.Radial, deadZone: 20, antiDeadZone: 95);
    var output = DeadzoneProcessor.Apply(new StickAxes(70, 70), settings);

    AssertLengthAtMost(GamepadConstants.StickLimit, output);
}

static void RadialAntiDeadzoneBelowDeadzoneStillRemapsResponse()
{
    var settings = NewSettings(DeadzoneType.Radial, deadZone: 40, antiDeadZone: 20);
    var output = DeadzoneProcessor.Apply(new StickAxes(60, 0), settings);

    AssertEqual(47, output.X);
    AssertEqual(0, output.Y);
}

static void SettingsClampPercentageValues()
{
    var settings = new StickSettings
    {
        X = new AxisDeadzoneSettings { DeadZone = -1, AntiDeadZone = 125, MaxZone = 150 },
        Y = new AxisDeadzoneSettings { DeadZone = 150, AntiDeadZone = -20, MaxZone = -20 },
    };

    settings.Normalize();

    AssertEqual(0, settings.X.DeadZone);
    AssertEqual(100, settings.X.AntiDeadZone);
    AssertEqual(100, settings.X.MaxZone);
    AssertEqual(99, settings.Y.DeadZone);
    AssertEqual(0, settings.Y.AntiDeadZone);
    AssertEqual(100, settings.Y.MaxZone);
}

static void SettingsKeepMaxZoneAboveDeadZone()
{
    var settings = new StickSettings
    {
        X = new AxisDeadzoneSettings { DeadZone = 10, AntiDeadZone = 80, MaxZone = 20 },
        Y = new AxisDeadzoneSettings { DeadZone = 70, AntiDeadZone = 20, MaxZone = 30 },
    };

    settings.Normalize();

    AssertEqual(10, settings.X.DeadZone);
    AssertEqual(80, settings.X.AntiDeadZone);
    AssertEqual(20, settings.X.MaxZone);
    AssertEqual(70, settings.Y.DeadZone);
    AssertEqual(20, settings.Y.AntiDeadZone);
    AssertEqual(71, settings.Y.MaxZone);
}

static void StickResetRestoresDefaultResponse()
{
    var settings = new StickSettings
    {
        Enabled = false,
        Type = DeadzoneType.Axial,
        X = new AxisDeadzoneSettings { DeadZone = 40, AntiDeadZone = 30, MaxZone = 70 },
        Y = new AxisDeadzoneSettings { DeadZone = 50, AntiDeadZone = 20, MaxZone = 80 },
    };

    settings.ResetToDefaults();

    AssertEqual((int)DeadzoneType.Radial, (int)settings.Type);
    AssertEqual(15, settings.X.DeadZone);
    AssertEqual(0, settings.X.AntiDeadZone);
    AssertEqual(100, settings.X.MaxZone);
    AssertEqual(15, settings.Y.DeadZone);
    AssertEqual(0, settings.Y.AntiDeadZone);
    AssertEqual(100, settings.Y.MaxZone);
    AssertEqual(0, settings.Enabled ? 1 : 0);
}

static StickSettings NewSettings(DeadzoneType type, int deadZone, int antiDeadZone, int maxZone = 100)
{
    return new StickSettings
    {
        Type = type,
        X = new AxisDeadzoneSettings { DeadZone = deadZone, AntiDeadZone = antiDeadZone, MaxZone = maxZone },
        Y = new AxisDeadzoneSettings { DeadZone = deadZone, AntiDeadZone = antiDeadZone, MaxZone = maxZone },
    };
}

static void AssertEqual(float expected, float actual)
{
    if (Math.Abs(expected - actual) > 0.001f)
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
}

static void AssertLengthAtMost(float expectedMax, StickAxes actual)
{
    var length = MathF.Sqrt((actual.X * actual.X) + (actual.Y * actual.Y));
    if (length > expectedMax + 0.001f)
        throw new InvalidOperationException($"Expected length <= {expectedMax}, got {length}.");
}
