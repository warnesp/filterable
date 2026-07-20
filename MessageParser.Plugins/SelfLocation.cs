using System;
using MessageParser.Core.Messages;
using MessageParser.Core.Filtering;

namespace MessageParser.Plugins
{
    public class SelfLocation : MessageBase
    {
        [Filterable("Latitude")]
        public double Latitude { get; set; }

        [Filterable("Longitude")]
        public double Longitude { get; set; }

        [Filterable("Alt")]
        public double Altitude { get; set; }

        [Filterable("GpsLock")]
        public string GpsLock { get; set; } = string.Empty;

        [Filterable("Precision")]
        public string Precision { get; set; } = string.Empty;
    }
}
