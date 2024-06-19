namespace CodeArt.ThreadUtils.Tests;

internal static class TestHelpers
{
    /// <summary>
    /// A helper method to start a synchronous action in a Task and wait until the new task is started (not completed)
    /// </summary>
    /// <param name="action">Action to run</param>
    /// <returns>The task that completes when the task is started after a delay of 1ms</returns>
    public static async Task<Task> StartSync(Action action)
    {
        var tcs = new TaskCompletionSource();
        var task = new Task(Closure);
        task.Start();
        await tcs.Task;
        await Task.Delay(1);
        return task;
        
        void Closure()
        {
            tcs.SetResult();
            action();
        }
    }
    
    /// <summary>
    /// A helper method to start a synchronous action in a Task and wait until the new task is started (not completed)
    /// </summary>
    /// <param name="action">Action to run</param>
    /// <returns>The task that completes when the task is started after a delay of 1ms</returns>
    public static async Task<Task<T>> StartSync<T>(Func<T> action)
    {
        var tcs = new TaskCompletionSource();
        var task = new Task<T>(Closure);
        task.Start();
        await tcs.Task;
        await Task.Delay(1);
        return task;
        
        T Closure()
        {
            tcs.SetResult();
            return action();
        }
    }
    
    /// <summary>
    /// A helper method to start an asynchronous action in a Task and wait until the new task is started (not completed)
    /// </summary>
    /// <param name="action">Action to run</param>
    /// <returns>The task that completes when the task is started after a delay of 1ms</returns>
    public static async Task<Task> StartAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource();
        var task = Closure();
        await tcs.Task;
        await Task.Delay(1);
        return task;
        
        async Task Closure()
        {
            tcs.SetResult();
            await action();
        }
    }
}