using System;
using System.Collections.Generic;
using RealLifeOverhaul.Giving;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the Giving pages do in the game: each is the director's own call, so a page can only reach what the director allows.</summary>
    public interface IGiveActions
    {
        /// <summary>A gift once to a charity: the step's index in the list.</summary>
        void Give(int charity, int step);

        /// <summary>A gift every month to a charity.</summary>
        void Monthly(int charity, int step);

        /// <summary>The monthly gift ends.</summary>
        void StopMonthly(int charity);
    }

    /// <summary>
    /// The Giving pages: the charities with a gift once or every month, the loans out, and the year's giving with its deduction. A page shows only what the rules allow, and every number
    /// comes from <see cref="GiveFacts"/>, which the director fills from the state and the game. Game-free.
    /// </summary>
    public static class GivePages
    {
        public const int TabCharities = 0, TabLoans = 1, TabYear = 2;
        public const string CharityKey = "c:";

        private static readonly UiIcon[] CharityIcons = { UiIcon.ForkKnife, UiIcon.Home, UiIcon.Cap };

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
                new UiNavEntry { Icon = UiIcon.Chart, Label = w.T("reallife_giving_tab_year", "This year") }
            };
            m.Tab = Math.Max(0, Math.Min(2, v.Tab));
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
            int step = Math.Max(0, Math.Min(GivingCatalogue.Steps.Length - 1, v.A));
            var left = new List<VentBlock>(4);
            var right = new List<VentBlock>(12);
            left.Add(VentBlock.Section(w.T("reallife_giving_sec_charities", "The city's charities")));
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
                    Icon = CharityIcons[Math.Max(0, Math.Min(2, c.Id))], Title = w.T(GivingCatalogue.NameKeys[c.Id], NameEnglish(c.Id)),
                    Sub = w.F("reallife_giving_item_sub", "Helped {n} this year", "n", c.PeopleYear.ToString()),
                    Chip = c.Standing > 0 ? w.F("reallife_giving_item_monthly", "{amount} a month", "amount", w.Money(c.Standing)) : null,
                    ChipTone = c.StandingSkipped ? UiRole.Warn : UiRole.Ok, Amount = w.Money(c.PlayerYear),
                    Run = runs.Add(() => { v.Pick = CharityKey + index; return true; })
                };
            }
            left.Add(new VentBlock { Kind = VentBlockKind.Items, Items = items });
            GiveCharityFact p = f.Charities[picked];
            int amount = GivingRules.StepNow(step, f.Level);
            right.Add(VentBlock.Section(w.T(GivingCatalogue.NameKeys[p.Id], NameEnglish(p.Id))));
            right.Add(VentBlock.Note(w.T(GivingCatalogue.AboutKeys[p.Id], AboutEnglish(p.Id))));
            right.Add(VentBlock.Row(w.T("reallife_giving_row_held", "Held now"), w.Money(p.Pot)));
            right.Add(VentBlock.Row(w.T("reallife_giving_row_cap", "Pays out at most a month"), w.Money(p.Cap)));
            right.Add(VentBlock.Row(w.T("reallife_giving_row_last", "Last month it paid"),
                w.F("reallife_giving_row_last_value", "{amount} to {n}", "amount", w.Money(p.LastMoney), "n", p.LastPeople.ToString())));
            right.Add(VentBlock.Row(w.T("reallife_giving_row_yours", "You gave in all"), w.Money(p.PlayerTotal)));
            right.Add(VentBlock.Stepper(w.T("reallife_giving_stepper", "Amount"), w.Money(amount), runs.Add(() => { v.A = Math.Max(0, step - 1); return true; }), step > 0,
                step < GivingCatalogue.Steps.Length - 1));
            runs.Add(() => { v.A = Math.Min(GivingCatalogue.Steps.Length - 1, step + 1); return true; });
            int charity = p.Id;
            bool can = f.Why == 0 && amount <= f.Spare;
            var actions = new List<UiActionModel>(3);
            if (p.Standing > 0)
            {
                actions.Add(new UiActionModel { Label = w.T("reallife_giving_stop_monthly", "Stop the monthly gift"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Close,
                    Arg = runs.Add(() => { act.StopMonthly(charity); return true; }) });
            }
            actions.Add(new UiActionModel { Label = w.T("reallife_giving_monthly", "Give every month"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Calendar, Disabled = f.Why != 0,
                Arg = runs.Add(() => { act.Monthly(charity, step); return true; }) });
            actions.Add(new UiActionModel { Label = w.T("reallife_giving_give", "Give"), Kind = UiButtonKind.Primary, Icon = UiIcon.Heart, Key = UiAction.Alt, Disabled = !can,
                Arg = runs.Add(() => { act.Give(charity, step); return true; }) });
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
                        w.Money(l.Paid), "total", w.Money(l.Principal), "day", l.Next.ToString())
                        : w.F("reallife_giving_loan_closed", "{paid} paid back of {total}", "paid", w.Money(l.Paid), "total", w.Money(l.Principal)),
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
            left.Add(VentBlock.Note(w.T("reallife_giving_tax_note", "Gifts to charities are deducted from your tax as the year's income allows,"
                + " and show on your tax statement as Charitable giving. Gifts to people and loans never are.")));
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
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        /// <summary>The English the samples and the checks read a charity's name in.</summary>
        public static string NameEnglish(int c) => c == 0 ? "Harbour Food Bank" : c == 1 ? "Stoop Housing Trust" : "Second Chance Training Fund";

        /// <summary>The English of what a charity does.</summary>
        public static string AboutEnglish(int c) => c == 0 ? "Pays down the debts of households that owe more than a month's income, the worst off first."
            : c == 1 ? "Pays the part of the rent over the line where a household moves out, for tenants the rent is crushing."
            : "Pays for courses for people out of work, so they come back with more skill and a better paid job.";
    }
}
