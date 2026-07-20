using System;
using System.Collections.Generic;

namespace MessageParser.Core
{
    public class ParsedMessage
    {
        public string Sender { get; set; } = string.Empty;
        public string Receiver { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public Dictionary<string, string> Payload { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public bool IsValid { get; set; }
        public string Error { get; set; } = string.Empty;
        public string RawMessage { get; set; } = string.Empty;
    }
}
