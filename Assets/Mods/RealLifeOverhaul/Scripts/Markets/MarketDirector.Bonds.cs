using System;
using System.Collections.Generic;
using System.Text;
using UI.Notification;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The player's bonds in the game (Markets/Bonds, MarketEngine.Bonds.cs): bought (one at a time, or a ladder over the
    /// Treasury's terms) and sold from the Bonds tab while the market trades, the most of one purchase by what the player
    /// is worth (BondRules.Most, read on a click), each set to roll over at its end or not, their payments, ends and
    /// rollovers told as the game's notifications once the frame's work is done, the banks named for their deposits
    /// (MarketBridges.Banks), and the save tools' and tests' parts. With "Bonds and deposits" off none is bought and none
    /// rolls over; those held still pay, end and can be sold. A save's market from before a company of the
    /// catalogue (the property trusts) takes it in at its load (MarketSetup.Join). Nothing here runs per frame.
    ///
    /// IN A GOING PUBLIC SESSION a player's bonds are theirs, as their holdings are: they live in their own save's line, a
    /// guest's come with them into the host's market (MarketShare.Adopt), nothing of them is sent, and they pay on the
    /// market's own calendar at the city's rates of the day, the same on every machine.
    /// </summary>
    public sealed partial class MarketDirector
    {
        private readonly List<string> _banks = new List<string>(BondCatalogue.BanksMost);
        private readonly List<BondOffer> _offers = new List<BondOffer>(16);
        private Action<BondNote> _bondTold;
        private Func<BondHolding, string> _bondName;

        // Every market engine made (a load, or a Going Public host's market taken on) tells its bonds' news here.
        private void HookBonds()
        {
            if (_bondTold == null)
            {
                _bondTold = BondHeard;
                _bondName = BondText.Name;
            }
            _engine.BondTold = _bondTold;
            _engine.BondName = _bondName;
        }

        // A save's market from before a company of the catalogue: it joins it now (MarketSetup.Join).
        private void JoinNew(MarketState state, bool fresh)
        {
            if (fresh)
            {
                return;
            }
            int joined = MarketSetup.Join(state);
            if (joined > 0)
            {
                Logger?.Info("Stock market: " + joined + " company(ies) of the catalogue this save's market did not have joined it at today's prices (the property trusts among them).");
            }
        }

        private BondWhy BondReady(bool trading)
        {
            return !Running ? BondWhy.Closed : Behind ? BondWhy.Behind : _engine.Held ? BondWhy.Held : trading && !Trading ? BondWhy.Closed : BondWhy.None;
        }

        /// <summary>The bonds on offer today (a deposit at each of the city's three largest banks); empty while the market is not running.</summary>
        public List<BondOffer> BondOffers()
        {
            _offers.Clear();
            if (!Running)
            {
                return _offers;
            }
            _banks.Clear();
            try
            {
                MarketBridges.Banks?.Invoke(_banks);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _banks.Clear();
            }
            _engine.BondOffers(_banks, _offers);
            return _offers;
        }

        /// <summary>The least and the most one purchase may be now (the most by what the player is worth, while larger purchases are on); 0 while the market is not running.</summary>
        public long BondLeast => Running ? BondRules.Least(_engine.State.Env.PriceLevel) : 0;

        public long BondMost => Running ? BondRules.Most(_engine.State.Env.PriceLevel, BondWorth()) : 0;

        /// <summary>The flat most and the cap of one purchase now (what the Bonds tab says the most is made of).</summary>
        public long BondFlat => Running ? BondRules.Most(_engine.State.Env.PriceLevel) : 0;

        public long BondCap => Running ? BondRules.Cap(_engine.State.Env.PriceLevel) : 0;

        // What the player is worth for the most of one purchase: nothing (the flat sum) with larger purchases off.
        private long BondWorth()
        {
            return Settings.MarketsLarge ? PlayerWorth() : 0L;
        }

        /// <summary>Whether a bond can be bought now for so much money (nothing is changed).</summary>
        public BondWhy CanBuyBond(BondOffer offer, long money)
        {
            BondWhy why = BondReady(true);
            return why != BondWhy.None ? why : _engine.CanBuyBond(offer, money, Trading, Settings.MarketsBonds, BondWorth());
        }

        /// <summary>A bond bought now with so much of the player's cash, set to roll over at its end or not. Null with the reason when it cannot be.</summary>
        public BondHolding BuyBond(BondOffer offer, long money, int reinvest, bool rollover, out BondWhy why)
        {
            FinishMoment();
            why = BondReady(true);
            if (why != BondWhy.None)
            {
                return null;
            }
            try
            {
                BondHolding h = _engine.BuyBond(offer, money, reinvest, Trading, Settings.MarketsBonds, out why, BondWorth(), rollover);
                if (h != null)
                {
                    Logger?.Info("Stock market: bought " + BondText.Name(h) + " for " + money + " at " + h.RateBp + " bp a year, until day " + h.EndDay + ", " + h.Payments + " payment(s), "
                                 + h.Reinvest + "% of its interest put back" + (h.Rollover ? ", rolling over at its end." : "."));
                }
                Moved();
                return h;
            }
            catch (Exception e)
            {
                Fault("buying a bond", e);
                return null;
            }
        }

        /// <summary>Whether a ladder can be bought now for so much (nothing is changed).</summary>
        public BondWhy LadderWhy(long money)
        {
            BondWhy why = BondReady(true);
            return why != BondWhy.None ? why : _engine.LadderWhy(money, Trading, Settings.MarketsBonds, BondWorth());
        }

        /// <summary>A ladder's pieces for a sum, the shortest term's first; how many (none while the market is not running).</summary>
        public int LadderPieces(long money, long[] into)
        {
            return Running ? _engine.LadderPieces(money, into) : 0;
        }

        /// <summary>A ladder bought now: one sum spread evenly over the Treasury's terms, a bond each. How many were bought (0 with the reason when it cannot be).</summary>
        public int BuyLadder(long money, int reinvest, bool rollover, out BondWhy why)
        {
            FinishMoment();
            why = BondReady(true);
            if (why != BondWhy.None)
            {
                return 0;
            }
            try
            {
                int bought = _engine.BuyLadder(money, reinvest, rollover, Trading, Settings.MarketsBonds, out why, BondWorth());
                if (bought > 0)
                {
                    Logger?.Info("Stock market: a ladder bought for " + money + ": " + bought + " Treasury bond(s), " + BondRules.ReinvestOf(reinvest) + "% of their interest put back"
                                 + (rollover ? ", each rolling over at its end." : "."));
                }
                Moved();
                return bought;
            }
            catch (Exception e)
            {
                Fault("buying a ladder", e);
                return 0;
            }
        }

        /// <summary>Whether a bond rolls over at its end, from now on.</summary>
        public bool SetBondRollover(int id, bool on, out BondWhy why)
        {
            FinishMoment();
            why = BondReady(false);
            bool done = why == BondWhy.None && _engine.SetBondRollover(id, on, out why);
            Moved();
            return done;
        }

        /// <summary>A bond sold (a deposit closed) now; what it brought. False with the reason when it cannot be.</summary>
        public bool SellBond(int id, out long money, out BondWhy why)
        {
            FinishMoment();
            money = 0;
            why = BondReady(true);
            if (why != BondWhy.None)
            {
                return false;
            }
            try
            {
                BondHolding h = _engine.State.Bonds.Holding(id);
                string name = BondText.Name(h);
                bool done = _engine.SellBond(id, Trading, out money, out why);
                if (done)
                {
                    Logger?.Info("Stock market: sold " + name + " for " + money + ".");
                }
                Moved();
                return done;
            }
            catch (Exception e)
            {
                Fault("selling a bond", e);
                return false;
            }
        }

        /// <summary>The share of a bond's interest put back into it, from its next payment on.</summary>
        public bool SetBondReinvest(int id, int pct, out BondWhy why)
        {
            FinishMoment();
            why = BondReady(false);
            bool done = why == BondWhy.None && _engine.SetBondReinvest(id, pct, out why);
            Moved();
            return done;
        }

        /// <summary>What a bond would bring if it were sold now, in cents; its price for each 100 it repays.</summary>
        public long BondSale(BondHolding h)
        {
            return Running && h != null ? _engine.BondSale(h) : 0;
        }

        public double BondPrice(BondHolding h)
        {
            return Running && h != null ? _engine.BondPrice(h) : 100.0;
        }

        // ---------------------------------------------------------------- what the bonds tell

        private void BondHeard(BondNote n)
        {
            MarketState s = _engine != null ? _engine.State : null;
            BondDef d = BondCatalogue.Find(n.Def);
            if (s == null || d == null)
            {
                return;
            }
            BondHolding h = s.Bonds.Holding(n.Holding);
            string name = h != null ? BondText.Name(h) : BondText.Name(d, string.Empty);
            if (Verbose)
            {
                Logger?.Info("Stock market check (bond " + n.Holding + ", day " + s.Day + "): " + n.Kind + " " + n.A + " paid, " + n.B + " put back, " + n.C + " tax"
                             + (h != null ? "; " + h.Paid + " of " + h.Payments + " payments, face " + h.Face / 100 : string.Empty) + ": "
                             + (n.A >= 0 && n.B >= 0 && n.C >= 0 && (h == null || h.Paid <= h.Payments) ? "PASS" : "a payment is under nothing"));
            }
            if (n.Kind == BondNoteKind.Repaid)
            {
                Queue(NotificationType.Success, "reallife_mkt_bond_note_repaid", "name", name, "amount", MarketText.Money(n.A));
                return;
            }
            if (n.Kind == BondNoteKind.Rolled)
            {
                Queue(NotificationType.Success, "reallife_mkt_bond_note_rolled", "name", name, "amount", MarketText.Money(n.A), "rate", BondText.Rate((int)n.B), "day", n.C.ToString());
                return;
            }
            if (n.A <= 0 && n.B <= 0)
            {
                return;   // (a payment too small for a whole unit: carried on to the next)
            }
            if (Settings.MarketsBondSoFar && h != null)
            {
                string total = MarketText.Money(BondRules.InterestSoFar(h) / 100);   // (this payment included)
                if (n.B > 0)
                {
                    Queue(NotificationType.Success, "reallife_mkt_bond_note_paid_back_total", "name", name, "amount", MarketText.Money(n.A), "back", MarketText.Money(n.B), "total", total);
                }
                else
                {
                    Queue(NotificationType.Success, "reallife_mkt_bond_note_paid_total", "name", name, "amount", MarketText.Money(n.A), "total", total);
                }
                return;
            }
            if (n.B > 0)
            {
                Queue(NotificationType.Success, "reallife_mkt_bond_note_paid_back", "name", name, "amount", MarketText.Money(n.A), "back", MarketText.Money(n.B));
            }
            else
            {
                Queue(NotificationType.Success, "reallife_mkt_bond_note_paid", "name", name, "amount", MarketText.Money(n.A));
            }
        }

        // ---------------------------------------------------------------- the save tools and the tests

        // Leaving a save: every bond repaid at today's price with no spread, a deposit in full; what that comes to.
        private long BondsSettled(StringBuilder log)
        {
            MarketState s = _engine.State;
            long total = 0;
            for (int i = 0; i < s.Bonds.Holdings.Count; i++)
            {
                BondHolding h = s.Bonds.Holdings[i];
                BondDef d = BondCatalogue.Find(h.Def);
                long cents = d == null || d.Kind == BondKind.Deposit ? h.Face : (long)Math.Floor(h.Face / 100.0 * _engine.BondPrice(h));
                long money = BondRules.Money(cents, ref h.Carry);
                total += money;
                log.Append(' ').Append(BondText.Name(h)).Append(" for ").Append(money).Append(';');
            }
            s.Bonds.Holdings.Clear();
            return total;
        }

        /// <summary>Test button: the Stocks app opened on its Bonds tab.</summary>
        public void TestBonds()
        {
            FinishMoment();
            const string Test = "Open the Bonds tab";
            if (!TestReady(Test, false))
            {
                return;
            }
            if (_view == null)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_mkt_test_no_app");
                return;
            }
            _view.Want(MarketApp.TabBonds);
            OpenAppPartial();
            Logger?.Info(Test + " test: the app is open on its Bonds tab (" + State.Bonds.Holdings.Count + " bond(s) held, " + BondOffers().Count + " on offer).");
        }

        /// <summary>Test button: each bond's next payment made now, as on its day (one at its end repays as well).</summary>
        public void TestBondPay()
        {
            FinishMoment();
            const string Test = "My bonds pay now";
            if (!TestReady(Test, true))
            {
                return;
            }
            if (State.Bonds.Holdings.Count == 0)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_mkt_bond_test_none");
                return;
            }
            int paid = _engine.PayBondsNow();
            Logger?.Info(Test + " test: " + paid + " bond payment(s) made now.");
            Moved();
        }

        /// <summary>Test button: one bond brought to its end now (the first set to roll over, else the first held), every payment still to come made, then repaid or rolled over.</summary>
        public void TestRollover()
        {
            FinishMoment();
            const string Test = "My first bond ended now, rolled over if set";
            if (!TestReady(Test, true))
            {
                return;
            }
            if (State.Bonds.Holdings.Count == 0)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_mkt_bond_test_none");
                return;
            }
            int count = State.Bonds.Holdings.Count;
            int id = _engine.EndBondNow();
            Logger?.Info(Test + " test: bond " + id + " brought to its end now; " + (State.Bonds.Holdings.Count == count ? "rolled over into bond " + (State.Bonds.NextId - 1) : "repaid") + ".");
            Moved();
        }
    }
}
