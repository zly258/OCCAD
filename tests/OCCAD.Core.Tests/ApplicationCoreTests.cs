namespace OCCAD.Core.Tests;

[TestClass]
public sealed class ApplicationCoreTests
{
    [TestMethod]
    public void ApplicationCoreOwnsSettingsWorkspaceAndDocumentSession()
    {
        using var app = new CadApplicationCore();
        app.Settings.Set(CadSettingKeys.SnapSize, 21);
        app.Settings.Set(CadSettingKeys.GripSize, 17);

        Assert.AreEqual(21, app.Workspace.Snap.MarkerSize);
        Assert.AreEqual(17, app.Workspace.Grips.MarkerSize);
        Assert.AreEqual(CadDocumentSession.UntitledName, app.Documents.DisplayName);
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

    [TestMethod]
    public void RuntimeDraftingAndSnapChangesPersistBackToStore()
    {
        using var app = new CadApplicationCore();

        app.Workspace.Snap.Enabled = false;
        app.Workspace.Snap.MarkerSize = 23;
        app.Workspace.Drafting.OrthogonalTrackingEnabled = true;

        Assert.IsFalse(app.Settings.Get(CadSettingKeys.SnapEnabled, true));
        Assert.AreEqual(23, app.Settings.Get(CadSettingKeys.SnapSize, 0));
        Assert.IsTrue(app.Settings.Get(CadSettingKeys.OrthogonalTrackingEnabled, false));
        Assert.IsFalse(app.Settings.Get(CadSettingKeys.PolarTrackingEnabled, true));
    }

    [TestMethod]
    public void ConflictingTrackingPreferencesNormalizeToEffectiveCoreState()
    {
        var settings = new CadSettingsStore();
        settings.Set(CadSettingKeys.OrthogonalTrackingEnabled, true);
        settings.Set(CadSettingKeys.PolarTrackingEnabled, true);

        using var app = new CadApplicationCore(settings);

        Assert.IsFalse(app.Workspace.Drafting.OrthogonalTrackingEnabled);
        Assert.IsTrue(app.Workspace.Drafting.PolarTrackingEnabled);
        Assert.IsFalse(app.Settings.Get(CadSettingKeys.OrthogonalTrackingEnabled, true));
        Assert.IsTrue(app.Settings.Get(CadSettingKeys.PolarTrackingEnabled, false));
    }

    [TestMethod]
    public void LegacyNewActionCannotBypassDocumentSession()
    {
        using var app = new CadApplicationCore();
        var action = app.Workspace.Actions.Find("file.new");

        Assert.IsNotNull(action);
        Assert.IsFalse(action.CanExecute());
        Assert.IsFalse(app.Workspace.Actions.Execute("file.new"));
    }

    [TestMethod]
    public void DocumentSessionRoundTripsAndTracksSavedState()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"occad-session-{Guid.NewGuid():N}.occad");

        try
        {
            using var app = new CadApplicationCore();
            var line = new CadLineEntity(default, new(10, 0, 0));
            app.Workspace.AddEntity(line);
            Assert.IsTrue(app.Workspace.IsModified);

            app.Documents.Save(path);
            Assert.IsFalse(app.Workspace.IsModified);
            Assert.AreEqual(Path.GetFileName(path), app.Documents.DisplayName);

            app.Workspace.TranslateEntities([line], new(5, 0, 0));
            Assert.IsTrue(app.Workspace.IsModified);

            app.Documents.New();
            Assert.IsEmpty(app.Workspace.Document.Entities);
            Assert.IsFalse(app.Workspace.IsModified);
            Assert.AreEqual(CadDocumentSession.UntitledName, app.Documents.DisplayName);

            app.Documents.Open(path);
            Assert.HasCount(1, app.Workspace.Document.Entities);
            Assert.IsFalse(app.Workspace.IsModified);
            Assert.AreEqual(Path.GetFileName(path), app.Documents.DisplayName);
            Assert.AreEqual(
                new OcctNet.OcctPoint3d(10, 0, 0),
                ((CadLineEntity)app.Workspace.Document.Entities[0]).End);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
