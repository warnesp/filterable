using System;
using MessageParser.Core.Messages;

namespace MessageParser.Plugins
{
    public class HeartBeat : MessageBase
    {
        public string DeviceId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Uptime { get; set; } = string.Empty;
        public string Battery { get; set; } = string.Empty;
    }
}
