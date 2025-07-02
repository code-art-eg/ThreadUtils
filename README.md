# CodeArt.ThreadUtils

A .NET library providing thread coordination primitives that work seamlessly with C\# async/await pattern. Designed for high-performance concurrent applications where proper thread synchronization is critical.
The lock primitives are based on and inspired by the series of [blog posts](https://devblogs.microsoft.com/dotnet/building-async-coordination-primitives-part-6-asynclock/) by [Stephen Toub](https://devblogs.microsoft.com/dotnet/author/toub) on the dotnet Dev Blogs.

The implementation is a bit different from the one in the blog because I want to have the log primitives support both Asynchronous and Synchronous callers.

The lock primitives are not complete (for now). I plan to add `AsyncSemaphore`, `AsyncManualResetEvent`, etc. 

## Features

### AsyncLock

A lock that supports both synchronous and asynchronous acquisition. This eliminates the need to block threads when waiting for a lock in async contexts.

```csharp
private readonly AsyncLock _lock = new();

// Async usage
async Task DoWorkAsync()
{
    using (await _lock.LockAsync())
    {
        // Critical section (async)
    }
}

// Sync usage
void DoWork()
{
    using (_lock.Lock())
    {
        // Critical section (sync)
    }
}
```

### AsyncReaderWriterLock

A reader-writer lock supporting both synchronous and asynchronous acquisition. Allows multiple concurrent readers or a single writer.

```csharp
private readonly AsyncReaderWriterLock _rwLock = new();

// Reader (async)
async Task ReadDataAsync()
{
    using (await _rwLock.ReaderLockAsync())
    {
        // Read operations
    }
}

// Writer (async)
async Task WriteDataAsync()
{
    using (await _rwLock.WriterLockAsync())
    {
        // Write operations
    }
}
```

### KeyedAsyncLock

Locks based on keys (e.g., resource IDs) without the overhead of maintaining lock objects for every possible key. Only allocates resources when locks are actively being used.

```csharp
private readonly KeyedAsyncLock<string> _keyedLock = new();

async Task ProcessResourceAsync(string resourceId)
{
    using (await _keyedLock.LockAsync(resourceId))
    {
        // Process the specific resource
    }
}
```

### InterlockedEx

Extensions to the standard Interlocked class that allow applying arbitrary functions atomically to numbers.

```csharp
private int _counter;

// Atomically apply a function to the counter
int newValue = InterlockedEx.Apply(ref _counter, x => x * 2 + 1);
```

## Installation

Install via NuGet:

```
Install-Package CodeArt.ThreadUtils
```

## Requirements

- .NET 9.0 or higher

## License

MIT License
