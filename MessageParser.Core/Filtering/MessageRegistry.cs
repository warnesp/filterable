using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace MessageParser.Core.Filtering
{
    public class MessageRegistry : IMessageRegistry
    {
        private readonly ConcurrentDictionary<string, MessageSchema> _schemas = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, Func<ParsedMessage, object>> _parsers = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<(string SchemaName, string PropName), Func<object, object>> _getters = new();

        public event Action<MessageSchema>? SchemaRegistered;

        public void RegisterSchema(MessageSchema schema)
        {
            _schemas[schema.MessageTypeName] = schema;

            // Compile and cache getters for all filterable properties in the schema
            foreach (var prop in schema.FilterableProperties.Keys)
            {
                try
                {
                    var getter = CompileGetter(schema.MessageType, prop);
                    _getters[(schema.MessageTypeName, prop)] = getter;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to compile getter for property '{prop}' on type '{schema.MessageType.Name}': {ex.Message}");
                }
            }

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

        public Func<object, object>? GetGetter(string schemaName, string propertyName)
        {
            return _getters.TryGetValue((schemaName, propertyName), out var getter) ? getter : null;
        }

        private static Func<object, object> CompileGetter(Type type, string propertyName)
        {
            var param = Expression.Parameter(typeof(object), "obj");
            var cast = Expression.Convert(param, type);
            var property = Expression.PropertyOrField(cast, propertyName);
            var box = Expression.Convert(property, typeof(object));

            return Expression.Lambda<Func<object, object>>(box, param).Compile();
        }
    }
}
