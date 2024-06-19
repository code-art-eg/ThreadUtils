// ReSharper disable MethodHasAsyncOverload

namespace CodeArt.ThreadUtils.Tests;

public static class AsyncReaderWriterLockTests
{
    public class WhenCanceledAsync
    {
        [Fact(Timeout = 10)]
        public async Task ShouldCancelReadLockAndAllowFurtherLocks()
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

        [Fact(Timeout = 10)]
        public async Task ShouldCancelWriteLockAndAllowFurtherLocks()
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
        [Fact(Timeout = 10)]
        public async Task AllowsSingleWriter()
        {
            var rwl = new AsyncReaderWriterLock();
            using var _ = await rwl.WriterLockAsync();
        }

        [Fact(Timeout = 10)]
        public async Task AllowsMultipleReaders()
        {
            var rwl = new AsyncReaderWriterLock();
            using var r1 = await rwl.ReaderLockAsync();
            using var r2 = await rwl.ReaderLockAsync();
        }
    }

    public class WhenUnlockedMix
    {
        [Fact(Timeout = 10)]
        public async Task AllowsMultipleReadersSyncFirst()
        {
            var rwl = new AsyncReaderWriterLock();
            using var r1 = rwl.ReaderLock();
            using var r2 = await rwl.ReaderLockAsync();
        }

        [Fact(Timeout = 10)]
        public async Task AllowsMultipleReadersAsyncFirst()
        {
            var rwl = new AsyncReaderWriterLock();
            using var r1 = await rwl.ReaderLockAsync();
            using var r2 = rwl.ReaderLock();
        }
    }

    public class WhenUnlockedSync
    {
        [Fact(Timeout = 10)]
        public void AllowsSingleWriter()
        {
            var rwl = new AsyncReaderWriterLock();
            using var w1 = rwl.WriterLock();
        }

        [Fact(Timeout = 10)]
        public void AllowsMultipleReaders()
        {
            var rwl = new AsyncReaderWriterLock();
            using var r1 = rwl.ReaderLock();
            using var r2 = rwl.ReaderLock();
        }
    }

    public class WhenWriterLockedAsync
    {
        [Fact(Timeout = 1500)]
        public async Task PreventsReaders()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = await rwl.WriterLockAsync();
            var r2T = rwl.ReaderLockAsync();
            await AssertHelper.TimesOutAsync(r2T);
            w1.Dispose();
            var r2 = await r2T;
            r2.Dispose();
        }

