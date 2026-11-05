using System;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Ui;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The city map's place card (Ui/Map, MapBridges): what Flare in the city knows of the business at a place, its rating, its reviews and its arrivals. With Flare's
        /// residents' side off the card has none and shows only what the building itself says. A fault is one line in the log and no facts.
        /// </summary>
        public static class MapLinks
        {
            internal static void Connect()
            {
                MapBridges.Flare = FlareFacts;
            }

            internal static void Disconnect()
            {
                MapBridges.Flare = null;
            }

            private static MapFlareFacts FlareFacts(string place)
            {
                try
                {
                    FlareDirector flare = FlareDirector.Instance;
                    return flare != null && flare.Running ? flare.PlaceFacts(place) : default;
                }
                catch (Exception e)
                {
                    Fault("MapLinks.FlareFacts", e);
                    return default;
                }
            }
        }
    }
}
