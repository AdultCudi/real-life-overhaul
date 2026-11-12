using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul.Ui;

// The Company app's Staff tab: the list of everybody who works for the company, the summary over it and the cards. Every person, wage, standing and day comes from the sample world (UiSamples.Company.World.cs).
internal static partial class UiSamples
{
    private static CompanyBlock CSec(string t, string right = null) => new CompanyBlock { Kind = CompanyBlockKind.Section, Text = t, Value = right };

    private static CompanyBlock CRow(string label, string value, UiRole tone = UiRole.None) => new CompanyBlock { Kind = CompanyBlockKind.Row, Text = label, Value = value, Tone = tone };

    private static CompanyBlock CNote(string text, UiRole tone = UiRole.None) => new CompanyBlock { Kind = CompanyBlockKind.Note, Text = text, Tone = tone };

    private static CompanyBlock CBtns(params CompanyButton[] b) => new CompanyBlock { Kind = CompanyBlockKind.Buttons, Buttons = b };

    private static CompanyButton[] CBack(CompanyWords w, string pick, int tab, int sub = 0) => new[] { CB(w.T("reallife_co_back", "Back"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, pick, tab, sub), UiIcon.Back) };

    private static CompanyBlock CMeter(CompanyWords w, string key, string english, int value) =>
        new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T(key, english), Value = value.ToString(), Number = value, Tone = value < CompanyWorld.LowBelow ? UiRole.Danger : value < 60 ? UiRole.Warn : UiRole.Ok };

    // The ring of a person's portrait: their mood in the staff list, so low standing and what waits on the owner read at once.
    private static UiRole StaffRing(CPerson p) => p.Low ? UiRole.Danger : p.Asked > 0 || p.Rival != null || p.Under > 0 ? UiRole.Warn : UiRole.Ok;

    private static string GoingNote(CompanyWords w, CPerson p) => w.F("reallife_co_going", "going {wage}", "wage", CompanyWorld.Wage(p.Going));

    // A person as a row of the staff list: their place, their pay and the going rate, and what they wait on.
    private static CompanyRow StaffRow(CompanyWords w, CPerson p)
    {
        var row = new CompanyRow
        {
            Id = "emp:" + p.Id, Title = p.Name, Sub = Cw.Pl(p.Place).Name, Face = true, Hue = p.Hue, Ring = StaffRing(p), Figure = CompanyWorld.PerHour(p.Paid), Note = GoingNote(w, p)
        };
        if (p.Asked > 0)
        {
            row.Chip = w.T("reallife_co_row_asks", "Asks for a raise");
            row.ChipTone = UiRole.Warn;
        }
        else if (p.Rival != null)
        {
            row.Chip = w.T("reallife_co_row_offer", "Rival's offer");
            row.ChipTone = UiRole.Danger;
        }
        return row;
    }

    // A person as an item of a card's list: the work they do, their hours and their pay; or, as a manager's crew, their skill graded.
    private static CompanyItem CrewItem(CompanyWords w, CPerson p, bool skill = false) => new CompanyItem
    {
        Title = p.Name, Sub = Pn(w, p), Face = true, Hue = p.Hue, Ring = p.Low ? UiRole.Danger : UiRole.Ok, Grade = skill ? p.MSkill : -1, Figure = skill ? p.MSkill.ToString() : w.F("reallife_rota_hours_n", "{hours} h", "hours", CompanyWorld.DayHours.ToString()),
        Note = skill ? null : CompanyWorld.PerHour(p.Paid), Press = true, Act = CA(CompanyDo.Go, "emp:" + p.Id, CompanyView.Staff)
    };

    // A heading's count of a list that shows only its first rows: the rows shown against all there are, so that the figure never says more than the list holds.
    private static string CountOf(CompanyWords w, int shown, int all) => shown >= all ? all.ToString() : w.F("reallife_rota_blocks_count", "{count} of {most}", "count", shown.ToString(), "most", all.ToString());

    private static void CompanyStaff(CompanyFacts f, CompanyWords w, string state)
    {
        CompanyPage p = f.Staff;
        bool one = Cw.Places.Count == 1;
        var rows = new List<CompanyRow>();
        CPlace speedy = one ? null : Cw.Pl("Speedy Bites"), hudson = one ? null : Cw.Pl("Hudson Warehouse");
        if (!one)
        {
            rows.Add(new CompanyRow { Id = "strike:Speedy Bites~7", Title = w.T("reallife_co_row_strike", "Strike called"), Sub = speedy.Name, Icon = UiIcon.Megaphone, Figure = w.F("reallife_co_row_strike_from", "from day {day}", "day", Dn(StrikeFrom)),
                Chip = w.T("reallife_co_row_answer", "Answer"), ChipTone = UiRole.Danger });
            rows.Add(new CompanyRow { Id = "ot:Hudson Warehouse~4", Title = w.T("reallife_co_row_overtime", "Overtime offered"), Sub = hudson.Name, Icon = UiIcon.Clock, Figure = w.F("reallife_co_row_a_day", "{amount} a day", "amount", CM(OvertimeCost(hudson))),
                Chip = w.T("reallife_co_row_answer", "Answer"), ChipTone = UiRole.Warn });
        }
        // Who waits on the owner first, then who is paid under the going rate, then who stands low, then the rest in the order of their places.
        var order = new List<CPerson>(Cw.People);
        order.Sort((a, b) => StaffRank(a).CompareTo(StaffRank(b)));
        foreach (CPerson person in order)
        {
            rows.Add(StaffRow(w, person));
        }
        p.List = new CompanyList { Title = w.T("reallife_co_staff_list", "Staff"), Count = Cw.People.Count.ToString(), Rows = rows.ToArray(), Empty = w.T("reallife_co_row_no_staff", "Nobody works for you yet.") };
        if (!one)
        {
            p.List.Filter = 0;
            var businesses = new List<string> { w.T("reallife_co_all_businesses", "All businesses") };
            foreach (CPlace pl in Cw.Places)
            {
                businesses.Add(pl.Name);
            }
            p.List.Bars = new[]
            {
                new CompanyFilterBar
                {
                    Options = new[] { w.T("reallife_co_f_all", "All"), w.T("reallife_co_f_waiting", "Waiting"), w.T("reallife_tab_co_staff_leaving", "Leaving"), w.T("reallife_tab_co_staff_underpaid", "Underpaid"),
                        w.T("reallife_tab_co_staff_fine", "In order") }, Chosen = 0, Shift = 0, Bits = 3, Toggle = true
                },
                new CompanyFilterBar { Options = businesses.ToArray(), Chosen = 0, Shift = 6, Bits = 7, Step = true },
                new CompanyFilterBar
                {
                    Options = new[] { w.T("reallife_co_sort_waiting", "waiting first"), w.T("reallife_co_sort_name", "name"), w.T("reallife_co_sort_business", "business"), w.T("reallife_co_sort_pay", "pay"),
                        w.T("reallife_co_sort_standing", "standing"), w.T("reallife_co_sort_skill", "skill") }, Chosen = 0, Shift = 3, Bits = 3, Step = true, Label = w.T("reallife_co_f_sort", "Sort: {choice}")
                }
            };
        }
        string pick = state == "staff_strike" ? "strike:Speedy Bites~7" : state == "staff_overtime" ? "ot:Hudson Warehouse~4" : state == "staff_offer" ? "emp:lena" : state == "staff_give" ? "emp:sana" : one ? "emp:ana" : state == "staff_summary" ? null : "emp:dev";
        for (int i = 0; i < rows.Count; i++)
        {
            p.List.Chosen = rows[i].Id == pick ? i : p.List.Chosen;
        }
        string back = "";
        if (pick == null)
        {
            p.List.Chosen = -1;
            p.Top = StaffSummary(w, one);
        }
        else if (pick == "strike:Speedy Bites~7")
        {
            p.Card = StrikeCardSample(w, back);
        }
        else if (pick == "ot:Hudson Warehouse~4")
        {
            p.Card = OvertimeCardSample(w, back);
        }
        else
        {
            p.Card = PersonCard(w, Cw.P(pick.Substring(4)));
        }
    }

    private const int StrikeFrom = 216, StrikeEnd = 218;      // the days the strike at Speedy Bites is called for, and the day it ends
    private const int StrikePayEach = 54;                     // a one-off payment to each, whole money

    private static int StaffRank(CPerson p) => p.Asked > 0 ? 0 : p.Rival != null ? 1 : p.Under > 0 ? 2 : p.Low ? 3 : 4;

    // What the crew of a place offer overtime for a day: their pay for the extra hours at time and a half.
    private static int OvertimeCost(CPlace pl)
    {
        int hundredths = 0;
        foreach (CPerson p in Cw.Crew(pl.Key))
        {
            hundredths += p.Paid * 2 * 3 / 2;
        }
        return (hundredths + 50) / 100;
    }

    private static CompanyItem WhoRow(CompanyWords w, CPerson p, string figure, int grade) =>
        new CompanyItem { Title = p.Name, Sub = Cw.Pl(p.Place).Name, Face = true, Hue = p.Hue, Ring = StaffRing(p), Grade = grade, Figure = figure, Press = true, Act = CA(CompanyDo.Go, "emp:" + p.Id, CompanyView.Staff) };

    private static CompanyCard StaffSummary(CompanyWords w, bool one)
    {
        var c = new CompanyCard { Title = w.T("reallife_co_staff_title", "Your staff"), Icon = UiIcon.People };
        List<CPerson> under = Cw.Underpaid(), low = Cw.LowStanding(), asking = Cw.Asking();
        c.FactLabels = one ? new[] { w.T("reallife_co_f_staff", "Staff") } : new[] { w.T("reallife_co_f_staff", "Staff"), w.T("reallife_co_f_underpaid", "Underpaid"), w.T("reallife_co_f_low", "Standing low") };
        c.FactValues = one ? new[] { Cw.People.Count.ToString() } : new[] { Cw.People.Count.ToString(), under.Count.ToString(), low.Count.ToString() };
        var blocks = new List<CompanyBlock>();
        if (one)
        {
            blocks.Add(CSec(w.T("reallife_co_staff_people", "Your people")));
            foreach (CPerson p in Cw.People)
            {
                blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = new[] { CrewItem(w, p) } });
                blocks.Add(CMeter(w, "reallife_co_f_standing", "Standing", p.Standing));
                blocks.Add(CMeter(w, "reallife_co_f_mood", "Mood", p.Mood));
            }
        }
        if (!one)
        {
            int total = 0;
            foreach (CPerson p in asking)
            {
                total += p.Asked - p.Paid;
            }
            blocks.Add(CSec(w.T("reallife_co_staff_asks_sec", "Asking for a raise")));
            blocks.Add(CNote(w.F("reallife_co_staff_asks", "{count} people ask for a raise, {amount} an hour between them.", "count", asking.Count.ToString(), "amount", CompanyWorld.Wage(total)), UiRole.WarnText));
            blocks.Add(CBtns(CB(w.T("reallife_co_staff_agree_all", "Agree to all"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.RaisesAll, null, 0), UiIcon.Check),
                CB(w.T("reallife_co_staff_less_all", "Offer less to all"), UiButtonKind.Secondary, UiAction.Option, CA(CompanyDo.RaisesAll, null, 2)),
                CB(w.T("reallife_co_staff_refuse_all", "Refuse all"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RaisesAll, null, 1), UiIcon.Close, false, null, true)));
            var asks = new List<CompanyItem>();
            foreach (CPerson p in asking)
            {
                asks.Add(new CompanyItem { Title = p.Name, Sub = Cw.Pl(p.Place).Name, Face = true, Hue = p.Hue, Ring = UiRole.Warn, Figure = CompanyWorld.PerHour(p.Asked), Note = w.F("reallife_co_staff_answer_by", "answer by day {day}", "day", Dn(p.AnswerBy)),
                    Grade = -1, Press = true, Act = CA(CompanyDo.Go, "emp:" + p.Id, CompanyView.Staff) });
            }
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = asks.ToArray() });
            // Paid under the going rate: the two furthest under (the list is the longer), and who stands lowest.
            under.Sort((a, b) => b.Under != a.Under ? b.Under.CompareTo(a.Under) : string.CompareOrdinal(a.Name, b.Name));
            blocks.Add(CSec(w.T("reallife_co_staff_under_sec", "Paid under the going rate"), w.F("reallife_rota_blocks_count", "{count} of {most}", "count", System.Math.Min(2, under.Count).ToString(), "most", under.Count.ToString())));
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = under.Take(2).Select(p => WhoRow(w, p, "-" + CompanyWorld.Wage(p.Under), -1)).ToArray() });
            blocks.Add(CSec(w.T("reallife_co_strike_low_sec", "Who stands low"), w.F("reallife_rota_blocks_count", "{count} of {most}", "count", System.Math.Min(2, low.Count).ToString(), "most", low.Count.ToString())));
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = low.Take(2).Select(p => WhoRow(w, p, p.Standing.ToString(), p.Standing)).ToArray() });
        }
        blocks.Add(new CompanyBlock
        {
            Kind = CompanyBlockKind.Choice, Text = w.T("reallife_co_staff_sick", "Sick pay"), Options = new[] { w.T("reallife_co_sick_none", "None"), w.T("reallife_co_sick_statutory", "Statutory"), w.T("reallife_co_sick_generous", "Generous") },
            Chosen = 1, Acts = new[] { CA(CompanyDo.SickPolicy, null, 0), CA(CompanyDo.SickPolicy, null, 1), CA(CompanyDo.SickPolicy, null, 2) }
        });
        blocks.Add(CNote(w.F("reallife_co_staff_sick_flat", "Off sick: paid {share} of a day's pay. Their standing does not change.", "share", "50%")));
        blocks.Add(new CompanyBlock
        {
            Kind = CompanyBlockKind.Link, Text = w.T("reallife_party_co_link", "Staff events"), Value = w.T("reallife_party_co_open", "Hold a staff event"), Act = CA(CompanyDo.OpenParties)
        });
        c.Blocks = blocks.ToArray();
        return c;
    }

    // A person's own page: what they earn and what they ask, how they feel, where they work, who works beside them. A raise asked, a rival's offer or pay under the going rate puts the answer first.
    private static CompanyCard PersonCard(CompanyWords w, CPerson p)
    {
        CPlace pl = Cw.Pl(p.Place);
        bool asks = p.Asked > 0, offer = p.Rival != null, give = !asks && !offer && p.Under > 0;
        bool plain = !asks && !offer && !give;
        var c = new CompanyCard
        {
            Kicker = w.T("reallife_co_tab_staff", "Staff"), Title = p.Name, Sub = pl.Name, Face = true, Hue = p.Hue, Ring = plain ? UiRole.Ok : StaffRing(p),
            FactLabels = new[] { w.T("reallife_co_f_paid", "Paid"), w.T("reallife_co_f_going", "Going rate") }, FactValues = new[] { CompanyWorld.Wage(p.Paid), CompanyWorld.Wage(p.Going) }
        };
        if (!plain)
        {
            c.Back = CBack(w, "", CompanyView.Staff);
        }
        if (asks)
        {
            c.Chips = new[] { w.T("reallife_co_emp_asks", "Asks for a raise") };
            c.ChipTones = new[] { UiRole.Warn };
        }
        else if (offer)
        {
            c.Chips = new[] { w.T("reallife_co_emp_offer", "A rival offers more") };
            c.ChipTones = new[] { UiRole.Danger };
        }
        var b = new List<CompanyBlock>();
        string first = FirstName(p.Name);
        if (offer)
        {
            b.Add(CSec(w.T("reallife_co_emp_offer", "A rival offers more")));
            b.Add(CNote(w.F("reallife_co_emp_offer_what", "{rival} offers {wage} an hour. You can answer until day {day}.", "rival", p.Rival, "wage", CompanyWorld.Wage(p.OfferWage), "day", Dn(p.OfferBy))));
            b.Add(CNote(w.F("reallife_co_emp_offer_choices", "Matching adds {raise} an hour. Letting go means {days} days' notice.", "raise", CompanyWorld.Wage(p.OfferWage - p.Paid), "days", "3")));
            b.Add(CBtns(CB(w.T("reallife_co_emp_match", "Match the offer"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.OfferAnswer, p.Id, 1), UiIcon.Check),
                CB(w.T("reallife_co_emp_let_go", "Let them go"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.OfferAnswer, p.Id, 0), UiIcon.Close, false, null, true)));
        }
        else if (asks)
        {
            b.Add(CSec(w.T("reallife_co_emp_asks", "Asks for a raise")));
            b.Add(CNote(w.F("reallife_co_emp_ask", "{name} asks for {wage} an hour. Answer by day {day}.", "name", first, "wage", CompanyWorld.Wage(p.Asked), "day", Dn(p.AnswerBy))));
            b.Add(CNote(w.F("reallife_co_emp_ask_choices", "Agreeing pays {all}; offering less pays {less}.", "all", CompanyWorld.Wage(p.Asked - p.Paid), "less", CompanyWorld.Wage((p.Asked - p.Paid) / 2))));
            b.Add(CBtns(CB(w.T("reallife_co_emp_agree", "Agree"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.RaiseAnswer, p.Id, 0), UiIcon.Check),
                CB(w.T("reallife_co_emp_less", "Offer less"), UiButtonKind.Secondary, UiAction.Option, CA(CompanyDo.RaiseAnswer, p.Id, 1)),
                CB(w.F("reallife_co_emp_bonus", "Bonus {amount}", "amount", CM(BonusAmount)), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RaiseAnswer, p.Id, 3)),
                CB(w.T("reallife_co_emp_refuse", "Refuse"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RaiseAnswer, p.Id, 2), UiIcon.Close, false, null, true)));
        }
        else if (give)
        {
            b.Add(CNote(w.F("reallife_co_emp_give_what", "Paid {raise} an hour under the going rate. A raise now keeps them.", "raise", CompanyWorld.Wage(p.Under))));
            b.Add(CBtns(CB(w.T("reallife_co_emp_give", "Give the raise"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.RaiseGive, p.Id), UiIcon.Check)));
        }
        b.Add(CSec(w.T("reallife_ibox_sec_pay", "Pay")));
        b.Add(CRow(w.L("reallife_wage_row_game", "The game's wage"), CompanyWorld.Wage(p.Game)));
        b.Add(CRow(w.L("reallife_wage_row_city", "The city's prices"), "+" + CompanyWorld.Wage(p.City)));
        if (p.Raise > 0)
        {
            b.Add(CRow(w.L("reallife_co_emp_raise", "Agreed raise on top"), CompanyWorld.Wage(p.Raise)));
        }
        if (plain)
        {
            b.Add(CRow(w.T("reallife_co_emp_hours_day", "Hours a day"), w.F("reallife_rota_hours_n", "{hours} h", "hours", CompanyWorld.DayHours.ToString())));
        }
        b.Add(new CompanyBlock { Kind = CompanyBlockKind.Break });
        b.Add(CSec(w.T("reallife_ibox_sec_feeling", "How they feel")));
        b.Add(CMeter(w, "reallife_co_f_standing", "Standing", p.Standing));
        b.Add(CMeter(w, "reallife_co_f_mood", "Mood", p.Mood));
        b.Add(CSec(w.T("reallife_co_emp_work_sec", "Their work")));
        b.Add(new CompanyBlock { Kind = CompanyBlockKind.Link, Text = w.T("reallife_co_business", "Business"), Value = pl.Name, Act = CA(CompanyDo.Go, pl.Key, CompanyView.Overview, CompanyView.SubBusiness) });
        b.Add(CRow(w.T("reallife_tab_co_col_skill", "Skill"), Pn(w, p)));
        if (!plain)
        {
            b.Add(CRow(w.T("reallife_co_emp_hours_day", "Hours a day"), w.F("reallife_rota_hours_n", "{hours} h", "hours", CompanyWorld.DayHours.ToString())));
        }
        b.Add(CRow(w.T("reallife_tab_co_col_day_pay", "Pay a day"), CM(p.Paid * CompanyWorld.DayHours / 100)));
        if (p.RaiseDays.Length > 0 && !plain)
        {
            b.Add(CSec(w.T("reallife_co_emp_raises_sec", "Recent raises")));
            for (int i = p.RaiseDays.Length - 1; i >= 0; i--)
            {
                b.Add(CRow(Dy(w, p.RaiseDays[i]), "+" + CompanyWorld.Wage(p.RaiseSizes[i]), UiRole.OkText));
            }
        }
        AddBeside(w, b, p, pl, plain);   // at the foot, as the live card has them: the answer first, the neighbours last
        if (Cw.People.Count <= SmallCrew)
        {
            // A small crew has no summary page, so the sick pay stands under the person (as the live card has it).
            b.Add(new CompanyBlock
            {
                Kind = CompanyBlockKind.Choice, Text = w.T("reallife_co_staff_sick", "Sick pay"), Options = new[] { w.T("reallife_co_sick_none", "None"), w.T("reallife_co_sick_statutory", "Statutory"), w.T("reallife_co_sick_generous", "Generous") },
                Chosen = 1, Acts = new[] { CA(CompanyDo.SickPolicy, null, 0), CA(CompanyDo.SickPolicy, null, 1), CA(CompanyDo.SickPolicy, null, 2) }
            });
            b.Add(CNote(w.F("reallife_co_staff_sick_flat", "Off sick: paid {share} of a day's pay. Their standing does not change.", "share", "50%")));
        }
        c.Blocks = b.ToArray();
        return c;
    }

    private const int SmallCrew = 4;   // a company of so few people has no summary page (CompanyLive.Staff.SmallCrew)

    // Who works beside a person: their count against the rows shown, and the first of them.
    private static void AddBeside(CompanyWords w, List<CompanyBlock> b, CPerson p, CPlace pl, bool all)
    {
        var beside = new List<CompanyItem>();
        foreach (CPerson other in Cw.StaffAt(p.Place))
        {
            if (other != p)
            {
                beside.Add(CrewItem(w, other));
            }
        }
        if (beside.Count > 0)
        {
            int shown = all ? beside.Count : System.Math.Min(3, beside.Count);
            b.Add(CSec(w.F("reallife_co_emp_beside", "Beside them at {business}", "business", pl.Name), CountOf(w, shown, beside.Count)));
            b.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = beside.Take(shown).ToArray() });
        }
    }

    private static CompanyCard StrikeCardSample(CompanyWords w, string back)
    {
        CPlace sb = Cw.Pl("Speedy Bites");
        List<CPerson> staff = Cw.StaffAt(sb.Key);
        var c = new CompanyCard
        {
            Kicker = w.T("reallife_co_tab_staff", "Staff"), Title = w.F("reallife_co_strike_title", "Strike at {business}", "business", sb.Name), Icon = UiIcon.Megaphone,
            Chips = new[] { w.T("reallife_ibox_chip_called", "Called") }, ChipTones = new[] { UiRole.Danger }, Back = CBack(w, "", CompanyView.Staff)
        };
        var low = new List<CPerson>();
        int raiseEach = 0;
        foreach (CPerson p in staff)
        {
            low.Add(p.Low ? p : null);
            raiseEach += p.Under;
        }
        low.RemoveAll(p => p == null);
        low.Sort((a, b) => a.Standing.CompareTo(b.Standing));
        var rows = new List<CompanyItem>();
        foreach (CPerson p in low)
        {
            rows.Add(new CompanyItem { Title = p.Name, Sub = Pn(w, p), Face = true, Hue = p.Hue, Ring = UiRole.Danger, Figure = p.Standing.ToString(), Grade = p.Standing, Press = true, Act = CA(CompanyDo.Go, "emp:" + p.Id, CompanyView.Staff) });
        }
        c.Blocks = new[]
        {
            CNote(w.F("reallife_co_strike_called", "A strike is called from day {from} to day {end}.", "from", Dn(StrikeFrom), "end", Dn(StrikeEnd)), UiRole.DangerText),
            CNote(w.F("reallife_co_strike_effect", "Footfall falls {percent}% and the shop is shut {hours} hours a day.", "percent", "30", "hours", "6")),
            CNote(w.F("reallife_co_strike_why", "{low} of {count} people stand low.", "low", low.Count.ToString(), "count", staff.Count.ToString())),
            CSec(w.T("reallife_co_strike_low_sec", "Who stands low"), low.Count.ToString()),
            new CompanyBlock { Kind = CompanyBlockKind.Items, Items = rows.ToArray() },
            CSec(w.T("reallife_co_strike_ways", "Ways to settle it")),
            CRow(w.T("reallife_co_strike_do_raise", "Raise everyone to the going rate"), CompanyWorld.PerHour(raiseEach)),
            CRow(w.T("reallife_co_strike_do_payment", "A one-off payment to each"), CM(StrikePayEach * staff.Count)),
            CNote(w.F("reallife_co_strike_sit", "Or sit it out until day {day}.", "day", Dn(StrikeEnd))),
            CBtns(CB(w.T("reallife_co_strike_do_raise", "Raise everyone to the going rate"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.StrikeAnswer, "Speedy Bites~7", 0), UiIcon.Check),
                CB(w.T("reallife_co_strike_do_payment", "A one-off payment to each"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.StrikeAnswer, "Speedy Bites~7", 1)),
                CB(w.T("reallife_co_strike_do_sit", "Sit it out"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "", CompanyView.Staff), UiIcon.Close))
        };
        return c;
    }

    private static CompanyCard OvertimeCardSample(CompanyWords w, string back)
    {
        CPlace hw = Cw.Pl("Hudson Warehouse");
        var c = new CompanyCard
        {
            Kicker = w.T("reallife_co_tab_staff", "Staff"), Title = w.F("reallife_co_overtime_title", "Overtime at {business}", "business", hw.Name), Icon = UiIcon.Clock, Back = CBack(w, "", CompanyView.Staff)
        };
        List<CPerson> crew = Cw.Crew(hw.Key);
        c.Blocks = new[]
        {
            CNote(w.F("reallife_co_overtime_by", "Answer by day {day}.", "day", Dn(OvertimeBy)), UiRole.WarnText),
            CNote(w.F("reallife_co_overtime_what", "The crew offers {hours} extra hours a day for {days} days.", "hours", "2", "days", "5")),
            CSec(w.T("reallife_ibox_sec_terms", "Terms")),
            CRow(w.L("reallife_co_overtime_cost", "Cost"), w.F("reallife_co_row_a_day", "{amount} a day", "amount", CM(OvertimeCost(hw)))),
            CRow(w.L("reallife_co_overtime_gain", "Footfall served"), "+12%"),
            CSec(w.T("reallife_co_staff_people", "Your people"), crew.Count.ToString()),
            new CompanyBlock { Kind = CompanyBlockKind.Items, Items = crew.Select(p => CrewItem(w, p)).ToArray() },
            CBtns(CB(w.T("reallife_co_overtime_agree", "Agree"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.OvertimeAnswer, "Hudson Warehouse~4", 1), UiIcon.Check),
                CB(w.T("reallife_co_overtime_refuse", "Refuse"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.OvertimeAnswer, "Hudson Warehouse~4", 0), UiIcon.Close, false, null, true))
        };
        return c;
    }

    private const int OvertimeBy = 217;   // the day the overtime offered at Hudson Warehouse is answered by
}
