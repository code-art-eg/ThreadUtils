namespace CodeArt.ThreadUtils;

/// <summary>
/// Interface representing thread or task waiting to acquire a lock
/// that can be awakened
/// </summary>
internal interface IWaiter
{
    /// <summary>
    /// Awaken the waiter (The lock would be acquired)
    /// </summary>
    /// <returns>True if the waiter was awakened successfully, false otherwise.
    /// This method can fail if the waiter had a cancellation token that was cancelled.</returns>
    bool Awaken();
}