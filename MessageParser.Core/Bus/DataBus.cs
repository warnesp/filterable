using System;
using System.Collections.Generic;
using System.Linq;

namespace MessageParser.Core.Bus
{
    public interface IDataBus
    {
        void Publish<T>(T message) where T : class;
        IDisposable Subscribe<T>(Action<T> action) where T : class;
    }

    public class DataBus : IDataBus
    {
        private readonly List<Subscription> _subscriptions = new List<Subscription>();
        private readonly object _lock = new object();

        public void Publish<T>(T message) where T : class
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            Type messageType = message.GetType();
            List<Subscription> targets;

            lock (_lock)
            {
                // Find all subscriptions where the subscribed type is assignable from the actual message type
                targets = _subscriptions
                    .Where(s => s.SubscribedType.IsAssignableFrom(messageType))
                    .ToList();
            }

            foreach (var sub in targets)
            {
                try
                {
                    sub.Invoke(message);
                }
                catch (Exception)
                {
                    // Prevent one failing subscriber from halting the whole bus execution
                }
            }
        }

        public IDisposable Subscribe<T>(Action<T> action) where T : class
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var sub = new Subscription(typeof(T), obj => action((T)obj), this);

            lock (_lock)
            {
                _subscriptions.Add(sub);
            }

            return sub;
        }

        private void Unsubscribe(Subscription subscription)
        {
            lock (_lock)
            {
                _subscriptions.Remove(subscription);
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
