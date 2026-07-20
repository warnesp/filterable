using System;

namespace MessageParser.Core.Filtering
{
    public class MessageRecord
    {
        public DateTime ReceivedTime { get; }
        public string MessageTypeName { get; }
        public string Sender { get; }
        public string Receiver { get; }
        public object NativeMessage { get; }

        public MessageRecord(DateTime receivedTime, string messageTypeName, string sender, string receiver, object nativeMessage)
        {
            ReceivedTime = receivedTime;
            MessageTypeName = messageTypeName ?? throw new ArgumentNullException(nameof(messageTypeName));
            Sender = sender ?? throw new ArgumentNullException(nameof(sender));
            Receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
            NativeMessage = nativeMessage ?? throw new ArgumentNullException(nameof(nativeMessage));
        }
    }
}
