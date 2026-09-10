using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class PreciseDrawingTests
{
    [TestMethod]
    public void AbsoluteCoordinatesUseActiveCustomPlane()
    {
        var plane = new CadWorkPlane();
        plane.SetCustom(
            new OcctPoint3d(10, 20, 30),
            OcctVector3d.UnitY,
            OcctVector3d.UnitZ);

        Assert.IsTrue(CadCoordinateInputParser.TryParse(
            "2,3,4",
            OcctPoint3d.Origin,
            plane,
            out var input));

        Assert.AreEqual(CadCoordinateInputMode.AbsoluteCartesian, input.Mode);
        Assert.AreEqual(new OcctPoint3d(14, 22, 33), input.Point);
    }

    [TestMethod]
    public void RelativeCoordinatesUseActiveCustomPlane()
    {
        var plane = new CadWorkPlane();
        plane.SetCustom(
            OcctPoint3d.Origin,
            OcctVector3d.UnitY,
            OcctVector3d.UnitZ);
        var reference = new OcctPoint3d(100, 200, 300);

        Assert.IsTrue(CadCoordinateInputParser.TryParse(
            "@2,3,4",
            reference,
            plane,
            out var input));

        Assert.AreEqual(CadCoordinateInputMode.RelativeCartesian, input.Mode);
        Assert.AreEqual(new OcctPoint3d(104, 202, 303), input.Point);
    }

    [TestMethod]
    public void XzPresetUsesWorldXHorizontalAndWorldZVertical()
    {
        var plane = new CadWorkPlane();
        plane.SetPreset(CadWorkPlanePreset.XZ);

        Assert.AreEqual(OcctVector3d.UnitX, plane.XAxis);
        Assert.AreEqual(OcctVector3d.UnitZ, plane.YAxis);

        Assert.IsTrue(CadCoordinateInputParser.TryParse(
            "25,40",
            OcctPoint3d.Origin,
            plane,
            out var input));
        Assert.AreEqual(new OcctPoint3d(25, 0, 40), input.Point);
    }

    [TestMethod]
    public void RelativePolarUsesActivePlaneAxes()
    {
        var plane = new CadWorkPlane();
        plane.SetPreset(CadWorkPlanePreset.XZ);
        var reference = new OcctPoint3d(5, 6, 7);

        Assert.IsTrue(CadCoordinateInputParser.TryParse(
            "@10<90",
            reference,
            plane,
            out var input));

        Assert.AreEqual(CadCoordinateInputMode.RelativePolar, input.Mode);
        Assert.AreEqual(5.0, input.Point.X, 1e-9);
        Assert.AreEqual(6.0, input.Point.Y, 1e-9);
        Assert.AreEqual(17.0, input.Point.Z, 1e-9);
    }
}
