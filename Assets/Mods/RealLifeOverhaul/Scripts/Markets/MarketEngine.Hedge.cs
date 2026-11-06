using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Part of the engine: a buyer's hedge (HedgeRules) bought as ordinary futures through the margin account and the
    /// broker's rules, every limit, spread and commission of a contract bought on the Futures tab, settled in cash; and
    /// the test tools' contracts settled now or undone. A few quotes a leg, on a press; nothing per frame.
    /// </summary>
    public sealed partial class MarketEngine
    {
        /// <summary>A material's price now in micros a unit (the last close's); 0 for one the exchange does not have.</summary>
        public long MaterialNow(string material)
        {
            RawMaterial m = MaterialOf(material);
            return m != null ? m.Fix : 0;
        }

        /// <summary>Units more of a material that may be under contract now, every contract on it counted.</summary>
        public long MaterialRoom(string material)
        {
            return Math.Max(0L, DerivCatalogue.MaterialUnitsMost - DerivRules.UnitsOn(Book, material));
        }

        /// <summary>A hedge's futures as they would be bought now: the margin they take in money, and the commission.</summary>
        public long HedgeCost(IList<HedgeLeg> legs, out long fees)
        {
            long margin = 0;
            fees = 0;
            for (int i = 0; legs != null && i < legs.Count; i++)
            {
                DerivQuote q = QuoteFuture(legs[i].Material, legs[i].SettleDay, true, legs[i].Units);
                if (q.Why == DerivWhy.None)
                {
                    margin += q.Margin;
                    fees += q.Fee;
                }
            }
            return margin;
        }

        /// <summary>
        /// A hedge bought now: each leg a future bought for its day, in cash, its margin and commission from the cash as any
        /// future's. Stops at the first leg that cannot be bought (why says it); the ids bought go into <paramref name="opened"/>.
        /// A test's hedge is the test tools' (marked, never delivered, put back by <see cref="UndoTests"/>) and may be bought
        /// outside a session. How many were bought.
        /// </summary>
        public int Hedge(IList<HedgeLeg> legs, bool open, bool test, List<int> opened, out DerivWhy why)
        {
            Finish();
            why = DerivWhy.None;
            int bought = 0;
            for (int i = 0; legs != null && i < legs.Count; i++)
            {
                HedgeLeg leg = legs[i];
                DerivContract c = OpenContract(ContractKind.Long, leg.Material, 0, leg.SettleDay, 0, leg.Units, null, null, open || test, test, out DerivWhy w, test);
                if (c == null)
                {
                    why = w;
                    break;
                }
                opened?.Add(c.Id);
                bought++;
            }
            return bought;
        }

        /// <summary>The test tools: the given contracts settle now as at their day, at today's prices. How many.</summary>
        public int SettleNow(IList<int> ids)
        {
            Finish();
            int count = 0;
            for (int i = 0; ids != null && i < ids.Count; i++)
            {
                DerivContract c = Book.Contract(ids[i]);
                RawMaterial m = c != null && c.IsOpen && c.IsFuture ? MaterialOf(c.Material) : null;
                if (m == null)
                {
                    continue;
                }
                long move = DerivRules.Move(c.Kind, c.Qty, c.Mark, m.Fix);
                Book.Balance += move;
                c.Result += move;
                c.Mark = m.Fix;
                SettleFuture(c, m);
                count++;
            }
            if (count > 0)
            {
                FuturesMargin(State.Day);
            }
            return count;
        }

        /// <summary>
        /// The test tools put back: every contract a test bought closed at its fair price, taken off the book with what it
        /// added to the account's figures, and the money it moved given back or taken, so the cash is as it was before (to a
        /// unit, as the broker rounds). How many; <paramref name="money"/> is what was given back (under nothing: taken).
        /// </summary>
        public int UndoTests(out long money)
        {
            Finish();
            money = 0;
            long cents = 0;
            int count = 0;
            for (int i = Book.Contracts.Count - 1; i >= 0; i--)
            {
                DerivContract c = Book.Contracts[i];
                if (!c.Test)
                {
                    continue;
                }
                if (c.IsOpen)
                {
                    RawMaterial m = c.IsFuture ? MaterialOf(c.Material) : null;
                    long price = m != null ? MaterialForward(m, c.SettleDay, State.Phase) : c.Mark;
                    if (c.IsFuture)
                    {
                        long move = DerivRules.Move(c.Kind, c.Qty, c.Mark, price);
                        Book.Balance += move;
                        c.Result += move;
                        c.Mark = price;
                    }
                    End(c, ContractEnd.Closed, price);
                }
                cents += c.Result;
                Book.Realised -= c.Result;
                Book.Commission -= c.Fees;
                Book.Contracts.RemoveAt(i);
                count++;
            }
            if (count == 0)
            {
                return 0;
            }
            Release(State.Day);   // their margins back to the cash, as every contract's are once it is no longer needed
            money = -HedgeRules.Saved(cents);
            if (money > 0)
            {
                Wallet.Give(money, DerivMoney.Back, string.Empty);
            }
            else if (money < 0)
            {
                Wallet.Force(-money, DerivMoney.Margin, string.Empty);
            }
            return count;
        }
    }
}
