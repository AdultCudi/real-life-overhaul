using System.Collections.Generic;

namespace RealLifeOverhaul.ParkSpaces
{
    /// <summary>Who owns the building a car park is under: the seller of its spaces.</summary>
    public enum SpaceOwner { City = 0, Player = 1, Rival = 2 }

    /// <summary>Why something asked of the parking spaces could not be done; the text key says so. Nothing is paid before one of these is ruled out.</summary>
    public enum SpaceRefusal { None = 0, Off, Session, NoPark, Full, Limit, ParkLimit, Cash, NotHeld, OwnBuilding, Rented, Owned }

    /// <summary>A building's car park as the rules see it: read from the game at a load and each day, game-free.</summary>
    public sealed class SpacePark
    {
        public int Number;              // the game's number of the car park (its vehicles' street number)
        public string Name;             // the building's address, as the game writes it
        public string District;         // as the game names it, for the page
        public int Capacity;            // its spaces
        public int CityShare;           // thousandths of its spaces the city's people hold
        public int RentMonth;           // a term's rent of a space
        public int Price;               // a space's price today
        public SpaceOwner Owner;
        public string OwnerName;        // the rival that owns the building, as the game names it; empty otherwise
    }

    /// <summary>A space held: by the player, or by a rival tycoon. Owned for good, or rented by the term.</summary>
    public sealed class SpaceHolding
    {
        public const int PlayerIs = -1;

        public int Park;                // the car park's number
        public int Slot;                // the space's number in it, from 1
        public int Who = PlayerIs;      // the player, or a tycoon's index
        public long Paid;               // what it was bought for; 0 for a rented one
        public long Rent;               // a term's rent as agreed; 0 for a bought one
        public int Day;                 // the day it was taken
        public int PaidTo;              // the last day the rent is paid for; 0 for a bought one
        public bool Ending;             // a rented one given up: it ends with the term
        public int Missing;             // the day its car park was first not found; 0 while it stands

        public bool Rented => Paid <= 0;
        public bool Players => Who == PlayerIs;
    }

    /// <summary>A move of a rival tycoon's at a day's look: a space bought or sold back, and the money that goes with it.</summary>
    public struct SpaceMove
    {
        public int Tycoon;
        public int Park, Slot;
        public long Amount;             // what the tycoon pays (a purchase) or gets back (a sale)
        public bool Sold;
    }

    /// <summary>What the save keeps of the parking spaces, game-free. A save from before has none of it.</summary>
    public sealed class SpaceState
    {
        public int Seed;                // drawn once a save: who of the city's people holds which space, and the tycoons' looks
        public int LastDay = -1;        // the last day passed
        public int LastHour = -1;       // the last hour counted (day x 24 + hour)
        public long RefundedTotal;      // the game's charges paid back in all
        public long PaidTotal;          // what the player has paid for spaces and rent in all, less what came back for them
        public readonly List<SpaceHolding> Held = new List<SpaceHolding>(SpaceCatalogue.HeldMost + 8);
        public readonly List<int> HourPark = new List<int>(4);     // car parks with car-hours of today not yet paid back...
        public readonly List<int> HourCount = new List<int>(4);    // ...and beside each, how many

        public SpaceHolding At(int park, int slot)
        {
            for (int i = 0; i < Held.Count; i++)
            {
                if (Held[i].Park == park && Held[i].Slot == slot)
                {
                    return Held[i];
                }
            }
            return null;
        }

        /// <summary>The spaces somebody holds: in one car park (or all with 0), by the player or a tycoon (or anybody but the player with -2).</summary>
        public int Count(int park, int who)
        {
            int n = 0;
            for (int i = 0; i < Held.Count; i++)
            {
                SpaceHolding h = Held[i];
                n += (park == 0 || h.Park == park) && (who == -2 ? !h.Players : h.Who == who) ? 1 : 0;
            }
            return n;
        }

        public int Hours(int park)
        {
            int at = HourPark.IndexOf(park);
            return at >= 0 ? HourCount[at] : 0;
        }
    }
}
