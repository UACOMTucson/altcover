using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AltCover.Recorder
{
    internal class TrackSplit
    {
        public ReadOnlyCollection<Int64> Times { get; private set; }
        public ReadOnlyCollection<int> Calls { get; private set; }

        private TrackSplit()
        { }

        public TrackSplit Do(IEnumerable<Track> ts)
        {
            var t = new List<Int64>();
            var c = new List<int>();

            foreach (var item in ts)
            {
                switch (item)
                {
                    case Track.Time tt:
                        t.Add(tt.Of);
                        break;

                    case Track.Call tc:
                        c.Add(tc.Of);
                        break;

                    case Track.Both tb:
                        var p = tb.Of;
                        t.Add(p.Time);
                        c.Add(p.Call);
                        break;
                }
            }

            return new TrackSplit()
            {
                Times = new ReadOnlyCollection<long>(t),
                Calls = new ReadOnlyCollection<int>(c)
            };
        }
    }
}