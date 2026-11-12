using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Ventures pages written from the facts: the Investors tab (offers and deals as a table, the valuation, and the chosen one's profile with its terms, promises and what the
    /// investor also backs), the steps of raising money and the Property tab. Game-free; the live source and the samples give it facts and the page is the same.
    /// </summary>
    public static class VenPages
    {
        public const int TabInvestors = 0, TabProperty = 1;
        public const int PageList = 0, PageBusiness = 1, PageType = 2, PageWho = 3;
        public const string OfferKey = "offer:", DealKey = "deal:", PledgeKey = "pledge:", BusinessKey = "biz:", NewKey = "new:", TypeKey = "type:", WhoKey = "inv:", EstateKey = "est:";

        public static void Fill(VentModel m, VentView v, VenFacts f, IVenActions act, VentWords w)
        {
            m.Title = w.T("reallife_ven_app", "Ventures");
            m.Icon = UiIcon.Coin;
            m.Prompts = Prompts(w);
            var runs = new VentRuns();
            m.Nav = Nav(f, w);
            m.Tab = Math.Max(0, Math.Min(m.Nav.Length - 1, v.Tab));
            if (!f.Ready)
            {
                m.Shape = VentShape.Off;
                m.OffTitle = f.OffTitle ?? w.T("reallife_ui_ven_off_title", "Ventures is not running");
                m.OffBody = f.OffBody ?? w.T("reallife_ui_ven_off_body", "Load a save with the economy switched on to see investors and deals here.");
                m.Runs = runs.ToArray();
                return;
            }
            if (m.Tab == TabProperty)
            {
                Property(m, v, f, act, w, runs);
            }
            else if (v.Page != PageList && f.InvestorsOn)
            {
                Raise(m, v, f, act, w, runs);
            }
            else
            {
                Investors(m, v, f, act, w, runs);
            }
            m.Runs = runs.ToArray();
        }

        /// <summary>The prompt bar's three words, as every kit app writes them.</summary>
        public static string[] Prompts(VentWords w) => new[]
        {
            w.T("reallife_ui_prompt_move", "Move"), w.T("reallife_ui_prompt_select", "Select"), w.T("reallife_ui_prompt_back", "Back")
        };

        private static UiNavEntry[] Nav(VenFacts f, VentWords w) => new[]
        {
            new UiNavEntry { Icon = UiIcon.Coin, Label = w.T("reallife_ven_tab_investors", "Investors"), Count = Count(f.Offers.Length + f.Pledges.Length + f.Deals.Length) },
            new UiNavEntry { Icon = UiIcon.Building, Label = w.T("reallife_ven_tab_property", "Property"), Count = Count(f.Property.Length) }
        };

        private static string Count(int n) => n > 0 ? n.ToString() : null;

        // ------------------------------------------------------------------------------------------------- the Investors tab

        private static void Investors(VentModel m, VentView v, VenFacts f, IVenActions act, VentWords w, VentRuns runs)
        {
            var left = new List<VentBlock>(6);
            var right = new List<VentBlock>(10);
            m.HeroFigures = new[]
            {
                new UiFigureModel { Label = w.T("reallife_ui_ven_fig_name", "Your name with investors"), Value = w.F("reallife_ven_of_hundred", "{value} of 100", "value",
                    f.Reputation.ToString()) },
                new UiFigureModel { Label = w.T("reallife_ui_ven_inv_in", "Put in"), Value = w.Money(f.Invested) },
                new UiFigureModel { Label = w.T("reallife_ui_ven_inv_out", "Paid out"), Value = w.Money(f.PaidOutTotal) },
                new UiFigureModel { Label = w.T("reallife_ui_ven_fig_promises", "Promises kept"), Value = f.PromisesTotal > 0 ? w.F("reallife_ui_ven_n_of_m", "{n} of {m}",
                    "n", f.PromisesKept.ToString(), "m", f.PromisesTotal.ToString()) : "-" }
            };
            string raiseWhy = f.InvestorsOn ? null : w.T("reallife_ui_ven_inv_off", "Investors are switched off in Mod settings. What was agreed carries on.");
            m.HeadActions = new[]
            {
                new UiActionModel
                {
                    Label = w.T("reallife_ven_raise", "Raise money"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Plus, Key = UiAction.Count, Command = UiAppWindow.ActionCommand,
                    Arg = runs.Add(() =>
                    {
                        v.Page = PageBusiness;
                        v.S1 = null;
                        v.S2 = null;
                        v.A = 3;
                        v.B = 0;
                        return true;
                    }),
                    Disabled = !f.InvestorsOn
                }
            };
            left.Add(VentBlock.Section(w.T("reallife_ui_ven_sec_table", "Offers and deals")));
            left.Add(DealsTable(f, v, w, out int picked));
            string pick = v.Pick ?? FirstKey(f);
            AddValuation(left, f, w);
            if (!string.IsNullOrEmpty(f.RivalLine))
            {
                left.Add(VentBlock.Note(f.RivalLine, UiRole.Warn));
            }
            if (!string.IsNullOrEmpty(f.HeardLine))
            {
                left.Add(VentBlock.Note(f.HeardLine, UiRole.Warn));
            }
            if (raiseWhy != null)
            {
                left.Add(VentBlock.Note(raiseWhy, UiRole.Warn));
            }
            Detail(right, f, pick, act, w, runs, v);
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        private static string FirstKey(VenFacts f)
        {
            return f.Offers.Length > 0 ? OfferKey + f.Offers[0].Id : f.Deals.Length > 0 ? DealKey + f.Deals[0].Id : f.Pledges.Length > 0 ? PledgeKey + f.Pledges[0].Id : null;
        }

        private static VentBlock DealsTable(VenFacts f, VentView v, VentWords w, out int picked)
        {
            var rows = new List<UiDataRow>(12);
            var keys = new List<string>(12);
            string want = v.Pick ?? FirstKey(f);
            picked = -1;
            for (int i = 0; i < f.Offers.Length; i++)
            {
                VenOfferFact o = f.Offers[i];
                rows.Add(Row(w, o.Who, o.For, o.Amount, o.ShareBp, o.Implied, w.F("reallife_ui_ven_chip_answer", "Answer in {days} days", "days", Math.Max(0, o.DaysLeft).ToString()),
                    o.DaysLeft <= 2 ? UiRole.Warn : UiRole.Notice));
                keys.Add(OfferKey + o.Id);
            }
            for (int i = 0; i < f.Pledges.Length; i++)
            {
                VenPledgeFact p = f.Pledges[i];
                rows.Add(Row(w, p.Who, w.F("reallife_ven_row_new", "A new {type}", "type", p.Type), p.Amount, p.ShareBp, VenturesImplied(p.Amount, p.ShareBp),
                    w.F("reallife_ven_row_open_by", "Open by day {day}", "day", p.OpenBy.ToString()), UiRole.Notice));
                keys.Add(PledgeKey + p.Id);
            }
            for (int i = 0; i < f.Deals.Length; i++)
            {
                VenDealFact d = f.Deals[i];
                UiRole tone = d.Stage == 3 || d.Stage == 1 ? UiRole.Danger : d.Stage == 2 ? UiRole.Muted : d.Total > 0 && d.Kept < d.Total ? UiRole.Warn : UiRole.Ok;
                rows.Add(Row(w, d.Who, d.Business, d.Amount, d.ShareNowBp, d.Implied, d.StateText, tone));
                keys.Add(DealKey + d.Id);
            }
            for (int i = 0; i < keys.Count; i++)
            {
                picked = keys[i] == want ? i : picked;
            }
            var table = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = w.T("reallife_ven_col_investor", "Investor"), Kind = UiCellKind.Person, Width = 0f },
                    new UiDataColumn { Title = w.T("reallife_ui_ven_col_for", "For"), Kind = UiCellKind.Text, Width = 0f },
                    new UiDataColumn { Title = w.T("reallife_ui_ven_col_amount", "Amount"), Kind = UiCellKind.Money, Width = 104f },
                    new UiDataColumn { Title = w.T("reallife_ui_ven_col_stake", "Stake"), Kind = UiCellKind.Figure, Width = 64f },
                    new UiDataColumn { Title = w.T("reallife_ui_ven_col_values", "Values you at"), Kind = UiCellKind.Money, Width = 128f },
                    new UiDataColumn { Title = w.T("reallife_ven_col_state", "State"), Kind = UiCellKind.Chip, Width = 168f }
                },
                Rows = rows.ToArray(), RowCount = rows.Count, Selected = picked, EmptyIcon = UiIcon.Coin,
                EmptyTitle = w.T("reallife_ui_ven_row_nothing", "No offers or deals yet"),
                EmptyBody = f.HowText ?? w.T("reallife_ui_ven_how_short", "Investors come to a business that keeps a profit. Raise money to go to them yourself.")
            };
            return new VentBlock { Kind = VentBlockKind.Table, Table = table, RowKeys = keys.ToArray(), Base = UiDataTable.First };
        }

        private static long VenturesImplied(long amount, int shareBp) => shareBp <= 0 ? 0L : amount * 10000L / shareBp;

        private static UiDataRow Row(VentWords w, VenInvestorFact who, string forText, long amount, int shareBp, long implied, string chip, UiRole tone)
        {
            return new UiDataRow
            {
                Cells = new[] { who.Name, forText, w.Money(amount), VentWords.Share(shareBp), implied > 0 ? w.Short(implied) : "-", chip },
                Values = new[] { 0, 0, 0, 0, 0, 0 },
                Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone },
                Subs = new[] { who.Kind, null, null, null, null, null }, Hue = VentWords.HueOf(who.Name), Ring = UiRole.None
            };
        }

        private static void AddValuation(List<VentBlock> into, VenFacts f, VentWords w)
        {
            int first = 0;
            while (first < f.Mine.Length && f.Mine[first] <= 0)
            {
                first++;
            }
            int n = f.Mine.Length - first;
            into.Add(VentBlock.Section(w.F("reallife_ui_ven_val_sec", "What investors see: valuation, {n} months", "n", Math.Max(2, n).ToString())));
            if (n < 2)
            {
                into.Add(VentBlock.Note(w.T("reallife_ui_ven_val_none", "Not yet: your valuation draws from your second month with a deal.")));
                return;
            }
            var points = new string[n];
            var mine = new float[n];
            var rival = new float[n];
            for (int i = 0; i < n; i++)
            {
                points[i] = first + i < f.MonthNames.Length ? f.MonthNames[first + i] : string.Empty;
                mine[i] = f.Mine[first + i];
                rival[i] = first + i < f.Rival.Length ? f.Rival[first + i] : 0f;
            }
            var series = new List<UiSeries> { new UiSeries { Label = w.T("reallife_ui_ven_val_you", "You"), Values = mine, Tint = UiRole.None } };
            if (!string.IsNullOrEmpty(f.RivalName))
            {
                series.Add(new UiSeries { Label = f.RivalName, Values = rival, Tint = UiRole.None });
            }
            into.Add(new VentBlock
            {
                Kind = VentBlockKind.Chart, ChartHeight = 176f,
                Chart = new UiChartModel { Kind = UiChartKind.Line, Title = null, Points = points, Series = series.ToArray(), Format = x => w.Short((long)x),
                    AxisFormat = x => w.Short((long)x), Still = true }
            });
        }

        // The chosen offer, deal or promise, or the investors the player can go to.
        private static void Detail(List<VentBlock> right, VenFacts f, string pick, IVenActions act, VentWords w, VentRuns runs, VentView v)
        {
            VenOfferFact offer = null;
            VenDealFact deal = null;
            VenPledgeFact pledge = null;
            for (int i = 0; pick != null && i < f.Offers.Length; i++)
            {
                offer = pick == OfferKey + f.Offers[i].Id ? f.Offers[i] : offer;
            }
            for (int i = 0; pick != null && i < f.Deals.Length; i++)
            {
                deal = pick == DealKey + f.Deals[i].Id ? f.Deals[i] : deal;
            }
            for (int i = 0; pick != null && i < f.Pledges.Length; i++)
            {
                pledge = pick == PledgeKey + f.Pledges[i].Id ? f.Pledges[i] : pledge;
            }
            if (offer != null)
            {
                OfferDetail(right, offer, f, act, w, runs);
            }
            else if (deal != null)
            {
                DealDetail(right, deal, f, act, w, runs);
            }
            else if (pledge != null)
            {
                PledgeDetail(right, pledge, act, w, runs);
            }
            else
            {
                InvestorList(right, f, w);
            }
        }

        private static VentBlock WhoItem(VenInvestorFact who, string chip, UiRole tone, VentWords w)
        {
            return new VentBlock
            {
                Kind = VentBlockKind.Items,
                Items = new[]
                {
                    new VentItem
                    {
                        Face = who.Name, Hue = VentWords.HueOf(who.Name), Title = who.Name, Chip = chip, ChipTone = tone, Run = -1,
                        Sub = w.F("reallife_ui_ven_who_sub", "{kind}, backed {count} businesses", "kind", who.Kind, "count", who.Portfolio.ToString())
                    }
                }
            };
        }

        private static void Alsos(List<VentBlock> into, VenInvestorFact who, VentWords w)
        {
            if (who.Backs.Length == 0)
            {
                return;
            }
            into.Add(VentBlock.Section(w.T("reallife_ui_ven_sec_also", "Also backs")));
            for (int i = 0; i < who.Backs.Length; i++)
            {
                into.Add(VentBlock.Row(who.Backs[i].Name, who.Backs[i].Pct + "%"));
            }
        }

        private static void OfferDetail(List<VentBlock> r, VenOfferFact o, VenFacts f, IVenActions act, VentWords w, VentRuns runs)
        {
            r.Add(WhoItem(o.Who, w.F("reallife_ui_ven_chip_expires", "Expires in {days} days", "days", Math.Max(0, o.DaysLeft).ToString()),
                o.DaysLeft <= 2 ? UiRole.Warn : UiRole.Notice, w));
            r.Add(VentBlock.Figures(
                new UiFigureModel { Label = w.T("reallife_ui_ven_col_amount", "Amount"), Value = w.Money(o.Amount) },
                new UiFigureModel { Label = w.T("reallife_ui_ven_col_stake", "Stake"), Value = VentWords.Share(o.ShareBp) },
                new UiFigureModel { Label = w.T("reallife_ui_ven_col_values", "Values you at"), Value = o.Implied > 0 ? w.Short(o.Implied) : "-" }));
            r.Add(VentBlock.Section(w.T("reallife_ui_ven_sec_terms", "Terms")));
            r.Add(VentBlock.Row(w.T("reallife_ui_ven_offer_for", "For"), o.For));
            r.Add(VentBlock.Row(w.T("reallife_ui_ven_offer_share", "Share of profit"), w.F("reallife_ui_ven_share_of_profit", "{share} of profit", "share", VentWords.Share(o.ShareBp))));
            r.Add(VentBlock.Row(w.T("reallife_ui_ven_offer_target", "They expect"), o.TargetLine));
            r.Add(VentBlock.Row(w.T("reallife_ui_ven_term_buy_back", "Buy back"), o.BuyBack ?? w.T("reallife_ui_ven_term_none", "None")));
            r.Add(VentBlock.Row(w.T("reallife_ui_ven_term_sale", "If you sell the firm"), o.SaleLine));
            PromiseBlocks(r, o.Promises, w, false);
            Alsos(r, o.Who, w);
            var actions = new List<UiActionModel>(3);
            actions.Add(new UiActionModel { Label = w.T("reallife_ven_refuse", "Refuse"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Close,
                Arg = runs.Add(() => { act.Refuse(o.Id); return true; }) });
            if (!o.Countered)
            {
                actions.Add(new UiActionModel
                {
                    Label = w.T("reallife_ui_ven_counter_more", "More money"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Coin, Key = UiAction.Option, Disabled = !o.CanMoreMoney,
                    Arg = runs.Add(() => { act.Counter(o.Id, true); return true; })
                });
                actions.Add(new UiActionModel
                {
                    Label = w.T("reallife_ven_counter_share", "Smaller share"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Percent,
                    Arg = runs.Add(() => { act.Counter(o.Id, false); return true; })
                });
            }
            actions.Add(new UiActionModel { Label = w.T("reallife_ui_ven_accept", "Take the offer"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt,
                Arg = runs.Add(() => { act.Accept(o.Id); return true; }) });
            r.Add(VentBlock.Buttons(actions.ToArray()));
        }

        private static void PromiseBlocks(List<VentBlock> r, VenPromiseFact[] promises, VentWords w, bool kept)
        {
            if (promises.Length == 0)
            {
                return;
            }
            r.Add(VentBlock.Section(w.T("reallife_ui_ven_sec_promise", "What you promise, checked each month")));
            for (int i = 0; i < promises.Length; i++)
            {
                r.Add(VentBlock.Check(promises[i].Text, promises[i].State == 1, promises[i].State == 0));
            }
            r.Add(VentBlock.Note(w.T("reallife_ui_ven_promise_seen", "Break a promise and the next investors see it.")));
        }

        private static void PledgeDetail(List<VentBlock> r, VenPledgeFact p, IVenActions act, VentWords w, VentRuns runs)
        {
            r.Add(WhoItem(p.Who, w.F("reallife_ven_row_open_by", "Open by day {day}", "day", p.OpenBy.ToString()), UiRole.Notice, w));
            r.Add(VentBlock.Figures(
                new UiFigureModel { Label = w.T("reallife_ui_ven_col_amount", "Amount"), Value = w.Money(p.Amount) },
                new UiFigureModel { Label = w.T("reallife_ui_ven_col_stake", "Stake"), Value = VentWords.Share(p.ShareBp) }));
            r.Add(VentBlock.Section(w.T("reallife_ui_ven_sec_terms", "Terms")));
            r.Add(VentBlock.Row(w.T("reallife_ui_ven_pledge_for", "For a new"), p.Type));
            r.Add(VentBlock.Note(w.F("reallife_ui_ven_pledge_how",
                "The money is paid in the day you open one, if it is by day {day}. Give the promise up and nothing is owed.", "day", p.OpenBy.ToString())));
            r.Add(VentBlock.Buttons(new UiActionModel
            {
                Label = w.T("reallife_ui_ven_pledge_drop", "Give the promise up"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Close,
                    Arg = runs.Add(() => { act.GiveUp(p.Id); return true; })
            }));
        }

        private static void DealDetail(List<VentBlock> r, VenDealFact d, VenFacts f, IVenActions act, VentWords w, VentRuns runs)
        {
            UiRole tone = d.Stage == 3 || d.Stage == 1 ? UiRole.Danger : d.Stage == 2 ? UiRole.Muted : UiRole.Ok;
            r.Add(WhoItem(d.Who, d.StateText, tone, w));
            r.Add(VentBlock.Figures(
                new UiFigureModel { Label = w.T("reallife_ui_ven_deal_in", "Put in"), Value = w.Money(d.Amount) },
                new UiFigureModel { Label = w.T("reallife_ui_ven_deal_out", "Paid out"), Value = w.Money(d.PaidOut) },
                new UiFigureModel { Label = w.T("reallife_ui_ven_col_values", "Values you at"), Value = d.Implied > 0 ? w.Short(d.Implied) : "-" }));
            r.Add(VentBlock.Section(w.T("reallife_ui_ven_sec_terms", "Terms")));
            r.Add(VentBlock.Row(w.T("reallife_ui_ven_offer_for", "For"), d.Business));
            r.Add(VentBlock.Row(w.T("reallife_ui_ven_offer_share", "Share of profit"), d.SeatLine ?? w.F("reallife_ui_ven_share_of_profit", "{share} of profit", "share",
                VentWords.Share(d.ShareNowBp))));
            r.Add(VentBlock.Row(w.T("reallife_ui_ven_deal_signed", "Signed"), d.Day));
            if (d.Arrears > 0)
            {
                r.Add(VentBlock.Row(w.T("reallife_ui_ven_deal_arrears", "Owed to them"), w.Money(d.Arrears), UiRole.WarnText));
            }
            if (d.TargetLine != null)
            {
                r.Add(VentBlock.Row(w.T("reallife_ui_ven_offer_target", "They expect"), d.TargetLine));
            }
            if (d.CostLine != null)
            {
                r.Add(VentBlock.Row(w.T("reallife_ui_ven_deal_cost_promise", "You promised"), d.CostLine));
            }
            r.Add(VentBlock.Row(w.T("reallife_ui_ven_deal_mood", "Mood"), d.MoodText));
            r.Add(VentBlock.Row(w.T("reallife_ui_ven_term_buy_back", "Buy back"), d.BuyBack ?? w.T("reallife_ui_ven_term_none", "None")));
            r.Add(VentBlock.Row(w.T("reallife_ui_ven_term_sale", "If you sell the firm"), d.SaleLine));
            if (d.CalledLine != null)
            {
                r.Add(VentBlock.Note(d.CalledLine, UiRole.Warn));
            }
            if (d.DemandLine != null)
            {
                r.Add(VentBlock.Note(d.DemandLine, UiRole.Warn));
            }
            PromiseBlocks(r, d.Promises, w, true);
            Alsos(r, d.Who, w);
            var actions = new List<UiActionModel>(4);
            if (d.Stage == 1)
            {
                if (d.CanSeat)
                {
                    actions.Add(new UiActionModel
                    {
                        Label = w.T("reallife_ui_ven_demand_seat", "Give them a board seat"), Kind = UiButtonKind.Secondary, Icon = UiIcon.People,
                            Arg = runs.Add(() => { act.Demand(d.Id, true); return true; })
                    });
                }
                actions.Add(new UiActionModel
                {
                    Label = w.T("reallife_ven_demand_costs", "Promise to cut costs"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Scales,
                        Arg = runs.Add(() => { act.Demand(d.Id, false); return true; })
                });
            }
            if (d.ReportDue)
            {
                actions.Add(new UiActionModel
                {
                    Label = w.T("reallife_ui_ven_report", "Send the report"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Send, Key = UiAction.Option,
                        Arg = runs.Add(() => { act.Report(d.Id); return true; })
                });
            }
            if (d.BuyBackNow && !d.BuyBackCalls && d.Stage != 3)
            {
                actions.Add(new UiActionModel
                {
                    Label = w.F("reallife_ui_ven_buy_back_at", "Buy back for {amount}", "amount", w.Money(d.BuyBackPrice)), Kind = UiButtonKind.Secondary, Icon = UiIcon.Coin,
                    Arg = runs.Add(() => { act.BuyBack(d.Id); return true; })
                });
            }
            actions.Add(new UiActionModel
            {
                Label = d.Stage == 3 ? w.F("reallife_ui_ven_pay_off_n", "Pay off {amount}", "amount", w.Money(d.BuyOut)) : w.F("reallife_ui_ven_buy_out_n",
                    "Buy out for {amount}", "amount", w.Money(d.BuyOut)),
                Kind = UiButtonKind.Primary, Icon = UiIcon.Coin, Key = UiAction.Alt, Arg = runs.Add(() => { act.BuyOut(d.Id); return true; })
            });
            r.Add(VentBlock.Buttons(actions.ToArray()));
        }

        private static void InvestorList(List<VentBlock> r, VenFacts f, VentWords w)
        {
            r.Add(VentBlock.Section(w.T("reallife_ui_ven_sec_investors", "Investors")));
            var items = new VentItem[f.Investors.Length];
            for (int i = 0; i < items.Length; i++)
            {
                VenInvestorFact v = f.Investors[i];
                items[i] = new VentItem
                {
                    Face = v.Name, Hue = VentWords.HueOf(v.Name), Title = v.Name, Sub = w.F("reallife_ui_ven_inv_sub", "{kind}, backed {count} businesses", "kind", v.Kind,
                        "count", v.Portfolio.ToString()),
                    Chip = v.OpenText, ChipTone = v.Open ? UiRole.Ok : v.Never ? UiRole.Danger : UiRole.Notice, Amount = w.Short(v.Most), Run = -1
                };
            }
            r.Add(new VentBlock { Kind = VentBlockKind.Items, Items = items });
            r.Add(VentBlock.Note(f.HowText ?? w.T("reallife_ui_ven_how_short", "Investors come to a business that keeps a profit. Raise money to go to them yourself.")));
        }

        // ------------------------------------------------------------------------------------------------- raising money

        // Three steps: the business the money is for (or a new one), its kind and budget, and the investor to approach. Back steps out one.
        private static void Raise(VentModel m, VentView v, VenFacts f, IVenActions act, VentWords w, VentRuns runs)
        {
            m.Sub = true;
            var left = new List<VentBlock>(4);
            var right = new List<VentBlock>(6);
            m.Next = w.T("reallife_ui_ven_raise_pick", "Which business is the money for?");
            VenRaiseFact picked = null;
            if (v.Page == PageBusiness)
            {
                BusinessStep(m, v, f, w, runs, left, right);
            }
            else if (v.Page == PageType)
            {
                TypeStep(m, v, f, w, runs, left, right);
            }
            else
            {
                WhoStep(m, v, f, act, w, runs, left, right, ref picked);
            }
            m.HeadActions = Prepend(Back(w), m.HeadActions);
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        private static UiActionModel Back(VentWords w) =>
            new UiActionModel { Label = w.T("reallife_ven_back", "Back"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Back, Key = UiAction.Back,
                Command = UiAppWindow.ActionCommand, Arg = VentFace.BackArg };

        private static UiActionModel[] Prepend(UiActionModel first, UiActionModel[] rest)
        {
            var all = new UiActionModel[rest.Length + 1];
            all[0] = first;
            Array.Copy(rest, 0, all, 1, rest.Length);
            return all;
        }

        private static void BusinessStep(VentModel m, VentView v, VenFacts f, VentWords w, VentRuns runs, List<VentBlock> left, List<VentBlock> right)
        {
            left.Add(VentBlock.Section(w.T("reallife_ven_raise", "Raise money")));
            var items = new List<VentItem>(f.RaiseBusinesses.Length + 1);
            items.Add(new VentItem
            {
                Icon = UiIcon.Plus, Tint = UiRole.None, Title = w.T("reallife_ui_ven_row_new_business", "A new business"), Sub = w.T("reallife_ui_ven_row_new_business_note",
                    "Money promised for one you have not opened yet"),
                Run = runs.Add(() => { v.S1 = null; v.Page = PageType; return true; })
            });
            for (int i = 0; i < f.RaiseBusinesses.Length; i++)
            {
                VenRaiseFact b = f.RaiseBusinesses[i];
                items.Add(new VentItem
                {
                    Icon = UiIcon.Store, Tint = UiRole.None, Title = b.Name, Sub = b.Line, Amount = w.F("reallife_ven_row_a_day", "{amount} a day", "amount", w.Money(b.AvgProfit)),
                    AmountInk = b.AvgProfit <= 0 ? UiRole.DangerText : UiRole.Text, Chip = b.Promised > 0 ? VentWords.Share(b.Promised) : null, ChipTone = UiRole.Notice,
                    Run = runs.Add(() => { v.S1 = b.Key; v.S2 = null; v.B = 0; v.Page = PageWho; return true; })
                });
            }
            left.Add(new VentBlock { Kind = VentBlockKind.Items, Items = items.ToArray() });
            right.Add(VentBlock.Section(w.T("reallife_ui_ven_raise_how_sec", "How it goes")));
            right.Add(VentBlock.Note(w.F("reallife_ui_ven_raise_how",
                "You face the investor in a meeting of {least} to {most} questions. They check what you say against the books.", "least", "3", "most", "6")));
            right.Add(VentBlock.Note(w.T("reallife_ui_ven_raise_truth", "Caught lying, you lose their trust and the others hear of it."), UiRole.Warn));
            right.Add(VentBlock.Note(w.T("reallife_ui_ven_raise_again", "An investor will not see you again for a while after a meeting.")));
        }

        private static void TypeStep(VentModel m, VentView v, VenFacts f, VentWords w, VentRuns runs, List<VentBlock> left, List<VentBlock> right)
        {
            m.Next = w.T("reallife_ui_ven_new_pick", "What kind of business will it be?");
            left.Add(VentBlock.Section(w.T("reallife_ui_ven_row_new_business", "A new business")));
            var items = new VentItem[f.RaiseTypes.Length];
            for (int i = 0; i < items.Length; i++)
            {
                VenRaiseFact t = f.RaiseTypes[i];
                items[i] = new VentItem
                {
                    Icon = UiIcon.Store, Title = t.Name, Sub = t.Sector, Chip = w.F("reallife_ui_ven_row_investors", "{count} investors like it", "count", t.Liked.ToString()),
                        ChipTone = t.Liked > 0 ? UiRole.Ok : UiRole.Notice,
                    Amount = t.Running > 0 ? w.F("reallife_ui_ven_row_you_run", "You run {count}", "count", t.Running.ToString()) : null,
                    Run = runs.Add(() => { v.S2 = t.Key; return true; })
                };
            }
            left.Add(new VentBlock { Kind = VentBlockKind.Items, Items = items, EmptyTitle = w.T("reallife_ui_ven_row_no_types", "No kind of business can be opened now"),
                EmptyBody = null });
            int steps = Math.Max(1, f.Budgets.Length);
            v.A = Math.Max(0, Math.Min(steps - 1, v.A));
            right.Add(VentBlock.Section(w.T("reallife_ven_new_budget", "What it will cost")));
            right.Add(VentBlock.Row(w.T("reallife_ui_ven_new_kind", "Kind"), v.S2 == null ? "-" : NameOf(f.RaiseTypes, v.S2)));
            right.Add(VentBlock.Stepper(w.T("reallife_ui_ven_new_budget_label", "Budget"), f.Budgets.Length > 0 ? w.Money(f.Budgets[v.A]) : "-",
                runs.Add(() => { v.A--; return true; }) , v.A > 0, v.A < steps - 1));
            runs.Add(() => { v.A++; return true; });
            right.Add(VentBlock.Note(w.F("reallife_ui_ven_new_how",
                "Investors offer between {least} and {most} per cent of the budget as a share of profit, and the money is promised for {days} days.", "least", "20", "most",
                    "70", "days", "60")));
            right.Add(VentBlock.Buttons(new UiActionModel
            {
                Label = w.T("reallife_ven_new_next", "Choose an investor"), Kind = UiButtonKind.Primary, Icon = UiIcon.Next, Key = UiAction.Alt, Disabled = v.S2 == null,
                Arg = runs.Add(() => { v.B = 0; v.Page = PageWho; return true; })
            }));
        }

        private static string NameOf(VenRaiseFact[] list, string key)
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].Key == key)
                {
                    return list[i].Name;
                }
            }
            return "-";
        }

        private static void WhoStep(VentModel m, VentView v, VenFacts f, IVenActions act, VentWords w, VentRuns runs, List<VentBlock> left, List<VentBlock> right, ref VenRaiseFact picked)
        {
            string about = v.S1 != null ? NameOf(f.RaiseBusinesses, v.S1) : v.S2 != null ? w.F("reallife_ven_row_new", "A new {type}", "type", NameOf(f.RaiseTypes, v.S2)) : "-";
            m.Next = w.F("reallife_ui_ven_who_pick", "Who will you go to for {about}?", "about", about);
            left.Add(VentBlock.Section(w.T("reallife_ui_ven_sec_investors", "Investors")));
            var items = new VentItem[f.Investors.Length];
            v.B = Math.Max(0, Math.Min(items.Length - 1, v.B));
            for (int i = 0; i < items.Length; i++)
            {
                VenInvestorFact who = f.Investors[i];
                int at = i;
                items[i] = new VentItem
                {
                    Face = who.Name, Hue = VentWords.HueOf(who.Name), Title = who.Name,
                    Sub = w.F("reallife_ui_ven_who_range", "{kind}, {least} to {most}", "kind", who.Kind, "least", w.Short(who.Least), "most", w.Short(who.Most)),
                    Chip = who.OpenText, ChipTone = who.Open ? UiRole.Ok : who.Never ? UiRole.Danger : UiRole.Notice, Amount = at == v.B ? w.T("reallife_ui_ven_chosen", "Chosen") : null,
                    AmountInk = UiRole.AccentText, Run = runs.Add(() => { v.B = at; return true; })
                };
            }
            left.Add(new VentBlock { Kind = VentBlockKind.Items, Items = items });
            if (items.Length == 0)
            {
                return;
            }
            VenInvestorFact c = f.Investors[v.B];
            right.Add(WhoItem(c, c.OpenText, c.Open ? UiRole.Ok : UiRole.Notice, w));
            right.Add(VentBlock.Row(w.T("reallife_ui_ven_inv_likes", "Likes"), c.Likes));
            right.Add(VentBlock.Row(w.T("reallife_ui_ven_inv_temper", "Temper"), c.Temper));
            right.Add(VentBlock.Row(w.T("reallife_ui_ven_inv_range", "Cheques"), w.F("reallife_ven_row_range", "{least} to {most}", "least", w.Short(c.Least), "most", w.Short(c.Most))));
            if (c.Committed > 0)
            {
                right.Add(VentBlock.Note(w.F("reallife_ui_ven_inv_committed", "A rival's round took {amount} of what they could put in this quarter.", "amount",
                    w.Short(c.Committed)), UiRole.Warn));
            }
            Alsos(right, c, w);
            string place = v.S1, type = v.S2;
            int budget = v.A;
            right.Add(VentBlock.Buttons(new UiActionModel
            {
                Label = w.T("reallife_ui_ven_pitch", "Go to the meeting"), Kind = UiButtonKind.Primary, Icon = UiIcon.Handshake, Key = UiAction.Alt, Disabled = !c.Open || !f.RaiseNow,
                Arg = runs.Add(() => { act.Pitch(c.Id, place, type, budget); v.Page = PageList; v.Pick = null; return true; })
            }));
        }

        // ------------------------------------------------------------------------------------------------- the Property tab

        private static void Property(VentModel m, VentView v, VenFacts f, IVenActions act, VentWords w, VentRuns runs)
        {
            m.HeroFigures = new[]
            {
                new UiFigureModel { Label = w.T("reallife_ui_ven_prop_week", "Rent this week"), Value = w.Money(f.PropWeek) },
                new UiFigureModel { Label = w.T("reallife_ui_ven_prop_month", "Rent in the last month"), Value = w.Money(f.PropMonth) },
                new UiFigureModel { Label = w.T("reallife_ui_ven_prop_count", "Buildings"), Value = f.Property.Length.ToString() },
                new UiFigureModel { Label = w.T("reallife_ui_ven_prop_paid", "Paid"), Value = w.Money(f.PropPaid) }
            };
            m.HeadActions = new[]
            {
                new UiActionModel
                {
                    Label = w.T("reallife_ui_ven_prop_bizman", "Open the Business manager"), Kind = UiButtonKind.Primary, Icon = UiIcon.Briefcase, Key = UiAction.Alt,
                        Command = UiAppWindow.ActionCommand,
                    Arg = runs.Add(() => { act.OpenBizMan(); return true; })
                }
            };
            var rows = new List<UiDataRow>(f.Property.Length);
            var keys = new List<string>(f.Property.Length);
            var locate = new List<int>(f.Property.Length);
            string pick = v.Pick ?? (f.Property.Length > 0 ? EstateKey + f.Property[0].Key : null);
            int selected = -1;
            VenPropertyFact chosen = null;
            for (int i = 0; i < f.Property.Length; i++)
            {
                VenPropertyFact p = f.Property[i];
                string place = p.Key;
                rows.Add(new UiDataRow
                {
                    Cells = new[] { p.Address, p.District ?? string.Empty, p.Let + "%", w.Money(p.Paid), p.Worth > 0 ? w.Money(p.Worth) : "-", w.Money(p.Week), p.Yield ?? "-",
                        string.Empty },
                    Values = new[] { (int)UiIcon.Building, 0, p.Let, 0, 0, 0, p.YieldGrade, (int)UiIcon.Locate },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                    Subs = new string[8]
                });
                keys.Add(EstateKey + p.Key);
                locate.Add(runs.Add(() => { act.Locate(place); return true; }));
                if (pick == EstateKey + p.Key)
                {
                    selected = i;
                    chosen = p;
                }
            }
            var table = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = w.T("reallife_ven_col_building", "Building"), Kind = UiCellKind.Tile, Width = 0f },
                    new UiDataColumn { Title = w.T("reallife_ui_ven_col_district", "District"), Kind = UiCellKind.Text, Width = 0f },
                    new UiDataColumn { Title = w.T("reallife_ui_ven_col_let", "Let"), Kind = UiCellKind.Meter, Width = 112f },
                    new UiDataColumn { Title = w.T("reallife_ui_ven_col_paid", "Paid"), Kind = UiCellKind.Money, Width = 104f },
                    new UiDataColumn { Title = w.T("reallife_ven_col_worth", "Worth"), Kind = UiCellKind.Money, Width = 104f },
                    new UiDataColumn { Title = w.T("reallife_ui_ven_col_rent_week", "Rent a week"), Kind = UiCellKind.Money, Width = 104f },
                    new UiDataColumn { Title = w.T("reallife_ven_col_yield", "Yield a year"), Kind = UiCellKind.Figure, Width = 96f, Graded = true },
                    new UiDataColumn { Title = w.T("reallife_ui_ven_col_map", "On the map"), Kind = UiCellKind.Action, Width = 72f }
                },
                Rows = rows.ToArray(), RowCount = rows.Count, Selected = selected, EmptyIcon = UiIcon.Building,
                EmptyTitle = w.T("reallife_ui_ven_row_no_property", "You own no buildings"), EmptyBody = w.T("reallife_ui_ven_prop_what",
                    "Buildings you own appear here with their rent and what they would yield, read only. Buy and let them from the Property app.")
            };
            m.Left = new[]
            {
                new VentBlock { Kind = VentBlockKind.Table, Table = table, RowKeys = keys.ToArray(), RowRuns = locate.ToArray(), Base = UiDataTable.First }
            };
            var right = new List<VentBlock>(8);
            if (chosen != null)
            {
                right.Add(VentBlock.Section(chosen.Address));
                right.Add(VentBlock.Row(w.T("reallife_ui_ven_prop_bought", "Bought"), chosen.Bought));
                if (chosen.Worth > 0)
                {
                    right.Add(VentBlock.Row(w.T("reallife_ui_ven_prop_worth", "Worth now"), w.Money(chosen.Worth)));
                }
                right.Add(VentBlock.Row(w.T("reallife_ui_ven_prop_let", "Let"), chosen.Let + "%"));
                right.Add(VentBlock.Row(w.T("reallife_ui_ven_prop_week", "Rent this week"), w.Money(chosen.Week)));
                right.Add(VentBlock.Row(w.T("reallife_ui_ven_prop_month", "Rent in the last month"), w.Money(chosen.Month)));
                right.Add(VentBlock.Row(w.T("reallife_ui_ven_prop_yield", "Yield a year"), chosen.Yield ?? "-"));
                if (chosen.Mortgage > 0)
                {
                    right.Add(VentBlock.Row(w.T("reallife_ui_ven_prop_mortgage", "Mortgage owed"), w.Money(chosen.Mortgage), UiRole.WarnText));
                }
            }
            right.Add(VentBlock.Note(w.T("reallife_ui_ven_prop_yield_note", "Yield is a year of rent over the price paid. A home you live in earns nothing.")));
            m.Right = right.ToArray();
        }
    }
}
