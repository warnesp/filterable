using System;
using MessageParser.Core.Messages;

namespace MessageParser.Plugins
{
    public class GroundTrack : MessageBase
    {
        public string UnitId { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Speed { get; set; }
        public double Heading { get; set; }
        public string Type { get; set; } = string.Empty;
    }
}
