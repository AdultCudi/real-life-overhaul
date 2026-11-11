using System;
using Localizor;
using RealLifeOverhaul.BizPace;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Interiors;

namespace RealLifeOverhaul.Loyalty
{
    /// <summary>The Loyalty tab's two columns: the week's figures first, then the scheme, its terms and its buttons.</summary>
    public static partial class LoyaltyTab
    {
        private static readonly int BizWrite = HotSpots.Slot("Loyalty: LoyaltyTab.Write");

        /// <summary>
        /// The page switched on: written for the business BizMan shows now, unless the boxes hold it already, written for this
        /// business in this showing of the business view, in this language and since the schemes last moved (the game is paused
        /// with its menu open). Flipping back to the tab then costs nothing; a press that changed what it says writes it afresh.
        /// </summary>
        internal static void Write()
        {
            if (_business == null || _left == null || _right == null)
            {
                return;
            }
            using var timed = BizPaceWatch.Time(BizWrite);   // a step of BizMan's tab change (BizPace)
            if (!Written())
            {
                Rewrite();
            }
        }

        // True if the boxes hold the page of the business shown, as it was written in this showing of the view, this language and this state of the schemes.
        private static bool Written()
        {
            LoyaltyDirector d = LoyaltyDirector.Instance;
            return _wrote && _wroteSession == Session && d != null && _wroteRevision == d.Revision && ReferenceEquals(_business.buildingRegistration, _wroteReg)
                   && string.Equals(_wroteLocale, LocalizorManager.LoadedLocale, StringComparison.Ordinal);
        }

        /// <summary>The page written afresh for the business BizMan shows now.</summary>
        internal static void Rewrite()
        {
            if (_business == null || _left == null || _right == null)
            {
                return;
            }
            BuildingRegistration reg = _business.buildingRegistration;
            _wrote = false;
            _left.Clear();
            _right.Clear();
            Lines(reg, new BoxLines(_left), new BoxLines(_right));
            _left.Done();
            _right.Done();
            LoyaltyDirector d = LoyaltyDirector.Instance;
            _wroteReg = reg;
            _wroteSession = Session;
            _wroteRevision = d != null ? d.Revision : -1;
            _wroteLocale = LocalizorManager.LoadedLocale;
            _wrote = true;
        }

        /// <summary>
        /// The tab for a business written as lines: the week's figures into the left, the scheme into the right, each line what
        /// the game's own boxes hold; the RLO interface asks for the same lines when BizMan is drawn on it. Written when a
        /// business opens or a button is pressed, never a frame.
        /// </summary>
        public static void Lines(BuildingRegistration reg, IInteriorLines left, IInteriorLines right)
        {
            LoyaltyDirector d = LoyaltyDirector.Instance;
            if (d == null || !d.Running || reg == null || !LoyaltyGame.IsShop(reg))
            {
                WriteNone(left, reg);
                return;
            }
            LoyaltyFile f = d.At(reg);
            WriteWeek(left, f, reg);
            WriteScheme(right, d, f, reg);
        }

        // A business the cards are not for: its name and why.
        private static void WriteNone(IInteriorLines b, BuildingRegistration reg)
        {
            b.Title(reg != null ? LoyaltyGame.Name(reg) : string.Empty);
            b.Line("reallife_loyalty_none".GetLocalization());
            b.Guide(GuideTitle);
        }

