using System;

namespace MessageParser.Core.Messages
{
    public class GeneratorStatus : MessageBase
    {
        public string GenId { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Load { get; set; } = string.Empty;
        public string FuelLevel { get; set; } = string.Empty;
        public string Temperature { get; set; } = string.Empty;
    }
}
