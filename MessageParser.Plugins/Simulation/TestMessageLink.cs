using System;
using MessageParser.Core.Messages;
using MessageParser.Core.Simulation;
using MessageParser.Plugins.Rules;

namespace MessageParser.Plugins.Simulation
{
    public class TestMessageLink : MessageLinkBase
    {
        private readonly Random _random = new Random();

        public override string LinkId => "LINK_TACTICAL_RADIO";
        public override string Name => "Tactical Radio Link";
        public override string Description => "Standard UHF/VHF Tactical Radio Link with Heartbeat, AirTrack, and Low Fuel Rules.";

        public double GeneratorFuelPercent { get; set; } = 85.0;

        public TestMessageLink(ITimeService? timeService = null)
            : base(new LinkStateMachine(timeService))
        {
            // Register standard built-in rules for tactical radio link
            StateMachine.RuleEngine.RegisterRule(new HeartbeatTimeoutRule());
            StateMachine.RuleEngine.RegisterRule(new AutoHeartbeatSendRule());
            StateMachine.RuleEngine.RegisterRule(new AirTrackUpdateRule());
            StateMachine.RuleEngine.RegisterRule(new LowFuelStatusRule());
        }

        public override string GenerateRandomMessage()
        {
            int messageType = _random.Next(5);

            switch (messageType)
            {
                case 0: // AirTrack
                    {
                        string callsign = $"AF{_random.Next(100, 999)}";
                        double lat = 34.0522 + (_random.NextDouble() - 0.5) * 0.1;
                        double lon = -118.2437 + (_random.NextDouble() - 0.5) * 0.1;
                        double alt = _random.Next(28000, 41000);
                        double speed = _random.Next(400, 550);
                        double heading = _random.Next(0, 360);
                        string squawk = _random.Next(1000, 7777).ToString();
                        return $"AWACS -> HQ | AIR_TRACK | callsign={callsign};lat={lat:F4};lon={lon:F4};alt={alt};speed={speed};heading={heading};squawk={squawk}";
                    }

                case 1: // GroundTrack
                    {
                        string unitId = $"T72_{_random.Next(1, 20):D2}";
                        double lat = 34.0532 + (_random.NextDouble() - 0.5) * 0.05;
                        double lon = -118.2447 + (_random.NextDouble() - 0.5) * 0.05;
                        double speed = _random.Next(15, 45);
                        double heading = _random.Next(0, 360);
                        string[] types = { "Tank", "APC", "Infantry", "Truck" };
                        string type = types[_random.Next(types.Length)];
                        return $"SCOUT_01 -> HQ | GROUND_TRACK | unit_id={unitId};lat={lat:F4};lon={lon:F4};speed={speed};heading={heading};type={type}";
                    }

                case 2: // HeartBeat
                    {
                        if (!IsReceivingHeartbeats)
                        {
                            return string.Empty; // Simulated heartbeat loss
                        }

                        string deviceId = $"DRN-{_random.Next(10, 99)}";
                        string[] statuses = { "NOMINAL", "WARNING", "MAINTENANCE" };
                        string status = statuses[_random.Next(statuses.Length)];
                        int uptime = _random.Next(100, 10000);
                        int battery = _random.Next(10, 100);

                        var hb = new HeartBeat
                        {
                            Sender = "DRONE_01",
                            Receiver = "C2",
                            Command = "HEARTBEAT",
                            DeviceId = deviceId,
                            Status = status,
                            Uptime = $"{uptime}s",
                            Battery = $"{battery}%",
                            ReceivedTime = TimeService.Now
                        };
                        StateMachine.ProcessIncomingMessage(hb);

                        return $"DRONE_01 -> C2 | HEARTBEAT | device_id={deviceId};status={status};uptime={uptime}s;battery={battery}%";
                    }

                case 3: // SelfLocation
                    {
                        string soldierId = $"SOLDIER_{_random.Next(1, 10):D2}";
                        double lat = 34.0512 + (_random.NextDouble() - 0.5) * 0.02;
                        double lon = -118.2427 + (_random.NextDouble() - 0.5) * 0.02;
                        double alt = _random.Next(100, 500);
                        string gpsLock = _random.Next(0, 10) > 1 ? "3D" : "2D";
                        double precision = Math.Round(_random.NextDouble() * 3 + 0.5, 1);
                        return $"{soldierId} -> HQ | SELF_LOCATION | lat={lat:F4};lon={lon:F4};alt={alt};gps_lock={gpsLock};precision={precision}m";
                    }

                case 4: // Generator Status
                    {
                        string genId = $"GEN_{_random.Next(1, 5)}";
                        string[] states = { "RUNNING", "IDLE", "OFFLINE" };
                        string state = states[_random.Next(states.Length)];
                        int load = state == "RUNNING" ? _random.Next(40, 95) : 0;
                        double fuel = GeneratorFuelPercent;
                        double temp = state == "RUNNING" ? 65.0 + _random.NextDouble() * 20 : 20.0 + _random.NextDouble() * 5;

                        var genStatus = new GeneratorStatus
                        {
                            Sender = "BASE_GEN",
                            Receiver = "MONITOR",
                            Command = "GENERATOR_STATUS",
                            GenId = genId,
                            State = state,
                            Load = $"{load}%",
                            FuelLevel = $"{fuel:F0}%",
                            Temperature = $"{temp:F1}C",
                            ReceivedTime = TimeService.Now
                        };
                        StateMachine.ProcessIncomingMessage(genStatus);

                        return $"BASE_GEN -> MONITOR | GENERATOR_STATUS | gen_id={genId};state={genStatus.State};load={load}%;fuel={fuel:F0}%;temp={temp:F1}C";
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
