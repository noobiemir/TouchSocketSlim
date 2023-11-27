using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TouchSocketSlim.Core;

namespace TouchSocketSlim.Sockets
{
    public class TcpCore : SocketAsyncEventArgs
    {
        private const string RemoteCloseConnection = "Remote actively closes";

        public readonly object SyncRoot = new();

        public int MinBufferSize { get; set; } = 1024 * 10;

        public int MaxBufferSize { get; set; } = 1024 * 512;

        private long _bufferRate;
        private bool _isDisposed;
        private volatile bool _online;
        private int _receiveBufferSize = 1024 * 10;
        private int _sendBufferSize = 1024 * 10;
        private Socket? _socket;
        private readonly SemaphoreSlim _semaphoreForSend = new(1, 1);

        public TcpCore()
        {
            ReceiveCounter = new ValueCounter
            {
                Period = TimeSpan.FromSeconds(1),
                OnPeriod = OnReceivePeriod
            };

            SendCounter = new ValueCounter
            {
                Period = TimeSpan.FromSeconds(1),
                OnPeriod = OnSendPeriod
            };
        }

        ~TcpCore()
        {
            Dispose(false);
        }

        public Action<TcpCore, bool, string>? OnBreakOut { get; set; }

        public Action<TcpCore, Exception>? OnException { get; set; }

        public bool Online => _online;

        public Action<TcpCore, ByteBlock>? OnReceived { get; set; }

        public int ReceiveBufferSize => _receiveBufferSize;

        public ValueCounter ReceiveCounter { get; }

        public int SendBufferSize => _sendBufferSize;

        public ValueCounter SendCounter { get; }

        public Socket? Socket => _socket;

        public SslStream? SslStream { get; private set; }

        public bool UseSsl { get; private set; }

        public virtual void Authenticate(ServiceSslOption sslOption)
        {
            if (sslOption == null) throw new ArgumentNullException(nameof(sslOption));
            if (sslOption.Certificate == null)
            {
                throw new InvalidOperationException("Certificate is null");
            }
            if (_socket == null)
            {
                throw new InvalidOperationException("Socket is null");
            }

            var sslStream = sslOption.CertificateValidationCallback != null
                ? new SslStream(new NetworkStream(_socket, false), false, sslOption.CertificateValidationCallback)
                : new SslStream(new NetworkStream(_socket, false), false);
            sslStream.AuthenticateAsServer(sslOption.Certificate);

            SslStream = sslStream;
            UseSsl = true;
        }

        public virtual void Authenticate(ClientSslOption sslOption)
        {
            if (sslOption == null) throw new ArgumentNullException(nameof(sslOption));
            if (sslOption.ClientCertificates == null)
            {
                throw new InvalidOperationException("ClientCertificates is null");
            }
            if (sslOption.TargetHost == null)
            {
                throw new InvalidOperationException("TargetHost is null");
            }
            if (_socket == null)
            {
                throw new InvalidOperationException("Socket is null");
            }

            var sslStream = sslOption.CertificateValidationCallback != null
                ? new SslStream(new NetworkStream(_socket, false), false, sslOption.CertificateValidationCallback)
                : new SslStream(new NetworkStream(_socket, false), false);
            if (sslOption.ClientCertificates == null)
            {
                sslStream.AuthenticateAsClient(sslOption.TargetHost);
            }
            else
            {
                sslStream.AuthenticateAsClient(sslOption.TargetHost, sslOption.ClientCertificates, sslOption.SslProtocols, sslOption.CheckCertificateRevocation);
            }
            SslStream = sslStream;
            UseSsl = true;
        }

        public virtual async Task AuthenticateAsync(ServiceSslOption sslOption)
        {
            if (sslOption == null) throw new ArgumentNullException(nameof(sslOption));
            if (sslOption.Certificate == null)
            {
                throw new InvalidOperationException("Certificate is null");
            }
            if (_socket == null)
            {
                throw new InvalidOperationException("Socket is null");
            }

            var sslStream = sslOption.CertificateValidationCallback != null
                ? new SslStream(new NetworkStream(_socket, false), false, sslOption.CertificateValidationCallback)
                : new SslStream(new NetworkStream(_socket, false), false);
            await sslStream.AuthenticateAsServerAsync(sslOption.Certificate);

            SslStream = sslStream;
            UseSsl = true;
        }

