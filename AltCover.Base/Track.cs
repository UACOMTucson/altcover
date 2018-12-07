using System;
using System.Collections.Generic;
using System.Text;

namespace AltCover.Recorder
{
    internal class Track
    {
        protected Track()
        {
        }

        internal class Null : Track
        {
        }

        internal class Time : Track
        {
            public Time(Int64 time)
            {
                Of = time;
            }

            public Int64 Of { get; private set; }
        }

        internal class Call : Track
        {
            public Call(int call)
            {
                Of = call;
            }

            public int Of { get; private set; }
        }

        internal class Both : Track
        {
            public Both(Pair both)
            {
                Of = both;
            }

            public Pair Of { get; private set; }
        }
    }
}