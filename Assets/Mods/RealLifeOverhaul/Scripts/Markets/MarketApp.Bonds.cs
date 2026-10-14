using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The Bonds tab of the Stocks app (Markets/Bonds): the bonds on offer today, with their yield, term and payments, a
    /// Treasury ladder (one sum over the Treasury's terms), and the player's own, with their price, what they would bring
    /// and their next payment (the filter shows one or the other). Picking a bond on offer puts what it pays in the box,
    /// with the amount to put in (up to the most of one purchase, by what the player is worth), the share of its interest
    /// to put back into it, whether it rolls over at its end, and the button to buy it; the ladder its pieces at each
    /// term's rate, with the same choices; picking one of the player's puts its record there, with those choices to change
    /// and the button to sell it (or close a deposit). Written when the app opens and when the player does something in
    /// it, never per frame.
    /// </summary>
    public sealed partial class MarketApp
    {
        public const int TabBonds = 7;
        private const string BondOfferRow = "bo:", BondHeldRow = "bd:", BondLadderRow = "bl:";

        // The purchase being written for the offer picked (or the ladder).
        private string _bondFor;
        private long _bondAmount;
        private int _bondReinvest;
        private bool _bondRoll;
        private readonly List<BondOffer> _ladderOffers = new List<BondOffer>(8);   // the Treasury's offers a ladder is made of, shortest first
        private readonly long[] _ladderPieces = new long[8];

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
            WriteLadderRow(offers, year);
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
                row.Set(0, BondText.Name(h) + (h.Rollover ? InvestText.Mark(MarketText.T("reallife_mkt_inv_mark_rolls")) : string.Empty));
                row.Set(1, BondText.KindWords(d));
                row.Set(2, MarketText.Fill("reallife_mkt_day", "day", h.EndDay.ToString()), h.EndDay);
                row.Set(3, BondText.Rate(h.RateBp), h.RateBp);
                row.Set(4, BondText.Pays(d));
                double price = _director.BondPrice(h);
                int move = (int)Math.Round((price - 100.0) * 100.0);
                row.Set(5, d.Kind == BondKind.Deposit ? "-" : BondText.Price(price), price, Tone(move));
                row.Set(6, BondText.Money(h.Face), h.Face / 100.0);
                row.Set(7, BondText.Money(sale), sale / 100.0, Tone(Math.Sign(sale - h.Cost)));
                row.Set(8, day > 0 ? NextWords(h, d, day) : string.Empty, day);
            }
            int offer = Picked(_picked, BondOfferRow);
            int held = Picked(_picked, BondHeldRow);
            BondHolding mine = held != int.MinValue ? s.Bonds.Holding(held) : null;
            if (offer != int.MinValue && offer >= 0 && offer < offers.Count)
            {
                WriteBondOffer(box, s, offers[offer], _picked);
            }
            else if (Picked(_picked, BondLadderRow) != int.MinValue && _ladderOffers.Count > 0)
            {
                WriteLadder(box, s, _picked);
            }
            else if (mine != null)
            {
                WriteBondHeld(box, s, mine);
            }
            else
            {
                box.Title(MarketText.T("reallife_mkt_tab_bonds"));
                StatusLine(box);
                box.Section("reallife_ibox_sec_now");
                box.Line(s.Bonds.Holdings.Count > 0
                    ? MarketText.Fill("reallife_mkt_bond_box_held", "count", s.Bonds.Holdings.Count.ToString(), "worth", MarketText.Money(worth), "day", next.ToString())
                    : MarketText.T("reallife_mkt_bond_box_none"));
                box.Line(MarketText.Fill("reallife_mkt_bond_box_rates", "rate", BondText.Rate(s.Env.RateBp),
                    "year", BondText.Rate(BondRules.YieldBp(BondKind.Treasury, -1, 1.0, s.Env)), "five", BondText.Rate(BondRules.YieldBp(BondKind.Treasury, -1, 5.0, s.Env))));
                box.Line(Settings.MarketsLarge
                    ? MarketText.Fill("reallife_mkt_bond_box_most", "most", MarketText.Money(_director.BondMost), "flat", MarketText.Money(_director.BondFlat),
                        "pct", BondCatalogue.MostWorthPct + "%", "cap", MarketText.Money(_director.BondCap))
                    : MarketText.Fill("reallife_mkt_bond_offer_limits", "least", MarketText.Money(_director.BondLeast), "most", MarketText.Money(_director.BondMost)));
                box.Line(MarketText.T(Settings.MarketsBonds ? "reallife_mkt_bond_box_help" : "reallife_mkt_bond_box_off"));
                Meta.Guide.Point(box, GuideTitle);
            }
            return MarketTables.Bonds;
        }

        // A bond's next payment in words: "120, day 34"; its last with what it repays, or, rolling over, its interest alone
        // and that it rolls over (what it repays goes into a new one, not into the cash).
        private static string NextWords(BondHolding h, BondDef d, int day)
        {
            bool last = h.Paid + 1 >= h.Payments, rolls = last && InvestRules.Rolls(h, Settings.MarketsBonds);
            long cents = BondRules.InterestCents(h, d) + (last && !rolls ? h.Face : 0);
            return MarketText.Fill(rolls ? "reallife_mkt_inv_next_roll" : "reallife_mkt_next_pay", "amount", BondText.Money(cents), "day", day.ToString());
        }

        // A bond on offer: what it pays, the amount to put in, the share of its interest to put back, and the button to buy it.
        private void WriteBondOffer(InfoBox box, MarketState s, BondOffer o, string rowId)
        {
            if (_bondFor != rowId)
            {
                _bondFor = rowId;
                _bondAmount = Math.Max(_director.BondLeast, _bondAmount);
                _bondReinvest = 0;
                _bondRoll = false;
            }
            int year = Math.Max(8, s.YearDays);
            string name = BondText.Name(o.Def, o.Issuer);
            box.Title(name);
            box.Chip(BondText.Rate(o.YieldBp), CellTone.Green);
            StatusLine(box);
            box.Section("reallife_ibox_sec_terms");
            box.Note(MarketText.Fill("reallife_mkt_bond_offer_what", "rate", BondText.Rate(o.YieldBp), "pays", BondText.Pays(o.Def), "term", BondText.Term(o.Def.TermQuarters),
                "day", (s.Day + BondRules.TermDays(o.Def, year)).ToString()));
            box.Line(MarketText.Fill(o.Def.Kind == BondKind.Deposit ? "reallife_mkt_bond_about_deposit" : o.Def.Kind == BondKind.City ? "reallife_mkt_bond_about_city" : "reallife_mkt_bond_about_treasury",
                "bank", o.Issuer ?? string.Empty, "months", BondCatalogue.DepositPenaltyMonths.ToString()));
            box.Note(MarketText.Fill("reallife_mkt_bond_offer_limits", "least", MarketText.Money(_director.BondLeast), "most", MarketText.Money(_director.BondMost)));
            PurchaseChoices(box);
            BondWhy why = _director.CanBuyBond(o, _bondAmount);
            if (why == BondWhy.None)
            {
                long each = _bondAmount * 100L * o.YieldBp / (10000L * Math.Max(1, o.Def.PaysPerYear));
                box.Line(MarketText.Fill("reallife_mkt_bond_offer_pays", "amount", BondText.Money(each), "count", o.Def.Payments.ToString()), CellTone.Green);
            }
            else
            {
                box.Line(BondText.Why(why), CellTone.Red);
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

        // The share of its interest put back and whether it rolls over, for a purchase being written (a bond or the ladder).
        private void PurchaseChoices(InfoBox box)
        {
            box.Selector(BondCatalogue.Reinvest.Length, Math.Max(0, Array.IndexOf(BondCatalogue.Reinvest, _bondReinvest)), ReinvestWords, i => UiClone.Safe(() =>
            {
                _bondReinvest = BondCatalogue.Reinvest[i];
                Refresh();
            }));
            box.Selector(2, _bondRoll ? 1 : 0, i => InvestText.Rollover(i == 1), i => UiClone.Safe(() =>
            {
                _bondRoll = i == 1;
                Refresh();
            }));
        }

        private void ConfirmBond(BondOffer o)
        {
            long amount = _bondAmount;
            int reinvest = _bondReinvest;
            bool roll = _bondRoll;
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
                    ReinvestWords(Math.Max(0, Array.IndexOf(BondCatalogue.Reinvest, reinvest)))) + (roll ? "\n" + MarketText.T("reallife_mkt_bond_confirm_roll") : string.Empty),
                () => UiClone.Safe(() =>
                {
                    BondHolding h = _director.BuyBond(o, amount, reinvest, roll, out BondWhy refused);
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
            box.Chip(BondText.Rate(h.RateBp));
            StatusLine(box);
            box.Note(MarketText.Fill("reallife_mkt_bond_held_what", "face", BondText.Money(h.Face), "rate", BondText.Rate(h.RateBp), "day", h.EndDay.ToString(), "paid", h.Paid.ToString(),
                "count", h.Payments.ToString()));
            int day = BondRules.NextPayDay(h);
            box.Section("reallife_ibox_sec_payments");
            if (day > 0 && d != null)
            {
                bool last = h.Paid + 1 >= h.Payments, rolls = last && InvestRules.Rolls(h, Settings.MarketsBonds);   // (rolling over: what it repays goes into a new one)
                box.Line(MarketText.Fill(rolls ? "reallife_mkt_bond_held_last_roll" : last ? "reallife_mkt_bond_held_last" : "reallife_mkt_bond_held_next", "day", day.ToString(),
                    "amount", BondText.Money(BondRules.InterestCents(h, d)), "face", BondText.Money(h.Face)));
            }
            box.Line(MarketText.Fill("reallife_mkt_bond_held_record", "received", BondText.Money(h.Received), "back", BondText.Money(h.Reinvested), "tax", BondText.Money(h.Taxed)));
            long sale = _director.BondSale(h);
            box.Section("reallife_ibox_sec_selling");
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
            box.Selector(2, h.Rollover ? 1 : 0, i => InvestText.Rollover(i == 1), i => UiClone.Safe(() =>
            {
                if (!_director.SetBondRollover(id, i == 1, out BondWhy why))
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

        // ---------------------------------------------------------------- a ladder

        // The ladder's row among the offers: the Treasury's terms it spreads over (shortest first), their average yield.
        private void WriteLadderRow(List<BondOffer> offers, int year)
        {
            _ladderOffers.Clear();
            for (int i = 0; i < offers.Count; i++)
            {
                if (offers[i].Def != null && offers[i].Def.Kind == BondKind.Treasury)
                {
                    int at = _ladderOffers.Count;
                    while (at > 0 && _ladderOffers[at - 1].Def.TermQuarters > offers[i].Def.TermQuarters)
                    {
                        at--;
                    }
                    _ladderOffers.Insert(at, offers[i]);
                }
            }
            if (_ladderOffers.Count == 0)
            {
                return;
            }
            BondDef first = _ladderOffers[0].Def, last = _ladderOffers[_ladderOffers.Count - 1].Def;
            int average = LadderYield();
            TableRow row = Row(BondLadderRow, 0);
            row.Kind = MarketTables.BondsOffered;
            row.Set(0, MarketText.T("reallife_mkt_bond_ladder"));
            row.Set(1, BondText.KindWords(first));
            row.Set(2, MarketText.Fill("reallife_mkt_bond_ladder_terms", "first", BondText.Term(first.TermQuarters), "last", BondText.Term(last.TermQuarters)), BondRules.TermDays(last, year));
            row.Set(3, BondText.Rate(average), average);
            row.Set(4, MarketText.T("reallife_mkt_bond_ladder_pays"));
            row.Set(5, BondText.Price(100.0), 100.0);
            row.Set(6, string.Empty, double.NaN);
            row.Set(7, string.Empty, double.NaN);
            row.Set(8, string.Empty);
        }

        // The ladder's yield: its terms' yields of today averaged (its pieces are even).
        private int LadderYield()
        {
            long sum = 0;
            for (int i = 0; i < _ladderOffers.Count; i++)
            {
                sum += _ladderOffers[i].YieldBp;
            }
            return _ladderOffers.Count > 0 ? (int)(sum / _ladderOffers.Count) : 0;
        }

        // The ladder picked: what it is, its pieces at each term's rate for the amount, the choices, and the button to buy it.
        private void WriteLadder(InfoBox box, MarketState s, string rowId)
        {
            int n = _ladderOffers.Count;
            if (_bondFor != rowId)
            {
                _bondFor = rowId;
                _bondAmount = Math.Max(_director.BondLeast * n, _bondAmount);
                _bondReinvest = 0;
                _bondRoll = false;
            }
            BondDef first = _ladderOffers[0].Def, last = _ladderOffers[n - 1].Def;
            box.Title(MarketText.T("reallife_mkt_bond_ladder"));
            box.Chip(BondText.Rate(LadderYield()), CellTone.Green);
            StatusLine(box);
            box.Section("reallife_ibox_sec_terms");
            box.Note(MarketText.Fill("reallife_mkt_bond_ladder_about", "count", n.ToString(), "first", BondText.Term(first.TermQuarters), "last", BondText.Term(last.TermQuarters)));
            int pieces = _director.LadderPieces(_bondAmount, _ladderPieces);
            for (int i = 0; i < pieces && i < n; i++)
            {
                box.RowWords(BondText.Term(_ladderOffers[i].Def.TermQuarters), MarketText.Fill("reallife_mkt_bond_ladder_piece", "amount", MarketText.Money(_ladderPieces[i]),
                    "rate", BondText.Rate(_ladderOffers[i].YieldBp)));
            }
            box.Note(MarketText.Fill("reallife_mkt_bond_ladder_limits", "least", MarketText.Money(_director.BondLeast), "most", MarketText.Money(_director.BondMost)));
            PurchaseChoices(box);
            BondWhy why = _director.LadderWhy(_bondAmount);
            if (why != BondWhy.None)
            {
                box.Line(BondText.Why(why), CellTone.Red);
            }
            Transform buttons = box.ButtonRow();
            AskNumber(buttons, InfoBox.Additional, MarketText.Fill("reallife_mkt_bond_amount", "amount", MarketText.Money(_bondAmount)), "reallife_mkt_bond_amount_placeholder", false,
                value => _bondAmount = value);
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_mkt_bond_ladder_buy", () => UiClone.Safe(ConfirmLadder));
            Meta.Guide.Point(box, GuideTitle);
        }

        private void ConfirmLadder()
        {
            long amount = _bondAmount;
            int reinvest = _bondReinvest;
            bool roll = _bondRoll;
            int n = _ladderOffers.Count;
            BondWhy why = n > 0 ? _director.LadderWhy(amount) : BondWhy.NoBank;   // (as it is now, not as it was drawn)
            if (why != BondWhy.None)
            {
                BondError(why);
                return;
            }
            string first = BondText.Term(_ladderOffers[0].Def.TermQuarters), last = BondText.Term(_ladderOffers[n - 1].Def.TermQuarters);
            GameConfirm.Show(
                MarketText.T("reallife_mkt_bond_ladder_confirm"),
                MarketText.Fill("reallife_mkt_bond_ladder_confirm_body", "amount", MarketText.Money(amount), "count", n.ToString(), "first", first, "last", last, "reinvest",
                    ReinvestWords(Math.Max(0, Array.IndexOf(BondCatalogue.Reinvest, reinvest)))) + (roll ? "\n" + MarketText.T("reallife_mkt_bond_ladder_confirm_roll") : string.Empty),
                () => UiClone.Safe(() =>
                {
                    int bought = _director.BuyLadder(amount, reinvest, roll, out BondWhy refused);
                    if (bought <= 0)
                    {
                        BondError(refused);
                    }
                    else
                    {
                        Notifications.Show(NotificationType.Success, "reallife_mkt_bond_note_ladder",
                            new Dictionary<string, string> { { "amount", MarketText.Money(amount) }, { "count", bought.ToString() }, { "first", first }, { "last", last } }, 6f, null, null, true, false);
                        _picked = null;
                    }
                    Refresh();
                }), null, "reallife_mkt_bond_ladder_buy", "common_cancel", false, false);
        }

        // ---------------------------------------------------------------- the account's line of the bonds held

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
