using System;
using MessageParser.Core.Messages;
using MessageParser.Core.Filtering;

namespace MessageParser.Plugins
{
    public class GeneratorStatus : MessageBase
    {
        [Filterable("GenId")]
        public string GenId { get; set; } = string.Empty;

        [Filterable("Status")]
        public string State { get; set; } = string.Empty;

        [Filterable("Load")]
        public string Load { get; set; } = string.Empty;

        [Filterable("Fuel")]
        public string FuelLevel { get; set; } = string.Empty;

        [Filterable("Temperature")]
        public string Temperature { get; set; } = string.Empty;
    }
}
