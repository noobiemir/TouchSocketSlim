using TouchSocketSlim.Core;

namespace TouchSocketSlim.Sockets;

public class TcpServiceConfig
{
    public TcpServiceConfig(Func<SingleStreamDataHandlingAdapter> adapterFactory, params IpHost[] listenIpHosts)
    {
        DataHandlingAdapterFactory = adapterFactory;
        ListenIpHosts = listenIpHosts;
        if (listenIpHosts.Length == 0)
        {
            throw new ArgumentException(nameof(listenIpHosts));
        }
    }

    public TcpServiceConfig(params IpHost[] listenIpHosts) : this(() => new NormalDataHandlingAdapter(), listenIpHosts)
    {
    }

    public int? MinBufferSize { get; set; }

    public int? MaxBufferSize { get; set; }

    public int MaxCount { get; set; } = 10000;

    public Func<string>? DefaultNewIdFactory { get; set; }

    public Action<List<TcpListenOption>>? ListenOptions { get; set; }

    public IpHost[] ListenIpHosts { get; }

    public Func<SingleStreamDataHandlingAdapter> DataHandlingAdapterFactory { get; }

    public bool? NoDelay { get; set; }

    public int SendTimeout { get; set; }

    public bool ReuseAddress { get; set; }

    public int Backlog { get; set; } = 100;

    public ServiceSslOption? SslOption { get; set; }
}