namespace CodeArt.ThreadUtils;

public static class InterlockedEx
{
    public static int Apply(ref int number, Func<int, int> fn)
    {
        var oldValue = Volatile.Read(ref number);
        var newValue = fn(oldValue);
        int actualValue;
        while ((actualValue = Interlocked.CompareExchange(ref number, newValue, oldValue)) != oldValue)
        {
            oldValue = actualValue;
            newValue = fn(oldValue);
        }
        return newValue;
    }

    public static long Apply(ref long number, Func<long, long> fn)
    {
        var oldValue = Volatile.Read(ref number);
        var newValue = fn(oldValue);
        long actualValue;
        while ((actualValue = Interlocked.CompareExchange(ref number, newValue, oldValue)) != oldValue)
        {
            oldValue = actualValue;
            newValue = fn(oldValue);
        }
        return newValue;
    }        
}