using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;

namespace AltCover.Recorder
{
    internal class XmlPointVisits
    {
        private ReadOnlyCollection<Track> tracks = null;
        public XmlElement Element { get; private set; }

        public ReadOnlyCollection<Track> Context
        {
            get
            {
                return tracks;
            }
        }

        private XmlPointVisits(XmlElement x, IList<Track> t)
        {
            Element = x;
            tracks = new ReadOnlyCollection<Track>(t);
        }

        public XmlPointVisits Build(XmlElement x, IList<Track> t)
        {
            return new XmlPointVisits(x, t);
        }
    }
}