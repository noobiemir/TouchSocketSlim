using System.Net.Sockets;
using TouchSocketSlim.Core;

namespace TouchSocketSlim.Sockets;

public class ConnectingEventArgs : TouchSocketEventArgs, IPermitEventArgs
{
    public ConnectingEventArgs(Socket socket, string? id = null)
    {
        Id = id;
        Socket = socket;
        IsPermitOperation = true;
    }

    public string? Id { get; set; }

    public bool IsPermitOperation { get; set; }

    public Socket Socket { get; }
}