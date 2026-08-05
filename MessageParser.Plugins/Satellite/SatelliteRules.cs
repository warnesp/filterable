using System;
using MessageParser.Core.Messages;
using MessageParser.Core.Rules;
using MessageParser.Core.Simulation;
using MessageParser.Plugins.Rules;

namespace MessageParser.Plugins.Satellite
{
    /// <summary>
    /// Satellite Rule 1: Orbit Pass Timeout. Degrades link after 3s loss, closes after 6s.
    /// </summary>
    public class OrbitPassTimeoutRule : RuleBase
    {
        public override string Id => "RULE_SAT_ORBIT_TIMEOUT";
        public override string Name => "Satellite Orbit Pass Timeout";
        public override string Description => "Degrades satellite link if signal lost for 3s. Closes link if signal lost for 6s during orbital pass.";
        public override string Category => "Satellite State";

        public OrbitPassTimeoutRule()
        {
            AddParameter("SignalDegradedTimeoutSeconds", 3.0);
            AddParameter("SignalLostCloseTimeoutSeconds", 6.0);
        }

        public override RuleExecutionResult Evaluate(RuleContext context)
        {
            if (!IsEnabled) return RuleExecutionResult.NoAction();

            double degradedSec = GetParameter("SignalDegradedTimeoutSeconds", 3.0);
            double closeSec = GetParameter("SignalLostCloseTimeoutSeconds", 6.0);

            if (context.IncomingMessage is HeartBeat)
            {
                context.LastHeartbeatReceivedTime = context.CurrentTime;
                if (context.CurrentState == MessageLinkState.Degraded)
                {
                    return RuleExecutionResult.Transition(
                        MessageLinkState.Connected,
                        "Satellite lock acquired: Signal restored to Connected state."
                    );
                }
                return RuleExecutionResult.NoAction();
            }

            if (context.CurrentState == MessageLinkState.Connected)
            {
                DateTime lastHb = context.LastHeartbeatReceivedTime ?? context.CurrentTime;
                TimeSpan elapsedSinceHb = context.CurrentTime - lastHb;

                if (elapsedSinceHb.TotalSeconds >= degradedSec)
                {
                    context.DegradedStateEnteredTime = context.CurrentTime;
                    return RuleExecutionResult.Transition(
                        MessageLinkState.Degraded,
                        $"Satellite signal fading: No heartbeat for {elapsedSinceHb.TotalSeconds:F1}s (threshold: {degradedSec}s)."
                    );
                }
            }
            else if (context.CurrentState == MessageLinkState.Degraded)
            {
                DateTime degradedStart = context.DegradedStateEnteredTime ?? context.CurrentTime;
                TimeSpan elapsedInDegraded = context.CurrentTime - degradedStart;

                if (elapsedInDegraded.TotalSeconds >= closeSec)
                {
                    return RuleExecutionResult.Transition(
                        MessageLinkState.Closed,
                        $"Satellite out of orbital range for {elapsedInDegraded.TotalSeconds:F1}s. Link closed."
                    );
                }
            }

            return RuleExecutionResult.NoAction();
        }
    }

    /// <summary>
    /// Satellite Rule 2: Broadcast Satellite Telemetry every 5 seconds while Connected.
    /// </summary>
    public class SatelliteTelemetryBroadcastRule : RuleBase
    {
        public override string Id => "RULE_SAT_TELEMETRY_BROADCAST";
        public override string Name => "Satellite Telemetry Beacon";
        public override string Description => "Broadcasts orbital telemetry beacon every 5 seconds while Connected.";
        public override string Category => "Outbound Messages";

        public SatelliteTelemetryBroadcastRule()
        {
            AddParameter("TelemetryIntervalSeconds", 5.0);
        }

        public override RuleExecutionResult Evaluate(RuleContext context)
        {
            if (!IsEnabled || context.CurrentState != MessageLinkState.Connected)
                return RuleExecutionResult.NoAction();

            double intervalSec = GetParameter("TelemetryIntervalSeconds", 5.0);

            DateTime lastSent = context.LastHeartbeatSentTime ?? DateTime.MinValue;
            TimeSpan elapsed = context.CurrentTime - lastSent;

            if (elapsed.TotalSeconds >= intervalSec)
            {
                context.LastHeartbeatSentTime = context.CurrentTime;

                var hb = new HeartBeat
                {
                    Sender = "SAT_LEO_09",
                    Receiver = "GROUND_STATION",
                    Command = "HEARTBEAT",
                    DeviceId = "SATCOM_LEO_09",
                    Status = "NOMINAL_ORBIT",
                    Uptime = "84000s",
                    Battery = "94%",
                    ReceivedTime = context.CurrentTime
                };

                return RuleExecutionResult.Send(hb, $"Satellite orbital telemetry beacon sent at {context.CurrentTime:HH:mm:ss.fff}.");
            }

            return RuleExecutionResult.NoAction();
        }
    }
}
