// ReSharper disable MethodHasAsyncOverload

namespace CodeArt.ThreadUtils.Tests;

public class AsyncLockTests
{
    [Fact(Timeout = 10)]
    public async Task AsyncLock_ShouldAllowSingleLockerAsync()
    {
        var lck = new AsyncLock();
        using var l1 = await lck.LockAsync();
    }

    [Fact(Timeout = 10)]
    public void AsyncLock_ShouldAllowSingleLocker()
    {
        var lck = new AsyncLock();
        using var l1 = lck.Lock();
    }

    [Fact(Timeout = 10)]
    public void AsyncLock_ShouldAllowLockAfterRelease()
    {
        var lck = new AsyncLock();
        var l1 = lck.Lock();
        l1.Dispose();
        var l2 = lck.Lock();
        l2.Dispose();
    }

    [Fact(Timeout = 10)]
    public async Task AsyncLock_ShouldAllowLockAfterReleaseAsync()
    {
        var lck = new AsyncLock();
        var l1 = await lck.LockAsync();
        var l2T = lck.LockAsync();
        l1.Dispose();
        var l2 = await l2T;
        l2.Dispose();
    }

    [Fact(Timeout = 10)]
    public async Task AsyncLock_ShouldAllowLockAfterReleaseAsyncFirst()
    {
        var lck = new AsyncLock();
        var l1 = await lck.LockAsync();
        var l2T = Task.Run(() => lck.Lock());
        l1.Dispose();
        var l2 = await l2T;
        l2.Dispose();
    }

    [Fact(Timeout = 10)]
    public async Task AsyncLock_ShouldAllowLockAfterReleaseSyncFirst()
    {
        var lck = new AsyncLock();
        var l1 = lck.Lock();
        var l2T = lck.LockAsync();
        l1.Dispose();
        var l2 = await l2T;
        l2.Dispose();
    }

    [Fact(Timeout = 1500)]
    public async Task AsyncLock_ShouldPreventSecondLocker()
    {
        var lck = new AsyncLock();
        using var l1 = lck.Lock();
        await AssertHelper.TimesOutAsync(() => lck.Lock());
    }

    [Fact(Timeout = 1500)]
    public async Task AsyncLock_ShouldPreventSecondLockerAsync()
    {
        var lck = new AsyncLock();
        using var l1 = await lck.LockAsync();
        await AssertHelper.TimesOutAsync(lck.LockAsync());
    }

    [Fact(Timeout = 1500)]
    public async Task AsyncLock_ShouldPreventSecondLockerAsyncFirst()
    {
        var lck = new AsyncLock();
        using var l1 = await lck.LockAsync();
        await AssertHelper.TimesOutAsync(() => lck.Lock());
    }

    [Fact(Timeout = 1500)]
    public async Task AsyncLock_ShouldPreventSecondLockerSyncFirst()
    {
        var lck = new AsyncLock();
        using var l1 = lck.Lock();
        await AssertHelper.TimesOutAsync(lck.LockAsync());
    }

    [Fact(Timeout = 10)]
    public async Task AsyncLock_ShouldWouldCancelLockAndAllowOtherLocks()
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

    [Fact(Timeout = 1500)]
    public async Task AsyncLock_ShouldPreventMultipleDisposeCalls()
    {
        var lck = new AsyncLock();

        var l1 = lck.LockAsync();
        var l2 = lck.LockAsync();
        var l3 = lck.LockAsync();

        var d1 = await l1;
        d1.Dispose();
        await l2;
        d1.Dispose();
        await AssertHelper.TimesOutAsync(l3);
    }

    [Fact(Timeout = 10)]
    public async Task AsyncLock_ShouldThreeLocks()
    {
        var lck = new AsyncLock();

        var l1 = lck.LockAsync();
        var l2 = lck.LockAsync();
        var l3 = lck.LockAsync();

        var d1 = await l1;
        d1.Dispose();
        var d2 = await l2;
        d2.Dispose();
        var d3 = await l3;
        d3.Dispose();
    }
    
    [Fact(Timeout = 10)]
    public async Task AsyncLock_ShouldAllowLockWithCancellationToken()
    {
        var lck = new AsyncLock();
        using var cts = new CancellationTokenSource();
        var l1 = await lck.LockAsync(cts.Token);
        var l2T = lck.LockAsync(cts.Token);
        
        
        l1.Dispose();

        var l2 = await l2T;
        l2.Dispose();
    }
}