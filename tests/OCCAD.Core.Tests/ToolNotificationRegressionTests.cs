namespace OCCAD.Core.Tests;

[TestClass]
public sealed class ToolNotificationRegressionTests
{
    [TestMethod]
    public void ToolChangedObserverFailureDoesNotTurnActivationIntoFailure()
    {
        using var w = new CadWorkspace();
        var notifications = 0;

        EventHandler<CadToolChangedEventArgs> throwing =
            (_, _) => throw new InvalidOperationException("simulated UI observer failure");
        EventHandler<CadToolChangedEventArgs> counting =
            (_, args) =>
            {
                if (args.Tool is not null)
                    notifications++;
            };

        w.Tools.ToolChanged += throwing;
        w.Tools.ToolChanged += counting;

        Assert.IsTrue(w.Tools.Activate("line"));
        Assert.IsNotNull(w.Tools.ActiveTool);
        Assert.AreEqual("line", w.Tools.ActiveTool.Id);
        Assert.AreEqual(1, notifications,
            "A failing observer must not prevent later observers from seeing activation.");

        w.Tools.ToolChanged -= throwing;
        w.Tools.ToolChanged -= counting;
        Assert.IsTrue(w.Tools.CancelCurrent());
        InteractionTests.AssertNeutral(w);
    }

    [TestMethod]
    public void ToolChangedObserverFailureDoesNotTurnDeactivationIntoFailure()
    {
        using var w = new CadWorkspace();
        Assert.IsTrue(w.Tools.Activate("line"));

        var neutralNotifications = 0;
        EventHandler<CadToolChangedEventArgs> throwing =
            (_, _) => throw new InvalidOperationException("simulated UI observer failure");
        EventHandler<CadToolChangedEventArgs> counting =
            (_, args) =>
            {
                if (args.Tool is null)
                    neutralNotifications++;
            };

        w.Tools.ToolChanged += throwing;
        w.Tools.ToolChanged += counting;

        Assert.IsTrue(w.Tools.CancelCurrent());
        Assert.IsNull(w.Tools.ActiveTool);
        Assert.AreEqual(CadInteractionMode.Normal, w.Tools.Mode);
        Assert.AreEqual(1, neutralNotifications,
            "A failing observer must not prevent later observers from seeing neutral state.");
        InteractionTests.AssertNeutral(w);

        w.Tools.ToolChanged -= throwing;
        w.Tools.ToolChanged -= counting;
    }

    [TestMethod]
    public void ToolUpdatedObserverFailureDoesNotBreakOffsetInput()
    {
        using var w = new CadWorkspace();
        Assert.IsTrue(w.Tools.Activate("line"));

        var updates = 0;
        EventHandler<CadToolChangedEventArgs> throwing =
            (_, _) => throw new InvalidOperationException("simulated UI observer failure");
        EventHandler<CadToolChangedEventArgs> counting =
            (_, args) =>
            {
                if (args.Tool is not null)
                    updates++;
            };

        w.Tools.ToolUpdated += throwing;
        w.Tools.ToolUpdated += counting;

        Assert.IsTrue(w.Tools.BeginOffsetInput());
        Assert.AreEqual(CadPointInputMode.Offset, w.Precision.PointMode);
        Assert.AreEqual(1, updates);
        Assert.IsNotNull(w.Tools.ActiveTool);

        w.Tools.ToolUpdated -= throwing;
        w.Tools.ToolUpdated -= counting;
        w.Tools.CancelCurrent();
        InteractionTests.AssertNeutral(w);
    }
}
