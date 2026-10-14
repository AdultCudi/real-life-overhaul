using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.FitOut
{
    /// <summary>What a building fitted out by the mod is: which rooms its bare floor is given (<see cref="FitPlanner"/>).</summary>
    public enum FitKind
    {
        TechOffice,    // a tech company's headquarters: open-plan desks, meeting rooms, a kitchen, a lounge, printers, a server closet
        CivicOffice,   // a city office or the back of a police station: desks, filing, an interview table, a break corner, waiting seats
        DataCentre,    // the floor of a data centre round its server hall: stores, a workbench, a staff desk, a break corner

        // The presets of a working day's desk (Careers: a job whose employer's building has no station of the game's that
        // suits it): one small room of its kind on the floor the building leaves free, round what the game furnished it with.
        OfficeRow,     // an office's: a row of desks, each with a computer and a chair, colleagues at some
        Ward,          // a hospital's nurses' station: two desks with computers and chairs, medical cabinets
        Classroom,     // the school's teacher's desk: a computer, a chair, a cabinet
        ServiceDesk,   // the council's service desks: a computer, a visitor's chair, filing
        BackOffice     // a business's back office desk: a computer, a cabinet, a bin
    }

    /// <summary>What a piece of a fit-out is to the plan.</summary>
    public enum FitRole
    {
        Floor,   // stands on the floor in anybody's way: a desk, a counter, a sofa, a cabinet, a partition
        Seat,    // a chair drawn up to what it serves: it stands on the floor, but closes nobody's way
        OnTop,   // stands on another piece (a computer on a desk) and goes with it
        Hung,    // on a wall
        Lamp,    // on the ceiling
        Flat     // on the floor and walked over: a carpet
    }

    /// <summary>What a seat is for, or that somebody walks up to a piece (a coffee machine, a printer).</summary>
    public enum FitUse
    {
        None,
        Desk,      // a chair at a desk: somebody works there
        Meeting,   // a chair at a meeting table
        Lounge,    // a sofa, an armchair, a chair at a break table
        Waiting,   // a waiting seat
        Stop       // something somebody walks up to and stands at
    }

    /// <summary>The drawn size of one of the game's pieces, in its own axes (x across, z deep, y up), and where its middle is from its pivot.</summary>
    public struct FitSize
    {
        public float W, D, H;     // across, deep, tall
        public float CX, CZ;      // the middle of the drawn bounds from the pivot, along its own x and z
        public float Top;         // how high its top is above its pivot (what stands on it stands there)

        public FitSize(float w, float d, float h, float cx = 0f, float cz = 0f, float top = -1f)
        {
            W = w;
            D = d;
            H = h;
            CX = cx;
            CZ = cz;
            Top = top >= 0f ? top : h;
        }
    }

    /// <summary>
    /// One piece of a fit-out: where its pivot goes on the floor (the game's axes inside the building), how high above the
    /// floor and turned how far, and the floor it takes along the room's axes. Plain numbers and the game's name for it.
    /// </summary>
    public struct FitPiece
    {
        public string Prefab;            // the game's item (its id, as the game names its prefabs), or a model of the mod's ("rlo:...")
        public FitRole Role;
        public FitUse Use;
        public float X, Z, Y, Yaw;       // the pivot, its height above the floor, its turn in degrees (its front is +Z turned so)
        public float CX, CZ;             // the middle of the floor it takes
        public float HalfX, HalfZ;       // half of that floor along the room's x and z
        public float Height;
        public int Anchor;               // the piece it stands on (OnTop) or is drawn up to (Seat); -1 for none
        public int Zone;                 // the room of the plan's own it belongs to; -1 for a piece of the game's design for the unit
        public bool Design;              // from the game's own design for the unit
        public bool Kept;
        public bool Tall;                // taller than a person's eye: never in front of the windows of the street wall

        public bool Contains(float x, float z, float margin) => Math.Abs(x - CX) <= HalfX + margin && Math.Abs(z - CZ) <= HalfZ + margin;

        public float FrontX => (float)Math.Sin(Yaw * Math.PI / 180.0);

        public float FrontZ => (float)Math.Cos(Yaw * Math.PI / 180.0);

        /// <summary>Half the piece's footprint along its own front.</summary>
        public float HalfAlongFront => Math.Abs(FrontX) * HalfX + Math.Abs(FrontZ) * HalfZ;
    }

    /// <summary>A rectangle of floor along the room's axes: something already there (solid: nobody walks through it), or floor kept free.</summary>
    public struct FitRect
    {
        public float X0, Z0, X1, Z1;
        public bool Solid;   // it stands there: the walkway goes round it
        public bool Wall;    // it hangs on the wall there (a screen): nothing of the plan's is hung within a metre of it

        public FitRect(float x0, float z0, float x1, float z1, bool solid, bool wall = false)
        {
            X0 = Math.Min(x0, x1);
            Z0 = Math.Min(z0, z1);
            X1 = Math.Max(x0, x1);
            Z1 = Math.Max(z0, z1);
            Solid = solid;
            Wall = wall;
        }

        public static FitRect Around(float x, float z, float halfX, float halfZ, bool solid, bool wall = false) => new FitRect(x - halfX, z - halfZ, x + halfX, z + halfZ, solid, wall);

        /// <summary>The distance from a point to the rectangle; 0 inside.</summary>
        public float Distance(float x, float z)
        {
            float dx = Math.Max(0f, Math.Max(X0 - x, x - X1)), dz = Math.Max(0f, Math.Max(Z0 - z, z - Z1));
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }

    /// <summary>A place on the floor with a reach: a way out kept clear round it, or a place that must still be walked to.</summary>
    public struct FitSpot
    {
        public float X, Z, R;

        public FitSpot(float x, float z, float r)
        {
            X = x;
            Z = z;
            R = r;
        }
    }

    /// <summary>What the planner is given about the room besides its floor: the door, what is already there, and what the building is.</summary>
    public sealed class FitSite
    {
        public FitKind Kind;
        public int Seed;                                                  // the building's own number: the same building gets the same rooms
        public float DoorX, DoorZ, InX, InZ;                              // where the player comes in, and the way into the room from there
        public float Erosion = 0.3f;                                      // how far the walkable floor stops short of a wall (the walkers' own width)
        public readonly List<FitRect> Fixed = new List<FitRect>(64);      // what already stands (a lobby, a counter, a cell): nothing on it, none of its ways closed
        public readonly List<FitRect> KeepFree = new List<FitRect>(16);   // floor to keep free (the way to a counter, round a server hall)
        public readonly List<FitSpot> Keep = new List<FitSpot>(32);       // places that must still be walked to (the front of a desk, a seat of somebody's)
        public readonly List<FitSpot> Exits = new List<FitSpot>(4);       // the other ways out and the drive-in bays, kept clear round them
        public readonly Dictionary<string, FitSize> Sizes = new Dictionary<string, FitSize>(64, StringComparer.Ordinal);
        public int MostPieces = FitPlanner.MostPieces;
        public bool Zones = true;                                         // bare floor is given rooms of the building's kind
        public bool ZoneLamps = true;                                     // and lamps over them
        public bool Windows = true;                                       // the street wall has windows: nothing tall in front of them

        /// <summary>Everything about the room let go, for the next.</summary>
        public void Reset()
        {
            Fixed.Clear();
            KeepFree.Clear();
            Keep.Clear();
            Exits.Clear();
            Sizes.Clear();
            MostPieces = FitPlanner.MostPieces;
            Zones = ZoneLamps = Windows = true;
        }

        /// <summary>The drawn size of a piece of the game's: measured by the game side, or the plan's own figure for it.</summary>
        public FitSize Size(string prefab)
        {
            if (prefab != null && Sizes.TryGetValue(prefab, out FitSize size) && size.W > 0.05f && size.D > 0.05f)
            {
                return size;
            }
            return FitRoles.Nominal(prefab);
        }
    }

    /// <summary>What the plan came to: the pieces kept (and those left out, Kept false), and what was measured on the way.</summary>
    public sealed class FitPlan
    {
        public readonly List<FitPiece> Pieces = new List<FitPiece>(400);
        public readonly List<FitSpot> Stops = new List<FitSpot>(32);     // where somebody walking about goes and stands
        public readonly int[] ZoneCounts = new int[FitPlanner.ZoneTypeCount];
        public int DesignOffered, DesignClashed, DesignCut, DesignCapped, Zones, ZoneTries;
        public int Unreached, Pockets;
        public float FloorArea, BareBefore, BareAfter;                   // square metres: the floor reached from the door; of it, more than 2 m from anything
        public bool DesignGivenUp;
        public string Why;

        public void Clear()
        {
            Pieces.Clear();
            Stops.Clear();
            Array.Clear(ZoneCounts, 0, ZoneCounts.Length);
            DesignOffered = DesignClashed = DesignCut = DesignCapped = Zones = ZoneTries = Unreached = Pockets = 0;
            FloorArea = BareBefore = BareAfter = 0f;
            DesignGivenUp = false;
            Why = null;
        }

        public int Kept
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Pieces.Count; i++)
                {
                    n += Pieces[i].Kept ? 1 : 0;
                }
                return n;
            }
        }

        public int KeptDesign
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Pieces.Count; i++)
                {
                    n += Pieces[i].Kept && Pieces[i].Design ? 1 : 0;
                }
                return n;
            }
        }

        /// <summary>The seats kept for a use.</summary>
        public int Seats(FitUse use)
        {
            int n = 0;
            for (int i = 0; i < Pieces.Count; i++)
            {
                FitPiece p = Pieces[i];
                n += p.Kept && p.Use == use && (p.Role == FitRole.Seat || p.Role == FitRole.Floor) ? 1 : 0;
            }
            return n;
        }

        public bool HasLamps
        {
            get
            {
                for (int i = 0; i < Pieces.Count; i++)
                {
                    if (Pieces[i].Kept && Pieces[i].Role == FitRole.Lamp)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }

    /// <summary>
    /// What the game's items are to a fit-out, by their ids: pure rules on the names the game gives its prefabs. Which
    /// pieces of a design are left out of every fit-out (a business's trade: tills, signs, delivery spots), which stand,
    /// sit, hang or light, what a chair is drawn up to, what somebody walks up to, and the small things that go first when
    /// a design has more pieces than a visit is given. And the plan's own figure for the size of the pieces it lays out,
    /// until the game side has measured them.
    /// </summary>
    public static class FitRoles
    {
        private static readonly string[] NotFitted =
        {
            "forsale", "jobboard", "signwithtext", "productsign", "menuboard", "deliverysign", "deliveryspot", "cashregister", "checkout",
            "shoppingbasket", "spawner", "showcase", "cardboardcutout", "christmas", "halloween", "jackolantern", "seasonal", "neon",
            "candycane", "irssign", "irscounter", "securityscanner", "hairdressersign", "turnstile", "ticketkiosk", "previewterminal"
        };

        private static readonly string[] Clutter = { "calculator", "mousepad", "officephone", "paperbin", "deskcalendar", "deskglobe", "stackofbooks", "decorativepapers", "paperholder" };

        private static readonly string[] Decor = { "deskplant", "painting", "pictureframe", "framedcertificate", "wallclock", "bathroomsign", "wallplant" };

        private static readonly string[] Stops = { "coffeemachine", "printer", "watercooler", "waterfountain", "fridge", "closetrack", "photocopier" };

        /// <summary>False for the pieces of a design that are a business's trade, never an office's furniture.</summary>
        public static bool Fitted(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }
            for (int i = 0; i < NotFitted.Length; i++)
            {
                if (id.IndexOf(NotFitted[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
            }
            return true;
        }

        public static bool IsLamp(string id) => Has(id, "ceilinglamp") || Has(id, "chandelier");

        public static bool IsFlat(string id) => Has(id, "carpet") || Has(id, "rug") || Has(id, "dancefloor") || Has(id, "floorarrow");

        /// <summary>A chair drawn up to a desk or a table (not an armchair, a cinema seat, a lounger).</summary>
        public static bool IsChair(string id)
        {
            return (Has(id, "chair") || Has(id, "stool")) && !Has(id, "armchair") && !Has(id, "hairdresser") && !Has(id, "cinema") && !Has(id, "theater")
                   && !Has(id, "lounger") && !Has(id, "deckchair") && !Has(id, "slot") && !Has(id, "waitingroom") && !Has(id, "garden");
        }

        /// <summary>A piece somebody sits on that stands in the way (a sofa, an armchair, a row of waiting seats).</summary>
        public static bool IsSofa(string id) => Has(id, "sofa") || Has(id, "armchair") || Has(id, "waitingroomchairs") || Has(id, "bench0");

        /// <summary>What a chair can be drawn up to: a desk (true) or a table or counter (false); null if neither.</summary>
        public static bool? Surface(string id)
        {
            if (Has(id, "desk") && !Has(id, "deskplant") && !Has(id, "deskcalendar") && !Has(id, "deskglobe") && !Has(id, "desklamp"))
            {
                return true;
            }
            if (Has(id, "table") || Has(id, "counter") || Has(id, "bar"))
            {
                return false;
            }
            return null;
        }

        /// <summary>A partition of a design's (a glass wall, a barrier): it makes the rooms, so it is the last to go.</summary>
        public static bool IsPartition(string id) => Has(id, "divider") || Has(id, "barrier") || Has(id, "fence");

        public static bool IsStop(string id)
        {
            for (int i = 0; i < Stops.Length; i++)
            {
                if (Has(id, Stops[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>How readily a piece goes when a design has too many: 2 the desk clutter, 1 the decoration, 0 the furniture.</summary>
        public static int Spare(string id)
        {
            for (int i = 0; i < Clutter.Length; i++)
            {
                if (Has(id, Clutter[i]))
                {
                    return 2;
                }
            }
            for (int i = 0; i < Decor.Length; i++)
            {
                if (Has(id, Decor[i]))
                {
                    return 1;
                }
            }
            return 0;
        }

        /// <summary>The plan's own figure for a piece's size (its own axes), until the game side has measured the game's.</summary>
        public static FitSize Nominal(string prefab)
        {
            switch (prefab)
            {
                case "officedesk1": return new FitSize(1.8f, 0.9f, 0.75f, 0f, 0f, 0.74f);
                case "officechair": case "officechair2": return new FitSize(0.62f, 0.62f, 1.0f);
                case "regularchair": return new FitSize(0.5f, 0.5f, 0.9f);
                case "computer": return new FitSize(0.5f, 0.3f, 0.45f);
                case "computermonitor": return new FitSize(0.55f, 0.2f, 0.45f);
                case "deskplant01": return new FitSize(0.2f, 0.2f, 0.3f);
                case "largemeetingtable": return new FitSize(1.2f, 3.0f, 0.75f, 0f, 0f, 0.75f);
                case "table1": return new FitSize(1.2f, 0.8f, 0.75f, 0f, 0f, 0.75f);
                case "roundtable": return new FitSize(1.0f, 1.0f, 0.75f, 0f, 0f, 0.75f);
                case "wallmountedtv": return new FitSize(1.5f, 0.1f, 0.9f);
                case "counter1": return new FitSize(1.0f, 0.65f, 0.9f, 0f, 0f, 0.88f);
                case "cheapcoffeemachine": return new FitSize(0.35f, 0.35f, 0.4f);
                case "standardfridge": return new FitSize(0.8f, 0.75f, 1.9f);
                case "watercooler": return new FitSize(0.4f, 0.4f, 1.3f);
                case "sofaluxury": return new FitSize(2.2f, 0.95f, 0.85f);
                case "armchair1": return new FitSize(0.9f, 0.9f, 0.85f);
                case "coffeetable1": return new FitSize(1.1f, 0.6f, 0.45f);
                case "printer": return new FitSize(0.7f, 0.65f, 1.15f);
                case "smallcabinet": return new FitSize(0.8f, 0.5f, 0.9f);
                case "bigcabinet": return new FitSize(1.0f, 0.5f, 2.0f);
                case "storageshelf": return new FitSize(1.8f, 0.6f, 2.2f);
                case "metalshelf": return new FitSize(1.2f, 0.5f, 2.0f);
                case "palletshelf": return new FitSize(2.8f, 1.1f, 3.0f);
                case "officelockers": return new FitSize(0.9f, 0.5f, 1.9f);
                case "waitingroomchairs": return new FitSize(2.0f, 0.65f, 0.85f);
                case "recyclingbinpaper": case "trashbin": return new FitSize(0.45f, 0.45f, 0.8f);
                case "decorativepalletwithboxes01": case "decorativepalletwithboxes02": case "decorativepalletwithboxes03": return new FitSize(1.2f, 1.0f, 1.2f);
                case "rlo:closetrack": return new FitSize(0.6f, 0.8f, 1.2f);
                case "rlo:bench": return new FitSize(1.4f, 0.7f, 0.75f, 0f, 0f, 0.75f);
                case "ceilinglampsquare": case "ceilinglampwarehouse": return new FitSize(0.6f, 0.6f, 0.1f);
                default:
                    return prefab != null && prefab.StartsWith("groundplant", StringComparison.Ordinal) ? new FitSize(0.6f, 0.6f, 1.2f) : new FitSize(0.6f, 0.6f, 1.0f);
            }
        }

        private static bool Has(string id, string part) => id != null && id.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Who works in a fitted-out building while the player is in it, by its kind and its seats: some at desks, a pair at a
    /// meeting table, somebody in the lounge, one or two walking about, somebody waiting; never more than the mod's people
    /// to spare (People/ActorPool), the desks first.
    /// </summary>
    public struct FitCrew
    {
        public int AtDesks, InMeeting, InLounge, Waiting, Walking;

        public int Total => AtDesks + InMeeting + InLounge + Waiting + Walking;

        public static FitCrew For(FitKind kind, int deskSeats, int meetingSeats, int loungeSeats, int waitingSeats, int stops, int most)
        {
            var crew = new FitCrew();
            switch (kind)
            {
                case FitKind.TechOffice:
                    crew.AtDesks = Clamp(deskSeats / 5, deskSeats > 0 ? 2 : 0, 6);
                    crew.InMeeting = meetingSeats >= 2 ? 2 : 0;
                    crew.InLounge = loungeSeats > 0 ? 1 : 0;
                    crew.Walking = stops >= 2 ? 2 : 0;
                    break;
                case FitKind.CivicOffice:
                    crew.AtDesks = Clamp(deskSeats / 3, deskSeats > 0 ? 1 : 0, 4);
                    crew.Waiting = waitingSeats > 0 ? 1 : 0;
                    crew.Walking = stops >= 2 ? 1 : 0;
                    break;
                case FitKind.OfficeRow:
                    crew.AtDesks = Clamp(deskSeats, 0, 3);   // (the player's own desk is not among the seats given)
                    break;
                case FitKind.Ward:
                case FitKind.ServiceDesk:
                    crew.AtDesks = deskSeats > 0 ? 1 : 0;    // a colleague at the other desk
                    break;
                case FitKind.Classroom:
                case FitKind.BackOffice:
                    break;                                   // the player's desk alone

                default:
                    crew.AtDesks = deskSeats > 0 ? 1 : 0;
                    break;
            }
            // Within what there is to spare: the walkers, the lounge, the meeting and the waiting go before the desks.
            most = Math.Max(0, most);
            while (crew.Total > most)
            {
                if (crew.InLounge > 0)
                {
                    crew.InLounge--;
                }
                else if (crew.Waiting > 0)
                {
                    crew.Waiting--;
                }
                else if (crew.InMeeting > 0)
                {
                    crew.InMeeting = 0;   // (a meeting is a pair or nobody)
                }
                else if (crew.Walking > 0)
                {
                    crew.Walking--;
                }
                else
                {
                    crew.AtDesks--;
                }
            }
            return crew;
        }

        private static int Clamp(int value, int least, int most) => value < least ? least : value > most ? most : value;
    }
}
