using System;
using System.Collections.Generic;
using MessageParser.Core.Bus;
using MessageParser.Core.Messages;

namespace MessageParser.Core.Filtering
{
    public class MessageFilterListener<T> : IDisposable where T : MessageBase
    {
        private readonly IDataBus _bus;
        private readonly IDisposable _subscription;
        private readonly Func<T, IFilterable> _wrapperFactory;
        private readonly IFilterInfo _filterInfo;
        private bool _infoPublished;
        private readonly object _lock = new object();

        public MessageFilterListener(IDataBus bus, IFilterInfo filterInfo, Func<T, IFilterable> wrapperFactory)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _filterInfo = filterInfo ?? throw new ArgumentNullException(nameof(filterInfo));
            _wrapperFactory = wrapperFactory ?? throw new ArgumentNullException(nameof(wrapperFactory));
            _subscription = _bus.Subscribe<T>(OnMessageReceived);
        }

        private void OnMessageReceived(T message)
        {
            bool shouldPublishInfo = false;
            lock (_lock)
            {
                if (!_infoPublished)
                {
                    _infoPublished = true;
                    shouldPublishInfo = true;
                }
            }

            if (shouldPublishInfo)
            {
                _bus.Publish(_filterInfo);
            }

            IFilterable filterable = _wrapperFactory(message);
            _bus.Publish(filterable);
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }
    }

    public class FilterBusListenerRegistry : IDisposable
    {
        private readonly List<IDisposable> _listeners = new List<IDisposable>();

        public FilterBusListenerRegistry(IDataBus bus)
        {
            // Register listener for AirTrack
            var airTrackProps = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                { "Sender", typeof(string) },
                { "Receiver", typeof(string) },
                { "Callsign", typeof(string) },
                { "Latitude", typeof(double) },
                { "Longitude", typeof(double) },
                { "Altitude", typeof(double) },
                { "Speed", typeof(double) },
                { "Heading", typeof(double) },
                { "Squawk", typeof(string) }
            };
            _listeners.Add(new MessageFilterListener<AirTrack>(
                bus,
                new GenericFilterInfo(typeof(AirTrack), "AirTrack", airTrackProps),
                msg => new AirTrackFilterable(msg)
            ));

            // Register listener for GroundTrack
            var groundTrackProps = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                { "Sender", typeof(string) },
                { "Receiver", typeof(string) },
                { "UnitId", typeof(string) },
                { "Latitude", typeof(double) },
                { "Longitude", typeof(double) },
                { "Speed", typeof(double) },
                { "Heading", typeof(double) },
                { "Type", typeof(string) }
            };
            _listeners.Add(new MessageFilterListener<GroundTrack>(
                bus,
                new GenericFilterInfo(typeof(GroundTrack), "GroundTrack", groundTrackProps),
                msg => new GroundTrackFilterable(msg)
            ));

            // Register listener for HeartBeat
            var heartbeatProps = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                { "Sender", typeof(string) },
                { "Receiver", typeof(string) },
                { "DeviceId", typeof(string) },
                { "Status", typeof(string) },
                { "Uptime", typeof(string) },
                { "Battery", typeof(string) }
            };
            _listeners.Add(new MessageFilterListener<HeartBeat>(
                bus,
                new GenericFilterInfo(typeof(HeartBeat), "HeartBeat", heartbeatProps),
                msg => new HeartBeatFilterable(msg)
            ));

            // Register listener for SelfLocation
            var selfLocationProps = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                { "Sender", typeof(string) },
                { "Receiver", typeof(string) },
                { "Latitude", typeof(double) },
                { "Longitude", typeof(double) },
                { "Altitude", typeof(double) },
                { "GpsLock", typeof(string) },
                { "Precision", typeof(string) }
            };
            _listeners.Add(new MessageFilterListener<SelfLocation>(
                bus,
                new GenericFilterInfo(typeof(SelfLocation), "SelfLocation", selfLocationProps),
                msg => new SelfLocationFilterable(msg)
            ));

            // Register listener for GeneratorStatus
            var generatorStatusProps = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                { "Sender", typeof(string) },
                { "Receiver", typeof(string) },
                { "GenId", typeof(string) },
                { "State", typeof(string) },
                { "Load", typeof(string) },
                { "FuelLevel", typeof(string) },
                { "Temperature", typeof(string) }
            };
            _listeners.Add(new MessageFilterListener<GeneratorStatus>(
                bus,
                new GenericFilterInfo(typeof(GeneratorStatus), "GeneratorStatus", generatorStatusProps),
                msg => new GeneratorStatusFilterable(msg)
            ));
        }

        public void Dispose()
        {
            foreach (var listener in _listeners)
            {
                listener.Dispose();
            }
            _listeners.Clear();
        }
    }
}
