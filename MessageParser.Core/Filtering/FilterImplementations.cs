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

}
