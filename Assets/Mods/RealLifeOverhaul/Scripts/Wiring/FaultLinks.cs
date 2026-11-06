using System;
using RealLifeOverhaul.Faults;
using RealLifeOverhaul.Flare;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The city's real faults with Flare's City Office and its crews, inspections, street crime, the litter mood,
        /// footfall, passers-by, potholes and reported crimes. Nothing per frame: asked when the faults change, on their
        /// timer, or on a report.
        /// </summary>
        public static class FaultLinks
        {
            private const int CleanUnknown = -1;

            private static Func<BuildingRegistration, int> _ambientBefore;

            internal static void Connect()
            {
                FlareBridges.FaultCheck = Check;
                FlareBridges.FaultActed = (caseId, fault, works) => FaultDirector.Instance?.Acted(caseId, fault, works);
                FlareBridges.FaultDone = caseId => FaultDirector.Instance?.Done(caseId);
                FlareBridges.FaultsReal = () => FaultDirector.Real;
                FaultBridges.CityReport = CityReport;
                FaultBridges.Cleanliness = Cleanliness;
                FaultBridges.CrimeScale = CityWatch.Dark;
                FaultBridges.LitterMood = hours =>
                {
                    if (Settings.Civic)
                    {
                        CivicEffects.Keep(ModState.CivicLitter, hours);
                    }
                };
                FaultBridges.Footfall = Footfall;
                FaultBridges.Trip = (at, within, forward) => Settings.Ragdolls && PedestrianIncidents.Instance != null && PedestrianIncidents.Instance.Trip(at, within, forward);
                CarOwnership.RoadHit = FaultDirector.RoadHit;
                _ambientBefore = BusinessGame.Ambient;
                BusinessGame.Ambient = Ambient;
                News.ReportedAt -= Heard;
                News.ReportedAt += Heard;
            }

            internal static void Disconnect()
            {
                FaultBridges.Reset();
                FlareBridges.FaultCheck = null;
                FlareBridges.FaultActed = null;
                FlareBridges.FaultDone = null;
                FlareBridges.FaultsReal = null;
                CarOwnership.RoadHit = null;
                CityWatch.Dark(1f);
                BusinessGame.Ambient = _ambientBefore;   // the interiors', Flare's and the others' pairings take theirs off next
                _ambientBefore = null;
                News.ReportedAt -= Heard;
            }

            private static int Check(FlareReport kind, string district, float x, float z, string reporter, int caseId, string place, out float fx, out float fz, out int fault)
            {
                fx = x;
                fz = z;
                fault = -1;
                try
                {
                    FaultDirector d = FaultDirector.Instance;
                    return d != null ? d.Check((int)kind, district, x, z, reporter, caseId, place, out fx, out fz, out fault) : -1;
                }
                catch (Exception e)
                {
                    Fault("FaultLinks.Check", e);
                    return -1;
                }
            }

            private static bool CityReport(int kind, string district, string address, Vector3 at)
            {
                try
                {
                    FlareDirector flare = FlareDirector.Instance;
                    return flare != null && flare.CityReport((FlareReport)kind, district, address, at.x, at.z);
                }
                catch (Exception e)
                {
                    Fault("FaultLinks.CityReport", e);
                    return false;
                }
            }

            // The player's business: the game's cleanliness of its floor; the city's: its customers' rating of it.
            private static int Cleanliness(string place)
            {
                try
                {
                    BuildingRegistration reg = FlareLiveGame.AnyBusiness(place);
                    if (reg == null)
                    {
                        return CleanUnknown;
                    }
                    if (reg.RentedByPlayer)
                    {
                        return BusinessGame.Cleanliness(reg);
                    }
                    return reg.satisfaction != null ? reg.satisfaction.cleanliness : CleanUnknown;
                }
                catch (Exception e)
                {
                    Fault("FaultLinks.Cleanliness", e);
                    return CleanUnknown;
                }
            }

            // The earlier pairings' part with the litter near the door on top; business pressure bounds the sum.
            private static int Ambient(BuildingRegistration reg)
            {
                int before = 0;
                try
                {
                    before = _ambientBefore != null ? _ambientBefore(reg) : 0;
                }
                catch (Exception e)
                {
                    Fault("FaultLinks.Ambient (the pairings before)", e);
                }
                try
                {
                    return before + FaultDirector.FootfallOf(reg);
                }
                catch (Exception e)
                {
                    Fault("FaultLinks.Ambient", e);
                    return before;
                }
            }

            private static void Footfall(BuildingRegistration reg)
            {
                try
                {
                    BusinessDirector.Footfall(reg, true, false, out _, out _);
                }
                catch (Exception e)
                {
                    Fault("FaultLinks.Footfall", e);
                }
            }

            private static void Heard(NewsKind kind, Vector3 where)
            {
                try
                {
                    if (kind == NewsKind.Crime || kind == NewsKind.BreakIn || kind == NewsKind.CarBreakIn)
                    {
                        FaultDirector.Instance?.Crime(where);
                    }
                }
                catch (Exception e)
                {
                    Fault("FaultLinks.Heard", e);
                }
            }
        }
    }
}
