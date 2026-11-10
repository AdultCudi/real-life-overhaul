using System.Collections.Generic;

namespace RealLifeOverhaul.ParkSpaces
{
    /// <summary>Who owns the building a car park is under: the seller of its spaces.</summary>
    public enum SpaceOwner { City = 0, Player = 1, Rival = 2 }

    /// <summary>How a bought space ends: sold back by the player, or settled because the building became theirs, or because its car park is gone.</summary>
    public enum SpaceEnd { SoldBack = 0, OwnBuilding, CarParkGone }

    /// <summary>Why something asked of the parking spaces could not be done; the text key says so. Nothing is paid before one of these is ruled out.</summary>
    public enum SpaceRefusal { None = 0, Off, Session, NoPark, Full, Limit, ParkLimit, Cash, NotHeld, OwnBuilding, Rented, Owned, Moved }

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
        public object Source;           // the game's building, kept so that who owns it can be read again without a walk of the city
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
        public long PriceSeen;          // a bought one: the price of a space in its car park when last looked at, what a car park that goes is settled by
        public bool OwnSeen;            // the building was the player's when last looked at

        public bool Rented => Paid <= 0;
        public bool Players => Who == PlayerIs;
    }

    /// <summary>A move of a rival tycoon's at a day's look: a space bought or sold back, and the money that goes with it.</summary>
    public struct SpaceMove
    {
        public int Tycoon;
        public int Park, Slot;
        public long Amount;             // what the tycoon pays (a purchase) or gets back (a sale)
        public long Worth;              // what it does to the tycoon's worth: a space bought is worth what was paid, so nothing; a sale loses what the space lost on the way
        public bool Sold;
    }

    /// <summary>
    /// Car-hours counted for a vehicle, to be paid back once the game has charged them: the day they were counted (the game takes a
    /// day's charges on the morning after it), the car park, the vehicle and how many hours.
    /// </summary>
    public sealed class SpaceHours
    {
        public int Day;
        public int Park;
        public string Vehicle;
        public int Hours;
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
        public readonly List<SpaceHours> Pending = new List<SpaceHours>(8);   // car-hours counted, not yet paid back

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

        /// <summary>The car-hours counted and not yet paid back: in one car park, or in all with 0.</summary>
        public int Hours(int park)
        {
            int n = 0;
            for (int i = 0; i < Pending.Count; i++)
            {
                n += park == 0 || Pending[i].Park == park ? Pending[i].Hours : 0;
            }
            return n;
        }
    }
}
