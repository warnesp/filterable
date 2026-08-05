using System;
using MessageParser.Core.Rules;

namespace MessageParser.Core.Simulation
{
    public class LinkStateMachine : LinkStateMachineBase<DefaultLinkRuleContext>
    {
        public override string LinkTypeName => "Tactical Radio Link";

        public LinkStateMachine(ITimeService? timeService = null, RuleEngine<DefaultLinkRuleContext>? ruleEngine = null)
            : base(timeService: timeService, ruleEngine: ruleEngine)
        {
        }
    }
}
