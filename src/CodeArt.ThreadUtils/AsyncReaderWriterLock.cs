namespace CodeArt.ThreadUtils;

/// <summary>
///     Async reader writer lock
///     Based on: 
///     https://devblogs.microsoft.com/pfxteam/building-async-coordination-primitives-part-7-asyncreaderwriterlock/
/// </summary>
public sealed class AsyncReaderWriterLock
{
    /// <summary>
    /// queue of waiting writers. also serves as the sync root for this object
    /// </summary>
    private readonly Queue<IReaderWriterLockWaiter> _writersQueue = new();

    /// <summary>
    ///   queue of waiting readers
    /// </summary>
    private readonly Queue<IReaderWriterLockWaiter> _readersQueue = new();

    /// <summary>
    ///     status. O means no one has the lock. -1 a writer has the lock, +ve is the number of readers having the lock.
    /// </summary>
    private int _status;

    /// <summary>
    ///     constructor
    /// </summary>
    public AsyncReaderWriterLock()
    {
    }

    /// <summary>
    ///     acquire a reader lock
    /// </summary>
    /// <returns>A releaser that releases the lock</returns>
    public IDisposable ReaderLock()
    {
        var releaser = new ReleaserDisposable(this, false);
        lock (_writersQueue)
        {
            // to avoid starvation of writers we only allow readers to acquire the lock if there are no writers waiting
            if (_status >= 0 && _writersQueue.Count == 0)
            {
                ++_status;
                return releaser;
            }

            _readersQueue.Enqueue(releaser);
        }

        lock (releaser)
        {
            Monitor.Wait(releaser);
        }

        return releaser;
    }

    /// <summary>
    ///     acquire a reader lock
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel waiting</param>
    /// <returns>a task that completes when a reader lock is acquired.</returns>
    public ValueTask<IDisposable> ReaderLockAsync(CancellationToken cancellationToken)
    {
        var releaser = new ReleaserDisposable(this, false);
        lock (_writersQueue)
        {
            // to avoid starvation of writers we only allow readers to acquire the lock if there are no writers waiting
            if (_status >= 0 && _writersQueue.Count == 0)
            {
                ++_status;
                return new ValueTask<IDisposable>(releaser);
            }

            var tcs = new TaskCompletionSource<IDisposable>();
            var registration = cancellationToken.Register(() => tcs.TrySetCanceled(), false);
            var waiter = new ReaderWriterAsyncWaiter(registration, tcs, releaser);
            _readersQueue.Enqueue(waiter);
            return new ValueTask<IDisposable>(tcs.Task);
        }
    }

    /// <summary>
    ///     acquire a reader lock
    /// </summary>
    /// <returns>a task that completes when a reader lock is acquired.</returns>
    public ValueTask<IDisposable> ReaderLockAsync()
    {
        var releaser = new ReleaserDisposable(this, false);
        lock (_writersQueue)
        {
            // to avoid starvation of writers we only allow readers to acquire the lock if there are no writers waiting 
            if (_status >= 0 && _writersQueue.Count == 0)
            {
                ++_status;
                return new ValueTask<IDisposable>(releaser);
            }

            var tcs = new TaskCompletionSource<IDisposable>();
            var waiter = new ReaderWriterAsyncWaiter(default, tcs, releaser);
            _readersQueue.Enqueue(waiter);
            return new ValueTask<IDisposable>(tcs.Task);
        }
    }

    /// <summary>
    ///     acquire a writer lock
    /// </summary>
    /// <returns>a releaser that releases the lock.</returns>
    public IDisposable WriterLock()
    {
        var releaser = new ReleaserDisposable(this, true);
        lock (_writersQueue)
        {
            // if no one has the lock, we can take it
            if (_status == 0)
            {
                _status = -1;
                return releaser;
            }

            _writersQueue.Enqueue(releaser);
        }

        lock (releaser)
        {
            Monitor.Wait(releaser);
        }

        return releaser;
    }

