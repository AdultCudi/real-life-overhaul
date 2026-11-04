using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul.Ui;

// The Company app's people tabs: Staff and Managers with their sub pages. Samples' own names; the live source reads the player's people.
internal static partial class UiSamples
{
    private static CompanyBlock CSec(string t, string right = null) => new CompanyBlock { Kind = CompanyBlockKind.Section, Text = t, Value = right };

    private static CompanyBlock CRow(string label, string value, UiRole tone = UiRole.None) => new CompanyBlock { Kind = CompanyBlockKind.Row, Text = label, Value = value, Tone = tone };

    private static CompanyBlock CNote(string text, UiRole tone = UiRole.None) => new CompanyBlock { Kind = CompanyBlockKind.Note, Text = text, Tone = tone };

    private static CompanyBlock CBtns(params CompanyButton[] b) => new CompanyBlock { Kind = CompanyBlockKind.Buttons, Buttons = b };

    private static CompanyButton[] CBack(CompanyWords w, string pick, int tab, int sub = 0) => new[] { CB(w.T("reallife_co_back", "Back"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, pick, tab, sub), UiIcon.Back) };

    // ------------------------------------------------------------------------------------------------- Staff

    private static void CompanyStaff(CompanyFacts f, CompanyWords w, string state)
    {
        CompanyPage p = f.Staff;
        bool one = state == "staff_one";
        var rows = new List<CompanyRow>();
        if (!one)
        {
            rows.Add(new CompanyRow { Id = "strike:Speedy Bites~7", Title = w.T("reallife_co_row_strike", "Strike called"), Sub = "Speedy Bites", Icon = UiIcon.Megaphone, Figure = w.F("reallife_co_row_strike_from", "from day {day}", "day", "216"), Chip = w.T("reallife_co_row_answer", "Answer"), ChipTone = UiRole.Danger });
            rows.Add(new CompanyRow { Id = "ot:Hudson Warehouse~4", Title = w.T("reallife_co_row_overtime", "Overtime offered"), Sub = "Hudson Warehouse", Icon = UiIcon.Clock, Figure = w.F("reallife_co_row_a_day", "{amount} a day", "amount", "$190"), Chip = w.T("reallife_co_row_answer", "Answer"), ChipTone = UiRole.Warn });
            rows.Add(new CompanyRow { Id = "emp:dev", Title = "Dev Kapoor", Sub = "Speedy Bites", Face = true, Hue = 0.35f, Ring = UiRole.Warn, Figure = "$17.00 / h", Note = w.F("reallife_co_going", "going {wage}", "wage", "$19.00"), Chip = w.T("reallife_co_row_asks", "Asks for a raise"), ChipTone = UiRole.Warn });
            rows.Add(new CompanyRow { Id = "emp:lena", Title = "Lena Adler", Sub = "Speedy Bites", Face = true, Hue = 0.1f, Ring = UiRole.Warn, Figure = "$16.50 / h", Note = w.F("reallife_co_going", "going {wage}", "wage", "$16.50"), Chip = w.T("reallife_co_row_offer", "Rival's offer"), ChipTone = UiRole.Danger });
            rows.Add(new CompanyRow { Id = "emp:maya", Title = "Maya Okafor", Sub = "Bloom & Co", Face = true, Hue = 0.6f, Ring = UiRole.Warn, Figure = "$18.20 / h", Note = w.F("reallife_co_going", "going {wage}", "wage", "$19.70"),
                Chip = w.T("reallife_co_row_asks", "Asks for a raise"), ChipTone = UiRole.Warn });
            rows.Add(new CompanyRow { Id = "emp:tomas", Title = "Tomas Reyes", Sub = "Hudson Warehouse", Face = true, Hue = 0.8f, Ring = UiRole.Ok, Figure = "$15.40 / h", Note = w.F("reallife_co_going", "going {wage}", "wage", "$15.40") });
            rows.Add(new CompanyRow { Id = "emp:sana", Title = "Sana Malik", Sub = "Pioneer Game Studio", Face = true, Hue = 0.25f, Ring = UiRole.Danger, Figure = "$24.00 / h", Note = w.F("reallife_co_going", "going {wage}", "wage", "$26.00") });
            string[][] more =
            {
                new[] { "rui", "Rui Tanaka", "Speedy Bites", "15.90", "0.5" }, new[] { "ines", "Ines Duarte", "Speedy Bites", "16.20", "0.15" }, new[] { "hana", "Hana Sato", "Speedy Bites", "15.60", "0.8" },
                new[] { "leo", "Leo Martin", "Speedy Bites", "16.00", "0.3" }, new[] { "hugo", "Hugo Brandt", "Hudson Warehouse", "15.60", "0.65" }, new[] { "mei", "Mei Lin", "Hudson Warehouse", "15.60", "0.05" },
                new[] { "carlos", "Carlos Vega", "Hudson Warehouse", "16.80", "0.4" }, new[] { "jonah", "Jonah Reyes", "Bloom & Co", "17.10", "0.55" }, new[] { "inespark", "Ines Park", "Pioneer Game Studio", "22.50", "0.9" }
            };
            for (int i = 0; i < more.Length; i++)
            {
                rows.Add(new CompanyRow { Id = "emp:" + more[i][0], Title = more[i][1], Sub = more[i][2], Face = true, Hue = float.Parse(more[i][4], System.Globalization.CultureInfo.InvariantCulture), Ring = i < 4 ? UiRole.Danger : UiRole.Ok,
                    Figure = "$" + more[i][3] + " / h", Note = w.F("reallife_co_going", "going {wage}", "wage", "$" + more[i][3]) });
            }
        }
        else
        {
            rows.Add(new CompanyRow { Id = "emp:ana", Title = "Ana Costa", Sub = "Corner Cafe", Face = true, Hue = 0.45f, Ring = UiRole.Ok, Figure = "$13.00 / h", Note = w.F("reallife_co_going", "going {wage}", "wage", "$13.00") });
            rows.Add(new CompanyRow { Id = "emp:jin", Title = "Jin Park", Sub = "Corner Cafe", Face = true, Hue = 0.7f, Ring = UiRole.Ok, Figure = "$13.00 / h", Note = w.F("reallife_co_going", "going {wage}", "wage", "$13.00") });
        }
        p.List = new CompanyList { Title = w.T("reallife_co_staff_list", "Staff"), Count = one ? "2" : "31", Rows = rows.ToArray(), Empty = w.T("reallife_co_row_no_staff", "Nobody works for you yet.") };
        if (!one)
        {
            p.List.Filter = 0;
            p.List.Bars = new[]
            {
                new CompanyFilterBar
                {
                    Options = new[] { w.T("reallife_co_f_all", "All"), w.T("reallife_co_f_waiting", "Waiting"), w.T("reallife_tab_co_staff_leaving", "Leaving"), w.T("reallife_tab_co_staff_underpaid", "Underpaid"),
                        w.T("reallife_tab_co_staff_fine", "In order") }, Chosen = 0, Shift = 0, Bits = 3, Toggle = true
                },
                new CompanyFilterBar { Options = new[] { w.T("reallife_co_all_businesses", "All businesses"), "Speedy Bites", "Bloom & Co", "Pioneer Game Studio", "Hudson Warehouse" }, Chosen = 0, Shift = 6, Bits = 7, Step = true },
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
            p.Card = pick == "emp:sana" ? GiveCardSample(w) : pick == "emp:ana" ? PlainCardSample(w) : EmployeeCardSample(w, pick == "emp:lena");
        }
    }

    private static CompanyItem WhoRow(string name, string place, float hue, int grade, string figure, string id, UiRole ring) =>
        new CompanyItem { Title = name, Sub = place, Face = true, Hue = hue, Ring = ring, Grade = grade, Figure = figure, Press = true, Act = CA(CompanyDo.Go, "emp:" + id, CompanyView.Staff) };

    private static CompanyCard StaffSummary(CompanyWords w, bool one)
    {
        var c = new CompanyCard { Title = w.T("reallife_co_staff_title", "Your staff"), Icon = UiIcon.People };
        c.FactLabels = one ? new[] { w.T("reallife_co_f_staff", "Staff") } : new[] { w.T("reallife_co_f_staff", "Staff"), w.T("reallife_co_f_underpaid", "Underpaid"), w.T("reallife_co_f_low", "Standing low") };
        c.FactValues = one ? new[] { "2" } : new[] { "31", "4", "5" };
        var blocks = new List<CompanyBlock>();
        if (one)
        {
            blocks.Add(CSec(w.T("reallife_co_staff_people", "Your people")));
            blocks.Add(new CompanyBlock
            {
                Kind = CompanyBlockKind.Items, Items = new[] { new CompanyItem { Title = "Ana Costa", Sub = "Corner Cafe", Face = true, Hue = 0.45f, Ring = UiRole.Ok, Figure = "$13.00 / h", Note = w.F("reallife_co_going", "going {wage}", "wage", "$13.00"), Grade = -1, Press = true, Act = CA(CompanyDo.Go, "emp:ana", CompanyView.Staff) } }
            });
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_standing", "Standing"), Value = "72", Number = 72, Tone = UiRole.Ok });
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_mood", "Mood"), Value = "66", Number = 66, Tone = UiRole.Ok });
            blocks.Add(new CompanyBlock
            {
                Kind = CompanyBlockKind.Items, Items = new[] { new CompanyItem { Title = "Jin Park", Sub = "Corner Cafe", Face = true, Hue = 0.7f, Ring = UiRole.Ok, Figure = "$13.00 / h", Note = w.F("reallife_co_going", "going {wage}", "wage", "$13.00"), Grade = -1, Press = true, Act = CA(CompanyDo.Go, "emp:jin", CompanyView.Staff) } }
            });
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_standing", "Standing"), Value = "64", Number = 64, Tone = UiRole.Ok });
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_mood", "Mood"), Value = "70", Number = 70, Tone = UiRole.Ok });
        }
        if (!one)
        {
            blocks.Add(CSec(w.T("reallife_co_staff_asks_sec", "Asking for a raise")));
            blocks.Add(CNote(w.F("reallife_co_staff_asks", "{count} people ask for a raise, {amount} an hour between them.", "count", "2", "amount", "$3.50"), UiRole.WarnText));
            blocks.Add(CBtns(CB(w.T("reallife_co_staff_agree_all", "Agree to all"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.RaisesAll, null, 0), UiIcon.Check),
                CB(w.T("reallife_co_staff_refuse_all", "Refuse all"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RaisesAll, null, 1), UiIcon.Close, false, null, true)));
            blocks.Add(new CompanyBlock
            {
                Kind = CompanyBlockKind.Items, Items = new[]
                {
                    new CompanyItem { Title = "Dev Kapoor", Sub = "Speedy Bites", Face = true, Hue = 0.35f, Ring = UiRole.Warn, Figure = "$19.00 / h", Note = w.F("reallife_co_staff_answer_by", "answer by day {day}", "day", "218"),
                        Grade = -1, Press = true, Act = CA(CompanyDo.Go, "emp:dev", CompanyView.Staff) },
                    new CompanyItem { Title = "Maya Okafor", Sub = "Bloom & Co", Face = true, Hue = 0.6f, Ring = UiRole.Warn, Figure = "$19.70 / h", Note = w.F("reallife_co_staff_answer_by", "answer by day {day}", "day", "220"),
                        Grade = -1, Press = true, Act = CA(CompanyDo.Go, "emp:maya", CompanyView.Staff) }
                }
            });
        }
        if (!one)
        {
            blocks.Add(CSec(w.T("reallife_co_staff_under_sec", "Paid under the going rate"), "4"));
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = new[]
            {
                WhoRow("Sana Malik", "Pioneer Game Studio", 0.25f, -1, "-$2.00", "sana", UiRole.Warn), WhoRow("Dev Kapoor", "Speedy Bites", 0.35f, -1, "-$2.00", "dev", UiRole.Warn),
                WhoRow("Maya Okafor", "Bloom & Co", 0.6f, -1, "-$1.50", "maya", UiRole.Warn), WhoRow("Rui Tanaka", "Speedy Bites", 0.5f, -1, "-$0.90", "rui", UiRole.Warn)
            } });
            blocks.Add(CSec(w.T("reallife_co_strike_low_sec", "Who stands low"), "5"));
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = new[]
            {
                WhoRow("Rui Tanaka", "Speedy Bites", 0.5f, 28, "28", "rui", UiRole.Danger), WhoRow("Sana Malik", "Pioneer Game Studio", 0.25f, 31, "31", "sana", UiRole.Danger),
                WhoRow("Ines Duarte", "Speedy Bites", 0.15f, 33, "33", "ines", UiRole.Danger), WhoRow("Hana Sato", "Speedy Bites", 0.8f, 35, "35", "hana", UiRole.Danger)
            } });
        }
        blocks.Add(new CompanyBlock
        {
            Kind = CompanyBlockKind.Choice, Text = w.T("reallife_co_staff_sick", "Sick pay"), Options = new[] { w.T("reallife_co_sick_none", "None"), w.T("reallife_co_sick_statutory", "Statutory"), w.T("reallife_co_sick_generous", "Generous") },
            Chosen = 1, Acts = new[] { CA(CompanyDo.SickPolicy, null, 0), CA(CompanyDo.SickPolicy, null, 1), CA(CompanyDo.SickPolicy, null, 2) }
        });
        blocks.Add(CNote(w.F("reallife_co_staff_sick_flat", "Off sick: paid {share} of a day's pay. Their standing does not change.", "share", "50%")));
        blocks.Add(CNote(w.T("reallife_co_staff_market", "A rival may offer your people more when they are paid under the going rate.")));
        if (!one)
        {
            blocks.Add(CNote(w.T("reallife_co_staff_picked", "Pick a person at the left to see what they earn, what they ask and how they feel.")));
        }
        c.Blocks = blocks.ToArray();
        return c;
    }

    // A person who asks for nothing and is paid the going rate: what they earn, how they feel, their work, and the policy for sick pay under them.
    private static CompanyCard PlainCardSample(CompanyWords w)
    {
        var c = new CompanyCard
        {
            Kicker = w.T("reallife_co_tab_staff", "Staff"), Title = "Ana Costa", Sub = "Corner Cafe", Face = true, Hue = 0.45f, Ring = UiRole.Ok,
            FactLabels = new[] { w.T("reallife_co_f_paid", "Paid"), w.T("reallife_co_f_going", "Going rate") }, FactValues = new[] { "$13.00", "$13.00" }
        };
        var b = new List<CompanyBlock>();
        b.Add(CSec(w.T("reallife_ibox_sec_pay", "Pay")));
        b.Add(CRow(SF("The game's wage"), "$12.00"));
        b.Add(CRow(SF("City prices"), "+$1.00"));
        b.Add(CRow(SF("Hours a day"), w.F("reallife_rota_hours_n", "{hours} h", "hours", "8")));
        b.Add(new CompanyBlock { Kind = CompanyBlockKind.Break });
        b.Add(CSec(w.T("reallife_ibox_sec_feeling", "How they feel")));
        b.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_standing", "Standing"), Value = "72", Number = 72, Tone = UiRole.Ok });
        b.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_mood", "Mood"), Value = "66", Number = 66, Tone = UiRole.Ok });
        b.Add(CSec(w.T("reallife_co_emp_work_sec", "Their work")));
        b.Add(new CompanyBlock { Kind = CompanyBlockKind.Link, Text = w.T("reallife_co_business", "Business"), Value = "Corner Cafe", Act = CA(CompanyDo.Go, "Corner Cafe~12", CompanyView.Overview, CompanyView.SubBusiness) });
        b.Add(CRow(w.T("reallife_tab_co_col_skill", "Skill"), "Cooking"));
        b.Add(CRow(w.T("reallife_tab_co_col_day_pay", "Pay a day"), "$104"));
        b.Add(new CompanyBlock
        {
            Kind = CompanyBlockKind.Choice, Text = w.T("reallife_co_staff_sick", "Sick pay"), Options = new[] { w.T("reallife_co_sick_none", "None"), w.T("reallife_co_sick_statutory", "Statutory"), w.T("reallife_co_sick_generous", "Generous") },
            Chosen = 1, Acts = new[] { CA(CompanyDo.SickPolicy, null, 0), CA(CompanyDo.SickPolicy, null, 1), CA(CompanyDo.SickPolicy, null, 2) }
        });
        b.Add(CNote(w.F("reallife_co_staff_sick_flat", "Off sick: paid {share} of a day's pay. Their standing does not change.", "share", "50%")));
        c.Blocks = b.ToArray();
        return c;
    }

    private static CompanyCard EmployeeCardSample(CompanyWords w, bool offer)
    {
        string name = offer ? "Lena Adler" : "Dev Kapoor";
        var c = new CompanyCard
        {
            Kicker = w.T("reallife_co_tab_staff", "Staff"), Title = name, Sub = "Speedy Bites", Face = true, Hue = offer ? 0.1f : 0.35f,
            Ring = UiRole.Warn, Chips = new[] { offer ? w.T("reallife_co_emp_offer", "A rival offers more") : w.T("reallife_co_emp_asks", "Asks for a raise") }, ChipTones = new[] { offer ? UiRole.Danger : UiRole.Warn },
            FactLabels = new[] { w.T("reallife_co_f_paid", "Paid"), w.T("reallife_co_f_going", "Going rate") },
            FactValues = offer ? new[] { "$16.50", "$16.50" } : new[] { "$17.00", "$19.00" }, Back = CBack(w, "", CompanyView.Staff)
        };
        var b = new List<CompanyBlock>();
        if (offer)
        {
            b.Add(CSec(w.T("reallife_co_emp_offer", "A rival offers more")));
            b.Add(CNote(w.F("reallife_co_emp_offer_what", "{rival} offers {wage} an hour. You can answer until day {day}.", "rival", "Harbour Grill", "wage", "$18.50", "day", "219")));
            b.Add(CNote(w.F("reallife_co_emp_offer_choices", "Matching adds {raise} an hour. Letting go means {days} days' notice.", "raise", "$2.00", "days", "3")));
            b.Add(CBtns(CB(w.T("reallife_co_emp_match", "Match the offer"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.OfferAnswer, "lena", 1), UiIcon.Check),
                CB(w.T("reallife_co_emp_let_go", "Let them go"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.OfferAnswer, "lena", 0), UiIcon.Close, false, null, true)));
        }
        else
        {
            b.Add(CSec(w.T("reallife_co_emp_asks", "Asks for a raise")));
            b.Add(CNote(w.F("reallife_co_emp_ask", "{name} asks for {wage} an hour. Answer by day {day}.", "name", "Dev", "wage", "$19.00", "day", "218")));
            b.Add(CNote(w.F("reallife_co_emp_ask_choices", "Agreeing pays {all}; offering less pays {less}.", "all", "$2.00", "less", "$1.00")));
            b.Add(CBtns(CB(w.T("reallife_co_emp_agree", "Agree"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.RaiseAnswer, "dev", 0), UiIcon.Check),
                CB(w.T("reallife_co_emp_less", "Offer less"), UiButtonKind.Secondary, UiAction.Option, CA(CompanyDo.RaiseAnswer, "dev", 1)),
                CB(w.F("reallife_co_emp_bonus", "Bonus {amount}", "amount", "$300"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RaiseAnswer, "dev", 3)),
                CB(w.T("reallife_co_emp_refuse", "Refuse"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RaiseAnswer, "dev", 2), UiIcon.Close, false, null, true)));
        }
        b.Add(CSec(w.T("reallife_ibox_sec_pay", "Pay")));
        b.Add(CRow(SF("The game's wage"), offer ? "$15.00" : "$16.00"));
        b.Add(CRow(SF("City prices"), "+$1.00"));
        if (offer)
        {
            b.Add(CRow(SF("Agreed raise"), "$0.50"));
        }
        else
        {
            b.Add(CRow(SF("Economy and district"), "+$2.00"));   // 16.00 + 1.00 + 2.00 is the going rate of the head
        }
        b.Add(new CompanyBlock { Kind = CompanyBlockKind.Break });
        b.Add(CSec(w.T("reallife_ibox_sec_feeling", "How they feel")));
        b.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_standing", "Standing"), Value = offer ? "64" : "52", Number = offer ? 64 : 52, Tone = UiRole.Warn });
        b.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_mood", "Mood"), Value = offer ? "58" : "47", Number = offer ? 58 : 47, Tone = UiRole.Warn });
        b.Add(CSec(w.T("reallife_co_emp_work_sec", "Their work")));
        b.Add(new CompanyBlock { Kind = CompanyBlockKind.Link, Text = w.T("reallife_co_business", "Business"), Value = "Speedy Bites", Act = CA(CompanyDo.Go, "Speedy Bites~7", CompanyView.Overview, CompanyView.SubBusiness) });
        b.Add(CRow(w.T("reallife_tab_co_col_skill", "Skill"), offer ? "Cooking" : "Customer service"));
        b.Add(CRow(w.T("reallife_co_emp_hours_day", "Hours a day"), w.F("reallife_rota_hours_n", "{hours} h", "hours", "8")));
        b.Add(CRow(w.T("reallife_tab_co_col_day_pay", "Pay a day"), offer ? "$132" : "$136"));
        b.Add(CSec(w.T("reallife_co_emp_raises_sec", "Recent raises")));
        b.Add(CRow(w.F("reallife_mgr_row_day", "Day {day}", "day", "188"), "+$0.50", UiRole.OkText));
        b.Add(CRow(w.F("reallife_mgr_row_day", "Day {day}", "day", "131"), "+$1.00", UiRole.OkText));
        c.Blocks = b.ToArray();
        return c;
    }

    // Somebody paid under the going rate who has not asked: the owner may give the raise unasked.
    private static CompanyCard GiveCardSample(CompanyWords w)
    {
        var c = new CompanyCard
        {
            Kicker = w.T("reallife_co_tab_staff", "Staff"), Title = "Sana Malik", Sub = "Pioneer Game Studio", Face = true, Hue = 0.25f, Ring = UiRole.Danger,
            FactLabels = new[] { w.T("reallife_co_f_paid", "Paid"), w.T("reallife_co_f_going", "Going rate") },
            FactValues = new[] { "$24.00", "$26.00" }, Back = CBack(w, "", CompanyView.Staff)
        };
        c.Blocks = new[]
        {
            CNote(w.F("reallife_co_emp_give_what", "Paid {raise} an hour under the going rate. A raise now keeps them.", "raise", "$2.00")),
            CBtns(CB(w.T("reallife_co_emp_give", "Give the raise"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.RaiseGive, "sana"), UiIcon.Check)),
            CSec(w.T("reallife_ibox_sec_pay", "Pay")),
            CRow(SF("The game's wage"), "$23.00"),
            CRow(SF("City prices"), "+$1.00"),
            new CompanyBlock { Kind = CompanyBlockKind.Break },
            CSec(w.T("reallife_ibox_sec_feeling", "How they feel")),
            new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_standing", "Standing"), Value = "31", Number = 31, Tone = UiRole.Danger },
            new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_mood", "Mood"), Value = "44", Number = 44, Tone = UiRole.Warn },
            CSec(w.T("reallife_co_emp_work_sec", "Their work")),
            new CompanyBlock { Kind = CompanyBlockKind.Link, Text = w.T("reallife_co_business", "Business"), Value = "Pioneer Game Studio", Act = CA(CompanyDo.Go, "Pioneer Game Studio~9", CompanyView.Overview, CompanyView.SubBusiness) },
            CRow(w.T("reallife_tab_co_col_skill", "Skill"), "Programming"),
            CRow(w.T("reallife_co_emp_hours_day", "Hours a day"), w.F("reallife_rota_hours_n", "{hours} h", "hours", "8")),
            CRow(w.T("reallife_tab_co_col_day_pay", "Pay a day"), "$192"),
            CSec(w.T("reallife_co_emp_raises_sec", "Recent raises")),
            CRow(w.F("reallife_mgr_row_day", "Day {day}", "day", "150"), "+$1.00", UiRole.OkText)
        };
        return c;
    }

    private static CompanyCard StrikeCardSample(CompanyWords w, string back)
    {
        var c = new CompanyCard
        {
            Kicker = w.T("reallife_co_tab_staff", "Staff"), Title = w.F("reallife_co_strike_title", "Strike at {business}", "business", "Speedy Bites"), Icon = UiIcon.Megaphone,
            Chips = new[] { w.T("reallife_ibox_chip_called", "Called") }, ChipTones = new[] { UiRole.Danger }, Back = CBack(w, "", CompanyView.Staff)
        };
        c.Blocks = new[]
        {
            CNote(w.F("reallife_co_strike_called", "A strike is called from day {from} to day {end}.", "from", "216", "end", "218"), UiRole.DangerText),
            CNote(w.F("reallife_co_strike_effect", "Footfall falls {percent}% and the shop is shut {hours} hours a day.", "percent", "30", "hours", "6")),
            CNote(w.F("reallife_co_strike_why", "{low} of {count} people stand low.", "low", "4", "count", "9")),
            CSec(w.T("reallife_co_strike_low_sec", "Who stands low")),
            new CompanyBlock
            {
                Kind = CompanyBlockKind.Items, Items = new[]
                {
                    new CompanyItem { Title = "Rui Tanaka", Sub = "Customer service", Face = true, Hue = 0.5f, Ring = UiRole.Danger, Figure = "28", Grade = 28, Press = true, Act = CA(CompanyDo.Go, "emp:rui", CompanyView.Staff) },
                    new CompanyItem { Title = "Ines Duarte", Sub = "Cooking", Face = true, Hue = 0.15f, Ring = UiRole.Danger, Figure = "33", Grade = 33, Press = true, Act = CA(CompanyDo.Go, "emp:ines", CompanyView.Staff) },
                    new CompanyItem { Title = "Hana Sato", Sub = "Cleaning", Face = true, Hue = 0.8f, Ring = UiRole.Danger, Figure = "35", Grade = 35, Press = true, Act = CA(CompanyDo.Go, "emp:hana", CompanyView.Staff) },
                    new CompanyItem { Title = "Leo Martin", Sub = "Cooking", Face = true, Hue = 0.3f, Ring = UiRole.Danger, Figure = "38", Grade = 38, Press = true, Act = CA(CompanyDo.Go, "emp:leo", CompanyView.Staff) }
                }
            },
            CSec(w.T("reallife_co_strike_ways", "Ways to settle it")),
            CRow(w.T("reallife_co_strike_do_raise", "Raise everyone to the going rate"), "$3.40 / h"),
            CRow(w.T("reallife_co_strike_do_payment", "A one-off payment to each"), "$540"),
            CNote(w.F("reallife_co_strike_sit", "Or sit it out until day {day}.", "day", "218")),
            CBtns(CB(w.T("reallife_co_strike_do_raise", "Raise everyone to the going rate"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.StrikeAnswer, "Speedy Bites~7", 0), UiIcon.Check),
                CB(w.T("reallife_co_strike_do_payment", "A one-off payment to each"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.StrikeAnswer, "Speedy Bites~7", 1)),
                CB(w.T("reallife_co_strike_do_sit", "Sit it out"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "", CompanyView.Staff), UiIcon.Close))
        };
        return c;
    }

    private static CompanyCard OvertimeCardSample(CompanyWords w, string back)
    {
        var c = new CompanyCard
        {
            Kicker = w.T("reallife_co_tab_staff", "Staff"), Title = w.F("reallife_co_overtime_title", "Overtime at {business}", "business", "Hudson Warehouse"), Icon = UiIcon.Clock, Back = CBack(w, "", CompanyView.Staff)
        };
        c.Blocks = new[]
        {
            CNote(w.F("reallife_co_overtime_by", "Answer by day {day}.", "day", "217"), UiRole.WarnText),
            CNote(w.F("reallife_co_overtime_what", "The crew offers {hours} extra hours a day for {days} days.", "hours", "2", "days", "5")),
            CSec(w.T("reallife_ibox_sec_terms", "Terms")),
            CRow(w.L("reallife_co_overtime_cost", "Cost"), w.F("reallife_co_row_a_day", "{amount} a day", "amount", "$190")),
            CRow(w.L("reallife_co_overtime_gain", "Footfall served"), "+12%"),
            CSec(w.T("reallife_co_staff_people", "Your people")),
            new CompanyBlock
            {
                Kind = CompanyBlockKind.Items, Items = new[]
                {
                    new CompanyItem { Title = "Hugo Brandt", Sub = "Driving", Face = true, Hue = 0.65f, Ring = UiRole.Ok, Figure = w.F("reallife_rota_hours_n", "{hours} h", "hours", "8"), Note = "$15.60 / h", Grade = -1, Press = true, Act = CA(CompanyDo.Go, "emp:hugo", CompanyView.Staff) },
                    new CompanyItem { Title = "Mei Lin", Sub = "Driving", Face = true, Hue = 0.05f, Ring = UiRole.Ok, Figure = w.F("reallife_rota_hours_n", "{hours} h", "hours", "8"), Note = "$15.60 / h", Grade = -1, Press = true, Act = CA(CompanyDo.Go, "emp:mei", CompanyView.Staff) },
                    new CompanyItem { Title = "Carlos Vega", Sub = "Logistics", Face = true, Hue = 0.4f, Ring = UiRole.Ok, Figure = w.F("reallife_rota_hours_n", "{hours} h", "hours", "8"), Note = "$16.80 / h", Grade = -1, Press = true, Act = CA(CompanyDo.Go, "emp:carlos", CompanyView.Staff) },
                    new CompanyItem { Title = "Tomas Reyes", Sub = "Cleaning", Face = true, Hue = 0.8f, Ring = UiRole.Ok, Figure = w.F("reallife_rota_hours_n", "{hours} h", "hours", "8"), Note = "$15.40 / h", Grade = -1, Press = true, Act = CA(CompanyDo.Go, "emp:tomas", CompanyView.Staff) }
                }
            },
            CBtns(CB(w.T("reallife_co_overtime_agree", "Agree"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.OvertimeAnswer, "Hudson Warehouse~4", 1), UiIcon.Check),
                CB(w.T("reallife_co_overtime_refuse", "Refuse"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.OvertimeAnswer, "Hudson Warehouse~4", 0), UiIcon.Close, false, null, true))
        };
        return c;
    }

    // ------------------------------------------------------------------------------------------------- Managers

    private static UiOrgNode ONode(UiOrgKind kind, int level, string name, string sub = null, string right = null, bool kids = false, float hue = 0f, UiRole ring = UiRole.Ok) =>
        new UiOrgNode { Kind = kind, Level = level, Name = name, Sub = sub, Right = right, HasKids = kids, Hue = hue, Ring = ring };

    private static void CompanyManagers(CompanyFacts f, CompanyWords w, string state)
    {
        CompanyManagersFacts m = f.Managers;
        f.Deep = state != "managers_flat" && state != "managers_none";
        m.Deep = f.Deep;
        if (state == "managers_off")
        {
            m.PartOff = true;
            m.OffTitle = w.T("reallife_co_tab_managers", "Managers");
            m.OffBody = w.T("reallife_mgr_off", "Managers are switched off in the options, or they stopped after an error. Nothing is decided by them and everything comes to you.");
            return;
        }
        bool bare = state == "managers_flat" || state == "managers_none";   // a company with a few managers, or none yet, and the businesses that wait for one
        var nodes = new List<UiOrgNode>();
        var ids = new List<string>();
        var where = new List<string>();
        var needs = new List<bool>();
        void Add(string id, UiOrgNode n, string district = null, bool waits = false)
        {
            ids.Add(id);
            nodes.Add(n);
            where.Add(district);
            needs.Add(waits);
        }
        string owner = "Alex Moreno";
        Add("cap:owner", ONode(UiOrgKind.Caption, 0, w.T("reallife_co_tree_owner", "Owner")));
        Add("own", ONode(UiOrgKind.Person, 0, owner, w.F("reallife_co_tree_owner_sub", "Owner, all {count} businesses", "count", bare ? "8" : "4"), null, false, 0.05f));
        if (f.Deep)
        {
            Add("cap:exec", ONode(UiOrgKind.Caption, 0, w.T("reallife_co_tree_executive", "Executive")));
            Add("exe:1", ONode(UiOrgKind.Person, 1, "Ethan Brooks", "Chief operating officer", w.F("reallife_co_tree_managers", "{count} managers", "count", "4"), true, 0.15f));
            Add("mgr:Speedy Bites~7", ONode(UiOrgKind.Person, 2, "Ray Walsh", "Speedy Bites", w.F("reallife_co_tree_staff", "{count} staff", "count", "9"), true, 0.55f), "Midtown", true);
            Add("crew:dev", ONode(UiOrgKind.Person, 3, "Dev Kapoor", null, "Customer service", false, 0.35f, UiRole.None));
            Add("crew:lena", ONode(UiOrgKind.Person, 3, "Lena Adler", null, "Cooking", false, 0.1f, UiRole.None));
            Add("more:Speedy Bites~7", ONode(UiOrgKind.More, 3, w.F("reallife_co_and_more", "and {count} more", "count", "6")));
            Add("mgr:Bloom & Co~214", ONode(UiOrgKind.Person, 2, "Tessa Brandt", "Bloom & Co", w.F("reallife_co_tree_staff", "{count} staff", "count", "5"), true, 0.9f), "Chelsea");
            Add("mgr:Hudson Warehouse~4", ONode(UiOrgKind.Person, 2, "Omar Haddad", "Hudson Warehouse", w.F("reallife_co_tree_staff", "{count} staff", "count", "4"), true, 0.75f, UiRole.Warn), "Harlem");
            Add("reg:Midtown", ONode(UiOrgKind.Person, 2, "Meera Iyer", w.F("reallife_co_tree_region", "Regional, {district}", "district", "Midtown"), null, false, 0.75f), "Midtown");
            Add("exe:2", ONode(UiOrgKind.Person, 1, "Priya Nair", "Chief human resources officer", null, false, 0.4f));
            Add("seat:3", ONode(UiOrgKind.Post, 1, "Chief financial officer", w.T("reallife_mgr_empty_post", "Empty post"), w.T("reallife_co_tree_hire", "Appoint")));
        }
        else if (state == "managers_flat")
        {
            Add("mgr:Speedy Bites~7", ONode(UiOrgKind.Person, 1, "Ray Walsh", "Speedy Bites", w.F("reallife_co_tree_staff", "{count} staff", "count", "9"), true, 0.55f), "Midtown", true);
            Add("crew:dev", ONode(UiOrgKind.Person, 2, "Dev Kapoor", null, "Customer service", false, 0.35f, UiRole.None));
            Add("more:Speedy Bites~7", ONode(UiOrgKind.More, 2, w.F("reallife_co_and_more", "and {count} more", "count", "7")));
            Add("mgr:Bloom & Co~214", ONode(UiOrgKind.Person, 1, "Tessa Brandt", "Bloom & Co", w.F("reallife_co_tree_staff", "{count} staff", "count", "5"), true, 0.9f), "Chelsea");
        }
        Add("cap:posts", ONode(UiOrgKind.Caption, 0, w.T("reallife_co_tree_open_posts", "Open posts")));
        string[] postNames = bare ? new[] { "Pioneer Game Studio", "Corner Cafe", "Riverside Gym", "Hudson Warehouse", "Uptown Books", "Dockside Garage" } : state == "managers_owner" ? new[] { "Pioneer Game Studio", "Corner Cafe", "Riverside Gym" } : new[] { "Pioneer Game Studio" };
        string[] postKinds = { "Office", "Cafe", "Gym", "Warehouse", "Shop", "Garage" };
        string[] postDistricts = { "Midtown", "Chelsea", "Harlem", "Harlem", "Chelsea", "Midtown" };
        for (int k = 0; k < postNames.Length; k++)
        {
            Add("post:" + postNames[k] + "~" + (9 + k), ONode(UiOrgKind.Post, 1, postNames[k], postKinds[k] + ", " + postDistricts[k],
                w.T("reallife_co_tree_hire", "Appoint")), postDistricts[k]);
        }
        m.Tree = nodes.ToArray();
        m.TreeIds = ids.ToArray();
        m.TreeDistricts = where.ToArray();
        m.TreeNeeds = needs.ToArray();
        m.Districts = new[] { "Midtown", "Chelsea", "Harlem" };
        m.TreeTitle = w.T("reallife_co_who_decides", "Who decides what");
        m.TreeFoot = f.Deep ? w.T("reallife_co_open_policy", "Open the policy centre") : null;
        m.Empty = w.T("reallife_co_mgr_none_body", "Pick a person in the tree to see what they decide, what they may spend and how they are doing.");
        string tail = state.Substring(state.IndexOf('_') + 1);
        switch (tail)
        {
            case "owner":
            case "none":
                m.Chosen = "own";
                m.Profile = OwnerProfileSample(w, tail == "none");
                break;
            case "region":
                m.Chosen = "reg:Midtown";
                m.Profile = RegionProfileSample(w);
                break;
            case "post":
                m.Chosen = "post:Pioneer Game Studio~9";
                m.Profile = PostProfileSample(w);
                break;
            default:
                m.Chosen = "mgr:Speedy Bites~7";
                m.Profile = ManagerProfileSample(w, bare);
                break;
        }
        if (tail.StartsWith("sub_", System.StringComparison.Ordinal))
        {
            ManagerSubSample(f, m, w, tail.Substring(4));
        }
    }

    private static CompanyProfile ManagerProfileSample(CompanyWords w, bool noExecutive = false)
    {
        string place = "Speedy Bites~7";
        var p = new CompanyProfile
        {
            Id = "mgr:" + place, Name = "Ray Walsh", Role = w.F("reallife_co_role_manager", "Manager, {place}", "place", "Speedy Bites"), Hue = 0.55f, Ring = UiRole.Ok, Status = w.T("reallife_mgr_in_post", "In post"), StatusTone = UiRole.Ok,
            FactLabels = new[] { w.T("reallife_co_f_manager_since", "Manager since"), w.T("reallife_co_f_wage", "Wage"), w.T("reallife_co_f_this_month", "This month"), w.T("reallife_co_f_asked", "Asked you") },
            FactValues = new[] { "Day 41", "$23.50 / h", w.F("reallife_co_f_decisions", "{count} decisions", "count", "86"), "1" },
            WaitingText = w.T("reallife_co_one_waiting", "1 question waiting for you"), WaitingLine = w.F("reallife_hier_say_exception", "{name} asks for {asked} an hour; the limit is {ceiling}.", "name", "Dev Kapoor", "asked", "$19.00", "ceiling", "$18.00"),
            Waiting = CB(w.T("reallife_co_answer", "Answer"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.Go, "card", 1), UiIcon.Next),
            TasksTitle = w.F("reallife_co_decisions_owns", "Decisions {name} owns", "name", "Ray"), TasksHint = w.F("reallife_co_switch_hint", "Switch on = {name} decides", "name", "Ray")
        };
        string[] titles = { "Raises", "Rival offers", "Overtime", "Insurance claims", "Stock", "Prices", "Rota", "Hiring", "Marketing", "Leases" };
        string[] lines = { "Up to 110% of the going rate", "Up to 110% of the going rate", "A $400 budget a month", "Files within the window", "Keeps 3 days of stock", "Between 90% and 115%", "Written Sundays at 18:00", "Keeps the place at 9", "A $500 limit a month", "Up to a 5% rise" };
        bool[] on = { true, true, true, false, true, true, true, true, false, true };
        UiIcon[] icons = { UiIcon.Coin, UiIcon.Handshake, UiIcon.Clock, UiIcon.ShieldCheck, UiIcon.Package, UiIcon.Tag, UiIcon.Calendar, UiIcon.People, UiIcon.Megaphone, UiIcon.Key };
        var tasks = new List<CompanyTask>();
        for (int t = 0; t < titles.Length; t++)
        {
            tasks.Add(new CompanyTask
            {
                Id = place + ":" + t, Icon = icons[t], Title = titles[t], Line = lines[t], On = on[t], Who = on[t] ? "Ray" : w.T("reallife_hier_you", "You"),
                Act = CA(CompanyDo.TaskSet, place, t, on[t] ? 0 : 1)
            });
        }
        p.Tasks = tasks.ToArray();
        p.LimitsTitle = w.T("reallife_co_limits_title", "Limits and where questions go");
        p.LimitsAll = CB(w.T("reallife_co_all_limits", "All limits"), UiButtonKind.Quiet, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubTask, 0));
        p.Limits = new[]
        {
            new CompanyLimit { Title = w.T("reallife_co_lim_raise", "Raise limit"), Sub = w.T("reallife_co_lim_raise_sub", "Of the going rate, above the pay now"), Value = "110%", CanLess = true, CanMore = true,
                Less = CA(CompanyDo.LimitSet, place, 0, 1), More = CA(CompanyDo.LimitSet, place, 0, 3), Typed = CA(CompanyDo.LimitSet, place, 0, 2) },
            new CompanyLimit { Title = w.T("reallife_co_lim_budget", "Spend limit"), Sub = w.T("reallife_co_lim_budget_sub", "A month, for overtime and hires"), Value = "$400", CanLess = true, CanMore = true,
                Less = CA(CompanyDo.LimitSet, place, 4, 1), More = CA(CompanyDo.LimitSet, place, 4, 3), Typed = CA(CompanyDo.LimitSet, place, 4, 2) }
        };
        p.RouteTitle = w.F("reallife_co_route_title", "Questions {name} cannot decide go to", "name", "Ray");
        p.RouteSub = noExecutive ? null : w.F("reallife_co_route_sub", "{name} picks up what you leave for a day", "name", "Ethan Brooks");
        p.RouteOptions = noExecutive ? new[] { w.T("reallife_hier_you", "You") } : new[] { w.T("reallife_hier_you", "You"), "Ethan Brooks" };
        p.RouteChosen = noExecutive ? 0 : 1;
        p.RouteActs = noExecutive ? new[] { CA(CompanyDo.TaskSet, place, -1, 0) } : new[] { CA(CompanyDo.TaskSet, place, -1, 0), CA(CompanyDo.TaskSet, place, -1, 1) };
        p.GradesTitle = w.F("reallife_co_how_doing", "How {name} is doing", "name", "Ray");
        p.GradeLabels = new[] { w.T("reallife_co_g_judgement", "Judgement"), w.T("reallife_co_g_speed", "Speed"), w.T("reallife_co_g_thrift", "Thrift"), w.T("reallife_co_g_morale", "Crew morale") };
        p.GradeValues = new[] { 92, 74, 61, 72 };
        p.LogTitle = w.T("reallife_co_log", "Log");
        p.LogAll = w.F("reallife_co_all_count", "All {count}", "count", "86");
        p.LogOpen = CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubDecision);
        p.Log = new[]
        {
            new CompanyDone { When = w.F("reallife_mgr_row_day", "Day {day}", "day", "214"), Text = w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "6", "day", "3"), Money = "-" + CM(410), Tone = -1 },
            new CompanyDone { When = w.F("reallife_mgr_row_day", "Day {day}", "day", "214"), Text = w.F("reallife_mgr_did_raise_agree", "Agreed a raise of {raise} for {name}", "name", "Lena Adler", "raise", "$0.50"), Money = "-" + CM(390), Tone = -1 },
            new CompanyDone { When = w.F("reallife_mgr_row_day", "Day {day}", "day", "213"), Text = w.F("reallife_mgr_did_prices", "Changed {count} prices", "count", "4"), Money = "+" + CM(85), Tone = 1 },
            new CompanyDone { When = w.F("reallife_mgr_row_day", "Day {day}", "day", "212"), Text = w.F("reallife_mgr_did_rota", "Wrote the rota: {shifts} shifts, {covered} of {wanted} hours covered", "shifts", "24", "covered", "112", "wanted", "112"), Money = "no cost" }
        };
        p.CrewTitle = w.F("reallife_co_crew_title", "{name}'s crew", "name", "Ray");
        p.CrewCount = w.F("reallife_co_staff_count_n", "{count} staff", "count", "9");
        p.CrewTotal = 9;
        p.Crew = new[]
        {
            new CompanyItem { Title = "Dev Kapoor", Sub = "Customer service", Face = true, Hue = 0.35f, Ring = UiRole.Warn, Grade = 52, Figure = "52", Press = true, Act = CA(CompanyDo.Go, "emp:dev", CompanyView.Staff) },
            new CompanyItem { Title = "Lena Adler", Sub = "Cooking", Face = true, Hue = 0.1f, Ring = UiRole.Ok, Grade = 64, Figure = "64", Press = true, Act = CA(CompanyDo.Go, "emp:lena", CompanyView.Staff) },
            new CompanyItem { Title = "Maya Okafor", Sub = "Customer service", Face = true, Hue = 0.6f, Ring = UiRole.Ok, Grade = 78, Figure = "78", Press = true, Act = CA(CompanyDo.Go, "emp:maya", CompanyView.Staff) }
        };
        p.Buttons = new[]
        {
            CB(w.T("reallife_mgr_replace", "Replace or stand down"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubAppoint), UiIcon.People),
            CB(w.T("reallife_hier_open_dashboard", "Open the dashboard"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubDashboard), UiIcon.ChartPie)
        };
        return p;
    }

    private static CompanyProfile OwnerProfileSample(CompanyWords w, bool nobody = false)
    {
        var p = new CompanyProfile
        {
            Id = "own", Name = "Alex Moreno", Role = w.T("reallife_co_role_owner", "Owner"), Hue = 0.05f, Ring = UiRole.Ok,
            FactLabels = nobody ? new[] { w.T("reallife_co_f_businesses", "Businesses") } : new[] { w.T("reallife_co_f_businesses", "Businesses"), w.T("reallife_co_f_managers", "Managers"), w.T("reallife_co_f_asked", "Asked you") },
            FactValues = nobody ? new[] { "8" } : new[] { "4", "6", "2" }
        };
        var more = new List<CompanyBlock>();
        if (!nobody)
        {
            more.Add(CSec(w.T("reallife_co_owner_waiting", "Waiting for you"), "2"));
            more.Add(new CompanyBlock
            {
                Kind = CompanyBlockKind.Items, Items = new[]
                {
                    new CompanyItem { Grade = -1, Title = w.T("reallife_co_q_raise_title", "A raise above my limit"), FaceName = "Ray Walsh", Sub = "Speedy Bites", Face = true, Hue = 0.55f, Ring = UiRole.Warn,
                        Figure = w.F("reallife_mgr_row_day", "Day {day}", "day", "214"), Press = true, Act = CA(CompanyDo.Go, "card", 1) },
                    new CompanyItem { Grade = -1, Title = w.T("reallife_co_q_lease_title", "A lease is up for renewal"), FaceName = "Omar Haddad", Sub = "Hudson Warehouse", Face = true, Hue = 0.75f, Ring = UiRole.Warn,
                        Figure = w.F("reallife_mgr_row_day", "Day {day}", "day", "213"), Press = true, Act = CA(CompanyDo.Go, "card", 2) }
                }
            });
            more.Add(CSec(w.T("reallife_mgr_sec_directory", "Your managers"), "6"));
            var rows = new List<CompanyItem>();
            string[] names = { "Ray Walsh", "Tessa Brandt", "Omar Haddad", "Meera Iyer", "Priya Nair", "Ethan Brooks" };
            string[] subs = { "Speedy Bites", "Bloom & Co", "Hudson Warehouse", "Regional, Midtown", "Chief human resources officer", "Chief operating officer" };
            float[] hues = { 0.55f, 0.9f, 0.75f, 0.75f, 0.4f, 0.15f };
            int[] standing = { 72, 81, 64, 68, 77, 58 };
            for (int i = 0; i < 6; i++)
            {
                rows.Add(new CompanyItem { Title = names[i], Sub = subs[i], Face = true, Hue = hues[i], Ring = i == 2 ? UiRole.Warn : UiRole.Ok, Grade = standing[i], Figure = standing[i].ToString(), Note = w.T("reallife_co_crew_standing", "crew standing"), Press = true,
                    Act = CA(CompanyDo.Go, i == 3 ? "reg:Midtown" : "mgr:Speedy Bites~7", CompanyView.Managers) });
            }
            more.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = rows.ToArray() });
            more.Add(new CompanyBlock { Kind = CompanyBlockKind.Break });
            more.Add(CSec(w.T("reallife_co_owner_month", "This month")));
            more.Add(CRow(w.T("reallife_co_owner_decided", "Decisions made for you"), "212"));
            more.Add(CRow(w.T("reallife_co_owner_cost", "Managers' allowances, a month"), CM(4210)));
        }
        else
        {
            more.Add(CNote(w.T("reallife_co_no_managers", "No manager yet. A manager decides what you hand over, and asks you what is past their limit.")));
            more.Add(CSec(w.T("reallife_co_hand_over", "What you can hand over"), "10"));
            more.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = HandOverRows(w) });
            more.Add(new CompanyBlock { Kind = CompanyBlockKind.Break });
        }
        more.Add(CSec(w.T("reallife_co_owner_levels", "Levels")));
        more.Add(new CompanyBlock { Kind = CompanyBlockKind.Switch, Text = w.T("reallife_co_deep_mode", "Deep Mode"), Sub = w.T("reallife_co_deep_sub", "The executives, regions, the policy centre and every question's inbox"),
            On = !nobody, Value = w.T(nobody ? "reallife_co_off" : "reallife_co_on", nobody ? "Off" : "On"), Act = CA(CompanyDo.DeepMode, null, nobody ? 1 : 0) });
        if (!nobody)
        {
            more.Add(new CompanyBlock
            {
                Kind = CompanyBlockKind.Items, Items = new[]
                {
                    new CompanyItem { Grade = -1, Title = w.T("reallife_co_inbox", "Questions inbox"), Sub = w.T("reallife_co_level_inbox", "Every question your managers pass up"), Icon = UiIcon.Envelope,
                        Figure = w.F("reallife_co_inbox_open", "{count} open", "count", "5"), Press = true, Act = CA(CompanyDo.Go, null, CompanyView.Managers, CompanyView.SubInbox) },
                    new CompanyItem { Grade = -1, Title = w.T("reallife_co_policy_centre", "Policy centre"), Sub = w.T("reallife_co_level_policy", "Rules for the company, a region or a place"), Icon = UiIcon.Sliders,
                        Press = true, Act = CA(CompanyDo.Go, null, CompanyView.Managers, CompanyView.SubPolicy) },
                    new CompanyItem { Grade = -1, Title = w.T("reallife_co_templates", "Templates"), Sub = w.T("reallife_co_level_templates", "Sets of rules to put on a scope in one step"), Icon = UiIcon.ClipboardText,
                        Figure = "3", Press = true, Act = CA(CompanyDo.Go, null, CompanyView.Managers, CompanyView.SubTemplates) }
                }
            });
        }
        p.More = more.ToArray();
        return p;
    }

    // The ten decisions a manager can own, as rows with their glyph: what a post hands over.
    private static CompanyItem[] HandOverRows(CompanyWords w)
    {
        string[] keys = { "raises", "offers", "overtime", "claims", "stock", "prices", "rota", "hiring", "marketing", "leases" };
        string[] titles = { "Raises", "Rival offers", "Overtime", "Insurance claims", "Stock", "Prices", "Rota", "Hiring", "Marketing", "Leases" };
        string[] lines = { "Up to 110% of the going rate", "Up to 110% of the going rate", "A $400 budget a month", "Files within the window", "Keeps 3 days of stock", "Between 90% and 115%",
            "Written Sundays at 18:00", "Keeps the place at 9", "A $500 limit a month", "Up to a 5% rise" };
        UiIcon[] glyphs = { UiIcon.Coin, UiIcon.Handshake, UiIcon.Clock, UiIcon.ShieldCheck, UiIcon.Package, UiIcon.Tag, UiIcon.Calendar, UiIcon.People, UiIcon.Megaphone, UiIcon.Key };
        var rows = new CompanyItem[keys.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = new CompanyItem { Grade = -1, Title = w.T("reallife_mgr_task_" + keys[i], titles[i]), Sub = lines[i], Icon = glyphs[i] };
        }
        return rows;
    }

    // The businesses that have no manager yet, as rows that open the post.
    private static CompanyItem[] OpenPostRows(int count)
    {
        string[] names = { "Pioneer Game Studio", "Corner Cafe", "Riverside Gym", "Hudson Warehouse", "Uptown Books", "Dockside Garage" };
        string[] kinds = { "Office, Midtown", "Cafe, Chelsea", "Gym, Harlem", "Warehouse, Harlem", "Shop, Chelsea", "Garage, Midtown" };
        var rows = new CompanyItem[count];
        for (int i = 0; i < count; i++)
        {
            rows[i] = new CompanyItem { Grade = -1, Title = names[i], Sub = kinds[i], Icon = UiIcon.Store, Press = true, Act = CA(CompanyDo.Go, "post:" + names[i] + "~" + (9 + i), CompanyView.Managers) };
        }
        return rows;
    }

    private static CompanyProfile RegionProfileSample(CompanyWords w)
    {
        var p = new CompanyProfile
        {
            Id = "reg:Midtown", Name = "Meera Iyer", Role = w.F("reallife_co_tree_region", "Regional, {district}", "district", "Midtown"), Hue = 0.75f, Ring = UiRole.Ok, Status = w.T("reallife_mgr_in_post", "In post"), StatusTone = UiRole.Ok,
            FactLabels = new[] { w.T("reallife_co_f_since", "Since"), w.T("reallife_co_f_places", "Places"), w.T("reallife_co_f_standing_in", "Standing in"), w.T("reallife_co_f_decisions_n", "Decisions") }, FactValues = new[] { "Day 88", "3", "68", "41" }
        };
        p.GradesTitle = w.F("reallife_co_how_doing", "How {name} is doing", "name", "Meera");
        p.GradeLabels = new[] { w.T("reallife_co_g_judgement", "Judgement"), w.T("reallife_co_g_speed", "Speed"), w.T("reallife_co_g_morale", "Crew morale"), w.T("reallife_hier_fig_score", "Score") };
        p.GradeValues = new[] { 88, 71, 68, 77 };
        p.More = new[]
        {
            CSec(w.T("reallife_mgr_region_their_places", "Their places"), "3"),
            new CompanyBlock
            {
                Kind = CompanyBlockKind.Items, Items = new[]
                {
                    new CompanyItem { Title = "Ray Walsh", Sub = "Speedy Bites", Face = true, Hue = 0.55f, Ring = UiRole.Ok, Grade = 72, Figure = "72", Press = true, Act = CA(CompanyDo.Go, "mgr:Speedy Bites~7", CompanyView.Managers) },
                    new CompanyItem { Title = "Pioneer Game Studio", Sub = w.T("reallife_mgr_empty_post", "Empty post"), Icon = UiIcon.Store, Grade = 64, Figure = "64", Press = true, Act = CA(CompanyDo.Go, "post:Pioneer Game Studio~9", CompanyView.Managers) },
                    new CompanyItem { Title = "Noor Haddad", Sub = "Midtown Deli", Face = true, Hue = 0.2f, Ring = UiRole.Ok, Grade = 69, Figure = "69", Press = true, Act = CA(CompanyDo.Go, "mgr:Speedy Bites~7", CompanyView.Managers) },
                }
            },
            new CompanyBlock { Kind = CompanyBlockKind.Break },
            CSec(w.T("reallife_mgr_sec_manager", "Manager")),
            CRow(w.T("reallife_mgr_skill", "Skill"), "81 / 64"),
            CRow(w.T("reallife_mgr_allowance", "Allowance"), w.F("reallife_co_row_a_day", "{amount} a day", "amount", "$84")),
            CRow(w.T("reallife_mgr_pay", "Pay"), "$31.00 / h"),
            CRow(w.T("reallife_mgr_mistakes", "Mistakes this month"), "2"),
            CSec(w.T("reallife_co_log", "Log"), "41"),
            new CompanyBlock
            {
                Kind = CompanyBlockKind.Lines, Rows = new[]
                {
                    new CompanyDone { When = w.F("reallife_mgr_row_day", "Day {day}", "day", "214"), Text = w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "6", "day", "3"), Money = "-" + CM(410), Tone = -1 },
                    new CompanyDone { When = w.F("reallife_mgr_row_day", "Day {day}", "day", "213"), Text = w.F("reallife_mgr_did_prices", "Changed {count} prices", "count", "4"), Money = "+" + CM(85), Tone = 1 },
                    new CompanyDone { When = w.F("reallife_mgr_row_day", "Day {day}", "day", "211"), Text = w.F("reallife_mgr_did_rota", "Wrote the rota: {shifts} shifts, {covered} of {wanted} hours covered", "shifts", "24", "covered", "112", "wanted", "112") },
                    new CompanyDone { When = w.F("reallife_mgr_row_day", "Day {day}", "day", "210"), Text = w.F("reallife_mgr_did_prices", "Changed {count} prices", "count", "2"), Money = "+" + CM(40), Tone = 1 },
                    new CompanyDone { When = w.F("reallife_mgr_row_day", "Day {day}", "day", "208"), Text = w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "3", "day", "3"), Money = "-" + CM(220), Tone = -1 }
                }
            },
            CNote(w.T("reallife_mgr_region_what", "A regional manager looks over the places of a district and stands in for a manager who is off sick."))
        };
        p.Buttons = new[]
        {
            CB(w.T("reallife_mgr_replace", "Replace or stand down"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "reg:Midtown", CompanyView.Managers, CompanyView.SubAppoint), UiIcon.People),
            CB(w.T("reallife_hier_open_dashboard", "Open the dashboard"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "reg:Midtown", CompanyView.Managers, CompanyView.SubRegion), UiIcon.ChartPie)
        };
        return p;
    }

    private static CompanyProfile PostProfileSample(CompanyWords w)
    {
        var p = new CompanyProfile
        {
            Id = "post:Pioneer Game Studio~9", Name = w.F("reallife_co_tree_post", "Manager of {place}", "place", "Pioneer Game Studio"), Role = "Office, Midtown", Glyph = UiIcon.IdBadge, GlyphTint = UiRole.Muted,
            Status = w.T("reallife_mgr_empty_post", "Empty post"), StatusTone = UiRole.Muted,
            FactLabels = new[] { w.T("reallife_co_f_staff", "Staff"), w.T("reallife_co_f_best_skill", "Best skill"), w.T("reallife_co_f_wage_ask", "Wages from") }, FactValues = new[] { "6", "78", "$16.50 / h" }
        };
        p.More = new[]
        {
            CSec(w.T("reallife_co_candidates", "Who could do it"), "4"),
            new CompanyBlock
            {
                Kind = CompanyBlockKind.Items, Items = new[]
                {
                    new CompanyItem { Title = "Ines Park", Sub = "Customer service", Face = true, Hue = 0.3f, Ring = UiRole.None, Grade = 78, Figure = "$23.50", Note = w.F("reallife_co_skill_n", "skill {n}", "n", "78"), Press = true, Act = CA(CompanyDo.Go, "post:Pioneer Game Studio~9", CompanyView.Managers, CompanyView.SubAppoint, 0, "mc:ray") },
                    new CompanyItem { Title = "Maya Okafor", Sub = "Customer service", Face = true, Hue = 0.6f, Ring = UiRole.None, Grade = 71, Figure = "$18.20", Note = w.F("reallife_co_skill_n", "skill {n}", "n", "71"), Press = true, Act = CA(CompanyDo.Go, "post:Pioneer Game Studio~9", CompanyView.Managers, CompanyView.SubAppoint, 0, "mc:maya") },
                    new CompanyItem { Title = "Lena Adler", Sub = "Cooking", Face = true, Hue = 0.1f, Ring = UiRole.None, Grade = 64, Figure = "$16.50", Note = w.F("reallife_co_skill_n", "skill {n}", "n", "64"), Press = true, Act = CA(CompanyDo.Go, "post:Pioneer Game Studio~9", CompanyView.Managers, CompanyView.SubAppoint, 0, "mc:lena") },
                    new CompanyItem { Title = "Dev Kapoor", Sub = "Customer service", Face = true, Hue = 0.35f, Ring = UiRole.None, Grade = 59, Figure = "$17.00", Note = w.F("reallife_co_skill_n", "skill {n}", "n", "59"), Press = true, Act = CA(CompanyDo.Go, "post:Pioneer Game Studio~9", CompanyView.Managers, CompanyView.SubAppoint, 0, "mc:dev") }
                }
            },
            new CompanyBlock { Kind = CompanyBlockKind.Break },
            CSec(w.T("reallife_mgr_sec_post", "The post")),
            CNote(w.T("reallife_mgr_place_none", "Nobody manages this place yet. A manager decides what you hand over and asks you what is past their limit.")),
            CRow(w.T("reallife_mgr_row_place", "Place"), "Pioneer Game Studio"),
            CRow(w.T("reallife_mgr_row_district", "District"), "Midtown"),
            CSec(w.T("reallife_mgr_sec_would", "What a manager here decides"), "6"),
            new CompanyBlock
            {
                Kind = CompanyBlockKind.Items, Items = new[]
                {
                    new CompanyItem { Grade = -1, Title = w.T("reallife_mgr_task_raises", "Raises"), Sub = "Up to 110% of the going rate", Icon = UiIcon.Coin },
                    new CompanyItem { Grade = -1, Title = w.T("reallife_mgr_task_overtime", "Overtime"), Sub = "A $400 budget a month", Icon = UiIcon.Clock },
                    new CompanyItem { Grade = -1, Title = "Stock", Sub = "Keeps 3 days of stock", Icon = UiIcon.Package },
                    new CompanyItem { Grade = -1, Title = w.T("reallife_mgr_task_prices", "Prices"), Sub = "Between 90% and 115%", Icon = UiIcon.Tag },
                    new CompanyItem { Grade = -1, Title = w.T("reallife_co_tab_rota", "Rota"), Sub = "Written Sundays at 18:00", Icon = UiIcon.Calendar },
                    new CompanyItem { Grade = -1, Title = "Hiring", Sub = "Keeps the place at 9", Icon = UiIcon.People }
                }
            }
        };
        p.Buttons = new[] { CB(w.T("reallife_mgr_appoint", "Appoint"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.Go, "post:Pioneer Game Studio~9", CompanyView.Managers, CompanyView.SubAppoint), UiIcon.People) };
        return p;
    }

    // The sub pages of Managers: a list and a card, each from the subject the tree had picked.
    private static void ManagerSubSample(CompanyFacts f, CompanyManagersFacts m, CompanyWords w, string which)
    {
        var page = new CompanyPage();
        string place = "Speedy Bites~7";
        switch (which)
        {
            case "appoint":
            {
                page.List = new CompanyList
                {
                    Title = w.T("reallife_co_candidates", "Who could do it"), Count = "4", Chosen = 1,
                    Rows = new[]
                    {
                        new CompanyRow { Id = "mc:ray", Title = "Ray Walsh", Sub = "Customer service", Face = true, Hue = 0.55f, Ring = UiRole.None, Figure = "$23.50", Note = w.F("reallife_co_skill_n", "skill {n}", "n", "78"), Chip = w.T("reallife_mgr_in_post", "In post"), ChipTone = UiRole.Ok },
                        new CompanyRow { Id = "mc:lena", Title = "Lena Adler", Sub = "Cooking", Face = true, Hue = 0.1f, Ring = UiRole.None, Figure = "$16.50", Note = w.F("reallife_co_skill_n", "skill {n}", "n", "64") },
                        new CompanyRow { Id = "mc:maya", Title = "Maya Okafor", Sub = "Customer service", Face = true, Hue = 0.6f, Ring = UiRole.None, Figure = "$18.20", Note = w.F("reallife_co_skill_n", "skill {n}", "n", "71") },
                        new CompanyRow { Id = "mc:dev", Title = "Dev Kapoor", Sub = "Customer service", Face = true, Hue = 0.35f, Ring = UiRole.None, Figure = "$17.00", Note = w.F("reallife_co_skill_n", "skill {n}", "n", "59") }
                    }
                };
                var c = new CompanyCard
                {
                    Kicker = "Speedy Bites", Title = "Lena Adler", Sub = w.F("reallife_co_emp_works", "{business}, {skill}", "business", "Speedy Bites", "skill", "Cooking"), Face = true, Hue = 0.1f, Ring = UiRole.None,
                    Back = CBack(w, "mgr:" + place, CompanyView.Managers),
                    FactLabels = new[] { w.T("reallife_mgr_skill_label", "Skill"), w.T("reallife_co_f_paid", "Paid"), w.T("reallife_mgr_allowance_label", "Allowance") },
                    FactValues = new[] { "64", "$16.50", w.F("reallife_co_row_a_day", "{amount} a day", "amount", "$52") }
                };
                c.Blocks = new[]
                {
                    CRow(w.T("reallife_mgr_mistake_chance", "Chance of a mistake"), "7.2%"),
                    new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_mgr_mistake_chance", "Chance of a mistake"), Value = "7.2%", Number = 14, Tone = UiRole.Warn },
                    CSec(w.T("reallife_mgr_sec_would_own", "What this person would own"), "10"),
                    new CompanyBlock { Kind = CompanyBlockKind.Items, Items = HandOverRows(w) },
                    CNote(w.F("reallife_mgr_appoint_replaces", "This replaces {name}.", "name", "Ray Walsh")),
                    CBtns(CB(w.T("reallife_mgr_appoint", "Appoint"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.Appoint, "lena", 0, 0, 0, place), UiIcon.Check),
                        CB(w.T("reallife_mgr_stand_down", "Stand down"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.StandDown, place, 0), UiIcon.Close, false, null, true),
                        CB(w.T("reallife_mgr_hand_back", "Hand back"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.HandBack, place), UiIcon.Back))
                };
                page.Card = c;
                break;
            }
            case "decisions":
            {
                page.List = new CompanyList
                {
                    Title = w.F("reallife_co_decisions_of", "{name}'s decisions", "name", "Ray"), Count = "5", Chosen = 0,
                    Rows = new[]
                    {
                        new CompanyRow { Id = "md:1", Title = "Stock", Sub = w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "6", "day", "3"), Icon = UiIcon.Package, Figure = w.F("reallife_mgr_row_day", "Day {day}", "day", "214"), Note = "-$410", Chip = w.T("reallife_mgr_state_done", "Done"), ChipTone = UiRole.Ok },
                        new CompanyRow { Id = "md:2", Title = w.T("reallife_mgr_task_raises", "Raises"), Sub = w.F("reallife_mgr_did_raise_agree", "Agreed a raise of {raise} for {name}", "name", "Lena Adler", "raise", "$0.50"), Icon = UiIcon.Coin, Figure = w.F("reallife_mgr_row_day", "Day {day}", "day", "214"), Note = "-$390", Chip = w.T("reallife_mgr_state_done", "Done"), ChipTone = UiRole.Ok },
                        new CompanyRow { Id = "md:3", Title = w.T("reallife_mgr_task_prices", "Prices"), Sub = w.F("reallife_mgr_did_prices", "Changed {count} prices", "count", "4"), Icon = UiIcon.Tag, Figure = w.F("reallife_mgr_row_day", "Day {day}", "day", "213"), Note = "+$85", Chip = w.T("reallife_mgr_state_done", "Done"), ChipTone = UiRole.Ok },
                        new CompanyRow { Id = "md:4", Title = w.T("reallife_co_tab_rota", "Rota"), Sub = w.F("reallife_mgr_did_rota", "Wrote the rota: {shifts} shifts, {covered} of {wanted} hours covered", "shifts", "24", "covered", "112", "wanted", "112"), Icon = UiIcon.Calendar, Figure = w.F("reallife_mgr_row_day", "Day {day}", "day", "212"), Note = "Ray Walsh", Chip = w.T("reallife_mgr_state_done", "Done"), ChipTone = UiRole.Muted },
                        new CompanyRow { Id = "md:5", Title = "Stock", Sub = w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "4", "day", "3"), Icon = UiIcon.Package, Figure = w.F("reallife_mgr_row_day", "Day {day}", "day", "211"), Note = "-$300", Chip = w.T("reallife_mgr_state_undone", "Undone"), ChipTone = UiRole.Muted }
                    }
                };
                var c = new CompanyCard
                {
                    Kicker = "Speedy Bites", Title = "Stock", Icon = UiIcon.Package, Sub = w.F("reallife_mgr_dec_when", "{place}, day {day} at {hour}:00, by {name}", "place", "Speedy Bites", "day", "214", "hour", "08", "name", "Ray Walsh"),
                    Chips = new[] { w.T("reallife_mgr_state_done", "Done") }, ChipTones = new[] { UiRole.Ok }, Back = CBack(w, "mgr:" + place, CompanyView.Managers)
                };
                c.Blocks = new[]
                {
                    CNote(w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "6", "day", "3")),
                    CRow("Buns", w.F("reallife_mgr_dec_from_to", "{before} to {after}", "before", "120", "after", "240")),
                    CRow("Patties", w.F("reallife_mgr_dec_from_to", "{before} to {after}", "before", "80", "after", "160")),
                    CRow("Fries", w.F("reallife_mgr_dec_from_to", "{before} to {after}", "before", "60", "after", "120")),
                    CRow("Cola", w.F("reallife_mgr_dec_from_to", "{before} to {after}", "before", "48", "after", "96")),
                    CRow("Salad", w.F("reallife_mgr_dec_from_to", "{before} to {after}", "before", "20", "after", "40")),
                    CRow("Napkins", w.F("reallife_mgr_dec_from_to", "{before} to {after}", "before", "300", "after", "600")),
                    CSec(w.T("reallife_mgr_sec_before", "Before it")),
                    new CompanyBlock { Kind = CompanyBlockKind.Lines, Rows = new[]
                    {
                        new CompanyDone { When = w.F("reallife_co_day_hour", "Day {day}, {hour}", "day", "211", "hour", "08:00"), Text = "Ray Walsh: " + w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "4", "day", "3"), Money = "-$300", Tone = -1 },
                        new CompanyDone { When = w.F("reallife_co_day_hour", "Day {day}, {hour}", "day", "208", "hour", "08:00"), Text = "Ray Walsh: " + w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "5", "day", "3"), Money = "-$355", Tone = -1 }
                    } },
                    CNote(w.T("reallife_mgr_dec_undo_what", "Undoing cancels the order if it has not been delivered.")),
                    CBtns(CB(w.T("reallife_mgr_undo", "Undo"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Undo, null, 1), UiIcon.Back, false, null, true))
                };
                page.Card = c;
                break;
            }
            case "task":
            {
                string[] titles = { "Raises", "Rival offers", "Overtime", "Insurance claims", "Stock", "Prices", "Rota", "Hiring", "Marketing", "Leases" };
                string[] lines = { "Up to 110% of the going rate", "Up to 110% of the going rate", "A $400 budget a month", "Files within the window", "Keeps 3 days of stock", "Between 90% and 115%", "Written Sundays at 18:00", "Keeps the place at 9", "A $500 limit a month", "Up to a 5% rise" };
                bool[] owns = { true, true, true, false, true, true, true, true, false, true };
                UiIcon[] glyphs = { UiIcon.Coin, UiIcon.Handshake, UiIcon.Clock, UiIcon.ShieldCheck, UiIcon.Package, UiIcon.Tag, UiIcon.Calendar, UiIcon.People, UiIcon.Megaphone, UiIcon.Key };
                var list = new List<CompanyRow>();
                for (int k = 0; k < titles.Length; k++)
                {
                    list.Add(new CompanyRow { Id = "mk:" + k, Title = titles[k], Sub = lines[k], Icon = glyphs[k], Chip = owns[k] ? "Ray" : w.T("reallife_hier_you", "You"), ChipTone = owns[k] ? UiRole.Ok : UiRole.Muted });
                }
                page.List = new CompanyList { Title = w.F("reallife_co_decisions_owns", "Decisions {name} owns", "name", "Ray"), Count = "10", Rows = list.ToArray(), Chosen = 0 };
                var c = new CompanyCard { Kicker = "Speedy Bites", Title = w.T("reallife_mgr_task_raises", "Raises"), Icon = UiIcon.Coin, Back = CBack(w, "mgr:" + place, CompanyView.Managers) };
                c.Blocks = new[]
                {
                    CNote("The manager answers a request for a raise inside the limit you set, and asks you about anything above it."),
                    new CompanyBlock { Kind = CompanyBlockKind.Switch, Text = w.T("reallife_mgr_task_given", "Ray decides this"), Sub = "Up to 110% of the going rate", On = true, Value = "Ray", Act = CA(CompanyDo.TaskSet, place, 0, 0) },
                    new CompanyBlock { Kind = CompanyBlockKind.Stepper, Text = w.T("reallife_co_lim_raise", "Raise limit"), Sub = w.T("reallife_co_lim_raise_sub", "Of the going rate, above the pay now"), Value = "110%", CanLess = true, CanMore = true,
                        Act = CA(CompanyDo.LimitSet, place, 0, 1), Act2 = CA(CompanyDo.LimitSet, place, 0, 3), Act3 = CA(CompanyDo.LimitSet, place, 0, 2) }
                };
                c.Blocks = c.Blocks.Concat(new[]
                {
                    CSec(w.T("reallife_co_log", "Log"), "3"),
                    new CompanyBlock { Kind = CompanyBlockKind.Lines, Rows = new[]
                    {
                        new CompanyDone { When = w.F("reallife_co_day_hour", "Day {day}, {hour}", "day", "214", "hour", "08:00"), Text = "Ray Walsh: " + w.F("reallife_mgr_did_raise_agree", "Agreed a raise of {raise} for {name}", "name", "Lena Adler", "raise", "$0.50"), Money = "-$390", Tone = -1 },
                        new CompanyDone { When = w.F("reallife_co_day_hour", "Day {day}, {hour}", "day", "188", "hour", "08:00"), Text = "Ray Walsh: " + w.F("reallife_mgr_did_raise_agree", "Agreed a raise of {raise} for {name}", "name", "Dev Kapoor", "raise", "$0.50"), Money = "-$390", Tone = -1 },
                        new CompanyDone { When = w.F("reallife_co_day_hour", "Day {day}, {hour}", "day", "131", "hour", "08:00"), Text = "Ray Walsh: " + w.F("reallife_mgr_did_raise_agree", "Agreed a raise of {raise} for {name}", "name", "Maya Okafor", "raise", "$1.00"), Money = "-$780", Tone = -1 }
                    } }
                }).ToArray();
                page.Card = c;
                break;
            }
            case "inbox":
            {
                string open = w.T("reallife_hier_state_open", "Open");
                page.List = new CompanyList
                {
                    Title = w.T("reallife_co_inbox", "Questions inbox"), Count = "3", Chosen = 0,
                    Rows = new[]
                    {
                        new CompanyRow { Id = "he:1", Title = w.T("reallife_co_q_raise_title", "A raise above my limit"), FaceName = "Ray Walsh", Face = true, Hue = 0.55f, Ring = UiRole.Warn, Sub = "Speedy Bites", Figure = w.F("reallife_mgr_row_day", "Day {day}", "day", "214"), Note = w.T("reallife_hier_you", "You"), Chip = open, ChipTone = UiRole.Warn },
                        new CompanyRow { Id = "he:2", Title = w.T("reallife_co_q_lease_title", "A lease is up for renewal"), FaceName = "Meera Iyer", Face = true, Hue = 0.75f, Ring = UiRole.Ok, Sub = "Hudson Warehouse", Figure = w.F("reallife_mgr_row_day", "Day {day}", "day", "213"), Note = "Meera Iyer", Chip = open, ChipTone = UiRole.Accent },
                        new CompanyRow { Id = "he:3", Title = w.T("reallife_co_q_budget", "Budget"), FaceName = "Tessa Brandt", Face = true, Hue = 0.9f, Ring = UiRole.Ok, Sub = "Bloom & Co", Figure = w.F("reallife_mgr_row_day", "Day {day}", "day", "211"), Note = "Tessa Brandt", Chip = w.T("reallife_hier_log_settled", "Settled"), ChipTone = UiRole.Muted }
                    }
                };
                var c = new CompanyCard
                {
                    Kicker = w.T("reallife_co_inbox", "Questions inbox"), Title = w.T("reallife_co_q_raise_title", "A raise above my limit"), Sub = "Speedy Bites", Icon = UiIcon.Envelope,
                    Chips = new[] { open, w.F("reallife_hier_chip_with", "With {who}", "who", w.T("reallife_hier_you", "You")) },
                    ChipTones = new[] { UiRole.Warn, UiRole.Accent }, Back = CBack(w, null, CompanyView.Managers)
                };
                c.Blocks = new[]
                {
                    new CompanyBlock { Kind = CompanyBlockKind.Items, Items = new[] { new CompanyItem { Grade = -1, Title = "Ray Walsh", Sub = w.T("reallife_co_q_manager_asks", "Manager"), Face = true, Hue = 0.55f, Ring = UiRole.Warn, Press = true, Act = CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers) } } },
                    CNote(w.F("reallife_hier_say_exception", "{name} asks for {asked} an hour; the limit is {ceiling}.", "name", "Dev Kapoor", "asked", "$19.00", "ceiling", "$18.00")),
                    CBtns(CB(w.T("reallife_co_answer", "Answer"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.Go, "card", 1), UiIcon.Next)),
                    CBtns(CB(w.T("reallife_hier_open_staff", "Open the staff"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "emp:dev", CompanyView.Staff), UiIcon.People),
                        CB(w.T("reallife_hier_open_dashboard", "Open the dashboard"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubDashboard), UiIcon.ChartPie),
                        CB(w.T("reallife_hier_open_policy", "Open the policy"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubPolicy), UiIcon.Sliders)),
                    CSec(w.T("reallife_hier_sec_story", "The story")),
                    CNote(w.F("reallife_hier_raised_by", "Raised on day {day} by {name}.", "day", "214", "name", "Ray Walsh")),
                    CNote(w.T("reallife_hier_what_yours", "This one is yours to answer."))
                };
                page.Card = c;
                break;
            }
            case "policy":
            case "rule":
            {
                var rows = new List<CompanyRow>
                {
                    new CompanyRow { Caption = true, Title = w.T("reallife_pol_cat_hiring", "Hiring") },
                    new CompanyRow { Id = "pf:0", Title = w.T("reallife_pol_field_skill_floor", "Least skill hired"), Icon = CompanyGlyphs.Policy(0), Figure = w.T("reallife_co_sick_none", "None"), Sub = w.T("reallife_pol_as_above", "As above") },
                    new CompanyRow { Id = "pf:1", Title = "Most a hire is paid", Icon = CompanyGlyphs.Policy(1), Figure = "100%", Sub = w.T("reallife_pol_as_above", "As above") },
                    new CompanyRow { Caption = true, Title = w.T("reallife_pol_cat_pay", "Pay") },
                    new CompanyRow { Id = "pf:3", Title = w.T("reallife_pol_field_pay_band", "Pay band"), Icon = CompanyGlyphs.Policy(3), Figure = "110%", Sub = w.T("reallife_pol_set_here_row", "Set here"), Chip = w.T("reallife_co_held", "Held"), ChipTone = UiRole.Warn },
                    new CompanyRow { Id = "pf:4", Title = w.T("reallife_pol_field_raise_most", "Largest raise"), Icon = CompanyGlyphs.Policy(4), Figure = "5%", Sub = w.T("reallife_pol_set_here_row", "Set here") },
                    new CompanyRow { Caption = true, Title = w.T("reallife_pol_cat_escalation", "Escalation") },
                    new CompanyRow { Id = "pf:21", Title = w.T("reallife_pol_field_unmet_days", "Days a request may wait"), Icon = CompanyGlyphs.Policy(21), Figure = "2 days", Sub = w.T("reallife_pol_set_here_row", "Set here") }
                };
                page.List = new CompanyList
                {
                    Title = w.T("reallife_hier_dir_policy", "Policy centre"), Rows = rows.ToArray(),
                    Filters = new[] { w.T("reallife_pol_scope_company", "Company"), "Midtown", "Speedy Bites" }, Filter = 2, Chosen = which == "rule" ? 4 : -1
                };
                m.FilterIds = new[] { "own", "reg:Midtown", "mgr:" + place };
                if (which == "rule")
                {
                    var c = new CompanyCard
                    {
                        Kicker = w.T("reallife_pol_cat_pay", "Pay") + ", " + w.F("reallife_pol_scope_store", "{store}", "store", "Speedy Bites"), Title = w.T("reallife_pol_field_pay_band", "Pay band"), Icon = CompanyGlyphs.Policy(3),
                        Back = CBack(w, "mgr:" + place, CompanyView.Managers, CompanyView.SubPolicy)
                    };
                    c.Blocks = new[]
                    {
                        CNote(w.T("reallife_pol_field_pay_band_what", "The most anybody is paid after a raise or an offer matched, as a share of the going rate.")),
                        CSec(w.T("reallife_pol_set_here", "Set here")),
                        new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = w.T("reallife_pol_field_pay_band", "Pay band"), Options = new[] { "Follow: 105%", "100%", "105%", "110%", "120%" }, Chosen = 3,
                            Acts = new[] { CA(CompanyDo.RuleSet, place, 3, -1, 2), CA(CompanyDo.RuleSet, place, 3, 0, 2), CA(CompanyDo.RuleSet, place, 3, 1, 2), CA(CompanyDo.RuleSet, place, 3, 2, 2), CA(CompanyDo.RuleSet, place, 3, 3, 2) } },
                        CSec(w.T("reallife_pol_sec_force", "In force")),
                        CRow(w.T("reallife_pol_scope_company", "The whole company"), "105%"),
                        CRow(w.T("reallife_pol_col_force", "In force here"), "105%", UiRole.WarnText),
                        CNote(w.F("reallife_pol_held_line", "A lower scope may only be stricter: this one is held to {value}.", "value", "105%"), UiRole.WarnText),
                        CNote(w.T("reallife_pol_way_up", "A higher step is looser."))
                    };
                    page.Card = c;
                }
                else
                {
                    var c = new CompanyCard { Kicker = w.T("reallife_hier_dir_policy", "Policy centre"), Title = w.T("reallife_pol_sec_scope", "This scope"), Icon = UiIcon.Sliders, Back = CBack(w, "mgr:" + place, CompanyView.Managers) };
                    c.Blocks = new[]
                    {
                        CNote(w.T("reallife_pol_what", "A rule set here holds for this scope. A lower scope may only be stricter than the one above it.")),
                        CSec(w.T("reallife_pol_own", "Rules set here"), "3"),
                        new CompanyBlock
                        {
                            Kind = CompanyBlockKind.Items, Items = new[]
                            {
                                new CompanyItem { Grade = -1, Title = w.T("reallife_pol_field_pay_band", "Pay band"), Sub = w.T("reallife_pol_cat_pay", "Pay"), Icon = CompanyGlyphs.Policy(3), Figure = "110%", Chip = w.T("reallife_co_held", "Held"), ChipTone = UiRole.Warn },
                                new CompanyItem { Grade = -1, Title = w.T("reallife_pol_field_raise_most", "Largest raise"), Sub = w.T("reallife_pol_cat_pay", "Pay"), Icon = CompanyGlyphs.Policy(4), Figure = "5%" },
                                new CompanyItem { Grade = -1, Title = w.T("reallife_pol_field_unmet_days", "Days a request may wait"), Sub = w.T("reallife_pol_cat_escalation", "Escalation"), Icon = CompanyGlyphs.Policy(21), Figure = "2 days" }
                            }
                        },
                        CBtns(CB(w.T("reallife_pol_templates", "Templates"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubTemplates), UiIcon.ClipboardText),
                            CB(w.T("reallife_pol_save_template", "Save as a template"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.TemplateSave, place, 0, 0, 2), UiIcon.FloppyDisk)),
                        CBtns(CB(w.T("reallife_pol_follow", "Follow the scope above"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RuleClear, place, 0, 0, 2), UiIcon.Back, false, null, true))
                    };
                    page.Card = c;
                }
                break;
            }
            case "templates":
            case "tpl_own":
            {
                bool own = which == "tpl_own";
                string saved = w.F("reallife_pol_tpl_saved", "Saved on day {day}", "day", "190");
                page.List = new CompanyList
                {
                    Title = w.T("reallife_pol_templates", "Templates"), Count = "3", Chosen = own ? 2 : 1,
                    Rows = new[]
                    {
                        new CompanyRow { Id = "pk:b0", Title = w.T("reallife_pol_tpl_start", "The starting policy"), Sub = w.T("reallife_pol_tpl_built", "Built in"), Icon = UiIcon.ClipboardText },
                        new CompanyRow { Id = "pk:b1", Title = w.T("reallife_pol_tpl_careful", "Careful"), Sub = w.T("reallife_pol_tpl_built", "Built in"), Icon = UiIcon.ClipboardText },
                        new CompanyRow { Id = "pk:o1", Title = saved, Sub = w.T("reallife_pol_tpl_yours", "Saved by you"), Icon = UiIcon.ClipboardText }
                    }
                };
                var c = new CompanyCard
                {
                    Kicker = w.F("reallife_pol_scope_store", "{store}", "store", "Speedy Bites"), Title = own ? saved : w.T("reallife_pol_tpl_careful", "Careful"),
                    Sub = own ? w.T("reallife_pol_tpl_yours", "Saved by you") : w.T("reallife_pol_tpl_built", "Built in"), Icon = UiIcon.ClipboardText, Back = CBack(w, "mgr:" + place, CompanyView.Managers, CompanyView.SubPolicy)
                };
                CompanyItem Rule(int field, string name, string value) => new CompanyItem { Grade = -1, Title = name, Icon = CompanyGlyphs.Policy(field), Figure = value };
                c.Blocks = new[]
                {
                    CNote(own ? w.T("reallife_pol_templates_what", "A template is a set of rules you can put on a scope in one step.") : w.T("reallife_pol_tpl_careful_what", "Small raises only, the owner approves every exception, and managers report daily.")),
                    CNote(w.F("reallife_pol_tpl_onto", "Applying it sets these rules for {scope}.", "scope", "Speedy Bites")),
                    CSec(w.T("reallife_pol_sec_template", "What it sets")),
                    new CompanyBlock
                    {
                        Kind = CompanyBlockKind.Items, Items = new[]
                        {
                            Rule(3, w.T("reallife_pol_field_pay_band", "Pay band"), "100%"), Rule(0, w.T("reallife_pol_field_skill_floor", "Least skill hired"), "50"),
                            Rule(6, w.T("reallife_pol_field_approver", "Who may agree an exception"), "You"), Rule(12, w.T("reallife_pol_field_budget", "Budget"), "$200"),
                            Rule(19, w.T("reallife_pol_field_gap_days", "Days a gap may last"), "1 day"), Rule(24, w.T("reallife_pol_field_reports", "Reports"), "Every day")
                        }
                    },
                    own
                        ? CBtns(CB(w.T("reallife_pol_tpl_apply", "Apply"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.TemplateApply, place, 0, 1, 2), UiIcon.Check),
                            CB(w.T("reallife_pol_tpl_delete", "Delete"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.TemplateDelete, null, 1), UiIcon.Close, false, null, true))
                        : CBtns(CB(w.T("reallife_pol_tpl_apply", "Apply"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.TemplateApply, place, 1, 0, 2), UiIcon.Check))
                };
                page.Card = c;
                break;
            }
            case "dashboard":
            {
                string open = w.T("reallife_hier_state_open", "Open");
                page.List = new CompanyList
                {
                    Title = w.T("reallife_hier_dash_pending", "Questions here"), Count = "2", Chosen = -1,
                    Rows = new[]
                    {
                        new CompanyRow { Id = "he:1", Title = w.T("reallife_co_q_raise_title", "A raise above my limit"), FaceName = "Ray Walsh", Face = true, Hue = 0.55f, Ring = UiRole.Warn, Sub = w.T("reallife_hier_you", "You"), Figure = w.F("reallife_mgr_row_day", "Day {day}", "day", "214"), Chip = open, ChipTone = UiRole.Warn },
                        new CompanyRow { Id = "he:7", Title = w.T("reallife_co_q_budget", "Budget"), FaceName = "Ray Walsh", Face = true, Hue = 0.55f, Ring = UiRole.Ok, Sub = "Ray Walsh", Figure = w.F("reallife_mgr_row_day", "Day {day}", "day", "201"), Chip = w.T("reallife_hier_log_settled", "Settled"), ChipTone = UiRole.Muted }
                    }
                };
                var c = new CompanyCard
                {
                    Kicker = "Speedy Bites, Ray Walsh", Title = w.T("reallife_hier_dash_title", "Store dashboard"), Icon = UiIcon.ChartPie, Back = CBack(w, "mgr:" + place, CompanyView.Managers),
                    FactLabels = new[] { w.T("reallife_co_f_staff", "Staff"), w.T("reallife_co_f_standing", "Standing"), w.T("reallife_co_f_score", "Score"), w.T("reallife_co_f_pending", "Pending") },
                    FactValues = new[] { "9 of 9", "72", "81", "1" }
                };
                c.Blocks = new[]
                {
                    new CompanyBlock { Kind = CompanyBlockKind.Figures, Options = new[] { w.T("reallife_hier_fig_budget", "Budget"), w.T("reallife_ibox_profit", "Profit") },
                        Values = new[] { w.F("reallife_mgr_spent_value", "{spent} of {budget}", "spent", "$310", "budget", "$400"), "+" + CM(1140) } },
                    new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_hier_fig_budget_used", "Budget used"), Value = "78%", Number = 78, Tone = UiRole.Ok },
                    CSec(w.T("reallife_hier_sec_quick", "Do now")),
                    CBtns(CB(w.T("reallife_hier_quick_fill", "Fill the gaps"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.StoreFill, place), UiIcon.People),
                        CB(w.T("reallife_hier_quick_raise", "Raise the underpaid"), UiButtonKind.Secondary, UiAction.Option, CA(CompanyDo.StoreRaise, place), UiIcon.Coin),
                        CB(w.T("reallife_hier_open_policy", "Open the policy"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubPolicy), UiIcon.Sliders)),
                    CSec(w.F("reallife_co_crew_title", "{name}'s crew", "name", "Ray"), "9"),
                    new CompanyBlock
                    {
                        Kind = CompanyBlockKind.Items, Items = new[]
                        {
                            new CompanyItem { Title = "Dev Kapoor", Sub = "Customer service", Face = true, Hue = 0.35f, Ring = UiRole.Warn, Grade = 52, Figure = "52", Press = true, Act = CA(CompanyDo.Go, "emp:dev", CompanyView.Staff) },
                            new CompanyItem { Title = "Lena Adler", Sub = "Cooking", Face = true, Hue = 0.1f, Ring = UiRole.Ok, Grade = 64, Figure = "64", Press = true, Act = CA(CompanyDo.Go, "emp:lena", CompanyView.Staff) },
                            new CompanyItem { Title = "Maya Okafor", Sub = "Customer service", Face = true, Hue = 0.6f, Ring = UiRole.Ok, Grade = 78, Figure = "78", Press = true, Act = CA(CompanyDo.Go, "emp:maya", CompanyView.Staff) }
                        }
                    },
                    new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = w.T("reallife_hier_region_pick", "Region"), Options = new[] { w.F("reallife_hier_region_own", "{district}", "district", "Midtown"), w.F("reallife_hier_region_custom", "Region {letter}", "letter", "A"), w.F("reallife_hier_region_custom", "Region {letter}", "letter", "B") }, Chosen = 0,
                        Acts = new[] { CA(CompanyDo.StoreArea, place, 0), CA(CompanyDo.StoreArea, place, 1), CA(CompanyDo.StoreArea, place, 2) } },
                    CNote(w.T("reallife_hier_dash_what", "A store dashboard shows what this manager holds and lets you act for them."))
                };
                page.Card = c;
                break;
            }
            case "regiondash":
            {
                page.List = new CompanyList
                {
                    Title = w.T("reallife_hier_dash_pending", "Questions here"), Count = "1", Chosen = -1,
                    Rows = new[] { new CompanyRow { Id = "he:2", Title = w.T("reallife_co_q_lease_title", "A lease is up for renewal"), FaceName = "Omar Haddad", Face = true, Hue = 0.75f, Ring = UiRole.Warn, Sub = "Hudson Warehouse", Figure = w.F("reallife_mgr_row_day", "Day {day}", "day", "213"), Chip = w.T("reallife_hier_state_open", "Open"), ChipTone = UiRole.Warn } }
                };
                var c = new CompanyCard
                {
                    Kicker = w.F("reallife_mgr_row_region", "Region {district}", "district", "Midtown"), Sub = "Meera Iyer", Title = w.T("reallife_hier_dash_title", "Store dashboard"), Icon = UiIcon.ChartPie,
                    Back = CBack(w, "reg:Midtown", CompanyView.Managers),
                    FactLabels = new[] { w.T("reallife_co_f_places", "Places"), w.T("reallife_co_f_gaps", "Gaps"), w.T("reallife_co_f_standing", "Standing"), w.T("reallife_co_f_pending", "Pending") },
                    FactValues = new[] { "2/3", "1", "68", "1" }
                };
                c.Blocks = new[]
                {
                    new CompanyBlock { Kind = CompanyBlockKind.Figures, Options = new[] { w.T("reallife_hier_fig_score", "Score"), w.T("reallife_hier_fig_profit", "Profit"), w.T("reallife_pol_field_profit_target", "Profit target") },
                        Values = new[] { "77", "+" + CM(1310), "$300" } },
                    CSec(w.T("reallife_hier_sec_quick", "Do now")),
                    CBtns(CB(w.T("reallife_hier_quick_fill", "Fill the gaps"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.RegionFill, "Midtown"), UiIcon.People),
                        CB(w.T("reallife_hier_quick_raise", "Raise the underpaid"), UiButtonKind.Secondary, UiAction.Option, CA(CompanyDo.RegionRaise, "Midtown"), UiIcon.Coin),
                        CB(w.T("reallife_hier_quick_move", "Move crew"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RegionMove, "Midtown"), UiIcon.Next)),
                    CBtns(CB(w.T("reallife_hier_quick_appoint", "Appoint managers"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RegionAppoint, "Midtown"), UiIcon.People),
                        CB(w.T("reallife_hier_open_policy", "Open the policy"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "reg:Midtown", CompanyView.Managers, CompanyView.SubPolicy), UiIcon.Sliders)),
                    CSec(w.T("reallife_mgr_region_their_places", "Their places"), "3"),
                    new CompanyBlock
                    {
                        Kind = CompanyBlockKind.Items, Items = new[]
                        {
                            new CompanyItem { Title = "Ray Walsh", Sub = "Speedy Bites", Face = true, Hue = 0.55f, Ring = UiRole.Ok, Grade = 72, Figure = "72", Press = true, Act = CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers) },
                            new CompanyItem { Title = "Pioneer Game Studio", Sub = w.T("reallife_mgr_empty_post", "Empty post"), Icon = UiIcon.Store, Grade = 64, Figure = "64", Press = true, Act = CA(CompanyDo.Go, "post:Pioneer Game Studio~9", CompanyView.Managers) },
                            new CompanyItem { Title = "Noor Haddad", Sub = "Midtown Deli", Face = true, Hue = 0.2f, Ring = UiRole.Ok, Grade = 69, Figure = "69", Press = true, Act = CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers) }
                        }
                    },
                    CNote(w.T("reallife_hier_region_dash_what", "A region dashboard shows what its manager holds and lets you act for them."))
                };
                page.Card = c;
                break;
            }
            default:   // seat
            {
                bool pick = which == "seat_pick", filled = which == "seat_filled";
                page.List = new CompanyList
                {
                    Title = w.T("reallife_co_candidates", "Who could do it"), Count = "2", Chosen = pick ? 0 : -1,
                    Rows = new[]
                    {
                        new CompanyRow { Id = "xc:pro1", Title = filled ? "Marta Silva" : "Noah Feldman", Sub = filled ? "Operations, senior" : "Finance, senior", Face = true, Hue = 0.2f, Ring = UiRole.None, Figure = filled ? "$46.00" : "$42.00", Note = "Pioneer HQ" },
                        new CompanyRow { Id = "xc:pro2", Title = filled ? "Jonah Reyes" : "Ines Duarte", Sub = filled ? "Operations, lead" : "Finance, lead", Face = true, Hue = 0.5f, Ring = UiRole.None, Figure = filled ? "$58.00" : "$55.00", Note = "Pioneer HQ" }
                    }
                };
                var c = new CompanyCard { Kicker = w.T("reallife_hier_seats", "Executive seats"), Title = "Chief financial officer", Icon = UiIcon.IdBadge, Back = CBack(w, "seat:3", CompanyView.Managers), Chips = new[] { w.T("reallife_mgr_empty_post", "Empty post") }, ChipTones = new[] { UiRole.Muted } };
                c.Blocks = new[]
                {
                    CNote(w.T("reallife_hier_seat_what_cfo", "The finance chief tops up the tax reserve when a quarter closes and watches the managers' budgets.")),
                    CNote(w.F("reallife_hier_seat_hire", "Choose one of your professionals: {roles}.", "roles", "Accountant, Financial analyst")),
                    CNote(w.T("reallife_hier_exec_pick", "Pick a professional at the left."))
                };
                if (pick)
                {
                    c = new CompanyCard
                    {
                        Kicker = "Chief financial officer", Title = "Noah Feldman", Sub = "Finance, senior, Pioneer HQ", Face = true, Hue = 0.2f, Ring = UiRole.None, Back = CBack(w, "seat:3", CompanyView.Managers)
                    };
                    c.Blocks = new[]
                    {
                        CRow(w.T("reallife_hier_going", "Going rate"), "$42.00"),
                        CNote(w.T("reallife_hier_seat_what_cfo", "The finance chief tops up the tax reserve when a quarter closes and watches the managers' budgets.")),
                        CBtns(CB(w.T("reallife_mgr_appoint", "Appoint"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.SeatAppoint, "pro1", 3), UiIcon.Check))
                    };
                }
                else if (filled)
                {
                    c = new CompanyCard
                    {
                        Kicker = w.T("reallife_hier_seats", "Executive seats"), Title = "Ethan Brooks", Sub = "Chief operating officer", Face = true, Hue = 0.15f, Ring = UiRole.Ok, Back = CBack(w, "exe:1", CompanyView.Managers),
                        Chips = new[] { w.T("reallife_mgr_in_post", "In post") }, ChipTones = new[] { UiRole.Ok }
                    };
                    c.Blocks = new[]
                    {
                        CNote(w.T("reallife_hier_seat_what_coo", "The operating chief answers what the managers pass up and watches their targets.")),
                        CRow(w.T("reallife_mgr_since", "Since"), w.F("reallife_mgr_row_day", "Day {day}", "day", "60")), CRow(w.T("reallife_hier_col_role", "Role"), "Operations, senior"), CRow(w.T("reallife_hier_going", "Going rate"), "$48.00"),
                        CRow(w.T("reallife_mgr_allowance", "Allowance"), w.F("reallife_co_row_a_day", "{amount} a day", "amount", "$96")), CRow(w.T("reallife_hier_paid_month", "Paid this month"), CM(2880)),
                        CRow(w.T("reallife_hier_area_open", "Open in their area"), "2"), CRow(w.T("reallife_hier_area_settled", "Settled this week"), "7"), CRow(w.T("reallife_mgr_decided", "Decided"), "41 / 38"),
                        CBtns(CB(w.T("reallife_mgr_stand_down", "Stand down"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.SeatStandDown, null, 1), UiIcon.Close, false, null, true))
                    };
                }
                page.Card = c;
                break;
            }
        }
        m.Sub = page;
    }
}
