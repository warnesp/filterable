using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MessageParser.Core.Filtering
{
    public class MessageRegistry : IMessageRegistry
    {
        private readonly ConcurrentDictionary<string, MessageSchema> _schemas = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, Func<ParsedMessage, object>> _parsers = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<Type, IMessageEvaluator> _evaluators = new();

        public event Action<MessageSchema>? SchemaRegistered;

        public void RegisterSchema(MessageSchema schema)
        {
            _schemas[schema.MessageTypeName] = schema;
            SchemaRegistered?.Invoke(schema);
        }

        public void RegisterParser(string command, Func<ParsedMessage, object> parser)
        {
            _parsers[command] = parser;
        }

        public void RegisterEvaluator(IMessageEvaluator evaluator)
        {
            if (evaluator == null) throw new ArgumentNullException(nameof(evaluator));
            _evaluators[evaluator.MessageType] = evaluator;
        }

        public bool EvaluateFilter(object message, string propertyName, Func<object?, bool> predicate)
        {
            if (message == null) return false;
            if (_evaluators.TryGetValue(message.GetType(), out var evaluator))
            {
                return evaluator.Evaluate(message, propertyName, predicate);
            }
            return false;
        }

        public MessageSchema? GetSchema(string messageTypeName)
        {
            return _schemas.TryGetValue(messageTypeName, out var schema) ? schema : null;
        }

        public IEnumerable<MessageSchema> GetSchemas()
        {
            return _schemas.Values;
        }

        public object? Parse(ParsedMessage parsed)
        {
            if (parsed == null || !parsed.IsValid) return null;
            return _parsers.TryGetValue(parsed.Command, out var parser) ? parser(parsed) : null;
        }
    }
}
