using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    internal sealed partial class CompanyLive
    {
        private static readonly string[] LicenceWhyKeys =
        {
            "reallife_co_lic_why_hygiene", "reallife_co_lic_why_alcohol", "reallife_co_lic_why_late", "reallife_co_lic_why_signage",
            "reallife_co_lic_why_fire", "reallife_co_lic_why_seating", "reallife_co_lic_why_delivery", "reallife_co_lic_why_bonded"
        };

        // One glyph a kind of licence.
        private static readonly UiIcon[] LicenceIcons =
        {
            UiIcon.Drop, UiIcon.Coffee, UiIcon.Moon, UiIcon.Tag, UiIcon.Fire, UiIcon.Sun, UiIcon.Truck, UiIcon.Lock
        };

        // Tax and licences: what needs the owner (the return, or the tax reserve falling short), every licence the businesses need with how it stands and what to do, what
        // is coming up; at the right the quarter so far, the reserve, the returns paid, the tools and the property tax. A sub page is one card.
        private void ReadTax(CompanyFacts f, CompanyDirector d, CompanyView v, Words w)
        {
            CompanyTaxFacts t = f.Tax;
            bool tax = d.On(CompanyPart.Tax), licences = d.On(CompanyPart.Licences);
            if (!tax && !licences)
            {
                t.PartOff = true;
                t.OffTitle = w.T("reallife_co_nav_tax", "Tax and licences");
                t.OffBody = CompanyText.T("reallife_co_part_off");
                return;
            }
            CompanyState s = d.State;
            TaxFile tf = s.Tax;
            int day = d.Today;
            // The licences.
            var lic = new List<CompanyLicence>(16);
            var coming = new List<KeyValuePair<int, CompanyDone>>(8);
            IReadOnlyList<CompanyShop> shops = d.Shops;
            for (int i = 0; licences && i < shops.Count; i++)
            {
                for (int k = 0; k < CompanyCatalogue.LicenceKinds; k++)
                {
                    if (!LicenceRules.Needed(shops[i].Needs, k))
                    {
                        continue;
                    }
                    Licence l = shops[i].File.Permits[k];
                    bool valid = LicenceRules.Valid(l, day), soon = valid && l.Until - day < CompanyCatalogue.LicenceRemindDays, pending = !valid && l.Pending;
                    ManagedPlace mp = d.ManagersOn ? d.ManagedAt(shops[i].File.Place) : null;
                    var row = new CompanyLicence
                    {
                        Id = "lic:" + shops[i].File.Place + ":" + k, Icon = LicenceIcons[Math.Min(k,
                            LicenceIcons.Length - 1)], Name = CompanyText.Capital(CompanyText.Licence(k)), Place = shops[i].Name,
                        Manager = mp != null && mp.Own != null ? mp.Own.Name : null, ManagerHue = mp != null && mp.Own != null ? UiColour.HueOf(mp.Own.Name) : 0f,
                        State = CompanyText.T(pending ? "reallife_co_row_lic_pending" : !valid ? "reallife_co_row_lic_missing" : l.Auto ? "reallife_co_row_lic_auto" : soon
                            ? "reallife_co_row_lic_soon" : "reallife_co_row_lic_valid"),
                        StateTone = pending ? UiRole.Warn : !valid ? UiRole.Danger : soon && !l.Auto ? UiRole.Warn : UiRole.Ok,
                        Day = valid ? CompanyText.Day(l.Until) : pending ? CompanyText.Day(l.Decide) : null,
                        Open = CompanyAct.Of(CompanyDo.Go, "lic:" + shops[i].File.Place + ":" + k, CompanyView.Tax, CompanyView.SubLicence)
                    };
                    if (!pending && (!valid || (soon && !l.Auto)))
                    {
                        row.HasAction = true;
                        row.Action = Btn(CompanyText.T(valid ? "reallife_co_lic_renew" : "reallife_co_lic_buy"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.LicenceBuy, shops[i].File.Place, k),
                            UiAction.Count, UiIcon.Check);
                    }
                    lic.Add(row);
                    if (valid && l.Until - day <= 14)
                    {
                        coming.Add(new KeyValuePair<int, CompanyDone>(l.Until, new CompanyDone
                        {
                            When = CompanyText.Day(l.Until), Text = CompanyText.T("reallife_co_coming_licence", "licence", CompanyText.Licence(k), "business", shops[i].Name), Money = l.Auto
                                ? CompanyText.Money(LicenceRules.Fee(k, d.Index)) : null,
                            Tone = l.Auto ? -1 : 0, Open = CompanyAct.Of(CompanyDo.Go, "lic:" + shops[i].File.Place + ":" + k, CompanyView.Tax, CompanyView.SubLicence)
                        }));
                    }
                }
            }
            t.Licences = lic.ToArray();
            t.LicencesTitle = w.T("reallife_co_licences", "Licences and permits");
            t.LicencesCount = lic.Count > 0 ? lic.Count.ToString() : null;
            t.LicencesEmpty = CompanyText.T("reallife_co_lic_intro");
            t.ExtraTitle = w.T("reallife_co_lic_not_needed", "Permits this business does not need");
            t.Extra = PermitsNotNeeded(d, w);
            if (!tax)
            {
                t.ComingUp = ComingOf(coming);
                if (v.Sub[CompanyView.Tax] == CompanyView.SubLicence)
                {
                    t.Card = TaxSubCard(d, v, w, default(TaxRules.QuarterFigures), tf, day);
                }
                return;
            }
            // The quarter so far, the reserve and the returns.
            TaxRules.QuarterFigures q = d.QuarterNow();
            t.QuarterTitle = w.T("reallife_co_quarter_so_far", "This quarter so far");
            t.QuarterRange = w.F("reallife_co_quarter_range", "Q{quarter}, day {from} to {to}", "quarter", tf.Quarter.ToString(), "from", tf.QuarterFrom.ToString(), "to", (tf.QuarterFrom
                + d.QuarterDays - 1).ToString());
            bool noTakings = day <= tf.QuarterFrom;
            var lines = new List<CompanyStatementLine>(8);
            if (!noTakings)
            {
                lines.Add(new CompanyStatementLine { Label = w.T("reallife_co_q_takings", "Takings"), Value = CompanyText.Money(q.Takings) });
            }
            lines.Add(new CompanyStatementLine { Label = w.T("reallife_co_q_costs", "Costs that count"), Value = CompanyText.Money(q.Costs) });
            if (!noTakings)
            {
                lines.Add(new CompanyStatementLine { Label = w.T("reallife_co_q_taxable", "Taxable profit"), Value = CompanyText.Money(q.Taxable), Strong = true });
                lines.Add(new CompanyStatementLine { Label = w.F("reallife_co_q_tax", "Business tax at {percent}%", "percent", q.Percent.ToString()), Value = CompanyText.Money(q.Tax) });
            }
            if (q.Reserve > 0 || q.Still > 0)
            {
                lines.Add(new CompanyStatementLine { Label = w.T("reallife_co_q_set_aside", "Set aside"), Value = CompanyText.Money(q.Reserve) });
                lines.Add(new CompanyStatementLine { Label = w.T("reallife_co_q_still", "Still to set aside"), Value = CompanyText.Money(q.Still), Strong = true, Tone = q.Still > 0 ? UiRole.WarnText
                    : UiRole.OkText });
            }
            t.Quarter = lines.ToArray();
            t.QuarterLen = d.QuarterDays;
            t.QuarterDay = Math.Max(0, Math.Min(d.QuarterDays, day - tf.QuarterFrom + 1));
            t.QuarterDayText = w.F("reallife_co_quarter_day", "Day {day} of {days}", "day", t.QuarterDay.ToString(), "days", d.QuarterDays.ToString());
            ReserveFacts(t, d, v, w, q, tf);
            t.TermsTitle = w.T("reallife_co_acc_terms_sec", "What an accountant costs");
            t.Terms = AccountantTerms(d, tf, w);
            // The lead: the return waiting for the owner, else the reserve falling short.
            TaxLead(t, d, v, w, q, tf, day);
            // Coming up.
            if (tf.ReturnWaiting)
            {
                coming.Add(new KeyValuePair<int, CompanyDone>(tf.ReturnDue, new CompanyDone
                {
                    When = CompanyText.Day(tf.ReturnDue), Text = CompanyText.T("reallife_co_coming_return", "quarter", tf.ReturnQuarter.ToString()), Money = null,
                    Open = CompanyAct.Of(CompanyDo.Go, "ret", CompanyView.Tax, CompanyView.SubReturn)
                }));
            }
            else
            {
                coming.Add(new KeyValuePair<int, CompanyDone>(tf.QuarterFrom + d.QuarterDays, new CompanyDone
                {
                    When = CompanyText.Day(tf.QuarterFrom + d.QuarterDays), Text = CompanyText.T("reallife_co_coming_quarter", "quarter", tf.Quarter.ToString()), Open = CompanyAct.Of(CompanyDo.Go,
                        "ret", CompanyView.Tax, CompanyView.SubReturn)
                }));
            }
            if (d.ReserveChief != null && tf.ReserveAuto && q.Still > 0)
            {
                coming.Add(new KeyValuePair<int, CompanyDone>(d.ReserveTopUpDay, new CompanyDone
                {
                    When = CompanyText.Day(d.ReserveTopUpDay), Text = CompanyText.T("reallife_co_coming_topup", "name", d.ReserveChief.Name), Money = CompanyText.Money(q.Still), Tone = -1
                }));
            }
            if (tf.Accountant >= 0)
            {
                coming.Add(new KeyValuePair<int, CompanyDone>(tf.AccountantNext, new CompanyDone
                {
                    When = CompanyText.Day(tf.AccountantNext), Text = CompanyText.T("reallife_co_coming_accountant", "name", CompanyText.Accountant(tf.Accountant)),
                    Money = CompanyText.Money(TaxRules.Fee(tf.Accountant, d.Index, d.Hidden() > 0)), Tone = -1, Open = CompanyAct.Of(CompanyDo.Go, "acc", CompanyView.Tax, CompanyView.SubAccountant)
                }));
            }
            t.ComingUp = ComingOf(coming);
            // Paid this year: the returns settled, the last few.
            t.PaidTitle = w.T("reallife_co_paid_year", "Paid this year");
            t.PaidEmpty = w.T("reallife_co_paid_none", "No return has been settled yet.");
            var paid = new List<CompanyItem>(TaxRules.SettledKept);
            for (int i = tf.Settled.Count - 1; i >= 0; i--)
            {
                SettledReturn r = tf.Settled[i];
                paid.Add(new CompanyItem
                {
                    Title = CompanyText.T("reallife_co_row_quarter", "quarter", r.Quarter.ToString()), Sub = CompanyText.Day(r.Day), Figure = CompanyText.Money(r.Tax),
                    Chip = CompanyText.T(r.State == TaxRules.StateOnTime ? "reallife_co_paid_on_time" : r.State == TaxRules.StateLate ? "reallife_ibox_chip_late" : "reallife_co_paid_missed"),
                    ChipTone = r.State == TaxRules.StateOnTime ? UiRole.Ok : r.State == TaxRules.StateLate ? UiRole.Warn : UiRole.Danger
                });
            }
            t.Paid = paid.ToArray();
            // The tools: the return, the accountant, the cash off the books of each business.
            t.ToolsTitle = w.T("reallife_co_tax_tools", "Return, accountant and books");
            bool late = tf.ReturnWaiting && day > tf.ReturnDue;
            var tools = new List<CompanyItem>(4)
            {
                new CompanyItem
                {
                    Title = CompanyText.T("reallife_co_row_return"), Sub = CompanyText.T("reallife_co_row_quarter", "quarter", (tf.ReturnWaiting ? tf.ReturnQuarter : tf.Quarter).ToString()),
                    Chip = CompanyText.T(late ? "reallife_co_row_return_late" : tf.ReturnWaiting ? "reallife_co_row_return_due" : "reallife_co_row_return_running"), ChipTone = late ? UiRole.Danger
                        : tf.ReturnWaiting ? UiRole.Warn : UiRole.Muted,
                    Press = true, Act = CompanyAct.Of(CompanyDo.Go, "ret", CompanyView.Tax, CompanyView.SubReturn)
                },
                new CompanyItem
                {
                    Title = CompanyText.T("reallife_co_row_accountant"), Sub = CompanyText.Accountant(tf.Accountant), Chip = null,
                    Figure = tf.Accountant >= 0 ? CompanyText.T("reallife_co_row_a_month", "amount", CompanyText.Money(TaxRules.Fee(tf.Accountant, d.Index, d.Hidden() > 0))) : null,
                    Press = true, Act = CompanyAct.Of(CompanyDo.Go, "acc", CompanyView.Tax, CompanyView.SubAccountant)
                }
            };
            for (int i = 0; Settings.EconomyCompanyHide && i < shops.Count && i < 4; i++)
            {
                tools.Add(new CompanyItem
                {
                    Title = shops[i].Name, Sub = CompanyText.T("reallife_co_row_hidden"), Figure = shops[i].File.Hide + "%", FigureTone = shops[i].File.Hide > 0 ? UiRole.WarnText : UiRole.None,
                    Press = true, Act = CompanyAct.Of(CompanyDo.Go, "hide:" + shops[i].File.Place, CompanyView.Tax, CompanyView.SubHide)
                });
            }
            t.Tools = tools.ToArray();
            // The property tax: of each building owned; a rented one has it inside the rent.
            t.PropertyTitle = w.T("reallife_co_property_tax", "Property tax");
            IReadOnlyList<CompanyGame.PropertyTaxLine> property = d.PropertyTax();
            var pl = new List<CompanyStatementLine>(4);
            for (int i = 0; i < property.Count && i < 4; i++)
            {
                pl.Add(new CompanyStatementLine { Label = property[i].Name, Value = CompanyText.Money(property[i].ToDate) });
            }
            t.Property = pl.ToArray();
            t.PropertyNote = property.Count == 0 ? CompanyText.T("reallife_co_property_rented") : CompanyText.T("reallife_co_property_note");
            if (v.Sub[CompanyView.Tax] != CompanyView.SubMain)
            {
                t.Card = TaxSubCard(d, v, w, q, tf, day);
                if (t.Card == null)
                {
                    v.Sub[CompanyView.Tax] = CompanyView.SubMain;
                }
            }
        }

        private static CompanyDone[] ComingOf(List<KeyValuePair<int, CompanyDone>> coming)
        {
            coming.Sort((a, b) => a.Key.CompareTo(b.Key));
            int n = Math.Min(5, coming.Count);
            var result = new CompanyDone[n];
            for (int i = 0; i < n; i++)
            {
                result[i] = coming[i].Value;
            }
            return result;
        }

        // The reserve: the figure to set aside (typed or stepped, never moved until the button), the finance chief's top-up and the release.
        private void ReserveFacts(CompanyTaxFacts t, CompanyDirector d, CompanyView v, Words w, TaxRules.QuarterFigures q, TaxFile tf)
        {
            int shown = v.Amount > 0 ? v.Amount : q.Still;
            int cash = CompanyGame.Cash;
            ExecutiveFile chief = d.ReserveChief;
            if (q.Tax <= 0 && q.Reserve <= 0 && chief == null)
            {
                t.ReserveControls = new CompanyBlock[0];   // no tax yet and nothing set aside: nothing to set aside, and nobody to do it
                return;
            }
            var blocks = new List<CompanyBlock>(4)
            {
                new CompanyBlock
                {
                    Kind = CompanyBlockKind.Stepper, Text = w.T("reallife_co_reserve_amount", "Set aside"), Sub = w.F("reallife_co_reserve_cash", "{amount} in the cash", "amount",
                        CompanyText.Money(cash)),
                    Value = CompanyText.Money(shown), CanLess = shown > 0, CanMore = true,
                    Act = CompanyAct.Of(CompanyDo.Go, "amount", -100, shown), Act2 = CompanyAct.Of(CompanyDo.Go, "amount", 100, shown), Act3 = CompanyAct.Of(CompanyDo.Go, "amount", 0, shown)
                },
                Buttons(
                    Btn(w.F("reallife_co_reserve_do", "Set aside {amount}", "amount", CompanyText.Money(shown)), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.ReserveSet, null, shown),
                        UiAction.Count, UiIcon.Check, false,
                        shown <= 0 || cash < shown, shown <= 0 ? w.T("reallife_co_reserve_none", "Nothing to set aside") : cash < shown ? w.T("reallife_co_reserve_short", "Not enough in the cash")
                            : null),
                    Btn(w.T("reallife_co_reserve_release", "Release"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.ReserveRelease), UiAction.Count, UiIcon.Back, true, tf.Reserve <= 0,
                        tf.Reserve <= 0 ? w.T("reallife_co_reserve_empty", "Nothing is set aside") : null))
            };
            if (chief != null)
            {
                blocks.Add(new CompanyBlock
                {
                    Kind = CompanyBlockKind.Switch, Text = w.T("reallife_co_reserve_auto", "Finance chief tops it up"), Sub = w.F("reallife_co_reserve_auto_on",
                        "{name} does it when a quarter closes", "name", chief.Name),
                    On = tf.ReserveAuto, Value = tf.ReserveAuto ? w.T("reallife_co_on", "On") : w.T("reallife_co_off", "Off"), Act = CompanyAct.Of(CompanyDo.ReserveAuto, null, tf.ReserveAuto ? 0 : 1)
                });
            }
            t.ReserveControls = blocks.ToArray();
        }

        // What needs the owner first: the return that waits, else the reserve falling short of the year's tax.
        private void TaxLead(CompanyTaxFacts t, CompanyDirector d, CompanyView v, Words w, TaxRules.QuarterFigures q, TaxFile tf, int day)
        {
            ExecutiveFile chief = d.ReserveChief;
            string owner = CompanyGame.OwnerName() ?? w.T("reallife_hier_you", "You");
            if (tf.ReturnWaiting)
            {
                bool late = day > tf.ReturnDue;
                t.HasLead = true;
                t.LeadKicker = w.T("reallife_co_tab_tax", "Tax and licences");
                t.LeadTitle = CompanyText.T(late ? "reallife_co_return_late" : "reallife_co_return_due", "day", tf.ReturnDue.ToString(), "amount",
                    CompanyText.Money(TaxRules.Penalty(tf.ReturnTakings)), "count", tf.ReturnPenalties.ToString());
                t.LeadBody = CompanyText.T("reallife_co_lead_return", "quarter", tf.ReturnQuarter.ToString(), "takings", CompanyText.Money(tf.ReturnTakings), "costs",
                    CompanyText.Money(tf.ReturnCosts));
                t.LeadAsker = owner;
                t.LeadHue = UiColour.HueOf(owner);
                FaceSources.Player(owner);
                bool order = CompanyGame.BooksInOrder();
                t.LeadButtons = new[]
                {
                    Btn(CompanyText.T("reallife_co_return_file"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.ReturnFile), UiAction.Alt, UiIcon.Check, false, !order, !order
                        ? CompanyText.T("reallife_co_return_books") : null),
                    Btn(w.T("reallife_co_open_return", "Open the return"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, "ret", CompanyView.Tax, CompanyView.SubReturn), UiAction.Option,
                        UiIcon.Next)
                };
                return;
            }
            if (q.Still > 0 && chief != null && !tf.ReserveAuto && day > tf.QuarterFrom)
            {
                t.HasLead = true;
                t.LeadKicker = chief != null ? w.F("reallife_co_lead_chief", "Finance chief, {name}", "name", chief.Name) : w.T("reallife_co_tab_tax", "Tax and licences");
                t.LeadTitle = w.F("reallife_co_lead_reserve", "The year's tax so far is {tax}. {set} is set aside.", "tax", CompanyText.Money(q.Year + q.Tax), "set", CompanyText.Money(q.Reserve));
                t.LeadBody = w.F("reallife_co_lead_reserve_body", "Setting aside {amount} puts it out of reach of the cash until the bill comes, so that the bill can be paid from it.", "amount",
                    CompanyText.Money(q.Still));
                string asker = chief != null ? chief.Name : owner;
                t.LeadAsker = asker;
                t.LeadHue = UiColour.HueOf(asker);
                if (chief != null)
                {
                    Face(chief.Name, chief.Id);
                }
                else
                {
                    FaceSources.Player(owner);
                }
                // The one thing the banner asks: that the finance chief does it. Setting an amount aside by hand is the stepper under the figures, in one place.
                var buttons = new List<CompanyButton>(1)
                {
                    Btn(w.F("reallife_co_reserve_let", "Let {name} do it", "name", FirstName(chief.Name)), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.ReserveAuto, null, 1), UiAction.Alt,
                        UiIcon.Handshake)
                };
                t.LeadButtons = buttons.ToArray();
            }
        }

        // ------------------------------------------------------------------------------------------------- the sub pages

        private CompanyCard TaxSubCard(CompanyDirector d, CompanyView v, Words w, TaxRules.QuarterFigures q, TaxFile tf, int day)
        {
            string pick = v.Pick[CompanyView.Tax] ?? string.Empty;
            var back = new[] { Btn(w.T("reallife_co_back", "Back"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, null, CompanyView.Tax, CompanyView.SubMain), UiAction.Count, UiIcon.Back) };
            switch (v.Sub[CompanyView.Tax])
            {
                case CompanyView.SubReturn: return ReturnCard(d, w, tf, day, back);
                case CompanyView.SubAccountant: return AccountantCard(d, v, w, tf, back);
                case CompanyView.SubHide:
                {
                    CompanyShop shop = pick.StartsWith("hide:", StringComparison.Ordinal) && Settings.EconomyCompanyHide ? d.ShopAt(pick.Substring(5)) : null;
                    return shop != null ? HideCard(d, w, tf, shop, back) : null;
                }
                case CompanyView.SubLicence:
                {
                    int last = pick.LastIndexOf(':');
                    int kind;
                    CompanyShop shop = pick.StartsWith("lic:", StringComparison.Ordinal) && last > 4 && int.TryParse(pick.Substring(last + 1), out kind) && kind >= 0
                        && kind < CompanyCatalogue.LicenceKinds
                        ? d.ShopAt(pick.Substring(4, last - 4)) : null;
                    return shop != null && d.On(CompanyPart.Licences) ? LicenceCard(d, w, shop, int.Parse(pick.Substring(last + 1)), day, back) : null;
                }
                default: return null;
            }
        }

        private CompanyCard ReturnCard(CompanyDirector d, Words w, TaxFile t, int day, CompanyButton[] back)
        {
            var card = new CompanyCard { Kicker = w.T("reallife_co_tab_tax", "Tax and licences"), Title = CompanyText.T("reallife_co_row_return"), Icon = UiIcon.Receipt, Back = back };
            bool late = t.ReturnWaiting && day > t.ReturnDue;
            card.Chips = new[] { CompanyText.T(!t.ReturnWaiting ? "reallife_ibox_chip_running" : late ? "reallife_ibox_chip_late" : "reallife_ibox_chip_due") };
            card.ChipTones = new[] { !t.ReturnWaiting ? UiRole.Muted : late ? UiRole.Danger : UiRole.Warn };
            var blocks = new List<CompanyBlock>(14);
            if (!t.ReturnWaiting)
            {
                blocks.Add(Note(CompanyText.Fill("reallife_co_return_running", "quarter", t.Quarter.ToString(), "from", t.QuarterFrom.ToString(), "day", (t.QuarterFrom + d.QuarterDays).ToString(),
                    "days", CompanyCatalogue.ReturnDays.ToString())));
                blocks.Add(Sec(CompanyText.T("reallife_co_return_costs")));
                for (int k = 0; k < t.Costs.Length; k++)
                {
                    blocks.Add(Row(CompanyText.T(CompanyText.CostKeys[k]), CompanyText.Money(t.Costs[k])));
                }
                card.Blocks = blocks.ToArray();
                return card;
            }
            blocks.Add(Note(CompanyText.T(late ? "reallife_co_return_late" : "reallife_co_return_due", "day", t.ReturnDue.ToString(), "amount", CompanyText.Money(TaxRules.Penalty(t.ReturnTakings)),
                "count", t.ReturnPenalties.ToString()), late ? UiRole.DangerText : UiRole.WarnText));
            blocks.Add(Sec(w.T("reallife_ibox_sec_return", "The return")));
            blocks.Add(Row(CompanyText.Label("reallife_co_return_quarter"), t.ReturnQuarter.ToString()));
            blocks.Add(Row(CompanyText.Label("reallife_co_return_takings"), CompanyText.Money(t.ReturnTakings)));
            blocks.Add(Row(CompanyText.Label("reallife_co_return_deduct"), CompanyText.Money(t.ReturnCosts)));
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Break });
            bool helped = TaxRules.Files(t.Accountant, d.Hidden() > 0);
            if (helped)
            {
                blocks.Add(Note(CompanyText.T("reallife_co_return_rebate", "name", CompanyText.Accountant(t.Accountant), "amount", CompanyText.Money(TaxRules.Rebate(t.Accountant, t.ReturnCosts))),
                    UiRole.OkText));
            }
            else if (t.Accountant >= 0)
            {
                blocks.Add(Note(CompanyText.T("reallife_co_return_wont", "name", CompanyText.Accountant(t.Accountant)), UiRole.WarnText));
            }
            bool order = CompanyGame.BooksInOrder();
            if (!order)
            {
                blocks.Add(Note(CompanyText.T("reallife_co_return_books"), UiRole.DangerText));
            }
            blocks.Add(Buttons(Btn(CompanyText.T("reallife_co_return_file"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.ReturnFile), UiAction.Alt, UiIcon.Check, false, !order, !order
                ? CompanyText.T("reallife_co_return_books") : null)));
            card.Blocks = blocks.ToArray();
            return card;
        }

        // The three accountants and what each costs and does: the same terms the accountant's page holds, as rows, so that the choice is seen before it is opened.
        private static CompanyItem[] AccountantTerms(CompanyDirector d, TaxFile t, Words w)
        {
            var rows = new CompanyItem[CompanyCatalogue.Accountants];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = new CompanyItem
                {
                    Title = CompanyText.Accountant(i),
                    Sub = CompanyText.T("reallife_co_acc_terms_line", "saves", CompanyText.Share(CompanyCatalogue.AccountantSaving[i]), "risk", CompanyText.Share(CompanyCatalogue.AccountantRisk[i])),
                    Figure = CompanyText.T("reallife_co_row_a_month", "amount", CompanyText.Money(TaxRules.Fee(i, d.Index, false))),
                    Chip = t.Accountant == i ? CompanyText.T("reallife_mgr_in_post") : null, ChipTone = UiRole.Ok, Press = true, Grade = -1,
                    Act = CompanyAct.Of(CompanyDo.Go, "acc", CompanyView.Tax, CompanyView.SubAccountant)
                };
            }
            return rows;
        }

        // The kinds of permit that no business of the company needs yet, each with its fee and term: what the catalogue has beyond the rows above, in the room a small company leaves.
        private static CompanyItem[] PermitsNotNeeded(CompanyDirector d, Words w)
        {
            var rows = new List<CompanyItem>(CompanyCatalogue.LicenceKinds);
            IReadOnlyList<CompanyShop> shops = d.Shops;
            for (int k = 0; k < CompanyCatalogue.LicenceKinds; k++)
            {
                bool needed = false;
                for (int i = 0; i < shops.Count && !needed; i++)
                {
                    needed = LicenceRules.Needed(shops[i].Needs, k);
                }
                if (!needed)
                {
                    rows.Add(new CompanyItem
                    {
                        Title = CompanyText.Capital(CompanyText.Licence(k)), Sub = CompanyText.T("reallife_co_lic_term_value", "days", CompanyCatalogue.LicenceTerm[k].ToString()),
                        Figure = CompanyText.Money(LicenceRules.Fee(k, d.Index)), Grade = -1
                    });
                }
            }
            return rows.ToArray();
        }

        private CompanyCard AccountantCard(CompanyDirector d, CompanyView v, Words w, TaxFile t, CompanyButton[] back)
        {
            bool hiding = d.Hidden() > 0;
            int tier = CompanyCatalogue.Clamp(v.Kind, 0, CompanyCatalogue.Accountants - 1);
            var card = new CompanyCard { Kicker = w.T("reallife_co_tab_tax", "Tax and licences"), Title = CompanyText.T("reallife_co_row_accountant"), Icon = UiIcon.Scales, Back = back };
            if (t.Accountant == tier)
            {
                card.Chips = new[] { CompanyText.T("reallife_mgr_in_post") };   // what they are is a state, said once, in the head: not a button that cannot be pressed
                card.ChipTones = new[] { UiRole.Ok };
            }
            var options = new string[CompanyCatalogue.Accountants];
            var acts = new CompanyAct[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                options[i] = CompanyText.Accountant(i);
                acts[i] = CompanyAct.Of(CompanyDo.Go, "kind", i);
            }
            card.FactLabels = new[] { w.T("reallife_co_f_hired", "Hired now"), w.T("reallife_co_f_fee", "Fee") };
            card.FactValues = new[] { CompanyText.Accountant(t.Accountant), CompanyText.T("reallife_co_row_a_month", "amount", CompanyText.Money(TaxRules.Fee(tier, d.Index, hiding))) };
            var blocks = new List<CompanyBlock>(10)
            {
                Sec(w.T("reallife_ibox_sec_terms", "Terms")),
                new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = w.T("reallife_co_acc_tier", "Accountant"), Options = options, Chosen = tier, Acts = acts },
                Note(CompanyText.T("reallife_co_acc_files", "days", CompanyCatalogue.ReturnDays.ToString())),
                new CompanyBlock { Kind = CompanyBlockKind.Break },
                Note(CompanyText.T("reallife_co_acc_saves", "share", CompanyText.Share(CompanyCatalogue.AccountantSaving[tier])), UiRole.OkText),
                Note(CompanyText.T("reallife_co_acc_risk", "share", CompanyText.Share(CompanyCatalogue.AccountantRisk[tier])))
            };
            blocks.Add(Sec(w.T("reallife_co_acc_terms_sec", "What an accountant costs")));
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = AccountantTerms(d, t, w) });
            if (tier == 0)
            {
                blocks.Add(Note(CompanyText.T("reallife_co_acc_careless", "chance", CompanyText.Share(CompanyCatalogue.AccountantErrorChance), "more",
                    CompanyText.Share(CompanyCatalogue.AccountantHidingFee - 1000))));
            }
            else
            {
                blocks.Add(Note(CompanyText.T(tier == CompanyCatalogue.Accountants - 1 ? "reallife_co_acc_honest_resigns" : "reallife_co_acc_honest_stays")));
            }
            bool resigns = TaxRules.Resigns(tier, hiding);
            var buttons = new List<CompanyButton>(2);
            if (t.Accountant != tier)
            {
                buttons.Add(Btn(CompanyText.T("reallife_co_acc_hire"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.AccountantHire, null, tier), UiAction.Alt, UiIcon.Check, false, resigns,
                    resigns ? CompanyText.T("reallife_co_acc_honest_resigns") : null));
            }
            if (t.Accountant >= 0)
            {
                buttons.Add(Btn(CompanyText.T("reallife_co_acc_dismiss"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.AccountantDismiss), UiAction.Count, UiIcon.Close, true));
            }
            if (buttons.Count > 0)
            {
                blocks.Add(Buttons(buttons.ToArray()));
            }
            card.Blocks = blocks.ToArray();
            return card;
        }

        private CompanyCard HideCard(CompanyDirector d, Words w, TaxFile t, CompanyShop shop, CompanyButton[] back)
        {
            int level = d.Level, most = TaxRules.HideMost(level), steps = 0, at = 0;
            for (int i = 0; i < CompanyCatalogue.HideSteps.Length && CompanyCatalogue.HideSteps[i] <= most; i++)
            {
                steps++;
                at = CompanyCatalogue.HideSteps[i] == shop.File.Hide ? i : at;
            }
            var options = new string[steps];
            var acts = new CompanyAct[steps];
            for (int i = 0; i < steps; i++)
            {
                options[i] = CompanyCatalogue.HideSteps[i] + "%";
                acts[i] = CompanyAct.Of(CompanyDo.HideSet, shop.File.Place, CompanyCatalogue.HideSteps[i]);
            }
            var card = new CompanyCard
            {
                Kicker = w.T("reallife_co_tab_tax", "Tax and licences"), Title = CompanyText.T("reallife_co_hide_title", "business", shop.Name), Icon = UiIcon.Percent, Back = back,
                FactLabels = new[] { w.T("reallife_co_f_saved", "Tax kept back"), w.T("reallife_co_f_risk", "Audit risk") },
                FactValues = new[] { CompanyText.Money(t.Saved), CompanyText.Share(TaxRules.AuditChance(t, d.Today) / 10) }
            };
            card.Blocks = new[]
            {
                Note(CompanyText.T("reallife_co_hide_what", "percent", CompanyGame.TaxPercent.ToString())),
                new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = w.T("reallife_co_hide_label", "Cash off the books"), Options = options, Chosen = at, Acts = acts },
                new CompanyBlock { Kind = CompanyBlockKind.Break },
                Sec(w.T("reallife_ibox_sec_risk", "The risk")),
                new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_risk", "Audit risk"), Value = CompanyText.Share(TaxRules.AuditChance(t, d.Today) / 10),
                    Number = Math.Min(100, TaxRules.AuditChance(t, d.Today) * 100 / Math.Max(1, CompanyCatalogue.RiskMost)), Tone = UiRole.Warn },
                Note(CompanyText.T("reallife_co_hide_risk", "grow", (CompanyCatalogue.RiskPerPercent / 100f).ToString("0.##"), "fall", (CompanyCatalogue.RiskDecay / 100f).ToString("0.##"),
                    "most", CompanyText.Share(CompanyCatalogue.RiskMost / 10))),
                Note(CompanyText.T(level == CompanyCatalogue.Relaxed ? "reallife_co_hide_audit_relaxed" : "reallife_co_hide_audit", "fine", CompanyText.Share(CompanyCatalogue.FineShare[level]),
                    "sum", CompanyText.Money(CompanyCatalogue.CourtSum))),
                Note(CompanyText.T("reallife_co_hide_threshold"))
            };
            return card;
        }

        private CompanyCard LicenceCard(CompanyDirector d, Words w, CompanyShop shop, int kind, int day, CompanyButton[] back)
        {
            Licence l = shop.File.Permits[kind];
            string place = shop.File.Place;
            bool valid = LicenceRules.Valid(l, day);
            var card = new CompanyCard
            {
                Kicker = shop.Name, Title = CompanyText.Capital(CompanyText.Licence(kind)), Icon = LicenceIcons[Math.Min(kind, LicenceIcons.Length - 1)], Back = back,
                Chips = new[] { CompanyText.T(valid ? "reallife_ibox_chip_valid" : l.Pending ? "reallife_co_row_lic_pending" : "reallife_ibox_chip_missing") },
                ChipTones = new[] { valid ? UiRole.Ok : l.Pending ? UiRole.Warn : UiRole.Danger },
                FactLabels = new[] { w.T("reallife_co_f_fee", "Fee"), w.T("reallife_co_f_term", "Term") },
                FactValues = new[] { CompanyText.Money(LicenceRules.Fee(kind, d.Index)), CompanyText.T("reallife_co_lic_term_value", "days", CompanyCatalogue.LicenceTerm[kind].ToString()) }
            };
            var blocks = new List<CompanyBlock>(10)
            {
                Note(valid ? CompanyText.T("reallife_co_lic_valid", "day", l.Until.ToString()) : l.Pending ? CompanyText.T("reallife_co_lic_pending", "day", l.Decide.ToString())
                    : CompanyText.T("reallife_co_lic_missing"),
                    valid ? UiRole.None : l.Pending ? UiRole.WarnText : UiRole.DangerText)
            };
            if (l.Pending)
            {
                blocks.Add(Row(w.T("reallife_co_lic_decides_in", "The city decides in"), CompanyText.T("reallife_co_lic_term_value", "days", LicenceRules.DaysToDecision(l, day).ToString()),
                    UiRole.WarnText));
            }
            blocks.Add(new CompanyBlock
            {
                Kind = CompanyBlockKind.Switch, Text = CompanyText.T("reallife_co_lic_auto"), On = l.Auto, Value = l.Auto ? w.T("reallife_co_on", "On") : w.T("reallife_co_off", "Off"),
                Act = CompanyAct.Of(CompanyDo.LicenceAuto, place, kind, l.Auto ? 0 : 1)
            });
            blocks.Add(Buttons(Btn(CompanyText.T(valid ? "reallife_co_lic_renew" : "reallife_co_lic_buy"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.LicenceBuy, place, kind), UiAction.Alt,
                UiIcon.Check, false, l.Pending, l.Pending ? CompanyText.T("reallife_co_lic_pending", "day", l.Decide.ToString()) : null)));
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Break });
            blocks.Add(Sec(w.T("reallife_co_lic_lapse_sec", "If it lapses")));
            blocks.Add(Note(CompanyText.T(LicenceRules.Shuts(kind) ? "reallife_co_lic_fine_shut" : "reallife_co_lic_fine", "amount", CompanyText.Money(LicenceRules.Fine(kind, d.Index)),
                "hours", CompanyCatalogue.LicenceClosedHours.ToString()), UiRole.WarnText));
            blocks.Add(Sec(w.T("reallife_co_lic_why_sec", "Why you need it")));
            blocks.Add(Note(CompanyText.T(LicenceWhyKeys[kind], "hour", CompanyCatalogue.LateHour.ToString(), "early", CompanyCatalogue.EarlyHour.ToString())));
            card.Blocks = blocks.ToArray();
            return card;
        }
    }
}
