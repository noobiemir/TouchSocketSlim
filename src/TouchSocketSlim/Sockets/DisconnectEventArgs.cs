using TouchSocketSlim.Core;

namespace TouchSocketSlim.Sockets;

public class DisconnectEventArgs : TouchSocketEventArgs
{
    public DisconnectEventArgs(bool manual, string message)
    {
        Message = message;
        Manual = manual;
    }

    public string Message { get; }

    public bool Manual { get; }
}