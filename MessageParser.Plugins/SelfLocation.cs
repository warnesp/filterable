using System;
using MessageParser.Core.Messages;

namespace MessageParser.Plugins
{
    public class SelfLocation : MessageBase
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public string GpsLock { get; set; } = string.Empty;
        public string Precision { get; set; } = string.Empty;
    }
}
