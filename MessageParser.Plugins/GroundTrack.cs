using System;
using MessageParser.Core.Messages;
using MessageParser.Core.Filtering;

namespace MessageParser.Plugins
{
    public class GroundTrack : MessageBase
    {
        [Filterable("Unit")]
        public string UnitId { get; set; } = string.Empty;

        [Filterable("Latitude")]
        public double Latitude { get; set; }

        [Filterable("Longitude")]
        public double Longitude { get; set; }

        [Filterable("Speed")]
        public double Speed { get; set; }

        [Filterable("Heading")]
        public double Heading { get; set; }

        [Filterable("Type")]
        public string Type { get; set; } = string.Empty;
    }
}
