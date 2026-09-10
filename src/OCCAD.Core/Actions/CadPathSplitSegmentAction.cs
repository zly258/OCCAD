namespace OCCAD;

public sealed class CadPathSplitSegmentAction(CadWorkspace workspace)
    : CadAction(workspace)
{
    public override string Id => "modify.path.splitsegment";
    public override string DisplayName => "Split Path Segment";
    public override string Description =>
        "Split the selected Path segment at the selected subobject point.";
    public override bool IsRepeatable => false;

    public override bool CanExecute() =>
        TryGetTarget(out _, out _, out _);

    public override void Execute()
    {
        if (!TryGetTarget(out var path, out var segmentIndex, out var point))
        {
            throw new InvalidOperationException(
                "Split Path Segment requires one selected Path edge.");
        }

        Workspace.ApplyGeneratedGeometryChange(
            [path],
            "Split Path Segment",
            entity => ((CadPathEntity)entity).SplitSegment(
                segmentIndex,
                point));
    }

    private bool TryGetTarget(
        out CadPathEntity path,
        out int segmentIndex,
        out OcctNet.OcctPoint3d point)
    {
        path = null!;
        segmentIndex = -1;
        point = default;

        if (Workspace.Subobjects.Primary is not { } primary ||
            !primary.TryGetPathSegment(out var segment) ||
            primary.Entity is not CadPathEntity value ||
            !Workspace.Document.IsEntitySelectable(value))
            return false;

        path = value;
        segmentIndex = segment.Index;
        point = primary.Point;
        return point.IsFinite;
    }
}
