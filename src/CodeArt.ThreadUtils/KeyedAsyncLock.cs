namespace CodeArt.ThreadUtils;

/// <summary>
/// This class provides a way to lock on a key. It is similar to <see cref="AsyncLock"/> but allows to lock on a key.
/// The difference between using a Dictionary of <see cref="AsyncLock"/> is that no resources are used when
/// there is no lock taken on the key while a dictionary of <see cref="AsyncLock"/> would have an entry for each key.
/// This class allows having both synchronous and asynchronous lock acquisition
/// </summary>
/// <typeparam name="TKey">The type of the key to use.</typeparam>
public class KeyedAsyncLock<TKey> where TKey : IEquatable<TKey>
{
    /// <summary>
    /// Waiters queue
    /// </summary>
    private readonly Dictionary<TKey, LockStatus> _queues = new();

    /// <summary>
    /// Constructor. Creates a new instance of <see cref="AsyncLock"/> class
    /// </summary>
    public KeyedAsyncLock()
    {
    }

    /// <summary>
    /// Acquire an exclusive lock
    /// </summary>
    /// <param name="key">key of the lock to obtain</param>
    /// <param name="cancellationToken">Cancellation token to cancel the wait</param>
    /// <returns>returns a task that completes when the lock is acquired</returns>
    public ValueTask<IDisposable> LockAsync(TKey key, CancellationToken cancellationToken)
    {
        var releaser = new ReleaserDisposable(key, this);
        lock (_queues)
        {
            if (!_queues.TryGetValue(key, out var status))
            {
                status = new LockStatus();
                _queues.Add(key, status);
            }

            if (!status.LockTaken)
            {
                status.LockTaken = true;
                return new ValueTask<IDisposable>(releaser);
            }

            var tcs = new TaskCompletionSource<IDisposable>();
            var registration = cancellationToken.Register(() =>
            {
                lock (_queues)
                {
                    tcs.TrySetCanceled();
                }
            }, false);
            var waiter = new AsyncWaiter(registration, tcs, releaser);
            status.Waiters.Enqueue(waiter);

            return new ValueTask<IDisposable>(tcs.Task);
        }
    }

    /// <summary>
    /// Acquire an exclusive lock
    /// </summary>
    /// <returns>returns a task that completes when the lock is acquired</returns>
    public ValueTask<IDisposable> LockAsync(TKey key)
    {
        var releaser = new ReleaserDisposable(key, this);
        lock (_queues)
        {
            if (!_queues.TryGetValue(key, out var status))
            {
                status = new LockStatus();
                _queues.Add(key, status);
            }

            if (!status.LockTaken)
            {
                status.LockTaken = true;
                return new ValueTask<IDisposable>(releaser);
            }

            var tcs = new TaskCompletionSource<IDisposable>();
            status.Waiters.Enqueue(new AsyncWaiter(default, tcs, releaser));
            return new ValueTask<IDisposable>(tcs.Task);
        }
    }

    /// <summary>
    /// Acquire an exclusive lock
    /// </summary>
    /// <returns>returns object that would release the lock when disposed.</returns>
    public IDisposable Lock(TKey key)
    {
        var releaser = new ReleaserDisposable(key, this);
        lock (_queues)
        {
            if (!_queues.TryGetValue(key, out var status))
            {
                status = new LockStatus();
                _queues.Add(key, status);
            }

            if (!status.LockTaken)
            {
                status.LockTaken = true;
                return releaser;
            }

            status.Waiters.Enqueue(releaser);
        }

        lock (releaser)
        {
            Monitor.Wait(releaser);
        }

        return releaser;
    }

    /// <summary>
    /// Release lock
    /// </summary>
    private void Release(TKey key)
    {
        lock (_queues)
        {
            if (!_queues.TryGetValue(key, out var status))
            {
                throw new InvalidOperationException("Lock was not taken");
            }

            IWaiter toWake;
            do
            {
                if (status.Waiters.Count == 0)
                {
                    _queues.Remove(key);
                    return;
                }

                toWake = status.Waiters.Dequeue();
            }
            while (!toWake.Awaken());
        }
    }

    #region Nested type: Releaser

    /// <summary>
    ///     a releaser helper that implements IDisposable to support
    ///     using statement
    /// </summary>
    private sealed class ReleaserDisposable : IDisposable, IWaiter
    {
        private readonly TKey _key;

        /// <summary>
        ///     underlying lock
        /// </summary>
        private readonly KeyedAsyncLock<TKey> _toRelease;

        private int _disposed;

        internal ReleaserDisposable(TKey key, KeyedAsyncLock<TKey> toRelease)
        {
            _key = key;
            _toRelease = toRelease;
        }

        #region IDisposable Members

        /// <summary>
        ///     Dispose. releases the lock
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0) _toRelease.Release(_key);
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

    #region Nested type : LockStatus

    private sealed class LockStatus
    {
        public bool LockTaken { get; set; }
        public Queue<IWaiter> Waiters { get; } = new();
    }

    #endregion
}