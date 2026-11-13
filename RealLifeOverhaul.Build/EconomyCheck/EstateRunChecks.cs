using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.Ui;

// A hundred days of the property business and of the car dealership as their directors run them, for what the interface conversion added. The
// directors call the game-free day entries (EstateDay: People, Services, Unit, Ring; DealerDay: Morning, with DealerRules and RivalRules for the
// hours), and so do these checks, over a seeded city with its other landlords, its other dealers and its people changing jobs: the state of each new
// feature changes as it should, the other side acts, a save written on day 30 reads back to the same next thirty days, and a save from before the
// features plays on.
internal static partial class Program
{
    private sealed class EstateRunState
    {
        public EstateState S;
        public TenantPoolStub Pool;
        public int Cash;
        public int Inspected, Passed, Asked, Accepted, Refused, Lapsed, Done, Skipped, Missed, Quit, Relet, CallsOfKind1, Named;
    }

    private static EstateRunState RunEstateNew(int seed, int units, EstateClimate c, int factor = 13)
    {
        var r = new EstateRunState { Pool = TenantPoolStub.Of(600, 120), S = new EstateState { Seed = seed } };
        for (int b = 0; b < 2; b++)
        {
            EstateHolding h = Units(units - b, 1000000, 560);   // an older building, so something goes wrong in it now and then
            h.Place = "Run%20Street~" + b;
            h.Name = "Run Street " + b;
            r.S.Holdings.Add(h);
            int rent = Math.Max(40, EstateRules.UnitRent(h, c) * factor / 10);   // a rent over the market's: the tenants are not easily content
            for (int u = 0; u < h.Units.Count; u++)
            {
                LetUnit(h, u, rent, 80, 1, 400);
            }
        }
        return r;
    }

    // One morning of the property business, as EstateDirector.HoldingDay runs it for a managed building (the game's money is the cash a run keeps).
    private static void RunEstateDay(EstateRunState r, int day, EstateClimate c, bool servicing, bool answer, int policy = 0)
    {
        r.S.LastDay = day;
        for (int i = 0; i < r.S.Holdings.Count; i++)
        {
            EstateHolding h = r.S.Holdings[i];
            EstateRules.Lapse(h, day);
            EstateRules.Wear(h);
            h.Cond = Math.Max(h.Cond, day % 10 == 0 ? 560 : h.Cond);   // the landlord keeps the building up (works are checked with the estate's own)
            EstateDay.People(r.S, h, day, r.Pool);
            if (servicing && day >= 3 && h.Services.Count == 0 && !ServiceRules.Holds(h, 1, day + 14))
            {
                ServiceRules.Book(h, 1, day, ServiceRules.Earliest(day), "Run Trades|1", ServiceRules.Fee(h, 1, c.Index));
            }
            int done = EstateDay.Services(h, day, c.DaysPerYear, b => { r.Cash -= b.Fee; return true; }, (b, ok) => { r.Done += ok ? 1 : 0; });
            for (int u = 0; u < h.Units.Count; u++)
            {
                EstateUnit unit = h.Units[u];
                int callBefore = unit.Call, askedBefore = unit.Improve, refusedBefore = unit.Refused;
                bool wasLet = unit.Let;
                TenancyDay d = EstateDay.Unit(r.S, h, u, day, c, true);
                r.Missed += d.Missed ? 1 : 0;
                r.Quit += d.Quit ? 1 : 0;
                if (unit.Let)
                {
                    // The landlord's duties of the morning, as DutiesMorning runs them: a call raised, a request made, the tenant's mood, a complaint, quitting.
                    DutyDay duty = EstateRules.Duty(h, u, r.S.Seed, day, c);
                    r.CallsOfKind1 += callBefore == 0 && unit.Call == 1 ? 1 : 0;
                    r.Asked += askedBefore == 0 && unit.Improve > 0 ? 1 : 0;
                    r.Lapsed += duty.AskLapsed ? 1 : 0;
                    if (answer && unit.Call > 0 && unit.CallAnswer == (int)CallAnswer.Waiting && day - unit.CallDay >= 1 && EstateRules.SendContractor(unit, day))
                    {
                        r.Cash -= unit.CallCost;
                    }
                                    }
                if (d.Lump > 0)
                {
                    r.Cash += d.Lump;
                }
                if (unit.Let && unit.Rent > 0 && !d.Missed)
                {
                    r.Cash += unit.Rent;
                }
                // The player answers a request three days after it is made: the first kind yes, the second no, the rest left to lapse.
                if (answer && unit.Improve > 0 && day - unit.ImproveDay >= 3 && (policy != 0 || unit.Improve <= 2))
                {
                    bool yes = policy == 2 || (policy == 0 && unit.Improve == 1);
                    EstateDay.AnswerResult answered = EstateDay.Answer(h, unit, yes, true, cost =>
                    {
                        r.Cash -= cost;
                        return true;
                    });
                    r.Accepted += answered == EstateDay.AnswerResult.Done && yes ? 1 : 0;
                    r.Refused += answered == EstateDay.AnswerResult.Done && !yes ? 1 : 0;
                }
                // The other side of a lease: a unit that stood empty for a week is let again to someone of the pool.
                if (!unit.Let && day % 7 == 0)
                {
                    LetUnit(h, u, Math.Max(40, EstateRules.UnitRent(h, c)), 70, day, 400);
                    EstateDay.Lease(r.S, h, u, day, r.Pool);
                    r.Relet++;
                    r.Named += h.Units[u].PersonId > 0 ? 1 : 0;
                }
            }
            Inspection insp = EstateDay.Inspect(r.S, h, day, c, false);
            r.Inspected += insp.Done ? 1 : 0;
            r.Passed += insp.Done && insp.Passed ? 1 : 0;
        }
    }

