using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>What a hedge of some purchases would be now, as a page shows it before it is bought.</summary>
    public struct HedgeSums
    {
        public int Legs;          // futures
        public long Money;        // of the purchases they cover, at today's prices
        public long Margin;       // taken into the margin account
        public long Fees;         // commission
        public long TooNear;      // money of purchases before the first contract day, or under a unit
        public long Unpriced;     // money of materials the exchange does not price
        public long Cut;          // units the exchange's limit on one material left out
        public DerivWhy Why;      // why it cannot be bought now; None when it can
    }

    /// <summary>
    /// Part of the market director: a buyer's hedge for the rest of the mod (the construction game's projects, through
    /// Wiring). It plans the futures (HedgeRules), says what they would take, buys them through the engine as any future is
    /// bought (in a session, caught up, every limit, spread and commission; settled in cash) and answers what a contract
    /// has made. Asked when a page is written or a hedge is put on; nothing per frame.
    /// </summary>
    public sealed partial class MarketDirector
    {
        private readonly int[] _hedgeDays = new int[DerivCatalogue.WeeksAhead];
        private Func<string, long> _priceNow, _roomNow;

        /// <summary>
        /// The futures a hedge of these purchases would be now, into <paramref name="legs"/>, held to the exchange's limits;
        /// false while the exchange cannot plan one (the market or futures off, no contract day on offer).
        /// </summary>
        public bool HedgePlan(IList<HedgeNeed> needs, List<HedgeLeg> legs, out HedgeSums sums)
        {
            sums = new HedgeSums { Why = DerivWhy.None };
            legs.Clear();
            if (!Running || !Settings.MarketsDerivatives)
            {
                sums.Why = !Running ? DerivWhy.Closed : DerivWhy.Off;
                return false;
            }
            if (_priceNow == null)
            {
                _priceNow = m => _engine != null ? _engine.MaterialNow(m) : 0;
                _roomNow = m => _engine != null ? _engine.MaterialRoom(m) : 0;
            }
            int days = _engine.ContractDays(_hedgeDays);
            if (days == 0)
            {
                sums.Why = DerivWhy.Closed;
                return false;
            }
            HedgeRules.Plan(needs, _hedgeDays, days, _priceNow, legs, out sums.TooNear, out sums.Unpriced);
            sums.Cut = HedgeRules.Fit(legs, _roomNow);
            int room = DerivCatalogue.ContractsMost - DerivRules.OpenCount(_engine.State.Derivs);
            while (legs.Count > Math.Max(0, room))
            {
                sums.Cut += legs[legs.Count - 1].Units;
                legs.RemoveAt(legs.Count - 1);
            }
            sums.Legs = legs.Count;
            for (int i = 0; i < legs.Count; i++)
            {
                sums.Money += legs[i].Money;
            }
            sums.Margin = _engine.HedgeCost(legs, out sums.Fees);
            sums.Why = legs.Count == 0 ? DerivWhy.NotOffered : DerivReady();
            if (sums.Why == DerivWhy.None && sums.Margin + sums.Fees > _engine.Wallet.Cash)
            {
                sums.Why = DerivWhy.NoCash;
            }
            return true;
        }

        /// <summary>
        /// A hedge of these purchases bought now: its futures' ids into <paramref name="opened"/>. A test's is bought by the
        /// test tools (outside a session too, put back by <see cref="HedgeUndoTests"/>). How many futures were bought.
        /// </summary>
        public int Hedge(IList<HedgeNeed> needs, bool test, List<int> opened, out DerivWhy why)
        {
            FinishMoment();
            var legs = new List<HedgeLeg>(8);
            if (!HedgePlan(needs, legs, out HedgeSums sums))
            {
                why = sums.Why;
                return 0;
            }
            if (!test && sums.Why != DerivWhy.None)
            {
                why = sums.Why;
                return 0;
            }
            if (legs.Count == 0)
            {
                why = DerivWhy.NotOffered;
                return 0;
            }
            try
            {
                int bought = _engine.Hedge(legs, Trading, test, opened, out why);
                Logger?.Info("Stock market: a hedge of " + bought + " of " + legs.Count + " future(s) bought" + (test ? " by the test tools" : string.Empty) + " for "
                             + sums.Money + " of purchases to come, margin " + sums.Margin + ", commission " + sums.Fees + (why != DerivWhy.None ? "; stopped: " + why : string.Empty) + ".");
                Moved();
                return bought;
            }
            catch (Exception e)
            {
                Fault("buying a hedge", e);
                why = DerivWhy.Closed;
                return 0;
            }
        }

        /// <summary>A contract's state: open or ended, and what it has made in cents (its commission in it); false once the book no longer has it.</summary>
        public bool ContractState(int id, out bool open, out long resultCents)
        {
            open = false;
            resultCents = 0;
            DerivContract c = Running ? _engine.State.Derivs.Contract(id) : null;
            if (c == null)
            {
                return false;
            }
            open = c.IsOpen;
            resultCents = c.Result;
            return true;
        }

        /// <summary>The test tools: the given contracts settle now at today's prices. How many.</summary>
        public int HedgeSettleTest(IList<int> ids)
        {
            FinishMoment();
            if (!Running)
            {
                return 0;
            }
            int n = _engine.SettleNow(ids);
            Moved();
            return n;
        }

        /// <summary>The test tools: every contract a test bought undone and its money given back. How many.</summary>
        public int HedgeUndoTests(out long money)
        {
            FinishMoment();
            money = 0;
            if (!Running)
            {
                return 0;
            }
            int n = _engine.UndoTests(out money);
            Moved();
            return n;
        }

        /// <summary>Whether a test may stage a hedge now: the market and futures running, out of a Going Public session.</summary>
        public string HedgeTestRefusal()
        {
            if (!Settings.Markets || !Settings.MarketsDerivatives)
            {
                return "reallife_mkt_hedge_test_off";
            }
            if (!Running || Save == null)
            {
                return "reallife_mkt_hedge_test_off";
            }
            return GoingPublic.InSession ? "reallife_mkt_drv_test_session" : null;
        }
    }
}
