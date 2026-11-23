using System;
using System.Collections.Generic;
using RealLifeOverhaul.Giving;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the Giving pages do in the game: each is the director's own call, so a page can only reach what the director allows.</summary>
    public interface IGiveActions
    {
        /// <summary>A gift once to a cause: the step's index in the list, and the district's number for a gift to a district's upkeep (else -1).</summary>
        void Give(int charity, int step, int district);

        /// <summary>A gift every month to a cause.</summary>
        void Monthly(int charity, int step, int district);

        /// <summary>The monthly gift ends.</summary>
        void StopMonthly(int charity);
    }

    /// <summary>
    /// The Giving pages: the causes with a gift once or every month, the loans out, the year's giving with its deduction and the names it has earned, and the people the player has
    /// helped. A page shows only what the rules allow, and every number comes from <see cref="GiveFacts"/>, which the director fills from the state and the game. Game-free.
    /// </summary>
    public static class GivePages
    {
        public const int TabCharities = 0, TabLoans = 1, TabYear = 2, TabPeople = 3;
        public const string CharityKey = "c:";

        private static readonly UiIcon[] CharityIcons = { UiIcon.ForkKnife, UiIcon.Home, UiIcon.Cap, UiIcon.Key, UiIcon.Presentation, UiIcon.Heartbeat, UiIcon.MapPin };

        public static void Fill(VentModel m, VentView v, GiveFacts f, IGiveActions act, VentWords w)
        {
            m.Title = w.T("reallife_giving_app", "Giving");
            m.Icon = UiIcon.Heart;
            m.Prompts = VenPages.Prompts(w);
            var runs = new VentRuns();
            int open = 0;
            for (int i = 0; i < f.Loans.Count; i++)
            {
                open += f.Loans[i].State == LoanState.Open ? 1 : 0;
            }
            m.Nav = new[]
            {
                new UiNavEntry { Icon = UiIcon.Heart, Label = w.T("reallife_giving_tab_charities", "Charities") },
                new UiNavEntry { Icon = UiIcon.Handshake, Label = w.T("reallife_giving_tab_loans", "Loans"), Count = open > 0 ? open.ToString() : null },
                new UiNavEntry { Icon = UiIcon.Chart, Label = w.T("reallife_giving_tab_year", "This year") },
                new UiNavEntry { Icon = UiIcon.People, Label = w.T("reallife_giving_tab_people", "People"), Count = f.People.Count > 0 ? f.People.Count.ToString() : null }
            };
            m.Tab = Math.Max(0, Math.Min(3, v.Tab));
            if (!f.Ready)
            {
                m.Shape = VentShape.Off;
                m.OffTitle = w.T("reallife_giving_off_title", "Giving is not running");
                m.OffBody = w.T("reallife_giving_off_body", "Load a save with Giving switched on to give to the city's charities and to people you know.");
                m.Runs = runs.ToArray();
                return;
            }
            m.HeroFigures = new[]
            {
                new UiFigureModel { Label = w.T("reallife_giving_fig_spare", "You can give"), Value = w.Money(f.Spare) },
                new UiFigureModel { Label = w.T("reallife_giving_fig_given", "Given to charities this year"), Value = w.Money(f.TaxGifts) },
                new UiFigureModel { Label = w.T("reallife_giving_fig_deducted", "Deducted from your tax"), Value = w.Money(f.TaxClaimed) }
            };
            switch (m.Tab)
            {
                case TabLoans: Loans(m, f, w, runs); break;
                case TabYear: Year(m, f, w); break;
                case TabPeople: People(m, f, w); break;
                default: Charities(m, v, f, act, w, runs); break;
            }
            m.Runs = runs.ToArray();
        }

        private static void Charities(VentModel m, VentView v, GiveFacts f, IGiveActions act, VentWords w, VentRuns runs)
        {
            int picked = 0;
            for (int i = 0; i < f.Charities.Length; i++)
            {
                picked = v.Pick == CharityKey + i ? i : picked;
            }
            int shown = GivingRules.ShownSteps(f.Spare, f.Level);
            int step = Math.Max(0, Math.Min(shown - 1, v.A));
            var left = new List<VentBlock>(4);
            var right = new List<VentBlock>(12);
            left.Add(VentBlock.Section(w.T("reallife_giving_sec_charities", "The city's causes")));
            if (f.Why != 0)
            {
                left.Add(VentBlock.Note(w.T(f.Why == 2 ? "reallife_giving_why_session" : "reallife_giving_why_people",
                    f.Why == 2 ? "In a Going Public session the city's people are the host's. Charities wait until you are out of it." :
                    "The city's people are not running, so a charity has nobody to help. Switch on Living population."), UiRole.Warn));
            }
            var items = new VentItem[f.Charities.Length];
            for (int i = 0; i < items.Length; i++)
            {
                GiveCharityFact c = f.Charities[i];
                int index = i;
                items[i] = new VentItem
                {
                    Icon = CharityIcons[Math.Max(0, Math.Min(CharityIcons.Length - 1, c.Id))], Title = w.T(GivingCatalogue.NameKeys[c.Id], NameEnglish(c.Id)),
                    Sub = c.Id == (int)Charity.Upkeep ? w.T("reallife_giving_item_sub_upkeep", "Spent at once in a district")
                        : w.F("reallife_giving_item_sub", "Helped {n} this year", "n", c.PeopleYear.ToString()),
                    Chip = c.Standing > 0 ? w.F("reallife_giving_item_monthly", "{amount} a month", "amount", w.Money(c.Standing)) : null,
                    ChipTone = c.StandingSkipped ? UiRole.Warn : UiRole.Ok, Amount = w.Money(c.PlayerYear),
                    Run = runs.Add(() => { v.Pick = CharityKey + index; return true; })
                };
            }
            left.Add(new VentBlock { Kind = VentBlockKind.Items, Items = items });
            GiveCharityFact p = f.Charities[picked];
            int amount = GivingRules.StepNow(step, f.Level);
            bool upkeep = p.Id == (int)Charity.Upkeep;
            int districts = f.Districts.Length;
            int district = districts > 0 ? Math.Max(0, Math.Min(districts - 1, v.B)) : -1;
            right.Add(VentBlock.Section(w.T(GivingCatalogue.NameKeys[p.Id], NameEnglish(p.Id))));
            right.Add(VentBlock.Note(w.T(GivingCatalogue.AboutKeys[p.Id], AboutEnglish(p.Id))));
            if (upkeep)
            {
                right.Add(districts > 0
                    ? VentBlock.Choice(w.T("reallife_giving_row_district", "District"), f.Districts[district], runs.Add(() => { v.B = (district + 1) % districts; return true; }))
                    : VentBlock.Note(w.T("reallife_giving_no_district", "This city has no district the mod knows, so there is none to give to."), UiRole.Warn));
                right.Add(VentBlock.Row(w.T("reallife_giving_row_district_yours", "You gave this district"), w.Money(district >= 0 ? f.DistrictTotals[district] : 0)));
            }
            else
            {
                right.Add(VentBlock.Row(w.T("reallife_giving_row_held", "Held now"), w.Money(p.Pot)));
                right.Add(VentBlock.Row(w.T("reallife_giving_row_cap", "Pays out at most a month"), w.Money(p.Cap)));
                right.Add(VentBlock.Row(w.T("reallife_giving_row_last", "Last month it paid"),
                    w.F("reallife_giving_row_last_value", "{amount} to {n}", "amount", w.Money(p.LastMoney), "n", p.LastPeople.ToString())));
            }
            right.Add(VentBlock.Row(w.T("reallife_giving_row_yours", "You gave in all"), w.Money(p.PlayerTotal)));
            right.Add(VentBlock.Stepper(w.T("reallife_giving_stepper", "Amount"), w.Money(amount), runs.Add(() => { v.A = Math.Max(0, step - 1); return true; }), step > 0,
                step < shown - 1));
            runs.Add(() => { v.A = Math.Min(shown - 1, step + 1); return true; });
            if (GivingRules.IsNaming(GivingRules.Back(amount, f.Level)))
            {
                right.Add(VentBlock.Note(w.T("reallife_giving_naming_hint", "A gift this size takes your name: a hall, a wing or a scholarship is called after you,"
                    + " and the City Ledger prints it. A monthly gift never does."), UiRole.Ok));
            }
            int charity = p.Id;
            int tag = upkeep ? district : -1;
            bool can = f.Why == 0 && amount <= f.Spare && (!upkeep || district >= 0);
            var actions = new List<UiActionModel>(3);
            if (p.Standing > 0)
            {
                actions.Add(new UiActionModel { Label = w.T("reallife_giving_stop_monthly", "Stop the monthly gift"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Close,
                    Arg = runs.Add(() => { act.StopMonthly(charity); return true; }) });
            }
            actions.Add(new UiActionModel { Label = w.T("reallife_giving_monthly", "Give every month"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Calendar,
                Disabled = f.Why != 0 || (upkeep && district < 0),
                Arg = runs.Add(() => { act.Monthly(charity, step, tag); return true; }) });
            actions.Add(new UiActionModel { Label = w.T("reallife_giving_give", "Give"), Kind = UiButtonKind.Primary, Icon = UiIcon.Heart, Key = UiAction.Alt, Disabled = !can,
                Arg = runs.Add(() => { act.Give(charity, step, tag); return true; }) });
            right.Add(VentBlock.Buttons(actions.ToArray()));
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        private static void Loans(VentModel m, GiveFacts f, VentWords w, VentRuns runs)
        {
            var left = new List<VentBlock>(3);
            left.Add(VentBlock.Section(w.T("reallife_giving_sec_loans", "Money you have lent")));
            var items = new List<VentItem>(f.Loans.Count);
            for (int i = f.Loans.Count - 1; i >= 0; i--)
            {
                GiveLoanFact l = f.Loans[i];
                bool openLoan = l.State == LoanState.Open;
                items.Add(new VentItem
                {
                    Icon = UiIcon.Handshake, Face = l.Name, Hue = VentWords.HueOf(l.Name), Title = l.Name,
                    Sub = openLoan ? w.F("reallife_giving_loan_sub", "{paid} paid back of {total}, next part about day {day}", "paid",
                        w.Money(l.Paid), "total", w.Money(l.Principal + l.Extra), "day", l.Next.ToString())
                        : w.F("reallife_giving_loan_closed", "{paid} paid back of {total}", "paid", w.Money(l.Paid), "total", w.Money(l.Principal + l.Extra)),
                    Chip = l.State == LoanState.Open ? (l.Misses > 0 ? w.T("reallife_giving_loan_late", "Behind") : w.T("reallife_giving_loan_open", "Open"))
                        : l.State == LoanState.Repaid ? w.T("reallife_giving_loan_repaid_chip", "Paid back") : w.T("reallife_giving_loan_off_chip", "Written off"),
                    ChipTone = l.State == LoanState.Repaid ? UiRole.Ok : l.State == LoanState.WrittenOff || l.Misses > 0 ? UiRole.Warn : UiRole.None,
                    Amount = openLoan ? w.Money(l.Remaining) : string.Empty, Run = -1
                });
            }
            if (items.Count == 0)
            {
                left.Add(VentBlock.Note(w.T("reallife_giving_loans_none", "You have lent nobody anything. Call someone you know and choose Lend money among the other things to say.")));
            }
            else
            {
                left.Add(new VentBlock { Kind = VentBlockKind.Items, Items = items.ToArray() });
            }
            var right = new List<VentBlock>(6);
            right.Add(VentBlock.Section(w.T("reallife_giving_sec_how_loans", "How a loan works")));
            right.Add(VentBlock.Note(w.F("reallife_giving_loans_how", "A loan has no interest and is paid back in {parts} parts, a month apart, out of what the person's household can spare. "
                + "A person who cannot pay for {misses} months is written off, and thinks less of you. A written off loan is not a tax deduction.",
                "parts", GivingCatalogue.LoanParts.ToString(), "misses", GivingCatalogue.LoanMisses.ToString())));
            right.Add(VentBlock.Note(w.F("reallife_giving_loans_business", "A loan to back a business takes {parts} months and carries {share} on top, once.",
                "parts", GivingCatalogue.BusinessParts.ToString(), "share", (GivingCatalogue.BusinessInterest / 10f).ToString("0.#") + "%")));
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        private static void Year(VentModel m, GiveFacts f, VentWords w)
        {
            var left = new List<VentBlock>(8);
            left.Add(VentBlock.Section(w.T("reallife_giving_sec_tax", "Your tax return")));
            left.Add(VentBlock.Row(w.T("reallife_giving_row_gifts", "Gifts to charities this tax year"), w.Money(f.TaxGifts)));
            left.Add(VentBlock.Row(w.T("reallife_giving_row_income", "Income so far this year"), w.Money(f.Income)));
            left.Add(VentBlock.Row(w.F("reallife_giving_row_cap_share", "The most that can be deducted ({share} of income)", "share", (f.DeductionShare / 10f).ToString("0.#")), w.Money(f.TaxCap)));
            left.Add(VentBlock.Row(w.T("reallife_giving_row_deducted", "Deducted so far"), w.Money(f.TaxClaimed)));
            left.Add(VentBlock.Row(w.F("reallife_giving_row_carried", "Carried forward, up to {years} years", "years", GivingCatalogue.CarryYears.ToString()), w.Money(f.Carried)));
            left.Add(VentBlock.Note(w.T("reallife_giving_tax_note", "Gifts to charities are deducted from your tax as the year's income allows,"
                + " and show on your tax statement as Charitable giving. What the cap holds back is carried forward to the next years. Gifts to people and loans never are.")));
            var right = new List<VentBlock>(8);
            right.Add(VentBlock.Section(w.T("reallife_giving_sec_city", "The city's giving")));
            right.Add(VentBlock.Row(w.T("reallife_giving_row_neighbours", "Neighbours helping neighbours"),
                w.F("reallife_giving_row_neighbours_value", "{amount}, {n} households", "amount", w.Money(f.NeighbourYear), "n", f.NeighbourHelped.ToString())));
            right.Add(VentBlock.Row(w.T("reallife_giving_row_rivals", "Given by the rivals"), w.Money(f.RivalsYear)));
            if (!string.IsNullOrEmpty(f.RivalLine))
            {
                right.Add(VentBlock.Note(f.RivalLine));
            }
            for (int i = 0; i < f.Charities.Length; i++)
            {
                GiveCharityFact c = f.Charities[i];
                right.Add(VentBlock.Row(w.T(GivingCatalogue.NameKeys[c.Id], NameEnglish(c.Id)),
                    w.F("reallife_giving_row_paid_value", "{amount} paid out, {n} helped", "amount", w.Money(c.PaidYear), "n", c.PeopleYear.ToString())));
            }
            if (f.Namings.Count > 0)
            {
                right.Add(VentBlock.Section(w.T("reallife_giving_sec_named", "Named after you")));
                for (int i = 0; i < f.Namings.Count; i++)
                {
                    right.Add(VentBlock.Row(f.Namings[i].Words, w.Money(f.Namings[i].Amount)));
                }
            }
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        // The people the player has given to, lent to, helped or said no to: what each is short of, how they lean on the help, what the rent paid for them still covers.
        private static void People(VentModel m, GiveFacts f, VentWords w)
        {
            var left = new List<VentBlock>(3);
            left.Add(VentBlock.Section(w.T("reallife_giving_sec_people", "People you have helped")));
            if (f.People.Count == 0)
            {
                left.Add(VentBlock.Note(w.T("reallife_giving_people_none", "You have given nobody anything yet. Call someone you know, or open their page in People, and choose Help with money.")));
            }
            else
            {
                var items = new VentItem[f.People.Count];
                for (int i = 0; i < items.Length; i++)
                {
                    GivePersonFact p = f.People[i];
                    string need = w.T(p.Need == GivingNeed.Work ? "reallife_giving_need_work" : p.Need == GivingNeed.Debt ? "reallife_giving_need_debt" : p.Need == GivingNeed.Rent
                        ? "reallife_giving_need_rent" : "reallife_giving_need_none", NeedEnglish(p.Need));
                    string lean = w.T(p.Dependence == 0 ? "reallife_giving_lean_0" : p.Dependence < 3 ? "reallife_giving_lean_1" : "reallife_giving_lean_2", LeanEnglish(p.Dependence));
                    items[i] = new VentItem
                    {
                        Icon = UiIcon.Person, Face = p.Name, Hue = VentWords.HueOf(p.Name), Title = p.Name, Sub = need + " " + lean,
                        Chip = p.CoveredDays > 0 ? w.F("reallife_giving_chip_covered", "Rent paid, {n} days", "n", p.CoveredDays.ToString())
                            : p.Loan > 0 ? w.F("reallife_giving_chip_owes", "Owes {amount}", "amount", w.Money(p.Loan))
                            : p.Proud ? w.T("reallife_giving_chip_proud", "Refused at first") : p.Cooling ? w.T("reallife_giving_chip_cooling", "Helped lately") : null,
                        ChipTone = p.Loan > 0 || p.Proud ? UiRole.Warn : UiRole.Ok, Amount = w.Money(p.Given), Run = -1
                    };
                }
                left.Add(new VentBlock { Kind = VentBlockKind.Items, Items = items });
            }
            var right = new List<VentBlock>(4);
            right.Add(VentBlock.Section(w.T("reallife_giving_sec_ways", "Ways to help")));
            right.Add(VentBlock.Note(w.F("reallife_giving_ways", "On a call or on their page in People, choose Help with money. You can give or lend, pay the rent,"
                + " clear a debt, back a small business, pay for a course, "
                + "and, for somebody close, buy a car or a home. A person can refuse. A gift can be up to {most}.", "most", w.Money(f.PersonMost))));
            right.Add(VentBlock.Note(w.T("reallife_giving_cannot", "Money buys a person's trouble out of the way. It does not buy their affection beyond a little:"
                + " the same sum earns the same however it is split, and somebody who is helped again and again will ask again.")));
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        private static string NeedEnglish(GivingNeed n) => n == GivingNeed.Work ? "Out of work." : n == GivingNeed.Debt ? "In debt."
            : n == GivingNeed.Rent ? "The rent is crushing them." : "In no trouble.";

        private static string LeanEnglish(int dependence) => dependence == 0 ? "Not leaning on you." : dependence < 3 ? "Has come to lean on your help." : "Leans on your help a good deal.";

        /// <summary>The English the samples and the checks read a charity's name in.</summary>
        public static string NameEnglish(int c) => c == 0 ? "Harbour Food Bank" : c == 1 ? "Stoop Housing Trust" : c == 2 ? "Second Chance Training Fund" : c == 3 ? "Lantern Night Shelter"
            : c == 4 ? "Open Doors Scholarship Fund" : c == 5 ? "Riverside Care Fund" : "Neighbourhood Upkeep Fund";

        /// <summary>The English of what a charity does.</summary>
        public static string AboutEnglish(int c) => c == 0 ? "Pays down the debts of households that owe more than a month's income, the worst off first."
            : c == 1 ? "Pays the part of the rent over the line where a household moves out, for tenants the rent is crushing."
            : c == 2 ? "Pays for courses for people out of work, so they come back with more skill and a better paid job."
            : c == 3 ? "Keeps households that the rent is about to drive out of the city in their homes, at a rent they can bear."
            : c == 4 ? "Pays the debts of households with a pupil or a student, and gives students a little more skill."
            : c == 5 ? "Pays part of what the city's households owe for their care, before it reaches their savings and debt."
            : "Spent at once in a district you choose: more standing there, and more custom in its shops for a while.";
    }
}
