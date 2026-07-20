using System;
using System.Collections.Generic;

namespace MessageParser.Core.Filtering
{
    public record PayloadField(string Key, string Value);

    public class MessageSchema : IFilterInfo
    {
        public string MessageTypeName { get; }
        public Type MessageType { get; }
        public IReadOnlyDictionary<string, Type> FilterableProperties { get; }
        public Func<object, string> SummaryFormatter { get; }
        public Func<object, IEnumerable<PayloadField>> PayloadExtractor { get; }

        public MessageSchema(
            string messageTypeName,
            Type messageType,
            IReadOnlyDictionary<string, Type> filterableProperties,
            Func<object, string> summaryFormatter,
            Func<object, IEnumerable<PayloadField>> payloadExtractor)
        {
            MessageTypeName = messageTypeName ?? throw new ArgumentNullException(nameof(messageTypeName));
            MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
            FilterableProperties = filterableProperties ?? throw new ArgumentNullException(nameof(filterableProperties));
            SummaryFormatter = summaryFormatter ?? throw new ArgumentNullException(nameof(summaryFormatter));
            PayloadExtractor = payloadExtractor ?? throw new ArgumentNullException(nameof(payloadExtractor));
        }
    }

    public interface IMessageRegistry
    {
        void RegisterSchema(MessageSchema schema);
        void RegisterParser(string command, Func<ParsedMessage, object> parser);
    }

    public interface IMessagePlugin
    {
        void Initialize(IMessageRegistry registry);
    }
}
