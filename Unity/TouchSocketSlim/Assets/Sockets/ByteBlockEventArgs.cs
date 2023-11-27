using System;
using TouchSocketSlim.Core;

namespace TouchSocketSlim.Sockets
{
    public class ByteBlockEventArgs : TouchSocketEventArgs
    {
        public ByteBlockEventArgs(ByteBlock byteBlock)
        {
            ByteBlock = byteBlock ?? throw new ArgumentNullException(nameof(byteBlock));
        }

        public ByteBlock ByteBlock { get; }
    }
}