namespace OCCAD.Core.Tests;

[TestClass]
public sealed class SettingsAtomicityTests
{
    [TestMethod]
    public void InvalidBoundSettingRollsBackStoreAndCoreState()
    {
        using var workspace = new CadWorkspace();
        var settings = new CadSettingsStore();
        settings.Set(CadSettingKeys.SnapSize, 15);
        using var binding = settings.Bind(workspace);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            settings.Set(CadSettingKeys.SnapSize, 1000));

        Assert.AreEqual(15, settings.Get(CadSettingKeys.SnapSize, -1));
        Assert.AreEqual(15, workspace.Snap.MarkerSize);
    }

    [TestMethod]
    public void InvalidBatchRestoresEarlierAppliedSettings()
    {
        using var workspace = new CadWorkspace();
        var settings = new CadSettingsStore();
        settings.Set(CadSettingKeys.GripSize, 15);
        settings.Set(CadSettingKeys.SnapSize, 15);
        using var binding = settings.Bind(workspace);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            settings.SetValues(new Dictionary<string, System.Text.Json.Nodes.JsonNode?>
            {
                [CadSettingKeys.GripSize] = System.Text.Json.Nodes.JsonValue.Create(17),
                [CadSettingKeys.SnapSize] = System.Text.Json.Nodes.JsonValue.Create(1000)
            }));

        Assert.AreEqual(15, settings.Get(CadSettingKeys.GripSize, -1));
        Assert.AreEqual(15, settings.Get(CadSettingKeys.SnapSize, -1));
        Assert.AreEqual(15, workspace.Grips.MarkerSize);
        Assert.AreEqual(15, workspace.Snap.MarkerSize);
    }
}
