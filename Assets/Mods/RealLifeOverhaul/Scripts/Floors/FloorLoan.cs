using System;

namespace RealLifeOverhaul.Floors
{
    /// <summary>What the floors do for one of the game's steps (<see cref="FloorLoan"/>'s answer), as flags.</summary>
    [Flags]
    public enum FloorDo
    {
        None = 0,
        Borrow = 1,                // the building is read as the floor's structure from now, its own size and version kept
        GiveBack = 2,              // the building's own size and version are put back
        PutBackDirt = 4,           // its registration's dirt spots are put back as they were before a borrowed structure was loaded
        Arrive = 8,                // the player is on the floor asked for: a visit begins
        Shadow = 16,               // the manager's registration is the place's own from now (never saved: it is in no list of the save)
        Leave = 32,                // the visit ends: the place's furniture and its own parts go
        PutBackRegistration = 64,  // the manager's registration is the building's own again while the player is still inside
        TakeOut = 128,             // the player is taken out to the street: the floor stood on a structure that is not the building's own
        Refused = 256              // nothing was done: another going in is under way, or there is no visit to change
    }

    /// <summary>
    /// What the floors hold of the game's while the player goes in, moves between floors and leaves: the building read as
    /// another structure while the game loads it, its registration's dirt spots the game rewrites for that structure, the
    /// manager's registration swapped for the place's own, and the floor asked for until the player is in. The director
    /// tells it each step of the game's (a going in asked or seen, the game's enter and exit events, the going in over, a
    /// lift, a place gone, the city gone) and does what it answers; nothing is decided anywhere else, so FloorsChecks can
    /// drive it through every way in and out and see each thing given back exactly once. Game-free; nothing of it is saved.
    /// </summary>
    public sealed class FloorLoan
    {
        public const float MostSeconds = 15f;   // real seconds the game is given to load a borrowed structure; then it is given back regardless

        private string _borrowed;        // the building whose size and version are borrowed, else null
        private float _borrowedAt;       // real time it was borrowed
        private bool _dirt;              // its registration's dirt spots are kept, to be put back with the size
        private string _pending;         // the building a floor was asked of, until the player is in
        private int _pendingFloor;
        private bool _pendingBorrowed;   // ...on a structure not the building's own
        private bool _reentering;        // ...from a floor of the same building, through the game's door: its exit first is no leaving
        private string _visit;           // the building of the floor the player is on, else null
        private int _visitFloor;
        private bool _visitBorrowed;     // ...on a structure not the building's own
        private bool _shadowed;          // ...with the manager's registration the place's own

        /// <summary>True while a building reads as another structure.</summary>
        public bool Borrowed => _borrowed != null;

        public string BorrowedOf => _borrowed;

        /// <summary>True from a floor asked for until the game has gone in, or given up.</summary>
        public bool Pending => _pending != null;

        public string PendingOf => _pending;

        public int PendingFloor => _pendingFloor;

        /// <summary>True when the floor asked for stands on a structure that is not the building's own.</summary>
        public bool PendingBorrowed => _pendingBorrowed;

        public bool Reentering => _reentering;

        /// <summary>True while the player is on a floor of a building the mod holds.</summary>
        public bool OnFloor => _visit != null;

        public string VisitOf => _visit;

        public int VisitFloor => _visitFloor;

        public bool VisitBorrowed => _visitBorrowed;

        public bool Shadowed => _shadowed;

        /// <summary>
        /// The player is to go in to a floor: a door's button, an app, the way back after a load, a lift to a floor of another
        /// structure (<paramref name="reenter"/>), a test. With <paramref name="borrow"/> the building reads as the floor's
        /// structure from now. Refused, holding nothing, while another going in is under way.
        /// </summary>
        public FloorDo Ask(string building, int floor, bool borrow, bool reenter, float now)
        {
            if (string.IsNullOrEmpty(building) || floor < 1 || _pending != null || _borrowed != null || (reenter && _visit != building))
            {
                return FloorDo.Refused;
            }
            _pending = building;
            _pendingFloor = floor;
            _pendingBorrowed = borrow;
            _reentering = reenter;
            if (!borrow)
            {
                return FloorDo.None;
            }
            _borrowed = building;
            _borrowedAt = now;
            _dirt = true;
            return FloorDo.Borrow;
        }

        /// <summary>The game has left a building (its onExitBuilding): the visit there ends; a floor asked for stays asked.</summary>
        public FloorDo Exited(string building)
        {
            if (_visit == null || !string.Equals(building, _visit, StringComparison.Ordinal))
            {
                return FloorDo.None;
            }
            ClearVisit();   // the game lets the manager's registration go with the building
            return FloorDo.Leave;
        }

