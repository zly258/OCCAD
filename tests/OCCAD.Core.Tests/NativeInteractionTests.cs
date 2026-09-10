using System.Runtime.InteropServices;
using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
[TestCategory("Native")]
public sealed class NativeInteractionTests
{
    [TestMethod]
    public void GripMarkerCanBeResizedBetweenSessions()
    {
        using var scene = new NativeScene(); var w = scene.Workspace;
        var line = new CadLineEntity(default, new(10, 0, 0));
        w.Document.Add(line);
        foreach (var size in new[] { 7, 31, 15 })
        {
            w.Grips.MarkerSize = size;
            w.Tools.BeginGripEdit(new(line, 0, default));
            Assert.IsTrue(w.Grips.HasDragTransient);
            w.Tools.CancelCurrent();
            InteractionTests.AssertNeutral(w);
            Assert.AreEqual(1, scene.Engine.ObjectCount);
        }
    }

    [TestMethod]
    public void NativeTopologyModesFollowScopeMaskAndRegeneration()
    {
        using var scene = new NativeScene(); var w = scene.Workspace;
        var box = new CadBoxEntity(default, 20, 20, 20); w.Document.Add(box);
        w.Selection.SetScope(CadSelectionScope.Subobject, CadSubshapeMask.Edge);
        scene.Engine.FitAll();
        scene.Engine.SelectRectangle(0, 0, 640, 480, false, true);
        var edges = scene.Engine.GetSelectedHits();
        Assert.IsNotEmpty(edges);
        Assert.IsTrue(edges.All(hit => hit.SubshapeType == OcctShapeType.Edge));
        scene.Engine.ClearSelection();
        w.Selection.SubshapeMask = CadSubshapeMask.Face;
        box.Length = 30;
        scene.Engine.FitAll();
        scene.Engine.SelectRectangle(0, 0, 640, 480, false, true);
        var faces = scene.Engine.GetSelectedHits();
        Assert.IsNotEmpty(faces);
        Assert.IsTrue(faces.All(hit => hit.SubshapeType == OcctShapeType.Face));
        scene.Engine.ClearSelection();
        w.Selection.Scope = CadSelectionScope.Entity;
        scene.Engine.SelectRectangle(0, 0, 640, 480, false, true);
        Assert.IsTrue(scene.Engine.GetSelectedHits().All(hit => !hit.IsSubshape));
    }

    [TestMethod]
    public void CircleCancelRemovesNativePreview()
    {
        using var scene = new NativeScene(); var w = scene.Workspace;
        w.Tools.Activate("circle"); w.Tools.CommitPoint(default);
        Move(w, new(20, 0, 0));
        Assert.IsTrue(w.Preview.IsVisible);
        w.Tools.CancelCurrent();
        InteractionTests.AssertNeutral(w);
        Assert.AreEqual(0, scene.Engine.ObjectCount);
        Assert.IsFalse(w.History.CanUndo);
    }

    [TestMethod]
    public void ExplicitGripPlaneWinsOverPlacement()
    {
        using var scene = new NativeScene(); var w = scene.Workspace;
        var line = new CadLineEntity(default, new(10, 0, 0));
        line.RotatePlacement(default, OcctVector3d.UnitX, 90); w.Document.Add(line);
        var plane = new CadGripWorkPlane(default, OcctVector3d.UnitY, OcctVector3d.UnitZ);
        w.Tools.BeginGripEdit(new(line, 0, default, plane));
        Assert.AreEqual(OcctVector3d.UnitY, w.WorkPlane.XAxis);
        Assert.AreEqual(OcctVector3d.UnitZ, w.WorkPlane.YAxis);
        w.Tools.CancelCurrent(); InteractionTests.AssertNeutral(w);
    }

