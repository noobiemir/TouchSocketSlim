using TouchSocketSlim.Core;

namespace TouchSocketSlim.Sockets;

public readonly struct ReceiverResult : IDisposable
{
    private readonly Action? _disposeAction;

    public ReceiverResult(Action disposeAction, ByteBlock? byteBlock, IRequestInfo? requestInfo)
    {
        _disposeAction = disposeAction;
        ByteBlock = byteBlock;
        RequestInfo = requestInfo;
    }

    public IRequestInfo? RequestInfo { get; }

    public ByteBlock? ByteBlock { get; }

    public bool IsClosed => ByteBlock == null && RequestInfo == null;

    public void Dispose()
    {
        _disposeAction?.Invoke();
    }
}