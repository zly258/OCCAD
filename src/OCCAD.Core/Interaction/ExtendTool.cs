using OcctNet;

namespace OCCAD;

public sealed class ExtendTool : CadSelectionTransformToolBase
{
    public override string Id => "extend";
    public override string DisplayName => "Extend";
    public override bool AllowsPreselectionDuringDrawing => true;

    protected override void OnActivated()
    {
        SetSelectionFilter(
            new CadSelectionFilter(
                "extend.boundaries",
                static entity =>
                    entity is CadLineEntity or
                    CadPolylineEntity or
                    CadCircleEntity or
                    CadArcEntity or
                    CadPathEntity));
        base.OnActivated();
    }

    protected override void OnTransformStarted()
    {
        SetSelectionFilter(
            new CadSelectionFilter(
                "extend.targets",
                static entity =>
                    entity is CadLineEntity or
                    CadArcEntity or
                    CadPolylineEntity or
                    CadPathEntity));
        SetStageLocalized(
            0,
            "Cad.Prompt.extend.Target",
            "Extend: click near the endpoint to extend [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input))
            return true;

        if (State == CadToolState.WaitForSelect)
            return false;

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            UpdatePreview();
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        return CommitHovered();
    }

    private void UpdatePreview()
    {
        if (!TryBuild(out _, out var replacement))
        {
            Context.Preview.Clear();
            return;
        }

        Context.Preview.Show(replacement);
    }

    private bool CommitHovered()
    {
        if (!TryBuild(out var target, out var replacement))
        {
            SetPromptLocalized(
                "Cad.Prompt.extend.Invalid",
                "Extend: the hovered line cannot be extended to the selected boundary lines.");
            return true;
        }

        Context.Preview.Clear();
        Context.Workspace.ApplyGeneratedGeometryChange(
            [target],
            "Extend",
            entity => entity.RestoreGeometry(replacement));
        SetPromptLocalized(
            "Cad.Prompt.extend.Target",
            "Extend: click near the endpoint to extend [Esc cancel]");
        return true;
    }

    private bool TryBuild(
        out CadEntity target,
        out CadEntity replacement)
    {
        target = null!;
        replacement = null!;

        if (Context.Workspace.Preselection.Current is not
            {
                Entity: var hovered,
                Point: var hit
            } ||
            Entities.Contains(hovered))
            return false;

        var boundaries = Entities.ToArray();

        switch (hovered)
        {
            case CadLineEntity line:
                if (!CadLineEditGeometry.TryExtend(
                        line,
                        boundaries,
                        hit,
                        Context.WorkPlane,
                        out var lineReplacement))
                    return false;
                replacement = lineReplacement;
                break;

            case CadArcEntity arc:
                if (!CadArcEditGeometry.TryExtend(
                        arc,
                        boundaries,
                        hit,
                        Context.WorkPlane,
                        out var arcReplacement))
                    return false;
                replacement = arcReplacement;
                break;

            case CadPolylineEntity polyline:
                if (!CadPolylineEditGeometry.TryExtend(
                        polyline,
                        boundaries,
                        hit,
                        Context.WorkPlane,
                        out var polylineReplacement))
                    return false;
                replacement = polylineReplacement;
                break;

            case CadPathEntity path:
                if (!TryExtendPath(
                        path,
                        boundaries,
                        hit,
                        out var pathReplacement))
                    return false;
                replacement = pathReplacement;
                break;

            default:
                return false;
        }

        target = hovered;
        return true;
    }

    private bool TryExtendPath(
        CadPathEntity path,
        IReadOnlyList<CadEntity> boundaries,
        OcctPoint3d hitPoint,
        out CadPathEntity replacement)
    {
        replacement = null!;
        if (path.Closed ||
            path.Segments.Count == 0 ||
            !CadPlanarCurveIntersections.IsBoundaryOnPlane(
                path,
                Context.WorkPlane))
            return false;

        var extendStart =
            hitPoint.DistanceTo(path.Start) <=
            hitPoint.DistanceTo(path.End);
        var segmentIndex =
            extendStart
                ? 0
                : path.Segments.Count - 1;
        var source = path.Segments[segmentIndex];
        var endpoint =
            extendStart
                ? path.Start
                : path.End;

        CadEntity extended;
        switch (source)
        {
            case CadLineEntity line:
                if (!CadLineEditGeometry.TryExtend(
                        line,
                        boundaries,
                        endpoint,
                        Context.WorkPlane,
                        out var lineReplacement))
                    return false;
                extended = lineReplacement;
                break;

            case CadArcEntity arc:
                if (!CadArcEditGeometry.TryExtend(
                        arc,
                        boundaries,
                        endpoint,
                        Context.WorkPlane,
                        out var arcReplacement))
                    return false;
                extended = arcReplacement;
                break;

            default:
                return false;
        }

        var segments = path.Segments
            .Select(CadPathEntity.SnapshotSegment)
            .ToArray();
        segments[segmentIndex] = extended;
        try
        {
            replacement = path.CopyWithSegments(segments);
            return true;
        }
        catch (ArgumentException)
        {
            replacement = null!;
            return false;
        }
    }
}
