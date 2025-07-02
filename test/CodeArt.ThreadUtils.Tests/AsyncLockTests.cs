// ReSharper disable MethodHasAsyncOverload

namespace CodeArt.ThreadUtils.Tests;

public class AsyncLockTests
{
    [Fact(Timeout = Timeouts.ShortTestTimeout)]
    public async Task AsyncLock_ShouldAllowSingleLockerAsync()
    {
        var lck = new AsyncLock();
        using var l1 = await lck.LockAsync();
    }

    [Fact(Timeout = Timeouts.ShortTestTimeout)]
    public Task AsyncLock_ShouldAllowSingleLocker()
    {
        var lck = new AsyncLock();
        using var l1 = lck.Lock();
        return Task.CompletedTask;
    }

    [Fact(Timeout = Timeouts.ShortTestTimeout)]
    public Task AsyncLock_ShouldAllowLockAfterRelease()
    {
        var lck = new AsyncLock();
        var l1 = lck.Lock();
        l1.Dispose();
        var l2 = lck.Lock();
        l2.Dispose();
        return Task.CompletedTask;
    }

    [Fact(Timeout = Timeouts.ShortTestTimeout)]
    public async Task AsyncLock_ShouldAllowLockAfterReleaseAsync()
    {
        var lck = new AsyncLock();
        var l1 = await lck.LockAsync();
        var l2T = lck.LockAsync();
        l1.Dispose();
        var l2 = await l2T;
        l2.Dispose();
    }

    [Fact(Timeout = 40)]
    public async Task AsyncLock_ShouldAllowLockAfterReleaseAsyncFirst()
    {
        var lck = new AsyncLock();
        var l1 = await lck.LockAsync();
        var l2T = await StartSync(() => lck.Lock());
        l1.Dispose();
        var l2 = await l2T;
        l2.Dispose();
    }

    [Fact(Timeout = Timeouts.ShortTestTimeout)]
    public async Task AsyncLock_ShouldAllowLockAfterReleaseSyncFirst()
    {
        var lck = new AsyncLock();
        var l1 = lck.Lock();
        var l2T = lck.LockAsync();
        l1.Dispose();
        var l2 = await l2T;
        l2.Dispose();
    }

    [Fact(Timeout = Timeouts.LongTestTimeout)]
    public async Task AsyncLock_ShouldPreventSecondLocker()
    {
        var lck = new AsyncLock();
        using var l1 = lck.Lock();
        await AssertHelper.TimesOutAsync(() => lck.Lock());
    }

    [Fact(Timeout = Timeouts.LongTestTimeout)]
    public async Task AsyncLock_ShouldPreventSecondLockerAsync()
    {
        var lck = new AsyncLock();
        using var l1 = await lck.LockAsync();
        await AssertHelper.TimesOutAsync(lck.LockAsync());
    }

    [Fact(Timeout = Timeouts.LongTestTimeout)]
    public async Task AsyncLock_ShouldPreventSecondLockerAsyncFirst()
    {
        var lck = new AsyncLock();
        using var l1 = await lck.LockAsync();
        await AssertHelper.TimesOutAsync(() => lck.Lock());
    }

    [Fact(Timeout = Timeouts.LongTestTimeout)]
    public async Task AsyncLock_ShouldPreventSecondLockerSyncFirst()
    {
        var lck = new AsyncLock();
        using var l1 = lck.Lock();
        await AssertHelper.TimesOutAsync(lck.LockAsync());
    }

    [Fact(Timeout = Timeouts.ShortTestTimeout)]
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

    [Fact(Timeout = Timeouts.LongTestTimeout)]
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

    [Fact(Timeout = Timeouts.ShortTestTimeout)]
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
    
    [Fact(Timeout = Timeouts.ShortTestTimeout)]
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
    
    [Fact(Timeout = Timeouts.LongTestTimeout)]
    public async Task AsyncLock_LockShouldTimeoutWhenTimeoutDurationElapses()
    {
        var lck = new AsyncLock();
        var l1 = await lck.LockAsync();
        await Assert.ThrowsAsync<TimeoutException>(() =>
        {
            using (lck.Lock(TimeSpan.FromMilliseconds(Timeouts.LongTimeout)))
            {
                
            }
            return Task.CompletedTask;
        });
        l1.Dispose();
    }
    
    [Fact(Timeout = Timeouts.LongTestTimeout)]
    public async Task AsyncLock_LockShouldReleaseLockInTime()
    {
        var lck = new AsyncLock();
        var l1 = await lck.LockAsync();
        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            l1.Dispose();
        });
        using (lck.Lock(TimeSpan.FromMilliseconds(Timeouts.LongTimeout)))
        {
            
        }
    }
}