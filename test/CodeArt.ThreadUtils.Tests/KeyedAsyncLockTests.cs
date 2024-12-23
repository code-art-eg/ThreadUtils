﻿// ReSharper disable MethodHasAsyncOverload

namespace CodeArt.ThreadUtils.Tests;

public class KeyedAsyncLockTests
{
    [Fact(Timeout = 10)]
    public async Task AllowSingleLockerAsync()
    {
        var lck = new KeyedAsyncLock<string>();
        using var l1 = await lck.LockAsync("x");
    }

    [Fact(Timeout = 10)]
    public void AllowSingleLocker()
    {
        var lck = new KeyedAsyncLock<string>();
        using var l1 = lck.Lock("x");
    }

    [Fact(Timeout = 10)]
    public void AllowLockAfterRelease()
    {
        var lck = new KeyedAsyncLock<string>();
        var l1 = lck.Lock("x");
        l1.Dispose();
        var l2 = lck.Lock("x");
        l2.Dispose();
    }

    [Fact(Timeout = 10)]
    public async Task AllowLockAfterReleaseAsync()
    {
        var lck = new KeyedAsyncLock<string>();
        var l1 = await lck.LockAsync("x");
        var l2T = lck.LockAsync("x");
        l1.Dispose();
        var l2 = await l2T;
        l2.Dispose();
    }

    [Fact(Timeout = 10)]
    public async Task AllowLockAfterReleaseAsyncFirst()
    {
        var lck = new KeyedAsyncLock<string>();
        var l1 = await lck.LockAsync("x");
        var l2T = Task.Run(() => lck.Lock("x"));
        l1.Dispose();
        var l2 = await l2T;
        l2.Dispose();
    }

    [Fact(Timeout = 10)]
    public async Task AllowLockAfterReleaseSyncFirst()
    {
        var lck = new KeyedAsyncLock<string>();
        var l1 = lck.Lock("x");
        var l2T = lck.LockAsync("x");
        l1.Dispose();
        var l2 = await l2T;
        l2.Dispose();
    }

    [Fact(Timeout = 1500)]
    public async Task PreventSecondLocker()
    {
        var lck = new KeyedAsyncLock<string>();
        using var l1 = lck.Lock("x");
        await AssertHelper.TimesOutAsync(() => lck.Lock("x"));
    }

    [Fact(Timeout = 1500)]
    public async Task PreventSecondLockerAsync()
    {
        var lck = new KeyedAsyncLock<string>();
        using var l1 = await lck.LockAsync("x");
        await AssertHelper.TimesOutAsync(lck.LockAsync("x"));
    }

    [Fact(Timeout = 1500)]
    public async Task PreventSecondLockerAsyncFirst()
    {
        var lck = new KeyedAsyncLock<string>();
        using var l1 = await lck.LockAsync("x");
        await AssertHelper.TimesOutAsync(() => lck.Lock("x"));
    }

    [Fact(Timeout = 1500)]
    public async Task PreventSecondLockerSyncFirst()
    {
        var lck = new KeyedAsyncLock<string>();
        using var l1 = lck.Lock("x");
        await AssertHelper.TimesOutAsync(lck.LockAsync("x"));
    }

    [Fact(Timeout = 10)]
    public async Task WouldCancelLockAndAllowOtherLocks()
    {
        var lck = new KeyedAsyncLock<string>();
        {
            using var l1 = lck.Lock("x");
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                var cts = new CancellationTokenSource();
                var t = lck.LockAsync("x", cts.Token);
                cts.Cancel();
                await t;
            });
        }
        using var l2 = lck.Lock("x");
    }

    [Fact(Timeout = 1500)]
    public async Task PreventMultipleDisposeCalls()
    {
        var lck = new KeyedAsyncLock<string>();

        var l1 = lck.LockAsync("x");
        var l2 = lck.LockAsync("x");
        var l3 = lck.LockAsync("x");

        var d1 = await l1;
        d1.Dispose();
        await l2;
        d1.Dispose();
        await AssertHelper.TimesOutAsync(l3);
    }

    [Fact(Timeout = 10)]
    public async Task ThreeLocks()
    {
        var lck = new KeyedAsyncLock<string>();

        var l1 = lck.LockAsync("x");
        var l2 = lck.LockAsync("x");
        var l3 = lck.LockAsync("x");

        var d1 = await l1;
        d1.Dispose();
        var d2 = await l2;
        d2.Dispose();
        var d3 = await l3;
        d3.Dispose();
    }

    [Fact(Timeout = 10)]
    public async Task MultipleKeys()
    {
        var lck = new KeyedAsyncLock<string>();

        using var l1 = await lck.LockAsync("x");
        using var l2 = await lck.LockAsync("y");
    }

    [Fact(Timeout = 10)]
    public async Task MultipleKeysTwice()
    {
        var lck = new KeyedAsyncLock<string>();

        var l1 = await lck.LockAsync("x");
        var l2 = await lck.LockAsync("y");
        l1.Dispose();
        l2.Dispose();

        var l3 = await lck.LockAsync("x");
        var l4 = await lck.LockAsync("y");
        l3.Dispose();
        l4.Dispose();
    }

    [Fact(Timeout = 10)]
    public async Task LockWithCancellationTwice()
    {
        var lck = new KeyedAsyncLock<string>();

        var l1 = await lck.LockAsync("x", CancellationToken.None);
        var l2T = lck.LockAsync("x", CancellationToken.None);
        l1.Dispose();

        var l2 = await l2T;

        l2.Dispose();
    }

    [Fact(Timeout = 10)]
    public async Task LockWithCancellationThriceCancelledTask()
    {
        var lck = new KeyedAsyncLock<string>();
        var cts = new CancellationTokenSource();
        var l1 = await lck.LockAsync("x", CancellationToken.None);
        var l2T = lck.LockAsync("x", cts.Token);
        var l3T = lck.LockAsync("x", CancellationToken.None);

        cts.Cancel();

        l1.Dispose();

        var cancelled = false;
        try
        {
            var l2 = await l2T;
            l2.Dispose();
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        Assert.True(cancelled);

        var l3 = await l3T;
        l3.Dispose();
    }
}