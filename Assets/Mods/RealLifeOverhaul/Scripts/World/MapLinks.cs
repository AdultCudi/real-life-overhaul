using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using Extensions;
using Localizor;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Every feature's places on the city map. The features know nothing of this class: it reads where their
    /// places are through members they already have (or a read-only accessor added for it) and hands them to
    /// <see cref="MapPlaces"/>, which gives each kind a row in the map's filter list, a marker with a name for
    /// every place, and "set destination".
    ///
    ///   kind (row)                   places                                                    ticked   in street
    ///   Bus stops                    every place of the bus network (TransitMap.Places)        by count no
    ///   Hotels                       the eight hotels (Hotels.Sites)                           yes      yes
    ///   Courthouse                   the courthouse (Courthouse.Front)                         yes      yes
    ///   Hudson City University       the university (CareersDirector.University), its own row  yes      yes
    ///   Places to study              the game's school and the hospitals' wards                yes      yes
    ///   Tech companies               the city's tech companies' headquarters (Tech/Sites)      yes      yes
    ///   Your cloud                   the player's data centres: the cloud's regions            yes      no
    ///   Driving schools              the learner-plate boards (LicenceDesks)                   yes      yes
    ///   Bike hire and two-wheelers   docks, bicycle racks, motorbike stands (BikePlaces.All)   yes      no
    ///   Custom shops                 the repair stations whose bays the custom shop works at   yes      no
    ///   Help wanted and employers    the help-wanted boards, named after their employer        yes      no
    ///   Newspaper kiosks             the City Ledger's kiosks near the home (LedgerDirector)   yes      yes
    ///   Polling places               one per district, during an election campaign             yes      no
    ///   Construction sites           the sites in progress (CivicWorks)                        yes      no
    ///   Sights and fishing spots     the landmarks; "seen" and "fishing nearby" in the name    yes      no
    ///   What's on                    a street event, a rally, the chess table, a plan's venue  yes      no
    ///
    /// (Police stations are Law's own category.) "By count": bus stops are ticked by default only in a city with
    /// forty or fewer; the bus network usually has over a hundred.
    ///
    /// Every building the mod claims as its own (Core/SpecialBuildings: the university, City Utilities, the courthouse, the
    /// tech companies' towers, the hotels, the police stations) has a row of its own with its own icon, as the game's
    /// special buildings have theirs (its school, its hospital), and its place is that building, so the row's tick lights
    /// the building on the open map as the game lights its own. The university was once only one of the "Places to study",
    /// under a star shared with the sights, so the map's filters had no row by its name and its arrow flew to the nearest
    /// school or hospital instead (NavCheck/MapRowCheck.cs checks every claimed building has its row).
    ///
    /// How it keeps up: one look every three seconds (real time: the map pauses the game). Each kind has a stamp,
    /// a few integer operations over what the feature already holds (a count, the ids of the sites, where the
    /// street event is); only when a stamp has changed are that kind's places read again and its markers replaced.
    /// Markers are made six at a time, a fifth of a second apart, so the hundred-odd bus stops do not arrive in
    /// one frame. A kind's row is made the first time it has a place and is offered only while it has one and
    /// its feature's option is on; with the option off its markers are taken away, not merely hidden. A kind
    /// whose places cannot be read is left off the map with one line in the log and the others carry on.
    /// Nothing per frame. Everything goes with the city (MapPlaces.Shutdown).
    /// </summary>
    public sealed class MapLinks : MonoBehaviour
    {
        private const float LookEvery = 3f;       // seconds between looks
        private const float FeedEvery = 0.2f;     // seconds between batches of markers while some are still to be made
        private const int FeedAtOnce = 6;         // markers made per kind per batch
        private const int ManyStops = 40;         // more bus stops than this: their row starts unticked

        public static IModLogger Logger;

        private sealed class Link
        {
            public string Id;
            public string TitleKey;
            public Color Colour;
            public MapIcon Icon;
            public bool InStreet;
            public Func<int, bool> TickedByDefault;      // from how many places it has the first time; null for yes
            public Func<bool> Enabled;                   // the feature's own options
            public Func<long> Stamp;                     // cheap; changes when the places do
            public Action<List<MapPlace>> Fill;          // the places as they are now
            public MapPlaces.Category Category;          // made the first time there is a place
            public readonly List<MapPlace> Waiting = new List<MapPlace>(8);   // read, not yet on the map
            public int Fed;                              // how many of Waiting are on the map
            public int Count;                            // places it has: on the map or waiting
            public long Last;
            public bool Failed;
        }

        private readonly List<Link> _links = new List<Link>(12);
        private readonly List<MapPlace> _scratch = new List<MapPlace>(160);
        private readonly List<Vector3> _spots = new List<Vector3>(16);
        private float _nextLook;

        private void Start()
        {
            try
            {
                Declare();
            }
            catch (Exception e)
            {
                _links.Clear();
                Logger?.Error(e);
                Logger?.Warn("Map links: could not be set up after the error above; the mod's places (other than police stations) are left off the city map.");
                return;
            }
            StartCoroutine(Guard.Routine(Run()));
        }

        private IEnumerator Run()
        {
            var look = new WaitForSecondsRealtime(LookEvery);
            var feed = new WaitForSecondsRealtime(FeedEvery);
            while (true)
            {
                bool waiting = false;
                if (SaveGameManager.Current != null && !GameManager.isCitySceneBeingUnloaded)
                {
                    if (Time.unscaledTime >= _nextLook)
                    {
                        _nextLook = Time.unscaledTime + LookEvery - 0.05f;
                        for (int i = 0; i < _links.Count; i++)
                        {
                            Look(_links[i]);
                        }
                    }
                    for (int i = 0; i < _links.Count; i++)
                    {
                        waiting |= Feed(_links[i]);
                    }
                }
                yield return waiting ? feed : look;
            }
        }

        // One kind looked at: its places are read again only if its stamp is not what it was.
        private void Look(Link link)
        {
            if (link.Failed)
            {
                return;
            }
            try
            {
                long stamp = Settings.MapPlacesShown && link.Enabled() ? link.Stamp() : 0L;
                if (stamp == link.Last)
                {
                    return;
                }
                link.Last = stamp;
                _scratch.Clear();
                if (stamp != 0L)
                {
                    link.Fill(_scratch);
                }
                link.Waiting.Clear();
                link.Fed = 0;
                link.Count = _scratch.Count;
                if (link.Category != null)
                {
                    link.Category.Clear();
                }
                if (_scratch.Count == 0)
                {
                    return;
                }
                if (link.Category == null)
                {
                    bool ticked = link.TickedByDefault == null || link.TickedByDefault(_scratch.Count);
                    link.Category = MapPlaces.Register(link.Id, link.TitleKey, link.Colour, link.Icon, ticked, inStreet: link.InStreet);
                    link.Category.Available = () => link.Count > 0 && link.Enabled();
                    Logger?.Info("Map links: " + link.Id + " on the city map with " + _scratch.Count + " place(s)" + (ticked ? "." : "; its row starts unticked (there are many)."));
                }
                link.Waiting.AddRange(_scratch);
                _scratch.Clear();
            }
            catch (Exception e)
            {
                Fail(link, e);
            }
        }

        // The next few of a kind's places onto the map. True while it has more to come.
        private bool Feed(Link link)
        {
            if (link.Failed || link.Category == null || link.Fed >= link.Waiting.Count)
            {
                return false;
            }
            try
            {
                for (int made = 0; made < FeedAtOnce && link.Fed < link.Waiting.Count; made++)
                {
                    link.Category.Add(link.Waiting[link.Fed++]);
                }
                if (link.Fed < link.Waiting.Count)
                {
                    return true;
                }
                link.Waiting.Clear();
                link.Fed = 0;
            }
            catch (Exception e)
            {
                Fail(link, e);
            }
            return false;
        }

        private static void Fail(Link link, Exception e)
        {
            link.Failed = true;
            link.Count = 0;
            link.Waiting.Clear();
            link.Fed = 0;
            Logger?.Error(e);
            Logger?.Warn("Map links: the places of '" + link.Id + "' could not be read (the error above); that kind of place is left off the city map this session.");
            try
            {
                if (link.Category != null)
                {
                    link.Category.Clear();
                }
            }
            catch (Exception)
            {
                // (the map itself is not as expected: MapPlaces has said so)
            }
        }

        // ---------------------------------------------------------------- the kinds

        private void Declare()
        {
            // Transport: every stop of the network, not only the three that are dressed near the player.
            _links.Add(new Link
            {
                Id = "bus_stops", TitleKey = "reallife_map_row_bus_stops", Colour = TransitDirector.StopColour, Icon = MapIcon.Bus,
                TickedByDefault = count => count <= ManyStops,
                Enabled = () => Settings.Buses,
                Stamp = () => TransitMap.Ready ? 1 + TransitMap.Places.Count : 0,
                Fill = BusStops
            });
            // Travel: the hotels (which used to add markers of their own).
            _links.Add(new Link
            {
                Id = "hotels", TitleKey = "reallife_map_row_hotels", Colour = new Color(0.70f, 0.55f, 0.12f), Icon = MapIcon.Bed, InStreet = true,
                Enabled = () => Settings.Hotels,
                Stamp = () =>
                {
                    TravelDirector travel = TravelDirector.Instance;
                    return travel != null && travel.Hotels != null ? 1 + travel.Hotels.Sites.Count + travel.Hotels.Moves * 64L : 0;   // (read again when one moves)
                },
                Fill = HotelSites
            });
            // Justice: the courthouse, where hearings are attended in person.
            _links.Add(new Link
            {
                Id = "courthouse", TitleKey = "reallife_map_row_courthouse", Colour = new Color(0.46f, 0.30f, 0.66f), Icon = MapIcon.Scales, InStreet = true,
                Enabled = () => JusticeSettings.Enabled && JusticeSettings.Courthouse,
                Stamp = () =>
                {
                    JusticeDirector justice = JusticeDirector.Instance;
                    return justice != null && justice.Court != null && justice.Court.Usable ? 2 + justice.Court.Moves * 4L : 0;   // (read again when it moves)
                },
                Fill = places =>
                {
                    Courthouse court = JusticeDirector.Instance.Court;
                    places.Add(new MapPlace(court.Front, At(T("reallife_map_row_courthouse"), court.Name), court.Building)
                    {
                        CardTypeKey = "reallife_justice_card_type",
                        CardStateKey = "reallife_justice_card_open"
                    });
                }
            });
            // Utilities: the office where power and water are applied for in person.
            _links.Add(new Link
            {
                Id = "utilities_office", TitleKey = "reallife_map_row_utilities", Colour = new Color(0.98f, 0.76f, 0.20f), Icon = MapIcon.Flag, InStreet = true,
                Enabled = () => Settings.Utilities,
                Stamp = () =>
                {
                    Utilities.UtilitiesDirector utilities = Utilities.UtilitiesDirector.Instance;
                    return utilities != null && utilities.Office != null && utilities.Office.Usable ? (SpecialBuildings.Active ? 3 : 2) + utilities.Office.Moves * 4L : 0;   // (read again when it moves)
                },
                Fill = places =>
                {
                    Utilities.UtilitiesOffice office = Utilities.UtilitiesDirector.Instance.Office;
                    string what = SpecialBuildings.NameFor(office.Building) ?? T("reallife_map_row_utilities");   // (its own name, while special buildings carry theirs)
                    places.Add(new MapPlace(office.Front, At(what, office.Name), office.Building)
                    {
                        CardTypeKey = "reallife_util_card_type",
                        CardStateKey = "reallife_util_card_open"
                    });
                }
            });
            // Careers: Hudson City University, a building the mod has made its own, with its own card: a row of its own under its
            // name and icon, there while the university's part is on (CareersDirector.UniversityOpen) and gone with it.
            _links.Add(new Link
            {
                Id = "university", TitleKey = "reallife_map_row_university", Colour = new Color(0.30f, 0.24f, 0.66f), Icon = MapIcon.Mortarboard, InStreet = true,
                Enabled = () => Careers.CareersDirector.UniversityOpen,
                Stamp = UniversityStamp,
                Fill = UniversityPlace
            });
            // Careers: the other places to study. The game's school (Manhattan Business School, which keeps its own card) and the
            // hospitals and clinics whose wards teach nursing.
            _links.Add(new Link
            {
                Id = "study", TitleKey = "reallife_map_row_study", Colour = new Color(0.55f, 0.16f, 0.22f), Icon = MapIcon.Star, InStreet = true,
                Enabled = () => Settings.CareersStudy,
                Stamp = StudyStamp,
                Fill = StudyPlaces
            });
            // Tech: the city's tech companies' towers, a floor each (Tech/Sites/Headquarters), where their jobs are.
            _links.Add(new Link
            {
                Id = "tech_hq", TitleKey = "reallife_map_row_tech_hq", Colour = new Color(0.18f, 0.42f, 0.82f), Icon = MapIcon.Tower, InStreet = true,
                Enabled = () => Settings.TechSiteHq,
                Stamp = () => Tech.TechSites.Instance != null ? Tech.TechSites.Instance.HeadquartersStamp : 0,
                Fill = places => Tech.TechSites.Instance.HeadquartersPlaces(places)
            });
            // Tech: the player's data centres, the regions of their cloud (a hyperscaler shows them all).
            _links.Add(new Link
            {
                Id = "tech_cloud", TitleKey = "reallife_map_row_cloud", Colour = new Color(0.08f, 0.62f, 0.68f), Icon = MapIcon.Cloud,
                Enabled = () => Settings.TechSiteCloud && Settings.TechSector,
                Stamp = () => Tech.TechSites.Instance != null ? Tech.TechSites.Instance.CloudStamp : 0,
                Fill = places => Tech.TechSites.Instance.CloudPlaces(places)
            });
            // Licences: the learner-plate boards at the school buildings (found a few buildings a second).
            _links.Add(new Link
            {
                Id = "driving_schools", TitleKey = "reallife_map_row_driving_schools", Colour = new Color(0.80f, 0.20f, 0.18f), Icon = MapIcon.Wheel, InStreet = true,
                Enabled = () => Settings.DrivingLicence && Settings.DrivingSchool,
                Stamp = () =>
                {
                    LicenceDirector licences = LicenceDirector.Instance;
                    return licences != null && licences.Desks != null ? 1 + licences.Desks.Count : 0;
                },
                Fill = places =>
                {
                    LicenceDesks desks = LicenceDirector.Instance.Desks;
                    for (int i = 0; i < desks.Count; i++)
                    {
                        places.Add(new MapPlace(desks.Front(i), At(T("reallife_map_driving_school"), desks.Address(i))) { Lit = desks.Building(i) });
                    }
                }
            });
            // Garage: where two-wheelers are hired and sold.
            _links.Add(new Link
            {
                Id = "two_wheelers", TitleKey = "reallife_map_row_two_wheelers", Colour = new Color(0.16f, 0.60f, 0.32f), Icon = MapIcon.Bicycle,
                Enabled = () => Settings.Garage && Settings.TwoWheelers,
                Stamp = () =>
                {
                    GarageDirector garage = GarageDirector.Instance;
                    BikePlaces bikes = garage != null && garage.Bikes != null ? garage.Bikes.Places : null;
                    return bikes != null && bikes.Found ? 1 + bikes.Count : 0;
                },
                Fill = places =>
                {
                    IReadOnlyList<BikePlace> all = GarageDirector.Instance.Bikes.Places.All;
                    for (int i = 0; i < all.Count; i++)
                    {
                        string what = all[i].Kind == BikePlaceKind.Dock ? "reallife_map_bike_dock"
                            : all[i].Kind == BikePlaceKind.BicycleShop ? "reallife_map_bicycle_shop" : "reallife_map_motorbike_dealer";
                        places.Add(new MapPlace(all[i].Position, At(T(what), BikePlaces.Whereabouts(all[i].Position))));
                    }
                }
            });
            // Garage: the repair stations whose bays the custom shop works at (one marker a station, at its first bay).
            _links.Add(new Link
            {
                Id = "custom_shops", TitleKey = "reallife_map_row_custom_shops", Colour = new Color(0.36f, 0.40f, 0.46f), Icon = MapIcon.Wrench,
                Enabled = () => Settings.Garage,
                Stamp = () =>
                {
                    GarageDirector garage = GarageDirector.Instance;
                    CityManager city = InstanceBehavior<CityManager>.Instance;
                    if (garage == null || garage.Shop == null || city == null || city.cityBuildingControllers == null || city.cityBuildingControllers.Length == 0)
                    {
                        return 0;   // (the bays are looked for once, and only when the city is there to look in)
                    }
                    return 1 + garage.Shop.Bays.Count;
                },
                Fill = CustomShops
            });
            // Jobs and careers: the help-wanted boards; a board that is an employer's office carries the employer's name.
            _links.Add(new Link
            {
                Id = "help_wanted", TitleKey = "reallife_map_row_help_wanted", Colour = new Color(0.52f, 0.36f, 0.24f), Icon = MapIcon.Briefcase,
                Enabled = () => Settings.Jobs,
                Stamp = () =>
                {
                    JobDirector jobs = JobDirector.Instance;
                    if (jobs == null || jobs.HelpBoards == null)
                    {
                        return 0;
                    }
                    long stamp = 1 + jobs.HelpBoards.Count;
                    if (CareerDirector.On && jobs.Career != null)
                    {
                        for (int e = 0; e < CareerCatalogue.All.Length; e++)
                        {
                            stamp = stamp * 9 + jobs.Career.DepotOf(e) + 2;
                        }
                    }
                    return stamp;
                },
                Fill = places =>
                {
                    JobDirector jobs = JobDirector.Instance;
                    JobBoards boards = jobs.HelpBoards;
                    for (int b = 0; b < boards.Count; b++)
                    {
                        int employer = CareerDirector.On && jobs.Career != null ? jobs.Career.EmployerAt(b) : -1;
                        string what = employer >= 0 && employer < CareerCatalogue.All.Length ? T(CareerCatalogue.All[employer].NameKey) : T("reallife_map_help_wanted");
                        places.Add(new MapPlace(boards.Front(b), At(what, boards.Address(b))) { Lit = boards.Building(b) });
                    }
                }
            });
            // The City Ledger: the newspaper kiosks near the player's home; the building each stands in front of lights up.
            _links.Add(new Link
            {
                Id = "newspaper_kiosks", TitleKey = "reallife_map_row_kiosks", Colour = new Color(0.16f, 0.38f, 0.27f), Icon = MapIcon.Paper, InStreet = true,
                Enabled = () => Settings.SocialNewspaper && Settings.LedgerKiosks,
                Stamp = () =>
                {
                    Ledger.LedgerDirector paper = Ledger.LedgerDirector.Instance;
                    Ledger.LedgerKiosks kiosks = paper != null ? paper.Kiosks : null;
                    return kiosks != null && kiosks.Count > 0 ? 1 + kiosks.Count + kiosks.Stamp * 8 : 0;
                },
                Fill = places =>
                {
                    Ledger.LedgerKiosks kiosks = Ledger.LedgerDirector.Instance.Kiosks;
                    for (int i = 0; i < kiosks.Count; i++)
                    {
                        places.Add(new MapPlace(kiosks.Front(i), At(T("reallife_map_kiosk"), kiosks.Address(i))) { Lit = kiosks.Building(i) });
                    }
                }
            });
            // Civic: each district's polling place, from the start of the campaign until the count.
            _links.Add(new Link
            {
                Id = "polling_places", TitleKey = "reallife_map_row_polling_places", Colour = new Color(0.14f, 0.18f, 0.42f), Icon = MapIcon.Ballot,
                Enabled = () => Settings.Civic && Settings.CivicElections,
                Stamp = () =>
                {
                    CivicDirector civic = CivicDirector.Instance;
                    return civic != null && civic.Running && civic.Government != null && civic.City != null && civic.City.Ready && civic.Government.Campaigning
                        ? 1 + civic.City.Districts.Count : 0;
                },
                Fill = places =>
                {
                    List<District> districts = CivicDirector.Instance.City.Districts;
                    for (int i = 0; i < districts.Count; i++)
                    {
                        if (districts[i] != null && districts[i].Polling != null)
                        {
                            places.Add(new MapPlace(districts[i].Door, At(T("reallife_map_polling_place"), T(districts[i].Id))) { Lit = districts[i].Polling });
                        }
                    }
                }
            });
            // Civic: the construction sites in progress; the markers follow the programme.
            _links.Add(new Link
            {
                Id = "construction", TitleKey = "reallife_map_row_construction", Colour = new Color(0.93f, 0.49f, 0.10f), Icon = MapIcon.Cone,
                Enabled = () => Settings.Civic && Settings.CivicConstruction,
                Stamp = () =>
                {
                    CivicDirector civic = CivicDirector.Instance;
                    if (civic == null || !civic.Running || civic.Works == null)
                    {
                        return 0;
                    }
                    List<SiteState> sites = civic.Works.State.Sites;
                    long stamp = 1;
                    for (int i = 0; i < sites.Count; i++)
                    {
                        if (sites[i] != null && !sites[i].Finished)
                        {
                            stamp = stamp * 31 + sites[i].Id + 1;
                        }
                    }
                    return stamp;
                },
                Fill = places =>
                {
                    List<SiteState> sites = CivicDirector.Instance.Works.State.Sites;
                    for (int i = 0; i < sites.Count; i++)
                    {
                        SiteState site = sites[i];
                        if (site == null || site.Finished)
                        {
                            continue;
                        }
                        string what;
                        try
                        {
                            what = CivicWorks.ProjectName(site);
                        }
                        catch (Exception)
                        {
                            what = T("reallife_map_row_construction");   // (a site from a save with a project this catalogue does not have)
                        }
                        places.Add(new MapPlace(new Vector3(site.X, site.Y, site.Z), At(what, CivicCity.AddressText(site.Street, site.Number)))
                        {
                            Lit = CivicCity.Find(site.Street, site.Number)   // (the building being worked on; null for a plot with none)
                        });
                    }
                }
            });
            // Leisure: the landmarks. One is a sight to tick off, and a waterside one says where a line can be cast.
            _links.Add(new Link
            {
                Id = "sights", TitleKey = "reallife_map_row_sights", Colour = new Color(0.50f, 0.64f, 0.14f), Icon = MapIcon.Star,
                Enabled = () => Settings.Leisure && (Settings.LeisureSights || Settings.LeisureFishing),
                Stamp = () => Leisure.LeisureVenues.Landmarks.Count == 0 ? 0
                    : 1 + Leisure.LeisureVenues.Landmarks.Count + Leisure.LeisureState.SeenCount * 100 + (Settings.LeisureSights ? 100000 : 0) + (Settings.LeisureFishing ? 200000 : 0),
                Fill = Sights
            });
            // What is on just now: each of these is at most one place, there for a while and then gone.
            _links.Add(new Link
            {
                Id = "whats_on", TitleKey = "reallife_map_row_whats_on", Colour = new Color(0.80f, 0.22f, 0.58f), Icon = MapIcon.Flag,
                Enabled = () => true,   // (each part asks its own feature's option)
                Stamp = () => WhatsOn(null),
                Fill = places => WhatsOn(places)
            });
        }

        // The university once it stands in a building of its own; read again when it moves or its name is switched on or off.
        private static long UniversityStamp()
        {
            Careers.CareersDirector careers = Careers.CareersDirector.Instance;
            Careers.CampusBuilding uni = careers != null ? careers.University : null;
            if (uni == null || !uni.Usable || uni.Building == null)
            {
                return 0;   // (still being chosen, none to be had, or its part is off: no row)
            }
            unchecked
            {
                long stamp = ((uni.Street ?? string.Empty).GetHashCode() * 1000003L + uni.Number) * 4 + (SpecialBuildings.Active ? 2 : 1);
                return stamp != 0L ? stamp : 1L;
            }
        }

        // The university's one place: its door, under its own name, and its building, which the row lights on the open map
        // and whose card on the map carries the name (Core/MapPlaces).
        private static void UniversityPlace(List<MapPlace> places)
        {
            Careers.CampusBuilding uni = Careers.CareersDirector.Instance.University;
            if (uni == null || !uni.Usable || uni.Building == null)
            {
                return;
            }
            places.Add(new MapPlace(uni.Front, SpecialBuildings.NameFor(uni.Building) ?? T("reallife_study_campus_university"), uni.Building)
            {
                CardTypeKey = "reallife_campus_card_type"
            });
        }

        // The game's school and the hospitals: there once the city's buildings are.
        private static long StudyStamp()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            return Careers.CareersDirector.Instance != null && city != null && city.cityBuildingControllers != null && city.cityBuildingControllers.Length > 0 ? 1 : 0;
        }

        private static void StudyPlaces(List<MapPlace> places)
        {
            // The game's school and the hospitals are found by their kind in the city map's search, under the game's own word.
            SpecialBuildings.Findable(Careers.CareersGame.SchoolType);
            SpecialBuildings.Findable(HospitalType);
            SpecialBuildings.Findable(ClinicType);
            CityBuildingController[] all = InstanceBehavior<CityManager>.Instance.cityBuildingControllers;
            string school = T("reallife_study_campus_school");
            string ward = T("reallife_map_study_ward");
            for (int i = 0; i < all.Length; i++)
            {
                CityBuildingController b = all[i];
                string type = b != null && b.building != null && b.building.SpecialService != null ? b.building.SpecialService.businessTypeName : null;
                bool isSchool = type == Careers.CareersGame.SchoolType;
                if (!isSchool && type != HospitalType && type != ClinicType)
                {
                    continue;
                }
                if (!JobSpots.Front(b, out Vector3 door))
                {
                    if (b.entranceDoors == null || b.entranceDoors.Length == 0 || b.entranceDoors[0] == null || b.entranceDoors[0].doorTransform == null)
                    {
                        continue;
                    }
                    door = b.entranceDoors[0].doorTransform.position;
                }
                places.Add(new MapPlace(door, isSchool ? school : At(ward, b.building.Address.ToFormattedString())) { Lit = b });
            }
        }

        private const string HospitalType = "ba:businesstype_hospital";
        private const string ClinicType = "ba:businesstype_clinic";

        private static void BusStops(List<MapPlace> places)
        {
            // One marker a place: a place several lines stop at names them all.
            List<TransitStop> stops = TransitMap.Places;
            string pattern = T("reallife_map_bus_stop");
            string shared = T("reallife_map_bus_stop_lines");
            for (int i = 0; i < stops.Count; i++)
            {
                TransitStop stop = stops[i];
                if (stop == null)
                {
                    continue;
                }
                TransitLine line = stop.Line >= 0 && stop.Line < TransitMap.Lines.Count ? TransitMap.Lines[stop.Line] : null;
                string name = stop.Calls.Count > 1
                    ? shared.Replace("{lines}", TransitMap.Numbers(stop, ", ")).Replace("{name}", stop.Name ?? (stop.Order + 1).ToString())
                    : pattern.Replace("{line}", line != null ? line.Number : string.Empty)
                        .Replace("{to}", line != null && !string.IsNullOrEmpty(line.ToKey) ? line.To : string.Empty)
                        .Replace("{name}", stop.Name ?? (stop.Order + 1).ToString());
                places.Add(new MapPlace(stop.Sign, name));
            }
        }

        private static void HotelSites(List<MapPlace> places)
        {
            IReadOnlyList<HotelSite> sites = TravelDirector.Instance.Hotels.Sites;
            for (int i = 0; i < sites.Count; i++)
            {
                HotelSite site = sites[i];
                if (site == null)
                {
                    continue;
                }
                // A hotel that is a building of its own has the game's card under its name; the cottages' board stands by
                // somebody else's building, which keeps its own card.
                bool own = site.Slot != null && site.Slot.Anchors == null;
                places.Add(new MapPlace(site.Desk, site.Name, own ? site.Building : null)
                {
                    CardTypeKey = own ? "reallife_map_card_hotel" : null,
                    CardStateKey = own ? "reallife_map_card_hotel_open" : null
                });
            }
        }

        private void CustomShops(List<MapPlace> places)
        {
            IReadOnlyList<GasStationTrigger> bays = GarageDirector.Instance.Shop.Bays;
            _spots.Clear();
            string what = T("reallife_map_custom_shop");
            for (int i = 0; i < bays.Count; i++)
            {
                if (bays[i] == null)
                {
                    continue;
                }
                Vector3 at = bays[i].transform.position;
                bool near = false;
                for (int s = 0; s < _spots.Count && !near; s++)
                {
                    near = (_spots[s] - at).sqrMagnitude < 45f * 45f;   // another bay of a station that is already there
                }
                if (near)
                {
                    continue;
                }
                _spots.Add(at);
                places.Add(new MapPlace(at, At(what, BikePlaces.Whereabouts(at))));
            }
        }

        private static void Sights(List<MapPlace> places)
        {
            List<Leisure.Landmark> landmarks = Leisure.LeisureVenues.Landmarks;
            for (int i = 0; i < landmarks.Count; i++)
            {
                Leisure.Landmark landmark = landmarks[i];
                if (landmark == null)
                {
                    continue;
                }
                bool sight = Settings.LeisureSights;
                bool fishing = Settings.LeisureFishing && landmark.Waterside;
                if (!sight && !fishing)
                {
                    continue;
                }
                bool seen = sight && Leisure.LeisureState.HasSeen(landmark.Id);
                string key = !sight ? "reallife_map_sight_fishing_only"
                    : seen ? (fishing ? "reallife_map_sight_seen_fishing" : "reallife_map_sight_seen")
                    : fishing ? "reallife_map_sight_fishing" : null;
                string name = T(landmark.NameKey);
                places.Add(new MapPlace(landmark.Position, key != null ? T(key).Replace("{name}", name) : name));
            }
        }

        // A street event (never a fire: the incident marker has that), a campaign rally, the chess player's table, the
        // place a plan with somebody is agreed for. With no list: only the stamp (zero for none of them).
        private static long WhatsOn(List<MapPlace> places)
        {
            long stamp = 0L;
            if (Settings.StreetEvents && Events.EventDirector.StreetEvent(out Vector3 at, out string nameKey))
            {
                stamp = Mix(stamp, 1, at);
                places?.Add(new MapPlace(at, T(string.IsNullOrEmpty(nameKey) ? "reallife_map_row_whats_on" : nameKey)));
            }
            CivicDirector civic = CivicDirector.Instance;
            if (Settings.Civic && Settings.CivicElections && civic != null && civic.Running && civic.Campaign != null && civic.Campaign.RallyOn(out at))
            {
                stamp = Mix(stamp, 2, at);
                places?.Add(new MapPlace(at, T("reallife_map_rally")));
            }
            Leisure.LeisureDirector leisure = Leisure.LeisureDirector.Instance;
            if (Settings.Leisure && Settings.LeisureChess && leisure != null && leisure.ChessTable(out at))
            {
                stamp = Mix(stamp, 3, at);
                places?.Add(new MapPlace(at, T("reallife_map_chess")));
            }
            SocialPlan plan = SocialState.Plan;
            // (Once the outing itself has begun the game's own destination guider is at the place, on the map too: ours stands down.)
            if (Settings.Social && SocialState.Loaded && plan != null && plan.Active && (plan.X != 0f || plan.Z != 0f) && !SocialDirector.Guiding)
            {
                at = new Vector3(plan.X, plan.Y, plan.Z);
                stamp = Mix(stamp, 4 + plan.Person, at);
                if (places != null)
                {
                    SocialPerson who = SocialState.Get(plan.Person);
                    string place = string.IsNullOrEmpty(plan.Place) ? T("reallife_map_row_whats_on") : plan.Place;
                    places.Add(new MapPlace(at, who != null && !string.IsNullOrEmpty(who.Name)
                        ? T("reallife_map_plan").Replace("{who}", who.Name).Replace("{place}", place) : place));
                }
            }
            return stamp;
        }

        private static long Mix(long stamp, int what, Vector3 at)
        {
            return (stamp * 31 + what) * 1000003L + Mathf.RoundToInt(at.x) * 4099L + Mathf.RoundToInt(at.z);
        }

        // ---------------------------------------------------------------- words

        private static string T(string key)
        {
            return string.IsNullOrEmpty(key) ? string.Empty : key.GetLocalization();
        }

        // "Driving school: 12 Third Street"; without a place, the thing by itself.
        private static string At(string what, string place)
        {
            return string.IsNullOrEmpty(place) ? what : T("reallife_map_name_at").Replace("{what}", what).Replace("{place}", place);
        }
    }
}
