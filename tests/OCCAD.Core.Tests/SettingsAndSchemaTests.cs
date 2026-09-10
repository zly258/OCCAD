namespace OCCAD.Core.Tests;

[TestClass]
public sealed class SettingsAndSchemaTests
{
    [TestMethod]
    public void SettingsBindingAppliesInteractionStateWithoutUiOwnership()
    {
        using var workspace = new CadWorkspace();
        var settings = new CadSettingsStore();
        using var binding = settings.Bind(workspace);

        settings.Set(CadSettingKeys.SelectionTolerance, 9);
        settings.Set(CadSettingKeys.GripSize, 17);
        settings.Set(CadSettingKeys.GripTolerance, 11.0);
        settings.Set(CadSettingKeys.SnapSize, 19);
        settings.Set(CadSettingKeys.SnapTolerance, 12.0);
        settings.Set(CadSettingKeys.SnapEnabled, false);
        settings.Set(CadSettingKeys.PolarIncrementDegrees, 30.0);
        settings.Set(CadSettingKeys.TrackingToleranceDegrees, 3.0);

        Assert.AreEqual(9, workspace.Selection.PixelTolerance);
        Assert.AreEqual(17, workspace.Grips.MarkerSize);
        Assert.AreEqual(11.0, workspace.Grips.PixelTolerance);
        Assert.AreEqual(19, workspace.Snap.MarkerSize);
        Assert.AreEqual(12.0, workspace.Snap.PixelTolerance);
        Assert.IsFalse(workspace.Snap.Enabled);
        Assert.AreEqual(30.0, workspace.Drafting.PolarIncrementDegrees);
        Assert.AreEqual(3.0, workspace.Drafting.TrackingToleranceDegrees);
    }

    [TestMethod]
    public void DraftingSettingsKeepOrthoAndPolarMutuallyExclusive()
    {
        using var workspace = new CadWorkspace();
        var settings = new CadSettingsStore();
        using var binding = settings.Bind(workspace);

        settings.Set(CadSettingKeys.OrthogonalTrackingEnabled, true);
        Assert.IsTrue(workspace.Drafting.OrthogonalTrackingEnabled);
        Assert.IsFalse(workspace.Drafting.PolarTrackingEnabled);

        settings.Set(CadSettingKeys.PolarTrackingEnabled, true);
        Assert.IsFalse(workspace.Drafting.OrthogonalTrackingEnabled);
        Assert.IsTrue(workspace.Drafting.PolarTrackingEnabled);
    }

    [TestMethod]
    public void ToolParameterSchemaRejectsDuplicateParameterIds()
    {
        var first = new CadBooleanToolParameterDescriptor("mode", "Mode", true);
        var second = new CadBooleanToolParameterDescriptor("MODE", "Mode", false);

        Assert.Throws<ArgumentException>(() =>
            new CadToolParameterSchema("Tool", [first, second]));
    }

    [TestMethod]
    public void ToolParameterSchemaProvidesCaseInsensitiveLookup()
    {
        var schema = new CadToolParameterSchema(
            "Tool",
            [new CadBooleanToolParameterDescriptor("enabled", "Enabled", true)]);

        Assert.AreSame(schema.Parameters[0], schema.Find("ENABLED"));
        Assert.AreSame(schema.Parameters[0], schema.GetRequired(" enabled "));
        Assert.IsNull(schema.Find("missing"));
        Assert.Throws<KeyNotFoundException>(() => schema.GetRequired("missing"));
    }

    [TestMethod]
    public void ToolParameterSchemaNormalizesPublishedValueKinds()
    {
        var schema = new CadToolParameterSchema(
            "Tool",
            [
                new CadChoiceToolParameterDescriptor(
                    "mode",
                    "Mode",
                    "Rectangular",
                    ["Rectangular", "Circular"]),
                new CadIntegerToolParameterDescriptor(
                    "count",
                    "Count",
                    2,
                    1,
                    10),
                new CadDoubleToolParameterDescriptor(
                    "angle",
                    "Angle",
                    0.0,
                    -360.0,
                    360.0),
                new CadOptionalDoubleToolParameterDescriptor(
                    "radius",
                    "Radius",
                    null,
                    1e-9,
                    1000.0),
                new CadBooleanToolParameterDescriptor(
                    "enabled",
                    "Enabled",
                    false)
            ]);

        Assert.IsTrue(schema.TryNormalizeValue("mode", "circular", out var mode));
        Assert.AreEqual("Circular", mode);
        Assert.IsFalse(schema.TryNormalizeValue("mode", "Radial", out _));

        Assert.IsTrue(schema.TryNormalizeValue("count", "10", out var count));
        Assert.AreEqual("10", count);
        Assert.IsFalse(schema.TryNormalizeValue("count", "11", out _));

        Assert.IsTrue(schema.TryNormalizeValue("angle", "-180", out var angle));
        Assert.AreEqual("-180", angle);
        Assert.IsFalse(schema.TryNormalizeValue("angle", "360.0001", out _));

        Assert.IsTrue(schema.TryNormalizeValue("radius", "", out var radius));
        Assert.AreEqual(string.Empty, radius);
        Assert.IsFalse(schema.TryNormalizeValue("radius", "1e-12", out _));

        Assert.IsTrue(schema.TryNormalizeValue("enabled", "TRUE", out var enabled));
        Assert.AreEqual("true", enabled);
    }

