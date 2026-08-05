using MessageParser.Core.Plugins;

namespace MessageParser.Plugins.Satellite
{
    public class SatelliteLinkPlugin : ILinkPlugin
    {
        public void RegisterLinks(LinkRegistry registry)
        {
            registry.RegisterLink(new LinkDescriptor(
                "LINK_SATELLITE_LEO",
                "LEO Satellite Link",
                "Low Earth Orbit Satellite Communications Link with Orbital Pass and Solar Telemetry rules.",
                timeService => new SatelliteMessageLink(timeService)
            ));
        }
    }
}
