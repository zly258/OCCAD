namespace OCCAD;

public interface ICadHistoryEntry
{
    string Name { get; }
    void Undo();
    void Redo();
}

public sealed class CadHistoryChangedEventArgs(
    bool canUndo,
    bool canRedo,
    string? undoName,
    string? redoName,
    long stateId) : EventArgs
{
    public bool CanUndo { get; } = canUndo;
    public bool CanRedo { get; } = canRedo;
    public string? UndoName { get; } = undoName;
    public string? RedoName { get; } = redoName;
    public long StateId { get; } = stateId;
}

public sealed class CadHistory
{
    public const int DefaultMaxEntries = 100;

    private readonly Stack<CadHistoryRecord> _undo = [];
    private readonly Stack<CadHistoryRecord> _redo = [];
    private long _nextStateId;

    public CadHistory(int maxEntries = DefaultMaxEntries)
    {
        if (maxEntries <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maxEntries),
                "History capacity must be greater than zero.");
        MaxEntries = maxEntries;
    }

    public int MaxEntries { get; }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string? UndoName =>
        _undo.TryPeek(out var record) ? record.Entry.Name : null;
    public string? RedoName =>
        _redo.TryPeek(out var record) ? record.Entry.Name : null;
    public long CurrentStateId { get; private set; }

    public event EventHandler<CadHistoryChangedEventArgs>? Changed;

    public void Execute(ICadHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        entry.Redo();
        RecordApplied(entry);
    }

    public void RecordApplied(ICadHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var record = new CadHistoryRecord(
            entry,
            CurrentStateId,
            ++_nextStateId);
        CurrentStateId = record.AfterStateId;
        _undo.Push(record);
        TrimUndo();
        _redo.Clear();
        RaiseChanged();
    }

    public bool Undo()
    {
        if (!_undo.TryPeek(out var record)) return false;

        record.Entry.Undo();
        _undo.Pop();
        CurrentStateId = record.BeforeStateId;
        _redo.Push(record);
        RaiseChanged();
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPeek(out var record)) return false;

        record.Entry.Redo();
        _redo.Pop();
        CurrentStateId = record.AfterStateId;
        _undo.Push(record);
        RaiseChanged();
        return true;
    }

    public void Clear()
    {
        var changed =
            _undo.Count > 0 ||
            _redo.Count > 0 ||
            CurrentStateId != 0 ||
            _nextStateId != 0;

        _undo.Clear();
        _redo.Clear();
        CurrentStateId = 0;
        _nextStateId = 0;

        if (changed) RaiseChanged();
    }

    private void TrimUndo()
    {
        if (_undo.Count <= MaxEntries) return;

        var retained = _undo.Take(MaxEntries).Reverse().ToArray();
        _undo.Clear();
        foreach (var record in retained)
            _undo.Push(record);
    }

    private void RaiseChanged() =>
        Changed?.Invoke(
            this,
            new CadHistoryChangedEventArgs(
                CanUndo,
                CanRedo,
                UndoName,
                RedoName,
                CurrentStateId));

    private sealed record CadHistoryRecord(
        ICadHistoryEntry Entry,
        long BeforeStateId,
        long AfterStateId);
}

internal sealed class CadSetCurrentLayerHistoryEntry(
    CadLayerManager manager,
    CadLayer before,
    CadLayer after) : ICadHistoryEntry
{
    private readonly CadLayerManager _manager =
        manager ?? throw new ArgumentNullException(nameof(manager));
    private readonly CadLayer _before =
        before ?? throw new ArgumentNullException(nameof(before));
    private readonly CadLayer _after =
        after ?? throw new ArgumentNullException(nameof(after));

    public string Name => "Set Current Layer";

    public void Undo() => _manager.SetCurrent(_before);
    public void Redo() => _manager.SetCurrent(_after);
}

internal sealed class CadAddEntitiesHistoryEntry : ICadHistoryEntry
{
    private readonly CadDocument _document;
    private readonly CadEntity[] _entities;

    public CadAddEntitiesHistoryEntry(
        CadDocument document,
        IEnumerable<CadEntity> entities,
        string name)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        ArgumentNullException.ThrowIfNull(entities);
        _entities = entities.ToArray();
        if (_entities.Length == 0)
            throw new ArgumentException(
                "At least one entity is required.",
                nameof(entities));
        Name = name;
    }

    public string Name { get; }

    public void Undo() => _document.RemoveRange(_entities.Reverse());
    public void Redo() => _document.AddRange(_entities);
}

internal sealed class CadReplaceEntitiesHistoryEntry : ICadHistoryEntry
{
    private readonly CadDocument _document;
    private readonly CadEntity[] _before;
    private readonly CadEntity[] _after;

    public CadReplaceEntitiesHistoryEntry(
        CadDocument document,
        IEnumerable<CadEntity> before,
        IEnumerable<CadEntity> after,
        string name)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        _before = before.Distinct().ToArray();
        _after = after.Distinct().ToArray();
        if (_before.Length == 0)
            throw new ArgumentException(
                "At least one source entity is required.",
                nameof(before));
        Name = string.IsNullOrWhiteSpace(name) ? "Edit" : name.Trim();
    }

    public string Name { get; }

    public void Undo()
    {
        _document.RemoveRange(_after.Reverse());
        _document.AddRange(_before);
    }

    public void Redo()
    {
        _document.RemoveRange(_before.Reverse());
        _document.AddRange(_after);
    }
}

