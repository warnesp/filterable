using System;
using MessageParser.Core.Simulation;

namespace MessageParser.Core.Rules
{
    /// <summary>
    /// Derived RuleContext containing heartbeat and track state fields for default/tactical link rules.
    /// </summary>
    public class DefaultLinkRuleContext : RuleContext
    {
        public DateTime? LastHeartbeatReceivedTime { get; set; }
        public DateTime? LastHeartbeatSentTime { get; set; }
        public DateTime? LastAirTrackSentTime { get; set; }

        public DefaultLinkRuleContext(ITimeService timeService) : base(timeService)
        {
            LastHeartbeatReceivedTime = timeService.Now;
            LastHeartbeatSentTime = timeService.Now;
            LastAirTrackSentTime = timeService.Now;
        }
    }
}
