using System;

namespace AltCover.Recorder
{
    internal class Pair
    {
        public static Pair Build(Int64 time, int call)
        {
            return new Pair(time, call);
        }

        private Pair(Int64 time, int call)
        {
            Call = call;
            Time = time;
        }

        public int Call { get; private set; }
        public Int64 Time { get; private set; }
    }
}