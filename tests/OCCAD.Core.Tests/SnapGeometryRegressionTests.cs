using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class SnapGeometryRegressionTests
{
    [TestMethod]
    public void IntersectionSnapReturnsExactPlanarIntersection()
    {
        using var w = new CadWorkspace();
        var horizontal = new CadLineEntity(
            new OcctPoint3d(-10.0, 0.0, 0.0),
            new OcctPoint3d(10.0, 0.0, 0.0));
        var vertical = new CadLineEntity(
            new OcctPoint3d(0.0, -10.0, 0.0),
            new OcctPoint3d(0.0, 10.0, 0.0));
        w.Document.AddRange([horizontal, vertical]);
        w.WorkPlane.BeginToolPlane(OcctPoint3d.Origin);
        w.Snap.Modes = CadSnapType.Intersection;

        var candidates = w.Snap.GetPrecisionCandidates(
            new OcctPoint3d(0.25, -0.4, 0.0),
            w.WorkPlane);
        var intersection = candidates.Single(
            point => point.Type == CadSnapType.Intersection);

        Assert.AreEqual(OcctPoint3d.Origin, intersection.Position);
        Assert.IsTrue(
            ReferenceEquals(intersection.Entity, horizontal) ||
            ReferenceEquals(intersection.Entity, vertical));
    }

    [TestMethod]
    public void PerpendicularSnapProjectsReferenceOntoFiniteCurve()
    {
        using var w = new CadWorkspace();
        var line = new CadLineEntity(
            new OcctPoint3d(-10.0, 0.0, 0.0),
            new OcctPoint3d(10.0, 0.0, 0.0));
        w.Document.Add(line);
        w.WorkPlane.BeginToolPlane(OcctPoint3d.Origin);
        w.Snap.Modes = CadSnapType.Perpendicular;

        var reference = new OcctPoint3d(3.0, 5.0, 0.0);
        var candidates = w.Snap.GetPrecisionCandidates(
            new OcctPoint3d(3.0, 0.5, 0.0),
            w.WorkPlane,
            reference);
        var perpendicular = candidates.Single(
            point => point.Type == CadSnapType.Perpendicular);

        Assert.AreEqual(3.0, perpendicular.Position.X, 1e-10);
        Assert.AreEqual(0.0, perpendicular.Position.Y, 1e-10);
        Assert.AreEqual(0.0, perpendicular.Position.Z, 1e-10);

        var curveDirection = line.End - line.Start;
        var referenceDirection = reference - perpendicular.Position;
        Assert.AreEqual(
            0.0,
            curveDirection.Dot(referenceDirection),
            1e-10);
    }

    [TestMethod]
    public void TangentSnapPointsSatisfyCircleTangency()
    {
        using var w = new CadWorkspace();
        var circle = new CadCircleEntity(
            OcctPoint3d.Origin,
            OcctVector3d.UnitZ,
            5.0);
        w.Document.Add(circle);
        w.WorkPlane.BeginToolPlane(OcctPoint3d.Origin);
        w.Snap.Modes = CadSnapType.Tangent;

        var reference = new OcctPoint3d(13.0, 0.0, 0.0);
        var candidates = w.Snap.GetPrecisionCandidates(
                new OcctPoint3d(4.0, 3.0, 0.0),
                w.WorkPlane,
                reference)
            .Where(point => point.Type == CadSnapType.Tangent)
            .ToArray();

        Assert.HasCount(2, candidates);
        foreach (var tangent in candidates)
        {
            Assert.AreSame(circle, tangent.Entity);
            Assert.AreEqual(
                circle.Radius,
                circle.Center.DistanceTo(tangent.Position),
                1e-9);

            var radius = tangent.Position - circle.Center;
            var tangentDirection = reference - tangent.Position;
            Assert.AreEqual(
                0.0,
                radius.Dot(tangentDirection),
                1e-8);
        }

        Assert.IsTrue(candidates[0].Position.Y * candidates[1].Position.Y < 0.0);
    }

    [TestMethod]
    public void MissingReferenceProducesNoPerpendicularOrTangentCandidates()
    {
        using var w = new CadWorkspace();
        var line = new CadLineEntity(
            new OcctPoint3d(-10.0, 0.0, 0.0),
            new OcctPoint3d(10.0, 0.0, 0.0));
        var circle = new CadCircleEntity(
            OcctPoint3d.Origin,
            OcctVector3d.UnitZ,
            5.0);
        w.Document.AddRange([line, circle]);
        w.WorkPlane.BeginToolPlane(OcctPoint3d.Origin);
        w.Snap.Modes = CadSnapType.Perpendicular | CadSnapType.Tangent;

        var candidates = w.Snap.GetPrecisionCandidates(
            new OcctPoint3d(2.0, 2.0, 0.0),
            w.WorkPlane,
            reference: null);

        Assert.IsEmpty(candidates);
    }
}
