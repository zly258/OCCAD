namespace OCCAD.Core.Tests;

[TestClass]
public sealed class ToolLifecycleArchitectureTests
{
    [TestMethod]
    public void IdleCancelRepairsAllToolLifetimeState()
    {
        using var workspace = new CadWorkspace();
        var line = new CadLineEntity(default, new(10, 0, 0));
        workspace.Document.Add(line);

        workspace.Preselection.Update(line, hit: null);
        workspace.Snap.Active = true;
        workspace.WorkPlane.BeginToolPlane(new(1, 2, 3));

        Assert.IsNotEmpty(workspace.Tools.NeutralStateViolations);
        Assert.IsFalse(workspace.Tools.CancelCurrent());
        Assert.IsEmpty(workspace.Tools.NeutralStateViolations);
        Assert.IsNull(workspace.Preselection.Current);
        Assert.IsFalse(workspace.Snap.Active);
        Assert.IsFalse(workspace.WorkPlane.IsActive);
    }

    [TestMethod]
    public void FailedActivationAlwaysReturnsToNeutralState()
    {
        using var workspace = new CadWorkspace();
        workspace.Tools.Register<FailingActivationTool>("test.fail-activate");

        Assert.Throws<InvalidOperationException>(
            () => workspace.Tools.Activate("test.fail-activate"));

        Assert.IsNull(workspace.Tools.ActiveTool);
        Assert.AreEqual(CadInteractionMode.Normal, workspace.Tools.Mode);
        Assert.IsEmpty(workspace.Tools.NeutralStateViolations);
        Assert.IsFalse(workspace.WorkPlane.IsActive);
        Assert.IsFalse(workspace.Snap.Active);
    }

    [TestMethod]
    public void DrawingPlaneApiRejectsCustomAndLockedChanges()
    {
        using var workspace = new CadWorkspace();

        Assert.IsFalse(
            workspace.Tools.TryChangeDrawingPlane(
                CadWorkPlanePreset.Custom));
        Assert.AreEqual(
            CadWorkPlanePreset.XY,
            workspace.WorkPlane.Preset);

        workspace.WorkPlane.SetUserPlaneLocked(true);
        Assert.IsFalse(
            workspace.Tools.TryChangeDrawingPlane(
                CadWorkPlanePreset.YZ));
        Assert.AreEqual(
            CadWorkPlanePreset.XY,
            workspace.WorkPlane.Preset);
    }

    public sealed class FailingActivationTool : CadTool
    {
        public override string Id => "test.fail-activate";
        public override string DisplayName => "Failing activation";

        protected override void OnActivated() =>
            throw new InvalidOperationException("activation failed");
    }
}
