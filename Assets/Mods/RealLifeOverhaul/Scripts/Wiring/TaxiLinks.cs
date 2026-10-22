using System;
using System.Collections.Generic;
using Helpers;
using RealLifeOverhaul.Nav;
using UI.Guiders;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The taxi called from the phone (TaxiBridges) with the holds, the bus, the weather and the GPS's road graph, and
        /// its mark on the minimap. The mark is read when the minimap gathers its places (every two seconds while it
        /// shows); one static read while no taxi is out.
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

            private static void Mark(List<NavMark> marks)
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
                catch (Exception e)
                {
                    Fault("TaxiLinks.Mark", e);
                }
            }
        }
    }
}
