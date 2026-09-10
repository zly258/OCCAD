namespace OCCAD;

/// <summary>
/// Applies application settings to their authoritative Core services. It does
/// not own CAD state and does not mirror service values.
/// </summary>
public sealed class CadSettingsBinding : IDisposable
{
    private CadWorkspace? _workspace;
    private CadSettingsStore? _store;

    internal CadSettingsBinding(
        CadWorkspace workspace,
        CadSettingsStore store)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _store.Changed += StoreChanged;
        Refresh();
    }

    public void Refresh()
    {
        var workspace = RequiredWorkspace();
        var store = RequiredStore();

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
    }

    public void Dispose()
    {
        var store = Interlocked.Exchange(ref _store, null);
        if (store is not null)
            store.Changed -= StoreChanged;
        _workspace = null;
    }

    private void StoreChanged(
        object? sender,
        CadSettingChangedEventArgs args)
    {
        var workspace = RequiredWorkspace();
        var store = RequiredStore();

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
                break;
            case CadSettingKeys.GripTolerance:
                workspace.Grips.PixelTolerance = store.Get(
                    args.Key,
                    workspace.Grips.PixelTolerance);
                break;

            case CadSettingKeys.SnapEnabled:
                workspace.Snap.Enabled = store.Get(
                    args.Key,
                    workspace.Snap.Enabled);
                break;
            case CadSettingKeys.SnapModes:
                workspace.Snap.Modes = store.Get(
                    args.Key,
                    workspace.Snap.Modes);
                break;
            case CadSettingKeys.SnapSize:
                workspace.Snap.MarkerSize = store.Get(
                    args.Key,
                    workspace.Snap.MarkerSize);
                break;
            case CadSettingKeys.SnapTolerance:
                workspace.Snap.PixelTolerance = store.Get(
                    args.Key,
                    workspace.Snap.PixelTolerance);
                break;

            case CadSettingKeys.OrthogonalTrackingEnabled:
                workspace.Drafting.OrthogonalTrackingEnabled = store.Get(
                    args.Key,
                    workspace.Drafting.OrthogonalTrackingEnabled);
                break;
            case CadSettingKeys.PolarTrackingEnabled:
                workspace.Drafting.PolarTrackingEnabled = store.Get(
                    args.Key,
                    workspace.Drafting.PolarTrackingEnabled);
                break;
            case CadSettingKeys.PolarIncrementDegrees:
                workspace.Drafting.PolarIncrementDegrees = store.Get(
                    args.Key,
                    workspace.Drafting.PolarIncrementDegrees);
                break;
            case CadSettingKeys.TrackingToleranceDegrees:
                workspace.Drafting.TrackingToleranceDegrees = store.Get(
                    args.Key,
                    workspace.Drafting.TrackingToleranceDegrees);
                break;
        }
    }

    private CadWorkspace RequiredWorkspace() =>
        _workspace ?? throw new ObjectDisposedException(nameof(CadSettingsBinding));

    private CadSettingsStore RequiredStore() =>
        _store ?? throw new ObjectDisposedException(nameof(CadSettingsBinding));
}
