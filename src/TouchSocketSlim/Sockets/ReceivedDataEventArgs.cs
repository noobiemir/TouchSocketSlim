using TouchSocketSlim.Core;

namespace TouchSocketSlim.Sockets;

public class ReceivedDataEventArgs : ByteBlockEventArgs
{
    public ReceivedDataEventArgs(ByteBlock byteBlock, IRequestInfo? requestInfo) : base(byteBlock)
    {
        RequestInfo = requestInfo;
    }
    public IRequestInfo? RequestInfo { get; }
}