using System;
using System.Collections.Generic;
using MessageParser.Core.Messages;

namespace MessageParser.Core.Filtering
{
    public class GenericFilterInfo : IFilterInfo
    {
        public Type MessageType { get; }
        public string MessageTypeName { get; }
        public IReadOnlyDictionary<string, Type> FilterableProperties { get; }

        public GenericFilterInfo(Type messageType, string messageTypeName, Dictionary<string, Type> properties)
        {
            MessageType = messageType;
            MessageTypeName = messageTypeName;
            FilterableProperties = properties;
        }
    }

    public abstract class FilterableBase<T> : IFilterable where T : MessageBase
    {
        public T TypedMessage { get; }
        public object WrappedMessage => TypedMessage;
        public Type MessageType => typeof(T);
        public string MessageTypeName => typeof(T).Name;
        public DateTime ReceivedTime => TypedMessage.ReceivedTime;
        public string Sender => TypedMessage.Sender;
        public string Receiver => TypedMessage.Receiver;

        protected FilterableBase(T message)
        {
            TypedMessage = message ?? throw new ArgumentNullException(nameof(message));
        }

        public abstract bool ApplyFilter(string propertyName, Func<object?, bool> predicate);
    }

    public class AirTrackFilterable : FilterableBase<AirTrack>
    {
        public AirTrackFilterable(AirTrack message) : base(message) { }

        public override bool ApplyFilter(string propertyName, Func<object?, bool> predicate)
        {
            if (predicate == null) return true;

            object? value = propertyName.ToUpperInvariant() switch
            {
                "SENDER" => TypedMessage.Sender,
                "RECEIVER" => TypedMessage.Receiver,
                "CALLSIGN" => TypedMessage.Callsign,
                "LATITUDE" => TypedMessage.Latitude,
                "LONGITUDE" => TypedMessage.Longitude,
                "ALTITUDE" => TypedMessage.Altitude,
                "SPEED" => TypedMessage.Speed,
                "HEADING" => TypedMessage.Heading,
                "SQUAWK" => TypedMessage.Squawk,
                _ => null
            };

            return predicate(value);
        }
    }

    public class GroundTrackFilterable : FilterableBase<GroundTrack>
    {
        public GroundTrackFilterable(GroundTrack message) : base(message) { }

        public override bool ApplyFilter(string propertyName, Func<object?, bool> predicate)
        {
            if (predicate == null) return true;

            object? value = propertyName.ToUpperInvariant() switch
            {
                "SENDER" => TypedMessage.Sender,
                "RECEIVER" => TypedMessage.Receiver,
                "UNITID" => TypedMessage.UnitId,
                "LATITUDE" => TypedMessage.Latitude,
                "LONGITUDE" => TypedMessage.Longitude,
                "SPEED" => TypedMessage.Speed,
                "HEADING" => TypedMessage.Heading,
                "TYPE" => TypedMessage.Type,
                _ => null
            };

            return predicate(value);
        }
    }

    public class HeartBeatFilterable : FilterableBase<HeartBeat>
    {
        public HeartBeatFilterable(HeartBeat message) : base(message) { }

        public override bool ApplyFilter(string propertyName, Func<object?, bool> predicate)
        {
            if (predicate == null) return true;

            object? value = propertyName.ToUpperInvariant() switch
            {
                "SENDER" => TypedMessage.Sender,
                "RECEIVER" => TypedMessage.Receiver,
                "DEVICEID" => TypedMessage.DeviceId,
                "STATUS" => TypedMessage.Status,
                "UPTIME" => TypedMessage.Uptime,
                "BATTERY" => TypedMessage.Battery,
                _ => null
            };

            return predicate(value);
        }
    }

    public class SelfLocationFilterable : FilterableBase<SelfLocation>
    {
        public SelfLocationFilterable(SelfLocation message) : base(message) { }

        public override bool ApplyFilter(string propertyName, Func<object?, bool> predicate)
        {
            if (predicate == null) return true;

            object? value = propertyName.ToUpperInvariant() switch
            {
                "SENDER" => TypedMessage.Sender,
                "RECEIVER" => TypedMessage.Receiver,
                "LATITUDE" => TypedMessage.Latitude,
                "LONGITUDE" => TypedMessage.Longitude,
                "ALTITUDE" => TypedMessage.Altitude,
                "GPSLOCK" => TypedMessage.GpsLock,
                "PRECISION" => TypedMessage.Precision,
                _ => null
            };

            return predicate(value);
        }
    }

    public class GeneratorStatusFilterable : FilterableBase<GeneratorStatus>
    {
        public GeneratorStatusFilterable(GeneratorStatus message) : base(message) { }

        public override bool ApplyFilter(string propertyName, Func<object?, bool> predicate)
        {
            if (predicate == null) return true;

            object? value = propertyName.ToUpperInvariant() switch
            {
                "SENDER" => TypedMessage.Sender,
                "RECEIVER" => TypedMessage.Receiver,
                "GENID" => TypedMessage.GenId,
                "STATE" => TypedMessage.State,
                "LOAD" => TypedMessage.Load,
                "FUELLEVEL" => TypedMessage.FuelLevel,
                "TEMPERATURE" => TypedMessage.Temperature,
                _ => null
            };

            return predicate(value);
        }
    }
}
