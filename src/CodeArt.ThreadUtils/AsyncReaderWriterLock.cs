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
    public class AsyncReaderWriterLock
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
        private readonly Queue<TaskCompletionSource<IDisposable>> _waitingWriters = new();

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
        /// Number of writers waiting using WriterLock (synchronous)
        /// </summary>
        private int _syncWritersWaiting;

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
                while(_status < 0 || _waitingWriters.Count != 0 || _syncWritersWaiting != 0)
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
        /// <returns>a task that completes when a reader lock is acquired.</returns>
        public Task<IDisposable> ReaderLockAsync()
        {
            lock (_waitingWriters)
            {
                if (_status >= 0
                    && _waitingWriters.Count == 0 && _syncWritersWaiting == 0)
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
            lock (_waitingWriters)
            {
                ++_syncWritersWaiting;
                while (_status != 0)
                {
                    Monitor.Wait(_waitingWriters);
                }
                --_syncWritersWaiting;
                _status = -1;
            }
            return _writerReleaser;
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
            TaskCompletionSource<IDisposable>? toWake = null;

            lock (_waitingWriters)
            {
                --_status;
                if (_status == 0
                    && _waitingWriters.Count > 0)
                {
                    _status = -1;
                    toWake = _waitingWriters.Dequeue();
                }
                else if (_syncWritersWaiting > 0)
                {
                    Monitor.PulseAll(_waitingWriters);
                }
            }

            if (toWake != null)
            {
                toWake.SetResult(new ReleaserDisposable(this, true));
            }
        }

        /// <summary>
        ///     release writer lock
        /// </summary>
        private void WriterRelease()
        {
            TaskCompletionSource<IDisposable>? toWake = null;
            var releaser = _readerReleaser;

            lock (_waitingWriters)
            {
                if (_waitingWriters.Count > 0)
                {
                    toWake = _waitingWriters.Dequeue();
                    releaser = _writerReleaser;
                }
                else if (_syncWritersWaiting > 0)
                {
                    _status = 0;
                    Monitor.PulseAll(_waitingWriters);
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

            if (toWake != null)
            {
                toWake.SetResult(releaser);
            }
        }

        #region Nested type: Releaser
        /// <summary>
        ///     a releaser helper that implements IDisposable to support
        ///     using statement
        /// </summary>
        private class ReleaserDisposable : IDisposable
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
                if (_toRelease != null)
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
            }
            #endregion
        }
        #endregion
    }
}
