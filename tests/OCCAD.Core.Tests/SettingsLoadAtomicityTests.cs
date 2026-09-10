using System.Text;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class SettingsLoadAtomicityTests
{
    [TestMethod]
    public void InvalidSettingsFileRestoresStoreAndRuntimeState()
    {
        using var workspace = new CadWorkspace();
        var settings = new CadSettingsStore();
        settings.Set(CadSettingKeys.GripSize, 15);
        settings.Set(CadSettingKeys.SnapSize, 15);
        using var binding = settings.Bind(workspace);

        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(
                "{\"grip.markerSize\":17,\"snap.markerSize\":1000}"));

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.Load(stream));

        Assert.AreEqual(15, settings.Get(CadSettingKeys.GripSize, -1));
        Assert.AreEqual(15, settings.Get(CadSettingKeys.SnapSize, -1));
        Assert.AreEqual(15, workspace.Grips.MarkerSize);
        Assert.AreEqual(15, workspace.Snap.MarkerSize);
    }

    [TestMethod]
    public void ValidSettingsFilePublishesDirectlyIntoApplicationCore()
    {
        using var app = new CadApplicationCore();
        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(
                "{\"selection.pixelTolerance\":12,\"snap.enabled\":false,\"drafting.polarIncrementDegrees\":30}"));

        app.LoadSettings(stream);

        Assert.AreEqual(12, app.Workspace.Selection.PixelTolerance);
        Assert.IsFalse(app.Workspace.Snap.Enabled);
        Assert.AreEqual(30.0, app.Workspace.Drafting.PolarIncrementDegrees);
    }
}
