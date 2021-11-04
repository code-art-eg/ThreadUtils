using System.Threading;
using System.Threading.Tasks;
using Xunit;
// ReSharper disable MethodHasAsyncOverload

namespace CodeArt.ThreadUtils.Tests
{
    public class AsyncLockTests
    {
        [Fact(Timeout = 10)]
        public async Task AllowSingleLockerAsync()
        {
            var lck = new AsyncLock();
            using var l1 = await lck.LockAsync();
        }

        [Fact(Timeout = 10)]
        public void AllowSingleLocker()
        {
            var lck = new AsyncLock();
            using var l1 = lck.Lock();
        }

        [Fact(Timeout = 10)]
        public void AllowLockAfterRelease()
        {
            var lck = new AsyncLock();
            var l1 = lck.Lock();
            l1.Dispose();
            var l2 = lck.Lock();
            l2.Dispose();
        }

        [Fact(Timeout = 10)]
        public async Task AllowLockAfterReleaseAsync()
        {
            var lck = new AsyncLock();
            var l1 = await lck.LockAsync();
            var l2T = lck.LockAsync();
            l1.Dispose();
            var l2 = await l2T;
            l2.Dispose();
        }

        [Fact(Timeout = 10)]
        public async Task AllowLockAfterReleaseAsyncFirst()
        {
            var lck = new AsyncLock();
            var l1 = await lck.LockAsync();
            var l2T = Task.Run(() => lck.Lock());
            l1.Dispose();
            var l2 = await l2T;
            l2.Dispose();
        }

        [Fact(Timeout = 10)]
        public async Task AllowLockAfterReleaseSyncFirst()
        {
            var lck = new AsyncLock();
            var l1 = lck.Lock();
            var l2T = lck.LockAsync();
            l1.Dispose();
            var l2 = await l2T;
            l2.Dispose();
        }

        [Fact(Timeout = 1500)]
        public async Task PreventSecondLocker()
        {
            var lck = new AsyncLock();
            using var l1 = lck.Lock();
            await AssertHelper.TimesOutAsync(() => lck.Lock());
        }

        [Fact(Timeout = 1500)]
        public async Task PreventSecondLockerAsync()
        {
            var lck = new AsyncLock();
            using var l1 = await lck.LockAsync();
            await AssertHelper.TimesOutAsync(lck.LockAsync());
        }

        [Fact(Timeout = 1500)]
        public async Task PreventSecondLockerAsyncFirst()
        {
            var lck = new AsyncLock();
            using var l1 = await lck.LockAsync();
            await AssertHelper.TimesOutAsync(() => lck.Lock());
        }

        [Fact(Timeout = 1500)]
        public async Task PreventSecondLockerSyncFirst()
        {
            var lck = new AsyncLock();
            using var l1 = lck.Lock();
            await AssertHelper.TimesOutAsync(lck.LockAsync());
        }

        [Fact(Timeout = 10)]
        public async Task WouldCancelLockAndAllowOtherLocks()
        {
            var lck = new AsyncLock();
            {
                using var l1 = lck.Lock();
                await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                {
                    var cts = new CancellationTokenSource();
                    var t = lck.LockAsync(cts.Token);
                    cts.Cancel();
                    await t;
                });
            }
            using var l2 = lck.Lock();
        }
    }
}