        // The figures first: the members, and the last seven days added up; then what the scheme does today and what drives members away.
        private static void WriteWeek(IInteriorLines b, LoyaltyFile f, BuildingRegistration reg)
        {
            b.Title(LoyaltyGame.Name(reg));
            b.Headline("reallife_loyalty_head_week");
            if (f == null)
            {
                b.Line("reallife_loyalty_week_none".GetLocalization());
                b.Guide(GuideTitle);
                return;
            }
            LoyaltyDay week = LoyaltyRules.Week(f, LoyaltyGame.Day);
            b.Line(LoyaltyText.T("reallife_loyalty_line_members", "members", LoyaltyText.Whole(f.Members), "joined", LoyaltyText.Whole(week.Signups), "left", LoyaltyText.Whole(week.Lapsed)));
            b.Line(LoyaltyText.T("reallife_loyalty_line_visits", "visits", LoyaltyText.N(week.Visits)));
            b.Line(LoyaltyText.T("reallife_loyalty_line_rewards", "amount", LoyaltyText.Money(week.Rewards)));
            b.Line(LoyaltyText.T("reallife_loyalty_line_costs", "amount", LoyaltyText.Money(week.Costs)));
            b.Line(LoyaltyText.T("reallife_loyalty_line_extra", "amount", LoyaltyText.Money(week.Extra)));
            b.Line(LoyaltyText.T("reallife_loyalty_line_margin", "amount", LoyaltyText.Money(week.Margin)));
            long net = week.Margin - week.Rewards - week.Costs;
            if (week.Rewards > 0 || week.Costs > 0 || week.Extra > 0)
            {
                b.Line(LoyaltyText.T(net >= 0 ? "reallife_loyalty_line_ahead" : "reallife_loyalty_line_behind", "amount", LoyaltyText.Money(Math.Abs(net))),
                    net >= 0 ? CellTone.Green : CellTone.Red);
            }
            b.Headline("reallife_loyalty_head_today");
            if (!f.On)
            {
                b.Line((f.Want ? "reallife_loyalty_today_starting" : "reallife_loyalty_today_off").GetLocalization());
                b.Guide(GuideTitle);
                return;
            }
            b.Line(LoyaltyText.T("reallife_loyalty_today_lift", "percent", LoyaltyText.N(f.Lift)));
            b.Line(LoyaltyText.T("reallife_loyalty_today_basket", "share", LoyaltyText.Mille(f.Basket)));
            int most = (int)(LoyaltyRules.Cap(f.Base, f.Sisters) * 1000 / Math.Max(1L, LoyaltyRules.BaseCustomers(f.Base)));
            b.Line(LoyaltyText.T("reallife_loyalty_today_share", "share", LoyaltyText.Mille(LoyaltyRules.Share(f.Members, f.Base)), "most", LoyaltyText.Mille(most)));
            if (f.Rivals > 0)
            {
                b.Line(LoyaltyText.T(f.RivalsRunning > 0 ? "reallife_loyalty_today_rivals" : "reallife_loyalty_today_rivals_none", "running", LoyaltyText.N(f.RivalsRunning),
                    "count", LoyaltyText.N(f.Rivals), "district", LoyaltyGame.DistrictName(reg), "percent", LoyaltyText.Mille(1000 - LoyaltyRules.RivalryKept(f.Rivalry))));
            }
            LoyaltyInput now = LoyaltyGame.Yesterday(reg, LoyaltyGame.Day - 1);
            if (f.BadHours > 0)
            {
                b.Line(LoyaltyText.T("reallife_loyalty_trouble_shut", "hours", LoyaltyText.N(f.BadHours), "open", LoyaltyText.N(f.OpenHours)), CellTone.Red);
            }
            if (now.Clean < LoyaltyRules.CleanFrom)
            {
                b.Line(LoyaltyText.T("reallife_loyalty_trouble_dirty", "clean", LoyaltyText.N(now.Clean), "from", LoyaltyText.N(LoyaltyRules.CleanFrom)), CellTone.Red);
            }
            if (now.Pricing < LoyaltyRules.PricesFrom)
            {
                b.Line(LoyaltyText.T("reallife_loyalty_trouble_dear", "percent", LoyaltyText.N(now.Pricing), "from", LoyaltyText.N(LoyaltyRules.PricesFrom)), CellTone.Red);
            }
            if (now.Satisfaction < LoyaltyRules.PleasedFull)
            {
                b.Line(LoyaltyText.T(now.Satisfaction <= LoyaltyRules.PleasedFrom ? "reallife_loyalty_trouble_unhappy" : "reallife_loyalty_trouble_lukewarm",
                    "satisfaction", LoyaltyText.N(now.Satisfaction), "from", LoyaltyText.N(LoyaltyRules.PleasedFrom), "full", LoyaltyText.N(LoyaltyRules.PleasedFull)),
                    now.Satisfaction <= LoyaltyRules.PleasedFrom ? CellTone.Red : CellTone.Yellow);
            }
            b.Guide(GuideTitle);
        }

        // The scheme: whether it runs, its terms as they are and as chosen, what each costs, and the buttons.
        private static void WriteScheme(IInteriorLines b, LoyaltyDirector d, LoyaltyFile f, BuildingRegistration reg)
        {
            b.Title("reallife_loyalty_head_scheme".GetLocalization());
            bool on = f != null && f.On, want = f != null && f.Want;
            if (on && want)
            {
                b.Line(LoyaltyText.T("reallife_loyalty_state_on", "day", LoyaltyText.N(f.Since)), CellTone.Green);
            }
            else if (on)
            {
                b.Line("reallife_loyalty_state_ending".GetLocalization(), CellTone.Yellow);
            }
            else
            {
                b.Line((want ? "reallife_loyalty_state_starting" : "reallife_loyalty_state_off").GetLocalization());
            }
            string brand;
            if (LoyaltyDirector.ChainOf(reg, out brand) != null && !string.IsNullOrEmpty(brand))
            {
                b.Line(LoyaltyText.T("reallife_loyalty_line_chain", "brand", brand, "count", LoyaltyText.N(f != null ? f.Sisters : 0)));
            }
            int change = Prices.At(LoyaltyRules.ChangeCost());
            if (on || want)
            {
                WriteTerms(b, f, change);
            }
            if (!want)
            {
                int cost = d.SetupCost(reg);
                if (!on)
                {
                    b.Line(LoyaltyText.T("reallife_loyalty_line_setup", "amount", LoyaltyText.Money(cost * 100L), "day", LoyaltyText.Money(Prices.At(LoyaltyRules.CardDay) * 100L)));
                }
                b.Button(InfoBox.Positive, on ? "reallife_loyalty_btn_keep" : "reallife_loyalty_btn_start", () => AskStart(reg, cost));
            }
            else
            {
                b.Button(InfoBox.Negative, "reallife_loyalty_btn_end", () => Pressed(dir => dir.End(reg)));
            }
            if (on || want)
            {
                b.Button(InfoBox.Additional, "reallife_loyalty_btn_rate", () => Pressed(dir => dir.NextRate(reg)));
                b.Button(InfoBox.Additional, "reallife_loyalty_btn_push", () => Pressed(dir => dir.NextPush(reg)));
                b.Button(InfoBox.Additional, "reallife_loyalty_btn_day", () => Pressed(dir => dir.NextOffer(reg)));
                b.Note(LoyaltyText.T("reallife_loyalty_note_changes", "amount", LoyaltyText.Money(change * 100L), "days", LoyaltyText.N(LoyaltyRules.OfferNotice)));
            }
            else
            {
                b.Note("reallife_loyalty_note_start".GetLocalization());
            }
            b.Settings("loyalty");
            b.Guide(GuideTitle);
        }

