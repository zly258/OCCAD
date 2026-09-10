using System.Globalization;
using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class CommandPointInputTests
{
    [TestMethod]
    public void CommandRoutesAbsoluteRelativeAndPolarPointsThroughActiveWorkPlane()
    {
        using var app = new CadApplicationCore();
        var workspace = app.Workspace;
        workspace.Tools.Register<PointProbeTool>("point-probe");
        workspace.WorkPlane.SetPreset(CadWorkPlanePreset.XZ);
        Assert.IsTrue(workspace.Tools.Activate("point-probe"));

        var tool = (PointProbeTool)workspace.Tools.ActiveTool!;

        Assert.IsTrue(app.Commands.Execute("#10,20").Success);
        Assert.AreEqual(new OcctPoint3d(10, 0, 20), tool.LastPoint);

        tool.ReferencePoint = new OcctPoint3d(10, 5, 20);
        Assert.IsTrue(app.Commands.Execute("@5,7").Success);
        Assert.AreEqual(new OcctPoint3d(15, 5, 27), tool.LastPoint);

        Assert.IsTrue(app.Commands.Execute("@10<90").Success);
        Assert.AreEqual(10.0, tool.LastPoint.X, 1e-9);
        Assert.AreEqual(5.0, tool.LastPoint.Y, 1e-9);
        Assert.AreEqual(30.0, tool.LastPoint.Z, 1e-9);
    }

    [TestMethod]
    public void CommandRecognizesSemicolonPointSyntaxForCommaDecimalCultures()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            using var app = new CadApplicationCore();
            var workspace = app.Workspace;
            workspace.Tools.Register<PointProbeTool>("point-probe");
            Assert.IsTrue(workspace.Tools.Activate("point-probe"));

            var result = app.Commands.Execute("1,5;2,5;3,5");
            Assert.IsTrue(result.Success);

            var tool = (PointProbeTool)workspace.Tools.ActiveTool!;
            Assert.AreEqual(new OcctPoint3d(1.5, 2.5, 3.5), tool.LastPoint);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void InvalidPointSyntaxDoesNotMutateActiveTool()
    {
        using var app = new CadApplicationCore();
        var workspace = app.Workspace;
        workspace.Tools.Register<PointProbeTool>("point-probe");
        Assert.IsTrue(workspace.Tools.Activate("point-probe"));

        var tool = (PointProbeTool)workspace.Tools.ActiveTool!;
        Assert.IsFalse(app.Commands.Execute("#").Success);
        Assert.IsNull(tool.LastPoint);
        Assert.AreSame(tool, workspace.Tools.ActiveTool);
    }

    public sealed class PointProbeTool : CadTool, ICadPointInputTool
    {
        public override string Id => "point-probe";
        public override string DisplayName => "Point Probe";
        public OcctPoint3d? ReferencePoint { get; set; }
        public OcctPoint3d? LastPoint { get; private set; }
        public override OcctPoint3d? PrecisionReferencePoint =>
            ReferencePoint ?? base.PrecisionReferencePoint;

        protected override void OnActivated() =>
            SetStage(0, "Point", CadPrecisionInputKind.None);

        public bool TryAcceptPoint(OcctPoint3d point)
        {
            if (!point.IsFinite)
                return false;
            LastPoint = point;
            return true;
        }
    }
}
