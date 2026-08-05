using System;
using MessageParser.Core.Messages;
using MessageParser.Core.Simulation;

namespace MessageParser.Plugins.Satellite
{
    public class SatelliteMessageLink : MessageLinkBase
    {
        private readonly Random _random = new Random();

        public override string LinkId => "LINK_SATELLITE_LEO";
        public override string Name => "LEO Satellite Link";
        public override string Description => "Low Earth Orbit Satellite Communications Link with Orbital Pass and Solar Telemetry rules.";

        public SatelliteMessageLink(ITimeService? timeService = null)
            : base(new SatelliteLinkStateMachine(timeService))
        {
        }

        public override string GenerateRandomMessage()
        {
            int type = _random.Next(3);
            switch (type)
            {
                case 0:
                    {
                        string satId = $"SAT_LEO_{_random.Next(1, 12):D2}";
                        double lat = (_random.NextDouble() - 0.5) * 180;
                        double lon = (_random.NextDouble() - 0.5) * 360;
                        double alt = 550 + _random.NextDouble() * 50;
                        double speed = 7.5; // km/s
                        string squawk = "7001";
                        return $"{satId} -> GROUND | AIR_TRACK | callsign={satId};lat={lat:F4};lon={lon:F4};alt={alt:F0};speed={speed:F1};heading=180;squawk={squawk}";
                    }
                case 1:
                    {
                        if (!IsReceivingHeartbeats) return string.Empty;

                        string satId = $"SAT_LEO_{_random.Next(1, 12):D2}";
                        int battery = _random.Next(20, 100);
                        var hb = new HeartBeat
                        {
                            Sender = satId,
                            Receiver = "GROUND_STATION",
                            Command = "HEARTBEAT",
                            DeviceId = satId,
                            Status = battery < 25 ? "ECLIPSE_LOW_POWER" : "NOMINAL",
                            Uptime = "142000s",
                            Battery = $"{battery}%",
                            ReceivedTime = TimeService.Now
                        };
                        StateMachine.ProcessIncomingMessage(hb);

                        return $"{satId} -> GROUND_STATION | HEARTBEAT | device_id={satId};status={hb.Status};uptime=142000s;battery={battery}%";
                    }
                case 2:
                    {
                        string genId = "SAT_SOLAR_ARRAY";
                        double charge = 95.0;
                        var genStatus = new GeneratorStatus
                        {
                            Sender = "SAT_POWER",
                            Receiver = "TELEMETRY",
                            Command = "GENERATOR_STATUS",
                            GenId = genId,
                            State = "CHARGING",
                            Load = "42%",
                            FuelLevel = $"{charge:F0}%",
                            Temperature = "-15.0C",
                            ReceivedTime = TimeService.Now
                        };
                        StateMachine.ProcessIncomingMessage(genStatus);

                        return $"SAT_POWER -> TELEMETRY | GENERATOR_STATUS | gen_id={genId};state={genStatus.State};load=42%;fuel={charge:F0}%;temp=-15.0C";
                    }
                default:
                    return string.Empty;
            }
        }

        protected override string FormatMessageToRaw(MessageBase msg)
        {
            if (msg is HeartBeat hb)
            {
                return $"{hb.Sender} -> {hb.Receiver} | HEARTBEAT | device_id={hb.DeviceId};status={hb.Status};uptime={hb.Uptime};battery={hb.Battery}";
            }
            if (msg is AirTrack at)
            {
                return $"{at.Sender} -> {at.Receiver} | AIR_TRACK | callsign={at.Callsign};lat={at.Latitude:F4};lon={at.Longitude:F4};alt={at.Altitude};speed={at.Speed};heading={at.Heading};squawk={at.Squawk}";
            }
            if (msg is GeneratorStatus gs)
            {
                return $"{gs.Sender} -> {gs.Receiver} | GENERATOR_STATUS | gen_id={gs.GenId};state={gs.State};load={gs.Load};fuel={gs.FuelLevel};temp={gs.Temperature}";
            }
            return string.Empty;
        }
    }
}
