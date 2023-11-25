namespace TouchSocketSlim.Core;

public partial interface IDisposableObject : IDisposable
{
    bool IsDisposed { get; }
}

#if NET6_0_OR_GREATER
public partial interface IDisposableObject : IAsyncDisposable
{

}
#endif