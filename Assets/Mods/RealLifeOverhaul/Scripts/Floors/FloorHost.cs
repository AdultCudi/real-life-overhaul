using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Floors
{
    /// <summary>
    /// A building the mod holds, with floors: what its holder (the university, a tech tower, the core itself for a building
    /// taken for places) gives the floors core. The holder sets the building, the place outside its door, its name and its own
    /// places from floor 1, then hands it over (FloorsDirector.Hold) and takes it back (Let); the core keeps the floor table, the
    /// panels at its door and lift, and its doors. A host needs a CityBuildingController with its Building (address, size,
    /// version, floor area) and an entrance door; its registration is found by address (BuildingHelper.GetBuildingRegistration),
    /// so a building made at run time serves as well as one of the city's. Nothing of it is saved.
    /// </summary>
    public abstract class FloorHost
    {
        private Func<string> _doorWhat, _liftWhat;   // the panels' log words, made once
        private bool _doorWhatOpen;
        private int _liftWhatFloor;

        protected FloorHost(string id, string doorCheck, string liftCheck)
        {
            Id = id;
            DoorPanel = new DoorPanel(FloorsDirector.Logger, doorCheck);
            LiftPanel = new DoorPanel(FloorsDirector.Logger, liftCheck);
        }

        /// <summary>Its holder's id for it (its id in Core/SpecialBuildings).</summary>
        public readonly string Id;

        /// <summary>The holder's own places, from floor 1; spare floors above them go to places that ask (FloorsDirector.Ask).</summary>
        public readonly List<FloorPlace> Own = new List<FloorPlace>(4);

        public CityBuildingController Building;
        public Vector3 Door;       // where the player stands outside to choose a floor
        public string Name;        // the panels' first line, final words
        public int Rank;           // its holder's turn for spare floors (FloorsRules.RankTowers, RankCampus, RankClaimed)

        internal readonly DoorPanel DoorPanel;   // the game's panel at its door, with its holder's self-check name
        internal readonly DoorPanel LiftPanel;   // ...and at its lift
        internal FloorSlot[] Table = new FloorSlot[0];   // floor - 1: the place on it
        internal int Used;                       // floors its own places take
        internal int Capacity;                   // floors it holds in all (FloorsRules.Capacity)
        internal string Key;                     // street#number while held
        internal bool[] DoorWas;                 // its doors' own "always open" before the floors opened them
        internal bool DoorsOpen;
        internal CityBuildingController DoorsOf; // the building the flags were taken of
        internal Action[] DoorPress, LiftPress;  // its panels' buttons, made once (each marks its own panel's key frame)
        internal readonly int[] DoorFloors = new int[FloorsRules.Slots];   // the floor on each button: above 0 a floor, -1 "more floors", 0 none
        internal readonly int[] LiftFloors = new int[FloorsRules.Slots];
        internal int DoorPage, LiftPage;

        /// <summary>The floors it has now: its own places' and the places given spare floors.</summary>
        public int Floors => Table.Length;

        /// <summary>The place on a floor (from 1), or null.</summary>
        public FloorPlace PlaceOn(int floor) => floor >= 1 && floor <= Table.Length ? Table[floor - 1].Place : null;

        /// <summary>Which of its place's floors a floor is, from 1; 0 for none.</summary>
        public int PartOf(int floor) => floor >= 1 && floor <= Table.Length ? Table[floor - 1].Part : 0;

        /// <summary>True while its doors let the player in, and its floors may be chosen at the door.</summary>
        public virtual bool Open() => true;

        /// <summary>True while the panel stands at its door at all: by default while open (a holder may show it closed, its floors greyed).</summary>
        public virtual bool ShowsDoor(bool open) => open;

        /// <summary>The floor the game's own door takes the player to; <paramref name="afterLoad"/> when they were found inside after a load. Asked often: no side effects.</summary>
        public virtual int DefaultFloor(bool afterLoad) => 1;

        /// <summary>The game's own click on the building was seen going in, to this floor.</summary>
        public virtual void GameWayIn(int floor)
        {
        }

        /// <summary>The door panel's second line: the words at its left and at its right, and the right one's colour.</summary>
        public virtual void DoorLine(bool open, out string left, out string right, out Color32 colour)
        {
            left = FloorsText.T("reallife_floors_host_door", "count", Floors.ToString());
            right = (open ? "common_open" : "common_closed").GetLocalization();
            colour = open ? WorldPanel.Green() : WorldPanel.Red();
        }

        /// <summary>Its doors were opened to the player, or shut again (for its holder's log line).</summary>
        public virtual void DoorsChanged(bool open)
        {
        }

        /// <summary>The door panel was shown (<paramref name="up"/>: the game took it), open or closed.</summary>
        public virtual void DoorUp(bool up, bool open)
        {
        }

        /// <summary>The player was found inside on no floor (a save made inside, or a way in not seen): taken to a floor, or out while closed.</summary>
        public virtual void FoundInside(int floor, bool throughDoor, bool open)
        {
        }

        /// <summary>The door panel's words in its self-check line.</summary>
        public virtual string DoorWhat(bool open) => "at the door of " + Name;

        /// <summary>The lift panel's words in its self-check line.</summary>
        public virtual string LiftWhat(int floor) => "the lift of " + Name + " on floor " + floor;

        // The panels' log words as delegates made once (DoorPanel asks one only when it writes its line).
        internal Func<string> DoorWords(bool open)
        {
            _doorWhatOpen = open;
            return _doorWhat ?? (_doorWhat = () => DoorWhat(_doorWhatOpen));
        }

        internal Func<string> LiftWords(int floor)
        {
            _liftWhatFloor = floor;
            return _liftWhat ?? (_liftWhat = () => LiftWhat(_liftWhatFloor));
        }

        /// <summary>Both its panels down. Safe at any time.</summary>
        internal void HidePanels(string why)
        {
            DoorPanel.Hide(why);
            LiftPanel.Hide(why);
        }
    }
}
