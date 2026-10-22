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
        /// Flare's living network with what the city's people post about (weather, news, prices, events, the election, the
        /// council's works), the City Office's crews as council sites (never on a guest of a session: the host's programme
        /// is the city's), the police and the cleaners, the staff, the way to a business or an event, photo mode's selfie,
        /// the rival tycoons and the paper. Nothing per frame: asked on Flare's day, its hour or a click.
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
            private static Vector3 _eventAt;   // where Flare's way to a city event points
            // Nothing else takes Flare's way to a city event off, so the watch does once that event is over.
            private static readonly GuideMark EventMark = new GuideMark("Flare's way to a city event").While(EventGoesOn);

            internal static void Connect()
            {
                FlareBridges.CityFacts = Facts;
                FlareBridges.Works = Works;
                FlareBridges.WorksDone = id =>
                {
                    CivicWorks works = CivicDirector.Instance != null ? CivicDirector.Instance.Works : null;
                    return works == null || works.Find(id) == null;
                };
                FlareBridges.SiteNear = (x, z, within) => CivicDirector.Instance != null && CivicDirector.Instance.Works != null
                    && CivicDirector.Instance.Works.Nearest(new Vector3(x, 0f, z), within) != null;
                FlareBridges.SiteReported = SiteReported;
                FlareBridges.Watched = Watched;
                FlareBridges.Tycoons = Tycoons;
                FlareBridges.PlayerDistrict = Districts.DistrictGame.PlayerDistrict;
                FlareBridges.Staff = Staff;
                FlareBridges.PlayerName = PlayerName;
                FlareBridges.Destination = Destination;
                FlareBridges.EventDestination = EventDestination;
                FlareBridges.SightAt = SightAt;
                FlareBridges.StreetEventAt = StreetEventAt;
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

            // The park or sight the player stands at, from Leisure's list as found behind the loading screen; never a search.
            private static bool SightAt(float x, float z, out string id, out float sightX, out float sightZ)
            {
                id = null;
                sightX = sightZ = 0f;
                Leisure.Landmark mark = Leisure.LeisureVenues.LandmarkAt(new Vector3(x, 0f, z));
                if (mark == null)
                {
                    return false;
                }
                id = mark.Id;
                sightX = mark.Position.x;
                sightZ = mark.Position.z;
                return true;
            }

            // The street event on now, its middle and its label.
            private static bool StreetEventAt(out float x, out float y, out float z, out string label)
            {
                bool on = EventDirector.StreetEvent(out Vector3 at, out label);
                x = at.x;
                y = at.y;
                z = at.z;
                return on;
            }

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
                    bool utility = kind == FlareReport.Lamp || kind == FlareReport.Unsafe;   // an unsafe place of the city's faults is a utility crew's
                    SiteState made = works.StartAt(utility ? SiteKind.Utility : SiteKind.Roadworks, place, day, false, utility ? LampHours : RoadHours);
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

            // The rivals in play, each with their latest move announced in the last two days.
            private static void Tycoons(List<FlareTycoon> into)
            {
                Rivals.RivalDirector rivals = Rivals.RivalDirector.Instance;
                Rivals.RivalFile f = rivals != null && rivals.Running && !rivals.Resting ? rivals.File : null;
                if (f == null)
                {
                    return;
                }
                for (int i = 0; i < f.Tycoons.Length; i++)
                {
                    Rivals.Tycoon t = f.Tycoons[i];
                    if (t == null || !t.Active)
                    {
                        continue;
                    }
                    int move = -1, at = -1;
                    for (int m = 0; m < f.Moves.Count; m++)
                    {
                        Rivals.RivalMove mv = f.Moves[m];
                        if (mv.Tycoon == t.Index && mv.Live && !mv.Test && mv.Announced >= f.SettledDay - 2 && mv.Announced > at)
                        {
                            at = mv.Announced;
                            move = (int)mv.Kind;
                        }
                    }
                    into.Add(new FlareTycoon { Index = t.Index, Name = t.Def.Name, Move = move });
                }
            }

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
                _eventAt = at;
                return EventMark.Show(at, nameKey != null ? nameKey.GetLocalization() : string.Empty);
            }

            // The street event Flare's way points to is still on, where it was.
            private static bool EventGoesOn()
            {
                return EventDirector.StreetEvent(out Vector3 at, out string _) && (at - _eventAt).sqrMagnitude < 4f;
            }

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
