using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CodeArt.ThreadUtils
{
    /// <summary>
    ///  Async lock that interacts with using statement
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
        /// When disposed the releaser would release the lock
        /// </summary>
        private readonly IDisposable _releaser;

        /// <summary>
        /// Releaser task
        /// </summary>
        private readonly Task<IDisposable> _releaserTask;

        /// <summary>
        /// Constructor. Creates a new instance of <see cref="AsyncLock"/> class
        /// </summary>
        public AsyncLock()
        {
            _releaser = new ReleaserDisposable(this);
            _releaserTask = Task.FromResult(_releaser);
        }

        /// <summary>
        /// Acquire an exclusive lock
        /// </summary>
        /// <returns>returns a task that completes when the lock is acquired</returns>
        public Task<IDisposable> LockAsync()
        {
            lock(_waiters)
            {
                if (!_lockTaken)
                {
                    _lockTaken = true;
                    return _releaserTask;
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
                    return _releaser;
                }
                newReleaser = new ReleaserDisposable(this);
                _waiters.Enqueue(newReleaser);
            }
            lock(newReleaser)
            {
                Monitor.Wait(newReleaser);
            }
            return _releaser;
        }

        /// <summary>
        /// Release lock
        /// </summary>
        private void Release()
        {
            object? toWake = null;
            lock(_waiters)
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
            if (toWake is TaskCompletionSource<IDisposable> tcs)
            {
                tcs.SetResult(_releaser);
            }
            else if (toWake is ReleaserDisposable releaser)
            {
                lock(releaser)
                {
                    Monitor.Pulse(releaser);
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
            /// <summary>
            ///     underlying lock
            /// </summary>
            private readonly AsyncLock _toRelease;

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
                _toRelease.Release();
            }
            #endregion
        }

        #endregion
    }
}
