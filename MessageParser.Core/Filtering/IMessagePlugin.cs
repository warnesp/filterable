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
            IFilterInfo filterInfo,
            Func<object, string> summaryFormatter,
            Func<object, IEnumerable<PayloadField>> payloadExtractor)
        {
            if (filterInfo == null) throw new ArgumentNullException(nameof(filterInfo));
            MessageTypeName = filterInfo.MessageTypeName;
            MessageType = filterInfo.MessageType;
            FilterableProperties = filterInfo.FilterableProperties;
            SummaryFormatter = summaryFormatter ?? throw new ArgumentNullException(nameof(summaryFormatter));
            PayloadExtractor = payloadExtractor ?? throw new ArgumentNullException(nameof(payloadExtractor));
        }
    }

    public interface IMessageRegistry
    {
        void RegisterSchema(MessageSchema schema);
        void RegisterParser(string command, Func<ParsedMessage, object> parser);
        void RegisterEvaluator(IMessageEvaluator evaluator);
        bool EvaluateFilter(object message, string propertyName, Func<object?, bool> predicate);
    }

    public interface IMessagePlugin
    {
        void Initialize(IMessageRegistry registry);
    }
}
