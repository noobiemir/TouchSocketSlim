using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TouchSocketSlim.Core;

namespace TouchSocketSlim.Sockets
{

    public class TcpService<TSocketClient> : TcpServiceBase where TSocketClient : SocketClient, new()
    {
        private readonly Func<string> _getDefaultNewId;
        private long _nextId;
        private readonly int _maxCount;
        private readonly List<TcpNetworkMonitor> _monitors = new();
        private readonly SocketClientCollection _socketClients = new();
        private ServerState _serverState;

        public TcpService(Func<SingleStreamDataHandlingAdapter> adapterFactory, params IpHost[] listenIpHosts) : this(new TcpServiceConfig(adapterFactory, listenIpHosts))
        {

        }

        public TcpService(params IpHost[] listenIpHosts) : this(new TcpServiceConfig(listenIpHosts))
        {

        }

        public TcpService(TcpServiceConfig config) : base(config)
        {
            _getDefaultNewId = config.DefaultNewIdFactory ?? GetDefaultNewId;
            _maxCount = config.MaxCount;
        }

        private string GetDefaultNewId()
        {
            return Interlocked.Increment(ref _nextId).ToString();
        }

        protected string GetNextNewId()
        {
            try
            {
                return _getDefaultNewId.Invoke();
            }
            catch
            {
                //ignore
            }

            return GetDefaultNewId();
        }

        public ReceivedEventHandler<TSocketClient>? Received { get; set; }

        public ConnectedEventHandler<TSocketClient>? Connected { get; set; }

        public ConnectingEventHandler<TSocketClient>? Connecting { get; set; }

        public DisconnectEventHandler<TSocketClient>? Disconnected { get; set; }

        public DisconnectEventHandler<TSocketClient>? Disconnecting { get; set; }


        internal override void OnClientConnected(SocketClient socketClient, ConnectedEventArgs e)
        {
            Connected?.Invoke((TSocketClient)socketClient, e);
        }

        internal override void OnClientConnecting(SocketClient socketClient, ConnectingEventArgs e)
        {
            Connecting?.Invoke((TSocketClient)socketClient, e);
        }

        internal override void OnClientDisconnected(SocketClient socketClient, DisconnectEventArgs e)
        {
            Disconnected?.Invoke((TSocketClient)socketClient, e);
        }

        internal override void OnClientDisconnecting(SocketClient socketClient, DisconnectEventArgs e)
        {
            Disconnecting?.Invoke((TSocketClient)socketClient, e);
        }

        internal override void OnClientReceivedData(SocketClient socketClient, byte[] buffer, int offset, int length)
        {
            OnReceived((TSocketClient)socketClient, buffer, offset, length);
        }

        protected virtual void OnReceived(TSocketClient socketClient, byte[] buffer, int offset, int length)
        {
            Received?.Invoke(socketClient, buffer, offset, length);
        }

        public override ISocketClientCollection SocketClients => _socketClients;

        public override int MaxCount => _maxCount;

        public override IEnumerable<TcpNetworkMonitor> Monitors => _monitors.ToArray();

        public override ServerState ServerState => _serverState;

        public override void AddListen(TcpListenOption options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            ThrowIfDisposed();
            var socket = new Socket(options.IpHost.EndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            if (options.ReuseAddress)
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            }

            var e = new SocketAsyncEventArgs();
            var networkMonitor = new TcpNetworkMonitor(options, socket, e);
            socket.Bind(options.IpHost.EndPoint);
            socket.Listen(options.Backlog);

            e.UserToken = networkMonitor;
            e.Completed += (_, args) =>
            {
                if (args.LastOperation == SocketAsyncOperation.Accept)
                {
                    OnAccepted(e);
                }
            };

            if (!networkMonitor.Socket.AcceptAsync(e))
            {
                OnAccepted(e);
            }
            _monitors.Add(networkMonitor);
        }

        private void OnAccepted(SocketAsyncEventArgs e)
        {
            if (!IsDisposed)
            {
                if (e is { SocketError: SocketError.Success, AcceptSocket: not null })
                {
                    var socket = e.AcceptSocket;

                    if (SocketClients.Count < _maxCount)
                    {
                        _ = OnClientSocketInitAsync(socket, (TcpNetworkMonitor)e.UserToken!);
                    }
                    else
                    {
                        socket.SafeDispose();
                    }
                }

                e.AcceptSocket = null;

                try
                {
                    if (!((TcpNetworkMonitor)e.UserToken!).Socket.AcceptAsync(e))
                    {
                        OnAccepted(e);
                    }
                }
                catch
                {
                    e.SafeDispose();
                }
            }
        }

        protected virtual TSocketClient CreateSocketClient()
        {
            return new TSocketClient();
        }

