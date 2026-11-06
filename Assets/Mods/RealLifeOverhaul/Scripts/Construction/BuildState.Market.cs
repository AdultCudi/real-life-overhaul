using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// One offer in the land market (docs/features/construction.md, "Land market"): the player's to a rival developer for land it holds,
    /// or a rival's for land of the player's. A waiting one is saved with the rest of the line's "^m:" part; an answered one is kept for
    /// a while, for the limits on offering again. Saved in <see cref="BuildStore"/>'s market part; a line without it has none.
    /// </summary>
    public sealed class LandOffer
    {
        public int Id;
        public LandOfferSide Side;
        public LandOfferState State;
        public string Lot = string.Empty;            // the land's first lot: the rival's site (Mine) or the player's (Theirs)
        public int Spaces = 1;                       // spaces it takes along its strip
        public int Grow;                             // Mine: the grown site's number; Theirs: the grown site beside it that it joins, 0 for none
        public int Rival;                            // the builder whose arm trades, or who does a holding's works
        public string Holding = string.Empty;        // a rival tycoon's holding that trades; empty for the builder's own arm
        public BuildUse Use;                         // Theirs: the use the developer would build
        public LandReason Reason;                    // Theirs: why it wants the land
        public string Design = string.Empty;         // Theirs: the design it would build, "-" for none yet
        public long Price;                           // what is offered
        public long Ask;                             // Mine: what the rival asked when it answered; 0 before
        public int Made;                             // the day it was made
        public int Due;                              // Mine: the day it is answered; Theirs: the last day the player may answer
        public int Raised;                           // Mine: times it has been raised after a refusal
        public int Answered;                         // the day it ended; 0 while waiting

        public bool Waiting => State == LandOfferState.Waiting;
    }

    public sealed partial class BuildState
    {
        public const int OffersMost = LandMarketRules.KeptMost;   // offers kept: those waiting always, then the newest answered

        public readonly List<LandOffer> Offers = new List<LandOffer>(8);
        public int NextMarketDay;                    // the day the rivals' land trading may next move; 0 before the market first looked
        public long CityLandBack;                    // money the city has paid for land sold back to it, the player's and the rivals'
        public readonly int[] MarketMoves = new int[LandMarketRules.MonthMost + 1];   // the days of the rivals' latest moves, the newest last (0 for none)

        /// <summary>The offer with this number, or null.</summary>
        public LandOffer Offer(int id)
        {
            for (int i = 0; i < Offers.Count; i++)
            {
                if (Offers[i].Id == id)
                {
                    return Offers[i];
                }
            }
            return null;
        }

        /// <summary>The player's offer for a rival's site that is still waiting, or null.</summary>
        public LandOffer WaitingOn(string lot)
        {
            for (int i = 0; i < Offers.Count; i++)
            {
                LandOffer o = Offers[i];
                if (o.Waiting && o.Side == LandOfferSide.Mine && string.Equals(o.Lot, lot, StringComparison.Ordinal))
                {
                    return o;
                }
            }
            return null;
        }

        /// <summary>The newest offer of one side for a lot, answered or waiting, or null.</summary>
        public LandOffer LastOn(LandOfferSide side, string lot)
        {
            LandOffer last = null;
            for (int i = 0; i < Offers.Count; i++)
            {
                LandOffer o = Offers[i];
                if (o.Side == side && string.Equals(o.Lot, lot, StringComparison.Ordinal) && (last == null || o.Made >= last.Made))
                {
                    last = o;
                }
            }
            return last;
        }

        /// <summary>The grown site with this number, or null.</summary>
        public GrowFile GrowById(int id)
        {
            for (int i = 0; id > 0 && i < Grows.Count; i++)
            {
                if (Grows[i].Id == id)
                {
                    return Grows[i];
                }
            }
            return null;
        }

        /// <summary>Offers of the rivals still waiting for the player.</summary>
        public int TheirsWaiting()
        {
            int n = 0;
            for (int i = 0; i < Offers.Count; i++)
            {
                n += Offers[i].Waiting && Offers[i].Side == LandOfferSide.Theirs ? 1 : 0;
            }
            return n;
        }

        /// <summary>The rivals' land trading noted for the monthly limit: the day of a move, the oldest forgotten.</summary>
        public void NoteMove(int day)
        {
            for (int i = 1; i < MarketMoves.Length; i++)
            {
                MarketMoves[i - 1] = MarketMoves[i];
            }
            MarketMoves[MarketMoves.Length - 1] = day;
        }

        /// <summary>The answered offers beyond the most kept let go, the oldest first; those waiting are never.</summary>
        public void TidyOffers()
        {
            for (int i = 0; i < Offers.Count && Offers.Count > OffersMost; i++)
            {
                if (!Offers[i].Waiting)
                {
                    Offers.RemoveAt(i);
                    i--;
                }
            }
        }
    }
}
