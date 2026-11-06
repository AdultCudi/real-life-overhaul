using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What competition keeps of one rival product (a startup's, or a listed company's that came into a niche), by the product's id. Game-free.</summary>
    public sealed class RivalryRec
    {
        public int Id;                       // RivalProduct.Id
        public bool Entrant;                 // a listed company's product that came into a niche (the sector's own products are in their markets from the start)
        public int Niche = -1;               // an entrant's niche; a startup's comes from its seed
        public int Backer = -1;              // the listed company that bought it (HiringCatalogue.RivalFirms); -1 none
        public int BackerDay;
        public int PeakShare, PeakDay;       // thousandths of its niche's buyers it held lately, and the day that was seen
        public int ChestStage = -1, ChestDay;   // a round brought forward: the stage it stood at, and the day it was called
        public int ChestUntil;               // its war chest is spent on reach and price until this day
        public long ChestMoney;              // cents at the start's prices that round raised
        public int CopyTarget, CopyAt;       // features it will have by the day it finishes copying the player's lead; 0 none under way
        public string CopyPlace = string.Empty;   // the player's firm whose lead it copies
        public int CopyDone;                 // the day it last finished a copy
        public int PoachNote;                // the day a line about its hiring was last made
        // worked out each day, never saved
        public bool Seen;
    }

    /// <summary>What the player's firm looked like when competition last looked: for a raise and a lead to be new. Game-free.</summary>
    public sealed class RivalrySeen
    {
        public string Place = string.Empty;
        public long Invested;                // cents at the start's prices put in by its investors, all the lines of its cap table
        public int Features;                 // the features of its best live product
        public int Day;
    }

    /// <summary>One reaction of a rival against one of the player's firms, for the firm's page. Game-free.</summary>
    public sealed class RivalryReaction
    {
        public int Day, Kind;                // ReactKind
        public int Rival;                    // RivalProduct.Id
        public string Name = string.Empty;   // the rival, as it was called
        public string Place = string.Empty;  // the player's firm it reacts to; empty for none
        public int Niche = -1;
        public long Amount;                  // a sum, a number of features or a day, by the kind
        public string Other = string.Empty;  // the listed company in it, if any
    }

    /// <summary>A firm of the player's that takes part in a bake off, with how it goes about it (0 demo, 1 price, 2 relationships). Game-free.</summary>
    public sealed class BakeEntry
    {
        public string Place = string.Empty;
        public int Tactic;
    }

    /// <summary>A big customer's bake off in a niche: one of the city's own businesses of the kind asks the products to compete. Game-free.</summary>
    public sealed class BakeOff
    {
        public int Id, Niche = -1;
        public string Customer = string.Empty;    // the business, as the game names it
        public int Opened, Closes;                // days
        public int Seats;                         // sites it signs for
        public long Value;                        // cents at the start's prices
        public int State;                         // BakeState
        public string Winner = string.Empty;
        public string Place = string.Empty;       // the player's firm that won
        public long Booked;                       // cents at the start's prices booked at the winner's address
        public readonly List<BakeEntry> Entries = new List<BakeEntry>(2);

        public BakeEntry EntryOf(string place)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Place == place)
                {
                    return Entries[i];
                }
            }
            return null;
        }
    }

    /// <summary>Everything competition in niches keeps in a save. Game-free.</summary>
    public sealed class RivalryFile
    {
        public int Since = -1;                    // the day competition first ran on this save: the shared pools fade in from it
        public int NextBake = 1;
        public readonly int[] BakeLast = new int[NicheCatalogue.Count];   // the day each niche last had a bake off
        public readonly List<RivalryRec> Recs = new List<RivalryRec>(16);
        public readonly List<RivalrySeen> Seen = new List<RivalrySeen>(4);
        public readonly List<RivalryReaction> Log = new List<RivalryReaction>(RivalryCatalogue.LogMost);
        public readonly List<BakeOff> Bakes = new List<BakeOff>(8);

        public RivalryRec RecOf(int id)
        {
            for (int i = 0; i < Recs.Count; i++)
            {
                if (Recs[i].Id == id)
                {
                    return Recs[i];
                }
            }
            return null;
        }

        public RivalryRec RecFor(int id)
        {
            RivalryRec r = RecOf(id);
            if (r == null)
            {
                r = new RivalryRec { Id = id };
                Recs.Add(r);
            }
            return r;
        }

        public RivalrySeen SeenOf(string place)
        {
            for (int i = 0; i < Seen.Count; i++)
            {
                if (Seen[i].Place == place)
                {
                    return Seen[i];
                }
            }
            return null;
        }

        public BakeOff BakeOf(int id)
        {
            for (int i = 0; i < Bakes.Count; i++)
            {
                if (Bakes[i].Id == id)
                {
                    return Bakes[i];
                }
            }
            return null;
        }

        /// <summary>The bake off open in a niche, or null.</summary>
        public BakeOff OpenIn(int niche)
        {
            for (int i = 0; i < Bakes.Count; i++)
            {
                if (Bakes[i].State == BakeState.Open && Bakes[i].Niche == niche)
                {
                    return Bakes[i];
                }
            }
            return null;
        }

        /// <summary>A reaction written first in the log; the oldest go when it is full.</summary>
        public void Say(RivalryReaction r)
        {
            Log.Insert(0, r);
            while (Log.Count > RivalryCatalogue.LogMost)
            {
                Log.RemoveAt(Log.Count - 1);
            }
        }
    }
}
