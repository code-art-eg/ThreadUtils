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
    private readonly Queue<object> _waiters = new();

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
    public Task<IDisposable> LockAsync(CancellationToken cancellationToken)
    {
        lock (_waiters)
        {
            if (!_lockTaken)
            {
                _lockTaken = true;
                return Task.FromResult<IDisposable>(new ReleaserDisposable(this));
            }

            var tcs = new TaskCompletionSource<IDisposable>();
            var registration = cancellationToken.Register(() => { tcs.TrySetCanceled(); }, false);
            var pair = new TaskSourceAndRegistrationPair(registration, tcs);
            _waiters.Enqueue(pair);

            return tcs.Task;
        }
    }

    /// <summary>
    /// Acquire an exclusive lock
    /// </summary>
    /// <returns>returns a task that completes when the lock is acquired</returns>
    public Task<IDisposable> LockAsync()
    {
        lock (_waiters)
        {
            if (!_lockTaken)
            {
                _lockTaken = true;
                return Task.FromResult<IDisposable>(new ReleaserDisposable(this));
            }

            var tcs = new TaskCompletionSource<IDisposable>();
            _waiters.Enqueue(tcs);
            return tcs.Task;
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
        while (true)
        {
            object? toWake = null;
            lock (_waiters)
            {
                if (_waiters.Count > 0)
                {
                    toWake = _waiters.Dequeue();
                }
                else
                {
                    _lockTaken = false;
                }
            }

            switch (toWake)
            {
                case TaskCompletionSource<IDisposable> tcs:
                    tcs.TrySetResult(new ReleaserDisposable(this));
                    break;
                case TaskSourceAndRegistrationPair pair:
                {
                    pair.Registration.Dispose();
                    if (!pair.Source.TrySetResult(new ReleaserDisposable(this)))
                    {
                        // Task was cancelled when a cancellationToken was cancelled
                        // Try to release another waiter if any
                        continue;
                    }
                    break;
                }
                case ReleaserDisposable releaser:
                {
                    lock (releaser)
                    {
                        Monitor.Pulse(releaser);
                    }

                    break;
                }
            }

            break;
        }
    }

    #region Nested type: Releaser

    /// <summary>
    ///     a releaser helper that implements IDisposable to support
    ///     using statement
    /// </summary>
    private sealed class ReleaserDisposable : IDisposable
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
    }

    #endregion

    #region Nested type: TaskSourceAndRegistrationPair

    private sealed class TaskSourceAndRegistrationPair(
        CancellationTokenRegistration registration,
        TaskCompletionSource<IDisposable> source)
    {
        public CancellationTokenRegistration Registration { get; } = registration;
        public TaskCompletionSource<IDisposable> Source { get; } = source;
    }

    #endregion
}