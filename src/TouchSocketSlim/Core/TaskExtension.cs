namespace TouchSocketSlim.Core;

internal static class TaskExtension
{
    public static async Task WaitAsync(this Task task, TimeSpan timeout)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource();
        var delayTask = Task.Delay(timeout, timeoutCancellationTokenSource.Token);
        if (await Task.WhenAny(task, delayTask) != task) throw new TimeoutException();
        timeoutCancellationTokenSource.Cancel();
        await task;
    }
}