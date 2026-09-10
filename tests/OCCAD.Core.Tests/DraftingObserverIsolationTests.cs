namespace OCCAD.Core.Tests;

[TestClass]
public sealed class DraftingObserverIsolationTests
{
    [TestMethod]
    public void ObserverFailureCannotInvalidateDraftingStateOrStarveLaterObservers()
    {
        var drafting = new CadDraftingSettings();
        var notifications = 0;

        drafting.Changed += (_, _) =>
            throw new InvalidOperationException("status bar observer failure");
        drafting.Changed += (_, _) => notifications++;

        drafting.OrthogonalTrackingEnabled = true;
        drafting.AxisLockEnabled = true;
        drafting.LockedLength = 25.0;
        drafting.LengthLockEnabled = true;

        Assert.IsTrue(drafting.OrthogonalTrackingEnabled);
        Assert.IsTrue(drafting.AxisLockEnabled);
        Assert.AreEqual(25.0, drafting.LockedLength, 1e-10);
        Assert.IsTrue(drafting.LengthLockEnabled);
        Assert.AreEqual(4, notifications);

        drafting.ResetTransientLocks();

        Assert.IsFalse(drafting.LengthLockEnabled);
        Assert.AreEqual(0.0, drafting.LockedLength, 1e-10);
        Assert.AreEqual(5, notifications);
    }

    [TestMethod]
    public void MutuallyExclusiveTrackingModesRemainCorrectWhenObserverThrows()
    {
        var drafting = new CadDraftingSettings();
        drafting.Changed += (_, _) =>
            throw new InvalidOperationException("UI observer failure");

        drafting.OrthogonalTrackingEnabled = true;
        drafting.PolarTrackingEnabled = true;

        Assert.IsFalse(drafting.OrthogonalTrackingEnabled);
        Assert.IsTrue(drafting.PolarTrackingEnabled);
    }
}
