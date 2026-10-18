using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>The transaction keys of the bonds' money; "shares_" files them under investments in the Finance app.</summary>
    public static class BondMoney
    {
        public const string Bought = "reallife_transaction_shares_bond_bought";
        public const string Sold = "reallife_transaction_shares_bond_sold";
        public const string Paid = "reallife_transaction_shares_bond_paid";       // a payment of interest, the part paid out
        public const string Repaid = "reallife_transaction_shares_bond_repaid";   // what it repays, at its end
    }

    /// <summary>
    /// Part of the engine: the player's bonds. At each close a bond makes every payment due, putting back the share chosen;
    /// at its end it repays, or rolls over at the day's yield with no money changing hands. A look at each bond at a close,
    /// nothing per frame.
    /// </summary>
    public sealed partial class MarketEngine
    {
        /// <summary>Told once the frame's work is done.</summary>
        public Action<BondNote> BondTold;

        /// <summary>A bond's name for the game's transactions, in the language of the moment; null in the checks.</summary>
        public Func<BondHolding, string> BondName;

        /// <summary>Settings.MarketsBonds, set by the director; off, a bond set to roll over is repaid at its end.</summary>
        public bool BondsOn = true;

        private readonly List<BondDef> _ladder = new List<BondDef>(8);   // the Treasury's bonds a ladder spreads over
        private readonly long[] _pieces = new long[8];                    // a ladder's pieces, in money

        private string NameOf(BondHolding h)
        {
            return BondName != null ? BondName(h) ?? string.Empty : string.Empty;
        }

        private void Income(int day)
        {
            List<BondHolding> held = State.Bonds.Holdings;
            for (int i = held.Count - 1; i >= 0; i--)
            {
                BondHolding h = held[i];
                BondDef d = BondCatalogue.Find(h.Def);
                if (d == null)
                {
                    continue;
                }
                while (h.Paid < h.Payments && BondRules.PayDay(h, h.Paid + 1) <= day)
                {
                    Pay(h, d, day);
                }
                if (h.Paid >= h.Payments)
                {
                    End(h, d, i, day);
                }
            }
        }

        // A bond after its last payment: rolled over in its place if set to while bonds are on, else repaid and gone.
        private void End(BondHolding h, BondDef d, int at, int day)
        {
            List<BondHolding> held = State.Bonds.Holdings;
            if (!h.Rollover || !BondsOn)
            {
                Repay(h, day);
                held.RemoveAt(at);
                return;
            }
            int yieldBp = BondRules.YieldBp(d.Kind, h.Bank, BondRules.TermDays(d, Year) / (double)Year, State.Env);
            long face = h.Face + Math.Max(0L, h.Carry);   // the cents carried go into it: nothing is lost
            h.Carry = 0;
            BondHolding next = BondRules.Rolled(h, d, State.Bonds.NextId++, yieldBp, face, day, Year);
            held[at] = next;
            BondTold?.Invoke(new BondNote { Kind = BondNoteKind.Rolled, Holding = next.Id, Def = d.Id, A = face / 100, B = yieldBp, C = next.EndDay });
        }

        // A payment of interest: tax withheld, the share chosen put back at the day's price (into the new bond at 100 when the
        // last rolls over), the rest paid out.
        private void Pay(BondHolding h, BondDef d, int day)
        {
            long interest = BondRules.InterestCents(h, d);
            h.Paid++;
            bool last = h.Paid >= h.Payments;
            bool rolls = last && h.Rollover && BondsOn;
            BondRules.Share(interest, State.Env.Lever(MarketInputs.BusinessTaxLever), last && !rolls ? 0 : h.Reinvest, out long tax, out long back, out long paid);
            long putBack = 0;
            if (back > 0 && rolls)
            {
                putBack = back;   // bought with the new bond, at 100 for each 100 (End)
                h.Face += back;
                h.Cost += back;
                h.Reinvested += back;
            }
            else if (back > 0)
            {
                long face = BondRules.Buys(back, BondRules.Price(h, d, State.Env, day, Year), out long left);
                putBack = back - left;
                h.Face += face;
                h.Cost += putBack;
                h.Reinvested += putBack;
                paid += left;
            }
            h.Taxed += tax;
            h.Received += paid;
            long money = BondRules.Money(paid, ref h.Carry);
            if (money > 0)
            {
                Wallet.Give(money, BondMoney.Paid, NameOf(h));
            }
            BondTold?.Invoke(new BondNote { Kind = BondNoteKind.Interest, Holding = h.Id, Def = h.Def, A = money, B = putBack / 100, C = tax / 100 });
        }

        private void Repay(BondHolding h, int day)
        {
            long money = BondRules.Money(h.Face, ref h.Carry);
            if (money > 0)
            {
                Wallet.Give(money, BondMoney.Repaid, NameOf(h));
            }
            BondTold?.Invoke(new BondNote { Kind = BondNoteKind.Repaid, Holding = h.Id, Def = h.Def, A = money });
        }

        /// <summary>The bonds on offer today, a deposit at each of the banks named, the largest first.</summary>
        public void BondOffers(IList<string> banks, List<BondOffer> into)
        {
            BondRules.Offers(State.Env, Year, banks, into);
        }

        public BondOffer Offer(int def, int bank, string issuer)
        {
            BondDef d = BondCatalogue.Find(def);
            if (d == null)
            {
                return default(BondOffer);
            }
            bool deposit = d.Kind == BondKind.Deposit;
            int at = deposit ? bank : -1;
            return new BondOffer
            {
                Def = d, Bank = at, Issuer = deposit ? issuer ?? string.Empty : string.Empty, YieldBp = BondRules.YieldBp(d.Kind, at, BondRules.TermDays(d, Year) / (double)Year, State.Env)
            };
        }

        /// <summary>Whether a bond can be bought for so much; <paramref name="worth"/> sets the most of one purchase (0: flat sum).</summary>
        public BondWhy CanBuyBond(BondOffer offer, long money, bool open, bool on, long worth = 0)
        {
            int level = State.Env.PriceLevel;
            return BondRules.CanBuy(State.Bonds, offer, money, Wallet.Cash, open, Held, on, BondRules.Least(level), BondRules.Most(level, worth), 1, BondRules.HoldingsMost(LargeOn));
        }

        /// <summary>Null with the reason when it cannot be bought.</summary>
        public BondHolding BuyBond(BondOffer offer, long money, int reinvest, bool open, bool on, out BondWhy why, long worth = 0, bool rollover = false)
        {
            Finish();
            why = CanBuyBond(offer, money, open, on, worth);
            if (why != BondWhy.None)
            {
                return null;
            }
            BondHolding h = BondRules.Make(State.Bonds.NextId, offer, money, State.Day, Year, reinvest, false);
            h.Rollover = rollover;
            if (!Wallet.Take(money, BondMoney.Bought, NameOf(h)))
            {
                why = BondWhy.NoCash;
                return null;
            }
            State.Bonds.NextId++;
            State.Bonds.Holdings.Add(h);
            return h;
        }

        /// <summary>Whether a ladder can be bought for so much; each piece is a purchase of its own, with room and cash for all.</summary>
        public BondWhy LadderWhy(long money, bool open, bool on, long worth = 0)
        {
            int n = BondRules.LadderTerms(_ladder);
            if (n == 0)
            {
                return BondWhy.NoBank;
            }
            BondRules.Ladder(money, n, _pieces);
            int level = State.Env.PriceLevel;
            long most = BondRules.Most(level, worth);
            BondOffer first = Offer(_ladder[0].Id, -1, null);
            BondWhy why = BondRules.CanBuy(State.Bonds, first, _pieces[n - 1], long.MaxValue, open, Held, on, BondRules.Least(level), most, n, BondRules.HoldingsMost(LargeOn));
            if (why != BondWhy.None)
            {
                return why;
            }
            return _pieces[0] > most ? BondWhy.TooMuch : money > Wallet.Cash ? BondWhy.NoCash : BondWhy.None;
        }

        /// <summary>One sum spread evenly over the Treasury's terms, a bond each; how many were bought, 0 with the reason.</summary>
        public int BuyLadder(long money, int reinvest, bool rollover, bool open, bool on, out BondWhy why, long worth = 0)
        {
            Finish();
            why = LadderWhy(money, open, on, worth);
            if (why != BondWhy.None)
            {
                return 0;
            }
            Wallet.Ready(money);   // one purchase: worked orders' money is taken back once, not for each piece
            int bought = 0;
            for (int i = 0; i < _ladder.Count; i++)
            {
                BondHolding h = BondRules.Make(State.Bonds.NextId, Offer(_ladder[i].Id, -1, null), _pieces[i], State.Day, Year, reinvest, false);
                h.Rollover = rollover;
                if (!Wallet.Take(_pieces[i], BondMoney.Bought, NameOf(h)))
                {
                    why = BondWhy.NoCash;   // the cash went between the check and the purchase: what was bought is kept
                    break;
                }
                State.Bonds.NextId++;
                State.Bonds.Holdings.Add(h);
                bought++;
            }
            return bought;
        }

        /// <summary>The ladder's pieces for a sum, shortest term first, as <see cref="BuyLadder"/> buys; how many.</summary>
        public int LadderPieces(long money, long[] into)
        {
            int n = BondRules.LadderTerms(_ladder);
            BondRules.Ladder(money, n, into);
            return n;
        }

        public bool SetBondRollover(int id, bool on, out BondWhy why)
        {
            Finish();
            BondHolding h = HeldBond(id, out why);
            if (h == null)
            {
                return false;
            }
            h.Rollover = on;
            return true;
        }

        /// <summary>A bond sold, or a deposit closed, into the player's cash; false with the reason when it cannot be.</summary>
        public bool SellBond(int id, bool open, out long money, out BondWhy why)
        {
            Finish();
            money = 0;
            BondHolding h = State.Bonds.Holding(id);
            why = h == null ? BondWhy.NotHeld : Held ? BondWhy.Held : !open ? BondWhy.Closed : BondWhy.None;
            if (why != BondWhy.None)
            {
                return false;
            }
            money = BondRules.Money(BondSale(h), ref h.Carry);
            State.Bonds.Holdings.Remove(h);
            if (money > 0)
            {
                Wallet.Give(money, BondMoney.Sold, NameOf(h));
            }
            return true;
        }

        /// <summary>From its next payment; false with the reason when it cannot be.</summary>
        public bool SetBondReinvest(int id, int pct, out BondWhy why)
        {
            Finish();
            BondHolding h = HeldBond(id, out why);
            if (h == null)
            {
                return false;
            }
            h.Reinvest = BondRules.ReinvestOf(pct);
            return true;
        }

        // A held bond to change, or null with the reason.
        private BondHolding HeldBond(int id, out BondWhy why)
        {
            BondHolding h = State.Bonds.Holding(id);
            why = h == null ? BondWhy.NotHeld : Held ? BondWhy.Held : BondWhy.None;
            return why == BondWhy.None ? h : null;
        }

        /// <summary>What a bond would bring if sold now, in cents.</summary>
        public long BondSale(BondHolding h)
        {
            BondDef d = BondCatalogue.Find(h.Def);
            return d != null ? BondRules.SaleCents(h, d, State.Env, State.Day, Year) : Math.Max(0L, h.Face);
        }

        /// <summary>For each 100 it repays.</summary>
        public double BondPrice(BondHolding h)
        {
            BondDef d = BondCatalogue.Find(h.Def);
            return d != null ? BondRules.Price(h, d, State.Env, State.Day, Year) : 100.0;
        }

        /// <summary>The test tools: every bond's next payment made now; one at its end repays or rolls over. How many.</summary>
        public int PayBondsNow()
        {
            Finish();
            int count = 0;
            List<BondHolding> held = State.Bonds.Holdings;
            for (int i = held.Count - 1; i >= 0; i--)
            {
                BondHolding h = held[i];
                BondDef d = BondCatalogue.Find(h.Def);
                if (d == null || h.Paid >= h.Payments)
                {
                    continue;
                }
                Pay(h, d, State.Day);
                count++;
                if (h.Paid >= h.Payments)
                {
                    End(h, d, i, State.Day);
                }
            }
            return count;
        }

        /// <summary>The test tools: one bond brought to its end now (the first set to roll over, else the first); its number, 0 with none.</summary>
        public int EndBondNow()
        {
            Finish();
            List<BondHolding> held = State.Bonds.Holdings;
            int at = -1;
            for (int i = 0; i < held.Count && at < 0; i++)
            {
                at = held[i].Rollover ? i : -1;
            }
            at = at < 0 && held.Count > 0 ? 0 : at;
            BondHolding h = at >= 0 ? held[at] : null;
            BondDef d = h != null ? BondCatalogue.Find(h.Def) : null;
            if (d == null)
            {
                return 0;
            }
            while (h.Paid < h.Payments)
            {
                Pay(h, d, State.Day);
            }
            End(h, d, at, State.Day);
            return h.Id;
        }
    }
}
