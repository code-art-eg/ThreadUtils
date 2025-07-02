namespace CodeArt.ThreadUtils;

internal class AsyncWaiter(CancellationTokenRegistration registration,
    TaskCompletionSource<IDisposable> source,
    IDisposable releaser): IWaiter
{
    private CancellationTokenRegistration _registration = registration;

    public bool Awaken()
    {
        _registration.Dispose();
        return source.TrySetResult(releaser);
    }
}