using System;
using TouchSocketSlim.Core;

namespace TouchSocketSlim.Sockets
{
public class TcpListenOption
{
    public TcpListenOption(IpHost ipHost)
    {
        IpHost = ipHost ?? throw new ArgumentNullException(nameof(ipHost));
    }

    public IpHost IpHost { get; }

    public int SendTimeout { get; set; }

    public bool ReuseAddress { get; set; }

    public int Backlog { get; set; } = 100;

    public bool? NoDelay { get; set; }

    public ServiceSslOption? ServiceSslOption { get; set; }

    public bool UseSsl => ServiceSslOption is not null;

    public Func<SingleStreamDataHandlingAdapter> AdapterFactory { get; set; } = () => new NormalDataHandlingAdapter();
}
}
