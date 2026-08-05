using System;
using System.Collections.Generic;
using MessageParser.Core.Messages;
using MessageParser.Core.Rules;
using MessageParser.Core.Simulation;

namespace MessageParser.Plugins.Rules
{
    public abstract class RuleBase : IRule
    {
        private readonly Dictionary<string, object> _parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract string Category { get; }
        public bool IsEnabled { get; set; } = true;

        public IReadOnlyDictionary<string, object> Parameters => _parameters;

        protected void AddParameter(string key, object defaultValue)
        {
            _parameters[key] = defaultValue;
        }

        public void SetParameter(string key, object value)
        {
            if (_parameters.ContainsKey(key))
            {
                _parameters[key] = value;
            }
        }

        protected T GetParameter<T>(string key, T defaultValue)
        {
            if (_parameters.TryGetValue(key, out object? val))
            {
                try
                {
                    return (T)Convert.ChangeType(val, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        public abstract RuleExecutionResult Evaluate(RuleContext context);
    }

    /// <summary>
    /// Rule 1: Heartbeat missing for 2s -> Degraded; in Degraded for 5s -> Closed.
    /// </summary>
    public class HeartbeatTimeoutRule : RuleBase
    {
        public override string Id => "RULE_HB_TIMEOUT";
        public override string Name => "Heartbeat Timeout State Control";
        public override string Description => "Degrades link if no heartbeat received for 2 seconds. Closes link if in Degraded state for 5 seconds.";
        public override string Category => "Link State";

        public HeartbeatTimeoutRule()
        {
            AddParameter("HeartbeatDegradedTimeoutSeconds", 2.0);
            AddParameter("DegradedCloseTimeoutSeconds", 5.0);
        }

        public override RuleExecutionResult Evaluate(RuleContext context)
        {
            if (!IsEnabled) return RuleExecutionResult.NoAction();

            double degradedSec = GetParameter("HeartbeatDegradedTimeoutSeconds", 2.0);
            double closeSec = GetParameter("DegradedCloseTimeoutSeconds", 5.0);

            // If incoming message is a HeartBeat, handle state recovery
            if (context.IncomingMessage is HeartBeat)
            {
                context.LastHeartbeatReceivedTime = context.CurrentTime;
                if (context.CurrentState == MessageLinkState.Degraded)
                {
                    return RuleExecutionResult.Transition(
                        MessageLinkState.Connected,
                        "Heartbeat received: Link restored from Degraded to Connected state."
                    );
                }
                return RuleExecutionResult.NoAction();
            }

            // Check timeouts
            if (context.CurrentState == MessageLinkState.Connected)
            {
                DateTime lastHb = context.LastHeartbeatReceivedTime ?? context.CurrentTime;
                TimeSpan elapsedSinceHb = context.CurrentTime - lastHb;

                if (elapsedSinceHb.TotalSeconds >= degradedSec)
                {
                    context.DegradedStateEnteredTime = context.CurrentTime;
                    return RuleExecutionResult.Transition(
                        MessageLinkState.Degraded,
                        $"No heartbeat received for {elapsedSinceHb.TotalSeconds:F1}s (threshold: {degradedSec}s). Transitioning link to Degraded state."
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
                        $"Link remained in Degraded state for {elapsedInDegraded.TotalSeconds:F1}s (threshold: {closeSec}s) without a heartbeat. Closing link."
                    );
                }
            }

            return RuleExecutionResult.NoAction();
        }
    }

    /// <summary>
    /// Rule 2: While link is Connected, send a heartbeat every 1 second.
    /// </summary>
    public class AutoHeartbeatSendRule : RuleBase
    {
        public override string Id => "RULE_AUTO_HB_SEND";
        public override string Name => "Outbound Heartbeat Transmission";
        public override string Description => "Sends an outbound heartbeat every 1 second while link is Connected.";
        public override string Category => "Outbound Messages";

        public AutoHeartbeatSendRule()
        {
            AddParameter("HeartbeatSendIntervalSeconds", 1.0);
        }

        public override RuleExecutionResult Evaluate(RuleContext context)
        {
            if (!IsEnabled || context.CurrentState != MessageLinkState.Connected)
                return RuleExecutionResult.NoAction();

            double intervalSec = GetParameter("HeartbeatSendIntervalSeconds", 1.0);

            DateTime lastSent = context.LastHeartbeatSentTime ?? DateTime.MinValue;
            TimeSpan elapsed = context.CurrentTime - lastSent;

            if (elapsed.TotalSeconds >= intervalSec)
            {
                context.LastHeartbeatSentTime = context.CurrentTime;

                var hb = new HeartBeat
                {
                    Sender = "LINK_C2",
                    Receiver = "HQ",
                    Command = "HEARTBEAT",
                    DeviceId = "LINK_SYS_01",
                    Status = "NOMINAL",
                    Uptime = "100s",
                    Battery = "98%",
                    ReceivedTime = context.CurrentTime
                };

                return RuleExecutionResult.Send(hb, $"Automated 1s Heartbeat sent at {context.CurrentTime:HH:mm:ss.fff}.");
            }

            return RuleExecutionResult.NoAction();
        }
    }

    /// <summary>
    /// Rule 3: Update AirTrack status every 10 seconds while Connected.
    /// </summary>
    public class AirTrackUpdateRule : RuleBase
    {
        private static readonly Random _rnd = new Random();

        public override string Id => "RULE_AIRTRACK_UPDATE";
        public override string Name => "AirTrack Status Periodic Update";
        public override string Description => "Broadcasts an updated AirTrack status every 10 seconds while link is Connected.";
        public override string Category => "Outbound Messages";

        public AirTrackUpdateRule()
        {
            AddParameter("AirTrackUpdateIntervalSeconds", 10.0);
        }

        public override RuleExecutionResult Evaluate(RuleContext context)
        {
            if (!IsEnabled || context.CurrentState != MessageLinkState.Connected)
                return RuleExecutionResult.NoAction();

            double intervalSec = GetParameter("AirTrackUpdateIntervalSeconds", 10.0);

            DateTime lastSent = context.LastAirTrackSentTime ?? DateTime.MinValue;
            TimeSpan elapsed = context.CurrentTime - lastSent;

            if (elapsed.TotalSeconds >= intervalSec)
            {
                context.LastAirTrackSentTime = context.CurrentTime;

                var airTrack = new AirTrack
                {
                    Sender = "AWACS_01",
                    Receiver = "C2",
                    Command = "AIR_TRACK",
                    Callsign = "E3-AWACS",
                    Latitude = 34.0522 + (_rnd.NextDouble() - 0.5) * 0.05,
                    Longitude = -118.2437 + (_rnd.NextDouble() - 0.5) * 0.05,
                    Altitude = 32000,
                    Speed = 480,
                    Heading = 270,
                    Squawk = "4721",
                    ReceivedTime = context.CurrentTime
                };

                return RuleExecutionResult.Send(airTrack, $"Periodic 10s AirTrack status update broadcasted.");
            }

            return RuleExecutionResult.NoAction();
        }
    }

    /// <summary>
    /// Rule 4: When fuel level < 20%, change status to "LowFuel".
    /// </summary>
    public class LowFuelStatusRule : RuleBase
    {
        public override string Id => "RULE_LOW_FUEL";
        public override string Name => "Low Fuel Telemetry Alert";
        public override string Description => "Monitors fuel telemetry. When fuel falls below 20%, changes generator/vehicle status to 'LowFuel'.";
        public override string Category => "Telemetry & Status";

        public LowFuelStatusRule()
        {
            AddParameter("LowFuelThresholdPercent", 20.0);
        }

        public override RuleExecutionResult Evaluate(RuleContext context)
        {
            if (!IsEnabled) return RuleExecutionResult.NoAction();

            double threshold = GetParameter("LowFuelThresholdPercent", 20.0);

            if (context.IncomingMessage is GeneratorStatus genStatus)
            {
                if (double.TryParse(genStatus.FuelLevel.Replace("%", "").Trim(), out double fuelVal))
                {
                    if (fuelVal < threshold && genStatus.State != "LowFuel")
                    {
                        string oldState = genStatus.State;
                        genStatus.State = "LowFuel";
                        return RuleExecutionResult.Send(genStatus, $"Fuel level ({fuelVal}%) is below {threshold}%. Generator state updated from '{oldState}' to 'LowFuel'.");
                    }
                }
            }

            return RuleExecutionResult.NoAction();
        }
    }
}
