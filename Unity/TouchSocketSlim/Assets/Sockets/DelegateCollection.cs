namespace TouchSocketSlim.Sockets
{
    public delegate void ConnectedEventHandler<in TClient>(TClient client, ConnectedEventArgs e);

    public delegate void ConnectingEventHandler<in TClient>(TClient client, ConnectingEventArgs e);

    public delegate void DisconnectEventHandler<in TClient>(TClient client, DisconnectEventArgs e);

    public delegate void ReceivedEventHandler<in TClient>(TClient client, byte[] buffer, int offset, int length);
}