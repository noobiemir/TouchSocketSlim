using System.Collections.Concurrent;

namespace TouchSocketSlim.Sockets;

internal class SocketClientCollection : ConcurrentDictionary<string, ISocketClient>, ISocketClientCollection
{
    public IEnumerable<ISocketClient> GetClients()
    {
        return Values;
    }

    public IEnumerable<string> GetIds()
    {
        return Keys;
    }

    public bool SocketClientExist(string id)
    {
        return !string.IsNullOrEmpty(id) && ContainsKey(id);
    }

    public bool TryGetSocketClient(string id, out ISocketClient? client)
    {
        if (string.IsNullOrEmpty(id))
        {
            client = null;
            return false;
        }

        return TryGetValue(id, out client);
    }

    public bool TryGetSocketClient<TClient>(string id, out TClient? client) where TClient : ISocketClient
    {
        if (string.IsNullOrEmpty(id))
        {
            client = default;
            return false;
        }

        if (this.TryGetValue(id, out var socketClient))
        {
            client = (TClient)socketClient;
            return true;
        }
        client = default;
        return false;
    }

    internal bool TryAdd(ISocketClient socketClient)
    {
        return TryAdd(socketClient.Id!, socketClient);
    }
    
    internal bool TryRemove<TClient>(string id, out TClient? socketClient) where TClient : ISocketClient
    {
        if (string.IsNullOrEmpty(id))
        {
            socketClient = default;
            return false;
        }

        if (TryRemove(id, out var client))
        {
            socketClient = (TClient)client;
            return true;
        }
        socketClient = default;
        return false;
    }
}