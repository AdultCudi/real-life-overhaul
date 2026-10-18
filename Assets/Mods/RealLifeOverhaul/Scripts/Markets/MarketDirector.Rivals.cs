using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>The transaction key of a takeover of a rival's holding; "shares_" files it under investments.</summary>
    public static class RivalMoney
    {
        public const string Takeover = "reallife_transaction_shares_rival_takeover";
    }

    /// <summary>
    /// Part of the market director: the market as the rival tycoons use it (Wiring/RivalLinks), only on a market of the
    /// player's own, never while it follows another player's; never per frame.
    /// </summary>
    public sealed partial class MarketDirector
    {
        // A market of this machine's own, settled for a change: running, not following a host.
        private bool OwnMarket()
        {
            if (!Running || _following || Save == null)
            {
                return false;
            }
            FinishMoment();
            return true;
        }

        /// <summary>The rivals act on the market only while it runs here on its own.</summary>
        public bool RivalsMayAct => Running && !_following;

        /// <summary>A rival's holding comes to the market now, sized by its tycoon's fortune; true if it is listed, or already was.</summary>
        public bool ListHolding(int id, long worth)
        {
            if (!OwnMarket())
            {
                return false;
            }
            bool listed = _engine.ListHolding(id, worth);
            if (listed)
            {
                Moved();
            }
            return listed;
        }

        public bool DelistHolding(int id)
        {
            if (!OwnMarket())
            {
                return false;
            }
            bool gone = _engine.DelistHolding(id);
            if (gone)
            {
                Moved();
            }
            return gone;
        }

        /// <summary>-1 while it is not listed.</summary>
        public int CompanyDays()
        {
            Stock own = MyCompany;
            return own != null ? Math.Max(0, _engine.State.Day - own.ListedDay) : -1;
        }

        public int YearDays => Year;

        // A company the market's own guard keeps safe: newer than a quarter of a year.
        private bool Young(Stock own)
        {
            return _engine.State.Day - own.ListedDay < Year / 4;
        }

        /// <summary>Its shares, the player's, the most one holder may have, its price in cents.</summary>
        public bool Holding(int id, out long shares, out long held, out long most, out int midCents)
        {
            shares = held = most = 0;
            midCents = 0;
            Stock s = Running ? _engine.State.Stock(id) : null;
            if (s == null || !s.Listed)
            {
                return false;
            }
            shares = s.Shares;
            held = _engine.State.Held(id);
            most = MarketTrading.MostHeld(s);
            midCents = s.MidCents;
            return true;
        }

        /// <summary>A rival's holding buys in to press the board for the demand its figures give, else a payout.</summary>
        public bool RivalStake(int holdingId)
        {
            Stock own = OwnMarket() ? MyCompany : null;
            Stock holding = own != null ? _engine.State.Stock(holdingId) : null;
            if (own == null || holding == null || !holding.Listed || own.BidCents > 0 || own.Own.ActBp > 0 || Young(own))
            {
                return false;
            }
            ActivistDemand demand = ListingVotes.Reason(own, _engine.State.IndexLast, _engine.State.Day, Year, out double _);
            bool pressed = _engine.PressBoard(own.Id, demand == ActivistDemand.None ? ActivistDemand.Payout : demand);
            if (pressed)
            {
                Logger?.Info("Markets: " + holding.Name + " holds " + own.Own.ActBp + " bp of " + own.Name + " and wants " + own.Own.ActDemand + " at a meeting on day " + own.Own.ActDay
                             + " (a rival tycoon's stake).");
                Moved();
            }
            return pressed;
        }

        public bool RivalBid(int holdingId)
        {
            Stock own = OwnMarket() ? MyCompany : null;
            Stock holding = own != null ? _engine.State.Stock(holdingId) : null;
            if (own == null || holding == null || !holding.Listed || Young(own) || !_engine.BidBy(own.Id, holdingId))
            {
                return false;
            }
            Logger?.Info("Markets: " + holding.Name + " bids " + own.BidCents + " cents a share for " + own.Name + ", to complete on day " + own.BidEndDay + "; the founder holds "
                         + own.Own.StakeBp(own) + " bp, the threshold is " + ListingCatalogue.TakeoverThresholdBp + " bp (a rival tycoon's bid).");
            Moved();
            return true;
        }

        /// <summary>The player takes a rival's holding over for <paramref name="cost"/>; false, nothing changed, without the cash or the holding.</summary>
        public bool TakeOverHolding(int id, long cost)
        {
            if (!OwnMarket())
            {
                return false;
            }
            Stock s = _engine.State.Stock(id);
            if (s == null || !s.Listed || s.Def == null || !s.Def.Holding || !_engine.Wallet.Take(cost, RivalMoney.Takeover, s.Name))
            {
                return false;
            }
            _engine.Delist(id, GoneReason.TakenOver, s.MidCents);
            Logger?.Info("Markets: the player took " + s.Name + " over for " + cost + "; it left the market at " + s.GoneCents + " cents a share.");
            Moved();
            return true;
        }
    }
}
