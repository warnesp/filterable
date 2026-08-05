using System;
using MessageParser.Core.Rules;
using MessageParser.Core.Simulation;

namespace MessageParser.Plugins.Satellite
{
    public class SatelliteLinkStateMachine : LinkStateMachineBase
    {
        public override string LinkTypeName => "LEO Satellite Link";

        public SatelliteLinkStateMachine(ITimeService? timeService = null, RuleEngine? ruleEngine = null)
            : base(timeService, ruleEngine)
        {
            // Register satellite-specific rules
            RuleEngine.RegisterRule(new OrbitPassTimeoutRule());
            RuleEngine.RegisterRule(new SatelliteTelemetryBroadcastRule());
        }
    }
}
