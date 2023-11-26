using System.Net.Sockets;

namespace TouchSocketSlim.Sockets;

public class TcpNetworkMonitor
{
    public TcpListenOption Option { get; }
    public Socket Socket { get; }
    public SocketAsyncEventArgs? SocketAsyncEvent { get; }

    public TcpNetworkMonitor(TcpListenOption option, Socket socket, SocketAsyncEventArgs? socketAsyncEvent)
    {
        Option = option ?? throw new ArgumentNullException(nameof(option));
        Socket = socket ?? throw new ArgumentNullException(nameof(socket));
        SocketAsyncEvent = socketAsyncEvent;
    }
}