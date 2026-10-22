namespace RealLifeOverhaul
{
    /// <summary>
    /// What of the mod's own in the street is hidden beyond the game's far band (<see cref="ModCulling"/>): only the kinds
    /// of <see cref="Kind"/>; <see cref="Inventory"/> says how every other kind is kept cheap. Checked offline (CullCheck.cs).
    /// </summary>
    public static class CullRules
    {
        public const float NearBand = 60f;    // metres from the player: the game's first band
        public const float FarBand = 200f;   // metres from the player: the game's second band; beyond it a registered thing is not drawn

        public const int Near = 0;            // a band as the engine reports it: within NearBand
        public const int Middle = 1;          // between the two
        public const int Far = 2;             // beyond FarBand

        /// <summary>The kinds of thing culled here; anything else is never registered.</summary>
        public enum Kind
        {
            BusStop,           // a bus stop's pole, shelter, lettering and strips: every place of the city stands all session
            StationFront,      // a police station's fascia, sign, lamps, bollards, flag pole and the patrol cars parked by it
            BuildingSign,      // the university's, the courthouse's and the driving school's signs: all session
            JobBoard,          // the help-wanted boards beside the special services' doors: all session
            NewsKiosk,         // the two newspaper kiosks near the player's home: all session
            Count
        }

        /// <summary>How a kind of the mod's things is kept from costing frames when it is far away.</summary>
        public enum Way
        {
            Here,           // registered with ModCulling: not drawn beyond FarBand
            OwnDistance,    // its owner puts it out near the player and takes it in further off (the distance is in the reason)
            OutOfView,      // its pool hides it whenever it is out of view
            Indoors,        // it stands inside a building, which the game draws only while the player is in it
            Game,           // it is the game's own object, culled by the game
            Never           // gameplay needs it while it matters: never culled
        }

        /// <summary>One line of the inventory.</summary>
        public sealed class Thing
        {
            public readonly string What;
            public readonly Way Way;
            public readonly Kind Kind;          // Kind.Count unless Way is Here
            public readonly string Files;       // the source files (under Scripts) that make it, separated by spaces
            public readonly string Why;

            public Thing(string what, Way way, Kind kind, string files, string why)
            {
                What = what;
                Way = way;
                Kind = kind;
                Files = files;
                Why = why;
            }
        }

        /// <summary>Every kind of thing the mod puts in the world, and how each is kept cheap far away.</summary>
        public static readonly Thing[] Inventory =
        {
            new Thing("bus stops: pole, shelter, bench, lettering, strips", Way.Here, Kind.BusStop, "Transport/BusStopDressing.cs",
                "every place of the city is dressed while the city is loaded; the bay's paint already ends at 120 m (RoadPaint)"),
            new Thing("police station fronts and their parked patrol cars", Way.Here, Kind.StationFront, "Law/PrecinctFrontage.cs",
                "dressed within 260 m and taken in beyond 330 m: drawn only to the far band in between"),
            new Thing("the university's, the courthouse's and the driving school's signs", Way.Here, Kind.BuildingSign,
                "Careers/CampusBuilding.cs Justice/Courthouse.cs Licences/Driving/DrivingSchool.cs", "they stand all session"),
            new Thing("help-wanted boards", Way.Here, Kind.JobBoard, "Jobs/JobBoards.cs", "they stand all session"),
            new Thing("newspaper kiosks", Way.Here, Kind.NewsKiosk, "Ledger/LedgerKiosks.cs", "they stand all session near the home"),
            new Thing("hotel fronts and the doorman", Way.OwnDistance, Kind.Count, "Travel/Hotels.Frontage.cs", "dressed within 150 m"),
            new Thing("data centre fronts", Way.OwnDistance, Kind.Count, "Tech/Sites/Frontages.cs", "dressed within 160 m"),
            new Thing("bike docks and stands", Way.OwnDistance, Kind.Count, "Garage/BikePlaces.cs", "stood within 90 m"),
            new Thing("the living city's stalls, carts, brooms and cones", Way.OwnDistance, Kind.Count, "CityLife/CityProps.cs",
                "a site is taken away beyond 150 m, or out of view beyond 95 m"),
            new Thing("civic stands and dressed sites", Way.OwnDistance, Kind.Count, "Civic/CivicProps.cs Civic/SiteDresser.cs",
                "taken in beyond 160 m and 175 m"),
            new Thing("a parked patrol car with officers", Way.OwnDistance, Kind.Count, "People/PolicePresence.cs",
                "put out 45 to 95 m away out of view, taken away beyond 175 m"),
            new Thing("street people of the mod (crowds, staff, officers, crews)", Way.OutOfView, Kind.Count, "CityLife/CityPeople.cs People/ActorPool.cs",
                "their pools stop drawing whoever is out of view"),
            new Thing("police stations, offices, server rooms, the showroom, the garage, campus rooms, home life", Way.Indoors, Kind.Count,
                "Law/StationInterior.cs Tech/Sites/OfficeView.cs Tech/Sites/ServerRoomView.cs Economy/Dealer/Floor/ShowroomGame.cs Garage/GarageCar.cs",
                "inside buildings"),
            new Thing("signs on the mod's buildings", Way.Game, Kind.Count, "Core/FrontSigns.cs",
                "the game's own sign holders, whose lights the game puts out beyond its near band"),
            new Thing("fires: flames, smoke, steam, hoses, the fire light", Way.Never, Kind.Count, "Events/FireEffects.cs Events/Hose.cs",
                "an incident in progress"),
            new Thing("accident scenes: wrecks, tape, cones, barriers, beacons, emergency vehicles", Way.Never, Kind.Count,
                "Traffic/SceneCordon.Props.cs Traffic/TrafficEffects.cs Traffic/EmergencyDriver.cs", "an incident in progress"),
            new Thing("police pursuits and reckless drivers", Way.Never, Kind.Count, "Traffic/PolicePursuit.cs Traffic/Reckless/RecklessDrivers.cs",
                "a car the player chases or is chased by"),
            new Thing("the city bus on the road", Way.Never, Kind.Count, "Transport/BusVehicle.cs", "the player waits for it or rides it"),
            new Thing("the tow truck and taxis", Way.Never, Kind.Count, "Traffic/Tow/TowTruck.cs Transport/Taxi/TaxiDirector.cs",
                "the player's shift or the player's ride"),
            new Thing("street events and their people", Way.Never, Kind.Count, "Events/EventProps.cs Events/EventPeople.cs", "an event in progress"),
            new Thing("jobs' carts, flyers and instruments", Way.Never, Kind.Count, "Jobs/JobProps.cs", "the player's shift"),
            new Thing("leisure, the driving test's bay and beacons, run checkpoints, community service", Way.Never, Kind.Count,
                "Leisure/LeisureProps.cs Licences/LicenceProps.cs Home/JogRun.cs Justice/CommunityService.cs", "the player's own activity, in progress"),
            new Thing("friends, family and roommates met in the street", Way.Never, Kind.Count, "Family/FamilyCast.cs Social/SocialDirector.cs",
                "a person in a conversation"),
            new Thing("the player's own bikes", Way.Never, Kind.Count, "Garage/BikeBody.cs", "the player's own vehicle, drawn as the game draws the player's cars"),
        };

        /// <summary>True for the kinds this file lets <see cref="ModCulling"/> take.</summary>
        public static bool MayCull(Kind kind) => kind >= Kind.BusStop && kind < Kind.Count;

        /// <summary>Whether a thing is drawn in a band: always within the far band, and while the city map shows the whole city.</summary>
        public static bool Drawn(Kind kind, int band, bool mapOpen) => !MayCull(kind) || mapOpen || band < Far;

        /// <summary>The kind's name for the census.</summary>
        public static string Name(Kind kind)
        {
            switch (kind)
            {
                case Kind.BusStop: return "bus stops";
                case Kind.StationFront: return "police station fronts";
                case Kind.BuildingSign: return "building signs";
                case Kind.JobBoard: return "help-wanted boards";
                case Kind.NewsKiosk: return "newspaper kiosks";
                default: return "other";
            }
        }
    }
}
