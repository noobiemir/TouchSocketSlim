using TouchSocketSlim.Core;

namespace TouchSocketSlim.Sockets;

public class Receiver : DisposableObject
{
    ~Receiver()
    {
        Dispose(false);
    }

    private readonly IClient _client;
    private readonly AutoResetEvent _resetEventForCompleteRead = new(false);
    private readonly AsyncAutoResetEvent _resetEventForRead = new(false);
    private ByteBlock? _byteBlock;
    private IRequestInfo? _requestInfo;

    public Receiver(IClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<ReceiverResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _resetEventForRead.WaitOneAsync(cancellationToken).ConfigureAwait(false);
        return new ReceiverResult(CompleteRead, _byteBlock, _requestInfo);
    }

    public bool TryInputReceive(ByteBlock? byteBlock, IRequestInfo? requestInfo)
    {
        if (IsDisposed)
        {
            return false;
        }

        _byteBlock = byteBlock;
        _requestInfo = requestInfo;
        if (byteBlock == null && requestInfo == null)
        {
            return true;
        }

        if (_resetEventForCompleteRead.WaitOne(TimeSpan.FromSeconds(10)))
        {
            return true;
        }

        return false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _client.ClearReceiver();
        }
        else
        {
            _resetEventForCompleteRead.SafeDispose();
        }

        _byteBlock = null;
        base.Dispose(disposing);
    }

    private void CompleteRead()
    {
        _byteBlock = default;
        _requestInfo = default;
        _resetEventForCompleteRead.Set();
    }
}