        public virtual async Task AuthenticateAsync(ClientSslOption sslOption)
        {
            if (sslOption == null) throw new ArgumentNullException(nameof(sslOption));
            if (sslOption.ClientCertificates == null)
            {
                throw new InvalidOperationException("ClientCertificates is null");
            }
            if (sslOption.TargetHost == null)
            {
                throw new InvalidOperationException("TargetHost is null");
            }
            if (_socket == null)
            {
                throw new InvalidOperationException("Socket is null");
            }

            var sslStream = sslOption.CertificateValidationCallback != null
                ? new SslStream(new NetworkStream(_socket, false), false, sslOption.CertificateValidationCallback)
                : new SslStream(new NetworkStream(_socket, false), false);
            if (sslOption.ClientCertificates == null)
            {
                await sslStream.AuthenticateAsClientAsync(sslOption.TargetHost);
            }
            else
            {
                await sslStream.AuthenticateAsClientAsync(sslOption.TargetHost, sslOption.ClientCertificates, sslOption.SslProtocols, sslOption.CheckCertificateRevocation);
            }
            SslStream = sslStream;
            UseSsl = true;
        }

        public virtual void BeginIocpReceive()
        {
            if (_socket == null)
            {
                throw new InvalidOperationException("Socket is null");
            }
            var byteBlock = new ByteBlock(ReceiveBufferSize);
            UserToken = byteBlock;
            SetBuffer(byteBlock.Buffer, 0, byteBlock.Capacity);
            if (!_socket.ReceiveAsync(this))
            {
                _bufferRate += 2;
                ProcessReceived(this);
            }
        }

        public virtual async Task BeginSslReceiveAsync()
        {
            if (!UseSsl)
            {
                throw new InvalidOperationException("Please complete Ssl verification authorization first");
            }

            while (_online)
            {
                var byteBlock = new ByteBlock(ReceiveBufferSize);

                try
                {
                    var r = await Task<int>.Factory.FromAsync(SslStream!.BeginRead, SslStream!.EndRead, byteBlock.Buffer, 0, byteBlock.Capacity, default);
                    if (r == 0)
                    {
                        PrivateBreakOut(false, RemoteCloseConnection);
                        return;
                    }

                    byteBlock.SetLength(r);
                    HandleBuffer(byteBlock);
                }
                catch (Exception exception)
                {
                    await byteBlock.DisposeAsync();
                    PrivateBreakOut(false, exception.Message);
                }
            }
        }

        public virtual void Close(string msg)
        {
            PrivateBreakOut(true, msg);
        }

        public virtual void Reset(Socket socket)
        {
            if (socket == null) throw new ArgumentNullException(nameof(socket));

            if (!socket.Connected)
            {
                throw new InvalidOperationException("The new Socket must be connected.");
            }

            Reset();

            _online = true;
            _socket = socket;
        }

        public virtual void Reset()
        {
            ReceiveCounter.Reset();
            SendCounter.Reset();
            SslStream?.Dispose();
            SslStream = null;
            _socket = null;
            OnReceived = null;
            OnBreakOut = null;
            UserToken = null;
            _bufferRate = 1;
            _receiveBufferSize = MinBufferSize;
            _sendBufferSize = MinBufferSize;
            _online = false;
        }

        protected void ThrowIfNotConnected()
        {
            if (!_online)
            {
                throw new IOException();
            }
        }

