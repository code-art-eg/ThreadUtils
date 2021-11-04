using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CodeArt.ThreadUtils
{
    /// <summary>
    ///     Async reader writer lock
    ///     Based on: 
    ///     https://devblogs.microsoft.com/pfxteam/building-async-coordination-primitives-part-7-asyncreaderwriterlock/
    /// </summary>
    public sealed class AsyncReaderWriterLock
    {
        /// <summary>
        ///     reader lock releaser
        /// </summary>
        private readonly IDisposable _readerReleaser;

        /// <summary>
        ///     reader lock releaser task
        /// </summary>
        private readonly Task<IDisposable> _readerReleaserTask;

        /// <summary>
        ///     queue of waiting writers
        /// </summary>
        private readonly Queue<object> _waitingWriters = new();

        /// <summary>
        ///     writer lock releaser
        /// </summary>
        private readonly IDisposable _writerReleaser;

        /// <summary>
        ///     writer lock releaser task
        /// </summary>
        private readonly Task<IDisposable> _writerReleaserTask;

        /// <summary>
        ///     number readers waiting
        /// </summary>
        private int _readersWaiting;

        /// <summary>
        ///     status. O means no one has the lock. -1 a writer has the lock, +ve is the number of readers having the lock.
        /// </summary>
        private int _status;

        /// <summary>
        ///     queue of waiting readers
        /// </summary>
        private TaskCompletionSource<IDisposable> _waitingReader = new();

        /// <summary>
        ///     constructor
        /// </summary>
        public AsyncReaderWriterLock()
        {
            _readerReleaser = new ReleaserDisposable(this, false);
            _readerReleaserTask = Task.FromResult(_readerReleaser);
            _writerReleaser = new ReleaserDisposable(this, true);
            _writerReleaserTask = Task.FromResult(_writerReleaser);
        }

        /// <summary>
        ///     acquire a reader lock
        /// </summary>
        /// <returns>A releaser that releases the lock</returns>
        public IDisposable ReaderLock()
        {
            lock (_waitingWriters)
            {
                while (_status < 0 || _waitingWriters.Count != 0)
                {
                    Monitor.Wait(_waitingWriters);
                }
                ++_status;
            }
            return _readerReleaser;
        }

        /// <summary>
        ///     acquire a reader lock
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel waiting</param>
        /// <returns>a task that completes when a reader lock is acquired.</returns>
        public Task<IDisposable> ReaderLockAsync(CancellationToken cancellationToken)
        {
            lock (_waitingWriters)
            {
                if (_status >= 0
                    && _waitingWriters.Count == 0)
                {
                    ++_status;
                    return _readerReleaserTask;
                }
                ++_readersWaiting;
                var cancelTaskSource = new TaskCompletionSource<bool>();
                var registration = cancellationToken.Register(() =>
                {
                    cancelTaskSource.SetResult(true);
                }, false);
                // ReSharper disable MethodSupportsCancellation
                var resultTask = Task.WhenAny(cancelTaskSource.Task, _waitingReader.Task)
                    .ContinueWith(t =>
                    {
                        registration.Dispose();
                        if (t.Result != cancelTaskSource.Task)
                        {
                            // Reader acquired.
                            return _waitingReader.Task.Result;
                        }

                        // Task was cancelled before the read is acquired.
                        // Dispose the reader as soon as the lock is acquired.
                        _waitingReader.Task.ContinueWith(wt =>
                        {
                            wt.Result.Dispose();
                        });
                        throw new TaskCanceledException();
                    });
                // ReSharper restore MethodSupportsCancellation
                return resultTask;
            }
        }

        /// <summary>
        ///     acquire a reader lock
        /// </summary>
        /// <returns>a task that completes when a reader lock is acquired.</returns>
        public Task<IDisposable> ReaderLockAsync()
        {
            lock (_waitingWriters)
            {
                if (_status >= 0
                    && _waitingWriters.Count == 0)
                {
                    ++_status;
                    return _readerReleaserTask;
                }
                ++_readersWaiting;
                return _waitingReader.Task;
            }
        }

        /// <summary>
        ///     acquire a writer lock
        /// </summary>
        /// <returns>a releaser that releases the lock.</returns>
        public IDisposable WriterLock()
        {
            ReleaserDisposable newReleaser;
            lock (_waitingWriters)
            {
                if (_status == 0)
                {
                    _status = -1;
                    return _writerReleaser;
                }
                newReleaser = new ReleaserDisposable(this, true);
                _waitingWriters.Enqueue(newReleaser);
            }
            lock (newReleaser)
            {
                Monitor.Wait(newReleaser);
            }
            return _writerReleaser;
        }

        /// <summary>
        ///     acquire a writer lock
        /// </summary>
        /// <param name="cancellationToken">cancellation token used to cancel the wait</param>
        /// <returns>a task that completes when a writer lock is acquired.</returns>
        public Task<IDisposable> WriterLockAsync(CancellationToken cancellationToken)
        {
            lock (_waitingWriters)
            {
                if (_status == 0)
                {
                    _status = -1;
                    return _writerReleaserTask;
                }
                var waiter = new TaskCompletionSource<IDisposable>();
                var registration = cancellationToken.Register(() =>
                {
                    waiter.TrySetCanceled();
                }, false);
                var pair = new TaskSourceAndRegistrationPair(registration, waiter);
                _waitingWriters.Enqueue(pair);
                return waiter.Task;
            }
        }

        /// <summary>
        ///     acquire a writer lock
        /// </summary>
        /// <returns>a task that completes when a writer lock is acquired.</returns>
        public Task<IDisposable> WriterLockAsync()
        {
            lock (_waitingWriters)
            {
                if (_status == 0)
                {
                    _status = -1;
                    return _writerReleaserTask;
                }
                var waiter = new TaskCompletionSource<IDisposable>();
                _waitingWriters.Enqueue(waiter);
                return waiter.Task;
            }
        }

        /// <summary>
        ///     Release reader lock
        /// </summary>
        private void ReaderRelease()
        {
            object? toWake = null;

            lock (_waitingWriters)
            {
                --_status;
                if (_status == 0
                    && _waitingWriters.Count > 0)
                {
                    _status = -1;
                    toWake = _waitingWriters.Dequeue();
                }
            }

            switch (toWake)
            {
                case TaskCompletionSource<IDisposable> tcs:
                    tcs.SetResult(_writerReleaser);
                    break;
                case ReleaserDisposable disposable:
                {
                    lock (disposable)
                    {
                        Monitor.Pulse(disposable);
                    }

                    break;
                }
            }
        }

        /// <summary>
        ///     release writer lock
        /// </summary>
        private void WriterRelease()
        {
            while (true)
            {
                object? toWake = null;
                var releaser = _readerReleaser;

                lock (_waitingWriters)
                {
                    if (_waitingWriters.Count > 0)
                    {
                        toWake = _waitingWriters.Dequeue();
                        releaser = _writerReleaser;
                    }
                    else if (_readersWaiting > 0)
                    {
                        toWake = _waitingReader;
                        _status = _readersWaiting;
                        _readersWaiting = 0;
                        _waitingReader = new TaskCompletionSource<IDisposable>();
                        Monitor.PulseAll(_waitingWriters);
                    }
                    else
                    {
                        _status = 0;
                        Monitor.PulseAll(_waitingWriters);
                    }
                }

                switch (toWake)
                {
                    case TaskCompletionSource<IDisposable> tcs:
                        tcs.SetResult(releaser);
                        break;
                    case TaskSourceAndRegistrationPair pair:
                    {
                        pair.Registration.Dispose();
                        if (!pair.Source.TrySetResult(_writerReleaser))
                        {
                            // Task was cancelled when a cancellationToken was cancelled
                            // Try to release another waiter if any
                            // This is tail call optimized in both 32-bit and 64-bit JIT using dotnet 5
                            continue;
                        }

                        break;
                    }
                    case ReleaserDisposable disposable:
                    {
                        lock (disposable)
                        {
                            Monitor.Pulse(disposable);
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
            private readonly AsyncReaderWriterLock _toRelease;

            /// <summary>
            ///     whether the lock acquired is a writer lock
            /// </summary>
            private readonly bool _writer;

            internal ReleaserDisposable(AsyncReaderWriterLock toRelease, bool writer)
            {
                _toRelease = toRelease;
                _writer = writer;
            }

            #region IDisposable Members
            /// <summary>
            ///     Dispose. releases the lock
            /// </summary>
            public void Dispose()
            {
                if (_writer)
                {
                    _toRelease.WriterRelease();
                }
                else
                {
                    _toRelease.ReaderRelease();
                }
            }
            #endregion
        }
        #endregion

        #region Nested type: TaskSourceAndRegistrationPair
        private sealed class TaskSourceAndRegistrationPair
        {
            public TaskSourceAndRegistrationPair(CancellationTokenRegistration registration, TaskCompletionSource<IDisposable> source)
            {
                Registration = registration;
                Source = source;
            }

            public CancellationTokenRegistration Registration { get; }
            public TaskCompletionSource<IDisposable> Source { get; }
        }
        #endregion
    }
}
