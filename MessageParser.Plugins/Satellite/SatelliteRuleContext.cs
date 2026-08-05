using System;
using MessageParser.Core.Rules;
using MessageParser.Core.Simulation;

namespace MessageParser.Plugins.Satellite
{
    /// <summary>
    /// Derived RuleContext for satellite tracking and telemetry rules.
    /// </summary>
    public class SatelliteRuleContext : RuleContext
    {
        public DateTime? LastHeartbeatReceivedTime { get; set; }
        public DateTime? LastHeartbeatSentTime { get; set; }

        public SatelliteRuleContext(ITimeService timeService) : base(timeService)
        {
            LastHeartbeatReceivedTime = timeService.Now;
            LastHeartbeatSentTime = timeService.Now;
        }
    }
}
