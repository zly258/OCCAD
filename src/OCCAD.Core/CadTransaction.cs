namespace OCCAD;

/// <summary>Explicit batch transaction. Commit once; disposal without Commit rolls back.</summary>
public sealed class CadTransaction : IDisposable
{
    private readonly CadWorkspace _workspace;
    private readonly string _name;
    private readonly CadWorkspaceSnapshot _before;
    private readonly bool _wasModified;
    private readonly IDisposable? _batch;
    private readonly IDisposable _changes;
    private bool _committed;
    private bool _hasChanges;
    private bool _disposed;

    public CadTransaction(CadWorkspace workspace, string name = "Modify")
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name.Trim();
        _wasModified = workspace.IsModified;
        _before = CadWorkspaceSnapshot.Capture(workspace);
        workspace.History.SuspendRecording();
        try
        {
            _batch = workspace.Engine?.BeginDisplayBatch();
            _changes = workspace.Document.BeginChangeSet();
        }
        catch
        {
            _batch?.Dispose();
            workspace.History.ResumeRecording();
            throw;
        }
    }

    public void Commit()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_committed) return;
        var after = CadWorkspaceSnapshot.Capture(_workspace);
        _hasChanges = !_before.SameState(after, _workspace.Entities);
        _workspace.History.ResumeRecording();
        // RecordApplied publishes after updating history. A failing observer must
        // not make Dispose roll back a command that already has an Undo entry.
        _committed = true;
        if (_hasChanges)
            _workspace.History.RecordApplied(new SnapshotEntry(_workspace, _name, _before, after));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (!_committed) _before.Restore(_workspace);
        }
        finally
        {
            _workspace.History.ResumeRecording();
            try { _changes.Dispose(); }
            finally
            {
                try { _batch?.Dispose(); }
                finally
                {
                    if (!_committed || !_hasChanges) _workspace.RestoreModifiedState(_wasModified);
                }
            }
        }
    }

    // Small commands retain their efficient, operation-specific history entries.
    internal static void Execute(CadWorkspace workspace, ICadHistoryEntry entry)
    {
        using var batch = workspace.Engine?.BeginDisplayBatch();
        using var changes = workspace.Document.BeginChangeSet();
        workspace.History.Execute(entry);
    }

    internal static bool ApplyEntities(CadWorkspace workspace, IReadOnlyList<CadEntity> targets,
        string name, Action<CadEntity> mutation, bool geometryOnly = false)
    {
        if (targets.Count == 0) return false;
        var before = workspace.CaptureEntityStates(targets);
        var wasModified = workspace.IsModified;
        var recorded = false;
        Exception? failure = null;
        using (workspace.Engine?.BeginDisplayBatch())
        using (workspace.Document.BeginChangeSet())
        {
            try
            {
                foreach (var entity in targets) mutation(entity);
                var after = workspace.CaptureEntityStates(targets);
                if (before.Zip(after).Any(pair => !workspace.Entities.StateEquals(pair.First, pair.Second)))
                {
                    // History notifications may throw after the entry is installed.
                    var state = workspace.History.CurrentStateId;
                    try
                    {
                        workspace.History.RecordApplied(geometryOnly
                            ? new CadGeometryHistoryEntry(targets, before, after, name)
                            : new CadEntityStateHistoryEntry(targets, before, after, name));
                        recorded = true;
                    }
                    finally { recorded |= workspace.History.CurrentStateId != state; }
                }
            }
            catch (Exception error)
            {
                failure = error;
                if (!recorded)
                {
                    List<Exception> failures = [error];
                    for (var i = 0; i < targets.Count; i++)
                    {
                        try { targets[i].RestoreState(before[i]); }
                        catch (Exception restoreError) { failures.Add(restoreError); }
                    }
                    if (failures.Count > 1) failure = new AggregateException(failures);
                }
            }
        }
        if (!recorded) workspace.RestoreModifiedState(wasModified);
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        workspace.Selection.RefreshValidity();
        workspace.Subobjects.RefreshValidity();
        return recorded;
    }

    private sealed record SnapshotEntry(CadWorkspace Workspace, string Name,
        CadWorkspaceSnapshot Before, CadWorkspaceSnapshot After) : ICadHistoryEntry
    {
        public void Undo() => Restore(Before, After);
        public void Redo() => Restore(After, Before);
        private void Restore(CadWorkspaceSnapshot state, CadWorkspaceSnapshot rollback)
        {
            try { state.Restore(Workspace); }
            catch (Exception failure)
            {
                try { rollback.Restore(Workspace); }
                catch (Exception recovery) { throw new AggregateException(failure, recovery); }
                throw;
            }
        }
    }
}

internal sealed record CadWorkspaceSnapshot(
    CadEntity[] Entities, CadEntity[] States,
    CadLayer[] Layers, CadLayerState[] LayerStates, CadLayer CurrentLayer)
{
    public static CadWorkspaceSnapshot Capture(CadWorkspace workspace) => new(
        workspace.Document.Entities.ToArray(),
        workspace.CaptureEntityStates(workspace.Document.Entities).ToArray(),
        workspace.Layers.Layers.ToArray(),
        workspace.Layers.Layers.Select(layer => layer.CaptureState()).ToArray(),
        workspace.Layers.Current);

    public bool SameState(CadWorkspaceSnapshot other, CadEntityRegistry registry) =>
        Entities.SequenceEqual(other.Entities) && Layers.SequenceEqual(other.Layers) &&
        LayerStates.SequenceEqual(other.LayerStates) && ReferenceEquals(CurrentLayer, other.CurrentLayer) &&
        States.Zip(other.States).All(pair => registry.StateEquals(pair.First, pair.Second));

    public void Restore(CadWorkspace workspace)
    {
        using var batch = workspace.Engine?.BeginDisplayBatch();
        using var changes = workspace.Document.BeginChangeSet();
        workspace.Selection.Clear();
        // Detach first so layer-name restoration cannot rewrite surviving entities.
        workspace.Document.RemoveRange(workspace.Document.Entities.ToArray());
        workspace.Layers.RestoreSnapshot(Layers, LayerStates, CurrentLayer);
        for (var i = 0; i < Entities.Length; i++) Entities[i].RestoreState(States[i]);
        workspace.Document.AddRange(Entities);
    }
}
