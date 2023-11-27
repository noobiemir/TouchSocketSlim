using System;
using System.Runtime.InteropServices;

namespace TouchSocketSlim.Sockets
{
    public class KeepAliveValue
    {
        public byte[] GetKeepAliveTime()
        {
            var uintSize = Marshal.SizeOf<uint>();
            var buffer = new byte[uintSize * 3];
            var span = buffer.AsSpan();
            uint value = 0;
            MemoryMarshal.Write(span[..uintSize],
#if !NET8_0_OR_GREATER
                ref
#endif
                value);
            value = Interval;
            MemoryMarshal.Write(span.Slice(uintSize, uintSize),
#if !NET8_0_OR_GREATER
                ref
#endif
                value);
            value = AckInterval;
            MemoryMarshal.Write(span.Slice(uintSize * 2, uintSize),
#if !NET8_0_OR_GREATER
                ref
#endif
                value);

            return buffer;
        }

        public uint Interval { get; set; } = 20 * 1000;

        public uint AckInterval { get; set; } = 2 * 1000;
    }
}
