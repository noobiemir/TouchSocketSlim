using System.Buffers;

namespace TouchSocketSlim.Core;

public abstract class SingleStreamDataHandlingAdapter : DataHandlingAdapter
{
    public TimeSpan CacheTimeout { get; set; } = TimeSpan.FromSeconds(1);

    public bool CacheTimeoutEnable { get; set; } = false;

    public Action<ByteBlock>? ReceivedCallBack { get; set; }

    public Action<byte[], int, int>? SendCallBack { get; set; }

    public Func<byte[], int, int, Task>? SendAsyncCallBack { get; set; }

    public bool UpdateCacheTimeWhenRev { get; set; } = true;

    protected DateTime LastCacheTime { get; set; }

    public override bool CanSendRequestInfo => false;

    public override bool CanSplicingSend => false;

    public override bool CanSendReadOnlySequence => false;

    public void ReceivedInput(ByteBlock byteBlock)
    {
        try
        {
            PreviewReceived(byteBlock);
        }
        catch (Exception ex)
        {
            OnError(ex.Message);
        }
    }

    public void SendInput(byte[] buffer, int offset, int length)
    {
        PreviewSend(buffer, offset, length);
    }

    public void SendInput(IList<ArraySegment<byte>> transferBytes)
    {
        PreviewSend(transferBytes);
    }

    public void SendInput(ReadOnlySequence<byte> transferBytes)
    {
        PreviewSend(transferBytes);
    }

    protected virtual void PreviewSend(IList<ArraySegment<byte>> transferBytes)
    {
        throw new NotImplementedException();
    }

    protected virtual void PreviewSend(ReadOnlySequence<byte> transferBytes)
    {
        throw new NotImplementedException();
    }

    protected virtual void PreviewSend(byte[] buffer, int offset, int length)
    {
        GoSend(buffer, offset, length);
    }

    protected virtual Task PreviewSendAsync(byte[] buffer, int offset, int length)
    {
        return this.GoSendAsync(buffer, offset, length);
    }

    protected virtual Task PreviewSendAsync(IList<ArraySegment<byte>> transferBytes)
    {
        throw new NotImplementedException();
    }

    protected virtual Task PreviewSendAsync(ReadOnlySequence<byte> transferBytes)
    {
        throw new NotImplementedException();
    }

    protected void GoReceived(ByteBlock byteBlock)
    {
        ReceivedCallBack?.Invoke(byteBlock);
    }

    protected void GoSend(byte[] buffer, int offset, int length)
    {
        SendCallBack?.Invoke(buffer, offset, length);
    }

    protected Task GoSendAsync(byte[] buffer, int offset, int length)
    {
        return SendAsyncCallBack?.Invoke(buffer, offset, length) ?? Task.CompletedTask;
    }

    protected abstract void PreviewReceived(ByteBlock byteBlock);

    protected override void Reset()
    {
        LastCacheTime = DateTime.Now;
    }

    public Task SendInputAsync(byte[] buffer, int offset, int length)
    {
        return PreviewSendAsync(buffer, offset, length);
    }

    public Task SendInputAsync(IList<ArraySegment<byte>> transferBytes)
    {
        return PreviewSendAsync(transferBytes);
    }

    public Task SendInputAsync(ReadOnlySequence<byte> transferBytes)
    {
        return PreviewSendAsync(transferBytes);
    }

}