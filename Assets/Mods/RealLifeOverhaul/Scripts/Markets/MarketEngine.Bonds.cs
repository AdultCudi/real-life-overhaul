using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>The transaction keys of the bonds' money. Each has a text and a "_label" in en.json; "shares_" puts them under investments in the Finance app.</summary>
    public static class BondMoney
    {
        public const string Bought = "reallife_transaction_shares_bond_bought";
        public const string Sold = "reallife_transaction_shares_bond_sold";
        public const string Paid = "reallife_transaction_shares_bond_paid";       // a payment of interest, the part paid out
        public const string Repaid = "reallife_transaction_shares_bond_repaid";   // what it repays, at its end
    }

    /// <summary>
    /// The player's bonds on the market's clock (Markets/Bonds/BondRules), at each market day's close after its
    /// dividends: each bond makes every payment whose day has come (a weekend's on the Monday), the part of its interest
    /// the player chose put back into it at the day's price and the rest paid into their cash as a transaction of its
    /// own; a bond at its end repays what it is worth with its last payment and is gone. Bought and sold while the market
    /// trades, at the city's rates of the day. Game-free. The cost: one look at each bond at a close; nothing per frame.
    /// </summary>
    public sealed partial class MarketEngine
    {
        /// <summary>What a bond did (a payment, its end), for the director to tell once the frame's work is done.</summary>
        public Action<BondNote> BondTold;

        /// <summary>A bond's name for the game's transactions (the director's, in the language of the moment); none in the checks.</summary>
        public Func<BondHolding, string> BondName;

        private string NameOf(BondHolding h)
        {
            return BondName != null ? BondName(h) ?? string.Empty : string.Empty;
        }

        // ---------------------------------------------------------------- at the close

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
                    Repay(h, day);
                    held.RemoveAt(i);
                }
            }
        }

        // A payment of interest: the tax withheld, the share chosen put back into the bond at the day's price (none with the
        // last: there is nothing left to buy), the rest paid out.
        private void Pay(BondHolding h, BondDef d, int day)
        {
            long interest = BondRules.InterestCents(h, d);
            h.Paid++;
            bool last = h.Paid >= h.Payments;
            BondRules.Share(interest, State.Env.Lever(MarketInputs.BusinessTaxLever), last ? 0 : h.Reinvest, out long tax, out long back, out long paid);
            long putBack = 0;
            if (back > 0)
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
                _world.Wallet.Give(money, BondMoney.Paid, NameOf(h));
            }
            BondTold?.Invoke(new BondNote { Kind = BondNoteKind.Interest, Holding = h.Id, Def = h.Def, A = money, B = putBack / 100, C = tax / 100 });
        }

        // The end of its term: what it repays (and the cents carried) into the player's cash.
        private void Repay(BondHolding h, int day)
        {
            long money = BondRules.Money(h.Face, ref h.Carry);
            if (money > 0)
            {
                _world.Wallet.Give(money, BondMoney.Repaid, NameOf(h));
            }
            BondTold?.Invoke(new BondNote { Kind = BondNoteKind.Repaid, Holding = h.Id, Def = h.Def, A = money });
        }

        // ---------------------------------------------------------------- the player's asks

        /// <summary>The bonds on offer today, a deposit at each of the banks named (the largest first).</summary>
        public void BondOffers(IList<string> banks, List<BondOffer> into)
        {
            BondRules.Offers(State.Env, Year, banks, into);
        }

        /// <summary>A bond of the catalogue (a deposit at a bank's rank and name) as offered today.</summary>
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

        /// <summary>Whether a bond can be bought now for so much money; 'open' while the market trades, 'on' while bonds are switched on.</summary>
        public BondWhy CanBuyBond(BondOffer offer, long money, bool open, bool on)
        {
            return BondRules.CanBuy(State.Bonds, offer, money, _world.Wallet.Cash, State.Env.PriceLevel, open, Held, on);
        }

        /// <summary>A bond bought now for so much of the player's cash, at its yield of today. Null with the reason when it cannot be.</summary>
        public BondHolding BuyBond(BondOffer offer, long money, int reinvest, bool open, bool on, out BondWhy why)
        {
            Finish();   // (a moment half played is played out first: Midway)
            why = CanBuyBond(offer, money, open, on);
            if (why != BondWhy.None)
            {
                return null;
            }
            BondHolding h = BondRules.Make(State.Bonds.NextId, offer, money, State.Day, Year, reinvest, false);
            if (!_world.Wallet.Take(money, BondMoney.Bought, NameOf(h)))
            {
                why = BondWhy.NoCash;
                return null;
            }
            State.Bonds.NextId++;
            State.Bonds.Holdings.Add(h);
            return h;
        }

        /// <summary>A bond sold (a deposit closed) now: what it brings into the player's cash. False with the reason when it cannot be.</summary>
        public bool SellBond(int id, bool open, out long money, out BondWhy why)
        {
            Finish();   // (a moment half played is played out first: Midway)
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
                _world.Wallet.Give(money, BondMoney.Sold, NameOf(h));
            }
            return true;
        }

        /// <summary>The share of each payment's interest put back into a bond, from its next payment on. False while orders wait after a load.</summary>
        public bool SetBondReinvest(int id, int pct, out BondWhy why)
        {
            Finish();   // (a moment half played is played out first: Midway)
            BondHolding h = State.Bonds.Holding(id);
            why = h == null ? BondWhy.NotHeld : Held ? BondWhy.Held : BondWhy.None;
            if (why != BondWhy.None)
            {
                return false;
            }
            h.Reinvest = BondRules.ReinvestOf(pct);
            return true;
        }

        /// <summary>What a bond would bring if it were sold now, in cents.</summary>
        public long BondSale(BondHolding h)
        {
            BondDef d = BondCatalogue.Find(h.Def);
            return d != null ? BondRules.SaleCents(h, d, State.Env, State.Day, Year) : Math.Max(0L, h.Face);
        }

        /// <summary>A bond's price today for each 100 it repays.</summary>
        public double BondPrice(BondHolding h)
        {
            BondDef d = BondCatalogue.Find(h.Def);
            return d != null ? BondRules.Price(h, d, State.Env, State.Day, Year) : 100.0;
        }

        /// <summary>The test tools: every bond's next payment made now, as on its day (one at its end repays as well). How many were paid.</summary>
        public int PayBondsNow()
        {
            Finish();   // (a moment half played is played out first: Midway)
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
                    Repay(h, State.Day);
                    held.RemoveAt(i);
                }
            }
            return count;
        }
    }
}
