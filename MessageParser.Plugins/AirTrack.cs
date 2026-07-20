using System;
using MessageParser.Core.Messages;
using MessageParser.Core.Filtering;

namespace MessageParser.Plugins
{
    public class AirTrack : MessageBase
    {
        [Filterable("Callsign")]
        public string Callsign { get; set; } = string.Empty;

        [Filterable("Latitude")]
        public double Latitude { get; set; }

        [Filterable("Longitude")]
        public double Longitude { get; set; }

        [Filterable("Alt")]
        public double Altitude { get; set; }

        [Filterable("Speed")]
        public double Speed { get; set; }

        [Filterable("Heading")]
        public double Heading { get; set; }

        [Filterable("Squawk")]
        public string Squawk { get; set; } = string.Empty;
    }
}
