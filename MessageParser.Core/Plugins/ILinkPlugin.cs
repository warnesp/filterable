using System;
using MessageParser.Core.Simulation;

namespace MessageParser.Core.Plugins
{
    public class LinkDescriptor
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public Func<ITimeService?, IMessageLink> Factory { get; }

        public LinkDescriptor(string id, string name, string description, Func<ITimeService?, IMessageLink> factory)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }
    }

    public interface ILinkPlugin
    {
        void RegisterLinks(LinkRegistry registry);
    }
}
