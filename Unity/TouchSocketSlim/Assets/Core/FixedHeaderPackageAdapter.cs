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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TouchSocketSlim.Core
{
    public class FixedHeaderPackageAdapter : SingleStreamDataHandlingAdapter
    {
        private ReadOnlyMemory<byte>? _agreementTempBytes;
        private int _surPlusLength;

        private ByteBlock? _tempByteBlock;

        public override bool CanSendRequestInfo => false;

        public override bool CanSendReadOnlySequence => true;

        public override bool CanSplicingSend => true;

        public FixedHeaderType FixedHeaderType { get; set; } = FixedHeaderType.Int;

        public int MinPackageSize { get; set; } = 0;

        protected override void PreviewReceived(ByteBlock byteBlock)
        {
            var buffer = byteBlock.Buffer;
            var length = (int)byteBlock.Length;

            if (CacheTimeoutEnable && DateTime.Now - LastCacheTime > CacheTimeout)
            {
                Reset();
            }

            if (_agreementTempBytes.HasValue)
            {
                SeamPackage(buffer, length);
            }
            else if (_tempByteBlock == null)
            {
                SplitPackage(buffer, 0, length);
            }
            else
            {
                if (_surPlusLength == length)
                {
                    _tempByteBlock.Write(buffer, 0, _surPlusLength);
                    PreviewHandle(_tempByteBlock);
                    _tempByteBlock = null;
                    _surPlusLength = 0;
                }
                else if (_surPlusLength < length)
                {
                    _tempByteBlock.Write(buffer, 0, _surPlusLength);
                    PreviewHandle(_tempByteBlock);
                    _tempByteBlock = null;
                    SplitPackage(buffer, _surPlusLength, length);
                }
                else
                {
                    _tempByteBlock.Write(buffer, 0, length);
                    _surPlusLength -= length;
                    if (UpdateCacheTimeWhenRev)
                    {
                        LastCacheTime = DateTime.Now;
                    }
                }
            }
        }

        protected override void Reset()
        {
            ReturnAgreementBytes();
            _surPlusLength = default;
            _tempByteBlock.SafeDispose();
            _tempByteBlock = null;
            base.Reset();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReturnAgreementBytes()
        {
            if (_agreementTempBytes.HasValue)
            {
                if (MemoryMarshal.TryGetArray(_agreementTempBytes.Value, out var segment) && segment.Array is not null)
                {
                    ArrayPool<byte>.Shared.Return(segment.Array);
                }
                else
                {
                    Debug.Assert(false, "Unable to get source array from agreement bytes");
                }
            }

            _agreementTempBytes = null;
        }

        private void PreviewHandle(ByteBlock byteBlock)
        {
            try
            {
                byteBlock.Position = 0;
                GoReceived(byteBlock);
            }
            finally
            {
                byteBlock.Dispose();
            }
        }

        private void SeamPackage(byte[] buffer, int length)
        {
            Debug.Assert(_agreementTempBytes.HasValue);
            var agreementTempBytes = _agreementTempBytes.Value;
            var byteBlock = new ByteBlock(length + agreementTempBytes.Length);
            byteBlock.Write(agreementTempBytes);
            byteBlock.Write(buffer, 0, length);
            length += agreementTempBytes.Length;
            ReturnAgreementBytes();
            SplitPackage(byteBlock.Buffer, 0, length);
            byteBlock.Dispose();
        }

        private void SplitPackage(byte[] buffer, int offset, int length)
        {
            while (offset < length)
            {
                if (length - offset <= (byte)FixedHeaderType)
                {
                    if (_agreementTempBytes.HasValue)
                    {
                        ReturnAgreementBytes();
                    }
                    _agreementTempBytes = CreateAgreementTempBytes(buffer, offset, length - offset);
                    if (UpdateCacheTimeWhenRev)
                    {
                        LastCacheTime = DateTime.Now;
                    }
                    return;
                }

                int packageLength;
                switch (FixedHeaderType)
                {
                    case FixedHeaderType.Byte:
                        packageLength = buffer[offset];
                        break;
                    case FixedHeaderType.Ushort:
                        packageLength = MemoryMarshal.Read<ushort>(buffer.AsSpan(offset, 2));
                        break;
                    case FixedHeaderType.Int:
                        packageLength = MemoryMarshal.Read<int>(buffer.AsSpan(offset, 4));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (packageLength < 0)
                {
                    throw new IOException("Received data length error, reception has been abandoned.");
                }

                if (packageLength < MinPackageSize)
                {
                    throw new IOException("The received data length is less than the set value, reception has been abandoned.");
                }

                if (packageLength > MaxPackageSize)
                {
                    throw new IOException("The received data length is greater than the set value, reception has been abandoned.");
                }

                var receivedSurPlusLength = length - offset - (byte)FixedHeaderType;
                if (receivedSurPlusLength >= packageLength)
                {
                    var byteBlock = new ByteBlock(packageLength);
                    byteBlock.Write(buffer, offset + (byte)FixedHeaderType, packageLength);
                    PreviewHandle(byteBlock);
                    _surPlusLength = 0;
                }
                else
                {
                    _tempByteBlock = new ByteBlock(packageLength);
                    _surPlusLength = packageLength - receivedSurPlusLength;
                    _tempByteBlock.Write(buffer, offset + (byte)FixedHeaderType, receivedSurPlusLength);
                    if (UpdateCacheTimeWhenRev)
                    {
                        LastCacheTime = DateTime.Now;
                    }
                }

                offset += packageLength + (byte)FixedHeaderType;
            }
        }

        private ReadOnlyMemory<byte> CreateAgreementTempBytes(byte[] buffer, int offset, int length)
        {
            var agreementArray = ArrayPool<byte>.Shared.Rent(length);
            Buffer.BlockCopy(buffer, offset, agreementArray, 0, length);
            return new ReadOnlyMemory<byte>(agreementArray, 0, length);
        }

        protected override void PreviewSend(byte[] buffer, int offset, int length)
        {
            EnsureLength(length);

            ByteBlock byteBlock;
            Span<byte> lengthBytes = stackalloc byte[4];
            switch (FixedHeaderType)
            {
                case FixedHeaderType.Byte:
                    {
                        var dataLength = (byte)(length - offset);
                        byteBlock = new ByteBlock(dataLength + 1);
                        lengthBytes[0] = dataLength;
                        lengthBytes = lengthBytes[..1];
                        break;
                    }
                case FixedHeaderType.Ushort:
                    {
                        var dataLength = (ushort)(length - offset);
                        byteBlock = new ByteBlock(dataLength + 2);
                        lengthBytes = lengthBytes[..2];
                        MemoryMarshal.Write(lengthBytes,
#if !NET8_0_OR_GREATER
                            ref
#endif
                            dataLength);
                        break;
                    }
                case FixedHeaderType.Int:
                    {
                        var dataLength = length - offset;
                        byteBlock = new ByteBlock(dataLength + 4);
                        MemoryMarshal.Write(lengthBytes,
#if !NET8_0_OR_GREATER
                            ref
#endif
                            dataLength);
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            try
            {
                byteBlock.Write(lengthBytes);
                byteBlock.Write(buffer, offset, length);
                GoSend(byteBlock.Buffer, 0, (int)byteBlock.Length);
            }
            finally
            {
                byteBlock.Dispose();
            }
        }

        protected override void PreviewSend(IList<ArraySegment<byte>> transferBytes)
        {
            if (transferBytes.Count == 0)
            {
                return;
            }

            var length = transferBytes.Sum(i => i.Count);
            EnsureLength(length);

            ByteBlock byteBlock;
            Span<byte> lengthBytes = stackalloc byte[4];
            switch (FixedHeaderType)
            {
                case FixedHeaderType.Byte:
                    {
                        var dataLength = (byte)length;
                        byteBlock = new ByteBlock(dataLength + 1);
                        lengthBytes = lengthBytes[..1];
                        lengthBytes[0] = dataLength;
                        break;
                    }
                case FixedHeaderType.Ushort:
                    {
                        var dataLength = (ushort)length;
                        byteBlock = new ByteBlock(dataLength + 2);
                        lengthBytes = lengthBytes[..2];
                        MemoryMarshal.Write(lengthBytes,
#if !NET8_0_OR_GREATER
                            ref
#endif
                            dataLength);
                        break;
                    }
                case FixedHeaderType.Int:
                    {
                        byteBlock = new ByteBlock(length + 4);
                        MemoryMarshal.Write(lengthBytes,
#if !NET8_0_OR_GREATER
                            ref
#endif
                            length);
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            try
            {
                byteBlock.Write(lengthBytes);
                foreach (var segment in transferBytes)
                {
                    byteBlock.Write(segment.Array!, segment.Offset, segment.Count);
                }
                GoSend(byteBlock.Buffer, 0, (int)byteBlock.Length);
            }
            finally
            {
                byteBlock.Dispose();
            }
        }

        protected override void PreviewSend(ReadOnlySequence<byte> transferBytes)
        {
            var length = (int)transferBytes.Length;
            EnsureLength(length);

            ByteBlock byteBlock;
            Span<byte> lengthBytes = stackalloc byte[4];
            switch (FixedHeaderType)
            {
                case FixedHeaderType.Byte:
                    {
                        var dataLength = (byte)length;
                        byteBlock = new ByteBlock(dataLength + 1);
                        lengthBytes = lengthBytes[..1];
                        lengthBytes[0] = dataLength;
                        break;
                    }
                case FixedHeaderType.Ushort:
                    {
                        var dataLength = (ushort)length;
                        byteBlock = new ByteBlock(dataLength + 2);
                        lengthBytes = lengthBytes[..2];
                        MemoryMarshal.Write(lengthBytes,
#if !NET8_0_OR_GREATER
                            ref
#endif
                            dataLength);
                        break;
                    }
                case FixedHeaderType.Int:
                    {
                        byteBlock = new ByteBlock(length + 4);
                        MemoryMarshal.Write(lengthBytes,
#if !NET8_0_OR_GREATER
                            ref
#endif
                            length);
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            try
            {
                byteBlock.Write(lengthBytes);
                byteBlock.Write(transferBytes);
                GoSend(byteBlock.Buffer, 0, (int)byteBlock.Length);
            }
            finally
            {
                byteBlock.Dispose();
            }
        }

        protected override async Task PreviewSendAsync(byte[] buffer, int offset, int length)
        {
            EnsureLength(length);

            ByteBlock byteBlock;
            var lengthBytes = ArrayPool<byte>.Shared.Rent(4);
            var lengthSpan = lengthBytes.AsMemory(0, 4);
            switch (FixedHeaderType)
            {
                case FixedHeaderType.Byte:
                    {
                        var dataLength = (byte)(length - offset);
                        byteBlock = new ByteBlock(dataLength + 1);
                        lengthSpan.Span[0] = dataLength;
                        lengthSpan = lengthSpan[..1];
                        break;
                    }
                case FixedHeaderType.Ushort:
                    {
                        var dataLength = (ushort)(length - offset);
                        byteBlock = new ByteBlock(dataLength + 2);
                        lengthSpan = lengthSpan[..2];
                        MemoryMarshal.Write(lengthSpan.Span,
#if !NET8_0_OR_GREATER
                            ref
#endif
                            dataLength);
                        break;
                    }
                case FixedHeaderType.Int:
                    {
                        var dataLength = length - offset;
                        byteBlock = new ByteBlock(dataLength + 4);
                        MemoryMarshal.Write(lengthSpan.Span,
#if !NET8_0_OR_GREATER
                            ref
#endif
                            dataLength);
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            try
            {
                byteBlock.Write(lengthSpan);
                byteBlock.Write(buffer, offset, length);
                await GoSendAsync(byteBlock.Buffer, 0, (int)byteBlock.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(lengthBytes);
                await byteBlock.DisposeAsync();
            }
        }

        protected override async Task PreviewSendAsync(IList<ArraySegment<byte>> transferBytes)
        {
            if (transferBytes.Count == 0)
            {
                return;
            }

            var length = transferBytes.Sum(i => i.Count);
            EnsureLength(length);

            ByteBlock byteBlock;
            var lengthBytes = ArrayPool<byte>.Shared.Rent(4);
            var lengthSpan = lengthBytes.AsMemory(0, 4);
            switch (FixedHeaderType)
            {
                case FixedHeaderType.Byte:
                    {
                        var dataLength = (byte)length;
                        byteBlock = new ByteBlock(dataLength + 1);
                        lengthSpan = lengthSpan[..1];
                        lengthSpan.Span[0] = dataLength;
                        break;
                    }
                case FixedHeaderType.Ushort:
                    {
                        var dataLength = (ushort)length;
                        byteBlock = new ByteBlock(dataLength + 2);
                        lengthSpan = lengthSpan[..2];
                        MemoryMarshal.Write(lengthSpan.Span,
#if !NET8_0_OR_GREATER
                            ref
#endif
                            dataLength);
                        break;
                    }
                case FixedHeaderType.Int:
                    {
                        byteBlock = new ByteBlock(length + 4);
                        MemoryMarshal.Write(lengthSpan.Span,
#if !NET8_0_OR_GREATER
                            ref
#endif
                            length);
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            try
            {
                byteBlock.Write(lengthSpan);
                foreach (var segment in transferBytes)
                {
                    byteBlock.Write(segment.Array!, segment.Offset, segment.Count);
                }
                await GoSendAsync(byteBlock.Buffer, 0, (int)byteBlock.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(lengthBytes);
                await byteBlock.DisposeAsync();
            }
        }

        protected override async Task PreviewSendAsync(ReadOnlySequence<byte> transferBytes)
        {
            var length = (int)transferBytes.Length;
            EnsureLength(length);

            ByteBlock byteBlock;
            var lengthBytes = ArrayPool<byte>.Shared.Rent(4);
            var lengthSpan = lengthBytes.AsMemory(0, 4);
            switch (FixedHeaderType)
            {
                case FixedHeaderType.Byte:
                    {
                        var dataLength = (byte)length;
                        byteBlock = new ByteBlock(dataLength + 1);
                        lengthSpan = lengthSpan[..1];
                        lengthSpan.Span[0] = dataLength;
                        break;
                    }
                case FixedHeaderType.Ushort:
                    {
                        var dataLength = (ushort)length;
                        byteBlock = new ByteBlock(dataLength + 2);
                        lengthSpan = lengthSpan[..2];
                        MemoryMarshal.Write(lengthSpan.Span,
#if !NET8_0_OR_GREATER
                            ref
#endif
                            dataLength);
                        break;
                    }
                case FixedHeaderType.Int:
                    {
                        byteBlock = new ByteBlock(length + 4);
                        MemoryMarshal.Write(lengthSpan.Span,
#if !NET8_0_OR_GREATER
                            ref
#endif
                            length);
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            try
            {
                byteBlock.Write(lengthSpan);
                byteBlock.Write(transferBytes);
                await GoSendAsync(byteBlock.Buffer, 0, (int)byteBlock.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(lengthBytes);
                await byteBlock.DisposeAsync();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureLength(int length)
        {
            if (length < MinPackageSize)
            {
                throw new IOException("The sent data is less than the set value. The same adapter may not receive valid data and the sending has been terminated.");
            }

            if (length > MaxPackageSize)
            {
                throw new IOException("The data sent is larger than the set value. The same adapter may not receive valid data and the sending has been terminated.");
            }
        }
    }

    public enum FixedHeaderType : byte
    {
        Byte = 1,
        Ushort = 2,
        Int = 4
    }

}