        private void ProcessReceived(SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                PrivateBreakOut(false, e.SocketError.ToString());
                return;
            }
            if (e.BytesTransferred > 0)
            {
                ByteBlock byteBlock = (ByteBlock)e.UserToken!;
                byteBlock.SetLength(e.BytesTransferred);
                HandleBuffer(byteBlock);
                try
                {
                    var newByteBlock = new ByteBlock((int)Math.Min(ReceiveBufferSize * _bufferRate, MaxBufferSize));
                    e.UserToken = newByteBlock;
                    e.SetBuffer(newByteBlock.Buffer, 0, newByteBlock.Capacity);

                    if (!_socket!.ReceiveAsync(e))
                    {
                        _bufferRate *= 2;
                        ProcessReceived(e);
                    }
                }
                catch (Exception exception)
                {
                    PrivateBreakOut(false, exception.ToString());
                }
            }
            else
            {
                PrivateBreakOut(false, RemoteCloseConnection);
            }
        }

        private void PrivateBreakOut(bool manual, string msg)
        {
            lock (SyncRoot)
            {
                if (_online)
                {
                    _online = false;
                    BreakOut(manual, msg);
                }
            }
        }

        private void HandleBuffer(ByteBlock byteBlock)
        {
            try
            {
                ReceiveCounter.Increment(byteBlock.Length);
                Received(byteBlock);
            }
            catch (Exception e)
            {
                Exception(e);
            }
            finally
            {
                byteBlock.Dispose();
            }
        }

        protected virtual void BreakOut(bool manual, string msg)
        {
            OnBreakOut?.Invoke(this, manual, msg);
        }

        protected virtual void Received(ByteBlock byteBlock)
        {
            OnReceived?.Invoke(this, byteBlock);
        }

        protected virtual void Exception(Exception ex)
        {
            OnException?.Invoke(this, ex);
        }

        private void OnReceivePeriod(long value)
        {
            _receiveBufferSize = Math.Max(TouchSocketUtility.HitBufferLength(value), MinBufferSize);
            if (_socket != null)
            {
                _socket.ReceiveBufferSize = _receiveBufferSize;
            }
        }

        private void OnSendPeriod(long value)
        {
            _sendBufferSize = Math.Max(TouchSocketUtility.HitBufferLength(value), MinBufferSize);
            if (_socket != null)
            {
                _socket.SendBufferSize = _sendBufferSize;
            }
        }

        protected sealed override void OnCompleted(SocketAsyncEventArgs e)
        {
            if (e.LastOperation == SocketAsyncOperation.Receive)
            {
                try
                {
                    _bufferRate = 1;
                    ProcessReceived(e);
                }
                catch (Exception exception)
                {
                    PrivateBreakOut(false, exception.Message);
                }
            }
        }

        public virtual void Send(byte[] buffer, int offset, int length)
        {
            ThrowIfNotConnected();

            try
            {
                var originLength = length;
                _semaphoreForSend.Wait();
                if (UseSsl)
                {
                    SslStream!.Write(buffer, offset, length);
                }
                else
                {
                    while (length > 0)
                    {
                        var r = _socket!.Send(buffer, offset, length, SocketFlags.None);
                        if (r == 0)
                        {
                            throw new IOException("Incomplete data sent");
                        }

                        offset += r;
                        length -= r;
                    }
                }

                SendCounter.Increment(originLength);
            }
            finally
            {
                _semaphoreForSend.Release();
            }
        }

        public virtual async Task SendAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken = default)
        {
            ThrowIfNotConnected();

            try
            {
                var originLength = length;
                await _semaphoreForSend.WaitAsync(cancellationToken);

                if (UseSsl)
                {
                    await SslStream!.WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, length), cancellationToken);
                }
                else
                {
                    while (length > 0)
                    {
                        var r = await _socket!.SendAsync(new ReadOnlyMemory<byte>(buffer, offset, length), SocketFlags.None, cancellationToken);
                        if (r == 0)
                        {
                            throw new IOException("Incomplete data sent");
                        }
                        offset += r;
                        length -= r;
                    }
                }
                SendCounter.Increment(originLength);
            }
            finally
            {
                _semaphoreForSend.Release();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _socket?.Dispose();
                }
                _isDisposed = true;
            }
            base.Dispose();
        }

        public new void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
