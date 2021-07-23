using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CodeArt.ThreadUtils.Tests
{
    public class AsyncReaderWriterLockTests
    {
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

        public class WhenUlockedSync
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
                var r2t = rwl.ReaderLockAsync();
                await AssertHelper.TimesoutAsync(r2t);
                w1.Dispose();
                var r2 = await r2t;
                r2.Dispose();
            }

            [Fact(Timeout = 1500)]
            public async Task PreventsWriters()
            {
                var rwl = new AsyncReaderWriterLock();
                var w1 = await rwl.WriterLockAsync();
                var w2t = rwl.WriterLockAsync();
                await AssertHelper.TimesoutAsync(w2t);
                w1.Dispose();
                var w2 = await w2t;
                w2.Dispose();
            }

            [Fact(Timeout = 1500)]
            public async Task AwakeAllReadersAtOnce()
            {
                var rwl = new AsyncReaderWriterLock();
                var w1 = await rwl.WriterLockAsync();
                long d1 = -1;
                long d2 = -1;
                var r1t = Task.Run(async () =>
                {
                    using var r1 = await rwl.ReaderLockAsync();
                    d1 = Stopwatch.GetTimestamp();
                    await Task.Delay(1000);
                });
                var r2t = Task.Run(async () =>
                {
                    using var r2 = await rwl.ReaderLockAsync();
                    d2 = Stopwatch.GetTimestamp();
                    await Task.Delay(1000);
                });
                w1.Dispose();
                await r1t;
                await r2t;

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
                var r2t = rwl.ReaderLockAsync();
                await AssertHelper.TimesoutAsync(r2t);
                w1.Dispose();
                var r2 = await r2t;
                r2.Dispose();
            }

            [Fact(Timeout = 1500)]
            public async Task PreventsReadersAsyncFirst()
            {
                var rwl = new AsyncReaderWriterLock();
                var w1 = await rwl.WriterLockAsync();
                var r2tcs = new TaskCompletionSource();
                await AssertHelper.TimesoutAsync(() =>
                {
                    using var r2 = rwl.ReaderLock();
                    r2tcs.SetResult();
                });
                w1.Dispose();
                await r2tcs.Task;
            }

            [Fact(Timeout = 1500)]
            public async Task PreventsWritersSyncFirst()
            {
                var rwl = new AsyncReaderWriterLock();
                var w1 = rwl.WriterLock();
                var w2t = rwl.WriterLockAsync();
                await AssertHelper.TimesoutAsync(w2t);
                w1.Dispose();
                var w2 = await w2t;
                w2.Dispose();
            }

            [Fact(Timeout = 1500)]
            public async Task PreventsWritersAsyncFirst()
            {
                var rwl = new AsyncReaderWriterLock();
                var w1 = await rwl.WriterLockAsync();
                var w2tcs = new TaskCompletionSource();
                await AssertHelper.TimesoutAsync(() =>
                {
                    using var w2 = rwl.WriterLock();
                    w2tcs.SetResult();
                });
                w1.Dispose();
                await w2tcs.Task;
            }

            [Fact(Timeout = 1500)]
            public async Task AwakeAllReadersAtOnceSyncWriter()
            {
                var rwl = new AsyncReaderWriterLock();
                var w1 = rwl.WriterLock();
                long d1 = -1;
                long d2 = -1;
                var r1t = Task.Run(async () =>
                {
                    using var r1 = await rwl.ReaderLockAsync();
                    d1 = Stopwatch.GetTimestamp();
                    await Task.Delay(1000);
                });
                var r2t = Task.Run(() =>
                {
                    using var r2 = rwl.ReaderLock();
                    d2 = Stopwatch.GetTimestamp();
                    Thread.Sleep(1000);
                });
                w1.Dispose();
                await r1t;
                await r2t;

                Assert.NotEqual(-1, d1);
                Assert.NotEqual(-1, d2);

                Assert.Equal((double)d1 / Stopwatch.Frequency, (double)d2 / Stopwatch.Frequency, 2);
            }

            [Fact(Timeout = 1500)]
            public async Task AwakeAllReadersAtOnceAsyncWriter()
            {
                var rwl = new AsyncReaderWriterLock();
                var w1 = await rwl.WriterLockAsync();
                long d1 = -1;
                long d2 = -1;
                var r1t = Task.Run(async () =>
                {
                    using var r1 = await rwl.ReaderLockAsync();
                    d1 = Stopwatch.GetTimestamp();
                    await Task.Delay(1000);
                });
                var r2t = Task.Run(() =>
                {
                    using var r2 = rwl.ReaderLock();
                    d2 = Stopwatch.GetTimestamp();
                    Thread.Sleep(1000);
                });
                w1.Dispose();
                await r1t;
                await r2t;

                Assert.NotEqual(-1, d1);
                Assert.NotEqual(-1, d2);

                Assert.Equal((double)d1 / Stopwatch.Frequency, (double)d2 / Stopwatch.Frequency, 2);
            }

            [Fact(Timeout = 100)]
            public async Task ShouldNotStarveSyncWriters()
            {
                var rwl = new AsyncReaderWriterLock();
                var w1 = await rwl.WriterLockAsync();

                var w2t = Task.Run(() =>
                {
                    return rwl.WriterLock();
                });
                await Task.Delay(2);
                var w3t = rwl.WriterLockAsync();
                w1.Dispose();
                var w2 = await w2t;
                w2.Dispose();
                var w3 = await w3t;
                w3.Dispose();
            }


            [Fact(Timeout = 100)]
            public async Task ShouldNotStarveAsyncWriters()
            {
                var rwl = new AsyncReaderWriterLock();
                var w1 = rwl.WriterLock();

                var w2t = rwl.WriterLockAsync();
                await Task.Delay(2);
                var w3t = Task.Run(() =>
                {
                    return rwl.WriterLock();
                });
                await Task.Delay(2);
                w1.Dispose();
                var w2 = await w2t;
                w2.Dispose();
                
                var w3 = await w3t;
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
                var r1tcs = new TaskCompletionSource();
                await AssertHelper.TimesoutAsync(() =>
                {
                    using var r1 = rwl.ReaderLock();
                    r1tcs.SetResult();
                });
                w1.Dispose();
                await r1tcs.Task;
            }

            [Fact(Timeout = 1500)]
            public async Task PreventsWriters()
            {
                var rwl = new AsyncReaderWriterLock();
                var w1 = rwl.WriterLock();
                var w2tcs = new TaskCompletionSource();
                await AssertHelper.TimesoutAsync(() =>
                {
                    using var w2 = rwl.WriterLock();
                    w2tcs.SetResult();
                });
                w1.Dispose();
                await w2tcs.Task;
            }

            [Fact(Timeout = 1500)]
            public async Task AwakeAllReadersAtOnce()
            {
                var rwl = new AsyncReaderWriterLock();
                var w1 = rwl.WriterLock();
                long d1 = -1;
                long d2 = -1;
                var r1t = Task.Run(() =>
                {
                    using var r1 = rwl.ReaderLock();
                    d1 = Stopwatch.GetTimestamp();
                    Thread.Sleep(1000);
                });
                var r2t = Task.Run(() =>
                {
                    using var r2 = rwl.ReaderLock();
                    d2 = Stopwatch.GetTimestamp();
                    Thread.Sleep(1000);
                });
                w1.Dispose();
                await r1t;
                await r2t;

                Assert.NotEqual(-1, d1);
                Assert.NotEqual(-1, d2);

                Assert.Equal((double)d1 / Stopwatch.Frequency, (double)d2 / Stopwatch.Frequency, 2);
            }
        }

        public class WhenReaderLockedAsync
        {
            [Fact(Timeout = 1500)]
            public async Task PreventWriters()
            {
                var rwl = new AsyncReaderWriterLock();
                var r1 = await rwl.ReaderLockAsync();
                var w1t = rwl.WriterLockAsync();
                await AssertHelper.TimesoutAsync(w1t);
                r1.Dispose();
                var w1 = await w1t;
                w1.Dispose();
            }

            [Fact(Timeout = 10)]
            public async Task WritersAreNotStarved()
            {
                var rwl = new AsyncReaderWriterLock();
                var r1 = await rwl.ReaderLockAsync();
                var r2 = await rwl.ReaderLockAsync();
                var w1t = rwl.WriterLockAsync();
                r1.Dispose();
                var r3t = rwl.ReaderLockAsync();
                var r4t = rwl.ReaderLockAsync();
                r2.Dispose();
                var w1 = await w1t;
                w1.Dispose();
                var r3 = await r3t;
                var r4 = await r4t;
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
                var w1tcs = new TaskCompletionSource();
                await AssertHelper.TimesoutAsync(() =>
                {
                    using var w1 = rwl.WriterLock();
                    w1tcs.SetResult();
                });
                r1.Dispose();
                await w1tcs.Task;
            }

            [Fact(Timeout = 10)]
            public async Task WritersAreNotStarved()
            {
                var rwl = new AsyncReaderWriterLock();
                var r1 = rwl.ReaderLock();
                var r2 = rwl.ReaderLock();

                var w1t = Task.Run(() =>
                {
                    using var w1 = rwl.WriterLock();
                });
                r1.Dispose();
                var r3t = Task.Run(() =>
                {
                    using var r3 = rwl.ReaderLock();
                });
                var r4t = Task.Run(() =>
                {
                    using var r3 = rwl.ReaderLock();
                });
                await Task.Yield();
                r2.Dispose();
                await w1t;
                await r3t;
                await r4t;
            }
        }

        public class WhenReaderLockedMix
        {
            [Fact(Timeout = 1500)]
            public async Task PreventWritersAsyncWriter()
            {
                var rwl = new AsyncReaderWriterLock();
                var r1 = rwl.ReaderLock();
                var w1tcs = new TaskCompletionSource();
                await AssertHelper.TimesoutAsync(rwl.WriterLockAsync().ContinueWith(t =>
                {
                    t.Result.Dispose();
                    w1tcs.SetResult();
                }));
                r1.Dispose();
                await w1tcs.Task;
            }

            [Fact(Timeout = 1500)]
            public async Task PreventWritersSyncWriter()
            {
                var rwl = new AsyncReaderWriterLock();
                var r1 = await rwl.ReaderLockAsync();
                var w1tcs = new TaskCompletionSource();
                await AssertHelper.TimesoutAsync(() =>
                {
                    using var w1 = rwl.WriterLock();
                    w1tcs.SetResult();
                });
                r1.Dispose();
                await w1tcs.Task;
            }

            [Fact(Timeout = 10)]
            public async Task WritersAreNotStarvedSyncWriter()
            {
                var rwl = new AsyncReaderWriterLock();
                var r1 = rwl.ReaderLock();
                var r2 = await rwl.ReaderLockAsync();

                var w1t = Task.Run(() =>
                {
                    using var w1 = rwl.WriterLock();
                });
                r1.Dispose();
                var r3t = Task.Run(async () =>
                {
                    using var r3 = await rwl.ReaderLockAsync();
                });
                var r4t = Task.Run(() =>
                {
                    using var r3 = rwl.ReaderLock();
                });
                await Task.Yield();
                r2.Dispose();
                await w1t;
                await r3t;
                await r4t;
            }

            [Fact(Timeout = 10)]
            public async Task WritersAreNotStarvedAsyncWriter()
            {
                var rwl = new AsyncReaderWriterLock();
                var r1 = rwl.ReaderLock();
                var r2 = await rwl.ReaderLockAsync();

                var w1t = Task.Run(async () =>
                {
                    using var w1 = await rwl.WriterLockAsync();
                });
                r1.Dispose();
                var r3t = Task.Run(async () =>
                {
                    using var r3 = await rwl.ReaderLockAsync();
                });
                var r4t = Task.Run(() =>
                {
                    using var r3 = rwl.ReaderLock();
                });
                await Task.Yield();
                r2.Dispose();
                await w1t;
                await r3t;
                await r4t;
            }

            [Fact(Timeout = 100)]
            public async Task ShouldNotStarveSyncWriters()
            {
                var rwl = new AsyncReaderWriterLock();
                var r1 = await rwl.ReaderLockAsync();

                var w1t = Task.Run(() =>
                {
                    return rwl.WriterLock();
                });
                await Task.Delay(2);
                var w2t = rwl.WriterLockAsync();
                r1.Dispose();
                var w1 = await w1t;
                w1.Dispose();
                var w2 = await w2t;
                w2.Dispose();
            }


            [Fact(Timeout = 100)]
            public async Task ShouldNotStarveAsyncWriters()
            {
                var rwl = new AsyncReaderWriterLock();
                var r1 = rwl.ReaderLock();

                var w1t = rwl.WriterLockAsync();
                await Task.Delay(2);
                var w2t = Task.Run(() =>
                {
                    return rwl.WriterLock();
                });
                await Task.Delay(2);
                r1.Dispose();
                var w1 = await w1t;
                w1.Dispose();

                var w2 = await w2t;
                w2.Dispose();
            }
        }
    }
}
