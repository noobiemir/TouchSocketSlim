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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using TouchSocketSlim.Core;

namespace TouchSocketSlim.Sockets
{
    public abstract class TcpServiceBase : DisposableObject, IIdSender
    {
        protected readonly TcpServiceConfig Config;

        private readonly ConcurrentStack<TcpCore> _tcpCores = new();

        protected TcpServiceBase(TcpServiceConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public abstract ISocketClientCollection SocketClients { get; }

        public int Count => SocketClients.Count;

        public abstract int MaxCount { get; }

        public abstract IEnumerable<TcpNetworkMonitor> Monitors { get; }

        public abstract ServerState ServerState { get; }

        public abstract void AddListen(TcpListenOption options);

        public abstract void Clear();

        public IEnumerable<string> GetIds()
        {
            return SocketClients.GetIds();
        }

        public abstract bool RemoveListen(TcpNetworkMonitor monitor);

        public TcpCore RentTcpCore()
        {
            if (_tcpCores.TryPop(out var tcpCore))
            {
                return tcpCore;
            }

            tcpCore = new InternalTcpCore();
            if (Config.MinBufferSize.HasValue)
            {
                tcpCore.MinBufferSize = Config.MinBufferSize.Value;
            }

            if (Config.MaxBufferSize.HasValue)
            {
                tcpCore.MaxBufferSize = Config.MaxBufferSize.Value;
            }
            return tcpCore;
        }

        public abstract void ResetId(string oldId, string newId);

        public void ReturnTcpCore(TcpCore tcpCore)
        {
            if (IsDisposed)
            {
                tcpCore.SafeDispose();
                return;
            }
            _tcpCores.Push(tcpCore);
        }

        public abstract bool SocketClientExist(string id);

        public abstract void Start();

        public abstract void Stop();

        internal abstract void OnClientConnected(SocketClient socketClient, ConnectedEventArgs e);

        internal abstract void OnClientConnecting(SocketClient socketClient, ConnectingEventArgs e);

        internal abstract void OnClientDisconnected(SocketClient socketClient, DisconnectEventArgs e);

        internal abstract void OnClientDisconnecting(SocketClient socketClient, DisconnectEventArgs e);

        internal abstract void OnClientReceivedData(SocketClient socketClient, byte[] buffer, int offset, int length);

        protected override void Dispose(bool disposing)
        {
            while (_tcpCores.TryPop(out var tcpCore))
            {
                tcpCore.SafeDispose();
            }
            base.Dispose(disposing);
        }

        public void Send(string id, byte[] buffer, int offset, int length)
        {
            if (!SocketClients.TryGetSocketClient(id, out var client)) throw new InvalidOperationException($"Client not found :{id}");
            client!.Send(buffer, offset, length);
        }

        public Task SendAsync(string id, byte[] buffer, int offset, int length)
        {
            if (SocketClients.TryGetSocketClient(id, out var client))
            {
                return client!.SendAsync(buffer, offset, length);
            }

            throw new InvalidOperationException($"Client not found :{id}");
        }

        public void Send(string id, IList<ArraySegment<byte>> transferBytes)
        {
            if (!SocketClients.TryGetSocketClient(id, out var client)) throw new InvalidOperationException($"Client not found :{id}");
            client!.Send(transferBytes);
        }

        public Task SendAsync(string id, IList<ArraySegment<byte>> transferBytes)
        {
            if (SocketClients.TryGetSocketClient(id, out var client))
            {
                return client!.SendAsync(transferBytes);
            }

            throw new InvalidOperationException($"Client not found :{id}");
        }

        public void Send(string id, ReadOnlySequence<byte> transferBytes)
        {
            if (!SocketClients.TryGetSocketClient(id, out var client)) throw new InvalidOperationException($"Client not found :{id}");
            client!.Send(transferBytes);
        }

        public Task SendAsync(string id, ReadOnlySequence<byte> transferBytes)
        {
            if (SocketClients.TryGetSocketClient(id, out var client))
            {
                return client!.SendAsync(transferBytes);
            }

            throw new InvalidOperationException($"Client not found :{id}");
        }
    }
}
