namespace CodeArt.ThreadUtils.Tests;

internal class TaskDidNotTimeoutException(int millisecondsTimeout, Task task) : XunitException(
    $"Task was expected to timeout after {millisecondsTimeout} ms, but it was completed with status: {task.Status}.");