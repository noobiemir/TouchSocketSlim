namespace TouchSocketSlim.Sockets;

internal interface IPermitEventArgs
{
    bool IsPermitOperation { get; set; }
}