    /// <summary>
    ///     acquire a writer lock
    /// </summary>
    /// <param name="cancellationToken">cancellation token used to cancel the wait</param>
    /// <returns>a task that completes when a writer lock is acquired.</returns>
    public ValueTask<IDisposable> WriterLockAsync(CancellationToken cancellationToken)
    {
        var releaser = new ReleaserDisposable(this, true);
        lock (_writersQueue)
        {
            // if no one has the lock, we can take it
            if (_status == 0)
            {
                _status = -1;
                return new ValueTask<IDisposable>(releaser);
            }

            var tcs = new TaskCompletionSource<IDisposable>();
            var registration = cancellationToken.Register(() => tcs.TrySetCanceled(), false);
            var waiter = new ReaderWriterAsyncWaiter(registration, tcs, releaser);
            _writersQueue.Enqueue(waiter);
            return new ValueTask<IDisposable>(tcs.Task);
        }
    }

    /// <summary>
    ///     acquire a writer lock
    /// </summary>
    /// <returns>a task that completes when a writer lock is acquired.</returns>
    public ValueTask<IDisposable> WriterLockAsync()
    {
        var releaser = new ReleaserDisposable(this, true);
        lock (_writersQueue)
        {
            // if no one has the lock, we can take it
            if (_status == 0)
            {
                _status = -1;
                return new ValueTask<IDisposable>(releaser);
            }

            var tcs = new TaskCompletionSource<IDisposable>();
            var waiter = new ReaderWriterAsyncWaiter(default, tcs, releaser);
            _writersQueue.Enqueue(waiter);
            return new ValueTask<IDisposable>(tcs.Task);
        }
    }

    /// <summary>
    ///     Release reader lock
    /// </summary>
    private void ReaderRelease()
    {
        lock (_writersQueue)
        {
            --_status;
            if (_status != 0) // there are still readers having the lock
            {
                return;
            }

            IReaderWriterLockWaiter toWake;
            do
            {
                do
                {
                    // Try to wake up a writer first
                    if (_writersQueue.Count > 0)
                    {
                        toWake = _writersQueue.Dequeue();
                    }
                    else if (_readersQueue.Count > 0)
                    {
                        Debug.Assert(false, "There should be no readers in the queue when the status is 0");
                        toWake = _readersQueue.Dequeue();
                    }
                    else
                    {
                        return;
                    }
                } while (!toWake.Awaken());

                if (toWake.IsWriter)
                {
                    _status = -1;
                }
                else
                {
                    _status++;
                }
            } while (!toWake.IsWriter);
        }
    }

    /// <summary>
    ///     release writer lock
    /// </summary>
    private void WriterRelease()
    {
        lock (_writersQueue)
        {
            _status = 0;
            IReaderWriterLockWaiter toWake;
            do
            {
                do
                {
                    // Try to wake up a writer first
                    if (_writersQueue.Count > 0)
                    {
                        toWake = _writersQueue.Dequeue();
                    }
                    else if (_readersQueue.Count > 0)
                    {
                        toWake = _readersQueue.Dequeue();
                    }
                    else
                    {
                        return;
                    }
                } while (!toWake.Awaken());

                if (toWake.IsWriter)
                {
                    _status = -1;
                }
                else
                {
                    _status++;
                }
            } while (!toWake.IsWriter);
        }
    }

    #region Nested type: Releaser

    private interface IReaderWriterLockWaiter : IWaiter
    {
        public bool IsWriter { get; }
    }

    /// <summary>
    ///     a releaser helper that implements IDisposable to support
    ///     using statement
    /// </summary>
    private sealed class ReleaserDisposable : IDisposable, IReaderWriterLockWaiter
    {
        /// <summary>
        ///     underlying lock
        /// </summary>
        private readonly AsyncReaderWriterLock _toRelease;

        /// <summary>
        ///     whether the lock acquired is a writer lock
        /// </summary>
        public bool IsWriter { get; }

        private int _disposed;

        internal ReleaserDisposable(AsyncReaderWriterLock toRelease, bool writer)
        {
            _toRelease = toRelease;
            IsWriter = writer;
        }

        #region IDisposable Members

        /// <summary>
        ///     Dispose. releases the lock
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) return;
            if (IsWriter)
            {
                _toRelease.WriterRelease();
            }
            else
            {
                _toRelease.ReaderRelease();
            }
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

    #region Nested type: TaskSourceAndRegistrationwaiter

    private sealed class ReaderWriterAsyncWaiter(
        CancellationTokenRegistration registration,
        TaskCompletionSource<IDisposable> source,
        ReleaserDisposable releaser) : AsyncWaiter(registration, source, releaser), IReaderWriterLockWaiter
    {
        public bool IsWriter => releaser.IsWriter;
    }

    #endregion
}