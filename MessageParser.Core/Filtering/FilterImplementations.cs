using System;
using System.Collections.Generic;
using MessageParser.Core.Messages;

namespace MessageParser.Core.Filtering
{
    public class GenericFilterInfo : IFilterInfo
    {
        public Type MessageType { get; }
        public string MessageTypeName { get; }
        public IReadOnlyDictionary<string, Type> FilterableProperties { get; }

        public GenericFilterInfo(Type messageType, string messageTypeName, Dictionary<string, Type> properties)
        {
            MessageType = messageType;
            MessageTypeName = messageTypeName;
            FilterableProperties = properties;
        }
    }

    public abstract class FilterableBase<T> : IFilterable where T : MessageBase
    {
        public T TypedMessage { get; }
        public object WrappedMessage => TypedMessage;
        public Type MessageType => typeof(T);
        public string MessageTypeName => typeof(T).Name;
        public DateTime ReceivedTime => TypedMessage.ReceivedTime;
        public string Sender => TypedMessage.Sender;
        public string Receiver => TypedMessage.Receiver;

        protected FilterableBase(T message)
        {
            TypedMessage = message ?? throw new ArgumentNullException(nameof(message));
        }

        public abstract bool ApplyFilter(string propertyName, Func<object?, bool> predicate);
    }

    public class DynamicFilterable : IFilterable
    {
        private readonly MessageRegistry _registry;

        public object WrappedMessage { get; }
        public Type MessageType { get; }
        public string MessageTypeName { get; }
        public DateTime ReceivedTime { get; }
        public string Sender { get; }
        public string Receiver { get; }

        public DynamicFilterable(object message, string messageTypeName, DateTime receivedTime, string sender, string receiver, MessageRegistry registry)
        {
            WrappedMessage = message ?? throw new ArgumentNullException(nameof(message));
            MessageType = message.GetType();
            MessageTypeName = messageTypeName ?? throw new ArgumentNullException(nameof(messageTypeName));
            ReceivedTime = receivedTime;
            Sender = sender ?? string.Empty;
            Receiver = receiver ?? string.Empty;
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public bool ApplyFilter(string propertyName, Func<object?, bool> predicate)
        {
            if (predicate == null) return true;

            var getter = _registry.GetGetter(MessageTypeName, propertyName);
            if (getter == null)
            {
                object? coreValue = propertyName.ToUpperInvariant() switch
                {
                    "SENDER" => Sender,
                    "RECEIVER" => Receiver,
                    "MESSAGETYPENAME" => MessageTypeName,
                    _ => null
                };
                return predicate(coreValue);
            }

            try
            {
                object val = getter(WrappedMessage);
                return predicate(val);
            }
            catch
            {
                return false;
            }
        }
    }
}
