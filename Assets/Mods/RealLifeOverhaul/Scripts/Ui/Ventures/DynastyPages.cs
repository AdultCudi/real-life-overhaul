using System;
using System.Collections.Generic;
using RealLifeOverhaul.Dynasty;
using RealLifeOverhaul.Giving;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the dynasty's pages do in the game: each is the director's own call, so a page can only reach what the director allows.</summary>
    public interface IDynastyActions
    {
        /// <summary>Names who carries on in the draft; None for the order of the law.</summary>
        void Heir(KinKind kind, int id, string name);

        /// <summary>Moves a share of the draft a step up or down.</summary>
        void Share(KinKind kind, int id, string name, int steps);

        /// <summary>The age the will sets for retiring, a step up or down.</summary>
        void RetireAge(int steps);

        /// <summary>What the founder keeps when they retire, a step up or down.</summary>
        void Keep(int steps);

        /// <summary>The draft is let go.</summary>
        void Discard();

        /// <summary>The draft is signed at the law firm, for its fee.</summary>
        void Sign();

        /// <summary>The founder retires and hands over (asked once more first).</summary>
        void Retire();
    }

    /// <summary>
    /// The Legacy pages: the will (who carries on, what the others are left, when to retire), the estate (what it is today, the tax on it, retiring, the statements) and the
    /// family line (the heads before, the retired founder, what the family was left, the rivals' lines and the city's estates). A page shows only what the rules allow, and
    /// every number comes from <see cref="DynastyFacts"/>, which the director fills from the state and the game. Game-free.
    /// </summary>
    public static class DynastyPages
    {
        public const int TabWill = 0, TabEstate = 1, TabLine = 2;
        public const string PersonKey = "p:", CharityKey = "c:";

        private static readonly UiIcon[] CharityIcons = { UiIcon.ForkKnife, UiIcon.Home, UiIcon.Cap };

        public static void Fill(VentModel m, VentView v, DynastyFacts f, IDynastyActions act, VentWords w)
        {
            m.Title = w.T("reallife_dynasty_app", "Legacy");
            m.Icon = UiIcon.Key;
            m.Prompts = VenPages.Prompts(w);
            var runs = new VentRuns();
            m.Nav = new[]
            {
                new UiNavEntry { Icon = UiIcon.Receipt, Label = w.T("reallife_dynasty_tab_will", "Will"), Count = f.Ready && f.Dirty ? "1" : null },
                new UiNavEntry { Icon = UiIcon.Bank, Label = w.T("reallife_dynasty_tab_estate", "Estate") },
                new UiNavEntry { Icon = UiIcon.People, Label = w.T("reallife_dynasty_tab_line", "Family line") }
            };
            m.Tab = Math.Max(0, Math.Min(2, v.Tab));
            if (!f.Ready)
            {
                m.Shape = VentShape.Off;
                m.OffTitle = w.T("reallife_dynasty_off_title", "Legacy is not running");
                m.OffBody = w.T("reallife_dynasty_off_body", "Load a save with Dynasty switched on to write a will, see your estate and hand over to an heir.");
                m.Runs = runs.ToArray();
                return;
            }
            Statement t = f.Today;
            m.HeroFigures = new[]
            {
                new UiFigureModel { Label = w.T("reallife_dynasty_fig_estate", "Your estate today"), Value = w.Money(t != null ? t.Net : 0) },
                new UiFigureModel { Label = w.T("reallife_dynasty_fig_tax", "Estate tax if it passed today"), Value = w.Money(t != null ? t.Tax : 0) },
                new UiFigureModel { Label = w.T("reallife_dynasty_fig_heir", "Who carries on"),
                    Value = t != null && f.HeirKnown ? t.To : w.T("reallife_dynasty_fig_nobody", "Nobody") }
            };
            switch (m.Tab)
            {
                case TabEstate: Estate(m, f, act, w, runs); break;
                case TabLine: Line(m, f, w); break;
                default: WillTab(m, v, f, act, w, runs); break;
            }
            m.Runs = runs.ToArray();
        }

        // ---- the will

        private static string KindWord(KinPerson c, VentWords w)
        {
            return c.Kind == KinKind.Partner ? w.T("reallife_dynasty_kind_partner", "Partner") : c.Kind == KinKind.Child ? w.T("reallife_dynasty_kind_child", "Child")
                : c.Family ? w.T("reallife_dynasty_kind_relative", "Relative") : w.T("reallife_dynasty_kind_friend", "Someone you know");
        }

        private static string Percent(int mille) => (mille / 10).ToString() + "%";

        private static void WillTab(VentModel m, VentView v, DynastyFacts f, IDynastyActions act, VentWords w, VentRuns runs)
        {
            Will d = f.Draft;
            var left = new List<VentBlock>(6);
            var right = new List<VentBlock>(16);
            left.Add(VentBlock.Section(w.T("reallife_dynasty_sec_people", "Who carries on, and who is left something")));
            if (f.Handing)
            {
                left.Add(VentBlock.Note(w.T("reallife_dynasty_handing_note", "The estate is passing now. The new head writes a will of their own once it has."), UiRole.Warn));
            }
            int pickedPerson = -1, pickedCharity = -1;
            var items = new List<VentItem>(f.People.Count + 3);
            for (int i = 0; i < f.People.Count; i++)
            {
                KinPerson c = f.People[i];
                int index = i;
                bool heir = DynastyWill.IsHeir(d, c.Kind, c.Id, c.Name);
                Bequest b = d.Find(c.Kind, c.Id, c.Name);
                pickedPerson = v.Pick == PersonKey + i ? i : pickedPerson;
                items.Add(new VentItem
                {
                    Icon = c.Kind == KinKind.Child ? UiIcon.Cap : UiIcon.Person, Face = c.Name, Hue = VentWords.HueOf(c.Name), Title = c.Name,
                    Sub = c.Age > 0 ? w.F("reallife_dynasty_person_sub", "{kind}, {age}", "kind", KindWord(c, w), "age", c.Age.ToString()) : KindWord(c, w),
                    Chip = heir ? w.T("reallife_dynasty_chip_heir", "Heir") : b != null ? Percent(b.Share) : null, ChipTone = heir ? UiRole.Ok : UiRole.None,
                    Amount = string.Empty, Run = runs.Add(() => { v.Pick = PersonKey + index; return true; })
                });
            }
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                Bequest b = d.Find(KinKind.Charity, i, null);
                pickedCharity = v.Pick == CharityKey + i ? i : pickedCharity;
                items.Add(new VentItem
                {
                    Icon = CharityIcons[i], Title = w.T(GivingCatalogue.NameKeys[i], GivePages.NameEnglish(i)),
                    Sub = w.T("reallife_dynasty_charity_sub", "A charity: what it is left is free of estate tax"),
                    Chip = b != null ? Percent(b.Share) : null, Amount = string.Empty, Run = runs.Add(() => { v.Pick = CharityKey + index; return true; })
                });
            }
            left.Add(new VentBlock { Kind = VentBlockKind.Items, Items = items.ToArray() });
            left.Add(VentBlock.Note(w.T("reallife_dynasty_law_note", "With no will, or if the heir it names cannot carry on, the law decides: your partner, then your eldest grown child."
                + " With neither, the state takes the estate and the game ends as it always did.")));
            if (pickedPerson < 0 && pickedCharity < 0 && f.People.Count > 0)
            {
                pickedPerson = 0;
            }
            else if (pickedPerson < 0 && pickedCharity < 0)
            {
                pickedCharity = 0;
            }
            bool can = !f.Handing;
            if (pickedPerson >= 0)
            {
                KinPerson c = f.People[pickedPerson];
                bool heir = DynastyWill.IsHeir(d, c.Kind, c.Id, c.Name);
                right.Add(VentBlock.Section(c.Name));
                if (DynastyRules.CanCarryOn(c))
                {
                    right.Add(VentBlock.Row(w.T("reallife_dynasty_row_upbringing", "Would start with, of what you know"), Percent(DynastyRules.Upbringing(c))));
                    KinKind kind = c.Kind;
                    int id = c.Id;
                    string name = c.Name;
                    right.Add(VentBlock.Choice(w.T("reallife_dynasty_choice_heir", "Carries on after you"), heir ? w.T("reallife_dynasty_choice_yes", "Yes") : w.T("reallife_dynasty_choice_no", "No"),
                        runs.Add(() => { act.Heir(heir ? KinKind.None : kind, id, name); return true; }), !can));
                }
                else
                {
                    right.Add(VentBlock.Note(w.F("reallife_dynasty_cannot_carry", "Cannot carry on: an heir is one of the family, between {least} and {most} years old."
                        + " They can still be left a share.",
                        "least", DynastyCatalogue.HeirLeast.ToString(), "most", DynastyCatalogue.HeirMost.ToString())));
                }
                if (!heir)
                {
                    ShareStepper(right, d, c.Kind, c.Id, c.Name, act, w, runs, can);
                }
                else
                {
                    right.Add(VentBlock.Note(w.T("reallife_dynasty_heir_rest", "The heir takes everything the will does not leave to somebody else, the debts with it.")));
                }
            }
            else
            {
                right.Add(VentBlock.Section(w.T(GivingCatalogue.NameKeys[pickedCharity], GivePages.NameEnglish(pickedCharity))));
                right.Add(VentBlock.Note(w.T(GivingCatalogue.AboutKeys[pickedCharity], GivePages.AboutEnglish(pickedCharity))));
                ShareStepper(right, d, KinKind.Charity, pickedCharity, w.T(GivingCatalogue.NameKeys[pickedCharity], GivePages.NameEnglish(pickedCharity)), act, w, runs, can);
            }
            right.Add(VentBlock.Section(w.T("reallife_dynasty_sec_retire_terms", "Retiring")));
            right.Add(VentBlock.Stepper(w.T("reallife_dynasty_step_age", "You may retire from the age of"), d.RetireAge.ToString(), runs.Add(() => { act.RetireAge(-1); return true; }),
                can && d.RetireAge > DynastyCatalogue.RetireLeast, can && d.RetireAge < DynastyCatalogue.RetireMost));
            runs.Add(() => { act.RetireAge(1); return true; });
            int keep = Math.Max(0, Math.Min(DynastyCatalogue.KeepSteps.Length - 1, d.Keep));
            right.Add(VentBlock.Stepper(w.T("reallife_dynasty_step_keep", "You keep, of the estate"), Percent(DynastyCatalogue.KeepSteps[keep]),
                runs.Add(() => { act.Keep(-1); return true; }), can && d.Keep > 0, can && d.Keep < DynastyCatalogue.KeepSteps.Length - 1));
            runs.Add(() => { act.Keep(1); return true; });
            right.Add(VentBlock.Section(w.T("reallife_dynasty_sec_sign", "Signing")));
            right.Add(VentBlock.Row(w.T("reallife_dynasty_row_left", "Left to others in all"),
                w.F("reallife_dynasty_row_left_value", "{share} of at most {most}", "share", Percent(d.Others()), "most", Percent(DynastyCatalogue.OthersMost))));
            right.Add(VentBlock.Row(w.T("reallife_dynasty_row_firm", "Drawn up by"), string.IsNullOrEmpty(f.Firm) ? w.T("reallife_dynasty_notary", "A notary") : f.Firm));
            right.Add(VentBlock.Row(w.T("reallife_dynasty_row_fee", "Fee"), f.OwnFirm ? w.T("reallife_dynasty_fee_own", "None: your own firm") : w.Money(f.Fee)));
            if (!f.Signed.Written)
            {
                right.Add(VentBlock.Note(w.T("reallife_dynasty_no_will", "You have no will yet.")));
            }
            bool sign = can && f.Dirty && f.DraftWhy == WillWhy.Ok && (f.OwnFirm || f.Fee <= f.Spare);
            var actions = new List<UiActionModel>(2);
            if (f.Dirty)
            {
                actions.Add(new UiActionModel { Label = w.T("reallife_dynasty_discard", "Discard the changes"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Close,
                    Arg = runs.Add(() => { act.Discard(); return true; }) });
            }
            actions.Add(new UiActionModel { Label = w.T("reallife_dynasty_sign", "Sign the will"), Kind = UiButtonKind.Primary, Icon = UiIcon.Receipt, Key = UiAction.Alt, Disabled = !sign,
                Arg = runs.Add(() => { act.Sign(); return true; }) });
            right.Add(VentBlock.Buttons(actions.ToArray()));
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        private static void ShareStepper(List<VentBlock> right, Will d, KinKind kind, int id, string name, IDynastyActions act, VentWords w, VentRuns runs, bool can)
        {
            Bequest b = d.Find(kind, id, name);
            int share = b != null ? b.Share : 0;
            bool more = can && DynastyRules.ShareRoom(d, b) >= share + DynastyCatalogue.ShareStep && (b != null || d.Bequests.Count < DynastyCatalogue.BequestsMost);
            right.Add(VentBlock.Stepper(w.T("reallife_dynasty_step_share", "Left to them, of the estate"), Percent(share), runs.Add(() => { act.Share(kind, id, name, -1); return true; }),
                can && share > 0, more));
            runs.Add(() => { act.Share(kind, id, name, 1); return true; });
        }

        // ---- the estate

        private static void Estate(VentModel m, DynastyFacts f, IDynastyActions act, VentWords w, VentRuns runs)
        {
            var left = new List<VentBlock>(18);
            var right = new List<VentBlock>(16);
            Statement s = f.Today;
            left.Add(VentBlock.Section(w.T("reallife_dynasty_sec_today", "If the estate passed today")));
            if (f.Handing)
            {
                left.Add(VentBlock.Steps(w.T("reallife_dynasty_handing", "The estate is passing"), Math.Max(1, f.Steps), f.Step));
            }
            if (s != null)
            {
                StatementRows(left, s, f, w);
            }
            right.Add(VentBlock.Section(w.T("reallife_dynasty_sec_retire", "Retiring")));
            right.Add(VentBlock.Row(w.T("reallife_dynasty_row_age", "Your age"), f.Age.ToString()));
            right.Add(VentBlock.Row(w.T("reallife_dynasty_row_retire_from", "Your will lets you retire from"),
                f.Signed.Written ? f.Signed.RetireAge.ToString() : w.T("reallife_dynasty_no_will_short", "No will")));
            Statement r = f.RetireToday;
            if (r != null && f.Signed.Written)
            {
                right.Add(VentBlock.Row(w.T("reallife_dynasty_row_keep", "You would keep"), w.Money(r.Keep)));
                right.Add(VentBlock.Row(w.T("reallife_dynasty_row_retire_tax", "Estate tax on the handover"), w.Money(r.Tax)));
            }
            right.Add(VentBlock.Note(w.T(RetireKey(f.Retire), RetireEnglish(f.Retire)), f.Retire == RetireWhy.Ok ? UiRole.None : UiRole.Warn));
            right.Add(VentBlock.Buttons(new UiActionModel { Label = w.T("reallife_dynasty_retire", "Retire and hand over"), Kind = UiButtonKind.Primary, Icon = UiIcon.Handshake, Key = UiAction.Alt,
                Disabled = f.Retire != RetireWhy.Ok, Arg = runs.Add(() => { act.Retire(); return true; }) }));
            if (f.LoanOwed > 0)
            {
                right.Add(VentBlock.Section(w.T("reallife_dynasty_sec_loan", "Owed for estate tax")));
                right.Add(VentBlock.Row(w.T("reallife_dynasty_row_owed", "Still owed"), w.Money(f.LoanOwed), UiRole.Warn));
                right.Add(VentBlock.Row(w.T("reallife_dynasty_row_per_day", "Taken a day"), w.Money(f.LoanPerDay)));
                right.Add(VentBlock.Note(w.F("reallife_dynasty_loan_note", "Tax the cash did not cover is owed at {rate} a year and taken a part a day. A part the cash cannot cover is a late day;"
                    + " after {days} in a row it is taken all the same.", "rate", Percent(DynastyCatalogue.LoanRate), "days", DynastyCatalogue.LoanLateMost.ToString()),
                    f.LoanLate > 0 ? UiRole.Warn : UiRole.None));
            }
            right.Add(VentBlock.Section(w.T("reallife_dynasty_sec_statements", "Estate statements")));
            if (f.Statements.Count == 0)
            {
                right.Add(VentBlock.Note(w.T("reallife_dynasty_statements_none", "No estate has passed in this family yet.")));
            }
            else
            {
                var items = new VentItem[f.Statements.Count];
                for (int i = 0; i < items.Length; i++)
                {
                    Statement p = f.Statements[f.Statements.Count - 1 - i];
                    items[i] = new VentItem
                    {
                        Icon = UiIcon.Receipt, Title = w.F("reallife_dynasty_statement_title", "{from} to {to}", "from", p.From, "to", p.To),
                        Sub = w.F("reallife_dynasty_statement_sub", "Day {day}: an estate of {net}, {paid} left to others", "day", p.Day.ToString(), "net", w.Money(p.Net), "paid", w.Money(p.Paid)),
                        Chip = ModeWord(p.Mode, w), Amount = w.Money(p.Tax), Run = -1
                    };
                }
                right.Add(new VentBlock { Kind = VentBlockKind.Items, Items = items });
            }
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        private static void StatementRows(List<VentBlock> left, Statement s, DynastyFacts f, VentWords w)
        {
            left.Add(VentBlock.Row(w.T("reallife_dynasty_row_cash", "Cash"), w.Money(s.Cash)));
            if (s.Reserved > 0)
            {
                left.Add(VentBlock.Row(w.T("reallife_dynasty_row_reserved", "Kept for the investors of your firms"), w.Money(-s.Reserved)));
            }
            left.Add(VentBlock.Row(w.T("reallife_dynasty_row_other", "Everything else you own, less every debt"), w.Money(s.Other)));
            left.Add(VentBlock.Row(w.T("reallife_dynasty_row_net", "The estate"), w.Money(s.Net)));
            if (s.UnderWater)
            {
                left.Add(VentBlock.Note(w.T("reallife_dynasty_under_water", "The debts are more than the estate. Nothing is written off: the heir takes it all, debts included,"
                    + " and no bequest is paid."),
                    UiRole.Warn));
            }
            left.Add(VentBlock.Row(w.F("reallife_dynasty_row_gifts", "Gifts to people in the last {years} years, counted in", "years", f.WindowYears.ToString()), w.Money(s.Gifts)));
            left.Add(VentBlock.Row(w.T("reallife_dynasty_row_charity", "Left to charities, free of tax"), w.Money(-s.Charity)));
            left.Add(VentBlock.Row(w.F("reallife_dynasty_row_spouse", "A married partner's part, free up to {share}", "share", Percent(DynastyCatalogue.SpouseShare)), w.Money(-s.Spouse)));
            left.Add(VentBlock.Row(w.F("reallife_dynasty_row_threshold", "Counted for tax, the first {amount} of it free", "amount",
                w.Money(DynastyRules.At(DynastyCatalogue.Threshold, s.Level))), w.Money(s.Taxable)));
            left.Add(VentBlock.Row(w.T("reallife_dynasty_row_tax", "Estate tax"), w.Money(s.Tax), s.Tax > 0 ? UiRole.Warn : UiRole.Text));
            if (s.Loan > 0)
            {
                left.Add(VentBlock.Row(w.T("reallife_dynasty_row_tax_owed", "Of it the cash does not cover, owed with interest"), w.Money(s.Loan), UiRole.Warn));
            }
            for (int i = 0; i < s.Lines.Count; i++)
            {
                BequestLine l = s.Lines[i];
                string name = l.Kind == KinKind.Charity ? w.T(GivingCatalogue.NameKeys[Math.Max(0, Math.Min(2, l.Id))], GivePages.NameEnglish(l.Id)) : l.Name;
                left.Add(VentBlock.Row(w.F("reallife_dynasty_row_bequest", "{name}, {share}", "name", name, "share", Percent(l.Share)),
                    l.Landing == Landing.None ? w.T("reallife_dynasty_bequest_lapsed", "Nobody there to receive it") : w.Money(l.Amount), l.Landing == Landing.None ? UiRole.Warn : UiRole.Text));
            }
            if (s.Scale < 1000 && s.Lines.Count > 0 && !s.UnderWater)
            {
                left.Add(VentBlock.Note(w.F("reallife_dynasty_scale_note", "Bequests are paid in cash. The cash left after the tax covers {share} of each.", "share", Percent(s.Scale)), UiRole.Warn));
            }
            left.Add(VentBlock.Row(w.F("reallife_dynasty_row_residue", "{name} is left", "name", string.IsNullOrEmpty(s.To) ? w.T("reallife_dynasty_fig_nobody", "Nobody") : s.To),
                w.Money(s.Residue)));
        }

        private static string ModeWord(HandMode mode, VentWords w)
        {
            return mode == HandMode.Retire ? w.T("reallife_dynasty_mode_retire", "Retired") : mode == HandMode.Elder ? w.T("reallife_dynasty_mode_elder", "The fund")
                : w.T("reallife_dynasty_mode_death", "Died");
        }

        /// <summary>The key of the line that says whether the founder may retire.</summary>
        public static string RetireKey(RetireWhy why)
        {
            switch (why)
            {
                case RetireWhy.Ok: return "reallife_dynasty_retire_ok";
                case RetireWhy.Running: return "reallife_dynasty_retire_running";
                case RetireWhy.Young: return "reallife_dynasty_retire_young";
                case RetireWhy.NoHeir: return "reallife_dynasty_retire_no_heir";
                case RetireWhy.Session: return "reallife_dynasty_retire_session";
                case RetireWhy.Law: return "reallife_dynasty_retire_law";
                case RetireWhy.Elder: return "reallife_dynasty_retire_elder";
                case RetireWhy.NoWill: return "reallife_dynasty_retire_no_will";
                default: return "reallife_dynasty_retire_off";
            }
        }

        /// <summary>The English the samples and the checks read it in.</summary>
        public static string RetireEnglish(RetireWhy why)
        {
            switch (why)
            {
                case RetireWhy.Ok: return "You may retire. The estate passes as it would at a death, by the same tax, and you stay in the family.";
                case RetireWhy.Running: return "The estate is passing now.";
                case RetireWhy.Young: return "You are not yet the age your will sets for retiring.";
                case RetireWhy.NoHeir: return "Nobody can carry on: an heir is your partner, a grown child or a relative your will names.";
                case RetireWhy.Session: return "Nothing changes hands in a Going Public session. Retire when you are out of it.";
                case RetireWhy.Law: return "The law has not finished with you: a case, a sentence, probation or a suspended licence. Retiring is no way out of it.";
                case RetireWhy.Elder: return "The family already keeps a retired founder. One at a time.";
                case RetireWhy.NoWill: return "Sign a will first: it sets the age you may retire from and what you keep.";
                default: return "Dynasty is not running for this save.";
            }
        }

        // ---- the family line

        private static void Line(VentModel m, DynastyFacts f, VentWords w)
        {
            var left = new List<VentBlock>(10);
            var right = new List<VentBlock>(12);
            left.Add(VentBlock.Section(w.T("reallife_dynasty_sec_line", "The heads of the family")));
            var heads = new List<VentItem>(f.Line.Count + 1);
            heads.Add(new VentItem
            {
                Icon = UiIcon.Person, Face = f.Name, Hue = VentWords.HueOf(f.Name), Title = f.Name,
                Sub = w.F("reallife_dynasty_head_now", "Generation {n}, {age} years old", "n", f.Generation.ToString(), "age", f.Age.ToString()),
                Chip = w.T("reallife_dynasty_chip_head", "Head"), ChipTone = UiRole.Ok, Amount = string.Empty, Run = -1
            });
            for (int i = f.Line.Count - 1; i >= 0; i--)
            {
                LineEntry e = f.Line[i];
                heads.Add(new VentItem
                {
                    Icon = UiIcon.Person, Face = e.Name, Hue = VentWords.HueOf(e.Name), Title = e.Name,
                    Sub = w.F("reallife_dynasty_head_past", "Generation {n}, handed over on day {day}: an estate of {net}, tax {tax}", "n", e.Generation.ToString(), "day", e.Day.ToString(),
                        "net", w.Money(e.Net), "tax", w.Money(e.Tax)),
                    Chip = ModeWord(e.Mode, w), Amount = string.Empty, Run = -1
                });
            }
            left.Add(new VentBlock { Kind = VentBlockKind.Items, Items = heads.ToArray() });
            if (f.Elder != null)
            {
                left.Add(VentBlock.Section(w.T("reallife_dynasty_sec_elder", "The retired founder")));
                left.Add(VentBlock.Row(f.Elder.Name, w.F("reallife_dynasty_elder_age", "{age} years old", "age", f.ElderAge.ToString())));
                left.Add(VentBlock.Row(w.T("reallife_dynasty_row_fund", "Their own fund"), w.Money(f.Elder.Fund)));
                left.Add(VentBlock.Row(w.T("reallife_dynasty_row_draw", "They live on, a month"), w.Money(f.Elder.Draw)));
                left.Add(VentBlock.Note(w.F("reallife_dynasty_elder_note", "At {age} what is left of the fund passes to the head of the family, taxed with the estate they handed over"
                    + " if that was within {years} years.",
                    "age", DynastyCatalogue.LastAge.ToString(), "years", f.WindowYears.ToString())));
            }
            if (f.Kin.Count > 0)
            {
                left.Add(VentBlock.Section(w.T("reallife_dynasty_sec_kin", "Left to the family")));
                for (int i = 0; i < f.Kin.Count; i++)
                {
                    left.Add(VentBlock.Row(f.Kin[i].Name, w.Money(f.Kin[i].Held)));
                }
                left.Add(VentBlock.Note(w.T("reallife_dynasty_kin_note", "What a will leaves somebody is theirs for good. It does not come back.")));
            }
            right.Add(VentBlock.Section(w.T("reallife_dynasty_sec_rivals", "The rivals' families")));
            if (f.RivalLines.Count == 0)
            {
                right.Add(VentBlock.Note(w.T("reallife_dynasty_rivals_none", "No rival tycoon has come forward yet. When they do, they age, retire and die,"
                    + " and their heirs pay the same estate tax.")));
            }
            for (int i = 0; i < f.RivalLines.Count; i++)
            {
                right.Add(VentBlock.Note(f.RivalLines[i]));
            }
            CityEstates c = f.City;
            right.Add(VentBlock.Section(w.T("reallife_dynasty_sec_city", "The city's estates")));
            right.Add(VentBlock.Row(w.T("reallife_dynasty_row_city_now", "Households ended this year"),
                w.F("reallife_dynasty_row_city_value", "{n}, leaving {amount}", "n", c.Count.ToString(), "amount", w.Money(c.Value))));
            right.Add(VentBlock.Row(w.T("reallife_dynasty_row_city_tax", "Estate tax on them by the same bands"),
                w.F("reallife_dynasty_row_city_tax_value", "{amount} on {n}", "amount", w.Money(c.Tax), "n", c.Taxed.ToString())));
            right.Add(VentBlock.Row(w.T("reallife_dynasty_row_city_unpaid", "Debts nothing was left to pay"), w.Money(c.Unpaid)));
            right.Add(VentBlock.Row(w.T("reallife_dynasty_row_city_last", "Last year"),
                w.F("reallife_dynasty_row_city_value", "{n}, leaving {amount}", "n", c.LastCount.ToString(), "amount", w.Money(c.LastValue))));
            right.Add(VentBlock.Note(w.T("reallife_dynasty_city_note", "A household that loses somebody keeps its savings and its debts: the ones left inherit."
                + " One with nobody left is taken in by family beyond the city, and its savings go with it.")));
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }
    }
}
