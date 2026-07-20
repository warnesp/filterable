using System;
using MessageParser.Core.Messages;
using MessageParser.Core.Filtering;

namespace MessageParser.Plugins
{
    public class HeartBeat : MessageBase
    {
        [Filterable("DeviceId")]
        public string DeviceId { get; set; } = string.Empty;

        [Filterable("Status")]
        public string Status { get; set; } = string.Empty;

        [Filterable("Uptime")]
        public string Uptime { get; set; } = string.Empty;

        [Filterable("Battery")]
        public string Battery { get; set; } = string.Empty;
    }
}
