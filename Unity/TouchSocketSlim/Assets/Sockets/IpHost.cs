using System;
using System.Net;

namespace TouchSocketSlim.Sockets
{
    public class IpHost : Uri
    {
        private EndPoint? _endPoint;

        public IpHost(string uriString) : base(VerifyUri(uriString))
        {
        }

        public IpHost(int port) : this($"0.0.0.0:{port}") { }

        public IpHost(IPAddress address, int port) : this($"{address}:{port}") { }

        public EndPoint EndPoint
        {
            get
            {
                if (_endPoint != null)
                {
                    return _endPoint;
                }

                _endPoint = HostNameType switch
                {
                    UriHostNameType.Unknown or UriHostNameType.Basic => throw new InvalidOperationException("Unable to get endpoint"),
                    UriHostNameType.Dns => new DnsEndPoint(DnsSafeHost, Port),
                    _ => new IPEndPoint(IPAddress.Parse(DnsSafeHost), Port)
                };

                return _endPoint;
            }
        }

        public new int Port
        {
            get
            {
                if (IsDefaultPort)
                {
                    switch (Scheme.ToLower())
                    {
                        case "http": return 80;
                        case "https": return 443;
                        case "tcp": return 8080;
                    }
                }
                return base.Port;
            }
        }

        private static string VerifyUri(string uriString)
        {
            if (TouchSocketUtility.IsUrl(uriString))
            {
                return uriString;
            }

            return $"tcp://{uriString}";
        }

        public static implicit operator IpHost(string value) => new(value);

        public static implicit operator IpHost(int value) => new(value);
    }
}
