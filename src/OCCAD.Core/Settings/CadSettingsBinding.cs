namespace OCCAD;

/// <summary>
/// Synchronizes application preferences with their authoritative Core services.
/// The settings store owns persistence; runtime services own live behavior.
/// Synchronization is intentionally two-way so direct CAD interactions persist
/// without adding UI-specific state mirrors.
/// </summary>
public sealed class CadSettingsBinding : IDisposable
{
    private CadWorkspace? _workspace;
    private CadSettingsStore? _store;
    private bool _synchronizing;

    internal CadSettingsBinding(
        CadWorkspace workspace,
        CadSettingsStore store)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _store = store ?? throw new ArgumentNullException(nameof(store));

        _store.Changed += StoreChanged;
        _workspace.Drafting.Changed += DraftingChanged;
        _workspace.Snap.SettingsChanged += SnapSettingsChanged;
        _workspace.Grips.SettingsChanged += GripSettingsChanged;
        Refresh();
    }

    public void Refresh()
    {
        var workspace = RequiredWorkspace();
        var store = RequiredStore();

        Synchronize(() =>
        {
            workspace.Selection.PixelTolerance = store.Get(
                CadSettingKeys.SelectionTolerance,
                workspace.Selection.PixelTolerance);

            workspace.Grips.MarkerSize = store.Get(
                CadSettingKeys.GripSize,
                workspace.Grips.MarkerSize);
            workspace.Grips.PixelTolerance = store.Get(
                CadSettingKeys.GripTolerance,
                workspace.Grips.PixelTolerance);

            workspace.Snap.Enabled = store.Get(
                CadSettingKeys.SnapEnabled,
                workspace.Snap.Enabled);
            workspace.Snap.Modes = store.Get(
                CadSettingKeys.SnapModes,
                workspace.Snap.Modes);
            workspace.Snap.MarkerSize = store.Get(
                CadSettingKeys.SnapSize,
                workspace.Snap.MarkerSize);
            workspace.Snap.PixelTolerance = store.Get(
                CadSettingKeys.SnapTolerance,
                workspace.Snap.PixelTolerance);

            workspace.Drafting.OrthogonalTrackingEnabled = store.Get(
                CadSettingKeys.OrthogonalTrackingEnabled,
                workspace.Drafting.OrthogonalTrackingEnabled);
            workspace.Drafting.PolarTrackingEnabled = store.Get(
                CadSettingKeys.PolarTrackingEnabled,
                workspace.Drafting.PolarTrackingEnabled);
            workspace.Drafting.PolarIncrementDegrees = store.Get(
                CadSettingKeys.PolarIncrementDegrees,
                workspace.Drafting.PolarIncrementDegrees);
            workspace.Drafting.TrackingToleranceDegrees = store.Get(
                CadSettingKeys.TrackingToleranceDegrees,
                workspace.Drafting.TrackingToleranceDegrees);

            PersistGripUnsafe(workspace, store);
            PersistSnapUnsafe(workspace, store);
            PersistDraftingUnsafe(workspace, store);
        });
    }

    public void Dispose()
    {
        var workspace = Interlocked.Exchange(ref _workspace, null);
        var store = Interlocked.Exchange(ref _store, null);

        if (store is not null)
            store.Changed -= StoreChanged;
        if (workspace is not null)
        {
            workspace.Drafting.Changed -= DraftingChanged;
            workspace.Snap.SettingsChanged -= SnapSettingsChanged;
            workspace.Grips.SettingsChanged -= GripSettingsChanged;
        }
    }

    private void StoreChanged(
        object? sender,
        CadSettingChangedEventArgs args)
    {
        if (_synchronizing)
            return;

        var workspace = RequiredWorkspace();
        var store = RequiredStore();

        Synchronize(() =>
        {
            var normalizeGrip = false;
            var normalizeSnap = false;
            var normalizeDrafting = false;

            switch (args.Key)
            {
                case CadSettingKeys.SelectionTolerance:
                    workspace.Selection.PixelTolerance = store.Get(
                        args.Key,
                        workspace.Selection.PixelTolerance);
                    break;

                case CadSettingKeys.GripSize:
                    workspace.Grips.MarkerSize = store.Get(
                        args.Key,
                        workspace.Grips.MarkerSize);
                    normalizeGrip = true;
                    break;
                case CadSettingKeys.GripTolerance:
                    workspace.Grips.PixelTolerance = store.Get(
                        args.Key,
                        workspace.Grips.PixelTolerance);
                    normalizeGrip = true;
                    break;

                case CadSettingKeys.SnapEnabled:
                    workspace.Snap.Enabled = store.Get(
                        args.Key,
                        workspace.Snap.Enabled);
                    normalizeSnap = true;
                    break;
                case CadSettingKeys.SnapModes:
                    workspace.Snap.Modes = store.Get(
                        args.Key,
                        workspace.Snap.Modes);
                    normalizeSnap = true;
                    break;
                case CadSettingKeys.SnapSize:
                    workspace.Snap.MarkerSize = store.Get(
                        args.Key,
                        workspace.Snap.MarkerSize);
                    normalizeSnap = true;
                    break;
                case CadSettingKeys.SnapTolerance:
                    workspace.Snap.PixelTolerance = store.Get(
                        args.Key,
                        workspace.Snap.PixelTolerance);
                    normalizeSnap = true;
                    break;

                case CadSettingKeys.OrthogonalTrackingEnabled:
                    workspace.Drafting.OrthogonalTrackingEnabled = store.Get(
                        args.Key,
                        workspace.Drafting.OrthogonalTrackingEnabled);
                    normalizeDrafting = true;
                    break;
                case CadSettingKeys.PolarTrackingEnabled:
                    workspace.Drafting.PolarTrackingEnabled = store.Get(
                        args.Key,
                        workspace.Drafting.PolarTrackingEnabled);
                    normalizeDrafting = true;
                    break;
                case CadSettingKeys.PolarIncrementDegrees:
                    workspace.Drafting.PolarIncrementDegrees = store.Get(
                        args.Key,
                        workspace.Drafting.PolarIncrementDegrees);
                    normalizeDrafting = true;
                    break;
                case CadSettingKeys.TrackingToleranceDegrees:
                    workspace.Drafting.TrackingToleranceDegrees = store.Get(
                        args.Key,
                        workspace.Drafting.TrackingToleranceDegrees);
                    normalizeDrafting = true;
                    break;
            }

            if (normalizeGrip)
                PersistGripUnsafe(workspace, store);
            if (normalizeSnap)
                PersistSnapUnsafe(workspace, store);
            if (normalizeDrafting)
                PersistDraftingUnsafe(workspace, store);
        });
    }

    private void DraftingChanged(object? sender, EventArgs args)
    {
        if (_synchronizing)
            return;

        var workspace = RequiredWorkspace();
        var store = RequiredStore();
        Synchronize(() => PersistDraftingUnsafe(workspace, store));
    }

    private void SnapSettingsChanged(object? sender, EventArgs args)
    {
        if (_synchronizing)
            return;

        var workspace = RequiredWorkspace();
        var store = RequiredStore();
        Synchronize(() => PersistSnapUnsafe(workspace, store));
    }

    private void GripSettingsChanged(object? sender, EventArgs args)
    {
        if (_synchronizing)
            return;

        var workspace = RequiredWorkspace();
        var store = RequiredStore();
        Synchronize(() => PersistGripUnsafe(workspace, store));
    }

    private static void PersistGripUnsafe(
        CadWorkspace workspace,
        CadSettingsStore store)
    {
        store.Set(
            CadSettingKeys.GripSize,
            workspace.Grips.MarkerSize);
        store.Set(
            CadSettingKeys.GripTolerance,
            workspace.Grips.PixelTolerance);
    }

    private static void PersistDraftingUnsafe(
        CadWorkspace workspace,
        CadSettingsStore store)
    {
        store.Set(
            CadSettingKeys.OrthogonalTrackingEnabled,
            workspace.Drafting.OrthogonalTrackingEnabled);
        store.Set(
            CadSettingKeys.PolarTrackingEnabled,
            workspace.Drafting.PolarTrackingEnabled);
        store.Set(
            CadSettingKeys.PolarIncrementDegrees,
            workspace.Drafting.PolarIncrementDegrees);
        store.Set(
            CadSettingKeys.TrackingToleranceDegrees,
            workspace.Drafting.TrackingToleranceDegrees);
    }

    private static void PersistSnapUnsafe(
        CadWorkspace workspace,
        CadSettingsStore store)
    {
        store.Set(
            CadSettingKeys.SnapEnabled,
            workspace.Snap.Enabled);
        store.Set(
            CadSettingKeys.SnapModes,
            workspace.Snap.Modes);
        store.Set(
            CadSettingKeys.SnapSize,
            workspace.Snap.MarkerSize);
        store.Set(
            CadSettingKeys.SnapTolerance,
            workspace.Snap.PixelTolerance);
    }

    private void Synchronize(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_synchronizing)
            return;

        _synchronizing = true;
        try
        {
            action();
        }
        finally
        {
            _synchronizing = false;
        }
    }

    private CadWorkspace RequiredWorkspace() =>
        _workspace ?? throw new ObjectDisposedException(nameof(CadSettingsBinding));

    private CadSettingsStore RequiredStore() =>
        _store ?? throw new ObjectDisposedException(nameof(CadSettingsBinding));
}
