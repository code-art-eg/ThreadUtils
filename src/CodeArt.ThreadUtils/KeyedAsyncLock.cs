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
    public Task<IDisposable> LockAsync(TKey key, CancellationToken cancellationToken)
    {
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
                return Task.FromResult<IDisposable>(new ReleaserDisposable(key, this));
            }

            var tcs = new TaskCompletionSource<IDisposable>();
            var registration = cancellationToken.Register(() =>
            {
                lock (_queues)
                {
                    tcs.TrySetCanceled();
                }
            }, false);
            var pair = new TaskSourceAndRegistrationPair(registration, tcs);
            status.Waiters.Enqueue(pair);

            return tcs.Task;
        }
    }

    /// <summary>
    /// Acquire an exclusive lock
    /// </summary>
    /// <returns>returns a task that completes when the lock is acquired</returns>
    public Task<IDisposable> LockAsync(TKey key)
    {
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
                return Task.FromResult<IDisposable>(new ReleaserDisposable(key, this));
            }

            var tcs = new TaskCompletionSource<IDisposable>();
            status.Waiters.Enqueue(tcs);
            return tcs.Task;
        }
    }

    /// <summary>
    /// Acquire an exclusive lock
    /// </summary>
    /// <returns>returns object that would release the lock when disposed.</returns>
    public IDisposable Lock(TKey key)
    {
        ReleaserDisposable newReleaser;
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
                return new ReleaserDisposable(key, this);
            }

            newReleaser = new ReleaserDisposable(key, this);
            status.Waiters.Enqueue(newReleaser);
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
    private void Release(TKey key)
    {
        lock (_queues)
        {
            if (!_queues.TryGetValue(key, out var status))
            {
                throw new InvalidOperationException("Lock was not taken");
            }

            while (true)
            {
                object? toWake = null;
                if (status.Waiters.Count > 0)
                {
                    toWake = status.Waiters.Dequeue();
                }
                else
                {
                    _queues.Remove(key);
                }

                switch (toWake)
                {
                    case TaskCompletionSource<IDisposable> tcs:
                        tcs.TrySetResult(new ReleaserDisposable(key, this));
                        break;
                    case TaskSourceAndRegistrationPair pair:
                    {
                        pair.Registration.Dispose();
                        if (pair.Source.Task.IsCanceled)
                        {
                            // Task was cancelled when a cancellationToken was cancelled
                            // Try to release another waiter if any
                            continue;
                        }

                        pair.Source.TrySetResult(new ReleaserDisposable(key, this));
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
    }

    #region Nested type: Releaser

    /// <summary>
    ///     a releaser helper that implements IDisposable to support
    ///     using statement
    /// </summary>
    private sealed class ReleaserDisposable : IDisposable
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

    #region Nested type : LockStatus

    private sealed class LockStatus
    {
        public bool LockTaken { get; set; }
        public Queue<object> Waiters { get; } = new();
    }

    #endregion
}