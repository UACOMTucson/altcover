using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AltCover.Recorder
{
    internal class PointVisits
    {
        private List<Track> tracks = new List<Track>();
        public int Count { get; private set; }

        public ReadOnlyCollection<Track> Context
        {
            get
            {
                return new ReadOnlyCollection<Track>(tracks);
            }
        }

        private PointVisits()
        {
            Count = 0;
        }

        public static PointVisits Default()
        {
            return new PointVisits();
        }

        public PointVisits Visit(Track context)
        {
            var result = Default();
            switch (context)
            {
                case Track.Null n:
                    result.Count = Count + 1;
                    result.tracks.AddRange(tracks);
                    break;

                default:
                    result.Count = Count + 1;
                    result.tracks.Add(context);
                    result.tracks.AddRange(tracks);
                    break;
            }

            return result;
        }

        public int Total()
        {
            return Count + tracks.Count;
        }
    }
}