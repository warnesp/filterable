using System;
using System.Collections.Generic;
using MessageParser.Core.Messages;
using MessageParser.Core.Rules;
using MessageParser.Core.Simulation;
using MessageParser.Plugins;
using MessageParser.Plugins.Rules;
using MessageParser.Plugins.Simulation;
using Xunit;

namespace MessageParser.Tests
{
    public class RuleEngineAndStateMachineTests
    {
        [Fact]
        public void Test_HeartbeatMissing2Seconds_TransitionsToDegraded()
        {
            var baseTime = new DateTime(2026, 8, 4, 10, 0, 0, DateTimeKind.Utc);
            var clock = new SimulationClock(baseTime);
            var stateMachine = new LinkStateMachine(clock);
            stateMachine.RuleEngine.RegisterRule(new HeartbeatTimeoutRule());

            LinkStateChangedEventArgs? lastStateEvent = null;
            using (stateMachine.StateChanged.Subscribe(e => lastStateEvent = e))
            {
                Assert.Equal(MessageLinkState.Connected, stateMachine.CurrentState);

                // Advance time by 2.1 seconds without receiving a heartbeat
                clock.AdvanceTime(TimeSpan.FromSeconds(2.1));
                stateMachine.EvaluateStateAndRules();

                Assert.Equal(MessageLinkState.Degraded, stateMachine.CurrentState);
                Assert.NotNull(lastStateEvent);
                Assert.Equal(MessageLinkState.Degraded, lastStateEvent.NewState);
            }
        }

        [Fact]
        public void Test_DegradedMissing5Seconds_TransitionsToClosed()
        {
            var baseTime = new DateTime(2026, 8, 4, 10, 0, 0, DateTimeKind.Utc);
            var clock = new SimulationClock(baseTime);
            var stateMachine = new LinkStateMachine(clock);
            stateMachine.RuleEngine.RegisterRule(new HeartbeatTimeoutRule());

            // Step 1: Transition to Degraded
            clock.AdvanceTime(TimeSpan.FromSeconds(2.1));
            stateMachine.EvaluateStateAndRules();
            Assert.Equal(MessageLinkState.Degraded, stateMachine.CurrentState);

            // Step 2: Stay in Degraded for 5.1 seconds
            clock.AdvanceTime(TimeSpan.FromSeconds(5.1));
            stateMachine.EvaluateStateAndRules();

            Assert.Equal(MessageLinkState.Closed, stateMachine.CurrentState);
        }

        [Fact]
        public void Test_HeartbeatReceived_RestoresConnectedFromDegraded()
        {
            var baseTime = new DateTime(2026, 8, 4, 10, 0, 0, DateTimeKind.Utc);
            var clock = new SimulationClock(baseTime);
            var stateMachine = new LinkStateMachine(clock);
            stateMachine.RuleEngine.RegisterRule(new HeartbeatTimeoutRule());

            // Transition to Degraded
            clock.AdvanceTime(TimeSpan.FromSeconds(2.1));
            stateMachine.EvaluateStateAndRules();
            Assert.Equal(MessageLinkState.Degraded, stateMachine.CurrentState);

            // Process an incoming HeartBeat
            var hb = new HeartBeat
            {
                Sender = "REMOTE",
                Receiver = "LOCAL",
                Command = "HEARTBEAT",
                DeviceId = "DEV_01",
                Status = "NOMINAL",
                ReceivedTime = clock.Now
            };

            stateMachine.ProcessIncomingMessage(hb);

            Assert.Equal(MessageLinkState.Connected, stateMachine.CurrentState);
        }

        [Fact]
        public void Test_AutoHeartbeatSend_Every1Second()
        {
            var baseTime = new DateTime(2026, 8, 4, 10, 0, 0, DateTimeKind.Utc);
            var clock = new SimulationClock(baseTime);
            var stateMachine = new LinkStateMachine(clock);
            stateMachine.RuleEngine.RegisterRule(new AutoHeartbeatSendRule());

            List<MessageBase> outbound = new List<MessageBase>();
            using (stateMachine.OutboundMessages.Subscribe(msg => outbound.Add(msg)))
            {
                // Advance time by 1.1 seconds
                clock.AdvanceTime(TimeSpan.FromSeconds(1.1));
                stateMachine.EvaluateStateAndRules();

                Assert.Contains(outbound, m => m is HeartBeat);
            }
        }

        [Fact]
        public void Test_AirTrackUpdate_Every10Seconds()
        {
            var baseTime = new DateTime(2026, 8, 4, 10, 0, 0, DateTimeKind.Utc);
            var clock = new SimulationClock(baseTime);
            var stateMachine = new LinkStateMachine(clock);
            stateMachine.RuleEngine.RegisterRule(new AirTrackUpdateRule());

            List<MessageBase> outbound = new List<MessageBase>();
            using (stateMachine.OutboundMessages.Subscribe(msg => outbound.Add(msg)))
            {
                // Advance time by 10.1 seconds
                clock.AdvanceTime(TimeSpan.FromSeconds(10.1));
                stateMachine.EvaluateStateAndRules();

                Assert.Contains(outbound, m => m is AirTrack);
            }
        }

        [Fact]
        public void Test_LowFuelStatusRule_ChangesStatusToLowFuel()
        {
            var baseTime = new DateTime(2026, 8, 4, 10, 0, 0, DateTimeKind.Utc);
            var clock = new SimulationClock(baseTime);
            var stateMachine = new LinkStateMachine(clock);
            stateMachine.RuleEngine.RegisterRule(new LowFuelStatusRule());

            var genStatus = new GeneratorStatus
            {
                Sender = "BASE_GEN",
                Receiver = "MONITOR",
                Command = "GENERATOR_STATUS",
                GenId = "GEN_1",
                State = "RUNNING",
                FuelLevel = "15%",
                Load = "50%",
                Temperature = "70C",
                ReceivedTime = clock.Now
            };

            stateMachine.ProcessIncomingMessage(genStatus);

            Assert.Equal("LowFuel", genStatus.State);
        }

        [Fact]
        public void Test_RuleToggleAndParameterEdit()
        {
            var baseTime = new DateTime(2026, 8, 4, 10, 0, 0, DateTimeKind.Utc);
            var clock = new SimulationClock(baseTime);
            var stateMachine = new LinkStateMachine(clock);
            stateMachine.RuleEngine.RegisterRule(new HeartbeatTimeoutRule());

            // Disable HeartbeatTimeoutRule
            bool disabled = stateMachine.RuleEngine.SetRuleEnabled("RULE_HB_TIMEOUT", false);
            Assert.True(disabled);

            // Advance time by 10 seconds - state should remain Connected because rule is disabled
            clock.AdvanceTime(TimeSpan.FromSeconds(10.0));
            stateMachine.EvaluateStateAndRules();
            Assert.Equal(MessageLinkState.Connected, stateMachine.CurrentState);

            // Re-enable and set timeout parameter to 1.0 second
            stateMachine.RuleEngine.SetRuleEnabled("RULE_HB_TIMEOUT", true);
            stateMachine.RuleEngine.SetRuleParameter("RULE_HB_TIMEOUT", "HeartbeatDegradedTimeoutSeconds", 1.0);

            // Advance time by 1.1 seconds - should transition to Degraded
            clock.AdvanceTime(TimeSpan.FromSeconds(1.1));
            stateMachine.EvaluateStateAndRules();
            Assert.Equal(MessageLinkState.Degraded, stateMachine.CurrentState);
        }
    }
}
