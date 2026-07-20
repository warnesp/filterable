using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MessageParser.Core.Filtering
{
    public class MessageRegistry : IMessageRegistry
    {
        private readonly ConcurrentDictionary<string, MessageSchema> _schemas = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, Func<ParsedMessage, object>> _parsers = new(StringComparer.OrdinalIgnoreCase);

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
