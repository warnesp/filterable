using System;

namespace MessageParser.Core.Messages
{
    public abstract class MessageBase
    {
        public string Sender { get; set; } = string.Empty;
        public string Receiver { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public DateTime ReceivedTime { get; set; } = DateTime.UtcNow;
    }
}
