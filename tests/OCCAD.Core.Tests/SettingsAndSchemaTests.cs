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
        Assert.AreEqual("visible", tool.LastId, ignoreCase: true);
        Assert.AreEqual("42", tool.LastValue);
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
}
