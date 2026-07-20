using System;
using System.Collections.Generic;

namespace MessageParser.Core.Filtering
{
    public interface IFilterInfo
    {
        Type MessageType { get; }
        string MessageTypeName { get; }
        IReadOnlyDictionary<string, Type> FilterableProperties { get; }
    }
}
