using System;
using System.Collections.Generic;
using Entities;
using Helpers;
using Localizor;
using RealLifeOverhaul.Events;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.GameUi;
using UI.Guiders;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Flare's living network with the rest of the mod (Social/Flare/FlareBridges.Live.cs), one line each:
        ///
        ///   CityFacts     what the city's people post about: the weather (Events/Weather), where the latest crime or crash
        ///                 was (Social/News), prices (the city's economy's inflation), today's and tomorrow's street event
        ///                 (Events/EventCalendar), the election and its candidates, the council's works running late, the
        ///                 street cleaning cut (Civic)
        ///   Works         the City Office's crew for a road or a lamp: a site of the council's works at the building nearest
        ///                 the report (Civic/CivicWorks.StartAt: roadworks or utility works, its days as the programme counts
        ///                 them), never on a guest of a session (the host's programme is the city's)
        ///   WorksDone, SiteNear, SiteReported  a site handed over; a site near a point; an unsafe site reported to the
        ///                 inspector (CivicWorks.ReportUnsafe)
        ///   Watched       the police in the district the player stands in (Crime/CityWatch: less street crime, offences
        ///                 likelier seen) and the cleaners' round there (the clean streets' mood, Civic/CivicEffects.Keep)
        ///   PlayerDistrict, Staff, PlayerName  where the player is (Districts), the staff's names (the game's employees),
        ///                 the player's own name (the game's character)
        ///   Destination, EventDestination  the way to a business, or to the street event on now (the game's guider)
        ///   TakeSelfie    photo mode as a selfie (Leisure/Photo/PhotoMode.ArmSelfie)
        ///   the paper     Flare's stories with their names (a pile-on, ignored complaints) in the morning's City Ledger,
        ///                 chained on to the others' (Social/News.Extra)
        ///
        /// Tycoons is left unset: the rival tycoons of the end game (feature/rival-tycoons) are not in the mod yet; when they
        /// are, one line here hands Flare their names. COST: nothing per frame; each answer is asked on Flare's day, its hour
        /// or a click.
        /// </summary>
        public static class FlareLiveLinks
        {
            private const float WorksNear = 160f;      // metres: the building a crew goes to is the nearest with a door within this
            private const float RoadHours = 24f;       // working hours of a road's repair
            private const float LampHours = 10f;       // ...and a street light's

            private static Action<int, List<NewsItem>> _paperBefore;
            private static readonly List<FlareStory> Stories = new List<FlareStory>(4);
            private static readonly List<EmployeeInstance> Staffed = new List<EmployeeInstance>(16);
            private static readonly List<BuildingRegistration> Shops = new List<BuildingRegistration>(16);
            private static readonly GuideMark EventMark = new GuideMark();

            internal static void Connect()
            {
                FlareBridges.CityFacts = Facts;
                FlareBridges.Works = Works;
                FlareBridges.WorksDone = id =>
                {
                    CivicWorks works = CivicDirector.Instance != null ? CivicDirector.Instance.Works : null;
                    return works == null || works.Find(id) == null;
                };
                FlareBridges.SiteNear = (x, z, within) => CivicDirector.Instance != null && CivicDirector.Instance.Works != null && CivicDirector.Instance.Works.Nearest(new Vector3(x, 0f, z), within) != null;
                FlareBridges.SiteReported = SiteReported;
                FlareBridges.Watched = Watched;
                FlareBridges.PlayerDistrict = Districts.DistrictGame.PlayerDistrict;
                FlareBridges.Staff = Staff;
                FlareBridges.PlayerName = PlayerName;
                FlareBridges.Destination = Destination;
                FlareBridges.EventDestination = EventDestination;
                FlareBridges.TakeSelfie = () => Leisure.PhotoMode.Instance?.ArmSelfie();
                FlareLiveGame.Logger = FlareDirector.Logger;
                _paperBefore = News.Extra;
                News.Extra = Paper;
            }

            internal static void Disconnect()
            {
                News.Extra = _paperBefore;
                _paperBefore = null;
                CityWatch.Set(false);
                EventMark.Hide();
            }

            // ---------------------------------------------------------------- the city as the day's posts read it

            private static void Facts(FlareCityFacts f)
            {
                try
                {
                    f.Rain = Weather.Sky == Sky.Rain || Weather.Sky == Sky.Storm;
                    Vector3 where;
                    string detail;
                    if (News.Recent(NewsKind.Crime, 1, out where, out detail) || News.Recent(NewsKind.BreakIn, 1, out where, out detail) || News.Recent(NewsKind.Accident, 1, out where, out detail))
                    {
                        f.NewsDistrict = where != Vector3.zero ? Districts.DistrictGame.NearestDistrict(where, 200f) : null;
                    }
                    float inflation = Economy.CityEconomy.InflationPercent;
                    f.PriceTrend = inflation > 3f ? 1 : inflation < 1f ? -1 : 0;
                    PlannedEvent today = EventCalendar.For(f.Day), tomorrow = EventCalendar.For(f.Day + 1);
                    if (today.Kind != EventKind.None)
                    {
                        f.EventToday = EventCalendar.Name(today.Kind);
                        f.EventTodayFrom = today.From;
                    }
                    if (tomorrow.Kind != EventKind.None)
                    {
                        f.EventTomorrow = EventCalendar.Name(tomorrow.Kind);
                        f.EventTomorrowFrom = tomorrow.From;
                    }
                    CivicGovernment gov = CivicDirector.Instance != null && Settings.Civic ? CivicDirector.Instance.Government : null;
                    if (gov != null && gov.Campaigning)
                    {
                        f.Election = true;
                        for (int i = 0; i < gov.State.CandidateCount; i++)
                        {
                            if (!gov.State.Candidates[i].Player)
                            {
                                f.Candidates.Add(CivicText.Name(gov.State.Candidates[i]));
                            }
                        }
                    }
                    CivicWorks works = CivicDirector.Instance != null && Settings.Civic ? CivicDirector.Instance.Works : null;
                    if (works != null)
                    {
                        f.LateSites = works.Late;
                        if (works.State.Sites.Count > 0)
                        {
                            SiteState site = works.State.Sites[f.Day % works.State.Sites.Count];
                            f.SiteDistrict = Districts.DistrictGame.NearestDistrict(new Vector3(site.X, site.Y, site.Z), 160f);
                        }
                    }
                    f.LitterPolicy = Settings.Civic && CivicPolicy.Position(Lever.Cleaning) < 0;
                }
                catch (Exception e)
                {
                    Fault("FlareLiveLinks.Facts", e);
                }
            }

            // ---------------------------------------------------------------- the City Office's crews

            private static bool Works(FlareReport kind, string district, float x, float z, int seed, out int site, out int days, out string address)
            {
                site = -1;
                days = 0;
                address = null;
                try
                {
                    CivicWorks works = Settings.Civic && CivicDirector.Instance != null ? CivicDirector.Instance.Works : null;
                    CityManager city = InstanceBehavior<CityManager>.Instance;
                    CityBuildingController[] all = city != null ? city.cityBuildingControllers : null;
                    if (works == null || works.Shared || all == null || all.Length == 0 || SaveGameManager.Current == null)
                    {
                        return false;
                    }
                    CityBuildingController place = null;
                    if (x != 0f || z != 0f)
                    {
                        float best = WorksNear * WorksNear;
                        for (int i = 0; i < all.Length; i++)
                        {
                            CityBuildingController b = all[i];
                            if (b == null || !CivicCity.HasDoor(b))
                            {
                                continue;
                            }
                            Vector3 door = b.entranceDoors[0].doorTransform.position;
                            float sqr = (door.x - x) * (door.x - x) + (door.z - z) * (door.z - z);
                            if (sqr < best && works.Nearest(door, 60f) == null)
                            {
                                best = sqr;
                                place = b;
                            }
                        }
                    }
                    if (place == null && district != null)
                    {
                        int count = 0;
                        for (int i = 0; i < all.Length; i++)
                        {
                            count += In(all[i], district, works) ? 1 : 0;
                        }
                        int pick = count > 0 ? (int)((uint)seed % (uint)count) : -1;
                        for (int i = 0; i < all.Length && pick >= 0 && place == null; i++)
                        {
                            if (In(all[i], district, works) && pick-- == 0)
                            {
                                place = all[i];
                            }
                        }
                    }
                    if (place == null)
                    {
                        return false;
                    }
                    int day = SaveGameManager.Current.Day;
                    SiteState made = works.StartAt(kind == FlareReport.Lamp ? SiteKind.Utility : SiteKind.Roadworks, place, day, false, kind == FlareReport.Lamp ? LampHours : RoadHours);
                    if (made == null)
                    {
                        return false;
                    }
                    site = made.Id;
                    days = Mathf.Max(1, works.DaysLeft(made, day, SaveGameManager.Current.Hour) + 1);
                    address = CivicCity.AddressText(made.Street, made.Number);
                    return true;
                }
                catch (Exception e)
                {
                    Fault("FlareLiveLinks.Works", e);
                    return false;
                }
            }

            private static bool In(CityBuildingController b, string district, CivicWorks works)
            {
                return b != null && b.building != null && b.building.Neighbourhood == district && CivicCity.HasDoor(b) && works.Nearest(b.entranceDoors[0].doorTransform.position, 60f) == null;
            }

            private static bool SiteReported(float x, float z)
            {
                try
                {
                    CivicWorks works = CivicDirector.Instance != null ? CivicDirector.Instance.Works : null;
                    SiteState site = works != null ? works.Nearest(new Vector3(x, 0f, z), 120f) : null;
                    return site != null && SaveGameManager.Current != null && works.ReportUnsafe(site, SaveGameManager.Current.Day);
                }
                catch (Exception e)
                {
                    Fault("FlareLiveLinks.SiteReported", e);
                    return false;
                }
            }

            private static void Watched(bool patrol, bool clean)
            {
                CityWatch.Set(patrol);
                if (clean && Settings.Civic)
                {
                    CivicEffects.Keep(ModState.CivicClean, 2);
                }
            }

            // ---------------------------------------------------------------- the player and the staff

            private static void Staff(List<string> into)
            {
                try
                {
                    Economy.Books.Businesses(Shops);
                    for (int i = 0; i < Shops.Count && into.Count < 6; i++)
                    {
                        Staffed.Clear();
                        EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { withAssignedAddress = Shops[i].Address, excludeBeingReplaced = true }, Staffed);
                        for (int k = 0; k < Staffed.Count && into.Count < 6; k++)
                        {
                            string name = Staffed[k] != null && Staffed[k].characterData != null ? Staffed[k].characterData.name : null;
                            if (!string.IsNullOrEmpty(name) && !into.Contains(name))
                            {
                                into.Add(name);
                            }
                        }
                    }
                    Staffed.Clear();
                }
                catch (Exception e)
                {
                    Fault("FlareLiveLinks.Staff", e);
                }
            }

            private static string PlayerName()
            {
                try
                {
                    return PlayerHelper.CharacterData != null ? PlayerHelper.CharacterData.name : null;
                }
                catch (Exception)
                {
                    return null;
                }
            }

            private static void Destination(string place)
            {
                try
                {
                    BuildingRegistration reg = FlareLiveGame.AnyBusiness(place);
                    if (reg != null && InstanceBehavior<GuidersManager>.Instance != null)
                    {
                        GuidersManager.SetGuiderTarget(reg.Address, DirectionGuiderType.Destination);
                    }
                }
                catch (Exception e)
                {
                    Fault("FlareLiveLinks.Destination", e);
                }
            }

            private static bool EventDestination()
            {
                Vector3 at;
                string nameKey;
                if (!EventDirector.StreetEvent(out at, out nameKey))
                {
                    return false;
                }
                return EventMark.Show(at, nameKey != null ? nameKey.GetLocalization() : string.Empty);
            }

            // ---------------------------------------------------------------- the paper

            private static void Paper(int day, List<NewsItem> into)
            {
                _paperBefore?.Invoke(day, into);
                FlareDirector flare = FlareDirector.Instance;
                if (flare == null)
                {
                    return;
                }
                flare.Stories(day, Stories);
                for (int i = 0; i < Stories.Count; i++)
                {
                    FlareStory s = Stories[i];
                    var data = new Dictionary<string, string>(4);
                    for (int k = 0; k + 1 < s.Data.Length; k += 2)
                    {
                        data[s.Data[k]] = s.Data[k + 1];
                    }
                    into.Add(new NewsItem { Key = s.Key, Data = data });
                }
            }
        }
    }
}
