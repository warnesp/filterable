using System;

namespace MessageParser.Core.Filtering
{
    public interface IFilterable
    {
        object WrappedMessage { get; }
        Type MessageType { get; }
        string MessageTypeName { get; }
        DateTime ReceivedTime { get; }
        string Sender { get; }
        string Receiver { get; }
        bool ApplyFilter(string propertyName, Func<object?, bool> predicate);
    }
}
