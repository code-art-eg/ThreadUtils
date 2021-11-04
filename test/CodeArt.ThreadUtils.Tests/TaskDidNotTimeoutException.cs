using System.Threading.Tasks;
using Xunit.Sdk;

namespace CodeArt.ThreadUtils.Tests;

internal class TaskDidNotTimeoutException : XunitException
{
    public TaskDidNotTimeoutException(int millisecondsTimeout, Task task) : base($"Task was expected to timeout after {millisecondsTimeout} ms, but it was completed with status: {task.Status}.")
    {

    }
}