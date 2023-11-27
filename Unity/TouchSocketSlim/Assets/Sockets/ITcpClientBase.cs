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
using System.Net.Sockets;
using System.Threading.Tasks;
using TouchSocketSlim.Core;

namespace TouchSocketSlim.Sockets
{
    public interface ITcpClientBase : IClient, IDisposable
    {
        bool CanSetDataHandlingAdapter { get; }

        SingleStreamDataHandlingAdapter? DataHandlingAdapter { get; }

        DisconnectEventHandler<ITcpClientBase>? Disconnected { get; set; }

        DisconnectEventHandler<ITcpClientBase>? Disconnecting { get; set; }

        string? Ip { get; }

        bool IsClient { get; }

        Socket? MainSocket { get; }

        int Port { get; }

        bool UseSsl { get; }

        void Close(string message = "");

        void SetDataHandlingAdapter(SingleStreamDataHandlingAdapter adapter);

        bool CanSend { get; }

        void Send(byte[] buffer, int offset, int length);

        Task SendAsync(byte[] buffer, int offset, int length);

        void DefaultSend(byte[] buffer, int offset, int length);

        Task DefaultSendAsync(byte[] buffer, int offset, int length);

        void Send(IList<ArraySegment<byte>> transferBytes);

        Task SendAsync(IList<ArraySegment<byte>> transferBytes);

        void Send(ReadOnlySequence<byte> transferBytes);

        Task SendAsync(ReadOnlySequence<byte> transferBytes);

        bool Online { get; }
    }
}
