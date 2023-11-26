using System.Buffers;
using TouchSocketSlim.Core;

namespace TouchSocketSlim.Sockets;

public interface IIdSender
{

    void Send(string id, byte[] buffer, int offset, int length);

    Task SendAsync(string id, byte[] buffer, int offset, int length);

    void Send(string id, IRequestInfo requestInfo);

    Task SendAsync(string id, IRequestInfo requestInfo);

    void Send(string id, IList<ArraySegment<byte>> transferBytes);

    Task SendAsync(string id, IList<ArraySegment<byte>> transferBytes);

    void Send(string id, ReadOnlySequence<byte> transferBytes);

    Task SendAsync(string id, ReadOnlySequence<byte> transferBytes);
}