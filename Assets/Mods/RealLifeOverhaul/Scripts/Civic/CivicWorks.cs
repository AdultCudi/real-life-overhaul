using System;
using System.Collections.Generic;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city's construction programme, as data. Six sites are in progress at once: two
    /// started within a walk of wherever the player was on the day, four anywhere in the city.
    /// Each has a kind (roadworks, scaffolding on a building front, the refit of an empty shop,
    /// utility works), a contractor, a project name and a number of working hours it needs.
    ///
    /// Work is done by the hour whether or not the player is anywhere near: ten hours a weekday
    /// (07:00 to 17:00) as the city came, changed by the permits policy (faster and until 19:00,
    /// or slower and 08:00 to 16:00). Rain stops outdoor work for the hour. A noise complaint
    /// can bring quiet hours (09:00 to 15:00) for a few days; a report of an unsafe site that
    /// was right stops it for a day. Hours missed in a time skip are caught up one by one, as
    /// dry hours. When the hours are done the site is handed over: it leaves the list, and
    /// whatever stood in the street for it is taken away (SiteDresser).
    ///
    /// There are no empty lots in the game's city and nothing in it is ever built or knocked
    /// down, so no new building goes up: the fourth kind of site is the refit of an empty retail
    /// unit that nobody rents (hoarding over the shopfront, "opening soon", then the hoarding
    /// comes down and the unit is back on the market as the game has it).
    /// </summary>
    public sealed class CivicWorks
    {
        private const string Retail = "ba:buildingtype_retail";
        private const string EmptyUnit = "ba:businesstype_empty";
        private const float SiteGap = 90f;          // two sites are never nearer each other than this
        private const float LocalNear = 60f, LocalFar = 320f;
        private const float Earshot = 60f;          // a site this near one of the player's doors is "next door"
        private const int CatchUpHours = 24 * 21;

        public readonly WorksState State = new WorksState();
        private readonly List<Vector3> _doors = new List<Vector3>(16);
        private readonly List<CityBuildingController> _pool = new List<CityBuildingController>(64);

        /// <summary>Where the permits lever stands; set by the director when levers change.</summary>
        public int Permits;

        /// <summary>Sites running more than a third over their time: the government hears of it.</summary>
        public int Late { get; private set; }

        /// <summary>Raised when the list of sites has changed (one started, one handed over).</summary>
        public Action SitesChanged;

        public static bool Weekday(int day)
        {
            return ((day - 1) % 7 + 7) % 7 < 5;
        }

        /// <summary>True in the hours a site may work on a day, quiet hours included.</summary>
        public bool Working(SiteState site, int day, int hour)
        {
            if (!Weekday(day) || site.HaltUntil > day)
            {
                return false;
            }
            int opens = CivicCatalogue.SiteOpens(Permits), shuts = CivicCatalogue.SiteShuts(Permits);
            if (site.QuietUntil >= day)
            {
                opens = Mathf.Max(opens, 9);
                shuts = Mathf.Min(shuts, 15);
            }
            return hour >= opens && hour < shuts;
        }

        private float Speed(SiteState site)
        {
            return CivicCatalogue.SiteSpeed(Permits) * (site.Favoured ? 1.5f : 1f);
        }

        /// <summary>About once a second: runs every hour that has gone by. The hour that is "now" is the only one the weather can stop.</summary>
        public void Tick(int day, int hour)
        {
            long now = (long)day * 24 + hour;
            if (State.LastHour <= 0 || now < State.LastHour || now - State.LastHour > CatchUpHours)
            {
                State.LastHour = now;
                return;
            }
            while (State.LastHour < now)
            {
                State.LastHour++;
                int d = (int)(State.LastHour / 24);
                int h = (int)(State.LastHour % 24);
                bool live = State.LastHour == now;
                HourOfWork(d, h, live && CivicBridges.WorkStopped());
                if (h == 0)
                {
                    NewDay(d, live);
                }
            }
        }

        private void HourOfWork(int day, int hour, bool stoppedByWeather)
        {
            bool handed = false;
            for (int i = State.Sites.Count - 1; i >= 0; i--)
            {
                SiteState site = State.Sites[i];
                if (!Working(site, day, hour))
                {
                    continue;
                }
                if (stoppedByWeather && site.Kind != SiteKind.Refit)
                {
                    site.RainHours++;
                    continue;
                }
                site.Done += Speed(site);
                if (site.Finished)
                {
                    HandOver(site, day);
                    State.Sites.RemoveAt(i);
                    handed = true;
                }
            }
            if (handed)
            {
                SitesChanged?.Invoke();
            }
        }

        private void HandOver(SiteState site, int day)
        {
            State.Completed++;
            string address = CivicCity.AddressText(site.Street, site.Number);
            if (site.Mine)
            {
                State.RefreshedStreet = site.Street;
                State.RefreshedNumber = site.Number;
                State.RefreshedDay = day;
                ModState.AddModifier(ModState.MoodUpBig);
                CivicText.Toast(NotificationType.Success, "reallife_civic_refit_done", "address", address, 14f);
            }
            else if (site.NearMine)
            {
                CivicText.Toast(NotificationType.Success, "reallife_civic_site_done_near", "project", ProjectName(site), "address", address, 12f);
            }
            CivicDirector.Logger?.Info("Civic: site " + site.Id + " (" + site.Kind + ", " + address + ") handed over on day " + day + " after " + site.RainHours + " hour(s) lost to rain and "
                                       + site.Complaints + " complaint(s).");
        }

        private void NewDay(int day, bool live)
        {
            // Empty units that have been let meanwhile are simply finished: the tenant has their own plans.
            for (int i = State.Sites.Count - 1; i >= 0; i--)
            {
                SiteState site = State.Sites[i];
                if (site.Kind == SiteKind.Refit && !site.Mine && live && Let(site))
                {
                    State.Sites.RemoveAt(i);
                    SitesChanged?.Invoke();
                }
            }
            Late = 0;
            for (int i = 0; i < State.Sites.Count; i++)
            {
                SiteState site = State.Sites[i];
                float planned = site.Needed / 10f / 5f * 7f;
                if (day - site.StartDay > planned * 1.35f + 1f)
                {
                    Late++;
                }
            }
            if (live)
            {
                TopUp(day);
                Earshots();
            }
        }

        private static bool Let(SiteState site)
        {
            CityBuildingController controller = CivicCity.Find(site.Street, site.Number);
            BuildingRegistration registration = controller != null && controller.building != null ? controller.building.GetRegistration() : null;   // asked afresh: the scene object's own copy may be old
            return registration != null && (registration.RentedByPlayer || registration.businessTypeName != EmptyUnit);
        }

        /// <summary>Marks the sites that are next door to something the player lives or works in. A walk over the player's buildings: daily, and when a site starts.</summary>
        public void Earshots()
        {
            CivicCity.PlayerDoors(_doors);
            for (int i = 0; i < State.Sites.Count; i++)
            {
                SiteState site = State.Sites[i];
                var at = new Vector3(site.X, site.Y, site.Z);
                bool near = false;
                for (int d = 0; d < _doors.Count && !near; d++)
                {
                    near = CivicCity.Flat(_doors[d] - at) < Earshot * Earshot;
                }
                site.NearMine = near;
            }
        }

        /// <summary>True if a site that makes a noise is at work next door to the player's home or business right now, or within earshot of where they stand.</summary>
        public bool NoiseNow(int day, int hour, Vector3 player, bool outdoors)
        {
            for (int i = 0; i < State.Sites.Count; i++)
            {
                SiteState site = State.Sites[i];
                if (!site.Noisy || !Working(site, day, hour))
                {
                    continue;
                }
                if (site.NearMine || (outdoors && CivicCity.Flat(new Vector3(site.X, site.Y, site.Z) - player) < 40f * 40f))
                {
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- starting sites

        /// <summary>Starts sites until the programme is full. Needs the city's buildings and the player; does nothing without them.</summary>
        public void TopUp(int day)
        {
            if (PlayerHelper.PlayerController == null || InstanceBehavior<CityManager>.Instance == null || BuildingManager.IsInsideBuilding)
            {
                return;   // indoors the city's buildings are not all there to choose from: tomorrow will do
            }
            Vector3 player = PlayerHelper.GetPosition();
            int local = 0, other = 0;
            for (int i = 0; i < State.Sites.Count; i++)
            {
                if (State.Sites[i].Mine)
                {
                    continue;
                }
                if (State.Sites[i].Local)
                {
                    local++;
                }
                else
                {
                    other++;
                }
            }
            bool any = false;
            int seed = SaveSeed();
            for (int guard = 0; guard < CivicCatalogue.ActiveSites && local + other < CivicCatalogue.ActiveSites; guard++)
            {
                bool wantLocal = local < CivicCatalogue.LocalSites;
                var random = new CivicRandom(CivicRandom.Mix(seed, State.NextId, day));
                SiteKind kind = DrawKind(ref random);
                CityBuildingController place = Choose(kind, wantLocal, player, ref random);
                if (place == null && kind == SiteKind.Refit)
                {
                    kind = SiteKind.Scaffold;   // no empty unit to be had there
                    place = Choose(kind, wantLocal, player, ref random);
                }
                if (place == null)
                {
                    break;
                }
                SiteState site = Start(kind, place, day, ref random);
                site.Local = wantLocal;
                if (wantLocal)
                {
                    local++;
                }
                else
                {
                    other++;
                }
                any = true;
            }
            if (any)
            {
                Earshots();
                for (int i = 0; i < State.Sites.Count; i++)
                {
                    SiteState site = State.Sites[i];
                    if (site.StartDay == day && site.NearMine && !site.Mine)
                    {
                        CivicText.Toast(NotificationType.Warning, "reallife_civic_site_starts_near", "project", ProjectName(site),
                            "address", CivicCity.AddressText(site.Street, site.Number), "days", DaysLeft(site, day, 7).ToString(), 14f);
                    }
                }
                SitesChanged?.Invoke();
            }
        }

        private static int SaveSeed()
        {
            CivicDirector director = CivicDirector.Instance;
            return director != null && director.Government != null ? director.Government.State.Seed : 1;
        }

        private static SiteKind DrawKind(ref CivicRandom random)
        {
            int roll = random.Range(0, 100);
            return roll < 35 ? SiteKind.Roadworks : roll < 65 ? SiteKind.Scaffold : roll < 80 ? SiteKind.Refit : SiteKind.Utility;
        }

        // A building for a site: with a street door, the right sort for the kind, not beside another site.
        private CityBuildingController Choose(SiteKind kind, bool local, Vector3 player, ref CivicRandom random)
        {
            CityBuildingController[] all = InstanceBehavior<CityManager>.Instance.cityBuildingControllers;
            if (all == null || all.Length == 0)
            {
                return null;
            }
            if (!local)
            {
                for (int tries = 0; tries < 60; tries++)
                {
                    CityBuildingController candidate = all[random.Range(0, all.Length)];
                    if (Suits(candidate, kind) && CivicCity.Flat(candidate.entranceDoors[0].doorTransform.position - player) > LocalFar * LocalFar)
                    {
                        return candidate;
                    }
                }
                return null;
            }
            _pool.Clear();
            for (int i = 0; i < all.Length; i++)
            {
                CityBuildingController candidate = all[i];
                if (!CivicCity.HasDoor(candidate))
                {
                    continue;
                }
                Vector3 offset = candidate.entranceDoors[0].doorTransform.position - player;
                float sqr = CivicCity.Flat(offset);
                if (sqr > LocalNear * LocalNear && sqr < LocalFar * LocalFar && Mathf.Abs(offset.y) < 6f && Suits(candidate, kind))
                {
                    _pool.Add(candidate);
                }
            }
            CityBuildingController chosen = _pool.Count > 0 ? _pool[random.Range(0, _pool.Count)] : null;
            _pool.Clear();
            return chosen;
        }

        private bool Suits(CityBuildingController candidate, SiteKind kind)
        {
            if (!CivicCity.HasDoor(candidate) || candidate.building.locatedInsidePrivateFenceIndex >= 0)
            {
                return false;
            }
            BuildingRegistration registration = candidate.building.GetRegistration();   // asked afresh: the scene object's own copy may be old
            if (kind == SiteKind.Refit)
            {
                if (registration == null || registration.RentedByPlayer || registration.businessTypeName != EmptyUnit || candidate.building.BuildingType != Retail)
                {
                    return false;
                }
            }
            else if (kind == SiteKind.Scaffold && registration != null && registration.RentedByPlayer)
            {
                return false;   // never over the player's own front without being asked
            }
            Vector3 door = candidate.entranceDoors[0].doorTransform.position;
            for (int i = 0; i < State.Sites.Count; i++)
            {
                SiteState site = State.Sites[i];
                if (CivicCity.Flat(new Vector3(site.X, site.Y, site.Z) - door) < SiteGap * SiteGap)
                {
                    return false;
                }
            }
            return true;
        }

        private SiteState Start(SiteKind kind, CityBuildingController place, int day, ref CivicRandom random)
        {
            Vector3 door = place.entranceDoors[0].doorTransform.position;
            var site = new SiteState
            {
                Id = State.NextId++,
                Kind = kind,
                Street = place.building.StreetName,
                Number = place.building.StreetNumber,
                X = door.x, Y = door.y, Z = door.z,
                Project = random.Range(0, 3),
                Contractor = random.Range(0, CivicCatalogue.Contractors.Length),
                StartDay = day,
                Needed = Mathf.Round(CivicCatalogue.SiteHours[(int)kind] * random.Range(0.8f, 1.3f)),
                UnsafeFrom = random.Value < 0.3f ? random.Range(0.15f, 0.55f) : -1f
            };
            State.Sites.Add(site);
            CivicDirector.Logger?.Info("Civic: site " + site.Id + " started: " + kind + " at " + CivicCity.AddressText(site.Street, site.Number) + ", " + site.Needed + " working hours.");
            return site;
        }

        /// <summary>A site of a kind at a building, now (a test button, or the player's own refit). Null if one is already there.</summary>
        public SiteState StartAt(SiteKind kind, CityBuildingController place, int day, bool mine, float hours)
        {
            if (!CivicCity.HasDoor(place))
            {
                return null;
            }
            for (int i = 0; i < State.Sites.Count; i++)
            {
                if (State.Sites[i].Street == place.building.StreetName && State.Sites[i].Number == place.building.StreetNumber)
                {
                    return null;
                }
            }
            var random = new CivicRandom(CivicRandom.Mix(SaveSeed(), State.NextId, day + 977));
            SiteState site = Start(kind, place, day, ref random);
            site.Mine = mine;
            site.Local = !mine;
            if (hours > 0f)
            {
                site.Needed = hours;
            }
            if (mine)
            {
                site.UnsafeFrom = -1f;
            }
            Earshots();
            SitesChanged?.Invoke();
            return site;
        }

        // ---------------------------------------------------------------- what the player can do, and what a notice says

        /// <summary>The player complains about the noise. Returns the locale key of the answer.</summary>
        public string Complain(SiteState site, int day)
        {
            site.Complaints++;
            if (Permits > 0)
            {
                return "reallife_civic_complaint_noted";   // fast-track permits: complaints are only noted
            }
            if (site.QuietUntil >= day)
            {
                return "reallife_civic_complaint_already";
            }
            // As the city came, the second complaint about a site brings an inspector; under strict permits the first does.
            if (Permits < 0 || site.Complaints >= 2)
            {
                site.QuietUntil = day + 3;
                return "reallife_civic_complaint_upheld";
            }
            return "reallife_civic_complaint_logged";
        }

        /// <summary>The player reports a site as unsafe. True if it was: the inspector stops it for a working day and has it put right.</summary>
        public bool ReportUnsafe(SiteState site, int day)
        {
            bool was = site.Unsafe;
            site.Reported = true;
            if (was)
            {
                site.HaltUntil = day + 2;
                ModState.AddModifier(ModState.Helped);
                SitesChanged?.Invoke();
            }
            return was;
        }

        /// <summary>The whole days until a site is done, as things stand (weekends, hours and speed counted; weather not).</summary>
        public int DaysLeft(SiteState site, int day, int hour)
        {
            float left = site.Needed - site.Done;
            float speed = Speed(site);
            int days = 0;
            for (int d = day; days < 90 && left > 0f; d++, days++)
            {
                for (int h = d == day ? hour : 0; h < 24 && left > 0f; h++)
                {
                    if (Working(site, d, h))
                    {
                        left -= speed;
                    }
                }
            }
            return Mathf.Max(0, days - 1);
        }

        public static string ProjectName(SiteState site)
        {
            return CivicText.T(CivicCatalogue.Projects[(int)site.Kind * 3 + site.Project % 3]);
        }

        public static string PhaseName(SiteState site)
        {
            return CivicText.T(CivicCatalogue.Phases[(int)site.Kind * CivicCatalogue.PhasesPerKind + site.Phase]);
        }

        public static string ContractorName(SiteState site)
        {
            return CivicText.T(CivicCatalogue.Contractors[site.Contractor % CivicCatalogue.Contractors.Length]);
        }

        /// <summary>The site nearest a point, within a distance; null if none.</summary>
        public SiteState Nearest(Vector3 point, float within)
        {
            SiteState best = null;
            float bestSqr = within * within;
            for (int i = 0; i < State.Sites.Count; i++)
            {
                float sqr = CivicCity.Flat(new Vector3(State.Sites[i].X, State.Sites[i].Y, State.Sites[i].Z) - point);
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = State.Sites[i];
                }
            }
            return best;
        }

        public SiteState Find(int id)
        {
            for (int i = 0; i < State.Sites.Count; i++)
            {
                if (State.Sites[i].Id == id)
                {
                    return State.Sites[i];
                }
            }
            return null;
        }

        /// <summary>Test button: every site does a full working day at once.</summary>
        public void AdvanceDay(int day)
        {
            float hours = CivicCatalogue.SiteShuts(Permits) - CivicCatalogue.SiteOpens(Permits);
            bool handed = false;
            for (int i = State.Sites.Count - 1; i >= 0; i--)
            {
                SiteState site = State.Sites[i];
                site.Done += hours * Speed(site);
                if (site.Finished)
                {
                    HandOver(site, day);
                    State.Sites.RemoveAt(i);
                    handed = true;
                }
            }
            SitesChanged?.Invoke();
            if (handed)
            {
                TopUp(day);
            }
        }

        /// <summary>Test button: the programme is emptied (nothing is handed over, nothing is announced).</summary>
        public void Clear()
        {
            State.Sites.Clear();
            SitesChanged?.Invoke();
        }
    }
}