        [Fact(Timeout = 1500)]
        public async Task PreventsWriters()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = await rwl.WriterLockAsync();
            var w2T = rwl.WriterLockAsync();
            await AssertHelper.TimesOutAsync(w2T);
            w1.Dispose();
            var w2 = await w2T;
            w2.Dispose();
        }

        [Fact(Timeout = 1500)]
        public async Task AwakeAllReadersAtOnce()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = await rwl.WriterLockAsync();
            long d1 = -1;
            long d2 = -1;
            var r1T = Task.Run(async () =>
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

            Assert.Equal((double)d1 / Stopwatch.Frequency, (double)d2 / Stopwatch.Frequency, 2);
        }
    }

    public class WhenWriterLockedMix
    {
        [Fact(Timeout = 1500)]
        public async Task PreventsReadersSyncFirst()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = rwl.WriterLock();
            var r2T = rwl.ReaderLockAsync();
            await AssertHelper.TimesOutAsync(r2T);
            w1.Dispose();
            var r2 = await r2T;
            r2.Dispose();
        }

        [Fact(Timeout = 1500)]
        public async Task PreventsReadersAsyncFirst()
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

        [Fact(Timeout = 1500)]
        public async Task PreventsWritersSyncFirst()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = rwl.WriterLock();
            var w2T = rwl.WriterLockAsync();
            await AssertHelper.TimesOutAsync(w2T);
            w1.Dispose();
            var w2 = await w2T;
            w2.Dispose();
        }

        [Fact(Timeout = 1500)]
        public async Task PreventsWritersAsyncFirst()
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

        [Fact(Timeout = 1500)]
        public async Task AwakeAllReadersAtOnceSyncWriter()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = rwl.WriterLock();
            long d1 = -1;
            long d2 = -1;
            var r1T = Task.Run(async () =>
            {
                using var r1 = await rwl.ReaderLockAsync();
                d1 = Stopwatch.GetTimestamp();
                await Task.Delay(1000);
            });
            var r2T = Task.Run(() =>
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

            Assert.Equal((double)d1 / Stopwatch.Frequency, (double)d2 / Stopwatch.Frequency, 1);
        }

        [Fact(Timeout = 1500)]
        public async Task AwakeAllReadersAtOnceAsyncWriter()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = await rwl.WriterLockAsync();
            long d1 = -1;
            long d2 = -1;
            var r1T = Task.Run(async () =>
            {
                using var r1 = await rwl.ReaderLockAsync();
                d1 = Stopwatch.GetTimestamp();
                await Task.Delay(1000);
            });
            var r2T = Task.Run(() =>
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

            Assert.Equal((double)d1 / Stopwatch.Frequency, (double)d2 / Stopwatch.Frequency, 1);
        }

        [Fact(Timeout = 100)]
        public async Task ShouldNotStarveSyncWriters()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = await rwl.WriterLockAsync();

            var w2T = Task.Run(rwl.WriterLock);
            await Task.Delay(2);
            var w3T = rwl.WriterLockAsync();
            w1.Dispose();
            var w2 = await w2T;
            w2.Dispose();
            var w3 = await w3T;
            w3.Dispose();
        }


        [Fact(Timeout = 100)]
        public async Task ShouldNotStarveAsyncWriters()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = rwl.WriterLock();

            var w2T = rwl.WriterLockAsync();
            await Task.Delay(2);
            var w3T = Task.Run(rwl.WriterLock);
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
        [Fact(Timeout = 1500)]
        public async Task PreventsReaders()
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

        [Fact(Timeout = 1500)]
        public async Task PreventsWriters()
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

        [Fact(Timeout = 1500)]
        public async Task AwakeAllReadersAtOnce()
        {
            var rwl = new AsyncReaderWriterLock();
            var w1 = rwl.WriterLock();
            long d1 = -1;
            long d2 = -1;
            var r1T = Task.Run(() =>
            {
                using var r1 = rwl.ReaderLock();
                d1 = Stopwatch.GetTimestamp();
                Thread.Sleep(1000);
            });
            var r2T = Task.Run(() =>
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

            Assert.Equal((double)d1 / Stopwatch.Frequency, (double)d2 / Stopwatch.Frequency, 1);
        }
    }

    public class WhenReaderLockedAsync
    {
        [Fact(Timeout = 1500)]
        public async Task PreventWriters()
        {
            var rwl = new AsyncReaderWriterLock();
            var r1 = await rwl.ReaderLockAsync();
            var w1T = rwl.WriterLockAsync();
            await AssertHelper.TimesOutAsync(w1T);
            r1.Dispose();
            var w1 = await w1T;
            w1.Dispose();
        }

        [Fact(Timeout = 10)]
        public async Task WritersAreNotStarved()
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
        [Fact(Timeout = 1500)]
        public async Task PreventWriters()
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

        [Fact(Timeout = 10)]
        public async Task WritersAreNotStarved()
        {
            var rwl = new AsyncReaderWriterLock();
            var r1 = rwl.ReaderLock();
            var r2 = rwl.ReaderLock();

            var w1T = Task.Run(() =>
            {
                using var w1 = rwl.WriterLock();
            });
            r1.Dispose();
            var r3T = Task.Run(() =>
            {
                using var r3 = rwl.ReaderLock();
            });
            var r4T = Task.Run(() =>
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
        [Fact(Timeout = 1500)]
        public async Task PreventWritersAsyncWriter()
        {
            var rwl = new AsyncReaderWriterLock();
            var r1 = rwl.ReaderLock();
            var w1Tcs = new TaskCompletionSource();
            await AssertHelper.TimesOutAsync(rwl.WriterLockAsync().ContinueWith(t =>
            {
                t.Result.Dispose();
                w1Tcs.SetResult();
            }));
            r1.Dispose();
            await w1Tcs.Task;
        }

        [Fact(Timeout = 1500)]
        public async Task PreventWritersSyncWriter()
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

        [Fact(Timeout = 10)]
        public async Task WritersAreNotStarvedSyncWriter()
        {
            var rwl = new AsyncReaderWriterLock();
            var r1 = rwl.ReaderLock();
            var r2 = await rwl.ReaderLockAsync();

            var w1T = Task.Run(() =>
            {
                using var w1 = rwl.WriterLock();
            });
            r1.Dispose();
            var r3T = Task.Run(async () =>
            {
                using var r3 = await rwl.ReaderLockAsync();
            });
            var r4T = Task.Run(() =>
            {
                using var r3 = rwl.ReaderLock();
            });
            await Task.Yield();
            r2.Dispose();
            await w1T;
            await r3T;
            await r4T;
        }

        [Fact(Timeout = 10)]
        public async Task WritersAreNotStarvedAsyncWriter()
        {
            var rwl = new AsyncReaderWriterLock();
            var r1 = rwl.ReaderLock();
            var r2 = await rwl.ReaderLockAsync();

            var w1T = Task.Run(async () =>
            {
                using var w1 = await rwl.WriterLockAsync();
            });
            r1.Dispose();
            var r3T = Task.Run(async () =>
            {
                using var r3 = await rwl.ReaderLockAsync();
            });
            var r4T = Task.Run(() =>
            {
                using var r3 = rwl.ReaderLock();
            });
            await Task.Yield();
            r2.Dispose();
            await w1T;
            await r3T;
            await r4T;
        }

        [Fact(Timeout = 100)]
        public async Task ShouldNotStarveSyncWriters()
        {
            var rwl = new AsyncReaderWriterLock();
            var r1 = await rwl.ReaderLockAsync();

            var w1T = Task.Run(rwl.WriterLock);
            await Task.Delay(2);
            var w2T = rwl.WriterLockAsync();
            r1.Dispose();
            var w1 = await w1T;
            w1.Dispose();
            var w2 = await w2T;
            w2.Dispose();
        }


        [Fact(Timeout = 100)]
        public async Task ShouldNotStarveAsyncWriters()
        {
            var rwl = new AsyncReaderWriterLock();
            var r1 = rwl.ReaderLock();

            var w1T = rwl.WriterLockAsync();
            await Task.Delay(2);
            var w2T = Task.Run(rwl.WriterLock);
            await Task.Delay(2);
            r1.Dispose();
            var w1 = await w1T;
            w1.Dispose();

            var w2 = await w2T;
            w2.Dispose();
        }
    }
}