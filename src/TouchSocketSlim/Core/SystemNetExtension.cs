using System.Net;

namespace TouchSocketSlim.Core;

internal static class SystemNetExtension
{
    public static string GetIp(this EndPoint endPoint)
    {
        var endpointString = endPoint.ToString();
        var r = endpointString!.LastIndexOf(":", StringComparison.Ordinal);
        return endpointString.Substring(0, r);
    }

    public static int GetPort(this EndPoint endPoint)
    {
        var endpointString = endPoint.ToString();
        var r = endpointString!.LastIndexOf(":", StringComparison.Ordinal);
        return Convert.ToInt32(endpointString.Substring(r + 1, endpointString.Length - (r + 1)));
    }
}