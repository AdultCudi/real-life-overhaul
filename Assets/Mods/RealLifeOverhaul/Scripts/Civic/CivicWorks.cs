using System;
using System.Collections.Generic;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city's construction programme as data: six sites at a time, worked by the hour on weekdays wherever the player is,
    /// stopped by rain, slowed by complaints, halted by inspections, and handed over when done. A rebuild wraps a whole
    /// building in the game's scaffolding (one at a time); a new plot is started only on ground given to it (<see cref="StartPlot"/>).
    /// In a Going Public session a guest takes the host's sites and works only its own refit (<see cref="Shared"/>).
    /// </summary>
    public sealed class CivicWorks
    {
        private const string Retail = "ba:buildingtype_retail";
        private const string EmptyUnit = "ba:businesstype_empty";
        private const float SiteGap = 90f;          // metres between two sites, at least
        private const float PlotClear = 2f;         // metres round a plot's own ground that no other site may stand in
        private const float LocalNear = 60f, LocalFar = 320f;   // metres from the player, for a local site
        private const float Earshot = 60f;          // metres: a site this near one of the player's doors is "next door"
        private const int CatchUpHours = 24 * 21;   // after a longer gap the programme simply moves on

        public readonly WorksState State = new WorksState();
        private readonly WrapShell _shell = new WrapShell();
        private readonly List<Vector3> _doors = new List<Vector3>(16);
        private readonly List<CityBuildingController> _pool = new List<CityBuildingController>(64);

        /// <summary>Where the permits lever stands; set by the director when levers change.</summary>
        public int Permits;

        /// <summary>A guest of a Going Public session that follows the host's programme: only its own refit is worked here, and no site is started or let.</summary>
        public bool Shared;

        /// <summary>Sites running more than a third over their time: the government hears of it.</summary>
        public int Late { get; private set; }

        public Action SitesChanged;

        /// <summary>A finished building's new look was added (a rebuild or a plot handed over): the director's looks stand it.</summary>
        public Action<SiteLook> LookAdded;

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
                if (!Working(site, day, hour) || (Shared && !site.Mine))
                {
                    continue;
                }
                if (stoppedByWeather && site.Kind != SiteKind.Refit)
                {
                    site.RainHours++;
                    continue;
                }
                site.Done += SiteBuilders.WorkOn(site, Speed(site), (long)day * 24 + hour);   // a builder's site goes at its crews' pace, one out to tender waits
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
            if (site.Kind == SiteKind.Rebuild || site.Kind == SiteKind.Plot)
            {
                AddLook(site, day);
            }
            SiteBuilders.HandedOver(site, day);   // the day of the hour that finished it, so a builder's lateness is reckoned from its works, not from when they were caught up
            if (SiteBuilders.IsOwned(site))
            {
                CivicDirector.Logger?.Info("Civic: site " + site.Id + " (" + site.Kind + ", " + PlaceText(site) + ") handed over to its builder on day " + day + " after " + site.RainHours
                                           + " hour(s) lost to rain.");
                return;   // a builder's own works: its builder tells the player what they need to know
            }
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
                CivicText.Toast(NotificationType.Success, "reallife_civic_site_done_near", "project", ProjectName(site), "address", PlaceText(site), 12f);
            }
            CivicDirector.Logger?.Info("Civic: site " + site.Id + " (" + site.Kind + ", " + PlaceText(site) + ") handed over on day " + day + " after " + site.RainHours + " hour(s) lost to rain and "
                                       + site.Complaints + " complaint(s).");
        }

        // A finished building's look is kept: a rebuild's design over it, a plot's building on its ground, from its builder (or one
        // picked to fit). One look a building or a plot: a newer one takes the older's place.
        private void AddLook(SiteState site, int day)
        {
            var look = new SiteLook
            {
                Kind = site.Kind, Street = site.Street, Number = site.Number, X = site.X, Y = site.Y, Z = site.Z,
                Design = SiteBuilders.DesignOf(site), Tag = SiteBuilders.TagOf(site), Day = day, Mine = Shared && site.Mine
            };
            if (look.Kind == SiteKind.Plot && SiteBuilders.IsTaken(look.Tag))
            {
                return;   // what stands there is its builder's own (a landmark's stadium) or the district's already: no look of the programme's
            }
            for (int i = State.Looks.Count - 1; i >= 0; i--)
            {
                if (State.Looks[i].Same(look))
                {
                    State.Looks.RemoveAt(i);
                }
            }
            if (State.Looks.Count >= SiteLook.Most)
            {
                State.Looks.RemoveAt(0);   // the oldest building keeps its own look again
            }
            State.Looks.Add(look);
            LookAdded?.Invoke(look);
        }

        /// <summary>The city's own sites in the programme: a builder's are not among its six.</summary>
        public int OwnCount()
        {
            int count = 0;
            for (int i = 0; i < State.Sites.Count; i++)
            {
                count += SiteBuilders.IsOwned(State.Sites[i]) ? 0 : 1;
            }
            return count;
        }

        /// <summary>A builder's site handed over now, its works done (a schedule reached its end). False for one not in the programme.</summary>
        public bool Finish(SiteState site, int day)
        {
            if (site == null || !State.Sites.Contains(site))
            {
                return false;
            }
            site.Done = site.Needed;
            HandOver(site, day);
            State.Sites.Remove(site);
            SitesChanged?.Invoke();
            return true;
        }

        /// <summary>A builder's site taken out of the programme with nothing handed over (its project given up). False for one not in it.</summary>
        public bool Drop(SiteState site)
        {
            if (site == null || !State.Sites.Remove(site))
            {
                return false;
            }
            CivicDirector.Logger?.Info("Civic: site " + site.Id + " (" + site.Kind + ", " + PlaceText(site) + ") taken out of the programme by its builder.");
            SitesChanged?.Invoke();
            return true;
        }

        private void NewDay(int day, bool live)
        {
            // Empty units that have been let meanwhile are simply finished: the tenant has their own plans.
            for (int i = State.Sites.Count - 1; i >= 0; i--)
            {
                SiteState site = State.Sites[i];
                if (site.Kind == SiteKind.Refit && !site.Mine && live && !Shared && Let(site))
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
                if (!Shared)
                {
                    TopUp(day);
                }
                Earshots();
            }
        }

        private static bool Let(SiteState site)
        {
            CityBuildingController controller = CivicCity.Find(site.Street, site.Number);
            BuildingRegistration registration = controller != null && controller.building != null ? controller.building.GetRegistration() : null;   // asked afresh: the scene's copy may be old
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

        /// <summary>Starts sites until the programme is full. Needs the city's buildings and the player; does nothing without them.</summary>
        public void TopUp(int day)
        {
            if (Shared || PlayerHelper.PlayerController == null || InstanceBehavior<CityManager>.Instance == null || BuildingManager.IsInsideBuilding)
            {
                return;   // indoors the city's buildings are not all there to choose from: tomorrow will do
            }
            Vector3 player = PlayerHelper.GetPosition();
            int local = 0, other = 0;
            for (int i = 0; i < State.Sites.Count; i++)
            {
                if (State.Sites[i].Mine || SiteBuilders.IsOwned(State.Sites[i]))
                {
                    continue;   // the player's own refit, and the builders' own works, are not the city's programme
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
            for (int guard = 0; guard < CivicCatalogue.ActiveSites && local + other < CivicCatalogue.ActiveSites && State.Sites.Count < CivicCatalogue.SitesMost; guard++)
            {
                bool wantLocal = local < CivicCatalogue.LocalSites;
                var random = new CivicRandom(CivicRandom.Mix(seed, State.NextId, day));
                SiteKind kind = DrawKind(ref random);
                if (kind == SiteKind.Rebuild && Rebuilds() >= CivicCatalogue.MostRebuilds)
                {
                    kind = SiteKind.Scaffold;   // one wrapped building in the city at a time
                }
                CityBuildingController place = Choose(kind, wantLocal, player, ref random);
                if (place != null && kind == SiteKind.Rebuild && !Wrappable(place))
                {
                    kind = SiteKind.Scaffold;   // too tall, or no street side: its front is scaffolded instead
                }
                if (place == null && kind == SiteKind.Refit)
                {
                    kind = SiteKind.Scaffold;   // no empty unit to be had there
                    place = Choose(kind, wantLocal, player, ref random);
                }
                if (place == null)
                {
                    break;
                }
                SiteState site = Start(kind, place, day, 0f, ref random);
                site.Local = wantLocal;
                SiteBuilders.SiteStarted(site);   // the city's own work: a builder may bid for it
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
                            "address", PlaceText(site), "days", DaysLeft(site, day, 7).ToString(), 14f);
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
            return roll < 33 ? SiteKind.Roadworks : roll < 58 ? SiteKind.Scaffold : roll < 72 ? SiteKind.Refit : roll < 88 ? SiteKind.Utility : SiteKind.Rebuild;
        }

        private int Rebuilds()
        {
            int count = 0;
            for (int i = 0; i < State.Sites.Count; i++)
            {
                count += State.Sites[i].Kind == SiteKind.Rebuild ? 1 : 0;
            }
            return count;
        }

        // A building the game's scaffolding can be stood round to the roof: its shell read from its own colliders, low enough
        // for the lifts the wrap has, with a street side that can be told by its pavement later.
        private bool Wrappable(CityBuildingController place)
        {
            if (!BuildingShell.Read(place, _shell, out _, out _))
            {
                return false;
            }
            return WrapRules.LiftsFor(_shell.Top) < WrapRules.MostLifts;
        }

        // A building for a site: with a street door, the right sort for the kind, not beside another site.
        private CityBuildingController Choose(SiteKind kind, bool local, Vector3 player, ref CivicRandom random)
        {
            CityBuildingController[] all = GameDlc.Open(InstanceBehavior<CityManager>.Instance);
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
            if (!CivicCity.HasDoor(candidate) || GameMembers.Fenced(candidate.building))
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
            else if ((kind == SiteKind.Scaffold || kind == SiteKind.Rebuild) && registration != null && registration.RentedByPlayer)
            {
                return false;   // never over the player's own front without being asked
            }
            if (kind == SiteKind.Rebuild && SpecialBuildings.Holds(candidate))
            {
                return false;   // one of the mod's own places: it keeps its face
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

        private SiteState Start(SiteKind kind, CityBuildingController place, int day, float hours, ref CivicRandom random)
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
                Needed = HoursOf(kind, hours, ref random),
                UnsafeFrom = random.Value < 0.3f ? random.Range(0.15f, 0.55f) : -1f
            };
            State.Sites.Add(site);
            CivicDirector.Logger?.Info("Civic: site " + site.Id + " started: " + kind + " at " + PlaceText(site) + ", " + site.Needed + " working hours.");
            return site;
        }

        // The hours a site is set out with, and its line says: whoever started it gives its own (a builder's, a refit's), else the
        // programme's for its kind. Drawn either way, so every draw after it comes out as before. (Given after the line, they
        // left it naming the programme's hours: a builder's plot of 49 hours read as one of 349.)
        private static float HoursOf(SiteKind kind, float hours, ref CivicRandom random)
        {
            float drawn = Mathf.Round(CivicCatalogue.SiteHours[(int)kind] * random.Range(0.8f, 1.3f));
            return hours > 0f ? hours : drawn;
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
            SiteState site = Start(kind, place, day, hours, ref random);
            site.Mine = mine;
            site.Local = !mine;
            if (Shared)
            {
                State.NextId--;                          // the host's programme counts its own ids
                site.Id = CivicShare.NextOwnId(State);   // the guest's own refit, apart from every site of the host's
            }
            if (mine)
            {
                site.UnsafeFrom = -1f;
            }
            Earshots();
            SitesChanged?.Invoke();
            return site;
        }

        /// <summary>
        /// A new building on ground given to it: whoever has the ground says where its middle is (on the ground), which way its
        /// front looks, its size and its storeys, and the working hours its own works take (0 for the programme's). Null with
        /// nothing started if the ground cannot take one, or a site already stands on its ground (and <see cref="PlotClear"/>
        /// round it); a Going Public guest starts none (the city's sites are the host's).
        /// </summary>
        public SiteState StartPlot(Vector3 middle, int yaw, int width, int depth, int storeys, int day, out string why, float hours = 0f)
        {
            why = PlotRules.Unfit(width, depth, storeys);
            if (why != null)
            {
                return null;
            }
            if (Shared)
            {
                why = "the host of the Going Public session runs the city's sites";
                return null;
            }
            float clear = Mathf.Max(width, depth) * 0.5f + PlotClear;   // the plot's own ground: a district's lots stand side by side
            if (Nearest(middle, clear) != null)
            {
                why = "another site is already within " + clear + " m";
                return null;
            }
            var random = new CivicRandom(CivicRandom.Mix(SaveSeed(), State.NextId, day + 1931));
            var site = new SiteState
            {
                Id = State.NextId++,
                Kind = SiteKind.Plot,
                Street = PlotRules.Encode(width, depth, yaw, storeys),
                Number = 0,
                X = middle.x, Y = middle.y, Z = middle.z,
                Project = random.Range(0, 3),
                Contractor = random.Range(0, CivicCatalogue.Contractors.Length),
                StartDay = day,
                Needed = hours > 0f ? hours : Mathf.Round(CivicCatalogue.SiteHours[(int)SiteKind.Plot] * (0.85f + 0.06f * storeys)),
                UnsafeFrom = -1f,
                Local = true
            };
            State.Sites.Add(site);
            CivicDirector.Logger?.Info("Civic: site " + site.Id + " started: Plot on " + width + " by " + depth + " m of ground, " + storeys + " storey(s), " + site.Needed + " working hours.");
            Earshots();
            SitesChanged?.Invoke();
            return site;
        }

        /// <summary>Where a site is, as the player reads it: the building's address, or for a plot the words for new ground.</summary>
        public static string PlaceText(SiteState site)
        {
            return PlotRules.IsPlot(site.Street) ? CivicText.T("reallife_construction_plot_place") : CivicCity.AddressText(site.Street, site.Number);
        }

        /// <summary>Test: a site moves on to its next stage now (a plot in its frame stage, a storey). False for a site already in its last.</summary>
        public bool AdvanceStage(SiteState site, int day)
        {
            int phase = site.Phase;
            float next = site.Kind == SiteKind.Plot && PlotRules.TryRead(site.Street, out _, out _, out _, out int storeys)
                ? PlotRules.NextStep(phase, site.Progress, storeys)
                : (phase + 1f) / CivicCatalogue.PhasesPerKind + 0.001f;
            if (phase >= CivicCatalogue.PhasesPerKind - 1 && next >= 1f)
            {
                site.Done = site.Needed;
                HandOver(site, day);
                State.Sites.Remove(site);
                SitesChanged?.Invoke();
                return false;
            }
            site.Done = Mathf.Min(site.Needed * 0.999f, site.Needed * next);
            SitesChanged?.Invoke();
            return true;
        }

        /// <summary>The player complains about the noise. Returns the locale key of the answer.</summary>
        public string Complain(SiteState site, int day)
        {
            Share("k|", site);
            return ComplaintOf(site, day);
        }

        private string ComplaintOf(SiteState site, int day)
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
            Share("u|", site);
            return Report(site, day, true);
        }

        private bool Report(SiteState site, int day, bool mine)
        {
            bool was = site.Unsafe;
            site.Reported = true;
            if (was)
            {
                site.HaltUntil = day + 2;
                if (mine)
                {
                    ModState.AddModifier(ModState.Helped);
                }
                SitesChanged?.Invoke();
            }
            return was;
        }

        // A guest's complaint or report about one of the host's sites goes to the host as well.
        private void Share(string what, SiteState site)
        {
            if (Shared && site != null && site.Id < CivicShare.OwnSites)
            {
                SharedCity.Ask('W', what + site.Id);
            }
        }

        /// <summary>The host of a Going Public session: a guest's complaint (k) or report of an unsafe site (u), by the site's id. The guest has had its own answer.</summary>
        public bool HostRequest(string line, int day)
        {
            string[] f = line.Split('|');
            if (f.Length < 2 || !int.TryParse(f[1], out int id))
            {
                return false;
            }
            SiteState site = Find(id);
            if (site == null)
            {
                return false;
            }
            if (f[0] == "k")
            {
                ComplaintOf(site, day);
                return true;
            }
            if (f[0] == "u")
            {
                Report(site, day, false);
                return true;
            }
            return false;
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
            return SiteBuilders.NameOf(site) ?? CivicText.T(CivicCatalogue.Contractors[site.Contractor % CivicCatalogue.Contractors.Length]);
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

        public void AdvanceDay(int day)
        {
            float hours = CivicCatalogue.SiteShuts(Permits) - CivicCatalogue.SiteOpens(Permits);
            bool handed = false;
            for (int i = State.Sites.Count - 1; i >= 0; i--)
            {
                SiteState site = State.Sites[i];
                site.Done += SiteBuilders.WorkOn(site, hours * Speed(site), (long)day * 24 + CivicCatalogue.SiteOpens(Permits));   // a test's day of work in one go, dated from its opening
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
