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
    private readonly ConcurrentDictionary<TKey, LockStatus> _queues = new();
    private static readonly LockStatus s_emptyLockStatus = new();

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
        var status = _queues.GetOrAdd(key, _ => new LockStatus());

        if (status.TrySetLockTaken())
        {
            return new ValueTask<IDisposable>(releaser);
        }

        var tcs = new TaskCompletionSource<IDisposable>();
        var registration = cancellationToken.Register(() => { tcs.TrySetCanceled(); }, false);

        var waiter = new AsyncWaiter(registration, tcs, releaser);
        status.Waiters.Enqueue(waiter);
        return new ValueTask<IDisposable>(tcs.Task);
    }

    /// <summary>
    /// Acquire an exclusive lock
    /// </summary>
    /// <returns>returns a task that completes when the lock is acquired</returns>
    public ValueTask<IDisposable> LockAsync(TKey key)
    {
        var releaser = new ReleaserDisposable(key, this);
        var status = _queues.GetOrAdd(key, _ => new LockStatus());

        if (status.TrySetLockTaken())
        {
            return new ValueTask<IDisposable>(releaser);
        }
        
        var tcs = new TaskCompletionSource<IDisposable>();
        status.Waiters.Enqueue(new AsyncWaiter(default, tcs, releaser));
        return new ValueTask<IDisposable>(tcs.Task);
    }

    /// <summary>
    /// Acquire an exclusive lock
    /// </summary>
    /// <returns>returns an object that would release the lock when disposed.</returns>
    public IDisposable Lock(TKey key)
    {
        var releaser = new ReleaserDisposable(key, this);
        var status = _queues.GetOrAdd(key, _ => new LockStatus());
        if (status.TrySetLockTaken())
        {
            return releaser;
        }
        
        status.Waiters.Enqueue(releaser);
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
        if (!_queues.TryGetValue(key, out var status))
        {
            throw new InvalidOperationException("Lock was not taken");
        }
        
        IWaiter? toWake;
        do
        {
            if (_queues.TryRemove(new KeyValuePair<TKey, LockStatus>(key, s_emptyLockStatus)))
            {
                return;
            }

            if (!status.Waiters.TryDequeue(out toWake))
            {
                // This should not happen because if the queue is actually empty,
                // the previous "if" statement should evaluate to true and the method would have returned.
                // Since no two objects can have the lock at the same time, 
                // there should be no risk of two threads calling Release simultaneously,
                // So there should be no race here.
                throw new InvalidOperationException("There was nothing to wake.");                
            }
            
        } while (!toWake.Awaken());
    }

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


        /// <summary>
        ///     Dispose, releases the lock
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0) _toRelease.Release(_key);
        }


        public bool Awaken()
        {
            lock (this)
            {
                Monitor.Pulse(this);
            }

            return true;
        }
    }


    /// <summary>
    /// 
    /// </summary>
    private sealed class LockStatus : IEquatable<LockStatus>
    {
        private int _lockTaken;

        public bool TrySetLockTaken()
        {
            return Interlocked.CompareExchange(ref _lockTaken, 1, 0) == 0;
        }

        public ConcurrentQueue<IWaiter> Waiters { get; } = new();

        /// <summary>
        /// The equality comparer compares the Waiters' Count only, the only value we care about is zero.
        /// This is because the comparison is only used when removing the LockStatus from the dictionary,
        /// and we want to remove if and only if the Waiters' Count reached zero.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(LockStatus? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.Waiters.Count == Waiters.Count;
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || obj is LockStatus other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Waiters.Count;
        }
    }
}