        private async Task OnClientSocketInitAsync(Socket socket, TcpNetworkMonitor monitor)
        {
            try
            {
                if (monitor.Option.NoDelay != null)
                {
                    socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, monitor.Option.NoDelay.Value);
                }

                socket.SendTimeout = monitor.Option.SendTimeout;

                var client = CreateSocketClient();
                client.Service = this;
                client.ListenOption = monitor.Option;
                client.SetSocket(socket);

                if (client.CanSetDataHandlingAdapter)
                {
                    client.SetDataHandlingAdapter(GetAdapter(monitor));
                }

                client.OnInitialized();

                var args = new ConnectingEventArgs(socket) { Id = GetNextNewId() };

                client.OnConnecting(args);

                if (args.Accepted)
                {
                    client.Id = args.Id;
                    if (!socket.Connected)
                    {
                        socket.SafeDispose();
                        return;
                    }

                    if (_socketClients.TryAdd(client))
                    {
                        client.OnConnected(new ConnectedEventArgs());

                        if (!socket.Connected)
                        {
                            return;
                        }

                        if (monitor.Option.UseSsl)
                        {
                            await client.AuthenticateAsync(monitor.Option.ServiceSslOption!).ConfigureAwait(false);
                            _ = client.BeginReceiveSsl();
                        }
                        else
                        {
                            client.BeginReceive();
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("Id Duplicate");
                    }
                }
                else
                {
                    socket.SafeDispose();
                }
            }
            catch
            {
                socket.SafeDispose();
            }
        }

        private SingleStreamDataHandlingAdapter GetAdapter(TcpNetworkMonitor monitor)
        {
            try
            {
                return monitor.Option.AdapterFactory.Invoke();
            }
            catch
            {
                // ignore
            }

            return new NormalDataHandlingAdapter();
        }

        public override void Clear()
        {
            foreach (var item in GetIds())
            {
                if (TryGetSocketClient(item, out var client))
                {
                    client.SafeDispose();
                }
            }
        }

        public IEnumerable<TSocketClient> GetClients()
        {
            return _socketClients.GetClients().Cast<TSocketClient>();
        }

        public bool TryGetSocketClient(string id, out TSocketClient? socketClient)
        {
            return _socketClients.TryGetSocketClient(id, out socketClient);
        }

        public override bool RemoveListen(TcpNetworkMonitor monitor)
        {
            if (monitor == null) throw new ArgumentNullException(nameof(monitor));

            if (_monitors.Remove(monitor))
            {
                monitor.Socket.SafeDispose();
                monitor.SocketAsyncEvent.SafeDispose();
                return true;
            }

            return false;
        }

        public override void ResetId(string oldId, string newId)
        {
            if (string.IsNullOrEmpty(oldId))
            {
                throw new ArgumentNullException(nameof(oldId));
            }
            if (string.IsNullOrEmpty(newId))
            {
                throw new ArgumentNullException(nameof(newId));
            }

            if (oldId == newId)
            {
                return;
            }

            if (_socketClients.TryGetSocketClient(oldId, out ISocketClient? client))
            {
                client!.ResetId(newId);
            }

            throw new InvalidOperationException("Client not found");
        }

        public override bool SocketClientExist(string id)
        {
            return SocketClients.SocketClientExist(id);
        }

        public override void Start()
        {
            try
            {
                var optionsList = new List<TcpListenOption>();

                Config.ListenOptions?.Invoke(optionsList);

                foreach (var ipHost in Config.ListenIpHosts)
                {
                    var option = new TcpListenOption(ipHost)
                    {
                        ServiceSslOption = Config.SslOption,
                        ReuseAddress = Config.ReuseAddress,
                        AdapterFactory = Config.DataHandlingAdapterFactory,
                        Backlog = Config.Backlog,
                        NoDelay = Config.NoDelay,
                        SendTimeout = Config.SendTimeout
                    };

                    optionsList.Add(option);
                }

                switch (_serverState)
                {
                    case ServerState.None:
                        BeginListen(optionsList);
                        break;
                    case ServerState.Running:
                        return;
                    case ServerState.Exception:
                        return;
                    case ServerState.Stopped:
                        BeginListen(optionsList);
                        break;
                    case ServerState.Disposed:
                        throw new ObjectDisposedException(GetType().Name);
                }

                _serverState = ServerState.Running;
            }
            catch
            {
                _serverState = ServerState.Exception;
                throw;
            }
        }

        private void BeginListen(List<TcpListenOption> optionList)
        {
            foreach (var option in optionList)
            {
                AddListen(option);
            }
        }

        public override void Stop()
        {
            foreach (var monitor in _monitors)
            {
                monitor.Socket.SafeDispose();
                monitor.SocketAsyncEvent.SafeDispose();
            }

            _monitors.Clear();

            Clear();

            _serverState = ServerState.Stopped;
        }

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
            {
                return;
            }

            if (disposing)
            {
                foreach (var monitor in _monitors)
                {
                    monitor.Socket.SafeDispose();
                    monitor.SocketAsyncEvent.SafeDispose();
                }
                _monitors.Clear();
                Clear();
                _serverState = ServerState.Disposed;
            }
            base.Dispose(disposing);
        }
    }

    public class TcpService : TcpService<SocketClient>
    {

    }
}