using System;

namespace MessageParser.Core.Filtering
{
    public interface IMessageEvaluator
    {
        Type MessageType { get; }
        bool Evaluate(object message, string propertyName, Func<object?, bool> predicate);
    }
}
