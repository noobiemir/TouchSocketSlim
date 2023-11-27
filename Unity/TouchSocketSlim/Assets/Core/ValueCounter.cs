using System;
using System.Threading;

namespace TouchSocketSlim.Core
{
    public struct ValueCounter
    {
        private long _count;
        private DateTime _lastIncrement;

        public long Count => _count;

        public DateTime LastIncrement => _lastIncrement;

        public Action<long> OnPeriod { get; set; }

        public TimeSpan Period { get; set; }

        public bool Increment(long value)
        {
            bool isPeriod;
            if (DateTime.Now - _lastIncrement > Period)
            {
                OnPeriod?.Invoke(_count);
                Interlocked.Exchange(ref _count, 0);
                isPeriod = true;
                _lastIncrement = DateTime.Now;
            }
            else
            {
                isPeriod = false;
            }

            Interlocked.Add(ref _count, value);
            return isPeriod;
        }

        public bool Increment()
        {
            return Increment(1);
        }

        public void Reset()
        {
            _count = 0;
            _lastIncrement = default;
        }
    }
}