internal sealed class CadRemoveEntitiesHistoryEntry : ICadHistoryEntry
{
    private readonly CadDocument _document;
    private readonly CadEntity[] _entities;

    public CadRemoveEntitiesHistoryEntry(
        CadDocument document,
        IEnumerable<CadEntity> entities)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        ArgumentNullException.ThrowIfNull(entities);
        _entities = entities.Distinct().ToArray();
        if (_entities.Length == 0)
            throw new ArgumentException(
                "At least one entity is required.",
                nameof(entities));
    }

    public string Name =>
        _entities.Length == 1
            ? "Delete"
            : $"Delete {_entities.Length}";

    public void Undo() => _document.AddRange(_entities);
    public void Redo() => _document.RemoveRange(_entities.Reverse());
}

internal sealed class CadGeometryHistoryEntry : ICadHistoryEntry
{
    private readonly CadEntity[] _targets;
    private readonly CadEntity[] _before;
    private readonly CadEntity[] _after;

    public CadGeometryHistoryEntry(
        IEnumerable<CadEntity> targets,
        IEnumerable<CadEntity> before,
        IEnumerable<CadEntity> after,
        string name)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        _targets = targets.ToArray();
        _before = before.ToArray();
        _after = after.ToArray();
        if (_targets.Length == 0 ||
            _targets.Length != _before.Length ||
            _targets.Length != _after.Length)
            throw new ArgumentException(
                "Geometry history arrays must be non-empty and have matching lengths.");
        Name = string.IsNullOrWhiteSpace(name) ? "Edit" : name;
    }

    public string Name { get; }

    public void Undo() => Restore(_before);
    public void Redo() => Restore(_after);

    private void Restore(IReadOnlyList<CadEntity> snapshots)
    {
        for (var index = 0; index < _targets.Length; index++)
            _targets[index].RestoreGeometrySnapshot(snapshots[index]);
    }
}

internal sealed class CadEntityStateHistoryEntry : ICadHistoryEntry
{
    private readonly CadEntity[] _targets;
    private readonly CadEntity[] _before;
    private readonly CadEntity[] _after;

    public CadEntityStateHistoryEntry(
        IEnumerable<CadEntity> targets,
        IEnumerable<CadEntity> before,
        IEnumerable<CadEntity> after,
        string name)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        _targets = targets.ToArray();
        _before = before.ToArray();
        _after = after.ToArray();
        if (_targets.Length == 0 ||
            _targets.Length != _before.Length ||
            _targets.Length != _after.Length)
            throw new ArgumentException(
                "Entity state history arrays must be non-empty and have matching lengths.");
        Name = string.IsNullOrWhiteSpace(name)
            ? "Property Edit"
            : name;
    }

    public string Name { get; }

    public void Undo() => Restore(_before);
    public void Redo() => Restore(_after);

    private void Restore(IReadOnlyList<CadEntity> snapshots)
    {
        for (var index = 0; index < _targets.Length; index++)
            _targets[index].RestoreState(snapshots[index]);
    }
}

internal sealed class CadLayerStateHistoryEntry(
    CadLayer layer,
    CadLayerState before,
    CadLayerState after,
    string name) : ICadHistoryEntry
{
    private readonly CadLayer _layer =
        layer ?? throw new ArgumentNullException(nameof(layer));
    private readonly CadLayerState _before = before;
    private readonly CadLayerState _after = after;

    public string Name { get; } =
        string.IsNullOrWhiteSpace(name) ? "Layer Edit" : name;

    public void Undo() => _layer.RestoreState(_before);
    public void Redo() => _layer.RestoreState(_after);
}

internal sealed class CadAddLayerHistoryEntry : ICadHistoryEntry
{
    private readonly CadLayerManager _manager;
    private readonly CadLayer _layer;
    private readonly CadLayer _previousCurrent;
    private readonly int _index;
    private readonly bool _makeCurrent;

    public CadAddLayerHistoryEntry(
        CadLayerManager manager,
        CadLayer layer,
        CadLayer previousCurrent,
        bool makeCurrent)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        _previousCurrent = previousCurrent ??
            throw new ArgumentNullException(nameof(previousCurrent));
        _index = _manager.IndexOf(_layer);
        _makeCurrent = makeCurrent;
    }

    public string Name => "Add Layer";

    public void Undo()
    {
        _manager.Remove(_layer);
        if (_makeCurrent && _manager.Layers.Contains(_previousCurrent))
            _manager.SetCurrent(_previousCurrent);
    }

    public void Redo()
    {
        _manager.Insert(_layer, _index);
        if (_makeCurrent)
            _manager.SetCurrent(_layer);
    }
}

internal sealed class CadRemoveLayerHistoryEntry : ICadHistoryEntry
{
    private readonly CadLayerManager _manager;
    private readonly CadLayer _layer;
    private readonly int _index;
    private readonly bool _wasCurrent;

    public CadRemoveLayerHistoryEntry(
        CadLayerManager manager,
        CadLayer layer)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        _index = _manager.IndexOf(_layer);
        _wasCurrent = ReferenceEquals(_manager.Current, _layer);
    }

    public string Name => "Remove Layer";

    public void Undo()
    {
        _manager.Insert(_layer, _index);
        if (_wasCurrent)
            _manager.SetCurrent(_layer);
    }

    public void Redo() => _manager.Remove(_layer);
}
