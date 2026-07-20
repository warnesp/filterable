using System;
using System.Threading;
using System.Threading.Tasks;

namespace MessageParser.Core.Simulation
{
    public class TestMessageLink
    {
        private readonly Random _random = new Random();
        private CancellationTokenSource? _cts;
        private Task? _runTask;

        // Event raised when a new raw message comes "off the wire"
        public event EventHandler<string>? RawMessageReceived;

        // Starts the simulation loop
        public void Start()
        {
            if (_runTask != null) return; // Already running

            _cts = new CancellationTokenSource();
            _runTask = RunSimulationLoopAsync(_cts.Token);
        }

        // Stops the simulation loop
        public void Stop()
        {
            if (_runTask == null) return;

            _cts?.Cancel();
            _runTask = null;
            _cts = null;
        }

        private async Task RunSimulationLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Wait for approximately 500ms
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);

                // Generate a random message
                string rawMessage = GenerateRandomMessage();

                // Raise the event
                RawMessageReceived?.Invoke(this, rawMessage);
            }
        }

        private string GenerateRandomMessage()
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
                        string deviceId = $"DRN-{_random.Next(10, 99)}";
                        string[] statuses = { "NOMINAL", "WARNING", "MAINTENANCE" };
                        string status = statuses[_random.Next(statuses.Length)];
                        int uptime = _random.Next(100, 10000);
                        int battery = _random.Next(10, 100);
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
                        int fuel = _random.Next(20, 100);
                        double temp = state == "RUNNING" ? 65.0 + _random.NextDouble() * 20 : 20.0 + _random.NextDouble() * 5;
                        return $"BASE_GEN -> MONITOR | GENERATOR_STATUS | gen_id={genId};state={state};load={load}%;fuel={fuel}%;temp={temp:F1}C";
                    }

                default:
                    return string.Empty;
            }
        }
    }
}
