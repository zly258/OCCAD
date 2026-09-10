namespace OCCAD;

public enum CadTransientChannel
{
    ToolPreview,
    Snap,
    Tracking,
    GripDrag,
    Preselection,
    SelectionWindow,
    SelectionMarkers
}

public enum CadTransientLifetime
{
    Tool,
    Workspace
}

/// <summary>
/// Central lifecycle authority for transient CAD presentation channels.
/// Individual presenters own native handles; this scene owns when every
/// channel must be cleared. Tool transitions are strict, while final workspace
/// shutdown is best-effort so one native deletion failure cannot strand the
/// remaining channels or block document detachment.
/// </summary>
public sealed class CadTransientScene
{
    private readonly Dictionary<CadTransientChannel, Channel> _channels = [];
    private long _nextOwner;

    public long CurrentToolOwner { get; private set; }

    public bool HasToolTransient =>
        _channels.Values.Any(channel =>
            channel.Lifetime == CadTransientLifetime.Tool &&
            channel.HasState());

    public IDisposable Register(
        CadTransientChannel channel,
        Action clear,
        Func<bool> hasState,
        CadTransientLifetime lifetime = CadTransientLifetime.Tool)
    {
        ArgumentNullException.ThrowIfNull(clear);
        ArgumentNullException.ThrowIfNull(hasState);

        if (_channels.ContainsKey(channel))
        {
            throw new InvalidOperationException(
                $"Transient channel '{channel}' is already registered.");
        }

        var registration = new Channel(
            this,
            channel,
            clear,
            hasState,
            lifetime);
        registration.Owner =
            lifetime == CadTransientLifetime.Tool
                ? CurrentToolOwner
                : 0;
        _channels.Add(channel, registration);
        return registration;
    }

    internal long BeginToolSession()
    {
        CurrentToolOwner = ++_nextOwner;
        foreach (var channel in _channels.Values.Where(channel =>
                     channel.Lifetime == CadTransientLifetime.Tool))
        {
            channel.Owner = CurrentToolOwner;
        }

        return CurrentToolOwner;
    }

    public void ClearOwner(long owner) =>
        ClearStrict(_channels.Values.Where(channel =>
            channel.Lifetime == CadTransientLifetime.Tool &&
            channel.Owner == owner));

    public void ClearToolState() =>
        ClearStrict(_channels.Values.Where(channel =>
            channel.Lifetime == CadTransientLifetime.Tool));

    /// <summary>
    /// Clears every channel during workspace shutdown. This method never throws:
    /// disposal must continue through every channel even when a native viewer is
    /// already partially destroyed. Operational tool cleanup remains strict via
    /// <see cref="ClearToolState"/> and <see cref="ClearOwner"/>.
    /// </summary>
    public void ClearAll()
    {
        foreach (var channel in _channels.Values.ToArray())
        {
            try
            {
                channel.Clear();
            }
            catch (Exception exception)
                when (IsRecoverableCleanupFailure(exception))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Transient shutdown cleanup failed for {channel.Kind}: {exception.Message}");
            }
        }
    }

    private static void ClearStrict(IEnumerable<Channel> channels)
    {
        List<Exception> failures = [];
        foreach (var channel in channels.ToArray())
        {
            try
            {
                channel.Clear();
            }
            catch (Exception error)
            {
                failures.Add(
                    new InvalidOperationException(
                        $"Transient channel {channel.Kind} failed to clear.",
                        error));
            }
        }

        if (failures.Count > 0)
            throw new AggregateException(failures);
    }

    private static bool IsRecoverableCleanupFailure(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

    private sealed class Channel(
        CadTransientScene scene,
        CadTransientChannel kind,
        Action clear,
        Func<bool> hasState,
        CadTransientLifetime lifetime) : IDisposable
    {
        public CadTransientChannel Kind { get; } = kind;
        public Action Clear { get; } = clear;
        public Func<bool> HasState { get; } = hasState;
        public CadTransientLifetime Lifetime { get; } = lifetime;
        public long Owner { get; set; }

        public void Dispose()
        {
            Clear();
            // Retain a failed native deletion so a later lifecycle cleanup can
            // retry instead of silently orphaning presentation state.
            if (!HasState())
                scene._channels.Remove(Kind);
        }
    }
}
