namespace OCCAD.Core.Tests;

[TestClass]
public sealed class ApplicationCoreTests
{
    [TestMethod]
    public void ApplicationCoreOwnsSettingsAndWorkspaceBinding()
    {
        using var app = new CadApplicationCore();
        app.Settings.Set(CadSettingKeys.SnapSize, 21);
        app.Settings.Set(CadSettingKeys.GripSize, 17);

        Assert.AreEqual(21, app.Workspace.Snap.MarkerSize);
        Assert.AreEqual(17, app.Workspace.Grips.MarkerSize);
    }

    [TestMethod]
    public void LoadingSettingsRefreshesCoreServices()
    {
        using var source = new MemoryStream();
        var settings = new CadSettingsStore();
        settings.Set(CadSettingKeys.SelectionTolerance, 13.0);
        settings.Set(CadSettingKeys.PolarIncrementDegrees, 30.0);
        settings.Save(source);
        source.Position = 0;

        using var app = new CadApplicationCore();
        app.LoadSettings(source);

        Assert.AreEqual(13.0, app.Workspace.Selection.PixelTolerance);
        Assert.AreEqual(30.0, app.Workspace.Drafting.PolarIncrementDegrees);
    }
}
