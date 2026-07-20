using System;
using MessageParser.Core.Messages;

namespace MessageParser.Plugins
{
    public class AirTrack : MessageBase
    {
        public string Callsign { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double Speed { get; set; }
        public double Heading { get; set; }
        public string Squawk { get; set; } = string.Empty;
    }
}
