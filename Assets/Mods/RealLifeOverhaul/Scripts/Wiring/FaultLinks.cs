using System;
using RealLifeOverhaul.Faults;
using RealLifeOverhaul.Flare;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The city's real faults (Scripts/Faults) with the rest of the mod, one line each:
        ///
        ///   FaultCheck    Flare's City Office asks the faults before it acts on a report (FaultDirector.Check): a real
        ///                 fault where it was made sends the crew there, nothing there is answered so
        ///   FaultActed, FaultDone  a crew sent on a true report takes its fault; the works handed over mend it
        ///   FaultsReal    while the faults run, the city's people on Flare report only real faults, through
        ///   CityReport    a post of theirs naming where it is and a case of the City Office's (FlareDirector.CityReport)
        ///   Cleanliness   a business's cleanliness for an inspection (the game's own for the player's businesses
        ///                 (Business/BusinessGame.Cleanliness), its customers' rating of it for the city's)
        ///   CrimeScale    a dark lamp near the player after dark: street crime there likelier (Crime/CityWatch.Dark)
        ///   LitterMood    litter in the player's district: the dirty streets' mood (Civic/CivicEffects.Keep, the civic
        ///                 litter mood the council's cleaning cut also gives)
        ///   Footfall      litter near a business of the player's: its part is added to <see cref="BusinessGame.Ambient"/>
        ///                 on top of the others' and written by business pressure's own writer (BusinessDirector.Footfall)
        ///   Trip          an unsafe place: a passer-by near it trips (Ragdoll/PedestrianIncidents.Trip, never hurt worse)
        ///   RoadHit       a pothole under the player's car (Transport/CarOwnership's drive tick: tyre wear and a jolt)
        ///   crimes        a crime reported in the street may make a hot spot there (News.ReportedAt)
        ///
        /// COST: nothing per frame. Each answer is asked when the faults change, on their timer, or on a report.
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
                BusinessGame.Ambient = _ambientBefore;   // (the interiors', Flare's and the others' own pairings take theirs off in a moment)
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

            // The player's own business: the game's cleanliness of its floor; the city's: what its customers rate its cleanliness.
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

            // What was asked for before this pairing, and the litter near the door on top. Business pressure keeps the sum inside its own bounds.
            private static int Ambient(BuildingRegistration reg)
            {
                return (_ambientBefore != null ? _ambientBefore(reg) : 0) + FaultDirector.FootfallOf(reg);
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
