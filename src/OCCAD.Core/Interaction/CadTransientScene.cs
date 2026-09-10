namespace OCCAD;

public enum CadTransientChannel { ToolPreview, Snap, Tracking, GripDrag, Preselection, SelectionWindow, SelectionMarkers }
public enum CadTransientLifetime { Tool, Workspace }

/// <summary>Coordinates existing presentation channels; managers retain native object ownership.</summary>
public sealed class CadTransientScene
{
    private readonly Dictionary<CadTransientChannel, Channel> _channels = [];
    private long _nextOwner;
    public long CurrentToolOwner { get; private set; }
    public bool HasToolTransient => _channels.Values.Any(c => c.Lifetime == CadTransientLifetime.Tool && c.HasState());

    public IDisposable Register(CadTransientChannel channel, Action clear, Func<bool> hasState,
        CadTransientLifetime lifetime = CadTransientLifetime.Tool)
    {
        ArgumentNullException.ThrowIfNull(clear);
        ArgumentNullException.ThrowIfNull(hasState);
        var registration = new Channel(this, channel, clear, hasState, lifetime);
        _channels.Add(channel, registration);
        registration.Owner = lifetime == CadTransientLifetime.Tool ? CurrentToolOwner : 0;
        return registration;
    }

    internal long BeginToolSession()
    {
        if (CurrentToolOwner != 0)
            throw new InvalidOperationException(
                $"Transient tool session {CurrentToolOwner} is still active.");

        CurrentToolOwner = ++_nextOwner;
        foreach (var channel in _channels.Values.Where(c => c.Lifetime == CadTransientLifetime.Tool))
            channel.Owner = CurrentToolOwner;
        return CurrentToolOwner;
    }

    public void ClearOwner(long owner)
    {
        if (owner == 0)
            return;

        var owned = _channels.Values
            .Where(c =>
                c.Lifetime == CadTransientLifetime.Tool &&
                c.Owner == owner)
            .ToArray();

        if (owned.Length == 0)
        {
            if (CurrentToolOwner == owner)
                CurrentToolOwner = 0;
            return;
        }

        List<Exception> failures = [];
        ClearPass(owned, failures);

        // A native viewer operation can fail after partially clearing its
        // presentation. Retry only channels that still report live state before
        // releasing ownership. This keeps stale preview/grip/snap objects tied to
        // the originating Tool instead of silently transferring them to the next
        // session.
        var remaining = Remaining(owned, failures);
        if (remaining.Length > 0)
        {
            ClearPass(remaining, failures);
            remaining = Remaining(remaining, failures);
        }

        var remainingSet = remaining.ToHashSet();
        foreach (var channel in owned)
        {
            if (!remainingSet.Contains(channel))
                channel.Owner = 0;
        }

        if (remaining.Length == 0)
        {
            if (CurrentToolOwner == owner)
                CurrentToolOwner = 0;

            if (failures.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Transient owner {owner} cleanup recovered after {failures.Count} failure(s)." +
                    Environment.NewLine + string.Join(Environment.NewLine, failures));
            }
            return;
        }

        if (failures.Count == 0)
        {
            failures.Add(new InvalidOperationException(
                $"Transient owner {owner} still owns: {string.Join(", ", remaining.Select(static channel => channel.Kind))}."));
        }

        throw new AggregateException(
            $"Transient owner {owner} cleanup is incomplete; ownership was retained for retry.",
            failures);
    }

    public void ClearToolState() => Clear(_channels.Values.Where(c => c.Lifetime == CadTransientLifetime.Tool));

    public void ClearAll()
    {
        try
        {
            Clear(_channels.Values);
        }
        catch (Exception exception) when (IsRecoverableCleanupFailure(exception))
        {
            // ClearAll is used by workspace/view lifetime disposal. One native
            // presentation channel failing to delete must not abort the rest of
            // the workspace teardown and leave event subscriptions or document
            // presentations attached. Individual tool-session cleanup remains
            // strict through ClearOwner/ClearToolState.
            System.Diagnostics.Debug.WriteLine(
                $"Workspace transient cleanup completed with recoverable failures: {exception}");
        }
    }

    private static void Clear(IEnumerable<Channel> channels)
    {
        List<Exception> failures = [];
        ClearPass(channels.ToArray(), failures);
        if (failures.Count > 0)
            throw new AggregateException(failures);
    }

    private static void ClearPass(
        IEnumerable<Channel> channels,
        ICollection<Exception> failures)
    {
        foreach (var channel in channels.ToArray())
        {
            try
            {
                channel.Clear();
            }
            catch (Exception error)
            {
                failures.Add(new InvalidOperationException(
                    $"Transient channel {channel.Kind} failed to clear.",
                    error));
            }
        }
    }

    private static Channel[] Remaining(
        IEnumerable<Channel> channels,
        ICollection<Exception> failures)
    {
        List<Channel> remaining = [];
        foreach (var channel in channels)
        {
            try
            {
                if (channel.HasState())
                    remaining.Add(channel);
            }
            catch (Exception error)
            {
                failures.Add(new InvalidOperationException(
                    $"Transient channel {channel.Kind} failed to report cleanup state.",
                    error));
                remaining.Add(channel);
            }
        }
        return remaining.ToArray();
    }

    private static bool IsRecoverableCleanupFailure(Exception exception)
    {
        if (exception is AggregateException aggregate)
            return aggregate.Flatten().InnerExceptions.All(IsRecoverableCleanupFailure);

        if (exception is OutOfMemoryException or StackOverflowException or AccessViolationException)
            return false;

        return exception.InnerException is null ||
               IsRecoverableCleanupFailure(exception.InnerException);
    }

    private sealed class Channel(CadTransientScene scene, CadTransientChannel kind, Action clear,
        Func<bool> hasState, CadTransientLifetime lifetime) : IDisposable
    {
        public CadTransientChannel Kind { get; } = kind;
        public Action Clear { get; } = clear;
        public Func<bool> HasState { get; } = hasState;
        public CadTransientLifetime Lifetime { get; } = lifetime;
        public long Owner { get; set; }

        public void Dispose()
        {
            try
            {
                Clear();
            }
            catch (Exception exception) when (IsRecoverableCleanupFailure(exception))
            {
                // Registration disposal belongs to UI/workspace teardown. Keep a
                // channel with surviving native state registered so ClearAll can
                // retry it later, but never abort the caller's remaining event
                // unsubscription and resource cleanup for a recoverable failure.
                System.Diagnostics.Debug.WriteLine(
                    $"Transient channel {Kind} failed to clear during registration disposal: {exception}");
            }

            if (!HasState())
                scene._channels.Remove(Kind);
        }
    }
}
