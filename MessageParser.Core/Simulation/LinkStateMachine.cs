using System;
using MessageParser.Core.Rules;

namespace MessageParser.Core.Simulation
{
    public class LinkStateMachine : LinkStateMachineBase
    {
        public override string LinkTypeName => "Tactical Radio Link";

        public LinkStateMachine(ITimeService? timeService = null, RuleEngine? ruleEngine = null)
            : base(timeService, ruleEngine)
        {
        }
    }
}
