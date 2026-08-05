using System;
using MessageParser.Core.Messages;

namespace MessageParser.Core.Simulation
{
    public interface IMessageLink : IDisposable
    {
        string LinkId { get; }
        string Name { get; }
        string Description { get; }
        LinkStateMachineBase StateMachine { get; }
        ITimeService TimeService { get; }

        IObservable<string> RawMessageStream { get; }
        IObservable<MessageBase> MessageStream { get; }

        bool IsReceivingHeartbeats { get; set; }

        void Start();
        void Stop();
        void Reset();
        string GenerateRandomMessage();
    }
}
