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

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace TouchSocketSlim.Sockets
{
    public class TouchSocketUtility
    {
        private static Regex UrlRegex()
        {
            return new Regex(@"^[a-zA-Z]+://(\w+(-\w+)*)(\.(\w+(-\w+)*))*(\?\S*)?$");
        }

        public static bool IsUrl(string url)
        {
            return UrlRegex().IsMatch(url);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HitBufferLength(long value)
        {
            return value switch
            {
                < 1024 * 100 => 1024,
                < 1024 * 512 => 1024 * 10,
                < 1024 * 1024 => 1024 * 64,
                < 1024 * 1024 * 50 => 1024 * 512,
                < 1024 * 1024 * 100 => 1024 * 1024,
                < 1024 * 1024 * 1024 => 1024 * 1024 * 2,
                < 1024 * 1024 * 1024 * 10L => 1024 * 1024 * 5,
                _ => 1024 * 1024 * 10
            };
        }
    }
}
