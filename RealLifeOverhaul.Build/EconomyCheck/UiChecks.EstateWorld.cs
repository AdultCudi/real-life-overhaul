using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Ui;

// One world, read the same way on every tab. Every state of the Property app and the Car Showroom is laid out and the figures one page puts in its head or its side are held
// against the rows it draws and against the other tabs that say the same thing: OWED TO YOU is the sum of the owed rows, PLANS BEHIND the number of behind rows, WARRANTIES PAID
// OUT the sum of the warranty rows, the reports' week the sum of the growth and showrooms charts' last seven days, the cars on the lots the showrooms' count, the profit chart's
// last day the margin today, the mean satisfaction the mean of its rows, the visitors of the event chart the showrooms' visitors; and no building is for sale and held, nor a
// hotel for sale and run. A head and its rows drawn from two sources fail here, whatever page draws them.
internal static partial class Program
{
    private static readonly Regex Digits = new Regex(@"-?\d[\d,]*(\.\d+)?", RegexOptions.CultureInvariant);

    private static long Num(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return 0;
        }
        Match m = Digits.Match(s.Replace("$", string.Empty));
        return m.Success ? (long)Math.Round(double.Parse(m.Value.Replace(",", string.Empty), CultureInfo.InvariantCulture)) : 0;
    }

    private static double Dec(string s)
    {
        Match m = Digits.Match(s ?? string.Empty);
        return m.Success ? double.Parse(m.Value.Replace(",", string.Empty), CultureInfo.InvariantCulture) : 0;
    }

    private static bool HasDigit(string s)
    {
        foreach (char c in s ?? string.Empty)
        {
            if (char.IsDigit(c))
            {
                return true;
            }
        }
        return false;
    }

    private static string WorldFault = "none";

    private static void Same(bool ok, string what, long a, long b)
    {
        if (!ok && WorldFault == "none")
        {
            WorldFault = what + " (" + a + " against " + b + ")";
        }
        WorldChecks++;
        WorldFailed += ok ? 0 : 1;
        if (!ok)
        {
            Check(false, "world: " + what + " (" + a + " against " + b + ")");
        }
    }

    private static int WorldChecks, WorldFailed;

    private static void Equal(string what, long a, long b) => Same(a == b, what, a, b);

    private static void Near(string what, double a, double b, double within) => Same(Math.Abs(a - b) <= within, what, (long)Math.Round(a * 10), (long)Math.Round(b * 10));

    private static UiWords EnWords()
    {
        Dictionary<string, string> en = LocaleFile("en");
        return (key, english) => en.TryGetValue(key, out string said) ? said : english;
    }

    private static string En(string key) => LocaleFile("en").TryGetValue(key, out string said) ? said : key;

    private static EstateModel PM(string state) => UiSamples.Estate(EnWords(), state);

    private static EstateModel DM(string state) => UiSamples.Dealer(EnWords(), state);

    private static long SumCol(UiDataRow[] rows, int col, Func<UiDataRow, bool> take)
    {
        long sum = 0;
        foreach (UiDataRow r in rows)
        {
            sum += take(r) ? Num(r.Cells[col]) : 0;
        }
        return sum;
    }

    private static int CountRows(UiDataRow[] rows, Func<UiDataRow, bool> take)
    {
        int n = 0;
        foreach (UiDataRow r in rows)
        {
            n += take(r) ? 1 : 0;
        }
        return n;
    }

    private static long LastPoint(UiChartModel chart) => chart == null || chart.Series.Length == 0 ? 0 : (long)Math.Round(chart.Series[0].Values[chart.Series[0].Values.Length - 1]);

    private static long SumLast(UiChartModel chart, int days)
    {
        long sum = 0;
        float[] v = chart.Series[0].Values;
        for (int i = Math.Max(0, v.Length - days); i < v.Length; i++)
        {
            sum += (long)Math.Round(v[i]);
        }
        return sum;
    }

    private static long Fig(EstateModel m, int at) => Num(m.FigValues[at]);

    private static UiFactLine SideRow(EstateModel m, string label)
    {
        foreach (UiFactLine line in m.Side.Lines)
        {
            if (line.Label == label)
            {
                return line;
            }
        }
        return default(UiFactLine);
    }

    private static long SideNum(EstateModel m, string key) => Num(SideRow(m, En(key)).Value);

    private static void WorldChecksRun()
    {
        WorldChecks = 0;
        WorldFailed = 0;
        PropertyWorldChecks();
        ShowroomWorldChecks();
        Check(WorldChecks > 120, "world: the figures of both apps held against their rows (" + WorldChecks + " comparisons)");
        Check(WorldFailed == 0, "world: every head, count and chart agrees with its rows and with the other tabs (" + WorldFailed + " differ)");
    }

    // ------------------------------------------------------------------------------------------------- Property

    private static void PropertyWorldChecks()
    {
        string noBody = En("reallife_estate_row_nobody"), applicants = En("reallife_estate_filter_applicants");
        foreach (string roll in new[] { "roll", "roll_twenty", "roll_first", "roll_empty" })
        {
            EstateModel m = PM(roll);
            UiDataRow[] rows = m.Table.Rows;
            Func<UiDataRow, bool> letRow = r => r.Tones[3] == UiRole.None && r.Cells[3] != "-" && r.Cells[3].Length > 0;
            Equal(roll + ": DUE is the sum of the rents a month the roll's let rows show", Fig(m, 0), SumCol(rows, 3, letRow));
            if (rows.Length > 0)
            {
                Equal(roll + ": the roll's total row is DUE", Num(m.Table.Totals[3]), Fig(m, 0));
                Equal(roll + ": the total row counts the let rows", Num(m.Table.TotalsLabel), CountRows(rows, letRow));
            }
            Equal(roll + ": EMPTY, ASKING is the sum of the empty rows' asking rents", Fig(m, 3), SumCol(rows, 3, r => r.Values[1] == UiDataTable.GlyphFace(UiIcon.People)));
            long owed = 0;
            foreach (UiDataRow r in rows)
            {
                // A late row says how many days: the rent of those days is owed (a month's rent is thirty days of it).
                owed += letRow(r) && HasDigit(r.Cells[4]) ? Num(r.Cells[3]) / UiSamplesMonthDays * Num(r.Cells[4]) : 0;
            }
            Equal(roll + ": IN ARREARS is the rent of the days the late rows are behind", Fig(m, 2), owed);
            Equal(roll + ": COLLECTED is DUE less IN ARREARS", Fig(m, 1), Fig(m, 0) - Fig(m, 2));
            if (roll == "roll")
            {
                // The repair list names a fault by its bare noun, and the landlords are the city's: a short list, never the same landlord twice.
                Equal("roll: no landlord stands twice", m.ListB.Length, DistinctTitles(m.ListB));
            }
        }
        EstateModel rollStd = PM("roll"), tenants = PM("tenants"), works = PM("works"), market = PM("market"), hotel = PM("hotel"), hire = PM("hire"), agency = PM("agency"), rents = PM("rents");
        UiDataRow[] tenantRows = tenants.Table.Rows;
        Func<UiDataRow, bool> unitLet = r => r.Values[1] != UiDataTable.GlyphFace(UiIcon.People) && r.Cells[4] != applicants;
        Equal("tenants: LET is the let rows", Fig(tenants, 0), CountRows(tenantRows, unitLet));
        Equal("tenants: EMPTY is the empty rows", Fig(tenants, 1), CountRows(tenantRows, r => r.Cells[1] == noBody));
        long tenantOwed = 0;
        double happySum = 0;
        int happyCount = 0;
        foreach (UiDataRow r in tenantRows)
        {
            tenantOwed += unitLet(r) && HasDigit(r.Cells[4]) ? Num(r.Cells[2]) / UiSamplesMonthDays * Num(r.Cells[4]) : 0;
            if (unitLet(r) && r.Values[5] >= 0)
            {
                happySum += r.Values[5];
                happyCount++;
            }
        }
        Equal("tenants: RENT OWED TO YOU is the rent of the days the late rows are behind", Fig(tenants, 2), tenantOwed);
        Near("tenants: MEAN SATISFACTION is the mean of the satisfaction its rows show", Dec(tenants.FigValues[3]), happySum / Math.Max(1, happyCount), 1.0);
        Equal("tenants: the late payers listed are the late rows", tenants.Related.Length, CountRows(tenantRows, r => unitLet(r) && HasDigit(r.Cells[4])));
        Equal("tenants: the roll and the tenants agree on the owed", Fig(rollStd, 2), Fig(tenants, 2));
        Equal("tenants: the sidebar counts the let units", Num(rollStd.Nav[1].Count), Fig(tenants, 0));
        Equal("tenants: the roll's total row counts the let units", Num(rollStd.Table.TotalsLabel), Fig(tenants, 0));
        // Works: the heads count the rows.
        UiDataRow[] workRows = works.Table.Rows;
        Func<UiDataRow, bool> callRow = r => r.Values[0] == (int)UiIcon.Wrench;
        Equal("works: OPEN CALLS and BOOKED are the call rows", Fig(works, 0) + Fig(works, 1), CountRows(workRows, callRow));
        Equal("works: BOOKED is the call rows that say booked", Fig(works, 1), CountRows(workRows, r => callRow(r) && r.Cells[3] == En("reallife_ibox_chip_booked")));
        Equal("works: CALLS COST is the call rows' costs", Fig(works, 3), SumCol(workRows, 4, callRow));
        Equal("works: UNFIT TO LET is the unfit rows", Fig(works, 2), CountRows(workRows, r => r.Cells[3] == En("reallife_ui_estate_unfit")));
        Equal("works: the heading counts the rows", Num(works.HeadTitle), workRows.Length);
        Equal("works: the sidebar counts the units with a call", Num(rollStd.Nav[2].Count), CountRows(workRows, callRow));
        foreach (UiDataRow r in workRows)
        {
            if (callRow(r))
            {
                Same(!r.Cells[1].StartsWith("The ", StringComparison.Ordinal) && !r.Cells[1].StartsWith("A ", StringComparison.Ordinal) && !r.Cells[1].StartsWith("An ", StringComparison.Ordinal),
                    "works: the WHAT column names the fault by its bare noun (" + r.Cells[1] + ")", 0, 0);
            }
        }
        // The market: held buildings are never for sale, hotels you run are never for sale, the donut is the table.
        var held = new HashSet<string>();
        foreach (UiDataRow r in rollStd.Table.Rows)
        {
            held.Add(r.Cells[0]);
        }
        foreach (UiDataRow r in tenantRows)
        {
            held.Add(r.Cells[0]);
        }
        foreach (UiDataRow r in workRows)
        {
            held.Add(r.Cells[0]);
        }
        foreach (UiDataRow r in hotel.Table.Rows)
        {
            held.Add(r.Cells[0]);
        }
        int bothWays = 0;
        foreach (UiDataRow r in market.Table.Rows)
        {
            bothWays += held.Contains(r.Cells[0]) ? 1 : 0;
        }
        Equal("market: no building or hotel is for sale and held or run by you", bothWays, 0);
        Equal("market: the heading counts the listings", Num(market.HeadTitle), market.Table.Rows.Length);
        EstateModel rentsView = rents;
        int forSale = market.Table.Rows.Length, homes = CountRows(market.Table.Rows, r => r.Values[0] == (int)UiIcon.Home), shops = CountRows(market.Table.Rows, r => r.Values[0] == (int)UiIcon.Store),
            offices = CountRows(market.Table.Rows, r => r.Values[0] == (int)UiIcon.Briefcase), hotels = CountRows(market.Table.Rows, r => r.Values[0] == (int)UiIcon.Building);
        Equal("rents: the donut counts the market's listings", Num(rentsView.Rents.ChartNote), forSale);
        int[] counts = { homes, shops, offices, hotels };
        for (int i = 0; i < 4; i++)
        {
            Equal("rents: the donut's " + i + " share is its kind's share of the market table", Num(rentsView.Rents.ChartValues[i]), (long)Math.Round(counts[i] * 100.0 / Math.Max(1, forSale)));
        }
        Equal("rents: the mean flat rent is the roll's chart last month", Num(rentsView.Rents.Rows[0].Value), LastPoint(rollStd.Lower));
        Equal("rents: YOUR UNITS, LET is the tenants' let", Num(rentsView.Rents.Rows[6].Value), Fig(tenants, 0));
        Equal("rents: of all the units you let yourself", Num(rentsView.Rents.Rows[6].Value.Substring(rentsView.Rents.Rows[6].Value.IndexOf(' '))), Fig(tenants, 0) + Fig(tenants, 1));
        Equal("rents: YOUR MEAN RENT is DUE over the let units", Num(rentsView.Rents.Rows[7].Value), Fig(rollStd, 0) / Math.Max(1, Fig(tenants, 0)));
        // The listing page says what the table says, in the same unit.
        EstateModel listing = PM("listing");
        UiDataRow listed = FindRow(market.Table.Rows, listing.DetailTitle);
        Same(listed.Cells != null, "listing: the page is of a listing the market table holds (" + listing.DetailTitle + ")", 0, 0);
        if (listed.Cells != null)
        {
            Equal("listing: RENT A MONTH is the table's", Num(listing.Left[0].Lines[0].Value), Num(listed.Cells[3]));
            Equal("listing: the yield a year is the table's", (long)Math.Round(Dec(listing.Left[0].Lines[1].Value) * 10), (long)Math.Round(Dec(listed.Cells[4]) * 10));
            Equal("listing: the asking price is the table's", Num(listing.Strip[0].Value), Num(listed.Cells[1]));
            Equal("listing: the condition is the table's", listing.Left[0].Lines[2].Fill, listed.Values[2]);
        }
        int alsoHeld = 0, alsoMissing = 0;
        foreach (EstateRow r in listing.Related2)
        {
            alsoHeld += held.Contains(r.Title) ? 1 : 0;
            alsoMissing += FindRow(market.Table.Rows, r.Title).Cells == null ? 1 : 0;
        }
        Equal("listing: the other buildings for sale are never held", alsoHeld, 0);
        Equal("listing: the other buildings for sale are in the market table", alsoMissing, 0);
        // Hotels: the heads add up their rows, the rates and the guests' words are two to a hotel, the roll lists the hotels.
        Equal("hotel: TAKINGS is the rows' takings", Num(hotel.Strip[1].Value), SumCol(hotel.Table.Rows, 5, r => true));
        Equal("hotel: COSTS is the rows' costs", Num(hotel.Strip[2].Value), SumCol(hotel.Table.Rows, 6, r => true));
        Equal("hotel: two rates to a hotel", hotel.Related.Length, hotel.Table.Rows.Length * 2);
        Equal("hotel: as many guests' words as rates", hotel.Related2.Length, hotel.Related.Length);
        Equal("hotel: the sidebar counts the hotels", Num(rollStd.Nav[5].Count), hotel.Table.Rows.Length);
        int rollHotels = 0;
        foreach (UiDataRow r in hotel.Table.Rows)
        {
            rollHotels += FindRow(rollStd.Table.Rows, r.Cells[0]).Cells != null ? 1 : 0;
        }
        Equal("hotel: the roll lists every hotel you run", rollHotels, hotel.Table.Rows.Length);
        var starsSeen = new HashSet<int>();
        foreach (EstateRow review in hotel.Related2)
        {
            starsSeen.Add(review.Stars);
            bool complaint = review.Title.Contains("too dear") || review.Title.Contains("not cleaned") || review.Title.Contains("nobody at the desk");
            Same(review.Stars > 0 && (complaint ? review.Stars <= 4 : review.Stars >= 8), "hotel: a guest's stars follow what they wrote (" + review.Title + ", " + review.Stars + ")", review.Stars, 0);
        }
        Same(starsSeen.Count > 1, "hotel: the guests do not all give the same stars", starsSeen.Count, 2);
        // Car hire.
        UiDataRow[] fleet = hire.Table.Rows;
        Equal("hire: CARS OUT is the cars that say out", Num(hire.Strip[0].Value), CountRows(fleet, r => r.Cells[1] == En("reallife_estate_car_out")));
        Equal("hire: HIRES is the rows' hires", Num(hire.Strip[1].Value), SumCol(fleet, 3, r => true));
        Equal("hire: TAKINGS is the rows' takings", Num(hire.Strip[2].Value), SumCol(fleet, 4, r => true));
        Equal("hire: MEAN RATE is the rows' mean", Num(hire.Strip[3].Value), SumCol(fleet, 2, r => true) / Math.Max(1, fleet.Length));
        Equal("hire: takings this year are the strip's takings", SideNum(hire, "reallife_ui_estate_fig_year_takings"), Num(hire.Strip[2].Value));
        Equal("hire: kept this year is takings less costs", SideNum(hire, "reallife_ui_estate_hire_kept_year"), SideNum(hire, "reallife_ui_estate_fig_year_takings") - SideNum(hire, "reallife_ui_estate_fig_year_costs"));
        Equal("hire: the sidebar counts the fleet", Num(rollStd.Nav[6].Count), fleet.Length);
        // The agency: the commission strip adds the rows; the offices it may work from are not let to a tenant of yours.
        Equal("agency: OPEN INSTRUCTIONS is the rows", Num(agency.Strip[0].Value), agency.Table.Rows.Length);
        Equal("agency: COMMISSION is the rows' commission", Num(agency.Strip[1].Value), SumCol(agency.Table.Rows, 2, r => true));
        Equal("agency: the sidebar counts the instructions", Num(rollStd.Nav[4].Count), agency.Table.Rows.Length);
        int officesLet = 0;
        foreach (EstateRow office in agency.Related)
        {
            foreach (UiDataRow r in tenantRows)
            {
                officesLet += r.Cells[0].StartsWith(office.Title, StringComparison.Ordinal) ? 1 : 0;
            }
        }
        Equal("agency: no office you hold for the agency is let to a tenant", officesLet, 0);
        // One unit, the same on the roll, the tenants, the building and its own page.
        foreach (string state in new[] { "unit_late", "unit_request", "unit_empty" })
        {
            EstateModel page = PM(state);
            EstateUnitPage u = page.Unit;
            UiDataRow onRoll = FindRow(rollStd.Table.Rows, u.Title);
            Same(onRoll.Cells != null, state + ": the unit stands on the roll (" + u.Title + ")", 0, 0);
            if (onRoll.Cells == null)
            {
                continue;
            }
            Equal(state + ": the page's rent is the roll's", Num(u.Rent), Num(onRoll.Cells[3]));
            if (state != "unit_empty")
            {
                Same(u.TenantName == onRoll.Cells[1], state + ": the tenant is the roll's (" + u.TenantName + ")", 0, 0);
                Equal(state + ": the page's satisfaction is the roll's", u.MeterValues[0], onRoll.Values[5]);
                Equal(state + ": the lease ends as the roll says", Num(u.FactValues[2]), Num(onRoll.Cells[2]));
            }
            if (state == "unit_late")
            {
                long perDay = Num(u.Rent) / UiSamplesMonthDays;
                List<long> head = Nums(u.NeedHead), body = Nums(u.NeedBody);
                long daysLate = head[0], amount = head[1];
                Equal("unit_late: what is owed is the rent of the days it is late", amount, perDay * daysLate);
                Equal("unit_late: the body owes the same amount", body[0], amount);
                Equal("unit_late: the days of rent in the body are the days late", body[1], daysLate);
                Equal("unit_late: the roll's chip says the same days", Num(onRoll.Cells[4]), daysLate);
                Equal("unit_late: the record's first row owes the amount", Num(u.Record[0].Amount), amount);
            }
            if (state == "unit_empty")
            {
                UiDataRow tenantRow = FindRow(tenantRows, u.Title);
                Same(tenantRow.Cells != null, state + ": the tenants page lists the empty unit", 0, 0);
                EstateModel applicant = PM("applicant");
                Equal(state + ": the market rent on the applicant's card is the unit page's", Num(applicant.Decision.FactValues[1]), Num(u.Strip[1].Value));
                Equal(state + ": the applicant's offer is the first applicant row's", Num(applicant.Decision.FactValues[0]), Num(u.Applicants[0].Amount));
                Equal(state + ": the applicant's offer stands until the first applicant row's day", Nums(applicant.Decision.Body)[0], Num(u.Applicants[0].Sub));
                Equal(state + ": the applicant's share of the market is offer over market", Num(applicant.Decision.FactValues[2]), (long)Math.Round(Num(applicant.Decision.FactValues[0]) * 100.0 / Num(applicant.Decision.FactValues[1])));
                Equal(state + ": the applicants listed are the tenants page's applicant rows", u.Applicants.Length, CountRows(tenantRows, r => r.Cells[4] == applicants));
                Equal(state + ": the asking rent in the card is the strip's asking", Num(u.Empty.Lines[0].Hint.Substring(u.Empty.Lines[0].Hint.IndexOf('$'))), Num(u.Strip[0].Value));
            }
        }
        EstateModel building = PM("building");
        int unitRows = 0;
        double sat = 0;
        int satCount = 0;
        foreach (UiDataRow r in tenantRows)
        {
            if (r.Cells[0].StartsWith(building.DetailTitle, StringComparison.Ordinal) && unitLet(r) && r.Values[5] >= 0)
            {
                sat += r.Values[5];
                satCount++;
            }
            unitRows += r.Cells[0].StartsWith(building.DetailTitle, StringComparison.Ordinal) && r.Cells[4] != applicants ? 1 : 0;
        }
        Near("building: TENANTS' SATISFACTION is the mean of its units on the tenants page", building.ShareValues[1], sat / Math.Max(1, satCount), 1.0);
        Equal("building: the flats listed are its units on the tenants page", building.Related.Length, unitRows);
        EstateModel worksPage = PM("works_page");
        Equal("works_page: the calls listed are the works table's call rows of the building", worksPage.Related.Length, CountRows(workRows, r => callRow(r) && r.Cells[0].StartsWith(worksPage.DetailTitle, StringComparison.Ordinal)));
        Equal("works_page: BOOKED is the visits already booked, which stand as disabled buttons", Num(worksPage.Strip[3].Value), worksPage.Grid.Length - CountEnabled(worksPage.Grid));
        EstateModel call = PM("call");
        UiDataRow callWork = FindRow(workRows, call.Decision.Kicker.Substring(call.Decision.Kicker.IndexOf(',') + 2));
        Same(callWork.Cells != null, "call: the call stands in the works table", 0, 0);
        if (callWork.Cells != null)
        {
            Equal("call: the contractor's quote is the table's cost", Num(call.Decision.FactValues[0]), Num(callWork.Cells[4]));
            Equal("call: reported days ago is today less the table's day", Num(call.Decision.FactValues[2]), UiSamplesToday - Num(callWork.Cells[5]));
        }
        // The first-state worlds hold their own: the single unit's roll is its tenants page too.
        EstateModel first = PM("roll_first");
        Equal("roll_first: the single unit is let and its rent is due", Fig(first, 0), Num(first.Table.Rows[0].Cells[3]));
    }

    private const int UiSamplesMonthDays = 30, UiSamplesToday = 412;

    private static int CountEnabled(UiActionModel[] actions)
    {
        int n = 0;
        foreach (UiActionModel a in actions)
        {
            n += a.Disabled ? 0 : 1;
        }
        return n;
    }

    private static int DistinctTitles(EstateRow[] rows)
    {
        var seen = new HashSet<string>();
        foreach (EstateRow r in rows)
        {
            seen.Add(r.Title);
        }
        return seen.Count;
    }

    private static UiDataRow FindRow(UiDataRow[] rows, string name)
    {
        foreach (UiDataRow r in rows)
        {
            if (r.Cells[0] == name)
            {
                return r;
            }
        }
        return default(UiDataRow);
    }

    // ------------------------------------------------------------------------------------------------- the Car Showroom

    private static void ShowroomWorldChecks()
    {
        EstateModel lots = DM("lots"), stock = DM("stock"), market = DM("market"), offers = DM("offers"), finance = DM("finance"), growth = DM("growth"), reports = DM("reports"), yard = DM("yard"),
            eventPage = DM("event"), auction = DM("auction"), used = DM("used"), deal = DM("deal");
        UiDataRow[] lotRows = lots.Table.Rows;
        Equal("lots: CARS is the sum of the cars on the showrooms' lots", Fig(lots, 0), SumCol(lotRows, 1, r => true));
        Equal("lots: SOLD TODAY is the rows' sold", Fig(lots, 1), SumCol(lotRows, 4, r => true));
        Equal("lots: TAKINGS TODAY is the rows' takings", Fig(lots, 2), SumCol(lotRows, 5, r => true));
        Equal("lots: OFFERS WAITING is the rows' offers", Fig(lots, 3), SumCol(lotRows, 6, r => true));
        Equal("lots: the chart under them ends on the day's takings", LastPoint(lots.Lower), Fig(lots, 2));
        Equal("lots: the sidebar counts the showrooms", Num(lots.Nav[0].Count), lotRows.Length);
        // Stock: the cars on the lots, as many as the lots say, every one different in its place.
        Equal("stock: the cards are the cars on the lots", stock.Catalogue.Cards.Length, Fig(lots, 0));
        Equal("stock: the sidebar counts the cars", Num(stock.Nav[1].Count), stock.Catalogue.Cards.Length);
        Equal("stock: the head counts the cards", Num(stock.HeadSub), stock.Catalogue.Cards.Length);
        // Offers: the head counts the waiting rows, the best offer is the best of them, the sold today and its margin are the sold rows' of today.
        UiDataRow[] offerRows = offers.Table.Rows;
        Func<UiDataRow, bool> waiting = r => r.Values[4] >= 0;
        Equal("offers: WAITING FOR YOU is the waiting rows", Fig(offers, 0), CountRows(offerRows, waiting));
        long best = 0;
        foreach (UiDataRow r in offerRows)
        {
            best = waiting(r) ? Math.Max(best, Num(r.Cells[2])) : best;
        }
        Equal("offers: BEST OFFER is the best of the waiting rows", Fig(offers, 1), best);
        Func<UiDataRow, bool> soldToday = r => !waiting(r) && Num(r.Cells[5]) == UiSamplesToday;
        Equal("offers: SOLD TODAY is the sold rows of today", Fig(offers, 2), CountRows(offerRows, soldToday));
        long margin = 0;
        foreach (UiDataRow r in offerRows)
        {
            margin += soldToday(r) ? Num(r.Cells[6]) : 0;
        }
        Equal("offers: MARGIN TODAY is the margins of the sold rows of today", Fig(offers, 3), margin);
        Equal("offers: SOLD TODAY is the showrooms' count", Fig(offers, 2), Fig(lots, 1));
        Equal("offers: the sidebar counts the waiting offers", Num(offers.Nav[3].Count), CountRows(offerRows, waiting));
        Equal("offers: the showrooms' offers are the waiting rows", Fig(lots, 3), CountRows(offerRows, waiting));
        foreach (UiDataRow r in offerRows)
        {
            if (!waiting(r))
            {
                Same(Num(r.Cells[2]) > 0 && r.Cells[3].Length == 0, "offers: a sold row gives its price and no asking price (" + r.Cells[1] + ")", 0, 0);
            }
        }
        // The rivals' asks against ours: over or under yours is the gap to the stock card's price of that car.
        foreach (EstateRow rival in offers.Related)
        {
            long ours = 0;
            foreach (UiCatalogueCard card in stock.Catalogue.Cards)
            {
                ours = card.Name == rival.Title ? Num(card.Figure) : ours;
            }
            long gap = Num(rival.Chip);
            bool over = rival.Chip.Contains("over");
            Equal("offers: " + rival.Title + " asks as far from the stock's price as the chip says", Num(rival.Amount) - ours, over ? gap : -gap);
        }
        // Finance: the heads are the plan and warranty rows.
        UiDataRow[] fin = finance.Table.Rows;
        Func<UiDataRow, bool> plan = r => r.Values[1] != (int)UiIcon.ShieldCheck;
        Equal("finance: OWED TO YOU is the plans' owed", Fig(finance, 0), SumCol(fin, 2, plan));
        Equal("finance: PAID IN A WEEK is the plans' weekly sums", Fig(finance, 1), SumCol(fin, 3, plan));
        Equal("finance: PLANS BEHIND is the behind rows", Fig(finance, 2), CountRows(fin, r => plan(r) && r.Tones[5] == UiRole.Danger));
        Equal("finance: WARRANTIES PAID OUT is the warranty rows' sums", Fig(finance, 3), SumCol(fin, 2, r => !plan(r)));
        Equal("finance: the heading counts the plans and the warranties", Num(finance.HeadTitle) * 1000 + Num(finance.HeadTitle.Substring(finance.HeadTitle.IndexOf(',') + 1)), CountRows(fin, plan) * 1000 + CountRows(fin, r => !plan(r)));
        var soonest = new List<long>();
        foreach (UiDataRow r in fin)
        {
            if (!plan(r))
            {
                soonest.Add(Num(r.Cells[4]));
            }
        }
        soonest.Sort();
        for (int i = 0; i < finance.Related.Length; i++)
        {
            Equal("finance: the warranties ending soonest are the earliest ends (" + i + ")", Num(finance.Related[i].Chip), soonest[i]);
        }
        Equal("finance: the shares add up to the buyers", Math.Min(1, finance.ShareValues.Length), 1);
        // Reports: the week and the day against the other tabs, the profit chart's day against the margin.
        Equal("reports: CARS ON THE LOTS is the showrooms' cars", SideNum(reports, "reallife_ui_dealer_fig_cars_lot"), Fig(lots, 0));
        double ages = 0;
        foreach (UiCatalogueCard card in stock.Catalogue.Cards)
        {
            ages += Num(card.State);
        }
        Equal("reports: DAYS ON THE LOT, ON AVERAGE is the stock cards' days", SideNum(reports, "reallife_ui_dealer_fig_age"), (long)((ages + stock.Catalogue.Cards.Length / 2) / stock.Catalogue.Cards.Length));
        Equal("reports: IN ARREARS is the owed of the plans that are behind", SideNum(reports, "reallife_ui_dealer_fig_arrears"), SumColWhere(fin, 2, r => plan(r) && r.Tones[5] == UiRole.Danger));
        Equal("reports: PLANS BEHIND is the finance tab's", SideNum(reports, "reallife_ui_dealer_fig_late"), Fig(finance, 2));
        Equal("reports: the profit chart's last day is the margin today", LastPoint(reports.Chart), Fig(offers, 3));
        Equal("reports: CARS SOLD this week is the growth chart's seven days", SideNum(reports, "reallife_ui_dealer_fig_cars_sold"), SumLast(growth.Lower, 7));
        Equal("reports: REVENUE this week is the showrooms' chart's seven days", SideNum(reports, "reallife_dealer_report_revenue"), SumLast(lots.Lower, 7));
        Equal("growth: the chart ends on the cars sold today", LastPoint(growth.Lower), Fig(growth, 2));
        Equal("growth: SOLD TODAY is the showrooms'", Fig(growth, 2), Fig(lots, 1));
        Equal("event: the visitors chart ends on the showrooms' visitors today", LastPoint(eventPage.Lower), SumCol(lotRows, 3, r => true));
        Equal("yard: the showroom's visitors today are its row's", Num(yard.Strip[0].Value), Num(lotRows[0].Cells[3]));
        Equal("yard: the showroom's sold today is its row's", Num(yard.Strip[3].Value), Num(lotRows[0].Cells[4]));
        List<long> lotNums = Nums(lotRows[0].Cells[1]);
        Equal("yard: the lot's places taken is cars over places", yard.ShareValues[0], lotNums[0] * 100 / Math.Max(1, lotNums[1]));
        // Growth rows: the done missions and the events running.
        UiDataRow[] gr = growth.Table.Rows;
        string[] done = growth.FigValues[1].Split('/');
        Equal("growth: MISSIONS DONE is the rows that say done", Num(done[0]), CountRows(gr, r => r.Values[0] == (int)UiIcon.Trophy && r.Cells[4] == En("reallife_dealer_state_mission_done")));
        Equal("growth: of all the missions the rows hold", Num(done[1]), CountRows(gr, r => r.Values[0] == (int)UiIcon.Trophy));
        Equal("growth: EVENTS ON is the events that are on", Fig(growth, 0), CountRows(gr, r => r.Values[0] == (int)UiIcon.Megaphone && r.Cells[1].Length > 0));
        // The market and the auction: the same lot leads the same way on both.
        UiDataRow[] marketRows = market.Table.Rows;
        Equal("market: NEW is the new rows", Fig(market, 1), CountRows(marketRows, r => r.Cells[1] == En("reallife_dealer_kind_wholesale")));
        Equal("market: USED is the used rows", Fig(market, 2), CountRows(marketRows, r => r.Cells[1] == En("reallife_dealer_kind_used")));
        Equal("market: AT AUCTION is the auction rows", Fig(market, 3), CountRows(marketRows, r => r.Cells[1] == En("reallife_dealer_kind_auction")));
        UiDataRow lot = FindRow(marketRows, auction.Auction.Title);
        Same(lot.Cells != null, "auction: the lot stands in the market table", 0, 0);
        if (lot.Cells != null)
        {
            string held = "none";
            foreach (UiFactLine line in auction.Auction.Right.Lines)
            {
                held = line.Label == En("reallife_ui_dealer_held_by") ? line.Value : held;
            }
            Same(held == lot.Cells[6], "auction: the leader on the lot's page is the market table's (" + held + " against " + lot.Cells[6] + ")", 0, 0);
            Equal("auction: the current bid is the table's price", Num(auction.Auction.Right.Lines[0].Value), Num(lot.Cells[4]));
        }
        foreach (EstateRow other in auction.Related)
        {
            UiDataRow row = FindRow(marketRows, other.Title);
            Same(row.Cells != null && row.Cells[6] == other.Chip && Num(row.Cells[4]) == Num(other.Amount), "auction: the other lot " + other.Title + " leads as the market table says", 0, 0);
        }
        foreach (EstateRow like in used.Related2)
        {
            bool found = false;
            foreach (UiDataRow row in marketRows)
            {
                found |= row.Cells[0] == like.Title && Num(row.Cells[4]) == Num(like.Amount);
            }
            Same(found, "used: the other car " + like.Title + " is on the market at its price", 0, 0);
        }
        // The deal: the offer, the asking price and the cost are the offers table's and the stock's.
        UiDataRow dealRow = FindRowBy(offerRows, 1, deal.Decision.Title);
        Same(dealRow.Cells != null, "deal: the offer stands in the offers table (" + deal.Decision.Title + ")", 0, 0);
        if (dealRow.Cells != null)
        {
            Equal("deal: the offer is the table's", Num(deal.Decision.FactValues[0]), Num(dealRow.Cells[2]));
            Equal("deal: the asking price is the table's", Num(deal.Decision.FactValues[1]), Num(dealRow.Cells[3]));
        }
        Same(stock.Catalogue.DetailLabels.Length > 0, "stock: the chosen car has its detail", 0, 0);
        int costAt = Array.IndexOf(stock.Catalogue.DetailLabels, En("reallife_ui_dealer_l_cost"));
        if (costAt >= 0 && stock.Catalogue.Cards[0].Name == deal.Decision.Title)
        {
            Equal("deal: the cost is the stock detail's", Num(deal.Decision.FactValues[2]), Num(stock.Catalogue.DetailValues[costAt]));
        }
        Equal("deal: the margin at the offer is the offer less the cost", Num(deal.Decision.FactValues[3]), Num(deal.Decision.FactValues[0]) - Num(deal.Decision.FactValues[2]));
        // Every full state keeps its cars apart: no model twice on one lot, and a full lot is full.
        EstateModel full = DM("stock_full");
        var names = new HashSet<string>();
        foreach (UiCatalogueCard card in full.Catalogue.Cards)
        {
            names.Add(card.Name);
        }
        Equal("stock_full: every car of the full lot is a different model", names.Count, full.Catalogue.Cards.Length);
        Equal("stock_empty: no car stands on the empty lots", DM("stock_empty").Catalogue.Cards.Length, 0);
    }

    private static UiDataRow FindRowBy(UiDataRow[] rows, int col, string name)
    {
        foreach (UiDataRow r in rows)
        {
            if (r.Cells[col] == name)
            {
                return r;
            }
        }
        return default(UiDataRow);
    }

    private static List<long> Nums(string s)
    {
        var all = new List<long>();
        foreach (Match m in Digits.Matches((s ?? string.Empty).Replace("$", string.Empty)))
        {
            all.Add((long)Math.Round(double.Parse(m.Value.Replace(",", string.Empty), CultureInfo.InvariantCulture)));
        }
        return all;
    }

    private static long SumColWhere(UiDataRow[] rows, int col, Func<UiDataRow, bool> take) => SumCol(rows, col, take);
}
