namespace TouchSocketSlim.Core;

public class NormalDataHandlingAdapter : SingleStreamDataHandlingAdapter
{
    protected override void PreviewReceived(ByteBlock byteBlock)
    {
        GoReceived(byteBlock);
    }
}