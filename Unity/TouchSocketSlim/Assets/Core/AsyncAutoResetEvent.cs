namespace TouchSocketSlim.Core
{
    public class AsyncAutoResetEvent : AsyncResetEvent
    {
        public AsyncAutoResetEvent() : this(false) { }
        public AsyncAutoResetEvent(bool set) : base(set, true) { }
    }
}
