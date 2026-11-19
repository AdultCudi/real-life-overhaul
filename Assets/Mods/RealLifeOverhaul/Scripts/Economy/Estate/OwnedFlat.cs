using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>A residential building as the flats' listing rule sees it: read from the city once a rebuild, never saved. Game-free.</summary>
    public struct FlatBuilding
    {
        public string Place, Name, District;
        public int Value;           // the building's value now by the property market (EstateGame.Value)
        public int Cond;            // 0 to 1000
        public int Income;          // the district's, 600 to 1400
        public int Units;           // the units the game counts in it (EstateRules.UnitCount)
        public int Sqm;             // its floor, square metres
        public int TaxYear;         // the game's property tax on it for a year (RealEstate.TaxesAmount)
        public string Landlord;     // the rival that owns it as the game names them; empty for the city's own landlord
    }

    /// <summary>One unit of a building for sale: what it is, its share of the building by floor area, and its price as the listing rule makes it. Never saved: drawn again from the building.</summary>
    public struct FlatListing
    {
        public string Place, Name, District, Landlord;
        public int Slot, Kind, Units, Sqm, Cond, Income;
        public int Freehold;        // the unit's share of the building's value by floor area: the shares of a building's units add up to it
        public int Price;           // what a long lease of it costs: the share at the lease's share of the freehold
        public int Share;           // the unit's share of the building's floor, millionths
        public int TaxYear;         // the building's property tax for a year, whole building
        public string Key => FlatRules.Key(Place, Slot);
    }

    /// <summary>
    /// A flat or studio the player owns: one unit of a building the player does not own, held as a long lease. It is a home in the game's own
    /// way (the building's registration rented by the player at no rent). The unit's lettings and sale run on <see cref="Hold"/>, a holding of
    /// one unit the rules of <see cref="EstateRules"/> already know, kept apart from the player's buildings.
    /// </summary>
    public sealed class OwnedFlat
    {
        public string Place;            // the building (EstateGame.Place)
        public int Slot;                // the unit in it, from 0
        public int Kind;                // FlatKind
        public int Units;               // the building's units when it was bought: the shares are drawn from this count
        public int Paid, BoughtDay;
        public int Value;               // the last value seen: it stands still while the property part is off
        public int BilledMonth;         // the calendar's month the service charge and the tax were last taken for
        public readonly EstateHolding Hold = new EstateHolding();

        // read from the game, not saved
        public string Name = string.Empty, District = string.Empty, Landlord = string.Empty;
        public int TaxYear, Sqm, Share;
        public bool Seen;               // the building is among the city's

        public string Key => FlatRules.Key(Place, Slot);
        public EstateUnit Unit => Hold.Units[0];
        public bool Let => Hold.Managed && Hold.Units[0].Let;
    }

    /// <summary>The player's flats beside the property business's state, in a line of their own in the save (<see cref="FlatStore"/>).</summary>
    public sealed class FlatBook
    {
        public readonly List<OwnedFlat> Flats = new List<OwnedFlat>(FlatCatalogue.HeldMost);
        public string Unread;           // a saved line this version could not read, kept as it was read

        public OwnedFlat Find(string place, int slot)
        {
            for (int i = 0; place != null && i < Flats.Count; i++)
            {
                if (Flats[i].Slot == slot && Flats[i].Place == place)
                {
                    return Flats[i];
                }
            }
            return null;
        }

        public OwnedFlat Find(string key)
        {
            for (int i = 0; key != null && i < Flats.Count; i++)
            {
                if (Flats[i].Key == key)
                {
                    return Flats[i];
                }
            }
            return null;
        }

        /// <summary>How many flats of a building the player holds.</summary>
        public int In(string place)
        {
            int count = 0;
            for (int i = 0; i < Flats.Count; i++)
            {
                count += Flats[i].Place == place ? 1 : 0;
            }
            return count;
        }
    }
}
