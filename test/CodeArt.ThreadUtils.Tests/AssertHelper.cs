namespace CodeArt.ThreadUtils.Tests;

internal static class AssertHelper
{
    public static async Task TimesOutAsync(Task task, int millisecondsTimeout = Timeouts.LongTimeout)
    {
        var completed = await Task.WhenAny(task, Task.Delay(millisecondsTimeout));
        if (completed == task)
            throw new TaskDidNotTimeoutException(millisecondsTimeout, task);
    }
    
    public static async Task TimesOutAsync<T>(ValueTask<T> task, int millisecondsTimeout = Timeouts.LongTimeout)
    {
        var asTask = task.AsTask();
        var completed = await Task.WhenAny(asTask, Task.Delay(millisecondsTimeout));
        if (completed == asTask)
            throw new TaskDidNotTimeoutException(millisecondsTimeout, asTask);
    }

    public static Task TimesOutAsync(Action action, int millisecondsTimeout = Timeouts.LongTimeout)
    {
        return TimesOutAsync(Task.Run(action), millisecondsTimeout);
    }
}