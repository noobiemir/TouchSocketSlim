using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using TouchSocketSlim.Core;

namespace TouchSocketSlim.Sockets
{
    [DebuggerDisplay("Id={Id},IpAddress={Ip}:{Port}")]
    public class SocketClient : DisposableObject, ISocketClient
    {
        private TcpCore? _tcpCore;

        public DateTime LastReceivedTime => GetTcpCore().ReceiveCounter.LastIncrement;

        public DateTime LastSendTime => GetTcpCore().SendCounter.LastIncrement;

        public bool CanSetDataHandlingAdapter => true;

        public SingleStreamDataHandlingAdapter? DataHandlingAdapter { get; private set; }

        public DisconnectEventHandler<ITcpClientBase>? Disconnected { get; set; }

        public DisconnectEventHandler<ITcpClientBase>? Disconnecting { get; set; }

        public string? Ip { get; private set; }

        public bool IsClient => false;

        public Socket? MainSocket { get; private set; }

        public int Port { get; private set; }

        public bool UseSsl => GetTcpCore().UseSsl;

        public bool CanSend => Online;

        public bool Online { get; private set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public string Id { get; internal set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public TcpServiceBase? Service { get; internal set; }

        public string? ServiceIp { get; private set; }

        public int? ServicePort { get; private set; }

        public TcpListenOption? ListenOption { get; internal set; }

        internal Task AuthenticateAsync(ServiceSslOption sslOption)
        {
            if (_tcpCore == null) throw new InvalidOperationException("tcp core is null");
            return _tcpCore.AuthenticateAsync(sslOption);
        }

        internal void BeginReceive()
        {
            if (_tcpCore == null) throw new InvalidOperationException("tcp core is null");
            try
            {
                _tcpCore.BeginIocpReceive();
            }
            catch (Exception ex)
            {
                BreakOut(false, ex.Message);
            }
        }

        internal Task BeginReceiveSsl()
        {
            if (_tcpCore == null) throw new InvalidOperationException("tcp core is null");
            return _tcpCore.BeginSslReceiveAsync();
        }

        internal void SetSocket(Socket socket)
        {
            if (Service == null)
            {
                throw new InvalidOperationException("Service has not been set up yet");
            }

            MainSocket = socket ?? throw new ArgumentNullException(nameof(socket));
            Ip = socket.RemoteEndPoint!.GetIp();
            Port = socket.RemoteEndPoint!.GetPort();
            ServiceIp = socket.LocalEndPoint!.GetIp();
            ServicePort = socket.LocalEndPoint!.GetPort();

            var tcpCore = Service.RentTcpCore();
            tcpCore.Reset(socket);
            tcpCore.OnReceived = HandleReceived;
            tcpCore.OnBreakOut = TcpCoreBreakOut;

            _tcpCore = tcpCore;
        }

        protected void BreakOut(bool manual, string message)
        {
            if (!GetSocketClientCollection().TryRemove(Id, out _)) return;
            if (!Online) return;
            Online = false;
            MainSocket.SafeDispose();
            DataHandlingAdapter.SafeDispose();
            OnDisconnected(new DisconnectEventArgs(manual, message));
        }

        private SocketClientCollection GetSocketClientCollection()
        {
            return (SocketClientCollection)Service!.SocketClients;
        }

        private void HandleReceived(TcpCore core, ByteBlock byteBlock)
        {
            try
            {
                if (IsDisposed) return;

                if (DataHandlingAdapter == null) return;

                DataHandlingAdapter.ReceivedInput(byteBlock);
            }
            catch
            {
                // ignore
            }
        }

        private void TcpCoreBreakOut(TcpCore core, bool manual, string message)
        {
            BreakOut(manual, message);
        }

        internal virtual void OnConnected(ConnectedEventArgs e)
        {
            Online = true;
            Service?.OnClientConnected(this, e);
        }

        internal virtual void OnConnecting(ConnectingEventArgs e)
        {
            Service?.OnClientConnecting(this, e);
        }

        protected virtual void OnDisconnected(DisconnectEventArgs e)
        {
            try
            {
                Disconnected?.Invoke(this, e);

                if (!e.Handled)
                {
                    Service?.OnClientDisconnected(this, e);
                }
            }
            finally
            {
                var tcp = _tcpCore;
                _tcpCore = null;
                Service?.ReturnTcpCore(tcp!);
                base.Dispose(true);
            }
        }

        protected virtual void OnDisconnecting(DisconnectEventArgs e)
        {
            Disconnecting?.Invoke(this, e);
            if (e.Handled)
            {
                return;
            }

            Service?.OnClientDisconnecting(this, e);
        }

        internal virtual void OnInitialized()
        {

        }

        public virtual void Close(string message = "")
        {
            lock (GetTcpCore())
            {
                if (Online)
                {
                    OnDisconnecting(new DisconnectEventArgs(true, message));
                    MainSocket?.TryClose();
                    BreakOut(true, message);
                }
            }
        }

        public void SetDataHandlingAdapter(SingleStreamDataHandlingAdapter adapter)
        {
            ThrowIfDisposed();

            if (!CanSetDataHandlingAdapter)
            {
                throw new InvalidOperationException("Set adapter is not allow");
            }

            SetAdapter(adapter);
        }

        public void Send(byte[] buffer, int offset, int length)
        {
            ThrowIfDisposed();
            if (DataHandlingAdapter == null)
            {
                throw new InvalidOperationException("Null Adapter");
            }
            DataHandlingAdapter.SendInput(buffer, offset, length);
        }

        public Task SendAsync(byte[] buffer, int offset, int length)
        {
            ThrowIfDisposed();
            if (DataHandlingAdapter == null)
            {
                throw new InvalidOperationException("Null Adapter");
            }
            return DataHandlingAdapter.SendInputAsync(buffer, offset, length);
        }

        public void DefaultSend(byte[] buffer, int offset, int length)
        {
            GetTcpCore().Send(buffer, offset, length);
        }

        public Task DefaultSendAsync(byte[] buffer, int offset, int length)
        {
            return GetTcpCore().SendAsync(buffer, offset, length);
        }

        public void Send(IList<ArraySegment<byte>> transferBytes)
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

        public async Task SendAsync(IList<ArraySegment<byte>> transferBytes)
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

        public void Send(ReadOnlySequence<byte> transferBytes)
        {
            ThrowIfDisposed();
            if (DataHandlingAdapter == null)
            {
                throw new InvalidOperationException($"{nameof(DataHandlingAdapter)} is null");
            }

            if (DataHandlingAdapter.CanSendReadOnlySequence)
            {
                DataHandlingAdapter.SendInput(transferBytes);
            }
            else
            {
                using var byteBlock = new ByteBlock((int)transferBytes.Length);
                byteBlock.Write(transferBytes);
                DataHandlingAdapter.SendInput(byteBlock.Buffer, 0, (int)byteBlock.Length);
            }
        }

        public async Task SendAsync(ReadOnlySequence<byte> transferBytes)
        {
            ThrowIfDisposed();
            if (DataHandlingAdapter == null)
            {
                throw new InvalidOperationException($"{nameof(DataHandlingAdapter)} is null");
            }

            if (DataHandlingAdapter.CanSendReadOnlySequence)
            {
                await DataHandlingAdapter.SendInputAsync(transferBytes);
            }
            else
            {
                await using var byteBlock = new ByteBlock((int)transferBytes.Length);
                byteBlock.Write(transferBytes);
                await DataHandlingAdapter.SendInputAsync(byteBlock.Buffer, 0, (int)byteBlock.Length);
            }
        }

        public void Send(string id, byte[] buffer, int offset, int length)
        {
            Service?.Send(id, buffer, offset, length);
        }

        public Task SendAsync(string id, byte[] buffer, int offset, int length)
        {
            return Service?.SendAsync(id, buffer, offset, length) ?? Task.CompletedTask;
        }

        public void Send(string id, IList<ArraySegment<byte>> transferBytes)
        {
            Service?.Send(id, transferBytes);
        }

        public Task SendAsync(string id, IList<ArraySegment<byte>> transferBytes)
        {
            return Service?.SendAsync(id, transferBytes) ?? Task.CompletedTask;
        }

        public void Send(string id, ReadOnlySequence<byte> transferBytes)
        {
            Service?.Send(id, transferBytes);
        }

        public Task SendAsync(string id, ReadOnlySequence<byte> transferBytes)
        {
            return Service?.SendAsync(id, transferBytes) ?? Task.CompletedTask;
        }

        public virtual void ResetId(string newId)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(newId))
            {
                throw new ArgumentNullException(nameof(newId));
            }

            if (Id == newId)
            {
                return;
            }

            var oldId = Id;
            if (GetSocketClientCollection().TryRemove(Id, out SocketClient? socketClient))
            {
                socketClient!.Id = newId;
                if (!GetSocketClientCollection().TryAdd(socketClient))
                {
                    socketClient.Id = oldId;
                    if (GetSocketClientCollection().TryAdd(socketClient))
                    {
                        throw new InvalidOperationException("Duplicate Id");
                    }

                    socketClient.Close("The operation failed when modifying the new Id, and also failed when rolling back the old Id.");
                }
            }
            else
            {
                throw new InvalidOperationException("Client not found.");
            }
        }

        protected override void Dispose(bool disposing)
        {
            lock (GetTcpCore())
            {
                if (Online)
                {
                    OnDisconnecting(new DisconnectEventArgs(true, "Dispose"));
                    BreakOut(true, "Dispose");
                }
            }
        }

        protected void SetAdapter(SingleStreamDataHandlingAdapter adapter)
        {
            if (adapter == null) throw new ArgumentNullException(nameof(adapter));

            adapter.OnLoaded(this);
            adapter.ReceivedCallBack = HandleReceivedData;
            adapter.SendCallBack = DefaultSend;
            adapter.SendAsyncCallBack = DefaultSendAsync;
            DataHandlingAdapter = adapter;
        }

        private void HandleReceivedData(ByteBlock byteBlock)
        {
            Service?.OnClientReceivedData(this, byteBlock.Buffer, 0, (int)byteBlock.Length);
        }

        private TcpCore GetTcpCore()
        {
            ThrowIfDisposed();
            return _tcpCore ?? throw new ObjectDisposedException(GetType().Name);
        }
    }
}
