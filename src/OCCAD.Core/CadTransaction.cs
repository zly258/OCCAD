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

        if (!_hasChanges)
        {
            _committed = true;
            return;
        }

        var state = _workspace.History.CurrentStateId;
        try
        {
            RecordApplied(
                _workspace.History,
                new SnapshotEntry(
                    _workspace,
                    _name,
                    _before,
                    after));
            _committed = true;
        }
        catch
        {
            // If history state advanced, the Undo entry is authoritative even if
            // a fatal observer failed afterwards. Only a failure before history
            // installation leaves this transaction uncommitted and eligible for
            // snapshot rollback in Dispose().
            _committed = _workspace.History.CurrentStateId != state;
            throw;
        }
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
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(entry);

        // Lightweight operations are allowed inside an explicit CadTransaction.
        // In that case the outer snapshot owns history and this entry only applies
        // its model mutation.
        if (workspace.History.RecordingSuspended)
        {
            entry.Redo();
            return;
        }

        var wasModified = workspace.IsModified;
        var state = workspace.History.CurrentStateId;
        var redoCompleted = false;
        var historyInstalled = false;
        var rollbackComplete = false;
        Exception? failure = null;

        using (workspace.Engine?.BeginDisplayBatch())
        {
            using (workspace.Document.BeginChangeSet())
            {
                try
                {
                    entry.Redo();
                    redoCompleted = true;
                    try
                    {
                        RecordApplied(workspace.History, entry);
                    }
                    finally
                    {
                        historyInstalled =
                            workspace.History.CurrentStateId != state;
                    }
                }
                catch (Exception error)
                {
                    failure = error;
                    if (redoCompleted && !historyInstalled)
                    {
                        try
                        {
                            entry.Undo();
                            rollbackComplete = true;
                        }
                        catch (Exception rollbackFailure)
                        {
                            failure = new AggregateException(
                                $"'{entry.Name}' applied but history installation and rollback both failed.",
                                error,
                                rollbackFailure);
                        }
                    }
                }
            }
        }

        if (failure is not null && rollbackComplete)
            workspace.RestoreModifiedState(wasModified);

        if (failure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    internal static void ApplyCreatedEntity(
        CadWorkspace workspace,
        CadEntity entity,
        string name,
        Action complete) =>
        ApplyCreatedEntities(
            workspace,
            [entity],
            name,
            complete);

    /// <summary>
    /// Applies newly-created entities and installs one lightweight history entry
    /// only after the caller-supplied completion step succeeds. If completion
    /// fails, all created entities are removed again and no Undo record is kept.
    /// </summary>
    internal static void ApplyCreatedEntities(
        CadWorkspace workspace,
        IReadOnlyList<CadEntity> entities,
        string name,
        Action complete)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(complete);

        if (workspace.History.RecordingSuspended)
            throw new InvalidOperationException(
                "A tool create commit cannot be nested inside another transaction.");

        var values = entities
            .Distinct()
            .ToArray();
        if (values.Length == 0)
            throw new ArgumentException(
                "At least one entity is required for a create commit.",
                nameof(entities));

        var wasModified = workspace.IsModified;
        var historyInstalled = false;
        var rollbackComplete = false;
        Exception? failure = null;

        using (workspace.Engine?.BeginDisplayBatch())
        {
            using (workspace.Document.BeginChangeSet())
            {
                try
                {
                    workspace.Document.AddRange(values);

                    // Tool completion is part of the command contract. The model is not
                    // considered committed until all tool-owned transient state has been
                    // released and the tool has returned to the neutral interaction state.
                    complete();

                    var state = workspace.History.CurrentStateId;
                    try
                    {
                        RecordApplied(
                            workspace.History,
                            new CadAddEntitiesHistoryEntry(
                                workspace.Document,
                                values,
                                name.Trim()));
                    }
                    finally
                    {
                        historyInstalled =
                            workspace.History.CurrentStateId != state;
                    }
                }
                catch (Exception error)
                {
                    failure = error;
                    if (!historyInstalled)
                    {
                        var failures = new List<Exception> { error };
                        var existing = values
                            .Where(workspace.Document.Entities.Contains)
                            .ToArray();
                        if (existing.Length > 0)
                        {
                            try
                            {
                                workspace.Document.RemoveRange(existing);
                            }
                            catch (Exception rollbackFailure)
                            {
                                failures.Add(rollbackFailure);
                            }
                        }

                        rollbackComplete = values.All(
                            entity => !workspace.Document.Entities.Contains(entity));

                        if (failures.Count > 1)
                        {
                            failure = new AggregateException(
                                "Create commit and rollback both failed.",
                                failures);
                        }
                    }
                }
            }
        }

        // ChangeSetCommitted drives IsModified, so restore the previous value only
        // after the outer change set has published its final Added+Removed batch.
        if (failure is not null &&
            !historyInstalled &&
            rollbackComplete)
        {
            workspace.RestoreModifiedState(wasModified);
        }

        if (failure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    internal static bool ApplyEntities(
        CadWorkspace workspace,
        IReadOnlyList<CadEntity> targets,
        string name,
        Action<CadEntity> mutation,
        bool geometryOnly = false,
        Action? complete = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(mutation);

        if (complete is not null &&
            workspace.History.RecordingSuspended)
        {
            throw new InvalidOperationException(
                "A tool entity commit cannot be nested inside another transaction.");
        }

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
                    // Tool completion belongs inside the same atomic boundary as
                    // the model mutation. A cleanup/deactivation failure therefore
                    // restores the entity states instead of reporting a committed
                    // operation as failed.
                    complete?.Invoke();

                    var state = workspace.History.CurrentStateId;
                    try
                    {
                        RecordApplied(
                            workspace.History,
                            geometryOnly
                                ? new CadGeometryHistoryEntry(targets, before, after, name)
                                : new CadEntityStateHistoryEntry(targets, before, after, name));
                    }
                    finally
                    {
                        recorded = workspace.History.CurrentStateId != state;
                    }
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

    private static void RecordApplied(
        CadHistory history,
        ICadHistoryEntry entry)
    {
        var state = history.CurrentStateId;
        try
        {
            history.RecordApplied(entry);
        }
        catch (Exception exception)
            when (history.CurrentStateId != state &&
                  IsRecoverableHistoryNotificationFailure(exception))
        {
            System.Diagnostics.Debug.WriteLine(
                $"History observer failed after '{entry.Name}' committed: {exception}");
        }
    }

    private static bool IsRecoverableHistoryNotificationFailure(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

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
