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

namespace TouchSocketSlim.Core;

public abstract class DataHandlingAdapter : DisposableObject
{
    public abstract bool CanSendRequestInfo { get; }

    public abstract bool CanSplicingSend { get; }

    public abstract bool CanSendReadOnlySequence { get; }

    public int MaxPackageSize { get; set; } = 1024 * 1024 * 10;

    public object? Owner { get; private set; }

    public virtual void OnLoaded(object owner)
    {
        if (Owner != null)
        {
            throw new InvalidOperationException("This adapter is already used by another terminal");
        }

        Owner = owner;
    }

    protected virtual void OnError(string error, bool reset = true)
    {
        if (reset)
        {
            Reset();
        }
    }

    protected abstract void Reset();
}