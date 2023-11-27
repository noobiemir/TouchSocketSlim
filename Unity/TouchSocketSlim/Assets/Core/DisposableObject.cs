using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace TouchSocketSlim.Core
{
    public partial class DisposableObject : IDisposableObject
    {
        private volatile bool _isDisposed;

        public bool IsDisposed => _isDisposed;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().Name);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            _isDisposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

#if NET6_0_OR_GREATER
    public partial class DisposableObject
    {
        public async ValueTask DisposeAsync()
        {
            await this.DisposeAsyncCore().ConfigureAwait(false);
            this.Dispose(disposing: false);
            GC.SuppressFinalize(this);
        }

        protected virtual ValueTask DisposeAsyncCore()
        {
            return ValueTask.CompletedTask;
        }
    }
#endif
}
