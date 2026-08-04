using MessageParser.Core.Messages;
using MessageParser.Core.Simulation;

namespace MessageParser.Core.Rules
{
    public class RuleExecutionResult
    {
        public bool Triggered { get; set; }
        public MessageLinkState? ProposedState { get; set; }
        public MessageBase? MessageToSend { get; set; }
        public string? LogMessage { get; set; }

        public static RuleExecutionResult NoAction() => new RuleExecutionResult { Triggered = false };
        
        public static RuleExecutionResult Transition(MessageLinkState newState, string logMessage) =>
            new RuleExecutionResult
            {
                Triggered = true,
                ProposedState = newState,
                LogMessage = logMessage
            };

        public static RuleExecutionResult Send(MessageBase message, string logMessage) =>
            new RuleExecutionResult
            {
                Triggered = true,
                MessageToSend = message,
                LogMessage = logMessage
            };

        public static RuleExecutionResult TransitionAndSend(MessageLinkState newState, MessageBase message, string logMessage) =>
            new RuleExecutionResult
            {
                Triggered = true,
                ProposedState = newState,
                MessageToSend = message,
                LogMessage = logMessage
            };
    }
}