    // What the game gives a building again when a save is loaded (its value and the income its units' rent is judged against), as EstateDirector.Start reads it from the game.
    private static void RunGameRefresh(EstateState from, EstateState into)
    {
        for (int i = 0; i < into.Holdings.Count && i < from.Holdings.Count; i++)
        {
            into.Holdings[i].Income = from.Holdings[i].Income;
            into.Holdings[i].Base = from.Holdings[i].Base;
        }
    }

    private static string EstateDigest(EstateRunState r, bool strain = true)
    {
        var sb = new StringBuilder(2048);
        sb.Append(r.Cash).Append('|').Append(r.S.RentRingCount).Append('|').Append(r.S.RentRingMonth).Append('|');
        for (int i = 0; i < r.S.RentRingCount; i++)
        {
            sb.Append(r.S.RentRing[i]).Append(',');
        }
        for (int i = 0; i < r.S.Marks.Count; i++)
        {
            sb.Append(r.S.Marks[i].Id).Append(':').Append(r.S.Marks[i].Rent).Append('/').Append(r.S.Marks[i].Prev).Append(',');
        }
        for (int i = 0; i < r.S.Holdings.Count; i++)
        {
            EstateHolding h = r.S.Holdings[i];
            sb.Append('#').Append(h.Cond).Append('.').Append(h.CostYear).Append('.').Append(h.CallsYear).Append('.').Append(h.Services.Count).Append('.').Append(h.LastInspectDay);
            for (int k = 0; k < h.ServiceUntil.Length; k++)
            {
                sb.Append(h.ServiceUntil[k]).Append(',');
            }
            for (int u = 0; u < h.Units.Count; u++)
            {
                EstateUnit x = h.Units[u];
                sb.Append('/').Append(x.Let ? 1 : 0).Append('.').Append(x.Rent).Append('.').Append(x.Arrears).Append('.').Append(x.Happy).Append('.').Append(x.PersonId).Append('.').Append(strain ? x.Strain : 0)
                    .Append('.').Append(x.Call).Append('.').Append(x.Improve).Append('.').Append(x.ImproveDay).Append('.').Append(x.Refused).Append('.').Append(x.PaymentCount);
                for (int p = 0; p < x.PaymentCount; p++)
                {
                    sb.Append(':').Append(x.Payments[p].Due).Append('-').Append(x.Payments[p].Paid).Append('-').Append(x.Payments[p].State);
                }
            }
        }
        return sb.ToString();
    }

    // The city's rents as the day's read gives them: districts at a rent that climbs, and two other landlords whose rents climb at their own pace.
    private static RentMapData RunCity(EstateState s, int day, int monthDays)
    {
        var data = new RentMapData();
        data.Clear(day, monthDays);
        int month = day / monthDays;
        string[] districts = { "Midtown", "Uptown", "Hell's Kitchen" };
        for (int d = 0; d < districts.Length; d++)
        {
            data.Add(districts[d], (int)EstateUse.Homes, (90 + d * 12) * (1000 + month * 12) / 1000 * 40, 4000, 40, 2 + d);
            data.Add(districts[d], (int)EstateUse.Shops, 300 * (1000 + month * 8) / 1000 * 6, 900, 6, 1);
        }
        data.AddLandlord("rival:1", (int)EstateUse.Homes, 1200 * (1000 + month * 15) / 1000, 12);
        data.AddLandlord("rival:2", (int)EstateUse.Homes, 700 * (1000 - month * 5) / 1000, 8);
        return data;
    }


    // ------------------------------------------------------------------------------------------------- the new actions, pressed through the screen

    // A source that writes a unit's page from a real state with the new actions on it, each carrying the very rule the director's method runs
    // (ImproveRules.Accept and Refuse, ServiceRules.Book and Cancel), so a press goes screen -> run -> rule -> state, and the state is read back.
    private sealed class EstateActionSource : IEstateSource
    {
        public readonly EstateState S;
        public readonly EstateClimate C;
        public int Cash, Day = 40, Generation_;
        public EstateDay.AnswerResult Answer = EstateDay.AnswerResult.Nothing;

        public EstateActionSource(EstateState s, EstateClimate c)
        {
            S = s;
            C = c;
        }

        public int Generation => Generation_;

