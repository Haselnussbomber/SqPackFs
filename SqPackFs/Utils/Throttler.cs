using Microsoft.UI.Dispatching;
using Microsoft.UI.Reactor;

namespace SqPackFs.Utils;

public class Throttler(TimeSpan cooldown, DispatcherQueueHandler action) : IDisposable
{
    private readonly Lock _lock = new();
    private bool _isThrottling;
    private bool _disposed;

    public void Invoke()
    {
        using (_lock.EnterScope())
        {
            if (_isThrottling || _disposed)
                return;

            _isThrottling = true;
        }

        ReactorApp.UIDispatcher?.TryEnqueue(action);

        Task.Delay(cooldown)
            .ContinueWith(_ =>
            {
                using (_lock.EnterScope())
                {
                    _isThrottling = false;
                }
            }, TaskContinuationOptions.ExecuteSynchronously)
            .ConfigureAwait(false);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
        }
    }
}
