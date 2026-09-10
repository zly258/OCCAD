using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class PrecisionAndInputTests
{
    [TestMethod]
    public void RejectedPointerCommitRunsOnceAndDoesNotFinish()
    {
        using var w = new CadWorkspace(); w.Tools.Register<RejectingTool>("test.reject");
        w.Tools.Activate("test.reject");
        var tool = (RejectingTool)w.Tools.ActiveTool!;
        Assert.IsFalse(w.Tools.HandlePointer(new(OcctPointerInputKind.Pressed, OcctPointerButton.Left,
            OcctPointerButtons.Left, 10, 10, 0, OcctInputModifiers.None)));
        Assert.AreEqual(1, tool.Attempts); Assert.IsFalse(tool.Finished);
        Assert.AreSame(tool, w.Tools.ActiveTool);
    }

    public sealed class RejectingTool : CadTool, ICadPointInputTool
    {
        public override string Id => "test.reject";
        public override string DisplayName => Id;
        public int Attempts { get; private set; }
        public bool Finished { get; private set; }
        public bool TryAcceptPoint(OcctPoint3d point) { Attempts++; return false; }
        protected override bool OnCommitCurrentStage(CadPointerPosition pointer) { Attempts++; return false; }
        public override bool HandlePointer(OcctPointerInputEventArgs input) { Attempts++; return false; }
        protected override bool CanFinishCore => true;
        protected override bool OnFinish() { Finished = true; return true; }
    }

    [TestMethod]
    [TestCategory("Native")]
    public void EqualPrioritySnapHasPixelHysteresis()
    {
        using var scene = new NativeScene(); var w = scene.Workspace;
        w.Document.Add(new CadLineEntity(new(-100, 0, 0), new(100, 0, 0)));
        scene.Engine.SetView(OcctViewOrientation.Top); scene.Engine.FitAll();
        var screen = scene.Engine.WorldToScreen(default);
        var second = scene.Engine.ScreenToPlane(screen.X + 6, screen.Y, default, OcctVector3d.UnitZ);
        var a = new CadPointEntity(default); var b = new CadPointEntity(second);
        w.Document.AddRange([a, b]); w.WorkPlane.BeginToolPlane(default);
        w.Snap.Active = true; w.Snap.Modes = CadSnapType.Node;
        Assert.AreSame(a, w.Snap.Resolve(screen.X, screen.Y, default, w.WorkPlane)!.Value.Entity);
        Assert.AreSame(a, w.Snap.Resolve(screen.X + 4, screen.Y, second, w.WorkPlane)!.Value.Entity);
        Assert.AreSame(b, w.Snap.Resolve(screen.X + 6, screen.Y, second, w.WorkPlane)!.Value.Entity);
        w.Tools.CancelCurrent(); InteractionTests.AssertNeutral(w);
    }

    [TestMethod]
    public void OffsetCanBeCanceledWithoutAdvancingTheTool()
    {
        using var w = new CadWorkspace(); w.Tools.Activate("line");
        Assert.IsTrue(w.Tools.BeginOffsetInput());
        w.Tools.SubmitPointText("10,20");
        Assert.IsTrue(w.Tools.StepBackCurrent());
        Assert.AreEqual(0, w.Tools.ActiveTool!.Stage);
        Assert.IsNull(w.Precision.OffsetOrigin);
        Assert.AreEqual(CadPointInputMode.Absolute, w.Precision.PointMode);
    }

    [TestMethod]
    [DataRow("#100,200", 100d, 200d)]
    [DataRow("100,200", 100d, 200d)]
    [DataRow("@100,0", 110d, 20d)]
    [DataRow("@100<90", 10d, 120d)]
    [DataRow("100<90", 0d, 100d)]
    public void CoordinatesHaveStableAbsoluteAndRelativeOrigins(string text, double x, double y)
    {
        var plane = new CadWorkPlane(); plane.BeginToolPlane(new(50, 60, 0));
        Assert.IsTrue(CadCoordinateInputParser.TryParse(text, new(10, 20, 0), plane, out var input));
        Assert.AreEqual(x, input.Point.X, 1e-8); Assert.AreEqual(y, input.Point.Y, 1e-8);
    }

    [TestMethod]
    [DataRow("1,,2")]
    [DataRow("1,2,")]
    [DataRow("@NaN,2")]
    [DataRow("1<Infinity")]
    public void MalformedCoordinatesAreRejected(string text) =>
        Assert.IsFalse(CadCoordinateInputParser.TryParse(text, default, new CadWorkPlane(), out _));

    [TestMethod]
    public void OffsetConsumesBasePointWithoutAdvancingTool()
    {
        using var w = new CadWorkspace(); var commands = CadCommandManager.ForWorkspace(w);
        w.Tools.Activate("line");
        Assert.IsTrue(commands.Execute("O").Success);
        Assert.IsTrue(commands.Execute("10,20").Success);
        Assert.AreEqual(0, w.Tools.ActiveTool!.Stage);
        Assert.AreEqual(new OcctPoint3d(10, 20, 0), w.Precision.OffsetOrigin);
        Assert.IsTrue(commands.Execute("5,6").Success);
        Assert.AreEqual(1, w.Tools.ActiveTool!.Stage);
        Assert.IsNull(w.Precision.OffsetOrigin);
        Assert.AreEqual(new OcctPoint3d(15, 26, 0), w.Tools.ActiveTool.PrecisionReferencePoint);
        w.Tools.CancelCurrent(); InteractionTests.AssertNeutral(w);
    }

    [TestMethod]
    [TestCategory("Native")]
    [DataRow("point")]
    [DataRow("command")]
    [DataRow("pointer")]
    [DataRow("enter")]
    [DataRow("submit")]
    public void InputSurfacesCommitSameCircleAndOneUndo(string route)
    {
        using var scene = new NativeScene(); var w = scene.Workspace;
        w.Tools.Activate("circle"); w.Tools.CommitPoint(default);
        var pixel = scene.Engine.WorldToScreen(new(20, 0, 0));
        w.Tools.HandlePointer(new(OcctPointerInputKind.Moved, OcctPointerButton.None,
            OcctPointerButtons.None, pixel.X, pixel.Y, 0, OcctInputModifiers.None));
        var preview = (CadCircleEntity)w.Preview.Entity!;
        var exact = w.LastResolvedPoint!.Value.Point;
        switch (route)
        {
            case "point": w.Tools.SubmitCurrent(new CadResolvedPoint(exact, null)); break;
            case "command":
                var text = FormattableString.Invariant($"#{exact.X},{exact.Y},{exact.Z}");
                Assert.IsTrue(CadCommandManager.ForWorkspace(w).Execute(text).Success); break;
            case "pointer": w.Tools.HandlePointer(new(OcctPointerInputKind.Pressed, OcctPointerButton.Left,
                OcctPointerButtons.Left, pixel.X, pixel.Y, 0, OcctInputModifiers.None)); break;
            case "enter": Assert.IsTrue(w.Tools.HandleKey(new(OcctKeyInputKind.Pressed, OcctKey.Enter, OcctInputModifiers.None))); break;
            case "submit": Assert.IsTrue(w.Tools.SubmitCurrent()); break;
        }
        var circle = (CadCircleEntity)w.Document.Entities.Single();
        Assert.AreEqual(preview.Radius, circle.Radius, 1e-7);
        InteractionTests.AssertNeutral(w);
        Assert.IsTrue(w.Undo()); Assert.IsFalse(w.History.CanUndo);
    }

    [TestMethod]
    [TestCategory("Native")]
    public void EndpointPriorityWinsOverCloserMidpointAndTemporarySnapOverridesIt()
    {
        using var scene = new NativeScene(); var w = scene.Workspace;
        var endpoint = new CadLineEntity(default, new(100, 0, 0));
        var midpoint = new CadLineEntity(new(-99, 1, 0), new(101, 1, 0));
        w.Document.AddRange([endpoint, midpoint]); scene.Engine.FitAll();
        w.WorkPlane.BeginToolPlane(default); w.Snap.Active = true;
        w.Snap.Modes = CadSnapType.Endpoint | CadSnapType.Midpoint;
        var point = new OcctPoint3d(1, 1, 0); var pixel = scene.Engine.WorldToScreen(point);
        var snap = w.Snap.Resolve(pixel.X, pixel.Y, point, w.WorkPlane);
        Assert.AreEqual(CadSnapType.Endpoint, snap!.Value.Type);
        w.Snap.TemporaryModes = CadSnapType.Midpoint;
        snap = w.Snap.Resolve(pixel.X, pixel.Y, point, w.WorkPlane);
        Assert.AreEqual(CadSnapType.Midpoint, snap!.Value.Type);
        w.Tools.CancelCurrent(); InteractionTests.AssertNeutral(w);
    }
}
