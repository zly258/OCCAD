namespace OCCAD.Core.Tests;

[TestClass]
public sealed class ApplicationCoreTests
{
    [TestMethod]
    public void ApplicationCoreOwnsSettingsWorkspaceCommandsAndDocumentSession()
    {
        using var app = new CadApplicationCore();
        app.Settings.Set(CadSettingKeys.SnapSize, 21);
        app.Settings.Set(CadSettingKeys.GripSize, 17);

        Assert.AreEqual(21, app.Workspace.Snap.MarkerSize);
        Assert.AreEqual(17, app.Workspace.Grips.MarkerSize);
        Assert.IsNotNull(app.Commands);
        Assert.AreEqual(CadDocumentSession.UntitledName, app.Documents.DisplayName);
    }

    [TestMethod]
    public void CommandSessionsAreApplicationScoped()
    {
        using var first = new CadApplicationCore();
        using var second = new CadApplicationCore();

        var result = first.Commands.Execute("not-a-real-command");

        Assert.AreEqual(CadCommandResultKind.Failed, result.Kind);
        CollectionAssert.AreEqual(
            new[] { "not-a-real-command" },
            first.Commands.History.ToArray());
        Assert.IsEmpty(second.Commands.History);
    }

    [TestMethod]
    public void LoadingSettingsPublishesIntoCoreServices()
    {
        using var source = new MemoryStream();
        var settings = new CadSettingsStore();
        settings.Set(CadSettingKeys.SelectionTolerance, 13);
        settings.Set(CadSettingKeys.PolarIncrementDegrees, 30.0);
        settings.Save(source);
        source.Position = 0;

        using var app = new CadApplicationCore();
        app.LoadSettings(source);

        Assert.AreEqual(13, app.Workspace.Selection.PixelTolerance);
        Assert.AreEqual(30.0, app.Workspace.Drafting.PolarIncrementDegrees);
    }

    [TestMethod]
    public void RuntimeInteractionPreferencesPersistBackToStore()
    {
        using var app = new CadApplicationCore();

        app.Workspace.Selection.PixelTolerance = 12;
        app.Workspace.Grips.MarkerSize = 19;
        app.Workspace.Grips.PixelTolerance = 14;
        app.Workspace.Snap.Enabled = false;
        app.Workspace.Snap.MarkerSize = 23;
        app.Workspace.Drafting.OrthogonalTrackingEnabled = true;

        Assert.AreEqual(12, app.Settings.Get(CadSettingKeys.SelectionTolerance, 0));
        Assert.AreEqual(19, app.Settings.Get(CadSettingKeys.GripSize, 0));
        Assert.AreEqual(14.0, app.Settings.Get(CadSettingKeys.GripTolerance, 0.0));
        Assert.IsFalse(app.Settings.Get(CadSettingKeys.SnapEnabled, true));
        Assert.AreEqual(23, app.Settings.Get(CadSettingKeys.SnapSize, 0));
        Assert.IsTrue(app.Settings.Get(CadSettingKeys.OrthogonalTrackingEnabled, false));
        Assert.IsFalse(app.Settings.Get(CadSettingKeys.PolarTrackingEnabled, true));
    }

    [TestMethod]
    public void InvalidPersistedPreferencesRollBackStoreAndCoreState()
    {
        using var source = new MemoryStream();
        var persisted = new CadSettingsStore();
        persisted.Set(CadSettingKeys.SelectionTolerance, 999);
        persisted.Set(CadSettingKeys.GripSize, 999);
        persisted.Set(CadSettingKeys.SnapSize, -5);
        persisted.Set(CadSettingKeys.PolarIncrementDegrees, 999.0);
        persisted.Save(source);
        source.Position = 0;

        using var app = new CadApplicationCore();
        var beforeSettings = app.Settings.Snapshot();
        var expectedSelection = app.Workspace.Selection.PixelTolerance;
        var expectedGrip = app.Workspace.Grips.MarkerSize;
        var expectedSnap = app.Workspace.Snap.MarkerSize;
        var expectedPolar = app.Workspace.Drafting.PolarIncrementDegrees;

        Assert.Throws<ArgumentOutOfRangeException>(() => app.LoadSettings(source));

        Assert.AreEqual(expectedSelection, app.Workspace.Selection.PixelTolerance);
        Assert.AreEqual(expectedGrip, app.Workspace.Grips.MarkerSize);
        Assert.AreEqual(expectedSnap, app.Workspace.Snap.MarkerSize);
        Assert.AreEqual(expectedPolar, app.Workspace.Drafting.PolarIncrementDegrees);
        CollectionAssert.AreEquivalent(
            beforeSettings.Keys.ToArray(),
            app.Settings.Keys.ToArray());
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
    public void DocumentLifecycleIsNotAWorkspaceAction()
    {
        using var app = new CadApplicationCore();

        Assert.IsNull(app.Workspace.Actions.Find("file.new"));
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
