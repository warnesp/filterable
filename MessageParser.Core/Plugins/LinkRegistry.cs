using System;
using System.Collections.Generic;
using System.Linq;
using MessageParser.Core.Simulation;

namespace MessageParser.Core.Plugins
{
    public class LinkRegistry
    {
        private readonly List<LinkDescriptor> _descriptors = new List<LinkDescriptor>();
        private readonly object _lock = new object();

        public IReadOnlyList<LinkDescriptor> Descriptors
        {
            get
            {
                lock (_lock)
                {
                    return _descriptors.ToList();
                }
            }
        }

        public void RegisterLink(LinkDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));

            lock (_lock)
            {
                if (!_descriptors.Any(d => d.Id.Equals(descriptor.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    _descriptors.Add(descriptor);
                }
            }
        }

        public IMessageLink CreateLink(string linkId, ITimeService? timeService = null)
        {
            LinkDescriptor? descriptor;
            lock (_lock)
            {
                descriptor = _descriptors.FirstOrDefault(d => d.Id.Equals(linkId, StringComparison.OrdinalIgnoreCase));
            }

            if (descriptor == null)
            {
                throw new InvalidOperationException($"No message link plugin registered with ID '{linkId}'.");
            }

            return descriptor.Factory(timeService);
        }
    }
}
