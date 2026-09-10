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
    public void MigratedActiveToolsUseSchemaWithoutLegacyPanelOverride()
    {
        CadTool[] tools =
        [
            new RectangleTool(),
            new RegularPolygonTool(),
            new ArrayTool(),
            new CylinderTool(),
            new ConeTool(),
            new SphereTool()
        ];

        foreach (var tool in tools)
        {
            Assert.IsNotNull(tool.ParameterSchema, tool.Id);
            Assert.IsNull(tool.ParameterPanel, tool.Id);
        }
    }

    [TestMethod]
    public void LegacyPanelDescriptorIsOnlyASchemaCompatibilityType()
    {
        CadToolParameterSchema schema = new CadToolPanelDescriptor(
            "Tool",
            [new CadBooleanToolParameterDescriptor("enabled", "Enabled", true)]);

        Assert.AreEqual("Tool", schema.Title);
        Assert.HasCount(1, schema.Parameters);
        Assert.AreEqual("enabled", schema.Parameters[0].Id);
    }
}
