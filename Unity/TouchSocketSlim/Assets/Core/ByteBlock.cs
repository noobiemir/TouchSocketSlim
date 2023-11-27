using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;

namespace TouchSocketSlim.Core
{
    public sealed class ByteBlock : Stream
    {
        private const int MemStreamMaxLength = int.MaxValue;
        private int _length;
        private int _position;
        private bool _isOpen;

        public ByteBlock(int byteSize = 1024 * 64)
        {
            Buffer = ArrayPool<byte>.Shared.Rent(byteSize);
            _length = byteSize;
            _isOpen = true;
        }

        public byte[] Buffer { get; private set; }

        public int Capacity => Buffer.Length;

        public override bool CanRead => _isOpen && _length - _position > 0;

        public override bool CanSeek => _isOpen;

        public override bool CanWrite => _isOpen;

        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set => _position = (int)value;
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin)
        {
            EnsureNotClosed();

            return SeekCore(offset, origin switch
            {
                SeekOrigin.Begin => 0,
                SeekOrigin.Current => _position,
                SeekOrigin.End => _length,
                _ => throw new ArgumentException("Invalid SeekOrigin")
            });
        }

        private long SeekCore(long offset, int loc)
        {
            if (offset > MemStreamMaxLength - loc) throw new ArgumentOutOfRangeException(nameof(offset));
            int tempPosition = unchecked(loc + (int)offset);
            if (unchecked(loc + offset) < 0 || tempPosition < 0) throw new IOException("Seek Before Begin");
            _position = tempPosition;
            return _position;
        }

        public override void SetLength(long value)
        {
            if (value < 0 || value > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(value));

            EnsureNotClosed();

            var allocatedNewArray = EnsureCapacity((int)value);
            if (!allocatedNewArray && value > _length)
            {
                Array.Clear(Buffer, _length, (int)(value - _length));
            }

            _length = (int)value;
            _position = Math.Min(_length, _position);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
#if NET6_0_OR_GREATER
            ValidateBufferArguments(buffer, offset, count);
#endif
            if (buffer.Length == 0) return;
            EnsureNotClosed();

            int i = _position + count;

            if (i > _length)
            {
                bool mustZero = _position > _length;

                if (i > Buffer.Length)
                {
                    var allocatedNewArray = EnsureCapacity(i);
                    if (allocatedNewArray)
                    {
                        mustZero = false;
                    }
                }

                if (mustZero)
                {
                    Array.Clear(Buffer, _length, i - _length);
                }

                _length = i;
            }

            if (count <= 8 && buffer != Buffer)
            {
                int byteCount = count;
                while (--byteCount >= 0)
                {
                    Buffer[_position + byteCount] = buffer[offset + byteCount];
                }
            }
            else
            {
                System.Buffer.BlockCopy(buffer, offset, Buffer, _position, count);
            }
            _position = i;
        }

        public void Write(ReadOnlyMemory<byte> buffer)
        {
            if (buffer.Length == 0) return;

            EnsureNotClosed();

            int i = _position + buffer.Length;
            if (i < 0) throw new IOException("IO Stream too long");

            if (i > _length)
            {
                bool mustZero = _position > _length;

                if (i > Buffer.Length)
                {
                    var allocatedNewArray = EnsureCapacity(i);
                    if (allocatedNewArray)
                    {
                        mustZero = false;
                    }
                }

                if (mustZero)
                {
                    Array.Clear(Buffer, _length, i - _length);
                }

                _length = i;
            }

            buffer.CopyTo(Buffer.AsMemory(_position, buffer.Length));
            _position = i;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length == 0) return;

            EnsureNotClosed();

            int i = _position + buffer.Length;
            if (i < 0) throw new IOException("IO Stream too long");

            if (i > _length)
            {
                bool mustZero = _position > _length;

                if (i > Buffer.Length)
                {
                    var allocatedNewArray = EnsureCapacity(i);
                    if (allocatedNewArray)
                    {
                        mustZero = false;
                    }
                }

                if (mustZero)
                {
                    Array.Clear(Buffer, _length, i - _length);
                }

                _length = i;
            }

            buffer.CopyTo(Buffer.AsSpan(_position, buffer.Length));
            _position = i;
        }

        public void Write(ReadOnlySequence<byte> buffer)
        {
            if (buffer.Length == 0) return;

            EnsureNotClosed();

            int i = (int)(_position + buffer.Length);
            if (i < 0) throw new IOException("IO Stream too long");

            if (i > _length)
            {
                bool mustZero = _position > _length;

                if (i > Buffer.Length)
                {
                    var allocatedNewArray = EnsureCapacity(i);
                    if (allocatedNewArray)
                    {
                        mustZero = false;
                    }
                }

                if (mustZero)
                {
                    Array.Clear(Buffer, _length, i - _length);
                }

                _length = i;
            }

            buffer.CopyTo(Buffer.AsSpan(_position, (int)buffer.Length));
            _position = i;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
#if NET6_0_OR_GREATER
            ValidateBufferArguments(buffer, offset, count);
#endif
            EnsureNotClosed();

            int n = _length - _position;
            if (n > count)
                n = count;
            if (n <= 0)
                return 0;

            if (n <= 8)
            {
                int byteCount = n;
                while (--byteCount >= 0)
                    buffer[offset + byteCount] = Buffer[_position + byteCount];
            }
            else
                System.Buffer.BlockCopy(Buffer, _position, buffer, offset, n);
            _position += n;

            return n;
        }
        public override int ReadByte()
        {
            EnsureNotClosed();

            if (_position >= _length) return -1;

            return Buffer[_position++];
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ArrayPool<byte>.Shared.Return(Buffer);
                _isOpen = false;
            }

            base.Dispose(disposing);
        }

        public void Clear()
        {
            EnsureNotClosed();

            Array.Clear(Buffer, 0, Buffer.Length);
            _position = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        private void EnsureNotClosed()
        {
            if (!_isOpen) throw new ObjectDisposedException("Stream closed");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EnsureCapacity(int value)
        {
            if (value < 0) throw new IOException("Stream too long.");

            if (value > Buffer.Length)
            {
                var newCapacity = Math.Max(value, 256);
                if (newCapacity < Buffer.Length * 2)
                {
                    newCapacity = Buffer.Length * 2;
                }

#if NET6_0_OR_GREATER
                if ((uint)(Buffer.Length * 2) > Array.MaxLength)
                {
                    newCapacity = Math.Max(value, Array.MaxLength);
                }
#else
            const int arrayMaxLength = 0X7FFFFFC7;
            if ((uint)(Buffer.Length * 2) > arrayMaxLength)
            {
                newCapacity = Math.Max(value, arrayMaxLength);
            }
#endif
                SetCapacity(newCapacity);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetCapacity(int value)
        {
            EnsureNotClosed();
            var newBuffer = ArrayPool<byte>.Shared.Rent(value);
            Buffer.AsSpan().CopyTo(newBuffer.AsSpan());
            if (value > Buffer.Length)
            {
                Array.Clear(newBuffer, Buffer.Length, value - Buffer.Length);
            }
            ArrayPool<byte>.Shared.Return(Buffer);
            Buffer = newBuffer;
        }
    }
}
