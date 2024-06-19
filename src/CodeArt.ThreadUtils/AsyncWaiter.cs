namespace CodeArt.ThreadUtils;

internal class AsyncWaiter(CancellationTokenRegistration registration,
    TaskCompletionSource<IDisposable> source,
    IDisposable releaser): IWaiter
{
    public bool Awaken()
    {
        registration.Dispose();
        return source.TrySetResult(releaser);
    }
}