        public void Opened(EstateView view)
        {
        }

        public void Fill(EstateModel m, EstateView v)
        {
            EstateHolding h = S.Holdings[0];
            EstateUnit u = h.Units[0];
            var runs = new EstateRuns();
            m.Title = "Property";
            m.Icon = UiIcon.Home;
            m.Nav = new UiNavEntry[0];
            m.Shape = EstateShape.Detail;
            m.Back = "Back";
            m.DetailIcon = UiIcon.Home;
            m.DetailTitle = h.Name;
            var buttons = new List<UiActionModel>();
            if (u.Improve > 0)
            {
                buttons.Add(runs.Button("Pay for it", UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, () =>
                {
                    Answer = EstateDay.Answer(h, u, true, true, cost =>
                    {
                        if (Cash < cost)
                        {
                            return false;
                        }
                        Cash -= cost;
                        return true;
                    });
                    Generation_++;
                }));
                buttons.Add(runs.Button("Say no", UiButtonKind.Secondary, UiIcon.None, UiAction.Count, () =>
                {
                    Answer = EstateDay.Answer(h, u, false, true, cost => true);
                    Generation_++;
                }));
            }
            buttons.Add(runs.Button("Book a service", UiButtonKind.Secondary, UiIcon.Wrench, UiAction.Count, () =>
            {
                EstateDay.BookService(h, 2, Day, S.Seed, new[] { "Run Trades|1" }, C.Index, true);
                Generation_++;
            }, h.Services.Count > 0));
            if (h.Services.Count > 0)
            {
                buttons.Add(runs.Button("Cancel it", UiButtonKind.Secondary, UiIcon.None, UiAction.Count, () =>
                {
                    EstateDay.CancelService(h, 2);
                    Generation_++;
                }));
            }
            m.Left = new[] { new UiFactCardModel { Title = "The unit", Heading = true, Lines = new[] { UiFactLine.Row("Happy", u.Happy.ToString()) }, Actions = buttons.ToArray() } };
            m.Right = new UiFactCardModel[0];
            m.Runs = runs.ToArray();
        }
    }

    // The answers of the new bands, pressed through the screen against the rules the directors call: Take it on and Decline on an offered instruction (AgencyRules, the
    // agency's own list), Send a reminder on a late unit (EstateRules.Remind) and Add to the fleet on the hire file. Each press is read back in the state.
    private sealed class EstateAnswerSource : IEstateSource
    {
        public readonly EstateState S;
        public readonly Instruction Offered;
        public readonly EstateUnit Late;
        public int Cash = 100000, Day = 60, Generation_;

        public EstateAnswerSource(EstateState s, Instruction offered, EstateUnit late)
        {
            S = s;
            Offered = offered;
            Late = late;
        }

        public int Generation => Generation_;

        public void Opened(EstateView view)
        {
        }

        public void Fill(EstateModel m, EstateView v)
        {
            var runs = new EstateRuns();
            m.Title = "Property";
            m.Icon = UiIcon.Home;
            m.Nav = new UiNavEntry[0];
            m.Shape = EstateShape.Detail;
            m.Back = "Back";
            m.DetailTitle = "Answers";
            var buttons = new List<UiActionModel>
            {
                runs.Button("Take it on", UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, () =>
                {
                    int cost = AgencyRules.TakeCost(Offered);
                    if (cost > 0 && Cash >= cost)
                    {
                        Cash -= cost;
                        AgencyRules.Take(Offered, Day);
                    }
                    Generation_++;
                }),
                runs.Button("Decline", UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () =>
                {
                    if (S.Agency.Find(Offered.Id) != null && Offered.Stage == (int)InstructionStage.Offered)
                    {
                        S.Agency.Open.Remove(Offered);
                    }
                    Generation_++;
                }),
                runs.Button("Send a reminder", UiButtonKind.Secondary, UiIcon.None, UiAction.Count, () =>
                {
                    EstateRules.Remind(Late);
                    Generation_++;
                }, Late.Reminded),
                runs.Button("Add to the fleet", UiButtonKind.Secondary, UiIcon.Plus, UiAction.Count, () =>
                {
                    S.Hire.Cars.Add(new HireCar { Id = "v" + (S.Hire.Cars.Count + 1), Type = "car" });
                    Generation_++;
                })
            };
            m.Left = new[] { new UiFactCardModel { Title = "Answers", Heading = true, Lines = new UiFactLine[0], Actions = buttons.ToArray() } };
            m.Right = new UiFactCardModel[0];
            m.Runs = runs.ToArray();
        }
    }