    [TestMethod]
    public void ParameterisedProductToolsUseDirectSchemaContract()
    {
        CadTool[] tools =
        [
            new CircleTool(),
            new ArcTool(),
            new RectangleTool(),
            new EllipseTool(),
            new RegularPolygonTool(),
            new ArrayTool(),
            new CopyTool(),
            new MirrorTool(),
            new BoxTool(),
            new CylinderTool(),
            new ConeTool(),
            new SphereTool()
        ];

        foreach (var tool in tools)
            Assert.IsNotNull(tool.ParameterSchema, tool.Id);
    }

    [TestMethod]
    public void LegacyToolParameterPanelContractIsRemoved()
    {
        Assert.IsNull(typeof(CadTool).GetProperty("ParameterPanel"));
        Assert.IsNull(
            typeof(CadTool).Assembly.GetType(
                "OCCAD.CadToolPanelDescriptor",
                throwOnError: false));
    }

    [TestMethod]
    public void ToolParameterMutationIsGatedByCurrentSchema()
    {
        using var workspace = new CadWorkspace();
        workspace.Tools.Register<SchemaGateProbeTool>("schema-gate-probe");
        Assert.IsTrue(workspace.Tools.Activate("schema-gate-probe"));

        var tool = (SchemaGateProbeTool)workspace.Tools.ActiveTool!;

        Assert.IsFalse(tool.TrySetParameter("internal-only", "42"));
        Assert.AreEqual(0, tool.SetCalls);

        Assert.IsTrue(tool.TrySetParameter("VISIBLE", "42"));
        Assert.AreEqual(1, tool.SetCalls);
        Assert.IsTrue(string.Equals(
            "visible",
            tool.LastId,
            StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("42", tool.LastValue);
    }

    [TestMethod]
    public void ToolParameterMutationRejectsInvalidValueBeforeToolHandler()
    {
        using var workspace = new CadWorkspace();
        workspace.Tools.Register<RangeGateProbeTool>("range-gate-probe");
        Assert.IsTrue(workspace.Tools.Activate("range-gate-probe"));

        var tool = (RangeGateProbeTool)workspace.Tools.ActiveTool!;

        Assert.IsFalse(tool.TrySetParameter("value", "10.0001"));
        Assert.AreEqual(0, tool.SetCalls);

        Assert.IsTrue(tool.TrySetParameter("value", "5"));
        Assert.AreEqual(1, tool.SetCalls);
        Assert.AreEqual("5", tool.LastValue);
    }

    [TestMethod]
    public void ArrayToolRejectsValuesOutsideItsPublishedSchema()
    {
        using var workspace = new CadWorkspace();
        workspace.Tools.Register<ArrayTool>("array-schema-probe");
        Assert.IsTrue(workspace.Tools.Activate("array-schema-probe"));

        var tool = (ArrayTool)workspace.Tools.ActiveTool!;

        Assert.IsFalse(tool.TrySetParameter("Mode", "Radial"));
        Assert.IsFalse(tool.TrySetParameter("ColumnSpacing", "1000001"));
        Assert.IsFalse(tool.TrySetParameter("RowSpacing", "-1000001"));
        Assert.IsTrue(tool.TrySetParameter("Mode", "circular"));
        Assert.IsFalse(tool.TrySetParameter("SweepAngle", "360.0001"));
        Assert.IsFalse(tool.TrySetParameter("SweepAngle", "-360.0001"));
        Assert.IsTrue(tool.TrySetParameter("SweepAngle", "-180"));

        var mode = (CadChoiceToolParameterDescriptor)
            tool.ParameterSchema.GetRequired("Mode");
        Assert.AreEqual("Circular", mode.Value);

        var sweep = (CadDoubleToolParameterDescriptor)
            tool.ParameterSchema.GetRequired("SweepAngle");
        Assert.AreEqual(-180.0, sweep.Value);
    }

    public sealed class SchemaGateProbeTool : CadTool
    {
        public override string Id => "schema-gate-probe";
        public override string DisplayName => "Schema Gate Probe";
        public override CadToolParameterSchema ParameterSchema =>
            new(
                "Schema Gate Probe",
                [new CadStringToolParameterDescriptor(
                    "visible",
                    "Visible",
                    "default")]);

        public int SetCalls { get; private set; }
        public string? LastId { get; private set; }
        public string? LastValue { get; private set; }

        protected override bool OnSetParameter(string id, string value)
        {
            SetCalls++;
            LastId = id;
            LastValue = value;
            return true;
        }
    }

    public sealed class RangeGateProbeTool : CadTool
    {
        public override string Id => "range-gate-probe";
        public override string DisplayName => "Range Gate Probe";
        public override CadToolParameterSchema ParameterSchema =>
            new(
                "Range Gate Probe",
                [new CadDoubleToolParameterDescriptor(
                    "value",
                    "Value",
                    0.0,
                    -10.0,
                    10.0)]);

        public int SetCalls { get; private set; }
        public string? LastValue { get; private set; }

        protected override bool OnSetParameter(string id, string value)
        {
            SetCalls++;
            LastValue = value;
            return true;
        }
    }
}
