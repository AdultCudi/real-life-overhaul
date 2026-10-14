using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The Bonds tab of the Stocks app (Markets/Bonds): the bonds on offer today, with their yield, term and payments, and
    /// the player's own, with their price, what they would bring and their next payment (the filter shows one or the
    /// other). Picking a bond on offer puts what it pays in the box, with the amount to put in, the share of its interest
    /// to put back into it and the button to buy it; picking one of the player's puts its record there, with that share
    /// to change and the button to sell it (or close a deposit). Written when the app opens and when the player does
    /// something in it, never per frame.
    /// </summary>
    public sealed partial class MarketApp
    {
        public const int TabBonds = 7;
        private const string BondOfferRow = "bo:", BondHeldRow = "bd:";

        // The purchase being written for the offer picked.
        private string _bondFor;
        private long _bondAmount;
        private int _bondReinvest;

        private TableSpec WriteBonds(InfoBox box, MarketState s)
        {
            List<BondOffer> offers = _director.BondOffers();
            int year = Math.Max(8, s.YearDays);
            for (int i = 0; i < offers.Count; i++)
            {
                BondOffer o = offers[i];
                TableRow row = Row(BondOfferRow, i);
                row.Kind = MarketTables.BondsOffered;
                row.Set(0, BondText.Name(o.Def, o.Issuer));
                row.Set(1, BondText.KindWords(o.Def));
                row.Set(2, BondText.Term(o.Def.TermQuarters), BondRules.TermDays(o.Def, year));
                row.Set(3, BondText.Rate(o.YieldBp), o.YieldBp);
                row.Set(4, BondText.Pays(o.Def));
                row.Set(5, BondText.Price(100.0), 100.0);
                row.Set(6, string.Empty, double.NaN);
                row.Set(7, string.Empty, double.NaN);
                row.Set(8, string.Empty);
            }
            long worth = 0;
            int next = 0;
            for (int i = 0; i < s.Bonds.Holdings.Count; i++)
            {
                BondHolding h = s.Bonds.Holdings[i];
                BondDef d = BondCatalogue.Find(h.Def);
                if (d == null)
                {
                    continue;
                }
                long sale = _director.BondSale(h);
                worth += sale / 100;
                int day = BondRules.NextPayDay(h);
                next = day > 0 && (next == 0 || day < next) ? day : next;
                TableRow row = Row(BondHeldRow, h.Id);
                row.Kind = MarketTables.BondsHeld;
                row.Set(0, BondText.Name(h));
                row.Set(1, BondText.KindWords(d));
                row.Set(2, MarketText.Fill("reallife_mkt_day", "day", h.EndDay.ToString()), h.EndDay);
                row.Set(3, BondText.Rate(h.RateBp), h.RateBp);
                row.Set(4, BondText.Pays(d));
                double price = _director.BondPrice(h);
                int move = (int)Math.Round((price - 100.0) * 100.0);
                row.Set(5, d.Kind == BondKind.Deposit ? "-" : BondText.Price(price), price, Tone(move));
                row.Set(6, BondText.Money(h.Face), h.Face / 100.0);
                row.Set(7, BondText.Money(sale), sale / 100.0, Tone(Math.Sign(sale - h.Cost)));
                row.Set(8, day > 0 ? NextWords(BondRules.InterestCents(h, d) + (h.Paid + 1 >= h.Payments ? h.Face : 0), day) : string.Empty, day);
            }
            int offer = Picked(_picked, BondOfferRow);
            int held = Picked(_picked, BondHeldRow);
            BondHolding mine = held != int.MinValue ? s.Bonds.Holding(held) : null;
            if (offer != int.MinValue && offer >= 0 && offer < offers.Count)
            {
                WriteBondOffer(box, s, offers[offer], _picked);
            }
            else if (mine != null)
            {
                WriteBondHeld(box, s, mine);
            }
            else
            {
                box.Title(MarketText.T("reallife_mkt_tab_bonds"));
                StatusLine(box);
                box.Line(s.Bonds.Holdings.Count > 0
                    ? MarketText.Fill("reallife_mkt_bond_box_held", "count", s.Bonds.Holdings.Count.ToString(), "worth", MarketText.Money(worth), "day", next.ToString())
                    : MarketText.T("reallife_mkt_bond_box_none"));
                box.Line(MarketText.Fill("reallife_mkt_bond_box_rates", "rate", BondText.Rate(s.Env.RateBp),
                    "year", BondText.Rate(BondRules.YieldBp(BondKind.Treasury, -1, 1.0, s.Env)), "five", BondText.Rate(BondRules.YieldBp(BondKind.Treasury, -1, 5.0, s.Env))));
                box.Line(MarketText.T(Settings.MarketsBonds ? "reallife_mkt_bond_box_help" : "reallife_mkt_bond_box_off"));
                Meta.Guide.Point(box, GuideTitle);
            }
            return MarketTables.Bonds;
        }

        // A payment to come in words: "120, day 34".
        private static string NextWords(long cents, int day)
        {
            return MarketText.Fill("reallife_mkt_next_pay", "amount", BondText.Money(cents), "day", day.ToString());
        }

        // A bond on offer: what it pays, the amount to put in, the share of its interest to put back, and the button to buy it.
        private void WriteBondOffer(InfoBox box, MarketState s, BondOffer o, string rowId)
        {
            if (_bondFor != rowId)
            {
                _bondFor = rowId;
                _bondAmount = Math.Max(_director.BondLeast, _bondAmount);
                _bondReinvest = 0;
            }
            int year = Math.Max(8, s.YearDays);
            string name = BondText.Name(o.Def, o.Issuer);
            box.Title(name);
            StatusLine(box);
            box.Note(MarketText.Fill("reallife_mkt_bond_offer_what", "rate", BondText.Rate(o.YieldBp), "pays", BondText.Pays(o.Def), "term", BondText.Term(o.Def.TermQuarters),
                "day", (s.Day + BondRules.TermDays(o.Def, year)).ToString()));
            box.Line(MarketText.Fill(o.Def.Kind == BondKind.Deposit ? "reallife_mkt_bond_about_deposit" : o.Def.Kind == BondKind.City ? "reallife_mkt_bond_about_city" : "reallife_mkt_bond_about_treasury",
                "bank", o.Issuer ?? string.Empty, "months", BondCatalogue.DepositPenaltyMonths.ToString()));
            box.Line(MarketText.Fill("reallife_mkt_bond_offer_limits", "least", MarketText.Money(_director.BondLeast), "most", MarketText.Money(_director.BondMost)));
            box.Selector(BondCatalogue.Reinvest.Length, Math.Max(0, Array.IndexOf(BondCatalogue.Reinvest, _bondReinvest)), ReinvestWords, i => UiClone.Safe(() =>
            {
                _bondReinvest = BondCatalogue.Reinvest[i];
                Refresh();
            }));
            BondWhy why = _director.CanBuyBond(o, _bondAmount);
            if (why == BondWhy.None)
            {
                long each = _bondAmount * 100L * o.YieldBp / (10000L * Math.Max(1, o.Def.PaysPerYear));
                box.Line(MarketText.Fill("reallife_mkt_bond_offer_pays", "amount", BondText.Money(each), "count", o.Def.Payments.ToString()));
            }
            else
            {
                box.Line(BondText.Why(why));
            }
            Transform buttons = box.ButtonRow();
            AskNumber(buttons, InfoBox.Additional, MarketText.Fill("reallife_mkt_bond_amount", "amount", MarketText.Money(_bondAmount)), "reallife_mkt_bond_amount_placeholder", false,
                value => _bondAmount = value);
            BondOffer offer = o;
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_mkt_bond_buy", () => UiClone.Safe(() => ConfirmBond(offer)));
            Meta.Guide.Point(box, GuideTitle);
        }

        private static string ReinvestWords(int i)
        {
            int pct = BondCatalogue.Reinvest[Mathf.Clamp(i, 0, BondCatalogue.Reinvest.Length - 1)];
            return pct <= 0 ? MarketText.T("reallife_mkt_bond_set_reinvest_none") : MarketText.Fill("reallife_mkt_bond_set_reinvest", "pct", pct.ToString());
        }

        private void ConfirmBond(BondOffer o)
        {
            long amount = _bondAmount;
            int reinvest = _bondReinvest;
            BondWhy why = _director.CanBuyBond(o, amount);   // (as it is now, not as it was drawn)
            if (why != BondWhy.None)
            {
                BondError(why);
                return;
            }
            string name = BondText.Name(o.Def, o.Issuer);
            int end = (_director.State != null ? _director.State.Day : 0) + BondRules.TermDays(o.Def, _director.State != null ? _director.State.YearDays : 60);
            GameConfirm.Show(
                MarketText.Fill("reallife_mkt_bond_confirm_buy", "name", name),
                MarketText.Fill("reallife_mkt_bond_confirm_buy_body", "amount", MarketText.Money(amount), "name", name, "rate", BondText.Rate(o.YieldBp), "day", end.ToString(), "reinvest",
                    ReinvestWords(Math.Max(0, Array.IndexOf(BondCatalogue.Reinvest, reinvest)))),
                () => UiClone.Safe(() =>
                {
                    BondHolding h = _director.BuyBond(o, amount, reinvest, out BondWhy refused);
                    if (h == null)
                    {
                        BondError(refused);
                    }
                    else
                    {
                        Notifications.Show(NotificationType.Success, "reallife_mkt_bond_note_bought",
                            new Dictionary<string, string> { { "name", name }, { "amount", MarketText.Money(amount) }, { "rate", BondText.Rate(h.RateBp) } }, 6f, null, null, true, false);
                        _picked = BondHeldRow + h.Id;
                    }
                    Refresh();
                }), null, "reallife_mkt_bond_buy", "common_cancel", false, false);
        }

        // One of the player's bonds: its record, the share of its interest put back, and the button to sell it.
        private void WriteBondHeld(InfoBox box, MarketState s, BondHolding h)
        {
            BondDef d = BondCatalogue.Find(h.Def);
            string name = BondText.Name(h);
            box.Title(name);
            StatusLine(box);
            box.Note(MarketText.Fill("reallife_mkt_bond_held_what", "face", BondText.Money(h.Face), "rate", BondText.Rate(h.RateBp), "day", h.EndDay.ToString(), "paid", h.Paid.ToString(),
                "count", h.Payments.ToString()));
            int day = BondRules.NextPayDay(h);
            if (day > 0 && d != null)
            {
                box.Line(MarketText.Fill(h.Paid + 1 >= h.Payments ? "reallife_mkt_bond_held_last" : "reallife_mkt_bond_held_next", "day", day.ToString(),
                    "amount", BondText.Money(BondRules.InterestCents(h, d)), "face", BondText.Money(h.Face)));
            }
            box.Line(MarketText.Fill("reallife_mkt_bond_held_record", "received", BondText.Money(h.Received), "back", BondText.Money(h.Reinvested), "tax", BondText.Money(h.Taxed)));
            long sale = _director.BondSale(h);
            if (d != null && d.Kind == BondKind.Deposit)
            {
                box.Line(MarketText.Fill("reallife_mkt_bond_held_close", "worth", BondText.Money(sale), "months", BondCatalogue.DepositPenaltyMonths.ToString()));
            }
            else
            {
                box.Line(MarketText.Fill("reallife_mkt_bond_held_price", "price", BondText.Price(_director.BondPrice(h)), "worth", BondText.Money(sale), "gain", BondText.Money(sale - h.Cost)));
            }
            int id = h.Id;
            box.Selector(BondCatalogue.Reinvest.Length, Math.Max(0, Array.IndexOf(BondCatalogue.Reinvest, h.Reinvest)), ReinvestWords, i => UiClone.Safe(() =>
            {
                if (!_director.SetBondReinvest(id, BondCatalogue.Reinvest[i], out BondWhy why))
                {
                    BondError(why);
                }
                Refresh();
            }));
            Transform buttons = box.ButtonRow();
            bool deposit = d != null && d.Kind == BondKind.Deposit;
            InfoBox.Button(buttons, InfoBox.Negative, deposit ? "reallife_mkt_bond_close" : "reallife_mkt_bond_sell", () => UiClone.Safe(() => ConfirmSellBond(id, name, deposit)));
            Meta.Guide.Point(box, GuideTitle);
        }

        private void ConfirmSellBond(int id, string name, bool deposit)
        {
            MarketState s = _director.State;
            BondHolding h = s != null ? s.Bonds.Holding(id) : null;
            if (h == null)
            {
                BondError(BondWhy.NotHeld);
                return;
            }
            GameConfirm.Show(
                MarketText.Fill(deposit ? "reallife_mkt_bond_confirm_close" : "reallife_mkt_bond_confirm_sell", "name", name),
                MarketText.Fill("reallife_mkt_bond_confirm_sell_body", "amount", BondText.Money(_director.BondSale(h))),
                () => UiClone.Safe(() =>
                {
                    if (!_director.SellBond(id, out long money, out BondWhy why))
                    {
                        BondError(why);
                    }
                    else
                    {
                        Notifications.Show(NotificationType.Success, "reallife_mkt_bond_note_sold", new Dictionary<string, string> { { "name", name }, { "amount", MarketText.Money(money) } }, 6f, null, null,
                            true, false);
                        _picked = null;
                    }
                    Refresh();
                }), null, deposit ? "reallife_mkt_bond_close" : "reallife_mkt_bond_sell", "common_cancel", false, true);
        }

        private static void BondError(BondWhy why)
        {
            Notifications.Show(NotificationType.Error, "reallife_mkt_bond_note_refused", new Dictionary<string, string> { { "why", BondText.Why(why) } }, 8f, null, null, true, false);
        }

        // ---------------------------------------------------------------- the Portfolio tab's next payments

        // A holding's next payment: a dividend owed after its record day, else one declared (a short's is owed by the player).
        private static string NextPayment(MarketState s, Stock k, Position p, out double key)
        {
            key = double.NaN;
            long cents = 0;
            int day = 0;
            for (int i = 0; i < s.Dues.Count; i++)
            {
                DividendDue due = s.Dues[i];
                if (due.StockId == k.Id && due.Bot == 0)
                {
                    cents += due.Cents;
                    day = day == 0 || due.PayDay < day ? due.PayDay : day;
                }
            }
            if (day == 0 && k.Listed && k.DeclaredCents > 0 && !k.ExDone && k.PayDay > 0)
            {
                cents = p.Shares * k.DeclaredCents;
                day = k.PayDay;
            }
            if (day == 0 || cents == 0)
            {
                return string.Empty;
            }
            key = day;
            return MarketText.Fill("reallife_mkt_next_pay", "amount", (cents < 0 ? "-" : string.Empty) + BondText.Money(Math.Abs(cents)), "day", day.ToString());
        }

        // The account box's line of the bonds held: their worth and the next payment.
        private void BondsLine(InfoBox box, MarketState s)
        {
            if (s.Bonds.Holdings.Count == 0)
            {
                return;
            }
            long worth = 0;
            int next = 0;
            for (int i = 0; i < s.Bonds.Holdings.Count; i++)
            {
                BondHolding h = s.Bonds.Holdings[i];
                worth += _director.BondSale(h) / 100;
                int day = BondRules.NextPayDay(h);
                next = day > 0 && (next == 0 || day < next) ? day : next;
            }
            box.Line(MarketText.Fill("reallife_mkt_account_bonds", "count", s.Bonds.Holdings.Count.ToString(), "worth", MarketText.Money(worth), "day", next.ToString()));
        }

        // ---------------------------------------------------------------- a property trust on its page

        // What a property trust pays: its year's rate a share, how often, and the next distribution declared.
        private static void TrustLine(InfoBox box, Stock k)
        {
            if (!TrustRules.Is(k))
            {
                return;
            }
            string every = MarketText.T(k.Def.PayDays >= MarketCatalogue.TrustWeekly ? "reallife_mkt_trust_every_week" : "reallife_mkt_trust_every_three");
            if (k.DeclaredCents > 0 && !k.ExDone && k.RecordDay > 0)
            {
                box.Line(MarketText.Fill("reallife_mkt_ticket_trust", "year", MarketText.Price(TrustRules.YearCents(k)), "every", every, "next", MarketText.Price(k.DeclaredCents),
                    "record", k.RecordDay.ToString(), "pay", k.PayDay.ToString()));
            }
            else
            {
                box.Line(MarketText.Fill("reallife_mkt_ticket_trust_none", "year", MarketText.Price(TrustRules.YearCents(k)), "every", every));
            }
        }
    }
}
