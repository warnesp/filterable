using System;
using System.Collections.Generic;
using MessageParser.Core.Messages;
using MessageParser.Core.Simulation;

namespace MessageParser.Core.Rules
{
    public class RuleContext
    {
        public MessageLinkState CurrentState { get; set; }
        public ITimeService TimeService { get; }
        public DateTime CurrentTime => TimeService.Now;
        public DateTime? DegradedStateEnteredTime { get; set; }
        public MessageBase? IncomingMessage { get; set; }

        public RuleContext(ITimeService timeService)
        {
            TimeService = timeService ?? throw new ArgumentNullException(nameof(timeService));
        }
    }
}
