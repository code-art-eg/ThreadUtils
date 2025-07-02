namespace CodeArt.ThreadUtils;

/// <summary>
///  Async lock that interacts with using statement
/// This is based on https://devblogs.microsoft.com/pfxteam/building-async-coordination-primitives-part-6-asynclock/
/// This class allows having both synchronous and asynchronous lock acquisition
/// </summary>
public sealed class AsyncLock
{
    /// <summary>
    /// Waiters queue
    /// </summary>
    private readonly ConcurrentQueue<IWaiter> _waiters = new();

    /// <summary>
    /// Whether the lock is taken
    /// </summary>
    private int _lockTaken;

    /// <summary>
    /// Constructor. Creates a new instance of <see cref="AsyncLock"/> class
    /// </summary>
    public AsyncLock()
    {
    }

    /// <summary>
    /// Acquire an exclusive lock
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the wait</param>
    /// <returns>returns a task that completes when the lock is acquired</returns>
    public ValueTask<IDisposable> LockAsync(CancellationToken cancellationToken)
    {
        var releaser = new ReleaserDisposable(this);
        if (Interlocked.CompareExchange(ref _lockTaken, 1, 0) == 0)
        {
            return new ValueTask<IDisposable>(releaser);
        }

        var tcs = new TaskCompletionSource<IDisposable>();
        var registration = cancellationToken.Register(() => { tcs.TrySetCanceled(); }, false);
        var waiter = new AsyncWaiter(registration, tcs, releaser);
        _waiters.Enqueue(waiter);

        return new ValueTask<IDisposable>(tcs.Task);
    }

    /// <summary>
    /// Acquire an exclusive lock
    /// </summary>
    /// <returns>returns a task that completes when the lock is acquired</returns>
    public ValueTask<IDisposable> LockAsync()
    {
        var releaser = new ReleaserDisposable(this);
        if (Interlocked.CompareExchange(ref _lockTaken, 1, 0) == 0)
        {
            return new ValueTask<IDisposable>(releaser);
        }

        var tcs = new TaskCompletionSource<IDisposable>();
        _waiters.Enqueue(new AsyncWaiter(default, tcs, releaser));
        return new ValueTask<IDisposable>(tcs.Task);
    }

    /// <summary>
    /// Acquire an exclusive lock
    /// </summary>
    /// <returns>returns an object that would release the lock when disposed.</returns>
    public IDisposable Lock()
    {
        var releaser = new ReleaserDisposable(this);
        if (Interlocked.CompareExchange(ref _lockTaken, 1, 0) == 0)
        {
            return releaser;
        }
        _waiters.Enqueue(releaser);
        lock (releaser)
        {
            Monitor.Wait(releaser);
        }
        return releaser;
    }
    
    /// <summary>
    /// Acquire an exclusive lock
    /// </summary>
    /// 
    /// <returns>returns an object that would release the lock when disposed.</returns>
    public IDisposable Lock(TimeSpan timeout)
    {
        var releaser = new ReleaserDisposable(this);
        if (Interlocked.CompareExchange(ref _lockTaken, 1, 0) == 0)
        {
            return releaser;
        }
        _waiters.Enqueue(releaser);
        lock (releaser)
        {
            if (!Monitor.Wait(releaser, timeout))
            {
                releaser.Dispose();
                throw new TimeoutException("Timeout waiting for lock");
            }
        }
        return releaser;
    }

    /// <summary>
    /// Release lock
    /// </summary>
    private void Release()
    {
        IWaiter? toWake;
        do
        {
            if (_waiters.TryDequeue(out toWake)) continue;
            Interlocked.Exchange(ref _lockTaken, 0);
            return;
        }
        while(!toWake.Awaken());
    }

    #region Nested type: Releaser

    /// <summary>
    ///     a releaser helper that implements IDisposable to support
    ///     using statement
    /// </summary>
    private sealed class ReleaserDisposable : IDisposable, IWaiter
    {
        /// <summary>
        ///     underlying lock
        /// </summary>
        private readonly AsyncLock _toRelease;

        private int _disposed;

        internal ReleaserDisposable(AsyncLock toRelease)
        {
            _toRelease = toRelease;
        }

        #region IDisposable Members

        /// <summary>
        ///     Dispose releases the lock
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0) _toRelease.Release();
        }

        #endregion

        public bool Awaken()
        {
            lock (this)
            {
                if (_disposed == 1)
                {
                    return false;
                }
                Monitor.Pulse(this);
            }
            return true;
        }
    }

    #endregion
}