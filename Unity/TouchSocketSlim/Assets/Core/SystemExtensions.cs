using System;

namespace TouchSocketSlim.Core
{
    internal static class SystemExtensions
    {
        public static void SafeDispose(this IDisposable? disposable)
        {
            try
            {
                disposable?.Dispose();
            }
            catch
            {
                //ignore
            }
        }
    }

}
