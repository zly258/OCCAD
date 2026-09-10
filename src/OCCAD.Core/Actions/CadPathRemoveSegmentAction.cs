namespace OCCAD;

public sealed class CadPathRemoveSegmentAction(CadWorkspace workspace)
    : CadAction(workspace)
{
    public override string Id => "modify.path.removesegment";
    public override string DisplayName => "Remove Path Segment";
    public override string Description =>
        "Remove the selected Path segment while preserving valid Path topology.";
    public override bool IsRepeatable => false;

    public override bool CanExecute() =>
        TryGetTarget(out _, out _);

    public override void Execute()
    {
        if (!TryGetTarget(out var path, out var segmentIndex))
        {
            throw new InvalidOperationException(
                "Remove Path Segment requires one selected Path edge.");
        }

        Workspace.ReplaceEntities(
            [path],
            BuildReplacements(path, segmentIndex),
            "Remove Path Segment");
    }

    private bool TryGetTarget(
        out CadPathEntity path,
        out int segmentIndex)
    {
        path = null!;
        segmentIndex = -1;

        if (Workspace.Subobjects.Primary is not { } primary ||
            !primary.TryGetPathSegment(out var segment) ||
            primary.Entity is not CadPathEntity value ||
            !Workspace.Document.IsEntitySelectable(value))
            return false;

        path = value;
        segmentIndex = segment.Index;
        return true;
    }

    private static IReadOnlyList<CadEntity> BuildReplacements(
        CadPathEntity path,
        int segmentIndex)
    {
        var segments = path.Segments;
        if ((uint)segmentIndex >= (uint)segments.Count)
            throw new ArgumentOutOfRangeException(nameof(segmentIndex));

        if (segments.Count == 1)
            return Array.Empty<CadEntity>();

        if (path.Closed)
        {
            var remaining = new List<CadEntity>(segments.Count - 1);
            for (var offset = 1; offset < segments.Count; offset++)
            {
                var index = (segmentIndex + offset) % segments.Count;
                remaining.Add(CadPathEntity.SnapshotSegment(segments[index]));
            }

            return [path.CopyWithSegments(remaining)];
        }

        if (segmentIndex == 0)
        {
            return
            [
                path.CopyWithSegments(
                    segments.Skip(1).Select(CadPathEntity.SnapshotSegment))
            ];
        }

        if (segmentIndex == segments.Count - 1)
        {
            return
            [
                path.CopyWithSegments(
                    segments.Take(segments.Count - 1)
                        .Select(CadPathEntity.SnapshotSegment))
            ];
        }

        return
        [
            path.CopyWithSegments(
                segments.Take(segmentIndex)
                    .Select(CadPathEntity.SnapshotSegment)),
            path.CopyWithSegments(
                segments.Skip(segmentIndex + 1)
                    .Select(CadPathEntity.SnapshotSegment))
        ];
    }
}
