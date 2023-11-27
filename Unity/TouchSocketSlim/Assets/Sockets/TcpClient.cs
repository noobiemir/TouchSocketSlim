//------------------------------------------------------------------------------
//  此代码版权（除特别声明或在XREF结尾的命名空间的代码）归作者本人若汝棋茗所有
//  源代码使用协议遵循本仓库的开源协议及附加协议，若本仓库没有设置，则按MIT开源协议授权
//  CSDN博客：https://blog.csdn.net/qq_40374647
//  哔哩哔哩视频：https://space.bilibili.com/94253567
//  Gitee源代码仓库：https://gitee.com/RRQM_Home
//  Github源代码仓库：https://github.com/RRQM
//  API首页：http://rrqm_home.gitee.io/touchsocket/
//  交流QQ群：234762506
//  感谢您的下载和使用
//------------------------------------------------------------------------------
//------------------------------------------------------------------------------

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TouchSocketSlim.Core;

namespace TouchSocketSlim.Sockets
{
    [System.Diagnostics.DebuggerDisplay("{Ip}:{Port}")]
    public class TcpClient : DisposableObject, ITcpClient
    {
        private readonly TcpClientConfig _config;
        private volatile bool _online;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly InternalTcpCore _tcpCore = new();

        public TcpClient(IpHost remoteIpHost, Func<SingleStreamDataHandlingAdapter> dataHandlingAdapterFactory) : this(new TcpClientConfig(remoteIpHost, dataHandlingAdapterFactory))
        {
        }

        public TcpClient(IpHost remoteIpHost) : this(new TcpClientConfig(remoteIpHost))
        {
        }

