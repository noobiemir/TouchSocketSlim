using TouchSocketSlim.Core;

namespace TouchSocketSlim.Sockets;

public class TcpClientConfig
{
    public TcpClientConfig(IpHost remoteIpHost, Func<SingleStreamDataHandlingAdapter> dataHandlingAdapterFactory)
    {
        DataHandlingAdapterFactory = dataHandlingAdapterFactory ?? throw new ArgumentNullException(nameof(dataHandlingAdapterFactory));
        RemoteIpHost = remoteIpHost ?? throw new ArgumentNullException(nameof(remoteIpHost));
    }

    public TcpClientConfig(IpHost remoteIpHost) : this(remoteIpHost, () => new FixedHeaderPackageAdapter())
    {

    }

    public Func<SingleStreamDataHandlingAdapter> DataHandlingAdapterFactory { get; }

    public IpHost RemoteIpHost { get; }

    public int SendTimeout { get; set; }

    public KeepAliveValue? KeepAlive { get; set; }

    public bool? NoDelay { get; set; }

    public int? MinBufferSize { get; set; }

    public int? MaxBufferSize { get; set; }

    public ClientSslOption? SslOption { get; set; }
}