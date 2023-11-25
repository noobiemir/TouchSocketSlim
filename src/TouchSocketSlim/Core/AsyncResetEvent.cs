namespace TouchSocketSlim.Core;

public class AsyncResetEvent : DisposableObject
{
    private readonly bool _autoReset;
    private readonly object _locker = new object();
    private readonly Queue<TaskCompletionSource<bool>> _waitQueue = new();
    private volatile bool _eventSet;

    public AsyncResetEvent(bool initialState, bool autoReset)
    {
        _eventSet = initialState;
        _autoReset = autoReset;
    }

    public async Task<bool> WaitOneAsync(TimeSpan timeout)
    {
        try
        {
            using var timeoutSource = new CancellationTokenSource(timeout);
            await WaitOneAsync(timeoutSource.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public Task WaitOneAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        lock (_locker)
        {
            if (_eventSet)
            {
                if (_autoReset)
                {
                    _eventSet = false;
                }

                return Task.CompletedTask;
            }

            var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var registration = cancellationToken.Register(() =>
            {
                lock (_locker)
                {
                    completionSource.TrySetCanceled(cancellationToken);
                }
            }, useSynchronizationContext: false);

            _waitQueue.Enqueue(completionSource);

            completionSource.Task.ContinueWith(_ => registration.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return completionSource.Task;
        }
    }

    public bool Reset()
    {
        lock (_locker)
        {
            _eventSet = false;
            return true;
        }
    }

    public bool Set()
    {
        lock (_locker)
        {
            while (_waitQueue.Count > 0)
            {
                var toRelease = this._waitQueue.Dequeue();

                if (toRelease.Task.IsCompleted)
                {
                    continue;
                }

                var b = toRelease.TrySetResult(true);

                if (_autoReset)
                {
                    return b;
                }
            }

            _eventSet = true;
            return false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (IsDisposed)
        {
            return;
        }
        while (true)
        {
            lock (_locker)
            {
                if (_waitQueue.Count == 0)
                {
                    break;
                }
            }

            Set();
        }
        base.Dispose(disposing);
    }
}