using System.Net.Sockets;
using TouchSocketSlim.Core;

namespace TouchSocketSlim.Sockets
{
    public class ConnectingEventArgs : TouchSocketEventArgs, IPermitEventArgs
    {
        public ConnectingEventArgs(Socket socket, string? id = null)
        {
            Id = id;
            Socket = socket;
            Accepted = true;
        }

        public string? Id { get; set; }

        public bool Accepted { get; set; }

        public Socket Socket { get; }
    }
}
