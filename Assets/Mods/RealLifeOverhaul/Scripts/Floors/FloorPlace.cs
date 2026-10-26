using System.Collections.Generic;
using RealLifeOverhaul.FitOut;

namespace RealLifeOverhaul.Floors
{
    /// <summary>
    /// A place on a floor of a building the mod holds: what a feature declares, and the floors core gives a floor, furnishes and
    /// takes the player to. A plain one needs no code of its own:
    ///
    ///   _office = new FloorPlace("cityhall", "civic", "reallife-cityhall") { Structure = new FloorStructure("ba:buildingsize_c", 1), Fit = FitKind.CivicOffice };
    ///   _office.Designs.Add("LawFirm");
    ///   FloorsDirector.Ask(_office);                        // a floor, now or as soon as a building has one
    ///   FloorsDirector.Where(_office, out FloorWhere at);   // where it is, for the map, the route guider and the Guide
    ///   FloorsDirector.Release(_office, "its option is off");
    ///
    /// A place with more to it (the university's classrooms, a tech company's lobby) overrides the hooks below. Each is called on
    /// the main thread from the core's one-second timer or one of the game's own events, never per frame.
    /// </summary>
    public class FloorPlace
    {
        private readonly Dictionary<int, object> _args = new Dictionary<int, object>(4);   // a floor's button arguments, made once a floor

        public FloorPlace(string id, string feature, string name, bool literal = false)
        {
            Id = id;
            Feature = feature;
            NameKey = name;
            Literal = literal;
        }

        /// <summary>Its own id, unique among places: the order of spare floors goes by it, the same on every machine.</summary>
        public readonly string Id;

        /// <summary>
        /// The feature of Meta/FeatureList that declares it: while that feature stands down in this city or is switched off after its
        /// faults, the core puts its floor away (the player off it first), and asks for it again once the feature is back.
        /// </summary>
        public readonly string Feature;

        public string NameKey;     // a locale key; with Literal the name itself (a company's proper name, never translated)
        public bool Literal;
        public int Floors = 1;     // floors it takes, one above the other
        public FloorStructure Structure;   // the game's interior its floors stand on; null for the building's own
        public FloorLayout Layout;         // the game's own furniture laid in by the core: a layout of a business type for that structure
        public string LayLast;             // ...its pieces of this item laid last (a classroom door after the chair it finds)
        public FitKind? Fit;               // else FitOut's fit-out of this kind round the door, the game's designs first (Designs)
        public readonly List<string> Designs = new List<string>(6);   // the kinds of business whose designs may furnish it, in order
        public string Check = "Floors check (way in)";           // its self-check lines on the way in begin so
        public string LayCheck = "Floors check (floor)";         // ...and those of its laying
        public string LogName;                                   // what it is in the log ("the university"); its name when null
        public string StructureWords = "the floor's structure";  // its structure in the log ("the school's structure")
        public int ClearFrames;    // frames a lift waits after its furniture went before the next floor's goes up: the cuts out of the walkable floor go with it
        public bool FacesIn;       // a lift turns the player the way they came in: its floor is measured from where they face

        internal FloorHost Host;   // the building the core put it in, null while it has no floor
        internal int First;        // its first floor there, 0 with none
        internal bool Guest;       // asked for by a feature (FloorsDirector.Ask), not one of a holder's own

        /// <summary>The building it is in now, or null.</summary>
        public FloorHost In => Host;

        /// <summary>Its first floor, from 1; 0 while it has none.</summary>
        public int FirstFloor => Host != null ? First : 0;

        /// <summary>Its name now, in the player's language.</summary>
        public virtual string Name => Literal ? NameKey : FloorsText.T(NameKey);

        /// <summary>What it is in the log.</summary>
        public string Said => LogName ?? (Literal ? NameKey : Id);

        /// <summary>True while its floor may be chosen at its building's door; its button is greyed otherwise.</summary>
        public virtual bool Open() => true;

        /// <summary>
        /// A floor's button at the door and the lift: a locale key and arguments made once and kept (the game's panel compares them
        /// by reference), marked in blue where the player is wanted.
        /// </summary>
        public virtual void Button(int floor, int part, out string key, out object args, out bool marked)
        {
            key = "reallife_floors_floor";
            args = FloorArgs(floor);
            marked = false;
        }

        /// <summary>The lift's second line on one of its floors.</summary>
        public virtual string LiftLine(int floor, int part) => FloorsText.T("reallife_floors_lift_here", "floor", floor.ToString(), "place", Name);

        /// <summary>True while the lift is not offered on its floor: the player is busy with what is done there.</summary>
        public virtual bool Busy() => false;

        /// <summary>Its button at a door was pressed: the player goes in to the floor. A place may refuse with a word of its own instead.</summary>
        public virtual void Pressed(FloorHost host, int floor)
        {
            FloorsDirector.Enter(host, floor, "the floor button at the door");
        }

        /// <summary>The player is on it now: in by its building's door, found inside after a load, or by the lift from another place.</summary>
        public virtual void Arrive(FloorVisit visit)
        {
        }

        /// <summary>Its layout (<see cref="Layout"/>) stands, and the game has read its pieces.</summary>
        public virtual void Laid(FloorVisit visit, FloorLaid laid)
        {
        }

        /// <summary>By the lift from another of its own floors, on the same furniture, under the dark screen.</summary>
        public virtual void Moved(FloorVisit visit, int fromFloor)
        {
        }

        /// <summary>A lift to it is over and the screen is light again.</summary>
        public virtual void Shown(FloorVisit visit)
        {
        }

        /// <summary>The player is off it: out of the building, by the lift to another place, it or its building gone, the city going.</summary>
        public virtual void Leave(FloorVisit visit)
        {
        }

        /// <summary>A lift to it keeps the screen dark until this is true, six seconds at most: its furniture stands.</summary>
        public virtual bool Ready(FloorVisit visit) => true;

        /// <summary>Once a second while the player is on it.</summary>
        public virtual void Tick(FloorVisit visit)
        {
        }

        /// <summary>
        /// The manager's registration while the player is on it, made as the game has just loaded the floor: the place's own,
        /// kept in no list of the save, or null to keep the game's. On a structure not the building's own (<paramref name="borrowed"/>)
        /// one is always wanted: its dirt spots are the structure's, while the building's own registration gets its own back.
        /// </summary>
        public virtual BuildingRegistration MakeShadow(BuildingRegistration real, bool borrowed)
        {
            return borrowed && Host != null ? FloorsGame.PlainShadow(real, Host.Building, Name) : null;
        }

        // The plain button's arguments for a floor, made once a floor.
        protected object FloorArgs(int floor)
        {
            if (!_args.TryGetValue(floor, out object args))
            {
                args = new { floor = floor.ToString(), place = Name };
                _args[floor] = args;
            }
            return args;
        }
    }
}
