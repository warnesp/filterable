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
        public IReadOnlyDictionary<string, string> PropertyNameMapping { get; }
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

            var propertyNameMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in filterInfo.FilterableProperties)
            {
                propertyNameMapping[kvp.Key] = kvp.Key;
            }
            PropertyNameMapping = propertyNameMapping;
        }

        public MessageSchema(
            string messageTypeName,
            Type messageType,
            Func<object, string> summaryFormatter,
            Func<object, IEnumerable<PayloadField>> payloadExtractor)
        {
            MessageTypeName = messageTypeName ?? throw new ArgumentNullException(nameof(messageTypeName));
            MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
            SummaryFormatter = summaryFormatter ?? throw new ArgumentNullException(nameof(summaryFormatter));
            PayloadExtractor = payloadExtractor ?? throw new ArgumentNullException(nameof(payloadExtractor));

            var filterableProperties = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            var propertyNameMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var prop in messageType.GetProperties())
            {
                var attr = (FilterableAttribute?)Attribute.GetCustomAttribute(prop, typeof(FilterableAttribute));
                if (attr != null)
                {
                    string friendlyName = attr.FriendlyName;
                    filterableProperties[friendlyName] = prop.PropertyType;
                    propertyNameMapping[friendlyName] = prop.Name;

                    if (!filterableProperties.ContainsKey(prop.Name))
                    {
                        filterableProperties[prop.Name] = prop.PropertyType;
                        propertyNameMapping[prop.Name] = prop.Name;
                    }
                }
            }

            if (!filterableProperties.ContainsKey("Sender"))
            {
                filterableProperties["Sender"] = typeof(string);
                propertyNameMapping["Sender"] = "Sender";
            }
            if (!filterableProperties.ContainsKey("Receiver"))
            {
                filterableProperties["Receiver"] = typeof(string);
                propertyNameMapping["Receiver"] = "Receiver";
            }

            FilterableProperties = filterableProperties;
            PropertyNameMapping = propertyNameMapping;
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
