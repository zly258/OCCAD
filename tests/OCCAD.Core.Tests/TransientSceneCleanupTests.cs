namespace OCCAD.Core.Tests;

[TestClass]
public sealed class TransientSceneCleanupTests
{
    [TestMethod]
    public void ClearAllContinuesAcrossRecoverableChannelFailures()
    {
        var scene = new CadTransientScene();
        var secondCleared = false;

        scene.Register(
            CadTransientChannel.SelectionWindow,
            () => throw new InvalidOperationException("recoverable cleanup failure"),
            () => true,
            CadTransientLifetime.Workspace);
        scene.Register(
            CadTransientChannel.SelectionMarkers,
            () => secondCleared = true,
            () => !secondCleared,
            CadTransientLifetime.Workspace);

        scene.ClearAll();

        Assert.IsTrue(secondCleared);
    }

    [TestMethod]
    public void ClearAllDoesNotSwallowFatalFailureHiddenByWrapper()
    {
        var scene = new CadTransientScene();
        scene.Register(
            CadTransientChannel.SelectionWindow,
            () => throw new AccessViolationException("fatal native cleanup failure"),
            () => true,
            CadTransientLifetime.Workspace);

        Assert.Throws<AggregateException>(scene.ClearAll);
    }

    [TestMethod]
    public void RegistrationDisposeDoesNotAbortCallerForRecoverableCleanupFailure()
    {
        var scene = new CadTransientScene();
        var registration = scene.Register(
            CadTransientChannel.SelectionMarkers,
            () => throw new InvalidOperationException("marker deletion failure"),
            () => true,
            CadTransientLifetime.Workspace);

        registration.Dispose();

        // Surviving native state keeps the channel registered so workspace-level
        // cleanup can retry it later. Dispose itself must not abort the caller's
        // remaining event/resource teardown.
        scene.ClearAll();
    }

    [TestMethod]
    public void ToolOwnerCleanupRemainsStrictAndEndsSession()
    {
        var scene = new CadTransientScene();
        var owner = scene.BeginToolSession();
        scene.Register(
            CadTransientChannel.ToolPreview,
            () => throw new InvalidOperationException("tool cleanup failure"),
            () => true);

        Assert.Throws<AggregateException>(() => scene.ClearOwner(owner));
        Assert.AreEqual(0L, scene.CurrentToolOwner);
    }
}
