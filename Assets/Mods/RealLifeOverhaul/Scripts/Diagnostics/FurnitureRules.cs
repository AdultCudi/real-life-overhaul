namespace RealLifeOverhaul
{
    /// <summary>
    /// Why the game would not lift a piece of furniture on a right click, in the order its own right click asks
    /// (ItemController.SecondaryInteract, then PlacementHelper.StartPlacementMode). Only <see cref="InUse"/>,
    /// <see cref="Structure"/> and <see cref="Residential"/> come with a notice of the game's; the rest do nothing at all.
    /// </summary>
    public enum MoveRefusal
    {
        None = 0,           // lifted: it follows the pointer
        Hidden = 1,         // the game has it hidden (another floor shown, the city map open)
        Activity = 2,       // an activity of the game's panel is under way
        Outdoors = 3,       // the player is not in a building
        AtTheDoor = 4,      // the player is going in or out
        NotYours = 5,       // a building the player neither rents nor owns
        InUse = 6,          // in use, by the game's own sum (ItemController.Occupied): its notice says it cannot be picked up
        Placing = 7,        // another piece is being placed
        Buying = 8,         // the game's buying panel is open
        ToHospital = 9,     // the player is being taken to hospital
        Previewing = 10,    // a building is being previewed
        Designer = 11,      // the interior designer is open
        ActivityPanel = 12, // the game's activity panel is open
        SmallMenu = 13,     // the game's small menu is open
        Using = 14,         // the player's own body is using it
        Vehicle = 15,       // the player is in a vehicle they do not walk about in
        Structure = 16,     // part of the building: its notice says its place cannot be changed
        Residential = 17,   // a piece the game will not let be moved in a flat (its notice says so)
    }

    /// <summary>What the game's right click on a piece asks of it, read once on the click.</summary>
    public struct MoveSignals
    {
        public bool Hidden, Activity, Inside, AtTheDoor, Yours, Editable, InUse, Placing, Buying, ToHospital, Previewing, Designer, ActivityPanel, SmallMenu, Using, Vehicle,
            Structure, Residential;
    }

    /// <summary>Why the game would not put down the piece being placed where the pointer is, in the order it asks (PlacementHelper.Run, then PlacementSystem).</summary>
    public enum PlaceRefusal
    {
        None = 0,           // put down here
        InputSelected = 1,  // a piece of the interface is selected (GameManager.HasInputSelected): the game leaves the piece where it is
        OverInterface = 2,  // the pointer counts as over the interface: the piece neither follows nor goes down
        NoFloor = 3,        // no floor (or wall, for a piece on a wall) of the building under the pointer
        OutsidePlot = 4,    // beyond the plot a house stands on
        Blocked = 5,        // on floor the building marks as blocked: a notice of the game's says so
        NoFit = 6,          // it does not fit there: something stands in the way, or a piece for a wall has no wall behind it
        Moved = 7,          // the pointer moved too far between the press and the release
    }

    /// <summary>What the game's place click asks, read once on the click.</summary>
    public struct PlaceSignals
    {
        public bool InputSelected, OverInterface, Floor, InsidePlot, Blocked, Fits, Moved;
    }

    /// <summary>Whose a thing in the way is, by the script on it or above it.</summary>
    public enum MoveOwner
    {
        Nobody = 0,     // no script on it or above it
        Game = 1,       // the game's (or the engine's), from the game's own folder of assemblies
        ThisMod = 2,    // this mod's
        OtherMod = 3,   // another mod's
    }

    /// <summary>
    /// The game-free part of the furniture move self-check (Diagnostics/ClickWatch): the first reason the game's own right
    /// click or place click would refuse, in its own order, its words for the log, and whose a thing in the way is. A few
    /// comparisons a click, checked offline.
    /// </summary>
    public static class FurnitureRules
    {
        public const float PointerSlack = 0.01f;   // of the screen's height: how far the pointer may move between press and release and still place (PlacementSystem.HasMovedCursorTooMuchSinceStart)

        /// <summary>The first reason the game's right click would not lift the piece; <see cref="MoveRefusal.None"/> when it lifts it.</summary>
        public static MoveRefusal First(in MoveSignals s)
        {
            if (s.Hidden)
            {
                return MoveRefusal.Hidden;
            }
            if (s.Activity)
            {
                return MoveRefusal.Activity;
            }
            if (!s.Inside)
            {
                return MoveRefusal.Outdoors;
            }
            if (s.AtTheDoor)
            {
                return MoveRefusal.AtTheDoor;
            }
            if (!s.Yours && !s.Editable)
            {
                return MoveRefusal.NotYours;
            }
            if (s.InUse && !s.Editable)
            {
                return MoveRefusal.InUse;
            }
            if (s.Placing)
            {
                return MoveRefusal.Placing;
            }
            if (s.Buying)
            {
                return MoveRefusal.Buying;
            }
            if (s.ToHospital)
            {
                return MoveRefusal.ToHospital;
            }
            if (s.Previewing)
            {
                return MoveRefusal.Previewing;
            }
            if (s.Designer)
            {
                return MoveRefusal.Designer;
            }
            if (s.ActivityPanel)
            {
                return MoveRefusal.ActivityPanel;
            }
            if (s.SmallMenu)
            {
                return MoveRefusal.SmallMenu;
            }
            if (s.Using)
            {
                return MoveRefusal.Using;
            }
            if (s.Vehicle)
            {
                return MoveRefusal.Vehicle;
            }
            if (s.Structure)
            {
                return MoveRefusal.Structure;
            }
            return s.Residential ? MoveRefusal.Residential : MoveRefusal.None;
        }

        /// <summary>The first reason the game's place click would not put the piece down where the pointer is; <see cref="PlaceRefusal.None"/> when it would.</summary>
        public static PlaceRefusal First(in PlaceSignals s)
        {
            if (s.InputSelected)
            {
                return PlaceRefusal.InputSelected;
            }
            if (s.OverInterface)
            {
                return PlaceRefusal.OverInterface;
            }
            if (!s.Floor)
            {
                return PlaceRefusal.NoFloor;
            }
            if (!s.InsidePlot)
            {
                return PlaceRefusal.OutsidePlot;
            }
            if (s.Blocked)
            {
                return PlaceRefusal.Blocked;
            }
            if (!s.Fits)
            {
                return PlaceRefusal.NoFit;
            }
            return s.Moved ? PlaceRefusal.Moved : PlaceRefusal.None;
        }

        /// <summary>True when the pointer moved farther between the press and the release than the game lets a place click be.</summary>
        public static bool MovedTooFar(float dx, float dy, float screenHeight)
        {
            float most = screenHeight * PointerSlack;
            return dx * dx + dy * dy > most * most;
        }

        /// <summary>True when the game says why itself, with a notice; the other refusals do nothing at all.</summary>
        public static bool Noticed(MoveRefusal r)
        {
            return r == MoveRefusal.InUse || r == MoveRefusal.Structure || r == MoveRefusal.Residential;
        }

        /// <summary>
        /// Whose a script is: this mod's by its assembly's name or by the mod's own name on the thing or above it, the game's
        /// when its assembly was loaded from the game's own folder of assemblies, another mod's otherwise; nobody's without one.
        /// </summary>
        public static MoveOwner Owner(string assembly, string location, string own, bool namedByThisMod)
        {
            if (namedByThisMod || (!string.IsNullOrEmpty(assembly) && assembly == own))
            {
                return MoveOwner.ThisMod;
            }
            if (string.IsNullOrEmpty(assembly))
            {
                return MoveOwner.Nobody;
            }
            string path = (location ?? string.Empty).Replace('\\', '/');
            return path.IndexOf("_Data/Managed/", System.StringComparison.OrdinalIgnoreCase) >= 0 ? MoveOwner.Game : MoveOwner.OtherMod;
        }

        /// <summary>The words for whose a thing is, for the log.</summary>
        public static string Words(MoveOwner owner, string assembly)
        {
            switch (owner)
            {
                case MoveOwner.Game: return "the game's";
                case MoveOwner.ThisMod: return "this mod's";
                case MoveOwner.OtherMod: return "another mod's (" + assembly + ")";
                default: return "nobody's script";
            }
        }

        /// <summary>The words for why the game would not lift a piece, for the log.</summary>
        public static string Words(MoveRefusal r)
        {
            switch (r)
            {
                case MoveRefusal.None: return "the game lifts it";
                case MoveRefusal.Hidden: return "refused: the game has it hidden (another floor is shown, or the city map is open)";
                case MoveRefusal.Activity: return "refused, with nothing said: an activity of the game's panel is under way";
                case MoveRefusal.Outdoors: return "refused, with nothing said: the player is not in a building";
                case MoveRefusal.AtTheDoor: return "refused, with nothing said: the player is going in or out";
                case MoveRefusal.NotYours: return "refused, with nothing said: the player neither rents nor owns this building";
                case MoveRefusal.InUse: return "refused: the game counts it as in use and says it cannot be picked up";
                case MoveRefusal.Placing: return "refused, with nothing said: another piece is being placed";
                case MoveRefusal.Buying: return "refused, with nothing said: the game's buying panel is open";
                case MoveRefusal.ToHospital: return "refused, with nothing said: the player is being taken to hospital";
                case MoveRefusal.Previewing: return "refused, with nothing said: a building is being previewed";
                case MoveRefusal.Designer: return "refused, with nothing said: the interior designer is open";
                case MoveRefusal.ActivityPanel: return "refused, with nothing said: the game's activity panel is open";
                case MoveRefusal.SmallMenu: return "refused, with nothing said: the game's small menu is open";
                case MoveRefusal.Using: return "refused, with nothing said: the player's own body is using it";
                case MoveRefusal.Vehicle: return "refused, with nothing said: the player is in a vehicle";
                case MoveRefusal.Structure: return "refused: it is part of the building, and the game says its place cannot be changed";
                case MoveRefusal.Residential: return "refused: the game will not let this piece be moved in a flat, and says so";
                default: return "refused for a reason this check does not know";
            }
        }

        /// <summary>The words for why the game would not put the piece down where the pointer is, for the log.</summary>
        public static string Words(PlaceRefusal r)
        {
            switch (r)
            {
                case PlaceRefusal.None: return "the game puts it down here";
                case PlaceRefusal.InputSelected: return "kept in the air: a piece of the interface is selected, so the game leaves the piece where it is";
                case PlaceRefusal.OverInterface: return "kept in the air: the pointer counts as over the interface, so the piece neither follows nor goes down";
                case PlaceRefusal.NoFloor: return "kept in the air: no floor of the building under the pointer (or no wall, for a piece on a wall)";
                case PlaceRefusal.OutsidePlot: return "kept in the air: beyond the plot the house stands on";
                case PlaceRefusal.Blocked: return "kept in the air: the building marks this floor as blocked, and the game says so";
                case PlaceRefusal.NoFit: return "kept in the air: it does not fit here";
                case PlaceRefusal.Moved: return "kept in the air: the pointer moved too far between the press and the release";
                default: return "kept in the air for a reason this check does not know";
            }
        }
    }
}