    private static void EstateAnswersPressed()
    {
        EstateClimate c = Climate();
        c.DaysPerYear = 360;
        EstateRunState run = RunEstateNew(5, 3, c);
        EstateState st = run.S;
        var offered = new Instruction { Id = 7, District = "Midtown", Sale = true, Sqm = 120, Value = 2000000, Ask = 1000, Stage = (int)InstructionStage.Offered, Hours = 35, Day = 70 };
        var second = new Instruction { Id = 8, District = "Uptown", Sale = false, Sqm = 60, Value = 3000, Ask = 1000, Stage = (int)InstructionStage.Offered, Hours = 12, Day = 70 };
        st.Agency.Open.Add(offered);
        st.Agency.Open.Add(second);
        EstateUnit late = st.Holdings[0].Units[0];
        late.Tenant = 1;
        late.Arrears = 900;
        late.ArrearsDays = 6;
        late.Reminded = false;
        late.Happy = 600;
        var src = new EstateAnswerSource(st, offered, late);
        var screen = new EstateScreen(src, "estate");
        int cost = AgencyRules.TakeCost(offered);
        bool took = (EstatePress(screen, "Take it on") || EstatePress(screen, "TAKE IT ON"));
        Check(took && offered.Stage == (int)InstructionStage.Marketing && offered.Day == 60 + EstateCatalogue.InstructionLapseDays && src.Cash == 100000 - cost && cost > 0,
            "estate: pressing Take it on marketing the offered instruction pays its bill and starts it (" + cost + " paid, stage " + offered.Stage + ")");
        bool again = (EstatePress(screen, "Take it on") || EstatePress(screen, "TAKE IT ON"));
        Check(again && src.Cash == 100000 - cost, "estate: an instruction already taken costs nothing a second time");
        var gone = new EstateAnswerSource(st, second, late);
        var declining = new EstateScreen(gone, "estate");
        bool declined = (EstatePress(declining, "Decline") || EstatePress(declining, "DECLINE"));
        Check(declined && st.Agency.Find(8) == null && st.Agency.Find(7) != null, "estate: pressing Decline takes the offered instruction off the agency's list and leaves the others");
        int happy = late.Happy;
        bool reminded = (EstatePress(screen, "Send a reminder") || EstatePress(screen, "SEND A REMINDER"));
        Check(reminded && late.Reminded && late.Happy == happy - EstateCatalogue.HappyReminder, "estate: pressing Send a reminder marks the unit reminded and costs the tenant's satisfaction");
        bool twice = (EstatePress(screen, "Send a reminder") || EstatePress(screen, "SEND A REMINDER"));
        Check(!twice && late.Happy == happy - EstateCatalogue.HappyReminder, "estate: a reminder already sent is a disabled button that does nothing");
        int cars = st.Hire.Cars.Count;
        bool added = (EstatePress(screen, "Add to the fleet") || EstatePress(screen, "ADD TO THE FLEET"));
        Check(added && st.Hire.Cars.Count == cars + 1, "estate: pressing Add to the fleet puts a car on the hire file");
    }

