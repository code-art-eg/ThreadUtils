// ReSharper disable MethodHasAsyncOverload

namespace CodeArt.ThreadUtils.Tests;

public static class AsyncReaderWriterLockTests
{
    public class WhenCanceledAsync
    {
        [Fact(Timeout = Timeouts.MediumTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldCancelReadLockAndAllowFurtherLocks()
        {
            var rwl = new AsyncReaderWriterLock();
            {
                using var w1 = await rwl.WriterLockAsync();
                await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                {
                    var cts = new CancellationTokenSource();
                    var t = rwl.ReaderLockAsync(cts.Token);
                    cts.Cancel();
                    await t;
                });
            }
            using var w2 = await rwl.WriterLockAsync();
        }

        [Fact(Timeout = Timeouts.MediumTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldCancelWriteLockAndAllowFurtherLocks()
        {
            var rwl = new AsyncReaderWriterLock();
            {
                using var w1 = await rwl.WriterLockAsync();
                await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                {
                    var cts = new CancellationTokenSource();
                    var t = rwl.WriterLockAsync(cts.Token);
                    cts.Cancel();
                    await t;
                });
            }
            using var w2 = await rwl.WriterLockAsync();
        }
    }

    public class WhenUnlockedAsync
    {
        [Fact(Timeout = Timeouts.MediumTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldAllowsSingleWriter()
        {
            var rwl = new AsyncReaderWriterLock();
            using var _ = await rwl.WriterLockAsync();
        }

        [Fact(Timeout = Timeouts.MediumTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldAllowsMultipleReaders()
        {
            var rwl = new AsyncReaderWriterLock();
            using var r1 = await rwl.ReaderLockAsync();
            using var r2 = await rwl.ReaderLockAsync();
        }
    }

    public class WhenUnlockedMix
    {
        [Fact(Timeout = Timeouts.MediumTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldAllowsMultipleReadersSyncFirst()
        {
            var rwl = new AsyncReaderWriterLock();
            using var r1 = rwl.ReaderLock();
            using var r2 = await rwl.ReaderLockAsync();
        }

        [Fact(Timeout = Timeouts.MediumTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldAllowsMultipleReadersAsyncFirst()
        {
            var rwl = new AsyncReaderWriterLock();
            using var r1 = await rwl.ReaderLockAsync();
            using var r2 = rwl.ReaderLock();
        }
    }

    public class WhenUnlockedSync
    {
        [Fact(Timeout = Timeouts.MediumTestTimeout)]
        public Task AsyncReaderWriterLock_ShouldAllowsSingleWriter()
        {
            var rwl = new AsyncReaderWriterLock();
            using var w1 = rwl.WriterLock();
            return Task.CompletedTask;
        }

        [Fact(Timeout = Timeouts.MediumTestTimeout)]
        public Task AsyncReaderWriterLock_ShouldAllowsMultipleReaders()
        {
            var rwl = new AsyncReaderWriterLock();
            using var r1 = rwl.ReaderLock();
            using var r2 = rwl.ReaderLock();
            return Task.CompletedTask;
        }
    }

    public class WhenWriterLockedAsync
    {
        [Fact(Timeout = Timeouts.LongTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldPreventsReaders()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = await rwl.WriterLockAsync();
            var r2T = rwl.ReaderLockAsync().AsTask();
            await AssertHelper.TimesOutAsync(r2T);
            w1.Dispose();
            var r2 = await r2T;
            r2.Dispose();
        }

        [Fact(Timeout = Timeouts.LongTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldPreventsWriters()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = await rwl.WriterLockAsync();
            var w2T = rwl.WriterLockAsync().AsTask();
            await AssertHelper.TimesOutAsync(w2T);
            w1.Dispose();
            var w2 = await w2T;
            w2.Dispose();
        }

        [Fact(Timeout = Timeouts.LongTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldAwakeAllReadersAtOnce()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = await rwl.WriterLockAsync();
            long d1 = -1;
            long d2 = -1;
            var r1T = await StartAsync(async () =>
            {
                using var r1 = await rwl.ReaderLockAsync();
                d1 = Stopwatch.GetTimestamp();
                await Task.Delay(1000);
            });
            var r2T = Task.Run(async () =>
            {
                using var r2 = await rwl.ReaderLockAsync();
                d2 = Stopwatch.GetTimestamp();
                await Task.Delay(1000);
            });
            w1.Dispose();
            await r1T;
            await r2T;

            Assert.NotEqual(-1, d1);
            Assert.NotEqual(-1, d2);

            Assert.True(Math.Abs((double)d1 / Stopwatch.Frequency - (double)d2 / Stopwatch.Frequency) < 0.1);
        }
    }

    public class WhenWriterLockedMix
    {
        [Fact(Timeout = Timeouts.LongTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldPreventsReadersSyncFirst()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = rwl.WriterLock();
            var r2T = rwl.ReaderLockAsync().AsTask();
            await AssertHelper.TimesOutAsync(r2T);
            w1.Dispose();
            var r2 = await r2T;
            r2.Dispose();
        }

        [Fact(Timeout = Timeouts.LongTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldPreventsReadersAsyncFirst()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = await rwl.WriterLockAsync();
            var r2Tcs = new TaskCompletionSource();
            await AssertHelper.TimesOutAsync(() =>
            {
                using var r2 = rwl.ReaderLock();
                r2Tcs.SetResult();
            });
            w1.Dispose();
            await r2Tcs.Task;
        }

        [Fact(Timeout = Timeouts.LongTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldPreventsWritersSyncFirst()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = rwl.WriterLock();
            var w2T = rwl.WriterLockAsync().AsTask();
            await AssertHelper.TimesOutAsync(w2T);
            w1.Dispose();
            var w2 = await w2T;
            w2.Dispose();
        }

        [Fact(Timeout = Timeouts.LongTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldPreventsWritersAsyncFirst()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = await rwl.WriterLockAsync();
            var w2Tcs = new TaskCompletionSource();
            await AssertHelper.TimesOutAsync(() =>
            {
                using var w2 = rwl.WriterLock();
                w2Tcs.SetResult();
            });
            w1.Dispose();
            await w2Tcs.Task;
        }

        [Fact(Timeout = Timeouts.LongTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldAwakeAllReadersAtOnceSyncWriter()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = rwl.WriterLock();
            long d1 = -1;
            long d2 = -1;
            var r1T = await StartAsync(async () =>
            {
                using var r1 = await rwl.ReaderLockAsync();
                d1 = Stopwatch.GetTimestamp();
                await Task.Delay(1000);
            });
            var r2T = await StartSync(() =>
            {
                using var r2 = rwl.ReaderLock();
                d2 = Stopwatch.GetTimestamp();
                Thread.Sleep(1000);
            });
            w1.Dispose();
            await r1T;
            await r2T;

            Assert.NotEqual(-1, d1);
            Assert.NotEqual(-1, d2);

            Assert.True(Math.Abs((double)d1 / Stopwatch.Frequency - (double)d2 / Stopwatch.Frequency) < 0.1);
        }

        [Fact(Timeout = Timeouts.LongTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldAwakeAllReadersAtOnceAsyncWriter()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = await rwl.WriterLockAsync();
            long d1 = -1;
            long d2 = -1;
            var r1T = await StartAsync(async () =>
            {
                using var r1 = await rwl.ReaderLockAsync();
                d1 = Stopwatch.GetTimestamp();
                await Task.Delay(1000);
            });
            var r2T = await StartSync(() =>
            {
                using var r2 = rwl.ReaderLock();
                d2 = Stopwatch.GetTimestamp();
                Thread.Sleep(1000);
            });
            w1.Dispose();
            await r1T;
            await r2T;

            Assert.NotEqual(-1, d1);
            Assert.NotEqual(-1, d2);

            Assert.True(Math.Abs((double)d1 / Stopwatch.Frequency - (double)d2 / Stopwatch.Frequency) < 0.1);
        }

        [Fact(Timeout = Timeouts.MediumTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldNotStarveSyncWriters()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = await rwl.WriterLockAsync();

            var w2T = await StartSync(rwl.WriterLock);
            await Task.Delay(2);
            var w3T = rwl.WriterLockAsync();
            w1.Dispose();
            var w2 = await w2T;
            w2.Dispose();
            var w3 = await w3T;
            w3.Dispose();
        }


        [Fact(Timeout = Timeouts.MediumTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldNotStarveAsyncWriters()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = rwl.WriterLock();

            var w2T = rwl.WriterLockAsync();
            await Task.Delay(2);
            var w3T = await StartSync(rwl.WriterLock);
            await Task.Delay(2);
            w1.Dispose();
            var w2 = await w2T;
            w2.Dispose();

            var w3 = await w3T;
            w3.Dispose();
        }
    }


    public class WhenWriterLockedSync
    {
        [Fact(Timeout = Timeouts.LongTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldPreventsReaders()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = rwl.WriterLock();
            var r1Tcs = new TaskCompletionSource();
            await AssertHelper.TimesOutAsync(() =>
            {
                using var r1 = rwl.ReaderLock();
                r1Tcs.SetResult();
            });
            w1.Dispose();
            await r1Tcs.Task;
        }

        [Fact(Timeout = Timeouts.LongTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldPreventsWriters()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = rwl.WriterLock();
            var w2Tcs = new TaskCompletionSource();
            await AssertHelper.TimesOutAsync(() =>
            {
                using var w2 = rwl.WriterLock();
                w2Tcs.SetResult();
            });
            w1.Dispose();
            await w2Tcs.Task;
        }

        [Fact(Timeout = Timeouts.LongTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldAwakeAllReadersAtOnce()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = rwl.WriterLock();
            long d1 = -1;
            long d2 = -1;
            var r1T = await StartSync(() =>
            {
                using var r1 = rwl.ReaderLock();
                d1 = Stopwatch.GetTimestamp();
                Thread.Sleep(1000);
            });
            var r2T = await StartSync(() =>
            {
                using var r2 = rwl.ReaderLock();
                d2 = Stopwatch.GetTimestamp();
                Thread.Sleep(1000);
            });
            w1.Dispose();
            await r1T;
            await r2T;

            Assert.NotEqual(-1, d1);
            Assert.NotEqual(-1, d2);

            Assert.True(Math.Abs((double)d1 / Stopwatch.Frequency - (double)d2 / Stopwatch.Frequency) < 0.1);
        }
    }

    public class WhenReaderLockedAsync
    {
        [Fact(Timeout = Timeouts.LongTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldPreventWriters()
        {
            var rwl = new AsyncReaderWriterLock();
            var r1 = await rwl.ReaderLockAsync();
            var w1T = rwl.WriterLockAsync().AsTask();
            await AssertHelper.TimesOutAsync(w1T);
            r1.Dispose();
            var w1 = await w1T;
            w1.Dispose();
        }

        [Fact(Timeout = Timeouts.ShortTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldNotStarveWriters()
        {
            var rwl = new AsyncReaderWriterLock();
            var r1 = await rwl.ReaderLockAsync();
            var r2 = await rwl.ReaderLockAsync();
            var w1T = rwl.WriterLockAsync();
            r1.Dispose();
            var r3T = rwl.ReaderLockAsync();
            var r4T = rwl.ReaderLockAsync();
            r2.Dispose();
            var w1 = await w1T;
            w1.Dispose();
            var r3 = await r3T;
            var r4 = await r4T;
            r3.Dispose();
            r4.Dispose();
        }
    }

    public class WhenReaderLockedSync
    {
        [Fact(Timeout = Timeouts.LongTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldPreventWriters()
        {
            var rwl = new AsyncReaderWriterLock();
            var r1 = rwl.ReaderLock();
            var w1Tcs = new TaskCompletionSource();
            await AssertHelper.TimesOutAsync(() =>
            {
                using var w1 = rwl.WriterLock();
                w1Tcs.SetResult();
            });
            r1.Dispose();
            await w1Tcs.Task;
        }

        [Fact(Timeout = Timeouts.MediumTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldNotStarveWriters()
        {
            var rwl = new AsyncReaderWriterLock();
            var r1 = rwl.ReaderLock();
            var r2 = rwl.ReaderLock();

            var w1T = await StartSync(() =>
            {
                using var w1 = rwl.WriterLock();
            });
            r1.Dispose();
            var r3T = await StartSync(() =>
            {
                using var r3 = rwl.ReaderLock();
            });
            var r4T = await StartSync(() =>
            {
                using var r3 = rwl.ReaderLock();
            });
            await Task.Yield();
            r2.Dispose();
            await w1T;
            await r3T;
            await r4T;
        }
    }

    public class WhenReaderLockedMix
    {
        [Fact(Timeout = Timeouts.LongTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldPreventWritersAsyncWriter()
        {
            var rwl = new AsyncReaderWriterLock();
            var r1 = rwl.ReaderLock();
            var w1Tcs = new TaskCompletionSource();
            await AssertHelper.TimesOutAsync(rwl.WriterLockAsync().AsTask().ContinueWith(t =>
            {
                t.Result.Dispose();
                w1Tcs.SetResult();
            }));
            r1.Dispose();
            await w1Tcs.Task;
        }

        [Fact(Timeout = Timeouts.LongTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldPreventWritersSyncWriter()
        {
            var rwl = new AsyncReaderWriterLock();
            var r1 = await rwl.ReaderLockAsync();
            var w1Tcs = new TaskCompletionSource();
            await AssertHelper.TimesOutAsync(() =>
            {
                using var w1 = rwl.WriterLock();
                w1Tcs.SetResult();
            });
            r1.Dispose();
            await w1Tcs.Task;
        }

        [Fact(Timeout = Timeouts.MediumTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldWritersAreNotStarvedSyncWriter()
        {
            var rwl = new AsyncReaderWriterLock();
            var r1 = rwl.ReaderLock();
            var r2 = await rwl.ReaderLockAsync();

            var w1T = await StartSync(() =>
            {
                using var w1 = rwl.WriterLock();
            });
            r1.Dispose();
            var r3T = await StartSync(async () =>
            {
                using var r3 = await rwl.ReaderLockAsync();
            });
            var r4T = await StartSync(() =>
            {
                using var r3 = rwl.ReaderLock();
            });
            await Task.Yield();
            r2.Dispose();
            await w1T;
            await r3T;
            await r4T;
        }

        [Fact(Timeout = Timeouts.MediumTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldWritersAreNotStarvedAsyncWriter()
        {
            var rwl = new AsyncReaderWriterLock();
            var r1 = rwl.ReaderLock();
            var r2 = await rwl.ReaderLockAsync();

            var w1T = await StartAsync(async () =>
            {
                using var w1 = await rwl.WriterLockAsync();
            });
            r1.Dispose();
            var r3T = await StartAsync(async () =>
            {
                using var r3 = await rwl.ReaderLockAsync();
            });
            var r4T = await StartSync(() =>
            {
                using var r3 = rwl.ReaderLock();
            });
            await Task.Yield();
            r2.Dispose();
            await w1T;
            await r3T;
            await r4T;
        }

        [Fact(Timeout = Timeouts.MediumTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldNotStarveSyncWriters()
        {
            var rwl = new AsyncReaderWriterLock();
            var r1 = await rwl.ReaderLockAsync();

            var w1T = await StartSync(rwl.WriterLock);
            await Task.Delay(2);
            var w2T = rwl.WriterLockAsync();
            r1.Dispose();
            var w1 = await w1T;
            w1.Dispose();
            var w2 = await w2T;
            w2.Dispose();
        }


        [Fact(Timeout = Timeouts.MediumTestTimeout)]
        public async Task AsyncReaderWriterLock_ShouldNotStarveAsyncWriters()
        {
            var rwl = new AsyncReaderWriterLock();
            var r1 = rwl.ReaderLock();

            var w1T = rwl.WriterLockAsync();
            await Task.Delay(2);
            var w2T = await StartSync(rwl.WriterLock);
            await Task.Delay(2);
            r1.Dispose();
            var w1 = await w1T;
            w1.Dispose();

            var w2 = await w2T;
            w2.Dispose();
        }
    }
}