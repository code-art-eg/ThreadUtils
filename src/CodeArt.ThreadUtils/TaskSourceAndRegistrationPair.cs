namespace CodeArt.ThreadUtils;

internal class TaskSourceAndRegistrationPair(CancellationTokenRegistration registration,
    TaskCompletionSource<IDisposable> source,
    IDisposable releaser): IWaiter
{
    public bool Awaken()
    {
        registration.Dispose();
        return source.TrySetResult(releaser);
    }
}