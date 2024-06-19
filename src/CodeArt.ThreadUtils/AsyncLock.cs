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
    private readonly Queue<IWaiter> _waiters = new();

    /// <summary>
    /// Whether the lock is taken
    /// </summary>
    private bool _lockTaken;

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
        lock (_waiters)
        {
            if (!_lockTaken)
            {
                _lockTaken = true;
                return new ValueTask<IDisposable>(releaser);
            }

            var tcs = new TaskCompletionSource<IDisposable>();
            var registration = cancellationToken.Register(() => { tcs.TrySetCanceled(); }, false);
            var waiter = new AsyncWaiter(registration, tcs, releaser);
            _waiters.Enqueue(waiter);

            return new ValueTask<IDisposable>(tcs.Task);
        }
    }

    /// <summary>
    /// Acquire an exclusive lock
    /// </summary>
    /// <returns>returns a task that completes when the lock is acquired</returns>
    public ValueTask<IDisposable> LockAsync()
    {
        var releaser = new ReleaserDisposable(this);
        lock (_waiters)
        {
            if (!_lockTaken)
            {
                _lockTaken = true;
                return new ValueTask<IDisposable>(releaser);
            }

            var tcs = new TaskCompletionSource<IDisposable>();
            _waiters.Enqueue(new AsyncWaiter(default, tcs, releaser));
            return new ValueTask<IDisposable>(tcs.Task);
        }
    }

    /// <summary>
    /// Acquire an exclusive lock
    /// </summary>
    /// <returns>returns object that would release the lock when disposed.</returns>
    public IDisposable Lock()
    {
        ReleaserDisposable newReleaser;
        lock (_waiters)
        {
            if (!_lockTaken)
            {
                _lockTaken = true;
                return new ReleaserDisposable(this);
            }

            newReleaser = new ReleaserDisposable(this);
            _waiters.Enqueue(newReleaser);
        }

        lock (newReleaser)
        {
            Monitor.Wait(newReleaser);
        }

        return newReleaser;
    }

    /// <summary>
    /// Release lock
    /// </summary>
    private void Release()
    {
        IWaiter? toWake;
        do
        {
            lock (_waiters)
            {
                if (_waiters.Count == 0)
                {
                    _lockTaken = false;
                    return;
                }
                toWake = _waiters.Dequeue();
            }
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
        ///     Dispose. releases the lock
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
                Monitor.Pulse(this);
            }
            return true;
        }
    }

    #endregion
}