using System;
using System.Collections.Generic;
using MessageParser.Core;
using MessageParser.Core.Filtering;
using MessageParser.Core.Messages;

namespace MessageParser.Plugins
{
    public class BaseMessagesPlugin : IMessagePlugin
    {
        public void Initialize(IMessageRegistry registry)
        {
            // AirTrack
            registry.RegisterSchema(new MessageSchema(
                new AirTrackFilterInfo(),
                msg =>
                {
                    var air = (AirTrack)msg;
                    return $"Callsign: {air.Callsign}, Alt: {air.Altitude}ft, Speed: {air.Speed}kts";
                },
                msg =>
                {
                    var air = (AirTrack)msg;
                    return new[]
                    {
                        new PayloadField("Callsign", air.Callsign),
                        new PayloadField("Latitude", air.Latitude.ToString("F6")),
                        new PayloadField("Longitude", air.Longitude.ToString("F6")),
                        new PayloadField("Altitude", $"{air.Altitude} ft"),
                        new PayloadField("Speed", $"{air.Speed} kts"),
                        new PayloadField("Heading", $"{air.Heading}°"),
                        new PayloadField("Squawk", air.Squawk)
                    };
                }
            ));
            registry.RegisterParser("AIR_TRACK", parsed => parsed.ToTypedMessage()!);
            registry.RegisterParser("AIRTRACK", parsed => parsed.ToTypedMessage()!);

            // GroundTrack
            registry.RegisterSchema(new MessageSchema(
                new GroundTrackFilterInfo(),
                msg =>
                {
                    var ground = (GroundTrack)msg;
                    return $"Unit: {ground.UnitId}, Type: {ground.Type}, Speed: {ground.Speed}mph";
                },
                msg =>
                {
                    var ground = (GroundTrack)msg;
                    return new[]
                    {
                        new PayloadField("Unit ID", ground.UnitId),
                        new PayloadField("Latitude", ground.Latitude.ToString("F6")),
                        new PayloadField("Longitude", ground.Longitude.ToString("F6")),
                        new PayloadField("Speed", $"{ground.Speed} mph"),
                        new PayloadField("Heading", $"{ground.Heading}°"),
                        new PayloadField("Type", ground.Type)
                    };
                }
            ));
            registry.RegisterParser("GROUND_TRACK", parsed => parsed.ToTypedMessage()!);
            registry.RegisterParser("GROUNDTRACK", parsed => parsed.ToTypedMessage()!);

            // HeartBeat
            registry.RegisterSchema(new MessageSchema(
                new HeartBeatFilterInfo(),
                msg =>
                {
                    var hb = (HeartBeat)msg;
                    return $"Device: {hb.DeviceId}, Status: {hb.Status}, Battery: {hb.Battery}";
                },
                msg =>
                {
                    var hb = (HeartBeat)msg;
                    return new[]
                    {
                        new PayloadField("Device ID", hb.DeviceId),
                        new PayloadField("Status", hb.Status),
                        new PayloadField("Uptime", hb.Uptime),
                        new PayloadField("Battery", hb.Battery)
                    };
                }
            ));
            registry.RegisterParser("HEARTBEAT", parsed => parsed.ToTypedMessage()!);

            // SelfLocation
            registry.RegisterSchema(new MessageSchema(
                new SelfLocationFilterInfo(),
                msg =>
                {
                    var self = (SelfLocation)msg;
                    return $"GPS: {self.GpsLock}, Lat/Lon: {self.Latitude:F3}/{self.Longitude:F3}, Alt: {self.Altitude}m";
                },
                msg =>
                {
                    var self = (SelfLocation)msg;
                    return new[]
                    {
                        new PayloadField("Latitude", self.Latitude.ToString("F6")),
                        new PayloadField("Longitude", self.Longitude.ToString("F6")),
                        new PayloadField("Altitude", $"{self.Altitude} m"),
                        new PayloadField("GPS Lock", self.GpsLock),
                        new PayloadField("Precision", self.Precision)
                    };
                }
            ));
            registry.RegisterParser("SELF_LOCATION", parsed => parsed.ToTypedMessage()!);
            registry.RegisterParser("SELFLOCATION", parsed => parsed.ToTypedMessage()!);

            // GeneratorStatus
            registry.RegisterSchema(new MessageSchema(
                new GeneratorStatusFilterInfo(),
                msg =>
                {
                    var gen = (GeneratorStatus)msg;
                    return $"Gen: {gen.GenId}, State: {gen.State}, Load: {gen.Load}, Fuel: {gen.FuelLevel}";
                },
                msg =>
                {
                    var gen = (GeneratorStatus)msg;
                    return new[]
                    {
                        new PayloadField("Generator ID", gen.GenId),
                        new PayloadField("State", gen.State),
                        new PayloadField("Load", gen.Load),
                        new PayloadField("Fuel Level", gen.FuelLevel),
                        new PayloadField("Temperature", gen.Temperature)
                    };
                }
            ));
            registry.RegisterParser("GENERATOR_STATUS", parsed => parsed.ToTypedMessage()!);
            registry.RegisterParser("GENERATORSTATUS", parsed => parsed.ToTypedMessage()!);

            // Register Evaluators
            foreach (var evaluator in GeneratedMessageEvaluators.GetAll())
            {
                registry.RegisterEvaluator(evaluator);
            }
        }
    }
}
