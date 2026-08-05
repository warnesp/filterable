using System;
using System.Collections.Generic;
using MessageParser.Core.Messages;
using MessageParser.Core.Plugins;
using MessageParser.Core.Simulation;
using MessageParser.Plugins;
using MessageParser.Plugins.Satellite;
using MessageParser.Plugins.Simulation;
using Xunit;

namespace MessageParser.Tests
{
    public class PluginLinkAndStateMachineTests
    {
        [Fact]
        public void Test_LinkRegistry_RegistersAndInstantiatesPluginLinks()
        {
            var registry = new LinkRegistry();

            var tacticalPlugin = new StandardTacticalLinkPlugin();
            tacticalPlugin.RegisterLinks(registry);

            var satPlugin = new SatelliteLinkPlugin();
            satPlugin.RegisterLinks(registry);

            Assert.Equal(2, registry.Descriptors.Count);

            using var tacticalLink = registry.CreateLink("LINK_TACTICAL_RADIO");
            Assert.NotNull(tacticalLink);
            Assert.Equal("Tactical Radio Link", tacticalLink.Name);

            using var satLink = registry.CreateLink("LINK_SATELLITE_LEO");
            Assert.NotNull(satLink);
            Assert.Equal("LEO Satellite Link", satLink.Name);
            Assert.Equal("LEO Satellite Link", satLink.StateMachine.LinkTypeName);
        }

        [Fact]
        public void Test_SatelliteLinkStateMachine_OrbitTimeoutTransitionsToDegradedAndClosed()
        {
            var baseTime = new DateTime(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);
            var clock = new SimulationClock(baseTime);

            using var satLink = new SatelliteMessageLink(clock);
            Assert.Equal(MessageLinkState.Connected, satLink.StateMachine.CurrentState);

            LinkStateChangedEventArgs? stateEvent = null;
            using (satLink.StateMachine.StateChanged.Subscribe(e => stateEvent = e))
            {
                // Satellite Orbit Pass Timeout rule degrades after 3s missing heartbeat
                clock.AdvanceTime(TimeSpan.FromSeconds(3.1));
                satLink.StateMachine.EvaluateStateAndRules();

                Assert.Equal(MessageLinkState.Degraded, satLink.StateMachine.CurrentState);
                Assert.NotNull(stateEvent);
                Assert.Equal(MessageLinkState.Degraded, stateEvent.NewState);

                // Stays degraded for 6.1s -> transitions to Closed
                clock.AdvanceTime(TimeSpan.FromSeconds(6.1));
                satLink.StateMachine.EvaluateStateAndRules();

                Assert.Equal(MessageLinkState.Closed, satLink.StateMachine.CurrentState);
                Assert.Equal(MessageLinkState.Closed, stateEvent.NewState);
            }
        }

        [Fact]
        public void Test_SatelliteLinkTelemetryBroadcast_SendsPeriodicBeacon()
        {
            var baseTime = new DateTime(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);
            var clock = new SimulationClock(baseTime);

            using var satLink = new SatelliteMessageLink(clock);

            List<MessageBase> outboundMsgs = new List<MessageBase>();
            using (satLink.MessageStream.Subscribe(msg => outboundMsgs.Add(msg)))
            {
                // Telemetry beacon fires every 5 seconds
                clock.AdvanceTime(TimeSpan.FromSeconds(5.1));
                satLink.StateMachine.EvaluateStateAndRules();

                Assert.Contains(outboundMsgs, m => m is HeartBeat hb && hb.DeviceId == "SATCOM_LEO_09");
            }
        }
    }
}