    private static bool EstatePress(EstateScreen screen, string label)
    {
        var s = new UiScene();
        screen.Lay(s, UiFrame.FullMenu(16f / 9f, "Property"), true, UiDefaultKeys.Keyboard);
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].Kind == UiKind.Text && s.Items[i].Text == label && s.Items[i].Node >= 0)
            {
                ref UiNode n = ref s.Nodes[s.Items[i].Node];
                return screen.Press(n.Command, n.Arg);
            }
        }
        // The label sits on a button whose node is its parent's.
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].Kind == UiKind.Text && s.Items[i].Text == label)
            {
                for (int k = 0; k < s.NodeCount; k++)
                {
                    ref UiNode n = ref s.Nodes[k];
                    if (n.Command == EstateFace.ActionCommand && s.Items[i].X >= n.X && s.Items[i].X <= n.X + n.W && s.Items[i].Y >= n.Y && s.Items[i].Y <= n.Y + n.H)
                    {
                        return screen.Press(n.Command, n.Arg);
                    }
                }
            }
        }
        return false;
    }

    private static void EstatePressed()
    {
        EstateClimate c = Climate();
        c.DaysPerYear = 360;
        EstateRunState run = RunEstateNew(5, 3, c);
        var src = new EstateActionSource(run.S, c);
        EstateHolding h = run.S.Holdings[0];
        EstateUnit u = h.Units[0];
        u.Improve = 1;
        u.ImproveDay = 38;
        u.ImproveCost = ImproveRules.Quote(h, 1, c.Index);
        u.Happy = 300;
        var screen = new EstateScreen(src, "estate");
        bool short_ = EstatePress(screen, "PAY FOR IT") || EstatePress(screen, "Pay for it");
        Check(short_ && src.Answer == EstateDay.AnswerResult.Short && u.Improve == 1 && u.Happy == 300, "with too little money the answer is Short: the request stands and the page can tell why");
        src.Cash = 1000000;
        bool a = EstatePress(screen, "PAY FOR IT") || EstatePress(screen, "Pay for it");
        Check(a && src.Answer == EstateDay.AnswerResult.Done && u.Improve == 0 && u.Happy > 300 && src.Cash < 1000000, "pressing a request's button pays for the improvement through the screen: the request is gone, the tenant gladder (" + u.Happy + "), the money spent (" + src.Cash + ")");
        u.Improve = 2;
        u.ImproveDay = 38;
        u.ImproveCost = 900;
        bool b = EstatePress(screen, "SAY NO") || EstatePress(screen, "Say no");
        Check(b && u.Improve == 0 && u.Refused == 1 && ImproveRules.QuitMore(u) > 0, "pressing the other button refuses it: the refusal is counted and the tenant is likelier to quit");
        bool booked = EstatePress(screen, "BOOK A SERVICE") || EstatePress(screen, "Book a service");
        Check(booked && h.Services.Count == 1 && h.Services[0].Kind == 2 && h.Services[0].Day >= src.Day + EstateCatalogue.ServiceDaysAhead, "pressing Book a service puts a visit in the building's list for a day at least " + EstateCatalogue.ServiceDaysAhead + " ahead");
        bool again = EstatePress(screen, "BOOK A SERVICE") || EstatePress(screen, "Book a service");
        Check(!again && h.Services.Count == 1, "a service already booked cannot be booked twice: the button is disabled and answers nothing");
        bool cancelled = EstatePress(screen, "CANCEL IT") || EstatePress(screen, "Cancel it");
        Check(cancelled && h.Services.Count == 0, "pressing Cancel takes the booking off the list");
    }


    // ------------------------------------------------------------------------------------------------- every action of the live sources has a real target

    // The game side of both screens reads the directors and carries every button to them. Each registration of a button, a link, a switch or a step in
    // the Live files must name its real target: a method of the estate's or the dealership's director that exists, a change of what the page shows
    // (the view's own fields, a page opened), or a jump into the game (the guide, the people page, the showroom floor). A "Show(" or a "Pay" alone is
    // no target. The counts by kind are printed so a page that lost its actions is seen.
    private static void EstateHandlers()
    {
        string root = EstateSourceRoot();
        if (root == null)
        {
            Check(false, "the Live files of the two apps were found for the handler scan");
            return;
        }
        var methods = new HashSet<string>();
        string economy = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, "..", "..", "Economy"));
        foreach (string folder in new[] { "Estate", "Dealer" })
        {
            foreach (string file in Directory.GetFiles(System.IO.Path.Combine(economy, folder), "*Director*.cs"))
            {
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(file), @"(?:public|internal)\s+[\w<>\[\],\.? ]+?\s+(\w+)\s*\("))
                {
                    methods.Add(m.Groups[1].Value);
                }
            }
        }
        var view = new System.Text.RegularExpressions.Regex(@"_v\.\w+\s*=|_eventYard\s*=|_eventFeature\s*=|_yardChoice\s*=|SetChoice\(|\bOpen\(|OpenWorks\(|OpenTenants\(");
        var game = new System.Text.RegularExpressions.Regex(@"EstateGame\.Locate\(|ShowroomView\.Instance\?\.(Look|Place)\(|EstateLive\.OpenPerson\(");
        var director = new System.Text.RegularExpressions.Regex(@"_d\.(\w+)\(");
        var kinds = new[] { "_runs.Button(", "_runs.Link(", "_runs.Switch(", "_runs.Step(", "_runs.Do(", "_runs.Add(", "Act(runs," };
        int found = 0, toDirector = 0, toView = 0, toGame = 0, bare = 0, unknown = 0, files = 0;
        string bareAt = "none", unknownAt = "none";
        foreach (string file in Directory.GetFiles(root, "*Live*.cs"))
        {
            files++;
            string text = File.ReadAllText(file);
            foreach (string kind in kinds)
            {
                int at = 0;
                while ((at = text.IndexOf(kind, at, StringComparison.Ordinal)) >= 0)
                {
                    int open = text.IndexOf('(', at);
                    int depth = 0, end = open;
                    bool quote = false;
                    for (; end < text.Length; end++)
                    {
                        char ch = text[end];
                        if (ch == '"' && text[end - 1] != '\\')
                        {
                            quote = !quote;
                        }
                        if (quote)
                        {
                            continue;
                        }
                        depth += ch == '(' ? 1 : ch == ')' ? -1 : 0;
                        if (depth == 0 && ch == ')')
                        {
                            break;
                        }
                    }
                    string call = text.Substring(at, Math.Min(text.Length - at, end - at + 1));
                    at += kind.Length;
                    if (call.StartsWith("_runs.Do(run)", StringComparison.Ordinal))
                    {
                        continue;   // the Act helper's own: its callers are scanned
                    }
                    found++;
                    var d = director.Matches(call);
                    if (d.Count > 0)
                    {
                        bool all = true;
                        foreach (System.Text.RegularExpressions.Match m in d)
                        {
                            all &= methods.Contains(m.Groups[1].Value);
                            if (!methods.Contains(m.Groups[1].Value))
                            {
                                unknownAt = System.IO.Path.GetFileName(file) + ": _d." + m.Groups[1].Value;
                            }
                        }
                        unknown += all ? 0 : 1;
                        toDirector++;
                    }
                    else if (game.IsMatch(call))
                    {
                        toGame++;
                    }
                    else if (view.IsMatch(call))
                    {
                        toView++;
                    }
                    else
                    {
                        bare++;
                        bareAt = System.IO.Path.GetFileName(file) + ": " + call.Substring(0, Math.Min(90, call.Length));
                    }
                }
            }
        }
        Console.WriteLine("  live actions: " + found + " registered in " + files + " files: " + toDirector + " to a director, " + toView + " to the page's own view, " + toGame + " into the game");
        Check(found >= 80 && bare == 0, "every button, link, switch and step of the live sources names its real target (" + found + " registered, " + bare + " without one: " + bareAt + ")");
        Check(unknown == 0, "every director method a live action calls exists in the director (" + unknown + " not found: " + unknownAt + ")");
    }


    // ------------------------------------------------------------------------------------------------- a vacancy, a re-let, and a refusal that ends in leaving

    private static void EstateVacancies()
    {
        EstateClimate c = Climate();
        c.DaysPerYear = 360;
        // Rents well over the market's, nothing answered: the tenants give notice and go, and each empty unit is let again to someone of the pool.
        int quits = 0, relets = 0, named = 0;
        bool distinct = true, fresh = true;
        for (int seed = 1; seed <= 6; seed++)
        {
            EstateRunState r = RunEstateNew(seed, 5, c, 20);
            var first = new Dictionary<string, int>();
            for (int day = 2; day <= 120; day++)
            {
                RunEstateDay(r, day, c, false, false);
                if (day == 2)
                {
                    for (int h = 0; h < r.S.Holdings.Count; h++)
                    {
                        for (int u = 0; u < r.S.Holdings[h].Units.Count; u++)
                        {
                            first[h + ":" + u] = r.S.Holdings[h].Units[u].PersonId;
                        }
                    }
                }
            }
            quits += r.Quit;
            relets += r.Relet;
            named += r.Named;
            var seen = new HashSet<int>();
            for (int h = 0; h < r.S.Holdings.Count; h++)
            {
                for (int u = 0; u < r.S.Holdings[h].Units.Count; u++)
                {
                    EstateUnit x = r.S.Holdings[h].Units[u];
                    if (x.Let)
                    {
                        distinct &= x.PersonId > 0 && seen.Add(x.PersonId);
                        fresh &= x.PersonSince <= 120;
                    }
                }
            }
        }
        Console.WriteLine("  estate vacancies: " + quits + " quits, " + relets + " re-lets, " + named + " named");
        Check(quits >= 3, "tenants who are not content give notice and go: " + quits + " left over six cities of a hundred and twenty days");
        Check(relets >= 3 && named == relets, "each empty unit is let again and the new tenant is named at once: " + relets + " re-lets, " + named + " named");
        Check(distinct && fresh, "after the leaving and the letting every let unit has its own person of the pool");

        // The refusal's path: the same cities, every request refused against every request paid for. The refused leave more.
        int refusedQuits = 0, paidQuits = 0, refusals = 0;
        for (int seed = 1; seed <= 30; seed++)
        {
            EstateRunState no = RunEstateNew(seed, 5, c, 14), yes = RunEstateNew(seed, 5, c, 14);
            for (int day = 2; day <= 120; day++)
            {
                RunEstateDay(no, day, c, false, true, 1);
                RunEstateDay(yes, day, c, false, true, 2);
            }
            refusedQuits += no.Quit;
            paidQuits += yes.Quit;
            refusals += no.Refused;
        }
        Check(refusals > 0 && refusedQuits > paidQuits, "a landlord who refuses every request loses more tenants than one who pays for them (" + refusedQuits + " quits against " + paidQuits + ", " + refusals + " refusals)");
    }

    // The city's rents of one save never show in the next: the slot is reset when a city goes.
    private static void EstateRentSlot()
    {
        var slot = new RentSlot();
        RentMapData into = slot.Begin(100);
        into.Clear(100, 30);
        into.Add("Midtown", (int)EstateUse.Homes, 3000, 4000, 40, 1);
        slot.Finish(into);
        Check(slot.Now.DistrictCount == 1 && !slot.Due(100) && slot.Due(101), "the day's read is kept once and is due again tomorrow");
        slot.Reset();
        Check(slot.Now.DistrictCount == 0 && slot.Now.Day == -1 && slot.Due(100), "a city that goes, or another save, empties the rents and makes the same day due again: the second save does not show the first's rents");
        RentMapData again = slot.Begin(100);
        again.Clear(100, 30);
        again.Add("Uptown", (int)EstateUse.Homes, 900, 2000, 10, 0);
        slot.Finish(again);
        Check(slot.Now.DistrictCount == 1 && slot.Now.Districts[0] == "Uptown", "the second city's read stands alone");
    }

    private static void EstateRun()
    {
        EstateClimate c = Climate();
        c.DaysPerYear = 360;
        int monthDays = MacroCatalogue.MonthDays(c.DaysPerYear);

        EstatePressed();
        EstateAnswersPressed();
        EstateVacancies();
        EstateRentSlot();
        EstateHandlers();

        // ---- one city, a hundred and twenty days, a save on day 30 and an old save at the same place
        EstateRunState run = RunEstateNew(11, 4, c);
        EstateRunState other = null;
        string saved = null, savedExtra = null;
        var extraData = new Dictionary<string, string>();
        var oldData = new Dictionary<string, string>();
        EstateRunState oldRun = null;
        bool namedAtOnce = false, namedDistinct = true, jobLoss = false;
        int lostAt = -1, ringDays = 0;
        var seenPeople = new HashSet<int>();
        for (int day = 2; day <= 121; day++)
        {
            if (day == 40)
            {
                // The people's side: the tenants of the first building lose their jobs in the pool.
                for (int u = 0; u < run.S.Holdings[0].Units.Count; u++)
                {
                    int id = run.S.Holdings[0].Units[u].PersonId;
                    for (int p = 0; p < run.Pool.People.Count; p++)
                    {
                        if (run.Pool.People[p].Id == id)
                        {
                            TenantFact f = run.Pool.People[p];
                            f.Working = false;
                            f.PayADay = 0;
                            run.Pool.People[p] = f;
                            jobLoss = true;
                        }
                    }
                }
                lostAt = day;
            }
            RunEstateDay(run, day, c, true, true);
            if (day == 2)
            {
                bool all = true;
                for (int h = 0; h < run.S.Holdings.Count; h++)
                {
                    for (int u = 0; u < run.S.Holdings[h].Units.Count; u++)
                    {
                        EstateUnit x = run.S.Holdings[h].Units[u];
                        all &= x.Let && x.PersonId > 0 && x.PersonSince == 2;
                        namedDistinct &= seenPeople.Add(x.PersonId);
                    }
                }
                namedAtOnce = all;
            }
            if (day % monthDays == 0)
            {
                EstateDay.Ring(run.S, RunCity(run.S, day, monthDays));
                ringDays++;
            }
            if (day == 30)
            {
                saved = EstateStore.Write(run.S);
                EstateExtraStore.Write(run.S, extraData);
                savedExtra = string.Join("~", new[] { extraData[EstateExtraStore.TenantsKey], extraData[EstateExtraStore.RentRingKey] });
                // Two readers of the same save: one with the new lines and one from before them.
                int version;
                other = new EstateRunState { S = EstateStore.Read(saved, out version), Pool = run.Pool, Cash = run.Cash };
                EstateExtraStore.Read(other.S, extraData);
                RunGameRefresh(run.S, other.S);
                string d0 = EstateDigest(run, false), d1 = EstateDigest(other, false);
                int k0 = 0;
                while (k0 < d0.Length && k0 < d1.Length && d0[k0] == d1[k0])
                {
                    k0++;
                }
                Check(d0 == d1, "a save written on day 30 reads back to the state it was written from" + (d0 == d1 ? string.Empty : " (they part at character " + k0 + ": " + d0.Substring(Math.Max(0, k0 - 40), Math.Min(100, d0.Length - Math.Max(0, k0 - 40))) + " / " + d1.Substring(Math.Max(0, k0 - 40), Math.Min(100, d1.Length - Math.Max(0, k0 - 40))) + ")"));
                oldData = new Dictionary<string, string>();
                oldRun = new EstateRunState { S = EstateStore.Read(saved, out version), Pool = run.Pool, Cash = run.Cash };
                EstateExtraStore.Read(oldRun.S, oldData);
                RunGameRefresh(run.S, oldRun.S);
            }
            else if (day > 30 && day <= 60 && other != null)
            {
                RunEstateDay(other, day, c, true, true);
                if (day % monthDays == 0)
                {
                    EstateDay.Ring(other.S, RunCity(other.S, day, monthDays));
                }
                string a = EstateDigest(run), b = EstateDigest(other);
                if (a != b)
                {
                    Check(false, "a save written on day 30 plays the same as the city it was written in: they part on day " + day + "\n  " + a.Substring(0, Math.Min(260, a.Length)) + "\n  " + b.Substring(0, Math.Min(260, b.Length)));
                    other = null;
                }
                else if (day == 60)
                {
                    Check(true, "a save written on day 30 plays the next thirty days exactly as the city it was written in (named tenants, records, requests, services, the ring and the marks; " + a.Length + " characters of state compared every day)");
                }
            }
            if (oldRun != null && day > 30 && day <= 40)
            {
                RunEstateDay(oldRun, day, c, true, true);
                if (day == 31)
                {
                    int unnamed = 0, named = 0;
                    for (int h = 0; h < oldRun.S.Holdings.Count; h++)
                    {
                        for (int u = 0; u < oldRun.S.Holdings[h].Units.Count; u++)
                        {
                            EstateUnit x = oldRun.S.Holdings[h].Units[u];
                            named += x.Let && x.PersonId > 0 ? 1 : 0;
                            unnamed += x.Let && x.PersonId <= 0 ? 1 : 0;
                        }
                    }
                    Check(oldRun.S.RentRingCount == 0 && unnamed == 0 && named > 0, "a save from before the features is loaded with no tenants named and no ring, and is named in its first morning (" + named + " tenants, none left unnamed)");
                }
            }
        }
        Console.WriteLine("  estate run: " + run.Asked + " asks, " + run.Accepted + " accepted, " + run.Refused + " refused, " + run.Lapsed + " lapsed, " + run.CallsOfKind1 + " calls of the first kind, " + run.Missed + " missed days, " + run.Relet + " relets, " + run.Quit + " quits, ring " + run.S.RentRingCount);
        Check(namedAtOnce && namedDistinct, "every let unit has its own person of the people pool from the first morning, with the day they came");
        Check(jobLoss && lostAt == 40, "the people's side acts: the tenants of the first building lose their jobs in the pool on day 40");
        int strained = 0, namedNow = 0;
        for (int u = 0; u < run.S.Holdings[0].Units.Count; u++)
        {
            EstateUnit x = run.S.Holdings[0].Units[u];
            strained += x.Let && x.Strain >= EstateCatalogue.TenantStrainJob ? 1 : 0;
            namedNow += x.Let && x.PersonId > 0 ? 1 : 0;
        }
        Check(strained > 0, "a tenant out of work is under the job's strain on the rent (" + strained + " of " + namedNow + " tenants of the first building)");
        Check(run.Done >= 4 && run.Cash != 0, "booked servicing is done and paid on its day: " + run.Done + " visits over a hundred and twenty days");
        Check(run.Inspected >= 2 && run.S.Holdings[0].LastInspectDay > 0, "the city inspects a let building and the day and result are kept: " + run.Inspected + " inspections, " + run.Passed + " passed, the last on day " + run.S.Holdings[0].LastInspectDay);
        Check(run.Asked >= 2, "a tenant who is not content asks for an improvement: " + run.Asked + " requests");
        Check(run.Accepted >= 1 && run.Refused >= 1, "a request is answered both ways (" + run.Accepted + " paid for, " + run.Refused + " refused)");
        Check(run.Lapsed >= 1, "a request nobody answers lapses (" + run.Lapsed + ")");
        Check(run.S.RentRingCount == ringDays && ringDays == 4 && run.S.Marks.Count == 2, "the city's rent ring has a month for each month of the run (" + run.S.RentRingCount + ") and the two other landlords are marked");
        int change = RentRingRules.Change(run.S), r1 = RentRingRules.LandlordChange(run.S, "rival:1"), r2 = RentRingRules.LandlordChange(run.S, "rival:2");
        Check(change > 0 && r1 > 0 && r2 < 0, "the rents the other landlords ask move with their own pace: the city " + change + " per thousand, the first landlord " + r1 + ", the second " + r2);
        bool recordsOk = true, filled = false, lateSeen = false;
        for (int h = 0; h < run.S.Holdings.Count; h++)
        {
            for (int u = 0; u < run.S.Holdings[h].Units.Count; u++)
            {
                EstateUnit x = run.S.Holdings[h].Units[u];
                recordsOk &= x.PaymentCount <= EstateCatalogue.PaymentsKept;
                for (int p = 1; p < x.PaymentCount; p++)
                {
                    recordsOk &= x.Payments[p].Due <= x.Payments[p - 1].Due;
                }
                filled |= x.PaymentCount >= 3;
                for (int p = 0; p < x.PaymentCount; p++)
                {
                    lateSeen |= x.Payments[p].State != (byte)PaymentState.OnTime;
                }
            }
        }
        Check(recordsOk && filled, "a unit's rent record fills month by month and keeps the last " + EstateCatalogue.PaymentsKept + ", the newest first");
        Check(lateSeen && run.Missed > 0, "...and shows the months paid late or not at all (" + run.Missed + " missed days in the run)");

        // ---- booked servicing keeps a kind of fault down: the same city, with and without
        int withCalls = 0, withoutCalls = 0;
        for (int seed = 1; seed <= 30; seed++)
        {
            EstateRunState a = RunEstateNew(seed, 6, c), b = RunEstateNew(seed, 6, c);
            for (int day = 2; day <= 121; day++)
            {
                RunEstateDay(a, day, c, true, true);
                RunEstateDay(b, day, c, false, true);
            }
            withCalls += a.CallsOfKind1;
            withoutCalls += b.CallsOfKind1;
        }
        Check(withoutCalls > 0 && withCalls < withoutCalls, "a building serviced for a kind of fault has fewer calls of it than one that is not (" + withCalls + " against " + withoutCalls + " over thirty cities)");

        // ---- the ring cap: a year and a half of months turns the ring over, never past twelve
        var s2 = new EstateState { Seed = 3 };
        for (int m = 1; m <= 40; m++)
        {
            EstateDay.Ring(s2, RunCity(s2, m * monthDays, monthDays));
        }
        Check(s2.RentRingCount == EstateCatalogue.RentRingKept && s2.RentRingMonth == 40 && !EstateDay.Ring(s2, RunCity(s2, 40 * monthDays, monthDays)), "the rent ring keeps twelve months and a month already written is not written twice");
    }
}
