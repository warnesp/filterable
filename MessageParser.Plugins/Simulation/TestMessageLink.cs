using System;
using System.Threading;
using System.Threading.Tasks;
using MessageParser.Core.Messages;
using MessageParser.Core.Rules;
using MessageParser.Core.Simulation;
using MessageParser.Plugins.Rules;

namespace MessageParser.Plugins.Simulation
{
    public class TestMessageLink
    {
        private readonly Random _random = new Random();
        private CancellationTokenSource? _cts;
        private Task? _runTask;

        public LinkStateMachine StateMachine { get; }
        public ITimeService TimeService => StateMachine.TimeService;

        // Configuration / Controls for simulation testing
        public bool IsReceivingHeartbeats { get; set; } = true;
        public double GeneratorFuelPercent { get; set; } = 85.0;

        // Event raised when a raw string message or typed message comes "off the wire"
        public event EventHandler<string>? RawMessageReceived;
        public event EventHandler<MessageBase>? MessageReceived;

        public TestMessageLink(ITimeService? timeService = null)
        {
            StateMachine = new LinkStateMachine(timeService);
            
            // Register standard built-in rules
            StateMachine.RuleEngine.RegisterRule(new HeartbeatTimeoutRule());
            StateMachine.RuleEngine.RegisterRule(new AutoHeartbeatSendRule());
            StateMachine.RuleEngine.RegisterRule(new AirTrackUpdateRule());
            StateMachine.RuleEngine.RegisterRule(new LowFuelStatusRule());

            StateMachine.OutboundMessageGenerated += OnRuleOutboundMessageGenerated;
        }

        private void OnRuleOutboundMessageGenerated(object? sender, MessageBase message)
        {
            MessageReceived?.Invoke(this, message);
            string formattedRaw = FormatMessageToRaw(message);
            if (!string.IsNullOrEmpty(formattedRaw))
            {
                RawMessageReceived?.Invoke(this, formattedRaw);
            }
        }

        // Starts the simulation loop
        public void Start()
        {
            if (_runTask != null) return; // Already running

            StateMachine.Start();
            _cts = new CancellationTokenSource();
            _runTask = RunSimulationLoopAsync(_cts.Token);
        }

        // Stops the simulation loop
        public void Stop()
        {
            StateMachine.Stop();
            if (_runTask == null) return;

            _cts?.Cancel();
            _runTask = null;
            _cts = null;
        }

        private async Task RunSimulationLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Delay using virtual time service (e.g. 500ms in simulated time)
                await TimeService.DelayAsync(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);

                // Generate a random incoming message if link is not closed
                if (StateMachine.CurrentState != MessageLinkState.Closed)
                {
                    string rawMessage = GenerateRandomMessage();
                    if (!string.IsNullOrEmpty(rawMessage))
                    {
                        RawMessageReceived?.Invoke(this, rawMessage);
                    }
                }
            }
        }

        public string GenerateRandomMessage()
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

                        return $"BASE_GEN -> MONITOR | GENERATOR_STATUS | gen_id={genId};state={genStatus.State};load={load}%;fuel={genStatus.FuelLevel};temp={temp:F1}C";
                    }

                default:
                    return string.Empty;
            }
        }

        private string FormatMessageToRaw(MessageBase msg)
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
