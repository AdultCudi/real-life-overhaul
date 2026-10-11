using Helpers;
using RealLifeOverhaul.Nav;
using UI.Guiders;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The taxi called from the phone with the rest of the mod (Transport/Taxi/TaxiRoads.cs has TaxiBridges):
        ///
        ///   Held      the player is held and it is not their doing: the police have business with them (a stop, an
        ///             arrest, custody: Crime, Law), a driving test is on (Licences), or they are in prison (Justice).
        ///             No taxi is sent; one that is out is let go with nothing charged
        ///   Riding    the player is on a bus (Transport): no taxi is sent; one that is out counts as cancelled
        ///   Wet       rain or a storm of Events/Weather, while "Weather that matters" is on: the call-out is dearer
        ///             and the taxi starts further off
        ///   Roads     the road graph the GPS has made (Navigation), searched without making it a second time
        ///
        /// and with the minimap (Navigation/NavBridges.MoreMarks): the called taxi is marked where it is, with the
        /// game's own vehicle picture in the colour the game gives its private driver. Read when the minimap gathers
        /// its places (every two seconds while it shows); one static read while no taxi is out.
        /// </summary>
        public static class TaxiLinks
        {
            internal static void Connect()
            {
                TaxiBridges.Held = () => PoliceAttention.IsBusy || LicenceDirector.IsActive || Justice.InPrison;
                TaxiBridges.Riding = () => TransitDirector.Riding;
                TaxiBridges.Wet = () => Settings.WeatherMatters && Events.Weather.Sky >= Events.Sky.Rain;
                TaxiBridges.Roads = () => NavDirector.Roads;
                NavBridges.MoreMarks = Mark;
            }

            internal static void Disconnect()
            {
                TaxiBridges.Held = null;
                TaxiBridges.Riding = null;
                TaxiBridges.Wet = null;
                TaxiBridges.Roads = null;
                NavBridges.MoreMarks = null;
            }

            private static void Mark(System.Collections.Generic.List<NavMark> marks)
            {
                try
                {
                    if (!TaxiDirector.Where(out Vector3 at))
                    {
                        return;
                    }
                    GlobalReferences references = InstanceBehavior<GlobalReferences>.Instance;
                    Color32 colour = InstanceBehavior<GuidersManager>.Instance != null ? (Color32)GuidersManager.GetGuiderColor(DirectionGuiderType.PrivateDriver) : new Color32(255, 255, 255, 255);
                    colour.a = 255;
                    marks.Add(new NavMark { X = at.x, Z = at.z, Icon = references != null ? references.vehiclePOIIcon : null, Colour = colour });
                }
                catch (System.Exception e)
                {
                    Fault("TaxiLinks.Mark", e);
                }
            }
        }
    }
}
