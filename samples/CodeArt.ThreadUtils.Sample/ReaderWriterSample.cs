using System;
using System.Threading;
using System.Threading.Tasks;

namespace CodeArt.ThreadUtils.Sample;

internal static class ReaderWriterSample
{
    private const int MillisecondsTimeout = 1000;
    private static readonly AsyncReaderWriterLock s_lock = new();

    public static async Task RunAsync()
    {
        const int n = 10;
        var list = new Task[n];
        for (var i = 0; i < n; i++)
        {
            if (i % 10 == 1)
            {
                list[i] = WriteAsync(i);
            }
            else if (i % 5 == 1)
            {
                var t = i;
                list[i] = Task.Run(() => Write(t));
            }
            else if (i % 2 == 0)
            {
                list[i] = ReadAsync(i);
            }
            else
            {
                var t = i;
                list[i] = Task.Run(() => Read(t));
            }
        }
        await Task.WhenAll(list);
    }

    private static async Task ReadAsync(int id)
    {
        Console.WriteLine($"Async Reader {id} starting at {DateTime.Now}.");
        using (await s_lock.ReaderLockAsync())
        {
            Console.WriteLine($"Async Reader {id} acquired reader lock at {DateTime.Now}.");
            await Task.Delay(MillisecondsTimeout);
            Console.WriteLine($"Async Reader {id} read complete at {DateTime.Now}.");
        }
        Console.WriteLine($"Async Reader {id} released reader lock at {DateTime.Now}.");
    }

    private static async Task WriteAsync(int id)
    {
        Console.WriteLine($"Async Writer {id} starting at {DateTime.Now}.");
        using (await s_lock.WriterLockAsync())
        {
            Console.WriteLine($"Async Writer {id} acquired writer lock at {DateTime.Now}.");
            await Task.Delay(MillisecondsTimeout);
            Console.WriteLine($"Async Writer {id} write complete at {DateTime.Now}.");
        }
        Console.WriteLine($"Async Writer {id} released writer lock at {DateTime.Now}.");
    }

    private static void Read(int id)
    {
        Console.WriteLine($"Sync Reader {id} starting at {DateTime.Now}.");
        using (s_lock.ReaderLock())
        {
            Console.WriteLine($"Sync Reader {id} acquired reader lock at {DateTime.Now}.");
            Thread.Sleep(MillisecondsTimeout);
            Console.WriteLine($"Sync Reader {id} read complete at {DateTime.Now}.");
        }
        Console.WriteLine($"Sync Reader {id} released reader lock at {DateTime.Now}.");
    }

    private static void Write(int id)
    {
        Console.WriteLine($"Sync Writer {id} starting at {DateTime.Now}.");
        using (s_lock.WriterLock())
        {
            Console.WriteLine($"Sync Writer {id} acquired writer lock at {DateTime.Now}.");
            Thread.Sleep(MillisecondsTimeout);
            Console.WriteLine($"Sync Writer {id} write complete at {DateTime.Now}.");
        }
        Console.WriteLine($"Sync Writer {id} released writer lock at {DateTime.Now}.");
    }
}