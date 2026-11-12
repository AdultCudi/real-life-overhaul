using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Stocks desk's sheets filled from the market: a bond to buy or hold, a contract to open or close, a firm to hire,
    /// a rule to write. Each figure is read from the state of the sheet and from the same engine the old app asks, so what
    /// the sheet says it costs is what the engine takes. Built when the sheet changes, never per frame.
    /// </summary>
    public static partial class StocksBuild
    {
        // ------------------------------------------------------------------------------------------------ helpers

        /// <summary>The old texts carry the game's rich words; the kit's rows do not read them.</summary>
        public static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0)
            {
                return text ?? string.Empty;
            }
            var sb = new StringBuilder(text.Length);
            bool tag = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<')
                {
                    tag = true;
                }
                else if (c == '>' && tag)
                {
                    tag = false;
                }
                else if (!tag)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static StocksFieldModel StepField(int id, string label, string value, long v, long min, long max, long step, bool typed)
        {
            return new StocksFieldModel
            {
                Kind = StocksFieldKind.Stepper, Id = id, Label = label, Value = value, Min = min, Max = max, Step = step, Typed = typed,
                CanLess = v > min, CanMore = max < min || v < max
            };
        }

        private static StocksFieldModel PickField(int id, string label, string value, int count)
        {
            return new StocksFieldModel { Kind = StocksFieldKind.Choice, Id = id, Label = label, Value = value, Count = count, Off = count < 2 };
        }

        private static StocksFieldModel SwitchField(int id, string label, bool on, UiWords w)
        {
            return new StocksFieldModel { Kind = StocksFieldKind.Switch, Id = id, Label = label, On = on, Value = on ? w("reallife_stk_sh_on", "On") : w("reallife_stk_sh_off", "Off") };
        }

        private static StocksFieldModel Fact(string label, string value, int tone = 0)
        {
            return new StocksFieldModel { Kind = StocksFieldKind.Fact, Label = label, Value = value, Tone = tone };
        }

        private static StocksFieldModel Note(string text, int tone = 0)
        {
            return new StocksFieldModel { Kind = StocksFieldKind.Note, Value = text, Tone = tone };
        }

        private static StocksFieldModel Head(string text)
        {
            return new StocksFieldModel { Kind = StocksFieldKind.Head, Label = text };
        }

        private static UiActionModel Extra(string label, bool danger = false, bool off = false)
        {
            return new UiActionModel { Label = label, Kind = danger ? UiButtonKind.Destructive : UiButtonKind.Secondary, Key = UiAction.Count, Disabled = off };
        }

        private static UiActionModel Armed(UiActionModel a, bool on)
        {
            if (on)
            {
                a.Kind = UiButtonKind.Armed;
            }
            return a;
        }

        private static StocksSheetModel Frame(UiWords w, string kicker, string title, UiIcon icon)
        {
            return new StocksSheetModel { Kicker = kicker, Title = title, Emblem = icon, Back = w("reallife_ui_prompt_back", "Back") };
        }

        private static string Money(long amount) => MarketText.Money(amount);

        private static long Nice(long value, long least)
        {
            long step = 1;
            while (step * 10 <= value)
            {
                step *= 10;
            }
            return Math.Max(least, step);
        }

        // ------------------------------------------------------------------------------------------------ bonds

        /// <summary>The Treasury's offers shortest first: the ladder's pieces, as the old app orders them.</summary>
        public static void LadderOffers(List<BondOffer> offers, List<BondOffer> into) => StocksMath.LadderOffers(offers, into);

        /// <summary>The average of the ladder's yields, as its pieces are even.</summary>
        public static int LadderYield(List<BondOffer> ladder) => StocksMath.LadderYield(ladder);

        public static string ReinvestWords(int pct)
        {
            return pct <= 0 ? MarketText.T("reallife_mkt_bond_set_reinvest_none") : MarketText.Fill("reallife_mkt_bond_set_reinvest", "pct", pct.ToString());
        }

        private static void BondChoices(StocksSheetState st, UiWords w, List<StocksFieldModel> f)
        {
            int at = (int)Math.Max(0, Math.Min(BondCatalogue.Reinvest.Length - 1, st.V[StocksSheets.Reinvest]));
            f.Add(PickField(StocksSheets.Reinvest, w("reallife_stk_sh_putback", "Interest"), ReinvestWords(BondCatalogue.Reinvest[at]), BondCatalogue.Reinvest.Length));
            f.Add(SwitchField(StocksSheets.Roll, w("reallife_stk_sh_roll", "At its end"), st.V[StocksSheets.Roll] != 0, w));
            f[f.Count - 1] = SwitchWord(f[f.Count - 1], InvestText.Rollover(st.V[StocksSheets.Roll] != 0));
        }

        private static StocksFieldModel SwitchWord(StocksFieldModel f, string value)
        {
            f.Value = value;
            return f;
        }

        public static StocksSheetModel BondBuy(MarketDirector d, StocksSheetState st, UiWords w)
        {
            MarketState s = d.State;
            List<BondOffer> offers = d.BondOffers();
            bool ladder = st.Kind == StocksSheets.Ladder;
            var pieces = new List<BondOffer>(8);
            LadderOffers(offers, pieces);
            long least = d.BondLeast, most = d.BondMost;
            int year = Math.Max(8, s.YearDays);
            var fields = new List<StocksFieldModel>(10);
            StocksSheetModel m;
            long amount = st.V[StocksSheets.Amount];
            BondWhy why;
            if (ladder)
            {
                m = Frame(w, w("reallife_mkt_tab_bonds", "Bonds"), w("reallife_mkt_bond_ladder", "Treasury ladder"), UiIcon.Bank);
                m.Name = MarketText.T("reallife_mkt_bond_ladder");
                m.Big = BondText.Rate(LadderYield(pieces));
                m.Sub = pieces.Count > 0 ? MarketText.Fill("reallife_mkt_bond_ladder_terms", "first", BondText.Term(pieces[0].Def.TermQuarters),
                    "last", BondText.Term(pieces[pieces.Count - 1].Def.TermQuarters)) : string.Empty;
                m.About = pieces.Count > 0 ? Plain(MarketText.Fill("reallife_mkt_bond_ladder_about", "count", pieces.Count.ToString(), "first", BondText.Term(pieces[0].Def.TermQuarters),
                    "last", BondText.Term(pieces[pieces.Count - 1].Def.TermQuarters))) : string.Empty;
                why = pieces.Count > 0 ? d.LadderWhy(amount) : BondWhy.NoBank;
                var each = new long[8];
                int n = d.LadderPieces(amount, each);
                m.SideLabels = new string[Math.Min(n, pieces.Count)];
                m.SideValues = new string[m.SideLabels.Length];
                for (int i = 0; i < m.SideLabels.Length; i++)
                {
                    m.SideLabels[i] = BondText.Term(pieces[i].Def.TermQuarters);
                    m.SideValues[i] = MarketText.Fill("reallife_mkt_bond_ladder_piece", "amount", Money(each[i]), "rate", BondText.Rate(pieces[i].YieldBp));
                }
            }
            else
            {
                BondOffer o = st.Key >= 0 && st.Key < offers.Count ? offers[st.Key] : default(BondOffer);
                string name = BondText.Name(o.Def, o.Issuer);
                m = Frame(w, w("reallife_mkt_tab_bonds", "Bonds"), w("reallife_stk_sh_buy_bond", "Buy a bond"), UiIcon.Bank);
                m.Name = name;
                m.Big = BondText.Rate(o.YieldBp);
                m.Sub = BondText.KindWords(o.Def);
                m.SideLabels = new[]
                {
                    w("reallife_stk_sh_term", "Term"), w("reallife_stk_sh_pays", "Pays"), w("reallife_mkt_col_price", "Price"), w("reallife_stk_sh_ends", "Ends on day"),
                    w("reallife_stk_sh_least", "Least"), w("reallife_stk_sh_most", "Most")
                };
                m.SideValues = new[]
                {
                    BondText.Term(o.Def != null ? o.Def.TermQuarters : 0), BondText.Pays(o.Def), BondText.Price(100.0),
                    (s.Day + (o.Def != null ? BondRules.TermDays(o.Def, year) : 0)).ToString(), Money(least), Money(most)
                };
                string about = o.Def != null && o.Def.Kind == BondKind.Deposit ? "reallife_mkt_bond_about_deposit"
                    : o.Def != null && o.Def.Kind == BondKind.City ? "reallife_mkt_bond_about_city" : "reallife_mkt_bond_about_treasury";
                m.About = Plain(MarketText.Fill(about, "bank", o.Issuer ?? string.Empty, "months", BondCatalogue.DepositPenaltyMonths.ToString()));
                why = o.Def != null ? d.CanBuyBond(o, amount) : BondWhy.NoBank;
            }
            long step = Nice(Math.Max(1, most) / 20, Math.Max(1, least));
            long floor = least * (ladder ? Math.Max(1, pieces.Count) : 1);
            fields.Add(StepField(StocksSheets.Amount, w("reallife_stk_sh_amount", "Amount"), Money(amount), amount, floor, Math.Max(floor,
                most * (ladder ? Math.Max(1, pieces.Count) : 1)), step, true));
            BondChoices(st, w, fields);
            if (why == BondWhy.None && !ladder)
            {
                BondOffer o = st.Key >= 0 && st.Key < offers.Count ? offers[st.Key] : default(BondOffer);
                long each = amount * 100L * o.YieldBp / (10000L * Math.Max(1, o.Def.PaysPerYear));
                fields.Add(Fact(w("reallife_stk_sh_each", "Each payment"), MarketText.Fill("reallife_stk_sh_each_of", "amount", BondText.Money(each),
                    "count", o.Def.Payments.ToString()), 1));
            }
            m.Fields = fields.ToArray();
            m.Reason = why == BondWhy.None ? null : BondText.Why(why);
            m.Confirm = ladder ? w("reallife_mkt_bond_ladder_buy", "Buy the ladder") : w("reallife_mkt_bond_buy", "Buy");
            m.ConfirmOff = why != BondWhy.None;
            return m;
        }

        public static StocksSheetModel BondHeld(MarketDirector d, StocksSheetState st, UiWords w)
        {
            MarketState s = d.State;
            BondHolding h = s.Bonds.Holding(st.Key);
            BondDef def = h != null ? BondCatalogue.Find(h.Def) : null;
            StocksSheetModel m = Frame(w, w("reallife_mkt_tab_bonds", "Bonds"), w("reallife_stk_sh_bond", "Your bond"), UiIcon.Bank);
            if (h == null || def == null)
            {
                m.Name = w("reallife_stk_none", "None");
                m.Reason = BondText.Why(BondWhy.NotHeld);
                return m;
            }
            long sale = d.BondSale(h);
            bool deposit = def.Kind == BondKind.Deposit;
            int day = BondRules.NextPayDay(h);
            m.Name = BondText.Name(h);
            m.Big = Money(sale / 100);
            m.Sub = BondText.Rate(h.RateBp) + "  " + BondText.KindWords(def);
            m.SideLabels = new[]
            {
                w("reallife_stk_sh_face", "Face"), w("reallife_stk_sh_ends", "Ends on day"), w("reallife_stk_sh_paid", "Payments made"), w("reallife_stk_sh_received", "Paid to you"),
                w("reallife_stk_sh_tax", "Tax withheld"), w("reallife_stk_sh_next", "Next payment")
            };
            m.SideValues = new[]
            {
                BondText.Money(h.Face), h.EndDay.ToString(), h.Paid + " / " + h.Payments, BondText.Money(h.Received), BondText.Money(h.Taxed),
                day > 0 ? MarketText.Fill("reallife_mkt_next_pay", "amount", BondText.Money(BondRules.InterestCents(h, def)), "day", day.ToString()) : "-"
            };
            m.About = deposit ? Plain(MarketText.Fill("reallife_mkt_bond_held_close", "worth", BondText.Money(sale), "months", BondCatalogue.DepositPenaltyMonths.ToString()))
                : Plain(MarketText.Fill("reallife_mkt_bond_held_price", "price", BondText.Price(d.BondPrice(h)), "worth", BondText.Money(sale), "gain", BondText.Money(sale - h.Cost)));
            var fields = new List<StocksFieldModel>(3);
            BondChoices(st, w, fields);
            m.Fields = fields.ToArray();
            m.Confirm = null;
            UiActionModel sell = Extra(w(deposit ? "reallife_mkt_bond_close" : "reallife_mkt_bond_sell", deposit ? "Close it" : "Sell"), true);
            m.Extras = new[] { Armed(sell, st.Armed == 0) };
            m.Reason = st.Armed == 0 ? Asked(MarketText.Fill(deposit ? "reallife_mkt_bond_confirm_close" : "reallife_mkt_bond_confirm_sell", "name", BondText.Name(h)),
                MarketText.Fill("reallife_mkt_bond_confirm_sell_body", "amount", BondText.Money(sale))) : null;
            return m;
        }

        // ------------------------------------------------------------------------------------------------ futures and options

        private static readonly string[] MaterialWhat = { "reallife_mkt_drv_what_buy", "reallife_mkt_drv_what_sell", "reallife_mkt_drv_what_call", "reallife_mkt_drv_what_put" };
        private static readonly string[] ShareWhat = { "reallife_mkt_drv_what_call", "reallife_mkt_drv_what_put" };

        /// <summary>The kind a material sheet is set to: its four choices are a long future, a short, a call and a put.</summary>
        public static ContractKind MaterialKind(long what) => (ContractKind)(int)Math.Max(0, Math.Min(3, what));

        public static int StrikeStep(long at) => DerivCatalogue.StrikeStepsBp[(int)Math.Max(0, Math.Min(DerivCatalogue.StrikeStepsBp.Length - 1, at))];

        private static string StrikeWords(long priceMicros, int stepBp)
        {
            string price = DerivText.Unit(DerivRules.Strike(priceMicros, stepBp));
            return stepBp == 0 ? MarketText.Fill("reallife_mkt_drv_strike_at", "price", price)
                : MarketText.Fill(stepBp < 0 ? "reallife_mkt_drv_strike_under" : "reallife_mkt_drv_strike_over", "pct", MarketText.Percent(Math.Abs(stepBp)), "price", price);
        }

        public static StocksSheetModel Material(MarketDirector d, StocksSheetState st, UiWords w, int[] settleDays, List<string> placeKeys, List<string> placeNames)
        {
            MarketState s = d.State;
            RawMaterial mat = st.Key >= 0 && st.Key < s.Materials.Count ? s.Materials[st.Key] : null;
            StocksSheetModel m = Frame(w, w("reallife_mkt_tab_futures", "Futures"), w("reallife_stk_sh_open_contract", "Open a contract"), UiIcon.Chart);
            if (mat == null)
            {
                m.Name = w("reallife_stk_none", "None");
                return m;
            }
            int days = d.ContractDays(settleDays);
            m.Name = MaterialGame.Name(mat.Name);
            m.Big = DerivText.Unit(mat.Fix);
            m.Sub = DerivText.Group(mat.Group) + "  " + MarketText.Change(MaterialRules.MoveBp(mat, 1));
            m.SideLabels = new string[days];
            m.SideValues = new string[days];
            for (int i = 0; i < days; i++)
            {
                DerivQuote f = d.QuoteFuture(mat.Name, settleDays[i], true, 1);
                m.SideLabels[i] = MarketText.Fill("reallife_mkt_drv_mat_week", "day", DerivRules.WeekFrom(settleDays[i]).ToString());
                m.SideValues[i] = DerivText.Unit(f.Fair);
            }
            m.About = d.Uses(mat.Name) ? MarketText.T("reallife_mkt_drv_mat_uses") : null;
            if (days == 0)
            {
                m.Reason = DerivText.Why(DerivWhy.Closed);
                return m;
            }
            int what = (int)Math.Max(0, Math.Min(3, st.V[StocksSheets.What]));
            int week = (int)Math.Max(0, Math.Min(days - 1, st.V[StocksSheets.Week]));
            int qi = (int)Math.Max(0, Math.Min(DerivCatalogue.MaterialQuantities.Length - 1, st.V[StocksSheets.Qty]));
            long qty = DerivCatalogue.MaterialQuantities[qi];
            int settle = settleDays[week];
            ContractKind kind = MaterialKind(what);
            bool option = what >= 2;
            int stepBp = StrikeStep(st.V[StocksSheets.Strike]);
            DerivQuote q = option ? d.QuoteOption(kind == ContractKind.Call, mat.Name, 0, settle, stepBp, qty) : d.QuoteFuture(mat.Name, settle, kind == ContractKind.Long, qty);
            var fields = new List<StocksFieldModel>(10);
            fields.Add(PickField(StocksSheets.What, w("reallife_stk_sh_what", "What"), MarketText.T(MaterialWhat[what]), MaterialWhat.Length));
            fields.Add(PickField(StocksSheets.Week, w("reallife_stk_sh_week", "Settles"), MarketText.Fill("reallife_mkt_drv_week", "day",
                settle.ToString(), "from", DerivRules.WeekFrom(settle).ToString()), days));
            if (option)
            {
                fields.Add(PickField(StocksSheets.Strike, w("reallife_stk_sh_strike", "Strike"), StrikeWords(q.Forward, stepBp), DerivCatalogue.StrikeStepsBp.Length));
            }
            fields.Add(PickField(StocksSheets.Qty, w("reallife_stk_sh_units", "Size"), MarketText.Fill("reallife_mkt_drv_qty", "count",
                qty.ToString("N0")), DerivCatalogue.MaterialQuantities.Length));
            string placeName = null;
            if (kind == ContractKind.Long && !DerivCatalogue.IsBuilding(mat.Name))
            {
                d.Places(placeKeys, placeNames);
                int pl = (int)Math.Max(0, Math.Min(placeKeys.Count, st.V[StocksSheets.Place]));
                fields.Add(PickField(StocksSheets.Place, w("reallife_stk_sh_place", "Goods"),
                    pl == 0 ? MarketText.T("reallife_mkt_drv_place_cash") : MarketText.Fill("reallife_mkt_drv_place", "place", placeNames[pl - 1]), placeKeys.Count + 1));
                placeName = pl > 0 ? placeNames[pl - 1] : null;
            }
            Quote(fields, kind, q, qty, settle, placeName, w);
            DerivWhy why = d.CanBuy(kind, mat.Name, 0, settle, qty, q);
            m.Fields = fields.ToArray();
            m.Reason = why == DerivWhy.None ? null : DerivText.Why(why);
            m.Confirm = w(kind == ContractKind.Short ? "reallife_mkt_drv_sell" : "reallife_mkt_drv_buy", kind == ContractKind.Short ? "Sell" : "Buy");
            m.ConfirmOff = why != DerivWhy.None;
            return m;
        }

        public static StocksSheetModel ShareOption(MarketDirector d, StocksSheetState st, UiWords w, int[] settleDays)
        {
            MarketState s = d.State;
            Stock k = s.Stock(st.Key);
            StocksSheetModel m = Frame(w, w("reallife_mkt_tab_futures", "Futures"), w("reallife_stk_sh_open_option", "Open an option"), UiIcon.Chart);
            if (k == null)
            {
                m.Name = w("reallife_stk_none", "None");
                return m;
            }
            int days = d.ContractDays(settleDays);
            m.Name = k.Name;
            m.Big = MarketText.Price(k.Last);
            m.Sub = k.Ticker + "  " + MarketText.Change(MarketHistory.DayMoveBp(k));
            m.SideLabels = new[] { w("reallife_mkt_col_sector", "Sector"), w("reallife_mkt_col_today", "Today") };
            m.SideValues = new[] { MarketText.Sector(k.Sector), MarketText.Change(MarketHistory.DayMoveBp(k)) };
            if (days == 0)
            {
                m.Reason = DerivText.Why(DerivWhy.Closed);
                return m;
            }
            int what = (int)Math.Max(0, Math.Min(1, st.V[StocksSheets.What]));
            int week = (int)Math.Max(0, Math.Min(days - 1, st.V[StocksSheets.Week]));
            int qi = (int)Math.Max(0, Math.Min(DerivCatalogue.ShareQuantities.Length - 1, st.V[StocksSheets.Qty]));
            long qty = DerivCatalogue.ShareQuantities[qi];
            int settle = settleDays[week];
            ContractKind kind = what == 0 ? ContractKind.Call : ContractKind.Put;
            int stepBp = StrikeStep(st.V[StocksSheets.Strike]);
            DerivQuote q = d.QuoteOption(kind == ContractKind.Call, null, k.Id, settle, stepBp, qty);
            var fields = new List<StocksFieldModel>(8);
            fields.Add(PickField(StocksSheets.What, w("reallife_stk_sh_what", "What"), MarketText.T(ShareWhat[what]), ShareWhat.Length));
            fields.Add(PickField(StocksSheets.Week, w("reallife_stk_sh_week", "Settles"), MarketText.Fill("reallife_mkt_drv_week", "day",
                settle.ToString(), "from", DerivRules.WeekFrom(settle).ToString()), days));
            fields.Add(PickField(StocksSheets.Strike, w("reallife_stk_sh_strike", "Strike"),
                StrikeWords((long)Math.Round(k.Mid * DerivCatalogue.Micro), stepBp), DerivCatalogue.StrikeStepsBp.Length));
            fields.Add(PickField(StocksSheets.Qty, w("reallife_stk_sh_units", "Size"), MarketText.Fill("reallife_mkt_drv_qty_shares", "count",
                qty.ToString("N0")), DerivCatalogue.ShareQuantities.Length));
            Quote(fields, kind, q, qty, settle, null, w);
            fields.Add(Note(MarketText.Fill("reallife_mkt_drv_share_settle", "day", settle.ToString(), "minutes", (DerivCatalogue.SettleSteps * MarketCatalogue.StepMinutes).ToString())));
            DerivWhy why = d.CanBuy(kind, null, k.Id, settle, qty, q);
            m.Fields = fields.ToArray();
            m.Reason = why == DerivWhy.None ? null : DerivText.Why(why);
            m.Confirm = w("reallife_mkt_drv_buy", "Buy");
            m.ConfirmOff = why != DerivWhy.None;
            return m;
        }

        // What a ticket would buy: its price, what it takes from the cash and what it does at its day, as rows.
        private static void Quote(List<StocksFieldModel> f, ContractKind kind, in DerivQuote q, long qty, int settle, string place, UiWords w)
        {
            if (q.Why != DerivWhy.None && q.Fair <= 0)
            {
                return;
            }
            f.Add(Head(w("reallife_stk_sh_quote", "Quote")));
            if (kind == ContractKind.Long || kind == ContractKind.Short)
            {
                long price = kind == ContractKind.Long ? q.Ask : q.Bid;
                f.Add(Fact(w("reallife_mkt_col_price", "Price"), DerivText.Unit(price)));
                f.Add(Fact(w("reallife_stk_sh_value", "Worth"), Money(q.Value)));
                f.Add(Fact(w("reallife_stk_sh_margin", "Held as margin"), Money(q.Margin)));
                f.Add(Fact(w("reallife_stk_sh_fee", "Commission"), Money(q.Fee)));
                f.Add(Note(GoingPublic.InSession && kind == ContractKind.Long ? MarketText.T("reallife_mkt_drv_quote_session")
                    : place != null ? MarketText.Fill("reallife_mkt_drv_quote_deliver", "day", settle.ToString(), "place", place, "price", DerivText.Unit(price))
                    : MarketText.Fill("reallife_mkt_drv_quote_cash", "day", settle.ToString(), "price", DerivText.Unit(price))));
            }
            else
            {
                f.Add(Fact(w("reallife_stk_sh_premium", "Premium a unit"), DerivText.Unit(q.Ask)));
                f.Add(Fact(w("reallife_stk_sh_cost", "You pay"), Money(q.Value)));
                f.Add(Fact(w("reallife_stk_sh_strike", "Strike"), DerivText.Unit(q.Strike)));
                f.Add(Fact(w("reallife_stk_sh_fee", "Commission"), Money(q.Fee)));
            }
        }

        public static StocksSheetModel Contract(MarketDirector d, StocksSheetState st, UiWords w, long worth, long made)
        {
            DerivContract c = d.State.Derivs.Contract(st.Key);
            StocksSheetModel m = Frame(w, w("reallife_mkt_tab_futures", "Futures"), w("reallife_stk_sh_contract", "Your contract"), UiIcon.Chart);
            if (c == null)
            {
                m.Name = w("reallife_stk_none", "None");
                m.Reason = DerivText.Why(DerivWhy.NotHeld);
                return m;
            }
            m.Name = DerivText.Name(c);
            m.Big = c.IsOpen ? Money(worth) : DerivText.Signed(c.Result / 100);
            m.Sub = c.IsOpen ? DerivText.Kind(c.Kind) : DerivText.End(c.End);
            var side = new List<string>(8);
            var vals = new List<string>(8);
            side.Add(w("reallife_stk_sh_qty", "Size")); vals.Add(c.Qty.ToString("N0"));
            side.Add(w("reallife_stk_sh_strike_price", "Agreed price")); vals.Add(DerivText.Unit(c.Price));
            side.Add(w("reallife_stk_sh_settles", "Settles on day")); vals.Add(c.SettleDay.ToString());
            if (c.IsOpen)
            {
                side.Add(w("reallife_stk_sh_made", "Made so far")); vals.Add(DerivText.Signed(made));
                if (c.IsFuture)
                {
                    side.Add(w("reallife_stk_sh_margin", "Held as margin")); vals.Add(Money(c.Margin / 100));
                    side.Add(w("reallife_stk_sh_mark", "At the last close")); vals.Add(DerivText.Unit(c.Mark));
                }
                else
                {
                    side.Add(w("reallife_stk_sh_premium", "Premium a unit")); vals.Add(DerivText.Unit(c.Premium));
                    side.Add(w("reallife_stk_sh_bid", "Sells back at")); vals.Add(DerivText.Unit(d.ClosePrice(c)));
                }
            }
            else
            {
                side.Add(w("reallife_stk_sh_ended", "Ended on day")); vals.Add(c.EndDay.ToString());
                side.Add(w("reallife_stk_sh_end_price", "At")); vals.Add(DerivText.Unit(c.EndPrice));
            }
            m.SideLabels = side.ToArray();
            m.SideValues = vals.ToArray();
            m.About = c.IsOpen ? (c.IsFuture ? (GoingPublic.InSession ? MarketText.T("reallife_mkt_drv_quote_session") : c.Warehouse.Length > 0
                ? MarketText.Fill("reallife_mkt_drv_quote_deliver", "day", c.SettleDay.ToString(), "place", c.PlaceName, "price", DerivText.Unit(c.Price))
                : MarketText.Fill("reallife_mkt_drv_quote_cash", "day", c.SettleDay.ToString(), "price", DerivText.Unit(c.Price))) : null)
                : (c.Delivered > 0 ? MarketText.Fill("reallife_mkt_drv_held_delivered", "count", c.Delivered.ToString("N0"), "place", DerivText.Place(c)) : null);
            if (c.IsOpen)
            {
                UiActionModel close = Extra(w(c.IsFuture ? "reallife_mkt_drv_close" : "reallife_mkt_drv_sell_back", c.IsFuture ? "Close it" : "Sell it back"), true);
                m.Extras = new[] { Armed(close, st.Armed == 0) };
                m.Reason = st.Armed == 0 ? Asked(MarketText.Fill("reallife_mkt_drv_confirm_close", "name", DerivText.Name(c)),
                    MarketText.Fill("reallife_mkt_drv_confirm_close_body", "price", DerivText.Unit(d.ClosePrice(c)), "result", DerivText.Signed(made))) : null;
            }
            return m;
        }

        // ------------------------------------------------------------------------------------------------ hired firms

        public static readonly string[] ScopeKeys = { "reallife_mkt_bot_scope_market", "reallife_mkt_bot_scope_picked", "reallife_mkt_bot_scope_held" };
        public static readonly string[] EveryKeys = { "reallife_mkt_bot_every_day", "reallife_mkt_bot_every_week", "reallife_mkt_bot_every_month" };
        public static readonly string[] StatusKeys = { "reallife_mkt_bot_status_running", "reallife_mkt_bot_status_stopped", "reallife_mkt_bot_status_selling" };
        public static readonly string[] StrategyKeys =
        {
            "reallife_mkt_bot_strategy_trend", "reallife_mkt_bot_strategy_reversion", "reallife_mkt_bot_strategy_momentum", "reallife_mkt_bot_strategy_income"
        };

        /// <summary>The companies a firm may be told to trade, in the order the old picker keeps: the player's own, its picks, holdings, the watch list.</summary>
        public static void PickCandidates(MarketState s, MarketEngine engine, List<int> picks, List<int> into)
        {
            into.Clear();
            Stock own = engine != null ? engine.MineListed() : null;
            AddCandidate(s, own != null ? own.Id : 0, into);
            for (int i = 0; i < picks.Count; i++)
            {
                AddCandidate(s, picks[i], into);
            }
            for (int i = 0; i < s.Positions.Count; i++)
            {
                if (s.Positions[i].Shares > 0)
                {
                    AddCandidate(s, s.Positions[i].StockId, into);
                }
            }
            for (int i = 0; i < s.Bots.Count; i++)
            {
                for (int j = 0; j < s.Bots[i].Holdings.Count; j++)
                {
                    AddCandidate(s, s.Bots[i].Holdings[j].StockId, into);
                }
            }
            for (int i = 0; i < s.Watch.Count; i++)
            {
                AddCandidate(s, s.Watch[i], into);
            }
        }

        private static void AddCandidate(MarketState s, int id, List<int> into)
        {
            Stock k = id > 0 ? s.Stock(id) : null;
            if (k != null && k.Listed && (k.Own == null || (MarketBots.Mine(s, k) && !k.Own.Firm)) && !into.Contains(id))
            {
                into.Add(id);
            }
        }

        public static string PayWords(int pct, BotPayEvery every)
        {
            return pct <= 0 ? MarketText.T("reallife_mkt_bot_set_pay_none") : MarketText.Fill("reallife_mkt_bot_plan_pays", "pct", pct.ToString(),
                "every", MarketText.T(EveryKeys[(int)every]));
        }

        public static StocksSheetModel Firm(MarketDirector d, StocksSheetState st, UiWords w, List<int> picks, List<int> candidates)
        {
            MarketState s = d.State;
            BotFirm f = MarketBots.Firm(st.Key);
            StocksSheetModel m = Frame(w, w("reallife_mkt_tab_bots", "Bots"), w("reallife_stk_sh_firm", "Trading firm"), UiIcon.Cpu);
            if (f == null)
            {
                m.Name = w("reallife_stk_none", "None");
                return m;
            }
            Bot b = MarketBots.Hired(s, f.Id);
            m.Name = f.Name;
            m.Sub = MarketText.T(StrategyKeys[(int)f.Strategy]);
            var side = new List<string>(8);
            var vals = new List<string>(8);
            side.Add(w("reallife_stk_sh_fee_month", "Fee a month")); vals.Add(MarketText.Percent(f.FeeBp));
            side.Add(w("reallife_stk_sh_profit_share", "Share of profit")); vals.Add(MarketText.Percent(f.ShareBp));
            if (b == null)
            {
                m.Big = MarketText.T("reallife_mkt_bot_status_free");
                side.Add(w("reallife_stk_sh_fee_least", "Fee at least")); vals.Add(Money(Prices.At(MarketBots.FeeLeastAtStart)));
                side.Add(w("reallife_stk_sh_capital_least", "Capital at least")); vals.Add(Money(d.BotCapitalLeast));
                m.About = Plain(About(f));
            }
            else
            {
                long now = MarketBots.Worth(s, b, s.Phase);
                int pct = b.Capital > 0 ? (int)Math.Max(-1000000L, Math.Min(1000000L, (now + b.PaidOut - b.Capital) * 10000L / b.Capital)) : 0;
                m.Big = Money(now);
                m.Sub += "  " + MarketText.T(StatusKeys[(int)b.Status]) + "  " + MarketText.Change(pct);
                side.Add(w("reallife_mkt_bot_col_capital", "Capital")); vals.Add(Money(b.Capital));
                side.Add(w("reallife_mkt_bot_col_trades", "Trades")); vals.Add(b.Trades + " / " + b.Wins + " / " + b.Closed);
                side.Add(w("reallife_mkt_bot_col_fall", "Deepest fall")); vals.Add(MarketText.Percent(b.DrawdownBp));
                side.Add(w("reallife_stk_sh_costs", "Fees and commission")); vals.Add(Money(b.Fees + b.Commission));
                side.Add(w("reallife_stk_sh_paid_out", "Paid to you")); vals.Add(Money(b.PaidOut));
                m.About = Plain(MarketText.Fill("reallife_mkt_bot_box_floor", "floor", Money(MarketBots.Floor(b)), "holdings", b.Holdings.Count.ToString()));
            }
            m.SideLabels = side.ToArray();
            m.SideValues = vals.ToArray();
            var fields = new List<StocksFieldModel>(12);
            long least = Math.Max(1, d.BotCapitalLeast);
            long cash = d.Cash;
            if (b == null)
            {
                long capital = st.V[StocksSheets.Capital];
                fields.Add(StepField(StocksSheets.Capital, w("reallife_mkt_bot_col_capital", "Capital"), Money(capital), capital, least,
                    Math.Max(least, cash), Nice(Math.Max(least, cash) / 20, least), true));
            }
            fields.Add(PickField(StocksSheets.Size, w("reallife_stk_sh_size", "Largest holding"), MarketText.Fill("reallife_mkt_bot_set_size", "pct",
                MarketBots.Sizes[Idx(st.V[StocksSheets.Size], MarketBots.Sizes.Length)].ToString()), MarketBots.Sizes.Length));
            int stopV = MarketBots.Stops[Idx(st.V[StocksSheets.Stop], MarketBots.Stops.Length)];
            fields.Add(PickField(StocksSheets.Stop, w("reallife_stk_sh_stop", "Stop"),
                stopV == 0 ? MarketText.T("reallife_mkt_bot_set_stop_none") : MarketText.Fill("reallife_mkt_bot_set_stop", "pct", stopV.ToString()), MarketBots.Stops.Length));
            fields.Add(PickField(StocksSheets.Loss, w("reallife_stk_sh_loss", "Most it may lose"), MarketText.Fill("reallife_mkt_bot_set_loss", "pct",
                MarketBots.Losses[Idx(st.V[StocksSheets.Loss], MarketBots.Losses.Length)].ToString()), MarketBots.Losses.Length));
            int scope = Idx(st.V[StocksSheets.Scope], 3);
            fields.Add(PickField(StocksSheets.Scope, w("reallife_stk_sh_scope", "Trades"), MarketText.T(ScopeKeys[scope]), 3));
            if (scope == (int)BotScope.Picked)
            {
                if (candidates.Count == 0)
                {
                    fields.Add(Note(MarketText.T("reallife_mkt_bot_pick_none")));
                }
                else
                {
                    int at = Idx(st.V[StocksSheets.PickAt], candidates.Count);
                    Stock k = s.Stock(candidates[at]);
                    string name = k == null ? string.Empty : MarketBots.Mine(s, k) ? MarketText.Fill("reallife_mkt_bot_pick_yours", "name", k.Name) : k.Name;
                    fields.Add(PickField(StocksSheets.PickAt, w("reallife_stk_sh_company", "Company"), name, candidates.Count));
                    fields.Add(SwitchField(StocksSheets.PickOn, w("reallife_stk_sh_trade_it", "Trade it"), picks.Contains(candidates[at]), w));
                }
            }
            int payV = MarketBots.PayPcts[Idx(st.V[StocksSheets.PayPct], MarketBots.PayPcts.Length)];
            fields.Add(PickField(StocksSheets.PayPct, w("reallife_stk_sh_payout", "Payout"),
                payV == 0 ? MarketText.T("reallife_mkt_bot_set_pay_none") : MarketText.Fill("reallife_mkt_bot_set_pay", "pct", payV.ToString()), MarketBots.PayPcts.Length));
            fields.Add(PickField(StocksSheets.Every, w("reallife_stk_sh_every", "Paid"), MarketText.T(EveryKeys[Idx(st.V[StocksSheets.Every], 3)]), 3));
            if (b != null && b.Status == BotStatus.Running)
            {
                long most = Math.Max(0, d.BotMostOut(f.Id));
                long ch = st.V[StocksSheets.Change];
                fields.Add(Note(Plain(MarketText.Fill("reallife_mkt_bot_box_capital", "cash", Money(Math.Max(0L, b.Cash)), "most", Money(most)))));
                fields.Add(StepField(StocksSheets.Change, w("reallife_stk_sh_change", "Add or take out"), Money(ch), ch, Math.Max(1,
                    d.BotChangeLeast), Math.Max(Math.Max(1, d.BotChangeLeast), cash), Nice(Math.Max(1, cash) / 20, Math.Max(1, d.BotChangeLeast)), true));
            }
            if (b == null)
            {
                int sizeV = MarketBots.Sizes[Idx(st.V[StocksSheets.Size], MarketBots.Sizes.Length)], lossV = MarketBots.Losses[Idx(st.V[StocksSheets.Loss], MarketBots.Losses.Length)];
                fields.Add(Note(Asked(MarketText.Fill("reallife_mkt_bot_confirm_hire", "firm", f.Name), MarketText.Fill("reallife_mkt_bot_confirm_hire_body", "firm", f.Name,
                    "capital", Money(st.V[StocksSheets.Capital]), "size", sizeV.ToString(), "loss", lossV.ToString(), "fee", MarketText.Percent(f.FeeBp), "share", MarketText.Percent(f.ShareBp)))));
            }
            m.Fields = fields.ToArray();
            if (b == null)
            {
                m.Confirm = w("reallife_mkt_bot_hire", "Hire");
                m.Reason = d.Trading ? null : MarketText.T(BotWhyKeys[(int)BotWhy.Closed]);
                m.ConfirmOff = !d.Trading;
            }
            else
            {
                // The buttons as FirmExtra reads them. Adding or taking out capital and letting the firm go ask first, as the old app's question box did:
                // the first press arms the button and the sheet says what it comes to, the second does it.
                var extras = new List<UiActionModel>(4);
                var codes = new List<string>(4);
                if (b.Status == BotStatus.Running)
                {
                    extras.Add(Extra(w("reallife_mkt_bot_add", "Add"), false));
                    codes.Add("add");
                    extras.Add(Extra(w("reallife_mkt_bot_take", "Take out"), false));
                    codes.Add("take");
                    extras.Add(Extra(w("reallife_mkt_bot_sell_up", "Sell up"), false));
                    codes.Add("sell");
                }
                else if (b.Status == BotStatus.Stopped)
                {
                    extras.Add(Extra(w("reallife_mkt_bot_restart", "Restart"), false));
                    codes.Add("restart");
                }
                extras.Add(Extra(w("reallife_mkt_bot_dismiss", "Dismiss"), true));
                codes.Add("dismiss");
                string armed = st.Armed >= 0 && st.Armed < codes.Count ? codes[st.Armed] : null;
                for (int i = 0; i < extras.Count; i++)
                {
                    bool asks = codes[i] == "add" || codes[i] == "take" || codes[i] == "dismiss";
                    extras[i] = Armed(extras[i], asks && st.Armed == i);
                }
                m.Extras = extras.ToArray();
                long change = st.V[StocksSheets.Change];
                if (armed == "add" || armed == "take")
                {
                    bool adding = armed == "add";
                    m.Reason = Asked(MarketText.Fill(adding ? "reallife_mkt_bot_confirm_add" : "reallife_mkt_bot_confirm_take", "firm", f.Name, "amount", Money(change)),
                        MarketText.Fill(adding ? "reallife_mkt_bot_confirm_add_body" : "reallife_mkt_bot_confirm_take_body", "firm", f.Name,
                            "capital", Money(adding ? b.Capital + change : b.Capital - change)));
                }
                else if (armed == "dismiss")
                {
                    m.Reason = Asked(MarketText.Fill("reallife_mkt_bot_confirm_dismiss", "firm", f.Name), MarketText.Fill("reallife_mkt_bot_confirm_dismiss_body", "firm", f.Name));
                }
            }
            return m;
        }

        public static readonly string[] BotWhyKeys =
        {
            "reallife_mkt_bot_why_none", "reallife_mkt_bot_why_closed", "reallife_mkt_bot_why_held", "reallife_mkt_bot_why_behind", "reallife_mkt_bot_why_hired",
            "reallife_mkt_bot_why_full", "reallife_mkt_bot_why_too_little", "reallife_mkt_bot_why_no_cash", "reallife_mkt_bot_why_not_hired", "reallife_mkt_bot_why_not_stopped",
            "reallife_mkt_bot_why_too_little_left", "reallife_mkt_bot_why_off",
            "reallife_mkt_bot_why_own_shares", "reallife_mkt_bot_why_not_listed", "reallife_mkt_bot_why_picks", "reallife_mkt_bot_why_not_running", "reallife_mkt_bot_why_change_too_little",
            "reallife_mkt_bot_why_too_much_out"
        };

        private static int Idx(long v, int n) => (int)Math.Max(0, Math.Min(n - 1, v));

        private static string About(BotFirm f)
        {
            return MarketText.Fill(f.Key + "_about", "fast", MarketBots.TrendFast.ToString(), "slow", MarketBots.TrendSlow.ToString(), "in", MarketBots.RsiIn.ToString(),
                "out", MarketBots.RsiOut.ToString(), "filter", MarketBots.ReversionFilter.ToString(), "days", MarketBots.ReversionDaysMost.ToString(),
                "momentum", MarketBots.MomentumDays.ToString(), "quarters", MarketBots.IncomeQuarters.ToString());
        }
    }
}
