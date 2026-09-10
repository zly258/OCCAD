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
        Exception? failure = null;
        try
        {
            Clear(_channels.Values.Where(c =>
                c.Lifetime == CadTransientLifetime.Tool &&
                c.Owner == owner));
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            foreach (var channel in _channels.Values.Where(c =>
                         c.Lifetime == CadTransientLifetime.Tool &&
                         c.Owner == owner))
            {
                channel.Owner = 0;
            }

            if (CurrentToolOwner == owner)
                CurrentToolOwner = 0;
        }

        if (failure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
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
        foreach (var channel in channels.ToArray())
        {
            try { channel.Clear(); }
            catch (Exception error) { failures.Add(new InvalidOperationException($"Transient channel {channel.Kind} failed to clear.", error)); }
        }
        if (failures.Count > 0) throw new AggregateException(failures);
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
