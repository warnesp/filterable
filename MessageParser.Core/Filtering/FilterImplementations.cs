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
}
