using MessageParser.Core.Plugins;
using MessageParser.Plugins.Simulation;

namespace MessageParser.Plugins
{
    public class StandardTacticalLinkPlugin : ILinkPlugin
    {
        public void RegisterLinks(LinkRegistry registry)
        {
            registry.RegisterLink(new LinkDescriptor(
                "LINK_TACTICAL_RADIO",
                "Tactical Radio Link",
                "Standard UHF/VHF Tactical Radio Link with Heartbeat, AirTrack, and Low Fuel Rules.",
                timeService => new TestMessageLink(timeService)
            ));
        }
    }
}