    [TestMethod]
    public void CirclePreviewCommitAndUndoHaveNoOrphans()
    {
        using var scene = new NativeScene(); var w = scene.Workspace;
        w.Tools.Activate("circle");
        Assert.IsTrue(w.Tools.CommitPoint(default));
        Move(w, new(20, 0, 0));
        Assert.IsTrue(w.Preview.IsVisible);
        Assert.IsEmpty(w.Document.Entities);
        Assert.IsFalse(w.History.CanUndo);
        Assert.IsTrue(w.Tools.CommitPoint(new(20, 0, 0)));
        InteractionTests.AssertNeutral(w);
        Assert.HasCount(1, w.Document.Entities);
        w.Selection.Clear();
        Assert.AreEqual(1, scene.Engine.ObjectCount);
        Assert.IsTrue(w.History.Undo());
        Assert.AreEqual(0, scene.Engine.ObjectCount);
        Assert.IsFalse(w.History.CanUndo);
        Assert.IsTrue(w.History.Redo());
        Assert.AreEqual(1, scene.Engine.ObjectCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GripUsesLocalPlaneAndRestoresScene(bool commit)
    {
        using var scene = new NativeScene(); var w = scene.Workspace;
        var line = new CadLineEntity(default, new(10, 0, 0));
        line.RotatePlacement(default, OcctVector3d.UnitX, 90);
        w.Document.Add(line);
        w.WorkPlane.SetPreset(CadWorkPlanePreset.YZ);
        var user = w.WorkPlane.UserPlane;
        w.Tools.BeginGripEdit(new(line, 0, line.ToWorldPoint(line.Start)));
        Assert.AreEqual(1, w.WorkPlane.YAxis.Z, 1e-10);
        Assert.IsTrue(w.WorkPlane.GripPlaneFixed);
        Assert.IsFalse(w.Tools.TryChangeDrawingPlane(CadWorkPlanePreset.XY));
        Assert.IsTrue(w.Preview.IsVisible);
        Assert.IsTrue(w.Grips.HasDragTransient);
        Assert.IsFalse(w.History.CanUndo);
        if (commit) Assert.IsTrue(w.Tools.CommitPoint(new(0, 0, 5)));
        else w.Tools.CancelCurrent();
        InteractionTests.AssertNeutral(w);
        Assert.AreEqual(user, w.WorkPlane.EffectivePlane);
        Assert.AreEqual(1, scene.Engine.ObjectCount);
        if (commit)
        {
            Assert.IsTrue(w.History.Undo());
            Assert.AreEqual(OcctPoint3d.Origin, line.Start);
            Assert.IsFalse(w.History.CanUndo);
        }
        else Assert.AreEqual(OcctPoint3d.Origin, line.Start);
    }

    [TestMethod]
    public void PointerFailureClearsReplacementOwnershipAndToolCanContinue()
    {
        using var scene = new NativeScene(); var w = scene.Workspace;
        w.Document.Add(new CadLineEntity(default, new(10, 0, 0)));
        w.Tools.Register<FailingPointerTool>("test.pointer");
        w.Tools.Activate("test.pointer");
        Assert.IsTrue(w.Preview.HasTransient);
        Move(w, new(20, 0, 0));
        Assert.IsFalse(w.Preview.HasTransient);
        Assert.IsNotNull(w.Tools.ActiveTool);
        Assert.AreEqual(1, scene.Engine.ObjectCount);
        w.Tools.CancelCurrent(); InteractionTests.AssertNeutral(w);
    }

    public sealed class FailingPointerTool : CadTool
    {
        public override string Id => "test.pointer";
        public override string DisplayName => Id;
        protected override void OnActivated()
        {
            var source = Context.Document.Entities[0];
            ShowReplacementPreview([source], [source.Duplicate()]);
        }
        public override bool HandlePointer(OcctPointerInputEventArgs input) => throw new ArgumentException("unsolvable geometry");
    }

    private static void Move(CadWorkspace w, OcctPoint3d point)
    {
        var screen = w.Engine!.WorldToScreen(point);
        w.Tools.HandlePointer(new(OcctPointerInputKind.Moved, OcctPointerButton.None,
            OcctPointerButtons.None, screen.X, screen.Y, 0, OcctInputModifiers.None));
    }
}

internal sealed class NativeScene : IDisposable
{
    private readonly nint _display;
    private readonly nuint _window;

    public CadApplicationCore Application { get; }
    public OcctEngine Engine { get; }
    public CadWorkspace Workspace => Application.Workspace;
    public CadCommandManager Commands => Application.Commands;

    public NativeScene()
    {
        if (!OperatingSystem.IsLinux() ||
            Environment.GetEnvironmentVariable("OCCAD_NATIVE_TESTS") != "1")
        {
            Assert.Inconclusive(
                "Set OCCAD_NATIVE_TESTS=1 with an X11 display and the native SDK to run viewer tests.");
        }

        XInitThreads();
        _display = XOpenDisplay(null);
        Assert.AreNotEqual(nint.Zero, _display, "An X11 display is required.");
        _window = XCreateSimpleWindow(
            _display,
            XDefaultRootWindow(_display),
            0,
            0,
            640,
            480,
            0,
            0,
            0);
        XMapWindow(_display, _window);
        XSync(_display, false);

        Engine = new OcctEngine();
        Engine.Initialize((nint)_window);
        Application = new CadApplicationCore();
        Workspace.AttachEngine(Engine);
    }

    public void Dispose()
    {
        Application.Dispose();
        Engine.Dispose();
        XDestroyWindow(_display, _window);
        XCloseDisplay(_display);
    }

    [DllImport("libX11.so.6")] private static extern int XInitThreads();
    [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(string? display);
    [DllImport("libX11.so.6")] private static extern nuint XDefaultRootWindow(nint display);
    [DllImport("libX11.so.6")] private static extern nuint XCreateSimpleWindow(nint display, nuint parent, int x, int y, uint width, uint height, uint borderWidth, nuint border, nuint background);
    [DllImport("libX11.so.6")] private static extern int XMapWindow(nint display, nuint window);
    [DllImport("libX11.so.6")] private static extern int XSync(nint display, bool discard);
    [DllImport("libX11.so.6")] private static extern int XDestroyWindow(nint display, nuint window);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
}
