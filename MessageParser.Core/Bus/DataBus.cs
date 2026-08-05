using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MessageParser.Core.Bus
{
    public class DataBusExceptionEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public object Message { get; }
        public Type SubscribedType { get; }

        public DataBusExceptionEventArgs(Exception exception, object message, Type subscribedType)
        {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            SubscribedType = subscribedType ?? throw new ArgumentNullException(nameof(subscribedType));
        }
    }

    public interface IDataBus
    {
        event EventHandler<DataBusExceptionEventArgs>? UnhandledException;
        void Publish<T>(T message) where T : class;
        IDisposable Subscribe<T>(Action<T> action) where T : class;
    }

    public class DataBus : IDataBus
    {
        private readonly List<Subscription> _subscriptions = new List<Subscription>();
        private readonly ConcurrentDictionary<Type, Subscription[]> _dispatchCache = new ConcurrentDictionary<Type, Subscription[]>();
        private readonly object _lock = new object();

        public event EventHandler<DataBusExceptionEventArgs>? UnhandledException;

        public void Publish<T>(T message) where T : class
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            Type messageType = message.GetType();
            Subscription[] targets = _dispatchCache.GetOrAdd(messageType, BuildDispatchArray);

            foreach (var sub in targets)
            {
                try
                {
                    sub.Invoke(message);
                }
                catch (Exception ex)
                {
                    // Prevent one failing subscriber from halting the whole bus execution
                    // and notify subscribers of the unhandled exception telemetry
                    UnhandledException?.Invoke(this, new DataBusExceptionEventArgs(ex, message, sub.SubscribedType));
                }
            }
        }

        private Subscription[] BuildDispatchArray(Type messageType)
        {
            lock (_lock)
            {
                return _subscriptions
                    .Where(s => s.SubscribedType.IsAssignableFrom(messageType))
                    .ToArray();
            }
        }

        public IDisposable Subscribe<T>(Action<T> action) where T : class
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var sub = new Subscription(typeof(T), obj => action((T)obj), this);

            lock (_lock)
            {
                _subscriptions.Add(sub);
                _dispatchCache.Clear();
            }

            return sub;
        }

        private void Unsubscribe(Subscription subscription)
        {
            lock (_lock)
            {
                if (_subscriptions.Remove(subscription))
                {
                    _dispatchCache.Clear();
                }
            }
        }

        private class Subscription : IDisposable
        {
            public Type SubscribedType { get; }
            private readonly Action<object> _handler;
            private readonly DataBus _bus;
            private bool _disposed;

            public Subscription(Type subscribedType, Action<object> handler, DataBus bus)
            {
                SubscribedType = subscribedType;
                _handler = handler;
                _bus = bus;
            }

            public void Invoke(object message)
            {
                _handler(message);
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _bus.Unsubscribe(this);
                    _disposed = true;
                }
            }
        }
    }
}
