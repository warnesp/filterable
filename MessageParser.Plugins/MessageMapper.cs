using System;
using System.Globalization;
using MessageParser.Core;
using MessageParser.Core.Messages;

namespace MessageParser.Plugins
{
    public static class MessageMapper
    {
        public static MessageBase? ToTypedMessage(this ParsedMessage parsed)
        {
            if (parsed == null || !parsed.IsValid) return null;

            switch (parsed.Command.ToUpperInvariant())
            {
                case "AIR_TRACK":
                case "AIRTRACK":
                    return new AirTrack
                    {
                        Sender = parsed.Sender,
                        Receiver = parsed.Receiver,
                        Command = parsed.Command,
                        Callsign = parsed.Payload.GetValueOrDefault("callsign"),
                        Latitude = ParseDouble(parsed.Payload.GetValueOrDefault("lat")),
                        Longitude = ParseDouble(parsed.Payload.GetValueOrDefault("lon")),
                        Altitude = ParseDouble(parsed.Payload.GetValueOrDefault("alt")),
                        Speed = ParseDouble(parsed.Payload.GetValueOrDefault("speed")),
                        Heading = ParseDouble(parsed.Payload.GetValueOrDefault("heading")),
                        Squawk = parsed.Payload.GetValueOrDefault("squawk")
                    };

                case "GROUND_TRACK":
                case "GROUNDTRACK":
                    return new GroundTrack
                    {
                        Sender = parsed.Sender,
                        Receiver = parsed.Receiver,
                        Command = parsed.Command,
                        UnitId = parsed.Payload.GetValueOrDefault("unit_id"),
                        Latitude = ParseDouble(parsed.Payload.GetValueOrDefault("lat")),
                        Longitude = ParseDouble(parsed.Payload.GetValueOrDefault("lon")),
                        Speed = ParseDouble(parsed.Payload.GetValueOrDefault("speed")),
                        Heading = ParseDouble(parsed.Payload.GetValueOrDefault("heading")),
                        Type = parsed.Payload.GetValueOrDefault("type")
                    };

                case "HEARTBEAT":
                    return new HeartBeat
                    {
                        Sender = parsed.Sender,
                        Receiver = parsed.Receiver,
                        Command = parsed.Command,
                        DeviceId = parsed.Payload.GetValueOrDefault("device_id"),
                        Status = parsed.Payload.GetValueOrDefault("status"),
                        Uptime = parsed.Payload.GetValueOrDefault("uptime"),
                        Battery = parsed.Payload.GetValueOrDefault("battery")
                    };

                case "SELF_LOCATION":
                case "SELFLOCATION":
                    return new SelfLocation
                    {
                        Sender = parsed.Sender,
                        Receiver = parsed.Receiver,
                        Command = parsed.Command,
                        Latitude = ParseDouble(parsed.Payload.GetValueOrDefault("lat")),
                        Longitude = ParseDouble(parsed.Payload.GetValueOrDefault("lon")),
                        Altitude = ParseDouble(parsed.Payload.GetValueOrDefault("alt")),
                        GpsLock = parsed.Payload.GetValueOrDefault("gps_lock"),
                        Precision = parsed.Payload.GetValueOrDefault("precision")
                    };

                case "GENERATOR_STATUS":
                case "GENERATORSTATUS":
                    return new GeneratorStatus
                    {
                        Sender = parsed.Sender,
                        Receiver = parsed.Receiver,
                        Command = parsed.Command,
                        GenId = parsed.Payload.GetValueOrDefault("gen_id"),
                        State = parsed.Payload.GetValueOrDefault("state"),
                        Load = parsed.Payload.GetValueOrDefault("load"),
                        FuelLevel = parsed.Payload.GetValueOrDefault("fuel"),
                        Temperature = parsed.Payload.GetValueOrDefault("temp")
                    };

                default:
                    return null;
            }
        }

        private static double ParseDouble(string? value)
        {
            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
            {
                return result;
            }
            return 0.0;
        }

        private static string GetValueOrDefault(this System.Collections.Generic.Dictionary<string, string> dict, string key)
        {
            return dict.TryGetValue(key, out var val) ? val : string.Empty;
        }
    }
}