        public TcpClient(TcpClientConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public ConnectedEventHandler<ITcpClient>? Connected { get; set; }

        public ConnectingEventHandler<ITcpClient>? Connecting { get; set; }

        public DisconnectEventHandler<ITcpClientBase>? Disconnected { get; set; }

        public DisconnectEventHandler<ITcpClientBase>? Disconnecting { get; set; }

        public ReceivedEventHandler<TcpClient>? Received { get; set; }

        public virtual bool CanSetDataHandlingAdapter => true;

        public DateTime LastReceivedTime => GetTcpCore().ReceiveCounter.LastIncrement;

        public DateTime LastSendTime => GetTcpCore().SendCounter.LastIncrement;

        public string? Ip { get; private set; }

        public Socket? MainSocket { get; private set; }

        public SingleStreamDataHandlingAdapter? DataHandlingAdapter { get; private set; }

        public bool Online => _online;

        public bool CanSend => _online;

        public int Port { get; private set; }

        public bool UseSsl => GetTcpCore().UseSsl;

        public IpHost RemoteIpHost => _config.RemoteIpHost;

        public bool IsClient => true;

        private void PrivateOnConnected(ConnectedEventArgs e)
        {
            OnConnected(e);
        }

        protected virtual void OnConnected(ConnectedEventArgs e)
        {
            Connected?.Invoke(this, e);
        }

        private void PrivateOnConnecting(ConnectingEventArgs e)
        {
            if (CanSetDataHandlingAdapter)
            {
                SetDataHandlingAdapter(_config.DataHandlingAdapterFactory.Invoke());
            }
            OnConnecting(e);
        }

        public virtual void SetDataHandlingAdapter(SingleStreamDataHandlingAdapter adapter)
        {
            if (!CanSetDataHandlingAdapter)
            {
                throw new InvalidOperationException("Set adapter is not allow");
            }
            SetAdapter(adapter);
        }

        protected void SetAdapter(SingleStreamDataHandlingAdapter adapter)
        {
            ThrowIfDisposed();
            if (adapter == null) throw new ArgumentNullException(nameof(adapter));

            adapter.OnLoaded(this);
            adapter.ReceivedCallBack = PrivateHandleReceivedData;
            adapter.SendCallBack = DefaultSend;
            adapter.SendAsyncCallBack = DefaultSendAsync;
            DataHandlingAdapter = adapter;
        }

        private void PrivateHandleReceivedData(ByteBlock byteBlock)
        {
            ReceivedData(byteBlock.Buffer, 0, (int)byteBlock.Length);
        }

        public void DefaultSend(byte[] buffer, int offset, int length)
        {
            GetTcpCore().Send(buffer, offset, length);
        }

        public Task DefaultSendAsync(byte[] buffer, int offset, int length)
        {
            return GetTcpCore().SendAsync(buffer, offset, length);
        }

        private TcpCore GetTcpCore()
        {
            ThrowIfDisposed();
            return _tcpCore ?? throw new ObjectDisposedException(GetType().Name);
        }

        protected virtual void ReceivedData(byte[] buffer, int offset, int length)
        {
            Received?.Invoke(this, buffer, offset, length);
        }

        protected virtual void OnConnecting(ConnectingEventArgs e)
        {
            Connecting?.Invoke(this, e);
        }

        protected virtual void OnDisconnected(DisconnectEventArgs e)
        {
            Disconnected?.Invoke(this, e);
        }

        private void PrivateOnDisconnecting(DisconnectEventArgs e)
        {
            OnDisconnecting(e);
        }

        protected virtual void OnDisconnecting(DisconnectEventArgs e)
        {
            Disconnecting?.Invoke(this, e);
        }

        public virtual void Close(string message = "")
        {
            lock (GetTcpCore())
            {
                if (_online)
                {
                    PrivateOnDisconnecting(new DisconnectEventArgs(true, message));
                    MainSocket?.TryClose();
                    BreakOut(true, message);
                }
            }
        }

        protected void BreakOut(bool manual, string message)
        {
            lock (GetTcpCore())
            {
                if (_online)
                {
                    _online = false;
                    MainSocket.SafeDispose();
                    DataHandlingAdapter.SafeDispose();
                    OnDisconnected(new DisconnectEventArgs(manual, message));
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            lock (GetTcpCore())
            {
                if (_online)
                {
                    PrivateOnDisconnecting(new DisconnectEventArgs(true, $"{nameof(Dispose)} disconnected"));
                    BreakOut(true, $"{nameof(Dispose)} disconnected");
                }
            }
            base.Dispose(disposing);
        }

        protected void TcpConnect(int timeout, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            try
            {
                _semaphore.Wait(cancellationToken);
                if (_online)
                {
                    return;
                }

                MainSocket.SafeDispose();
                var socket = CreateSocket();
                PrivateOnConnecting(new ConnectingEventArgs(socket));
                var task = Task.Run(() => { socket.Connect(_config.RemoteIpHost.Host, _config.RemoteIpHost.Port); }, cancellationToken);
                task.ConfigureAwait(false);
                if (!task.Wait(timeout, cancellationToken))
                {
                    socket.SafeDispose();
                    throw new TimeoutException();
                }

                _online = true;
                SetSocket(socket);
                BeginReceive();

                PrivateOnConnected(new ConnectedEventArgs());
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void BeginReceive()
        {
            if (_config.SslOption != null)
            {
                GetTcpCore().Authenticate(_config.SslOption);
                _ = GetTcpCore().BeginSslReceiveAsync();
            }
            else
            {
                GetTcpCore().BeginIocpReceive();
            }
        }

        private void SetSocket(Socket? socket)
        {
            if (socket == null)
            {
                Ip = null;
                Port = -1;
                return;
            }

            Ip = socket.RemoteEndPoint!.GetIp();
            Port = socket.RemoteEndPoint!.GetPort();
            MainSocket = socket;

            _tcpCore.Reset(socket);
            _tcpCore.OnReceived = HandleReceived;
            _tcpCore.OnBreakOut = TcpCoreBreakOut;

            if (_config.MinBufferSize.HasValue)
            {
                _tcpCore.MinBufferSize = _config.MinBufferSize.Value;
            }

            if (_config.MaxBufferSize.HasValue)
            {
                _tcpCore.MaxBufferSize = _config.MaxBufferSize.Value;
            }
        }

        private void HandleReceived(TcpCore core, ByteBlock byteBlock)
        {
            try
            {
                if (IsDisposed)
                {
                    return;
                }

                if (DataHandlingAdapter == null)
                {
                    return;
                }

                DataHandlingAdapter.ReceivedInput(byteBlock);
            }
            catch
            {
                // ignore
            }
        }

        private void TcpCoreBreakOut(TcpCore core, bool manual, string msg)
        {
            BreakOut(manual, msg);
        }

        private Socket CreateSocket()
        {
            Socket socket;
            if (_config.RemoteIpHost.HostNameType == UriHostNameType.Dns)
            {
                socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
                {
                    SendTimeout = _config.SendTimeout
                };
            }
            else
            {
                socket = new Socket(_config.RemoteIpHost.EndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    SendTimeout = _config.SendTimeout
                };
            }

            if (_config.KeepAlive != null)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    socket.IOControl(IOControlCode.KeepAliveValues, _config.KeepAlive.GetKeepAliveTime(), null);
                }
            }

            if (_config.NoDelay.HasValue)
            {
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, _config.NoDelay.Value);
            }

            return socket;
        }

#if NET6_0_OR_GREATER
        protected async Task TcpConnectAsync(int timeout, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            try
            {
                await _semaphore.WaitAsync(cancellationToken);
                if (_online)
                {
                    return;
                }
                MainSocket.SafeDispose();
                var socket = CreateSocket();
                PrivateOnConnecting(new ConnectingEventArgs(socket));
                if (cancellationToken.CanBeCanceled)
                {
                    await socket.ConnectAsync(_config.RemoteIpHost.Host, _config.RemoteIpHost.Port, cancellationToken);
                }
                else
                {
                    using var tokenSource = new CancellationTokenSource(timeout);
                    try
                    {
                        await socket.ConnectAsync(_config.RemoteIpHost.Host, _config.RemoteIpHost.Port, tokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        throw new TimeoutException();
                    }
                }

                _online = true;
                SetSocket(socket);
                BeginReceive();

                PrivateOnConnected(new ConnectedEventArgs());
            }
            finally
            {
                _semaphore.Release();
            }
        }
#else
    protected async Task TcpConnectAsync(int timeout, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            if (_online)
            {
                return;
            }
            MainSocket.SafeDispose();
            var socket = CreateSocket();
            PrivateOnConnecting(new ConnectingEventArgs(socket));

            var task = Task.Factory.FromAsync(socket.BeginConnect, socket.EndConnect, _config.RemoteIpHost.Host, _config.RemoteIpHost.Port, null);
            await task.WaitAsync(TimeSpan.FromMilliseconds(timeout));

            _online = true;
            SetSocket(socket);
            BeginReceive();

            PrivateOnConnected(new ConnectedEventArgs());
        }
        finally
        {
            _semaphore.Release();
        }
    }
#endif

        public virtual void Connect(int timeout, CancellationToken cancellationToken = default)
        {
            TcpConnect(timeout, cancellationToken);
        }

        public virtual Task ConnectAsync(int timeout, CancellationToken cancellationToken = default)
        {
            return TcpConnectAsync(timeout, cancellationToken);
        }

        public virtual void Send(byte[] buffer, int offset, int length)
        {
            ThrowIfDisposed();
            if (DataHandlingAdapter == null)
            {
                throw new InvalidOperationException($"{nameof(DataHandlingAdapter)} is null");
            }

            DataHandlingAdapter.SendInput(buffer, offset, length);
        }

        public virtual void Send(IList<ArraySegment<byte>> transferBytes)
        {
            ThrowIfDisposed();
            if (DataHandlingAdapter == null)
            {
                throw new InvalidOperationException($"{nameof(DataHandlingAdapter)} is null");
            }

            if (DataHandlingAdapter.CanSplicingSend)
            {
                DataHandlingAdapter.SendInput(transferBytes);
            }
            else
            {
                var length = transferBytes.Sum(i => i.Count);
                using var byteBlock = new ByteBlock(length);
                foreach (var item in transferBytes)
                {
                    byteBlock.Write(item);
                }
                DataHandlingAdapter.SendInput(byteBlock.Buffer, 0, (int)byteBlock.Length);
            }
        }

        public virtual void Send(ReadOnlySequence<byte> sequence)
        {
            ThrowIfDisposed();
            if (DataHandlingAdapter == null)
            {
                throw new InvalidOperationException($"{nameof(DataHandlingAdapter)} is null");
            }

            if (DataHandlingAdapter.CanSendReadOnlySequence)
            {
                DataHandlingAdapter.SendInput(sequence);
            }
            else
            {
                using var byteBlock = new ByteBlock((int)sequence.Length);
                byteBlock.Write(sequence);
                DataHandlingAdapter.SendInput(byteBlock.Buffer, 0, (int)byteBlock.Length);
            }
        }

        public virtual Task SendAsync(byte[] buffer, int offset, int length)
        {
            ThrowIfDisposed();

            if (DataHandlingAdapter == null)
            {
                throw new InvalidOperationException($"{nameof(DataHandlingAdapter)} is null");
            }

            return DataHandlingAdapter.SendInputAsync(buffer, offset, length);
        }

        public virtual async Task SendAsync(IList<ArraySegment<byte>> transferBytes)
        {
            ThrowIfDisposed();
            if (DataHandlingAdapter == null)
            {
                throw new InvalidOperationException($"{nameof(DataHandlingAdapter)} is null");
            }

            if (DataHandlingAdapter.CanSplicingSend)
            {
                await DataHandlingAdapter.SendInputAsync(transferBytes);
            }
            else
            {
                var length = transferBytes.Sum(i => i.Count);
                await using var byteBlock = new ByteBlock(length);
                foreach (var item in transferBytes)
                {
                    byteBlock.Write(item);
                }
                await DataHandlingAdapter.SendInputAsync(byteBlock.Buffer, 0, (int)byteBlock.Length);
            }
        }

        public virtual async Task SendAsync(ReadOnlySequence<byte> sequence)
        {
            ThrowIfDisposed();
            if (DataHandlingAdapter == null)
            {
                throw new InvalidOperationException($"{nameof(DataHandlingAdapter)} is null");
            }

            if (DataHandlingAdapter.CanSendReadOnlySequence)
            {
                await DataHandlingAdapter.SendInputAsync(sequence);
            }
            else
            {
                await using var byteBlock = new ByteBlock((int)sequence.Length);
                byteBlock.Write(sequence);
                await DataHandlingAdapter.SendInputAsync(byteBlock.Buffer, 0, (int)byteBlock.Length);
            }
        }
    }
}
