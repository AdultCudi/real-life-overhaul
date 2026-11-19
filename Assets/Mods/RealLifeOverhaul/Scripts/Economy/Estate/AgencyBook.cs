using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Where a block of another owner's flats stands with the agency. Saved as a number: never renumber, add at the end.</summary>
    public enum BlockStage
    {
        Offered = 0,    // the owner has asked agencies to pitch; it is decided on its last day
        Held = 1,       // the agency manages it and is paid a share of the rent each month
        Lost = 2        // given to another agency, or taken away; kept a while so the page can say so
    }

    /// <summary>Why a block was lost. Saved as a number: never renumber, add at the end.</summary>
    public enum BlockWhy
    {
        None = 0,
        Rival = 1,      // another agency was chosen for it
        Nobody = 2,     // the player did not pitch and no rival wanted it either
        Standard = 3,   // the tenants were kept unhappy month after month
        Inspection = 4, // the city failed the building on inspection too often
        Resigned = 5    // the player handed it back
    }

    /// <summary>What a rival agency offered for a block: the agency (a rival of the save, by its id), its fee as a share of the asking fee, and the skill of its agents.</summary>
    public sealed class AgencyBid
    {
        public string Rival = string.Empty;     // the game's rival id of the landlord who runs the agency
        public int Pct;                         // of the asking fee: 85 to 105
        public int Skill;                       // 0 to 100
    }

    /// <summary>
    /// Another owner's block of flats, asked for or managed. The game keeps no tenants for a building that is not the player's, so a block is a few figures of the
    /// mod's that the agency's work moves by the same measures a building of the player's is judged by (<see cref="EstateRules.Drift"/>,
    /// <see cref="EstateRules.RenewChance"/>, <see cref="EstateRules.CallChance"/>, the city's inspection): nothing here reads or writes the owner's real buildings.
    /// </summary>
    public sealed class ManagedBlock
    {
        public int Id;
        public string Owner = string.Empty;     // a rival's id of the game, or "city:" and a number for a landlord of the city
        public string District = string.Empty;  // the game's neighbourhood
        public string Place = string.Empty;     // a real residential building of the owner's the block is tied to, as the property business writes a place; empty for a landlord of the city
        public int Stage;                       // BlockStage
        public int Why;                         // BlockWhy, once lost
        public string Rival = string.Empty;     // the rival of the save whose agency was chosen instead, once lost to one
        public int Units;                       // flats
        public int Sqm;                         // a flat's floor area
        public int RentDay;                     // dollars a flat a day at the agreed rent
        public int FeeAsk;                      // thousandths of the rent collected: the owner's asking fee
        public int FeePct = 100;                // of the asking fee: what the player pitched at (100 or the undercut), what a rival's pitch is compared with
        public int Standard;                    // the satisfaction the owner expects, 0 to 1000
        public int Until;                       // an offer: the day it is decided; a held block: the day it was taken on
        public bool Pitched;                    // the player has put in a pitch for an offer
        public readonly List<AgencyBid> Bids = new List<AgencyBid>(3);   // rival agencies' pitches for an offer

        // a held block's running figures
        public int Let;                         // flats with a tenant
        public int Happy = -1;                  // the tenants' satisfaction, 0 to 1000; -1 until first judged
        public int Cond;                        // the building's condition, 0 to 1000
        public int Strikes;                     // failed inspections in a row
        public int NextInspect;                 // the city's next inspection day
        public int Missed;                      // months in a row the owner's standard was not met
        public int Collected;                   // rent collected for the owner in the running month, dollars
        public int HappySum, HappyDays;         // the running month's satisfaction, summed over its days
        public int Fees, Earned, Months;        // fees paid so far, and months managed
        public int Penalised;                   // fee withheld for missed standards, in all
        public int LettingsDone;                // flats re-let by the agency
        public int Served = 1000;               // the agency's service today, thousandths of the hours the block needs
        public readonly List<int> Calls = new List<int>(AgencyBookCatalogue.CallsMost);   // the days its open repair calls were reported

        public bool Held => Stage == (int)BlockStage.Held;
        public int Empty => Units - Let;
    }

    /// <summary>The agency's book of other owners' blocks: offers, the blocks it holds and the month's figures. Saved in a line of its own.</summary>
    public sealed class AgencyBook
    {
        public int NextId = 1;
        public int LastOfferDay = -1000;
        public int MonthLettings;                              // letting fees paid in the running market month, dollars
        public int LastMonthFees, LastMonthPenalty, LastMonthLettings;   // paid, withheld and paid as letting fees in the month before it, dollars: shown on the page
        public int Won, Lost;                                  // pitches won and given to another agency, in all
        public int Month;                                      // the market month the monthly figures belong to
        public readonly List<ManagedBlock> Blocks = new List<ManagedBlock>(AgencyBookCatalogue.BlocksMost + AgencyBookCatalogue.OffersMost + AgencyBookCatalogue.LostKept);

        // read from the game, not saved
        public int Served = 1000;                              // thousandths of them the agency's agents cover today
        public int OwnUnits;                                   // units of the player's own buildings under the agency
        public bool OwnPaused;                                 // the agents cannot cover them today

        public ManagedBlock Find(int id)
        {
            for (int i = 0; i < Blocks.Count; i++)
            {
                if (Blocks[i].Id == id)
                {
                    return Blocks[i];
                }
            }
            return null;
        }

        public int Count(BlockStage stage)
        {
            int n = 0;
            for (int i = 0; i < Blocks.Count; i++)
            {
                n += Blocks[i].Stage == (int)stage ? 1 : 0;
            }
            return n;
        }

        public ManagedBlock OfBlock(string owner, string district)
        {
            for (int i = 0; i < Blocks.Count; i++)
            {
                if (Blocks[i].Stage != (int)BlockStage.Lost && Blocks[i].Owner == owner && Blocks[i].District == district)
                {
                    return Blocks[i];
                }
            }
            return null;
        }
    }

    /// <summary>A rival of the save who is a landlord, as the day's read of the city's rents has them: for the offers' owners and the rival agencies.</summary>
    public struct AgencyLandlord
    {
        public string Id;       // the game's rival id
        public int Units;       // units they hold in the city
        public System.Collections.Generic.IList<RentHome> Homes;   // some of their residential buildings, for a block to be tied to; null with none
    }

    /// <summary>What the agency's day did, for the director to pay, tell and record.</summary>
    public struct BlockEvent
    {
        public const int Won = 1, LostToRival = 2, Unwanted = 3, Fee = 4, Fined = 5, Dropped = 6, Letting = 7, Penalty = 8;

        public int Kind;
        public int Id;
        public int Amount;      // the fee, the fine, the letting fee or the withheld share
        public string Name;     // a rival agency's rival, for a pitch that lost
    }
}