        // The terms in force and, where the player has chosen otherwise, what comes in and when; the margin a rate needs beside the shop's own.
        private static void WriteTerms(IInteriorLines b, LoyaltyFile f, int change)
        {
            b.Headline("reallife_loyalty_head_terms");
            b.Line(LoyaltyText.T("reallife_loyalty_line_rate", "rate", LoyaltyText.Rate(f.Rate)));
            if (f.NextRate != f.Rate)
            {
                b.Line(LoyaltyText.T(f.On ? "reallife_loyalty_line_rate_next" : "reallife_loyalty_line_rate_first", "rate", LoyaltyText.Rate(f.NextRate),
                    "amount", LoyaltyText.Money(change * 100L)), CellTone.Yellow);
            }
            bool offer = f.NextOffer >= 0;
            int even = LoyaltyRules.BreakEven(f.NextRate, offer, f.Rivalry);
            if (f.MarginMille == LoyaltyRules.MarginUnknown)
            {
                b.Line(LoyaltyText.T("reallife_loyalty_line_even_unknown", "even", LoyaltyText.Mille(even)));
            }
            else if (LoyaltyRules.Sound(f.NextRate, offer, f.Rivalry, f.MarginMille))
            {
                b.Line(LoyaltyText.T("reallife_loyalty_line_even", "even", LoyaltyText.Mille(even), "margin", LoyaltyText.Mille(f.MarginMille)));
            }
            else
            {
                b.Line(LoyaltyText.T("reallife_loyalty_line_loss", "even", LoyaltyText.Mille(even), "margin", LoyaltyText.Mille(f.MarginMille)), CellTone.Red);
            }
            b.Line(LoyaltyText.T("reallife_loyalty_line_push", "push", LoyaltyText.Push(f.Push),
                "amount", LoyaltyText.Money((long)Math.Round(Prices.At(LoyaltyRules.PushCost[LoyaltyRules.Clamp(f.Push, 0, LoyaltyRules.Pushes - 1)] / 100f) * 100f))));
            if (f.NextOffer == f.OfferDay)
            {
                b.Line(f.OfferDay < 0 ? "reallife_loyalty_line_day_none".GetLocalization()
                    : LoyaltyText.T("reallife_loyalty_line_day", "day", LoyaltyText.Day(f.OfferDay), "times", LoyaltyText.N(LoyaltyRules.OfferPoints)));
            }
            else if (f.NextOffer < 0)
            {
                b.Line("reallife_loyalty_line_day_ending".GetLocalization(), CellTone.Yellow);
            }
            else
            {
                int from = f.OfferFrom > 0 && f.OfferAsked == f.NextOffer ? f.OfferFrom : LoyaltyGame.Day + 1 + LoyaltyRules.OfferNotice;
                b.Line(LoyaltyText.T("reallife_loyalty_line_day_next", "day", LoyaltyText.Day(f.NextOffer), "from", LoyaltyText.N(from)), CellTone.Yellow);
            }
        }

        // The set-up is asked about before any money moves; taking an ending back costs nothing and asks nothing.
        private static void AskStart(BuildingRegistration reg, int cost)
        {
            if (cost <= 0)
            {
                Pressed(dir => dir.Start(reg, cost));
                return;
            }
            string body = LoyaltyText.T("reallife_loyalty_q_start", "business", LoyaltyGame.Name(reg), "amount", LoyaltyText.Money(cost * 100L),
                "day", LoyaltyText.Money(Prices.At(LoyaltyRules.CardDay) * 100L));
            GameConfirm.Show("reallife_loyalty_q_start_title".GetLocalization(), body, () => UiClone.Safe(() => Pressed(dir => dir.Start(reg, cost))), null, null, null, false, false);
        }

        // A press on the page: done by the director, and the game's own boxes written afresh if they are showing.
        private static void Pressed(Func<LoyaltyDirector, LoyaltyRefusal> press)
        {
            LoyaltyDirector d = LoyaltyDirector.Instance;
            if (d == null)
            {
                return;
            }
            press(d);
            if (_page != null && _page.activeInHierarchy)
            {
                UiClone.Safe(Rewrite);
            }
        }
    }
}
