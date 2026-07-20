using System;

namespace MessageParser.Core.Messages
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
