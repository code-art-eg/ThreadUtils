namespace CodeArt.ThreadUtils.Tests;

internal static class AssertHelper
{
    public static async Task TimesOutAsync(Task task, int millisecondsTimeout = 1000)
    {
        var completed = await Task.WhenAny(task, Task.Delay(millisecondsTimeout));
        if (completed == task)
            throw new TaskDidNotTimeoutException(millisecondsTimeout, task);
    }
    
    public static async Task TimesOutAsync(ValueTask task, int millisecondsTimeout = 1000)
    {
        var asTask = task.AsTask();
        var completed = await Task.WhenAny(asTask, Task.Delay(millisecondsTimeout));
        if (completed == asTask)
            throw new TaskDidNotTimeoutException(millisecondsTimeout, asTask);
    }
    
    public static async Task TimesOutAsync<T>(ValueTask<T> task, int millisecondsTimeout = 1000)
    {
        var asTask = task.AsTask();
        var completed = await Task.WhenAny(asTask, Task.Delay(millisecondsTimeout));
        if (completed == asTask)
            throw new TaskDidNotTimeoutException(millisecondsTimeout, asTask);
    }

    public static Task TimesOutAsync(Action action, int millisecondsTimeout = 1000)
    {
        return TimesOutAsync(Task.Run(action), millisecondsTimeout);
    }
}