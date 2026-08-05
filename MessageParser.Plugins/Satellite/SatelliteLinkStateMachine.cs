using System;
using MessageParser.Core.Rules;
using MessageParser.Core.Simulation;

namespace MessageParser.Plugins.Satellite
{
    public class SatelliteLinkStateMachine : LinkStateMachineBase<SatelliteRuleContext>
    {
        public override string LinkTypeName => "LEO Satellite Link";

        public SatelliteLinkStateMachine(ITimeService? timeService = null, RuleEngine<SatelliteRuleContext>? ruleEngine = null)
            : base(timeService: timeService, ruleEngine: ruleEngine)
        {
            // Register satellite-specific rules
            RuleEngine.RegisterRule(new OrbitPassTimeoutRule());
            RuleEngine.RegisterRule(new SatelliteTelemetryBroadcastRule());
        }
    }
}
