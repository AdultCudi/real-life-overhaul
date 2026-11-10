using System;
using System.Collections.Generic;
using RealLifeOverhaul.Suppliers;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the Suppliers pages do in the game: each is the director's own call, so a page can only reach what the director allows.</summary>
    public interface ISupplyActions
    {
        /// <summary>Signs what a supplier offers a place: a term in weeks (0: one delivery at the day's price) at a step of the week's sales.</summary>
        void Sign(int supplier, string place, int weeks, int shareStep);

        /// <summary>Ends a contract early, the settlement due (the game asks first).</summary>
        void End(int contract);

        /// <summary>Leaves a contract by right after the supplier's own failures.</summary>
        void Leave(int contract);

        /// <summary>Pays what is owed to suppliers as far as the cash goes.</summary>
        void PayOwed();

        /// <summary>Moves one of a contract's rules for its buyer to its next value (SupplierCatalogue.RuleRenew and the rest).</summary>
        void Rule(int contract, int rule);
    }

    /// <summary>
    /// The Suppliers pages: the suppliers a place can buy from with each one's character and its offer, the contracts with their rules, and the market with the
    /// suppliers' news and the rivals. A page shows only what the rules allow, and every number comes from <see cref="SupplyFacts"/>, which the director fills from the
    /// state and the game. Game-free.
    /// </summary>
    public static class SupplyPages
    {
        public const int TabSuppliers = 0, TabContracts = 1, TabMarket = 2;
        public const string FirmKey = "f:", ContractKey = "c:";
        public const int TermDefault = 2;   // the term step a page starts at: the catalogue's second term, four weeks

        /// <summary>The term step a view's figure stands for: nothing chosen yet is the default.</summary>
        public static int TermOf(int b) => b > 0 ? b - 1 : TermDefault;

        /// <summary>The share step a view's figure stands for: nothing chosen yet is the week's sales.</summary>
        public static int ShareOf(int c) => c > 0 ? c - 1 : SupplierCatalogue.ShareDefault;

        private static readonly UiIcon[] KindIcons = { UiIcon.Sun, UiIcon.Store, UiIcon.Person, UiIcon.Spark, UiIcon.Sofa, UiIcon.Briefcase };

        public static void Fill(VentModel m, VentView v, SupplyFacts f, ISupplyActions act, VentWords w)
        {
            m.Title = w.T("reallife_supply_app", "Suppliers");
            m.Icon = UiIcon.Truck;
            m.Prompts = VenPages.Prompts(w);
            var runs = new VentRuns();
            int running = 0;
            for (int i = 0; i < f.Contracts.Count; i++)
            {
                running += f.Contracts[i].Running ? 1 : 0;
            }
            m.Nav = new[]
            {
                new UiNavEntry { Icon = UiIcon.Truck, Label = w.T("reallife_supply_tab_suppliers", "Suppliers") },
                new UiNavEntry { Icon = UiIcon.Briefcase, Label = w.T("reallife_supply_tab_contracts", "Contracts"), Count = running > 0 ? running.ToString() : null },
                new UiNavEntry { Icon = UiIcon.Chart, Label = w.T("reallife_supply_tab_market", "The market") }
            };
            m.Tab = Math.Max(0, Math.Min(2, v.Tab));
            if (!f.Ready)
            {
                m.Shape = VentShape.Off;
                m.OffTitle = w.T("reallife_supply_off_title", "Suppliers are not running");
                m.OffBody = w.T("reallife_supply_off_body", "Load a save with Living population and Suppliers and contracts switched on to choose who brings your stock.");
                m.Runs = runs.ToArray();
                return;
            }
            long week = 0;
            for (int i = 0; i < f.Contracts.Count; i++)
            {
                week += f.Contracts[i].Running ? f.Contracts[i].Value : 0L;
            }
            m.HeroFigures = new[]
            {
                new UiFigureModel { Label = w.T("reallife_supply_fig_running", "Contracts running"), Value = running.ToString() },
                new UiFigureModel { Label = w.T("reallife_supply_fig_week", "Deliveries a week"), Value = w.Money(week / 100f) },
                new UiFigureModel { Label = w.T("reallife_supply_fig_owed", "Owed to suppliers"), Value = w.Money(f.Owed / 100f) }
            };
            switch (m.Tab)
            {
                case TabContracts: Contracts(m, v, f, act, w, runs); break;
                case TabMarket: Market(m, f, w); break;
                default: Suppliers(m, v, f, act, w, runs); break;
            }
            m.Runs = runs.ToArray();
        }

        // ---- words both faces of the app share

        /// <summary>The words of a term step: one delivery, or so many weeks.</summary>
        public static string Term(VentWords w, int termStep)
        {
            int weeks = SupplyFacts.Weeks(termStep);
            return weeks == 0 ? w.T("reallife_supply_term_once", "One delivery, today's price") : w.F("reallife_supply_term_weeks", "{n} weeks, price fixed", "n", weeks.ToString());
        }

        /// <summary>A price against the market's, in words: under it, over it or at it.</summary>
        public static string Against(VentWords w, int against)
        {
            int gap = Math.Abs(against - 1000);
            string share = (gap / 10f).ToString(gap % 10 == 0 ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";
            return gap == 0 ? w.T("reallife_supply_at_market", "At today's market price")
                : against < 1000 ? w.F("reallife_supply_under_market", "{share} under today's market price", "share", share)
                : w.F("reallife_supply_over_market", "{share} over today's market price", "share", share);
        }

        /// <summary>The kinds of goods of a set of bits, in words.</summary>
        public static string Kinds(VentWords w, int kinds)
        {
            var text = new System.Text.StringBuilder(48);
            for (int k = 0; k < SupplierCatalogue.Kinds; k++)
            {
                if ((kinds & (1 << k)) != 0)
                {
                    text.Append(text.Length > 0 ? ", " : string.Empty).Append(w.T(SupplierCatalogue.KindKeys[k], KindEnglish(k)));
                }
            }
            return text.ToString();
        }

        public static string FirmName(VentWords w, int id)
        {
            SupplierDef def = SupplierCatalogue.Firm(id);
            return def != null ? w.T(def.NameKey, FirmEnglish(id)) : w.T(SupplierKeys.WholesalersKey, FirmEnglish(0));
        }

        /// <summary>How a supplier treats a customer who cannot pay on the day, in words, with the catalogue's own days and fee.</summary>
        public static string Late(VentWords w, LatePolicy late)
        {
            int at = Math.Max(0, Math.Min(2, (int)late));
            return w.F(SupplierKeys.LateKeys[at], LateEnglish(late), "days", SupplierCatalogue.GraceDays[at].ToString(), "fee", Percent(SupplierCatalogue.LateFee[at]),
                "most", SupplierCatalogue.WithheldMost.ToString());
        }

        private static string Percent(int thousandths)
        {
            return (thousandths / 10f).ToString(thousandths % 10 == 0 ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }

        // ---- the suppliers

        private static void Suppliers(VentModel m, VentView v, SupplyFacts f, ISupplyActions act, VentWords w, VentRuns runs)
        {
            var left = new List<VentBlock>(4);
            var right = new List<VentBlock>(16);
            left.Add(VentBlock.Section(w.T("reallife_supply_sec_place", "Who brings the stock")));
            if (f.Places.Count == 0)
            {
                left.Add(VentBlock.Note(w.T("reallife_supply_no_places", "None of your businesses sells goods a supplier brings yet. Open a shop and put its goods on the shelves, "
                    + "or give a purchasing agent a plan for a warehouse.")));
                m.Left = left.ToArray();
                m.Right = right.ToArray();
                return;
            }
            int place = Math.Max(0, Math.Min(f.Places.Count - 1, f.Place));
            SupplyPlaceFact at = f.Places[place];
            left.Add(VentBlock.Stepper(w.T("reallife_supply_step_place", "Deliver to"), at.Name, runs.Add(() => { v.A = Math.Max(0, place - 1); return true; }), place > 0, place < f.Places.Count - 1));
            runs.Add(() => { v.A = Math.Min(f.Places.Count - 1, place + 1); return true; });
            int picked = 0;
            for (int i = 0; i < f.Firms.Count; i++)
            {
                picked = v.Pick == FirmKey + f.Firms[i].Id ? i : picked;
            }
            var items = new List<VentItem>(f.Firms.Count);
            for (int i = 0; i < f.Firms.Count; i++)
            {
                SupplyFirmFact s = f.Firms[i];
                if (s.Id != SupplierCatalogue.Wholesalers && s.Why == Refusal.Kind)
                {
                    picked = picked == i ? 0 : picked;
                    continue;   // it trades nothing this place sells
                }
                string key = FirmKey + s.Id;
                bool offers = s.Id != SupplierCatalogue.Wholesalers && s.Why == Refusal.None;
                items.Add(new VentItem
                {
                    Icon = s.Id == SupplierCatalogue.Wholesalers ? UiIcon.Store : UiIcon.Truck, Title = FirmName(w, s.Id), Sub = Kinds(w, s.Kinds),
                    Chip = !s.Open ? w.T("reallife_supply_chip_shut", "Shut") : s.TroubleKind != 0 ? w.T("reallife_supply_chip_trouble", "In trouble") : null,
                    ChipTone = UiRole.Warn,
                    Amount = offers ? w.Money(s.Value / 100f) : string.Empty, Run = runs.Add(() => { v.Pick = key; return true; })
                });
            }
            left.Add(new VentBlock { Kind = VentBlockKind.Items, Items = items.ToArray() });
            SupplyFirmFact p = f.Firms[picked];
            right.Add(VentBlock.Section(FirmName(w, p.Id)));
            if (p.Id == SupplierCatalogue.Wholesalers)
            {
                right.Add(VentBlock.Note(w.T("reallife_supply_wholesalers_about", "The city's wholesale stores are as they always were: buy from their shelves, or set a weekly delivery "
                    + "on the Deliveries page of BizMan. Their prices follow the market every day, and they hold no price for you.")));
                for (int k = 0; k < f.Kinds.Length; k++)
                {
                    right.Add(VentBlock.Row(w.T(SupplierCatalogue.KindKeys[k], KindEnglish(k)), Against(w, f.Kinds[k].Factor),
                        f.Kinds[k].Factor > 1050 ? UiRole.Warn : UiRole.Text));
                }
                m.Left = left.ToArray();
                m.Right = right.ToArray();
                return;
            }
            right.Add(VentBlock.Note(w.T("reallife_supply_firm_about_" + p.Id, AboutEnglish(p.Id))));
            right.Add(VentBlock.Row(w.T("reallife_supply_row_owner", "Owner"),
                string.IsNullOrEmpty(p.OwnerName) ? w.T("reallife_supply_owner_own", "Its own") : w.F("reallife_supply_owner_rival", "{rival}'s holding", "rival", p.OwnerName)));
            if (!string.IsNullOrEmpty(p.Depot))
            {
                right.Add(VentBlock.Row(w.T("reallife_supply_row_depot", "Depot"), p.Depot));
            }
            right.Add(VentBlock.Row(w.T("reallife_supply_row_ontime", "Whole and on the day"),
                w.F("reallife_supply_ontime_value", "{share} in calm weather, {missed} of its last {all} were not", "share", Percent(p.Reliability), "missed", p.Missed.ToString(),
                    "all", (p.Brought + p.Missed).ToString())));
            right.Add(VentBlock.Row(w.T("reallife_supply_row_lead", "First delivery"), w.F("reallife_supply_lead_value", "in {n} days", "n", Math.Max(1, p.LeadDays).ToString())));
            right.Add(VentBlock.Row(w.T("reallife_supply_row_min", "Smallest delivery"), w.Money(p.Min / 100f)));
            right.Add(VentBlock.Row(w.T("reallife_supply_row_late", "If you cannot pay on the day"), Late(w, p.Late)));
            right.Add(VentBlock.Row(w.T("reallife_supply_row_health", "Its books"), w.T(SupplierKeys.HealthKeys[Math.Max(0, Math.Min(2, p.Band))], HealthEnglish(p.Band)),
                p.Band == 2 ? UiRole.Warn : UiRole.Text));
            right.Add(VentBlock.Row(w.T("reallife_supply_row_customers", "Shops of the city it serves"), p.Customers.ToString()));
            if (!p.Open)
            {
                right.Add(VentBlock.Note(w.F("reallife_supply_shut_note", "It went under. New owners open it on day {day}.", "day", p.ReopenDay.ToString()), UiRole.Warn));
            }
            else if (p.TroubleKind != 0)
            {
                right.Add(VentBlock.Note(w.F("reallife_supply_trouble_note", "{trouble} until day {day}. Its deliveries are more often late or short meanwhile.",
                    "trouble", w.T(SupplierKeys.TroubleKeys[Math.Max(0, Math.Min(2, p.TroubleKind))], TroubleEnglish(p.TroubleKind)), "day", p.TroubleUntil.ToString()), UiRole.Warn));
            }
            int term = Math.Max(0, Math.Min(SupplierCatalogue.TermWeeks.Length, f.TermStep)), share = Math.Max(0, Math.Min(SupplierCatalogue.Shares.Length - 1, f.ShareStep));
            right.Add(VentBlock.Stepper(w.T("reallife_supply_step_term", "Term"), Term(w, term), runs.Add(() => { v.B = Math.Max(0, term - 1) + 1; return true; }), term > 0,
                term < SupplierCatalogue.TermWeeks.Length));
            runs.Add(() => { v.B = Math.Min(SupplierCatalogue.TermWeeks.Length, term + 1) + 1; return true; });
            right.Add(VentBlock.Stepper(w.T("reallife_supply_step_share", "Quantity"),
                w.F("reallife_supply_share_value", "{share} of last week's sales", "share", Percent(SupplierCatalogue.Shares[share])),
                runs.Add(() => { v.C = Math.Max(0, share - 1) + 1; return true; }), share > 0, share < SupplierCatalogue.Shares.Length - 1));
            runs.Add(() => { v.C = Math.Min(SupplierCatalogue.Shares.Length - 1, share + 1) + 1; return true; });
            bool can = p.Why == Refusal.None;
            if (can)
            {
                right.Add(VentBlock.Row(w.T("reallife_supply_row_delivery", "Each delivery"),
                    w.F("reallife_supply_delivery_value", "{amount} for {n} products", "amount", w.Money(p.Value / 100f), "n", p.Lines.ToString())));
                right.Add(VentBlock.Row(w.T("reallife_supply_row_price", "Its price"), Against(w, p.Against), p.Against <= 1000 ? UiRole.Ok : UiRole.Text));
                right.Add(VentBlock.Note(term > 0
                    ? w.F("reallife_supply_take_or_pay", "Take or pay: every delivery is paid for in full, whether your shelves have room or not. Ending early costs {share} of what "
                        + "is still to come.", "share", Percent(SupplierCatalogue.BreakShare))
                    : w.T("reallife_supply_once_note", "One delivery at today's price. It is paid for in full when it comes, whether your shelves have room or not.")));
            }
            else
            {
                right.Add(VentBlock.Note(w.T(SupplierKeys.RefusalKeys[Math.Max(0, Math.Min(SupplierKeys.RefusalKeys.Length - 1, (int)p.Why))], RefusalEnglish(p.Why)), UiRole.Warn));
            }
            int supplier = p.Id, weeks = SupplyFacts.Weeks(term);
            string placeKey = at.Key;
            right.Add(VentBlock.Buttons(new UiActionModel
            {
                Label = term > 0 ? w.T("reallife_supply_sign", "Sign the contract") : w.T("reallife_supply_order", "Order once"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check,
                Key = UiAction.Alt, Disabled = !can, Arg = runs.Add(() => { act.Sign(supplier, placeKey, weeks, share); return true; })
            }));
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        // ---- the contracts

        /// <summary>The chip of a contract: how it stands, or how its next delivery will come.</summary>
        public static string Chip(VentWords w, SupplyContractFact c, out UiRole tone)
        {
            tone = UiRole.None;
            if (!c.Running)
            {
                tone = c.State == ContractState.Done || c.State == ContractState.Left ? UiRole.None : UiRole.Warn;
                return w.T(SupplierKeys.StateKeys[Math.Max(0, Math.Min(6, (int)c.State))], StateEnglish(c.State));
            }
            if (c.Owed > 0)
            {
                tone = UiRole.Warn;
                return w.T("reallife_supply_chip_owes", "Unpaid");
            }
            if (c.Out == Arrival.Late || c.Out == Arrival.Short)
            {
                tone = UiRole.Warn;
                return c.Out == Arrival.Late ? w.T("reallife_supply_chip_late", "Late") : w.T("reallife_supply_chip_short", "Short");
            }
            tone = UiRole.Ok;
            return w.T("reallife_supply_state_running", "Running");
        }

        private static void Contracts(VentModel m, VentView v, SupplyFacts f, ISupplyActions act, VentWords w, VentRuns runs)
        {
            var left = new List<VentBlock>(4);
            var right = new List<VentBlock>(24);
            left.Add(VentBlock.Section(w.T("reallife_supply_sec_contracts", "Your contracts")));
            if (f.Owed > 0)
            {
                left.Add(VentBlock.Note(w.F("reallife_supply_owed_note", "You owe suppliers {amount}. It is taken as soon as you have the cash, and nobody offers you terms meanwhile.",
                    "amount", w.Money(f.Owed / 100f)), UiRole.Warn));
            }
            if (f.Contracts.Count == 0)
            {
                left.Add(VentBlock.Note(w.T("reallife_supply_contracts_none", "No contracts yet. Choose a supplier on the Suppliers tab.")));
                if (f.Owed > 0)
                {
                    right.Add(VentBlock.Buttons(new UiActionModel { Label = w.T("reallife_supply_pay_owed", "Pay what is owed"), Kind = UiButtonKind.Primary, Icon = UiIcon.Coin,
                        Key = UiAction.Alt, Disabled = f.Cash <= 0, Arg = runs.Add(() => { act.PayOwed(); return true; }) }));
                }
                m.Left = left.ToArray();
                m.Right = right.ToArray();
                return;
            }
            int picked = 0;
            for (int i = 0; i < f.Contracts.Count; i++)
            {
                picked = v.Pick == ContractKey + f.Contracts[i].Id ? i : picked;
            }
            var items = new VentItem[f.Contracts.Count];
            for (int i = 0; i < items.Length; i++)
            {
                SupplyContractFact c = f.Contracts[i];
                string key = ContractKey + c.Id;
                string chip = Chip(w, c, out UiRole tone);
                items[i] = new VentItem
                {
                    Icon = UiIcon.Truck, Title = FirmName(w, c.Supplier),
                    Sub = c.Running ? w.F("reallife_supply_item_sub", "{business}, next delivery day {day}", "business", c.Place, "day", (c.Due + c.Slip).ToString())
                        : w.F("reallife_supply_item_sub_ended", "{business}, ended day {day}", "business", c.Place, "day", c.Ended.ToString()),
                    Chip = chip, ChipTone = tone, Amount = w.Money(c.Value / 100f), Run = runs.Add(() => { v.Pick = key; return true; })
                };
            }
            left.Add(new VentBlock { Kind = VentBlockKind.Items, Items = items });
            SupplyContractFact p = f.Contracts[picked];
            right.Add(VentBlock.Section(w.F("reallife_supply_sec_contract", "{supplier} for {business}", "supplier", FirmName(w, p.Supplier), "business", p.Place)));
            right.Add(VentBlock.Row(w.T("reallife_supply_row_term", "Term"), p.Weeks > 0
                ? w.F("reallife_supply_term_done", "{made} of {all} deliveries", "made", p.Made.ToString(), "all", p.Deliveries.ToString()) : w.T("reallife_supply_term_once", "One delivery, today's price")));
            if (p.Running)
            {
                right.Add(VentBlock.Row(w.T("reallife_supply_row_next", "Next delivery"), w.F("reallife_supply_next_value", "day {day}", "day", (p.Due + p.Slip).ToString())));
                if (p.Out == Arrival.Late)
                {
                    right.Add(VentBlock.Note(w.F("reallife_supply_next_late", "It comes {days} days late: {cause}.", "days", p.Slip.ToString(),
                        "cause", w.T(SupplierKeys.CauseKeys[(int)p.Why], CauseEnglish(p.Why))), UiRole.Warn));
                }
                else if (p.Out == Arrival.Short)
                {
                    right.Add(VentBlock.Note(w.F("reallife_supply_next_short", "It brings {share} less: {cause}.", "share", Percent(1000 - p.OutShare),
                        "cause", w.T(SupplierKeys.CauseKeys[(int)p.Why], CauseEnglish(p.Why))), UiRole.Warn));
                }
            }
            right.Add(VentBlock.Row(w.T("reallife_supply_row_delivery", "Each delivery"), w.Money(p.Value / 100f)));
            if (p.Running && p.MarketValue > 0)
            {
                int against = (int)(p.Value * 1000 / p.MarketValue);
                right.Add(VentBlock.Row(w.T("reallife_supply_row_price", "Its price"), Against(w, against), against <= 1000 ? UiRole.Ok : UiRole.Warn));
            }
            right.Add(VentBlock.Row(w.T("reallife_supply_row_paid", "Paid so far"), w.Money(p.Paid / 100f)));
            if (p.Credit > 0)
            {
                right.Add(VentBlock.Row(w.T("reallife_supply_row_credit", "Credit against the next payment"), w.Money(p.Credit / 100f), UiRole.Ok));
            }
            if (p.Owed > 0)
            {
                right.Add(VentBlock.Row(w.T("reallife_supply_row_owed", "Owed on it"), w.Money(p.Owed / 100f), UiRole.Warn));
            }
            if (p.Failures > 0)
            {
                right.Add(VentBlock.Row(w.T("reallife_supply_row_failures", "Deliveries it failed"),
                    w.F("reallife_supply_failures_value", "{n} of the {most} that let you leave", "n", p.Failures.ToString(), "most", SupplierCatalogue.FailuresToLeave.ToString())));
            }
            if (p.NotTaken > 0)
            {
                right.Add(VentBlock.Row(w.T("reallife_supply_row_not_taken", "Paid for and not taken, no room"), p.NotTaken.ToString(), UiRole.Warn));
            }
            for (int i = 0; i < p.Lines.Count; i++)
            {
                right.Add(VentBlock.Row(p.Lines[i].Name, w.F("reallife_supply_line_value", "{units} at {price}", "units", p.Lines[i].Units.ToString(), "price", w.Price((p.Lines[i].Mills + 5) / 10))));
            }
            if (!p.Running)
            {
                m.Left = left.ToArray();
                m.Right = right.ToArray();
                return;
            }
            int id = p.Id;
            if (p.Weeks > 0)
            {
                right.Add(VentBlock.Section(w.T("reallife_supply_sec_rules", "Rules for your buyer")));
                right.Add(VentBlock.Note(p.Buyer ? w.T("reallife_supply_rules_buyer", "A manager who orders stock here follows these rules for you.")
                    : w.T("reallife_supply_rules_no_buyer", "Nobody orders stock here, so the rules wait: appoint a manager with stock orders on, and you are told what to decide meanwhile."),
                    p.Buyer ? UiRole.None : UiRole.Warn));
                right.Add(VentBlock.Choice(w.T("reallife_supply_rule_renew", "When the term ends"), w.T(SupplierKeys.RenewKeys[(int)p.Renew], RenewEnglish(p.Renew)),
                    runs.Add(() => { act.Rule(id, SupplierCatalogue.RuleRenew); return true; })));
                int limit = SupplierCatalogue.RenewLimits[Math.Max(0, Math.Min(SupplierCatalogue.RenewLimits.Length - 1, p.RenewLimit))];
                right.Add(VentBlock.Choice(w.T("reallife_supply_rule_limit", "Renew only up to"),
                    limit >= 100000 ? w.T("reallife_supply_limit_any", "Any price") : limit == 0 ? w.T("reallife_supply_limit_market", "The market price")
                        : w.F("reallife_supply_limit_value", "{share} over the market price", "share", Percent(limit)),
                    runs.Add(() => { act.Rule(id, SupplierCatalogue.RuleLimit); return true; }), p.Renew == RenewRule.Off));
                right.Add(VentBlock.Switch(w.T("reallife_supply_rule_cover", "Cover a late or short delivery from another supplier"), p.Cover,
                    runs.Add(() => { act.Rule(id, SupplierCatalogue.RuleCover); return true; })));
                right.Add(VentBlock.Switch(w.T("reallife_supply_rule_leave", "Leave and switch when it has failed too often"), p.LeaveRule,
                    runs.Add(() => { act.Rule(id, SupplierCatalogue.RuleLeave); return true; })));
            }
            var actions = new List<UiActionModel>(3);
            if (f.Owed > 0)
            {
                actions.Add(new UiActionModel { Label = w.T("reallife_supply_pay_owed", "Pay what is owed"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Coin, Disabled = f.Cash <= 0,
                    Arg = runs.Add(() => { act.PayOwed(); return true; }) });
            }
            if (p.MayLeave)
            {
                actions.Add(new UiActionModel { Label = w.T("reallife_supply_leave", "Leave, nothing to pay"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt,
                    Arg = runs.Add(() => { act.Leave(id); return true; }) });
            }
            else
            {
                actions.Add(new UiActionModel
                {
                    Label = w.F("reallife_supply_end", "End it for {amount}", "amount", w.Money(p.BreakCost / 100f)), Kind = UiButtonKind.Destructive, Icon = UiIcon.Close,
                    Arg = runs.Add(() => { act.End(id); return true; })
                });
            }
            right.Add(VentBlock.Buttons(actions.ToArray()));
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        // ---- the market

        private static void Market(VentModel m, SupplyFacts f, VentWords w)
        {
            var left = new List<VentBlock>(10);
            left.Add(VentBlock.Section(w.T("reallife_supply_sec_market", "Prices today, by kind of goods")));
            var items = new VentItem[f.Kinds.Length];
            for (int k = 0; k < items.Length; k++)
            {
                SupplyKindFact kind = f.Kinds[k];
                string chip = kind.Shock == 1 ? (kind.StartsIn > 0 ? w.T("reallife_supply_chip_shortage_soon", "Shortage tomorrow") : w.T("reallife_supply_chip_shortage", "Shortage"))
                    : kind.Shock == 2 ? w.T("reallife_supply_chip_glut", "Glut") : null;
                items[k] = new VentItem
                {
                    Icon = KindIcons[k], Title = w.T(SupplierCatalogue.KindKeys[k], KindEnglish(k)),
                    Sub = kind.Shock != 0 ? w.F("reallife_supply_shock_sub", "{days} days more, about {share}", "days", (kind.StartsIn + kind.Days).ToString(), "share", Percent(kind.Size))
                        : w.T("reallife_supply_calm_sub", "Nothing out of the ordinary"),
                    Chip = chip, ChipTone = kind.Shock == 1 ? UiRole.Warn : UiRole.Ok, Amount = Percent(kind.Factor), Run = -1
                };
            }
            left.Add(new VentBlock { Kind = VentBlockKind.Items, Items = items });
            left.Add(VentBlock.Note(w.T("reallife_supply_market_note", "100% is the calm price. A shortage raises the price for everyone, the rival shops too, and suppliers ration "
                + "what they bring. A fixed price holds through it.")));
            var right = new List<VentBlock>(14);
            right.Add(VentBlock.Section(w.T("reallife_supply_sec_record", "Your record with suppliers")));
            right.Add(VentBlock.Row(w.T("reallife_supply_row_on_time", "Deliveries paid on the day"), f.OnTime.ToString()));
            right.Add(VentBlock.Row(w.T("reallife_supply_row_paid_late", "Paid late or not at all"), f.Late.ToString(), f.Late > 0 ? UiRole.Warn : UiRole.Text));
            right.Add(VentBlock.Row(w.T("reallife_supply_row_defaults", "Contracts ended unpaid"), f.Defaults.ToString(), f.Defaults > 0 ? UiRole.Warn : UiRole.Text));
            right.Add(VentBlock.Section(w.T("reallife_supply_sec_news", "The suppliers")));
            if (f.News.Count == 0)
            {
                right.Add(VentBlock.Note(w.T("reallife_supply_news_none", "Nothing to report. Every supplier is trading.")));
            }
            for (int i = 0; i < f.News.Count; i++)
            {
                right.Add(VentBlock.Note(f.News[i]));
            }
            right.Add(VentBlock.Section(w.T("reallife_supply_sec_rivals", "The rivals")));
            if (f.Session)
            {
                right.Add(VentBlock.Note(w.T("reallife_supply_rivals_session", "In a Going Public session the rival tycoons rest, and the city's shops are the host's.")));
            }
            else if (f.Rivals.Count == 0)
            {
                right.Add(VentBlock.Note(w.T("reallife_supply_rivals_none", "The city's shops buy from the same suppliers and are hit by the same shortages. No tycoon's outlets "
                    + "have anything to report this month.")));
            }
            for (int i = 0; i < f.Rivals.Count; i++)
            {
                right.Add(VentBlock.Note(f.Rivals[i]));
            }
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        // ---- the English the samples and the checks read

        public static string KindEnglish(int k) => k == 0 ? "Fresh produce and flowers" : k == 1 ? "Groceries and drink" : k == 2 ? "Clothing and gifts"
            : k == 3 ? "Electronics and appliances" : k == 4 ? "Home and office goods" : "General goods";

        public static string FirmEnglish(int id)
        {
            switch (id)
            {
                case 1: return "Kettle and Crane Provisions";
                case 2: return "Penny Barrow Cash and Carry";
                case 3: return "Three Lanterns Grocers Supply";
                case 4: return "Bolt and Bobbin Cloth Hall";
                case 5: return "Silver Thimble Imports";
                case 6: return "Loom and Ledger Trading";
                case 7: return "Copper Coil Wholesale";
                case 8: return "Northlight Tech Distribution";
                case 9: return "Oak and Ink Home and Office";
                default: return "The city's wholesale stores";
            }
        }

        public static string AboutEnglish(int id)
        {
            switch (id)
            {
                case 1: return "A dear and dependable house for fresh goods and groceries. It delivers the next day, takes small orders and gives a customer a week to pay.";
                case 2: return "The cheapest for food and general goods, for those who buy in bulk. It is slow, it is late more often, and it brings nothing without payment.";
                case 3: return "A steady grocers' supplier between the two: fair prices, two days to deliver, four days to pay with a fee on top.";
                case 4: return "Cloth and clothing at the lowest prices in the trade, in large lots only. Slow, less dependable, and strict about payment.";
                case 5: return "An importer of clothing and gifts that charges more and seldom lets a shop down. Small orders welcome, a week to pay.";
                case 6: return "A trading house for clothing and home goods with fair prices and few surprises.";
                case 7: return "Electronics and home goods by the pallet at low prices. Four days to deliver, late more often, nothing without payment.";
                case 8: return "A dear and dependable distributor of electronics. Two days to deliver and a week to pay.";
                default: return "Home, office and general goods, with some electronics, at fair prices and steady deliveries.";
            }
        }

        public static string LateEnglish(LatePolicy late) => late == LatePolicy.Lenient ? "It delivers and gives you {days} days, no fee"
            : late == LatePolicy.Firm ? "It delivers and gives you {days} days, with a {fee} fee" : "It brings nothing, and ends the contract after {most} such days";

        public static string HealthEnglish(int band) => band == 0 ? "Sound" : band == 1 ? "Stretched" : "In trouble";

        public static string TroubleEnglish(int kind) => kind == 1 ? "A drivers' strike" : kind == 2 ? "A fire at its depot" : "No trouble";

        public static string RenewEnglish(RenewRule rule) => rule == RenewRule.Off ? "Ask me" : rule == RenewRule.Same ? "Renew with the same supplier" : "Take the cheapest offer";

        public static string StateEnglish(ContractState state)
        {
            switch (state)
            {
                case ContractState.Done: return "Term ended";
                case ContractState.Broken: return "Ended by you";
                case ContractState.Left: return "Left by right";
                case ContractState.Failed: return "Supplier went under";
                case ContractState.Unpaid: return "Ended unpaid";
                case ContractState.Closed: return "Business gone";
                default: return "Running";
            }
        }

        public static string CauseEnglish(Cause why)
        {
            switch (why)
            {
                case Cause.Own: return "the supplier's own doing";
                case Cause.Trouble: return "trouble at the supplier's depot";
                case Cause.Cash: return "the supplier is short of money";
                case Cause.Weather: return "the weather";
                case Cause.Streets: return "streets closed for a city event";
                case Cause.Season: return "fresh goods are scarce in winter";
                case Cause.Shortage: return "a shortage across the whole market";
                default: return "no cause";
            }
        }

        public static string RefusalEnglish(Refusal why)
        {
            switch (why)
            {
                case Refusal.Shut: return "It has gone under and is not trading.";
                case Refusal.Kind: return "It trades nothing this business sells.";
                case Refusal.NoGoods: return "This business has nothing a supplier could bring yet.";
                case Refusal.Small: return "The delivery is under its smallest. Raise the quantity or choose a supplier that takes small orders.";
                case Refusal.Owes: return "You owe a supplier money. Nobody offers terms until it is paid.";
                case Refusal.Sour: return "A contract with it ended unpaid. It offers you nothing for now.";
                case Refusal.Credit: return "Your credit rating is under what it asks of a customer.";
                case Refusal.Full: return "You have as many contracts running as suppliers will keep with one customer.";
                case Refusal.Held: return "This business already has a fixed price contract with it. Order once, or wait for the term to end.";
                case Refusal.Off: return "Suppliers are not running.";
                default: return "It offers these terms.";
            }
        }
    }
}
