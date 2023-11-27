using System.Net.Sockets;

namespace TouchSocketSlim.Sockets
{
    internal static class SocketExtension
    {
        public static void TryClose(this Socket socket)
        {
            lock (socket)
            {
                try
                {
                    if (socket.Connected)
                    {
                        socket.Close();
                    }
                }
                catch
                {
                    //ignore
                }
            }
        }
    }
}