        /// <summary>
        /// The game has entered a building (its onEnterBuilding): whatever it entered, a borrowed structure has been loaded or
        /// passed over and goes back; the player is on the floor asked for if it is that building's, with the place's own
        /// registration when <paramref name="shadow"/>.
        /// </summary>
        public FloorDo Entered(string building, bool shadow)
        {
            FloorDo d = FloorDo.None;
            if (_visit != null)
            {
                d |= FloorDo.Leave;   // an exit the game made without saying so: the old visit is over all the same
                ClearVisit();
            }
            d |= GiveBackNow();
            if (_pending != null && string.Equals(building, _pending, StringComparison.Ordinal))
            {
                _visit = _pending;
                _visitFloor = _pendingFloor;
                _visitBorrowed = _pendingBorrowed;
                _shadowed = shadow;
                d |= FloorDo.Arrive | (shadow ? FloorDo.Shadow : FloorDo.None);
            }
            ClearPending();
            return d;
        }

        /// <summary>The game's going in is over and no floor came of it (it refused, or failed): what was borrowed goes back.</summary>
        public FloorDo EnteringOver()
        {
            FloorDo d = GiveBackNow();
            ClearPending();
            return d;
        }

        /// <summary>The game has taken longer than <see cref="MostSeconds"/> to go in: the building's own size and version go back, and the floor is not waited for.</summary>
        public FloorDo Timeout(float now)
        {
            if (_borrowed == null || now - _borrowedAt < MostSeconds)
            {
                return FloorDo.None;
            }
            FloorDo d = GiveBackNow();
            ClearPending();
            return d;
        }

        /// <summary>
        /// The player is inside a building the mod holds on no floor (a save made inside, or a way in that was not seen) and its
        /// floor stands on the building's own structure: on it now, as the game's door would have put them.
        /// </summary>
        public FloorDo Found(string building, int floor, bool shadow)
        {
            if (string.IsNullOrEmpty(building) || floor < 1 || _pending != null || _borrowed != null || _visit != null)
            {
                return FloorDo.Refused;
            }
            _visit = building;
            _visitFloor = floor;
            _visitBorrowed = false;
            _shadowed = shadow;
            return FloorDo.Arrive | (shadow ? FloorDo.Shadow : FloorDo.None);
        }

        /// <summary>A lift to another floor of the structure on show: only the floor changes.</summary>
        public FloorDo Moved(int floor)
        {
            if (_visit == null || floor < 1)
            {
                return FloorDo.Refused;
            }
            _visitFloor = floor;
            return FloorDo.None;
        }

        /// <summary>
        /// The building is no longer the mod's, or the place of the floor the player is on is gone (its option switched off): a
        /// going in under way gets the building's own structure, the manager's registration is the building's own again and, on a
        /// floor of a structure that is not the building's own, the player is taken out.
        /// </summary>
        public FloorDo Gone(string building)
        {
            FloorDo d = FloorDo.None;
            if (_borrowed != null && string.Equals(building, _borrowed, StringComparison.Ordinal))
            {
                d |= GiveBackNow();
            }
            if (_pending != null && string.Equals(building, _pending, StringComparison.Ordinal))
            {
                ClearPending();
            }
            if (_visit != null && string.Equals(building, _visit, StringComparison.Ordinal))
            {
                d |= FloorDo.Leave | (_shadowed ? FloorDo.PutBackRegistration : FloorDo.None) | (_visitBorrowed ? FloorDo.TakeOut : FloorDo.None);
                ClearVisit();
            }
            return d;
        }

        /// <summary>The city is going, or the floors stand down: everything borrowed goes back now (the game drops the manager's registration itself).</summary>
        public FloorDo CityGone()
        {
            FloorDo d = GiveBackNow();
            if (_visit != null)
            {
                d |= FloorDo.Leave;
                ClearVisit();
            }
            ClearPending();
            return d;
        }

        // The borrowed size and version, and the dirt spots with them, go back.
        private FloorDo GiveBackNow()
        {
            if (_borrowed == null)
            {
                return FloorDo.None;
            }
            FloorDo d = FloorDo.GiveBack | (_dirt ? FloorDo.PutBackDirt : FloorDo.None);
            _borrowed = null;
            _dirt = false;
            return d;
        }

        private void ClearPending()
        {
            _pending = null;
            _pendingFloor = 0;
            _pendingBorrowed = false;
            _reentering = false;
        }

        private void ClearVisit()
        {
            _visit = null;
            _visitFloor = 0;
            _visitBorrowed = false;
            _shadowed = false;
        }
    }
}
