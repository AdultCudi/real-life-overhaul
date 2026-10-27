using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Construction;

// Car parks (docs/features/construction.md, "Parking lots"; Scripts/Construction: CarParkRules, CarParkLayout, BuildState.Park,
// BuildStore.Park, ConstructionDirector.Park, BuildMode.Park; Scripts/Civic: SiteKind.Parking and its dressing): the layout of bays on
// every size of site the plan's squares make, the works (a day of plans, a day at City Hall, one to three days of building), the cost
// by area through the programme's own tables, where a car park may stand, the price band, how full it is by the hour, what it takes,
// the leases offered and paid, the city's builders' wish and pick, the store's part and the project's new fields, then the sources
// (nothing a frame, the option, a session rests, the bridges, the tests) and the texts. Each check was made to fail once.
internal static partial class Program
{
    private static void ParkChecks()
    {
        ParkLayoutChecks();
        ParkWorksChecks();
        ParkZoningChecks();
        ParkPriceChecks();
        ParkOccupancyChecks();
        ParkLeaseChecks();
        ParkNpcChecks();
        ParkStoreChecks();
        ParkSiteChecks();
        ParkExtraChecks();
        ParkSourceChecks();
        ParkTextChecks();
    }

    // ------------------------------------------------------------------ the layout

    private static bool Overlap(ParkRect a, ParkRect b, float slack = 0.02f)
    {
        return a.X0 < b.X1 - slack && b.X0 < a.X1 - slack && a.Z0 < b.Z1 - slack && b.Z0 < a.Z1 - slack;
    }

    private static bool Inside(ParkRect r, CarParkLayout l, float slack = 0.02f)
    {
        float hx = l.Width * 0.5f + slack, hz = l.Depth * 0.5f + slack;
        return r.X0 >= -hx && r.X1 <= hx && r.Z0 >= -hz && r.Z1 <= hz;
    }

    private static bool InsideAt(float x, float z, CarParkLayout l)
    {
        return Math.Abs(x) <= l.Width * 0.5f + 0.02f && Math.Abs(z) <= l.Depth * 0.5f + 0.02f;
    }

    // Everything one plan lays, tried against everything else: inside the site, the rows, the aisles, the connector and the booth apart, the mouth a gap in the street's
    // kerb that the connector reaches, every aisle against the connector, the lamps and the barrier's posts on the ground.
    private static string ParkPlanFault(CarParkLayout l)
    {
        if (l.Bays < 1 || l.Rows.Count < 1 || l.Aisles.Count < 1)
        {
            return "no bays, rows or aisles";
        }
        int total = 0;
        for (int i = 0; i < l.Rows.Count; i++)
        {
            ParkRect box = l.Rows[i].Box;
            total += l.Rows[i].Bays;
            if (!Inside(box, l))
            {
                return "row " + i + " outside the site";
            }
            if (Overlap(box, l.Connector))
            {
                return "row " + i + " over the connector";
            }
            for (int k = i + 1; k < l.Rows.Count; k++)
            {
                if (Overlap(box, l.Rows[k].Box))
                {
                    return "rows " + i + " and " + k + " overlap";
                }
            }
            for (int k = 0; k < l.Aisles.Count; k++)
            {
                if (Overlap(box, l.Aisles[k]))
                {
                    return "row " + i + " over aisle " + k;
                }
            }
            float along = l.Rows[i].Along ? box.Length : box.Width, across = l.Rows[i].Along ? box.Width : box.Length;
            if (Math.Abs(along / CarParkRules.BayWidth - l.Rows[i].Bays) > 0.01f || Math.Abs(across - CarParkRules.BayDepth) > 0.01f)
            {
                return "row " + i + " is not whole bays long and a bay deep";
            }
        }
        if (total != l.Bays)
        {
            return "the bays are not the rows' bays";
        }
        for (int i = 0; i < l.Aisles.Count; i++)
        {
            ParkRect a = l.Aisles[i];
            if (!Inside(a, l) || Overlap(a, l.Connector))
            {
                return "aisle " + i + " outside the site or over the connector";
            }
            bool touches = l.Across ? Math.Abs(Math.Min(Math.Abs(a.X0 - l.Connector.X1), Math.Abs(a.X1 - l.Connector.X0))) < 0.02f && a.Z1 > l.Connector.Z0 && a.Z0 < l.Connector.Z1
                                    : Math.Abs(Math.Min(Math.Abs(a.Z0 - l.Connector.Z1), Math.Abs(a.Z1 - l.Connector.Z0))) < 0.02f && a.X1 > l.Connector.X0 && a.X0 < l.Connector.X1;
            if (!touches)
            {
                return "aisle " + i + " does not meet the connector";
            }
            for (int k = i + 1; k < l.Aisles.Count; k++)
            {
                if (Overlap(a, l.Aisles[k]))
                {
                    return "aisles " + i + " and " + k + " overlap";
                }
            }
        }
        if (!Inside(l.Connector, l))
        {
            return "the connector is outside the site";
        }
        float front = -l.Depth * 0.5f;
        bool gapOk = Math.Abs(l.Mouth.Z0 - front) < 0.02f && l.Mouth.Z1 - l.Mouth.Z0 > 0.2f && l.Mouth.X0 >= l.Connector.X0 - 0.02f && l.Mouth.X1 <= l.Connector.X1 + 0.02f
                     && Math.Abs(l.Mouth.Width - CarParkRules.ConnectorWidth) < 0.02f;
        if (!gapOk)
        {
            return "the mouth is not a gap in the street's side that the connector reaches";
        }
        ParkRect booth = new ParkRect(l.Booth.X - 0.92f, l.Booth.Z - 0.92f, l.Booth.X + 0.92f, l.Booth.Z + 0.92f);
        if (!Inside(booth, l, 0.05f) || Overlap(booth, l.Connector, 0.05f))
        {
            return "the booth is outside the site or in the connector";
        }
        for (int i = 0; i < l.Rows.Count; i++)
        {
            if (Overlap(booth, l.Rows[i].Box, 0.05f))
            {
                return "the booth stands in row " + i;
            }
        }
        for (int i = 0; i < l.Aisles.Count; i++)
        {
            if (Overlap(booth, l.Aisles[i], 0.05f))
            {
                return "the booth stands in aisle " + i;
            }
        }
        for (int i = 0; i < l.Lamps.Count; i++)
        {
            if (!InsideAt(l.Lamps[i].X, l.Lamps[i].Z, l))
            {
                return "lamp " + i + " outside the site";
            }
        }
        for (int i = 0; i < l.Barriers.Count; i++)
        {
            ParkPoint b = l.Barriers[i];
            if (b.X < l.Connector.X0 - 0.02f || b.X > l.Connector.X1 + 0.02f || b.Z < l.Connector.Z0 - 0.02f || b.Z > l.Connector.Z1 + 0.02f)
            {
                return "barrier post " + i + " off the connector";
            }
        }
        for (int i = 0; i < l.Kerbs.Count; i++)
        {
            if (!Inside(l.Kerbs[i], l) || Overlap(l.Kerbs[i], l.Mouth, 0.001f))
            {
                return "kerb " + i + " outside the site or across the mouth";
            }
        }
        for (int i = 0; i < l.Blue.Count; i++)
        {
            bool inRow = false;
            for (int k = 0; k < l.Rows.Count && !inRow; k++)
            {
                inRow = Overlap(l.Blue[i], l.Rows[k].Box, 0.001f);
            }
            if (!inRow)
            {
                return "blue square " + i + " is not in a row";
            }
        }
        return l.Accessible >= 1 && l.Blue.Count == l.Accessible ? null : "the bays for the disabled do not match their squares";
    }

    private static void ParkLayoutChecks()
    {
        // Every size of site the plan's squares make: 2 to 7 columns of 7 to 9 m, 2 to 5 rows of about 7.5 m, and every whole metre between.
        int bad = 0, ok = 0, none = 0;
        string first = null;
        for (int w = 12; w <= 64; w++)
        {
            for (int d = 12; d <= 40; d++)
            {
                for (int high = 0; high < 2; high++)
                {
                    CarParkLayout l = CarParkRules.Layout(w, d, high == 1);
                    if (!l.Ok)
                    {
                        none++;
                        bad += l.Why.Length == 0 ? 1 : 0;
                        continue;
                    }
                    ok++;
                    string fault = ParkPlanFault(l);
                    if (fault != null)
                    {
                        bad++;
                        first = first ?? w + " by " + d + " (mouth " + (high == 1 ? "high" : "low") + "): " + fault;
                    }
                }
            }
        }
        Check(bad == 0 && ok > 1000 && none > 50, "car park layout: every one of " + ok + " plans (12 to 64 by 12 to 40 m, the mouth at either end) lays its rows, aisles, connector, booth, mouth, lamps and kerbs apart and inside the site; "
              + none + " sites too small say why" + (first != null ? "; the first fault: " + first : string.Empty));
        // By hand: 24 by 37 m. The aisles along the street: rows of (24 - 1.2 - 6.5) / 2.5 = 6 bays, a row, an aisle, two rows, an aisle, a row (35.8 m holds two modules of 16): 4 rows of 6, the
        // booth's bay out of the first: 23. The aisles from the street back: rows of 11, one module of 16 in 22.8 m: 2 rows of 11, less the booth's bay: 21. The better is 23.
        CarParkLayout a = CarParkRules.Layout(24, 37, false), b = CarParkRules.Layout(24, 37, true);
        Check(a.Ok && a.Across && a.Bays == 23 && a.Rows.Count == 4 && a.Aisles.Count == 2 && a.Rows[0].Booth && a.Rows[0].Bays == 5 && b.Bays == 23 && b.MouthHigh && a.Accessible == 1 && a.Blue.Count == 1,
            "car park layout: 24 by 37 m holds 23 bays in four rows with two aisles, the booth in the first row's first bay and one bay marked for the disabled (" + a.Bays + ")");
        Check(a.Mouth.MidX < 0f && b.Mouth.MidX > 0f && Math.Abs(a.Mouth.MidX + b.Mouth.MidX) < 0.02f && Math.Abs(a.Mouth.Z0 + 18.5f) < 0.02f,
            "car park layout: the mouth is at the low end of the street's side or the high end, the same distance from the middle, on the site's front line");
        CarParkLayout small = CarParkRules.Layout(14, 14, false), narrow = CarParkRules.Layout(14, 40, false), shallow = CarParkRules.Layout(40, 12, false);
        Check(!small.Ok && small.Why.Length > 0 && !shallow.Ok && shallow.Why.Length > 0, "car park layout: a site too small, or too shallow for an aisle and a row, is no car park and says why (" + small.Why + ")");
        Check(narrow.Ok && narrow.Bays >= CarParkRules.BaysLeast, "car park layout: two columns' width (14 m) of the deepest site still holds a car park (" + narrow.Bays + " bays)");
        // Monotone: a bigger site never holds fewer bays.
        bool monotone = true;
        for (int w = 14; w <= 63 && monotone; w++)
        {
            for (int d = 14; d <= 39 && monotone; d++)
            {
                int here = CarParkRules.BaysOf(w, d);
                monotone = CarParkRules.BaysOf(w + 1, d) >= here && CarParkRules.BaysOf(w, d + 1) >= here;
            }
        }
        Check(monotone, "car park layout: a wider or deeper site never holds fewer bays");
        // The same twice, and the whole metres alone decide it.
        CarParkLayout c = CarParkRules.Layout(31, 29, false), d2 = CarParkRules.Layout(31, 29, false);
        Check(c.Bays == d2.Bays && c.Rows.Count == d2.Rows.Count && c.Lines.Count == d2.Lines.Count && c.Lamps.Count == d2.Lamps.Count && c.Booth.X == d2.Booth.X && c.Across == d2.Across,
            "car park layout: the same site gives the same plan every time");
        // The lines: a divider for every bay and one more at the end of each row (the first row's, after the booth's bay, one fewer).
        int lines = 0;
        for (int i = 0; i < a.Rows.Count; i++)
        {
            lines += a.Rows[i].Bays + 1;
        }
        Check(a.Lines.Count == lines && a.Dashes.Count > 4 && a.Lamps.Count >= 2 && a.Barriers.Count == 2 && a.Kerbs.Count >= 5,
            "car park layout: a divider for every bay and one more to close each row, dashes down the aisles and the connector, a lamp every 16 m of aisle, two barrier posts, a kerb all round but the mouth (" + a.Lines.Count + " lines)");
        ParkRect apron = a.Apron(CarParkRules.PavementDepth);
        Check(Math.Abs(apron.Z1 - a.Mouth.Z0) < 0.001f && Math.Abs(apron.Length - CarParkRules.PavementDepth) < 0.001f && Math.Abs(apron.Width - CarParkRules.ConnectorWidth) < 0.001f,
            "car park layout: the apron over the pavement is the mouth's width and the pavement's depth, outside the site's front line");
    }

    // ------------------------------------------------------------------ the works

    private static void ParkWorksChecks()
    {
        Check(CarParkRules.Hours(1) == 10 && CarParkRules.Hours(360) == 10 && CarParkRules.Hours(361) == 11 && CarParkRules.Hours(1080) == 30 && CarParkRules.Hours(5000) == 30 && CarParkRules.Hours(-3) == 10,
            "car park works: 10 working hours at the least, one for every 36 m2 over 360, 30 at the most");
        Check(CarParkRules.WorkingDays(10) == 1 && CarParkRules.WorkingDays(11) == 2 && CarParkRules.WorkingDays(20) == 2 && CarParkRules.WorkingDays(21) == 3 && CarParkRules.WorkingDays(30) == 3,
            "car park works: one to three working days by size");
        bool days = true;
        for (int w = 14; w <= 63; w++)
        {
            for (int d = 14; d <= 39; d++)
            {
                if (CarParkRules.BaysOf(w, d) >= CarParkRules.BaysLeast)
                {
                    int n = CarParkRules.WorkingDays(CarParkRules.Hours(w * d));
                    days &= n >= 1 && n <= 3;
                }
            }
        }
        Check(days, "car park works: no site the plan can make takes more than three working days");
        PlansQuote q = CarParkRules.Quote(1, 40000L), cheap = CarParkRules.Quote(0, 5000L);
        Check(q.Days == 1 && q.Fee == 400 && q.Quality == 78 && cheap.Fee == 400 && CarParkRules.Quote(2, 200000L).Fee == 2000 && CarParkRules.Quote(2, 200000L).Quality == 86 && CarParkRules.Quote(9, 0L).Days == 1,
            "car park plans: any practice draws them in a day for a hundredth of the estimate, 400 at the least, its own quality");
        Check(CarParkRules.PermitDays == 1 && CarParkRules.Permit(true, true) && !CarParkRules.Permit(false, true) && !CarParkRules.Permit(true, false),
            "car park permission: City Hall decides in a working day and grants whenever the land is zoned for one and the car park is allowed there, with no odds to draw");
        // The cost, by the programme's own tables for its kind of site: more area, more hours, more money; about 40 a square metre at the start's prices.
        int[] exchange = { 1000, 1000, 1000, 1000, 1000 };
        long small = PlansRules.EstimateWorks(CarParkRules.WorksKind, CarParkRules.Hours(200), 1000, exchange).Total;
        long mid = PlansRules.EstimateWorks(CarParkRules.WorksKind, CarParkRules.Hours(900), 1000, exchange).Total;
        long big = PlansRules.EstimateWorks(CarParkRules.WorksKind, CarParkRules.Hours(2400), 1000, exchange).Total;
        long perM2 = mid / 900L;
        Check(small > 0 && small < mid && mid < big && perM2 >= 20L && perM2 <= 80L, "car park cost: by area through the programme's table for its works (" + small + " for 200 m2, " + mid + " for 900, " + big + " for 2400; "
              + perM2 + " a square metre at 900)");
        Check(BuildCatalogue.WorksLoads.Length == 8 && BuildCatalogue.WorksCrew.Length == 8 && BuildCatalogue.WorksPlant.Length == 8 && BuildCatalogue.WorksRate.Length == 8 && CarParkRules.WorksKind == 7
              && BuildCatalogue.WorksCrew[7][(int)RealLifeOverhaul.Construction.Trade.Electrician] == 1 && BuildCatalogue.WorksPlant[7][(int)Plant.Digger] == 1 && BuildCatalogue.WorksPlant[7][(int)Plant.Crane] == 0
              && BuildCatalogue.WorksLoads[7][(int)Material.Cement] > 0,
            "car park works: the works tables hold a row for the new kind of site (labourers, a mason and an electrician; the digger and the mixer; cement and steel)");
        // The project: a car park is worked as the programme's works, one final inspection, and built by crews or a hired builder as any.
        var p = new ProjectFile { Kind = ProjectKind.Parking };
        Check(p.Model == ProjectKind.Contract && PlansRules.Inspections(p.Model) == 1 && PlansRules.InspectionAfter(p.Model, WorkStage.Envelope) == (int)Inspection.Final && !p.Hired,
            "car park project: its works are the programme's own, with the one final inspection");
        // Saved as a number and never renumbered: the kinds before it keep theirs.
        Check((int)ProjectKind.Parking == 7 && (int)ProjectKind.Conversion == 6 && (int)ProjectKind.Plot == 0, "car park project: the new kind is the number after the last, the others keep theirs");
    }

    // ------------------------------------------------------------------ where, the price, how full

    private static void ParkZoningChecks()
    {
        int shops = BuildCatalogue.Zone(BuildUse.Shops), offices = BuildCatalogue.Zone(BuildUse.Offices), warehouses = BuildCatalogue.Zone(BuildUse.Warehouses), homes = BuildCatalogue.Zone(BuildUse.Homes);
        Check(CarParkRules.Allowed(shops, 200, out _) && CarParkRules.Allowed(offices, 200, out _) && CarParkRules.Allowed(warehouses, 200, out _) && CarParkRules.Allowed(homes | shops, 200, out _),
            "car park zoning: land zoned for shops, offices or warehouses takes a car park of any size, and so does land zoned for those and homes");
        Check(CarParkRules.HomesBaysMost == 12 && CarParkRules.Allowed(homes, CarParkRules.HomesBaysMost, out string none) && none == null && !CarParkRules.Allowed(homes, CarParkRules.HomesBaysMost + 1, out string big) && big == "reallife_park_answer_homes_big",
            "car park zoning: land zoned for homes alone takes the residents' own, " + CarParkRules.HomesBaysMost + " bays at most, and says so");
        Check(!CarParkRules.Allowed(0, 10, out string unzoned) && unzoned == "reallife_park_answer_not_zoned", "car park zoning: land zoned for nothing takes none");
        Check(CarParkRules.UseOf(shops | offices | homes) == BuildUse.Shops && CarParkRules.UseOf(offices | warehouses) == BuildUse.Offices && CarParkRules.UseOf(warehouses) == BuildUse.Warehouses
              && CarParkRules.UseOf(homes) == BuildUse.Homes && CarParkRules.UseOf(0) == BuildUse.Homes,
            "car park zoning: a plan files a car park under the first commercial use the land is zoned for");
    }

    private static void ParkPriceChecks()
    {
        Check(CarParkRules.Fair(9.0) == 900 && CarParkRules.Fair(4.0) == 400 && CarParkRules.Fair(7.5) == 750 && CarParkRules.Fair(0.0) == 100 && CarParkRules.Fair(0.4) == 100 && CarParkRules.Fair(20000.0) == 1000000,
            "car park price: the street rate is the district's an hour in cents on a 50 cent step, a dollar at the least");
        Check(CarParkRules.PriceLeast(900) == 450 && CarParkRules.PriceMost(900) == 2250 && CarParkRules.PriceLeast(100) == 50 && CarParkRules.PriceMost(100) == 250,
            "car park price: the band is half the street rate to two and a half times it");
        Check(CarParkRules.ClampPrice(100, 900) == 450 && CarParkRules.ClampPrice(99999, 900) == 2250 && CarParkRules.ClampPrice(901, 900) == 900 && CarParkRules.ClampPrice(930, 900) == 950
              && CarParkRules.Stepped(900, 900, 1) == 950 && CarParkRules.Stepped(900, 900, -1) == 850 && CarParkRules.Stepped(450, 900, -1) == 450 && CarParkRules.Stepped(2250, 900, 1) == 2250,
            "car park price: a price is put inside the band on a step, and moved a step up or down never out of it");
        Check(CarParkRules.PriceFactor(900, 900) == 1000 && CarParkRules.PriceFactor(450, 900) == 1300 && CarParkRules.PriceFactor(1800, 900) == 400 && CarParkRules.PriceFactor(5000, 900) == CarParkRules.PriceFactorLeast
              && CarParkRules.PriceFactor(1, 900) >= 1500 - 1,
            "car park price: a price at the street rate keeps all the custom, half of it brings a third more, twice it loses three fifths");
        // What a price earns: the dearer, the fewer cars; the takings are best a third over the street rate and fall away beyond it.
        long best = 0L;
        int bestAt = 0;
        for (int price = CarParkRules.PriceLeast(900); price <= CarParkRules.PriceMost(900); price += CarParkRules.PriceStep)
        {
            long takes = (long)price * CarParkRules.PriceFactor(price, 900);
            if (takes > best)
            {
                best = takes;
                bestAt = price;
            }
        }
        Check(bestAt >= 1150 && bestAt <= 1250, "car park price: the best price a lot can set is about a third over the street rate (" + bestAt + " cents on a rate of 900)");
    }

    private static void ParkOccupancyChecks()
    {
        int catchment = CarParkRules.Catchment(30, 10);
        Check(catchment == 17 && CarParkRules.Catchment(0, 0) == 0 && CarParkRules.Catchment(100, 99) == (100 * 40 + CarParkRules.NearbyMost * 50 + 50) / 100 && CarParkRules.Catchment(-4, -2) == 0,
            "car park custom: the cars looking for a bay at the busiest hour come of the lot's traffic and the businesses near it, counted to " + CarParkRules.NearbyMost);
        // A day at a typical site: 23 bays, traffic 30, ten businesses near, no other car park near, the street rate, the market even, nine in ten driving.
        int bays = 23;
        long takings = 0L;
        int total = 0, peak = 0, night = 0;
        var byHour = new int[24];
        for (int h = 0; h < 24; h++)
        {
            int cars = CarParkRules.Cars(bays, h, false, catchment, 0, 900, 900, 900, 1000, 7, 3, 20);
            byHour[h] = cars;
            total += cars;
            takings += CarParkRules.Takings(cars, 900);
            peak = Math.Max(peak, cars);
            night = h < 5 ? Math.Max(night, cars) : night;
        }
        int fill = CarParkRules.DayFill(byHour, bays);
        long perBayDay = takings / 100L / bays;
        Check(fill >= 200 && fill <= 600 && peak >= 10 && peak <= bays && night <= 2 && perBayDay >= 30L && perBayDay <= 120L,
            "car park custom: a typical site is busy by day and empty at night: " + fill / 10 + "% full over the day, " + peak + " cars at the peak, " + night + " at night, " + perBayDay + " a bay a day at the street rate");
        // Never more than the bays, never fewer than none, however busy.
        bool bounds = true;
        for (int h = 0; h < 24; h++)
        {
            int cars = CarParkRules.Cars(6, h, h % 2 == 0, 400, 0, 450, 900, 1000, 1500, 9, 1, h);
            int none = CarParkRules.Cars(0, h, false, 400, 0, 900, 900, 900, 1000, 9, 1, h);
            bounds &= cars >= 0 && cars <= 6 && none == 0;
        }
        Check(bounds, "car park custom: never more cars than bays and never fewer than none");
        // The price: dearer, fewer cars. The other bays near: fewer again. A busier site: more.
        int at = 12;
        int fair = CarParkRules.Cars(40, at, false, 40, 0, 900, 900, 900, 1000, 5, 5, 5);
        int dear = CarParkRules.Cars(40, at, false, 40, 0, 1800, 900, 900, 1000, 5, 5, 5);
        int cheap = CarParkRules.Cars(40, at, false, 40, 0, 450, 900, 900, 1000, 5, 5, 5);
        int crowded = CarParkRules.Cars(40, at, false, 40, 120, 900, 900, 900, 1000, 5, 5, 5);
        int busy = CarParkRules.Cars(40, at, false, 60, 0, 900, 900, 900, 1000, 5, 5, 5);
        Check(cheap > fair && fair > dear && fair > crowded && busy > fair, "car park custom: a dearer price brings fewer cars, a cheaper more; other public bays near take their share; a busier site fills more (" + cheap + ", " + fair + ", " + dear + ", " + crowded + ", " + busy + ")");
        // The hour: the weekday's morning rush and the weekend's night.
        int monday8 = CarParkRules.Cars(60, 8, false, 50, 0, 900, 900, 900, 1000, 5, 5, 5), saturday8 = CarParkRules.Cars(60, 8, true, 50, 0, 900, 900, 900, 1000, 5, 5, 5);
        int monday21 = CarParkRules.Cars(60, 21, false, 50, 0, 900, 900, 900, 1000, 5, 5, 5), saturday21 = CarParkRules.Cars(60, 21, true, 50, 0, 900, 900, 900, 1000, 5, 5, 5);
        Check(monday8 > saturday8 && saturday21 > monday21, "car park custom: the weekday's morning is busier than the weekend's, and the weekend's night than the weekday's");
        // The same inputs, the same cars; a different day may draw a car more or less.
        Check(CarParkRules.Cars(30, 12, false, 30, 0, 900, 900, 900, 1000, 3, 4, 5) == CarParkRules.Cars(30, 12, false, 30, 0, 900, 900, 900, 1000, 3, 4, 5),
            "car park custom: the same inputs give the same cars on every machine");
        Check(CarParkRules.Occupied(0, 20) == 0 && CarParkRules.Occupied(10, 20) == 50 && CarParkRules.Occupied(25, 20) == 100 && CarParkRules.Occupied(5, 0) == 0,
            "car park custom: the share of the game's cars left to park in the lane's bays is the cars over the bays");
        // Upkeep: a bay a day, dearer for a public lot than for one let by the month.
        Check(CarParkRules.Upkeep(20, CarParkMode.Public, 1000) == 440L && CarParkRules.Upkeep(20, CarParkMode.Lease, 1000) == 120L && CarParkRules.Upkeep(20, CarParkMode.Public, 2000) == 880L && CarParkRules.Upkeep(0, CarParkMode.Public, 1000) == 0L,
            "car park upkeep: 22 a bay a day when public, 6 when let by the month, at today's prices");
        Check(CarParkRules.OwnFee(10, 9.0) == 90L && CarParkRules.OwnFee(0, 9.0) == 0L && CarParkRules.OwnFee(5, 0.0) == 0L, "car park own cars: the street rate paid back for the hours a car stood in its owner's lot");
    }

    // ------------------------------------------------------------------ leases

    private static void ParkLeaseChecks()
    {
        long monthly = CarParkRules.LeaseMonthly(900);
        Check(monthly == 110100L && CarParkRules.LeaseMonthly(0) == 0L && monthly % 100L == 0L, "car park lease: a bay a month is 17% of what it would take round the clock at the street rate, in whole dollars (" + monthly + " cents)");
        bool band = true;
        for (int week = 1; week < 60; week++)
        {
            long offer = CarParkRules.LeaseOffer(900, 11, 3, week, week % 5);
            band &= offer >= monthly * 899L / 1000L && offer <= monthly * 1101L / 1000L && offer % 100L == 0L;
        }
        Check(band && CarParkRules.LeaseOffer(900, 11, 3, 4, 2) == CarParkRules.LeaseOffer(900, 11, 3, 4, 2), "car park lease: an offer's price is 90% to 110% of the usual, the same every time it is asked");
        Check(CarParkRules.AskChance(1, 0) == 140 && CarParkRules.AskChance(10, 100) == 650 && CarParkRules.AskChance(5, 30) == 420 && CarParkRules.AskChance(99, 999) == CarParkRules.ChanceMost && CarParkRules.AskChance(0, 0) == CarParkRules.ChanceLeast + 20,
            "car park lease: a business asks more often the bigger it is and the busier the lot, within 12% and 65% a week");
        bool bays = true;
        for (int weight = 1; weight <= 10; weight++)
        {
            for (int free = 0; free <= 14; free++)
            {
                int n = CarParkRules.OfferBays(weight, free, 5, 2, 7, weight);
                bays &= n >= 0 && n <= free && n <= CarParkRules.LeaseBaysMost && (free < CarParkRules.LeaseBaysLeast || n >= CarParkRules.LeaseBaysLeast);
            }
        }
        Check(bays, "car park lease: an offer asks for " + CarParkRules.LeaseBaysLeast + " to " + CarParkRules.LeaseBaysMost + " bays and never for more than are free");
        Check(CarParkRules.Refund(110100L, 4, 15) == 220200L && CarParkRules.Refund(110100L, 4, 30) == 440400L && CarParkRules.Refund(110100L, 4, 0) == 0L && CarParkRules.Refund(110100L, 4, 90) == 440400L && CarParkRules.Refund(1L, -1, 5) == 0L,
            "car park lease: ending one early pays back the unused days of the month, never more than the month");
        // Mode and price: the leases pay less than the hours at a busy site but the same whatever the custom, and cost less to keep.
        long perBayMonth = monthly / 100L, publicMonth = perBayDay(900) * CarParkRules.LeaseDays;
        Check(perBayMonth > 0L && publicMonth > perBayMonth, "car park lease: at a busy site a bay earns more by the hour than by the month (" + publicMonth + " against " + perBayMonth + " a month at the street rate)");
    }

    // A bay's takings a day at the typical site, in dollars.
    private static long perBayDay(int fair)
    {
        int catchment = CarParkRules.Catchment(30, 10);
        long total = 0L;
        for (int h = 0; h < 24; h++)
        {
            total += CarParkRules.Takings(CarParkRules.Cars(23, h, false, catchment, 0, fair, fair, 900, 1000, 7, 3, 20), fair);
        }
        return total / 100L / 23L;
    }

    // ------------------------------------------------------------------ the city's builders

    private static void ParkNpcChecks()
    {
        Check(CarParkRules.NpcWants(60, 0, 0, 0, 0) && !CarParkRules.NpcWants(49, 0, 0, 0, 0) && !CarParkRules.NpcWants(60, 0, 0, 0, 1) && !CarParkRules.NpcWants(60, 100, 3, 900, 0)
              && !CarParkRules.NpcWants(60, 160, 1, 900, 0) && !CarParkRules.NpcWants(60, 60, 1, 400, 0) && CarParkRules.NpcWants(60, 60, 1, 700, 0) && CarParkRules.NpcWants(100, 100, 2, 600, 0),
            "car park city: the builders begin one where shops and offices want it (50 or more), there is none under way, fewer than three stand, the public bays fall short of three a point and the lots are busy");
        int leasing = 0;
        for (int lot = 1; lot <= 300; lot++)
        {
            leasing += CarParkRules.NpcLeases(41, lot) ? 1 : 0;
        }
        Check(leasing >= 70 && leasing <= 130 && CarParkRules.NpcLeases(41, 9) == CarParkRules.NpcLeases(41, 9), "car park city: about one of the city's lots in three lets leases instead of the hour (" + leasing + " of 300), the same each time");
        Check(CarParkRules.NpcColumns(5, 40, 1) == 0 && CarParkRules.NpcColumns(5, 40, 2) == 2 && CarParkRules.NpcColumns(5, 40, 7) >= 2 && CarParkRules.NpcColumns(5, 40, 7) <= 4 && CarParkRules.NpcColumns(5, 40, 3) <= 3,
            "car park city: a builder takes two to four columns of a free run, none of a single space");
    }

    // ------------------------------------------------------------------ the save

    private static void ParkStoreChecks()
    {
        var s = new BuildState { Seed = 9, FirstDay = 2, LastDay = 14, ParkRefunded = 123456L };
        s.ParkNextDay[0] = 33;
        s.ParkNextDay[1] = 47;
        s.Lots.Add(new LotFile { Id = "u.2.1.3", Bought = 4, Zones = 3, Spaces = 3, Rows = "555" });
        s.Projects.Add(new ProjectFile { Id = 5, Kind = ProjectKind.Parking, Stage = ProjectStage.Ready, Lot = "u.2.1.3", Width = 24, Depth = 37, Area = 888, Park = 4, Design = "carpark", CivicKind = 7, Hours = 25 });
        s.Projects.Add(new ProjectFile { Id = 6, Kind = ProjectKind.Plot, Stage = ProjectStage.Design, Lot = "u.2.1.3", Grow = 2, Look = "corner_diner", CostPermille = 1200 });
        s.Projects.Add(new ProjectFile { Id = 7, Kind = ProjectKind.Extension, Stage = ProjectStage.Design, Street = "ba:street_x", Number = 4, Added = 3 });
        var c = new CarParkFile
        {
            Id = 20, Owner = CarParkOwner.Player, Stage = CarParkStage.Open, Lot = "u.2.1.3", Spaces = 3, Rows = "555", Width = 24, Depth = 37, Mouth = 4, Bays = 23, Mode = CarParkMode.Lease,
            Price = 1050, Project = 5, Day = 20, Until = 0, Opened = 24, Cars = 11, Seen = 24 * 30 + 9, Taken = 12345L, Earned = 99999999999L, Spent = 8888L, LastDay = 29, Others = 0, Own = 7
        };
        c.Daily.AddRange(new[] { 120, -40, 0, 560 });
        c.Leases.Add(new CarParkLease { Id = 21, Name = "Dyer & Sons; \"Cafe\" | co", Who = CarParkLease.Business, Bays = 4, Monthly = 110100L, Start = 22, PaidTo = 51, Ending = true });
        c.Leases.Add(new CarParkLease { Id = 22, Name = string.Empty, Who = CarParkLease.Player, Bays = 2, Monthly = 99000L, Start = 25, PaidTo = 54 });
        c.Offers.Add(new CarParkOffer { Id = 23, Name = "Hale Holdings, Ltd", Who = CarParkLease.Rival, Bays = 6, Monthly = 120000L, Expires = 38 });
        s.CarParks.Add(c);
        s.CarParks.Add(new CarParkFile { Id = 30, Owner = CarParkOwner.Builder, Rival = 5, Stage = CarParkStage.Building, Lot = "u.3.4.5", Spaces = 2, Width = 16, Depth = 35, Bays = 14, SiteId = 91, Until = 40, Day = 33, Others = 3, Own = 0 });
        string line = BuildStore.Write(s);
        BuildState r = BuildStore.Read(line, out int version);
        CarParkFile p = r != null ? r.CarPark(20) : null, q = r != null ? r.CarPark(30) : null;
        Check(r != null && version == BuildStore.Version && p != null && p.Owner == CarParkOwner.Player && p.Stage == CarParkStage.Open && p.Lot == "u.2.1.3" && p.Spaces == 3 && p.Rows == "555" && p.Width == 24
              && p.Depth == 37 && p.Mouth == 4 && p.Bays == 23 && p.Mode == CarParkMode.Lease && p.Price == 1050 && p.Project == 5 && p.Day == 20 && p.Opened == 24 && p.Cars == 11 && p.Seen == 24 * 30 + 9
              && p.Taken == 12345L && p.Earned == 99999999999L && p.Spent == 8888L && p.LastDay == 29 && p.Own == 7,
            "car park store: a car park's figures, land, mode and price read back");
        Check(p != null && p.Daily.SequenceEqual(new[] { 120, -40, 0, 560 }) && p.Leases.Count == 2 && p.Leases[0].Name == "Dyer & Sons; \"Cafe\" | co" && p.Leases[0].Ending && p.Leases[0].Monthly == 110100L && p.Leases[0].PaidTo == 51
              && p.Leases[1].Who == CarParkLease.Player && p.Leases[1].Name == string.Empty && p.Offers.Count == 1 && p.Offers[0].Name == "Hale Holdings, Ltd" && p.Offers[0].Who == CarParkLease.Rival && p.Offers[0].Expires == 38
              && p.Leased == 6 && p.LeasedByPlayer == 2 && p.Free == 17 && p.LeaseIncome == 4L * 110100L + 2L * 99000L,
            "car park store: the daily list, the leases and the offers read back, a name with the dividers in it unharmed");
        Check(q != null && q.Owner == CarParkOwner.Builder && q.Rival == 5 && q.Stage == CarParkStage.Building && q.SiteId == 91 && q.Until == 40 && q.Others == 3 && q.UnderWay && !q.Standing && q.Free == 14 - 3
              && r.ParkNextDay[0] == 33 && r.ParkNextDay[1] == 47 && r.ParkRefunded == 123456L && r.CarParkSite(91) == q && r.CarParkOfProject(5) == p,
            "car park store: a city builder's car park, the days the builders may next begin one and the money paid back read back");
        ProjectFile park = r != null ? r.Project(5) : null, plot = r != null ? r.Project(6) : null, ext = r != null ? r.Project(7) : null;
        Check(park != null && park.Kind == ProjectKind.Parking && park.Park == 4 && park.CivicKind == 7 && park.Hours == 25 && park.Width == 24 && park.Depth == 37 && park.Design == "carpark"
              && plot != null && plot.Grow == 2 && plot.Look == "corner_diner" && plot.Park == 0 && plot.CostPermille == 1200 && ext != null && ext.Added == 3 && ext.Park == 0,
            "car park store: a car park's project keeps its kind, works and mouth; a Let it grow plot's tier and corner model and an extension's storeys are read as before");
        // A line from before: no part, no project of the new kind.
        var before = new BuildState { Seed = 9, FirstDay = 2, LastDay = 14 };
        before.Projects.Add(new ProjectFile { Id = 3, Kind = ProjectKind.Plot, Lot = "u.x", Grow = 1, Look = "corner_loft" });
        string plain = BuildStore.Write(before);
        BuildState back = BuildStore.Read(plain, out _);
        Check(!plain.Contains("^j:") && back != null && back.CarParks.Count == 0 && back.ParkNextDay[0] == 0 && back.ParkRefunded == 0L && back.Project(3).Grow == 1 && back.Project(3).Look == "corner_loft",
            "car park store: a save that never had a car park writes its line as before and reads back with none");
        // An older reader: the new part has a letter it passes over, and the project's new field sits after every field it knows.
        string oldLine = Regex.Replace(line, @"\^j:[^\^]*", string.Empty);
        BuildState older = BuildStore.Read(oldLine, out _);
        Check(older != null && older.CarParks.Count == 0 && older.Lot("u.2.1.3") != null && older.Project(6) != null && older.Project(6).Grow == 2,
            "car park store: the line without the part still reads, as an older line does");
        // The cap: as many car parks as kept at most, the newest.
        var many = new BuildState { Seed = 1 };
        for (int i = 0; i < BuildState.CarParksMost + 20; i++)
        {
            many.CarParks.Add(new CarParkFile { Id = i + 1, Lot = "u." + i, Bays = 10 });
        }
        BuildState capped = BuildStore.Read(BuildStore.Write(many), out _);
        Check(capped != null && capped.CarParks.Count == BuildState.CarParksMost && capped.CarParks[capped.CarParks.Count - 1].Id == BuildState.CarParksMost + 20,
            "car park store: at most " + BuildState.CarParksMost + " car parks are kept, the newest");
        // Garbage in the part: a record that cannot be read is let go, the rest kept, nothing throws.
        string damaged = Regex.Replace(line, @"\^j:", "^j:5,5|zz;;not|a|record;");
        BuildState hurt = BuildStore.Read(damaged, out _);
        Check(hurt != null && hurt.CarPark(20) != null && hurt.CarPark(20).Bays == 23, "car park store: a damaged record is passed over and the others read");
    }

    // ------------------------------------------------------------------ the works' site

    private static void ParkSiteChecks()
    {
        Check(ParkSiteRules.Encode(24, 37, 90) == "park:24x37:090" && ParkSiteRules.Encode(24, 37, -90) == "park:24x37:270" && ParkSiteRules.Encode(8, 40, 450) == "park:8x40:090",
            "car park site: the land is the site's street field as text, its turn in whole degrees from 0 to 359");
        bool read = ParkSiteRules.TryRead("park:24x37:090", out int w, out int d, out int yaw) && w == 24 && d == 37 && yaw == 90;
        bool back = ParkSiteRules.TryRead(ParkSiteRules.Encode(64, 40, 270), out w, out d, out yaw) && w == 64 && d == 40 && yaw == 270;
        Check(read && back && !ParkSiteRules.TryRead("plot:24x20:090:5", out _, out _, out _) && !ParkSiteRules.TryRead("park:24x37", out _, out _, out _) && !ParkSiteRules.TryRead("park:axb:090", out _, out _, out _)
              && !ParkSiteRules.TryRead("park:5x37:090", out _, out _, out _) && !ParkSiteRules.TryRead("park:24x97:090", out _, out _, out _) && !ParkSiteRules.TryRead(null, out _, out _, out _)
              && ParkSiteRules.IsPark("park:1x1:000") && !ParkSiteRules.IsPark("plot:1x1:000") && !ParkSiteRules.IsPark(null),
            "car park site: the land is read back from the street field, and anything that is not a car park's or is a size no site can be is refused");
        Check(ParkSiteRules.Unfit(ParkSiteRules.Least, ParkSiteRules.Least) == null && ParkSiteRules.Unfit(ParkSiteRules.Most, ParkSiteRules.Most) == null && ParkSiteRules.Unfit(ParkSiteRules.Least - 1, 20) != null
              && ParkSiteRules.Unfit(20, ParkSiteRules.Most + 1) != null, "car park site: land of " + ParkSiteRules.Least + " to " + ParkSiteRules.Most + " m each way takes the works, other land does not");
        // Every size a car park can have is a size its works can be set out on.
        bool sizes = true;
        for (int sw = 12; sw <= 64; sw += 4)
        {
            for (int sd = 12; sd <= 40; sd += 4)
            {
                sizes &= CarParkRules.BaysOf(sw, sd) < CarParkRules.BaysLeast || ParkSiteRules.Unfit(sw, sd) == null;
            }
        }
        Check(sizes, "car park site: every size that holds a car park is a size its works are set out on");
        var spots = new List<PlotSpot>(16);
        ParkSiteRules.HoardingSpots(24, 37, spots);
        bool hoarding = ParkSiteRules.GateWidth == CarParkRules.ConnectorWidth && spots.Count >= 4 && spots.All(p => Math.Abs(p.Z + 18.5f) < 0.001f && Math.Abs(p.Yaw - 180f) < 0.001f && p.X - PlotRules.Hoarding * 0.5f >= -12f + ParkSiteRules.GateWidth - 0.001f
                                                           && p.X + PlotRules.Hoarding * 0.5f <= 12f + 0.011f);
        Check(hoarding, "car park site: the hoarding runs along the street's side with a gate as wide as the way in left open at the low-x end, " + spots.Count + " panels on 24 m");
        ShellBox slab = ParkSiteRules.Base(24, 37, ParkSiteRules.BaseHeight);
        Check(Math.Abs(slab.MaxX - 10.8f) < 0.001f && Math.Abs(slab.MinX + 10.8f) < 0.001f && Math.Abs(slab.MaxZ - 17.3f) < 0.001f && Math.Abs(slab.MinZ + 17.3f) < 0.001f && slab.MinY == 0f
              && Math.Abs(slab.MaxY - ParkSiteRules.BaseHeight) < 0.0001f, "car park site: the base is the land less a margin of " + ParkSiteRules.Margin + " m each side");
        Check(!ParkSiteRules.BaseLaid(0) && !ParkSiteRules.BaseLaid(1) && ParkSiteRules.BaseLaid(2) && ParkSiteRules.BaseLaid(4) && !ParkSiteRules.SurfaceLaid(2) && ParkSiteRules.SurfaceLaid(3) && ParkSiteRules.SurfaceLaid(4),
            "car park site: the base is laid from the third stage, the surface from the fourth");
        var xs = new List<float>(4);
        var zs = new List<float>(4);
        ParkSiteRules.Corners(100f, 200f, 0, 20, 10, xs, zs);
        bool straight = xs.Count == 4 && Math.Abs(xs.Min() - 90f) < 0.001f && Math.Abs(xs.Max() - 110f) < 0.001f && Math.Abs(zs.Min() - 195f) < 0.001f && Math.Abs(zs.Max() - 205f) < 0.001f;
        ParkSiteRules.Corners(100f, 200f, 90, 20, 10, xs, zs);
        bool turned = xs.Count == 4 && Math.Abs(xs.Min() - 95f) < 0.001f && Math.Abs(xs.Max() - 105f) < 0.001f && Math.Abs(zs.Min() - 190f) < 0.001f && Math.Abs(zs.Max() - 210f) < 0.001f;
        Check(straight && turned, "car park site: the four corners of the land in the world, turned with it");
    }

    // ------------------------------------------------------------------ rows, lease cars, plans' sorting

    private static void ParkExtraChecks()
    {
        // Every row's cars nose in, away from the aisle the row opens on (the side the nose does not point to is an aisle); the end by the throat is where the bays for the disabled begin.
        bool noseOk = true, nearOk = true, blueOk = true;
        string noseFault = null;
        int plans = 0;
        for (int w = 14; w <= 64 && noseOk && nearOk && blueOk; w += 5)
        {
            for (int d = 14; d <= 40 && noseOk && nearOk && blueOk; d += 3)
            {
                for (int code = 1; code <= 4; code++)
                {
                    CarParkLayout l = CarParkRules.LayoutOf(w, d, code);
                    if (!l.Ok)
                    {
                        continue;
                    }
                    plans++;
                    for (int r = 0; r < l.Rows.Count; r++)
                    {
                        ParkRow row = l.Rows[r];
                        // The aisle is on the side the nose does not point: a point a metre off the row's middle that way is in an aisle (or the connector for the row by the throat), the other way is not.
                        float rad = row.Yaw * (float)Math.PI / 180f, nx = (float)Math.Sin(rad), nz = (float)Math.Cos(rad);
                        float backX = row.X + nx * (CarParkRules.BayDepth * 0.5f + 0.4f), backZ = row.Z + nz * (CarParkRules.BayDepth * 0.5f + 0.4f);
                        float openX = row.X - nx * (CarParkRules.BayDepth * 0.5f + 0.4f), openZ = row.Z - nz * (CarParkRules.BayDepth * 0.5f + 0.4f);
                        bool openOnAisle = l.Aisles.Any(a => openX >= a.X0 + 0.01f && openX <= a.X1 - 0.01f && openZ >= a.Z0 + 0.01f && openZ <= a.Z1 - 0.01f);
                        bool backOnAisle = l.Aisles.Any(a => backX >= a.X0 + 0.01f && backX <= a.X1 - 0.01f && backZ >= a.Z0 + 0.01f && backZ <= a.Z1 - 0.01f);
                        if (!openOnAisle)
                        {
                            noseFault = noseFault ?? w + " by " + d + " code " + code + " row " + r + " yaw " + row.Yaw + " at " + row.X + "," + row.Z + " back " + backOnAisle;
                        }
                        noseOk &= openOnAisle;   // a row between two aisles is parked from either: its open side is an aisle all the same
                        nearOk &= row.NearHigh == (l.Across && l.MouthHigh);
                    }
                    ParkRow first = l.Rows[0];
                    ParkRect box = first.Box;
                    for (int k = 0; k < l.Accessible; k++)
                    {
                        ParkRect blue = l.Blue[k];
                        float at = first.Along ? blue.MidZ : blue.MidX;
                        float from = first.Along ? box.Z0 : first.NearHigh ? box.X1 : box.X0;
                        float reach = Math.Abs(at - from);
                        blueOk &= reach >= k * CarParkRules.BayWidth && reach <= (k + 1) * CarParkRules.BayWidth;
                    }
                }
            }
        }
        Check(plans > 100 && noseOk, "car park rows: every row's cars nose in, away from the aisle it opens on (" + plans + " plans; " + (noseFault ?? "all") + ")");
        Check(nearOk, "car park rows: the end of a row by the way in is its high end exactly when the aisles run along the street and the way in is at the high end");
        Check(blueOk, "car park rows: the bays for the disabled are the first bays of the first row counted from its end by the throat, on every plan");

        // Cars of the lessees: none with no leased bays, never more than the bays, busy by day on a weekday, few at night and at the weekend.
        Check(CarParkRules.LeaseCars(0, 10, false) == 0 && CarParkRules.LeaseCars(-3, 10, false) == 0, "lease cars: none where nothing is leased");
        bool bounded = true;
        for (int bays = 1; bays <= 12; bays++)
        {
            for (int h = 0; h < 24; h++)
            {
                bounded &= CarParkRules.LeaseCars(bays, h, false) <= bays && CarParkRules.LeaseCars(bays, h, true) <= bays && CarParkRules.LeaseCars(bays, h, false) >= 0;
            }
        }
        Check(bounded, "lease cars: never more than the leased bays, never below none");
        Check(CarParkRules.LeaseCars(10, 12, false) >= 8 && CarParkRules.LeaseCars(10, 3, false) <= 1 && CarParkRules.LeaseCars(10, 12, true) <= 4 && CarParkRules.LeaseCars(10, 12, false) > CarParkRules.LeaseCars(10, 12, true),
            "lease cars: most of ten leased bays hold a car at noon on a weekday, one at most at three in the morning, a few at the weekend");

        // The four ways a site can be laid, the most bays first, each made the way its code says.
        var list = new List<CarParkLayout>(4);
        bool sorted = true, coded = true;
        for (int w = 14; w <= 60; w += 7)
        {
            for (int d = 14; d <= 44; d += 5)
            {
                CarParkRules.Candidates(w, d, list);
                for (int i = 0; i < list.Count; i++)
                {
                    sorted &= i == 0 || list[i - 1].Bays >= list[i].Bays;
                    CarParkLayout again = CarParkRules.LayoutOf(w, d, list[i].Code);
                    coded &= again.Ok && again.Bays == list[i].Bays && again.Across == list[i].Across && again.MouthHigh == list[i].MouthHigh;
                }
                coded &= list.Count == 0 || CarParkRules.BaysOf(w, d) == list[0].Bays;
            }
        }
        Check(sorted && coded, "car park plans: the candidates are the plans a code lays, most bays first, and the best of them is what a site holds");
        Check(!CarParkRules.LayoutOf(30, 30, 0).Ok && !CarParkRules.LayoutOf(30, 30, 5).Ok && CarParkRules.LayoutOf(30, 30, 1).Ok, "car park plans: a code outside one to four lays nothing");
    }

    // ------------------------------------------------------------------ the sources

    private static void ParkSourceChecks()
    {
        string game = ModSource(@"Construction\CarParkGame.cs"), pointer = ModSource(@"Construction\CarParkPointer.cs"), desk = ModSource(@"Construction\CarParkDesk.cs");
        string park = ModSource(@"Construction\ConstructionDirector.Park.cs"), run = ModSource(@"Construction\ConstructionDirector.ParkRun.cs"), npc = ModSource(@"Construction\ConstructionDirector.ParkNpc.cs");
        string world = ModSource(@"Construction\ConstructionDirector.ParkWorld.cs"), mode = ModSource(@"Construction\BuildMode.Park.cs"), tests = ModSource(@"Construction\ConstructionDirector.ParkTests.cs");
        string app = ModSource(@"Construction\ConstructionApp.Park.cs"), gameFile = ModSource(@"Construction\ConstructionGame.Park.cs"), mesh = ModSource(@"Construction\CarParkMesh.cs");
        string rules = ModSource(@"Construction\CarParkRules.cs") + ModSource(@"Construction\CarParkLayout.cs"), state = ModSource(@"Construction\BuildState.Park.cs") + ModSource(@"Construction\BuildStore.Park.cs");
        string director = ModSource(@"Construction\ConstructionDirector.cs"), projects = ModSource(@"Construction\ConstructionDirector.Projects.cs"), hire = ModSource(@"Construction\ConstructionDirector.Hire.cs");
        string sites = ModSource(@"Construction\ConstructionDirector.Sites.cs"), day = ModSource(@"Construction\ConstructionDirector.Day.cs"), guide = ModSource(@"Construction\ConstructionGuide.cs");
        string window = ModSource(@"Construction\BuildMode.Window.cs"), buildMode = ModSource(@"Construction\BuildMode.cs"), join = ModSource(@"Construction\ConstructionDirector.Join.cs");
        string settings = ModSource(@"Core\Settings.cs"), wiring = ModSource(@"Wiring\Wiring.ConstructionLinks.cs"), bridges = ModSource(@"Construction\ConstructionBridges.cs");
        string contract = ModSource(@"Core\GameContract.cs"), dresser = ModSource(@"Civic\SiteDresser.cs"), works = ModSource(@"Civic\CivicWorks.cs"), catalogue = ModSource(@"Civic\CivicCatalogue.cs");
        string store = ModSource(@"Construction\BuildStore.cs"), buildCatalogue = ModSource(@"Construction\BuildCatalogue.cs"), sitePark = ModSource(@"Civic\SiteDresser.Park.cs");
        Check(game.Length > 0 && pointer.Length > 0 && desk.Length > 0 && park.Length > 0 && run.Length > 0 && npc.Length > 0 && world.Length > 0 && mode.Length > 0 && tests.Length > 0 && app.Length > 0
              && gameFile.Length > 0 && mesh.Length > 0 && rules.Length > 0 && state.Length > 0 && sitePark.Length > 0, "car park sources: every file is where the checks look");
        string all = game + pointer + desk + park + run + npc + world + mode + app + gameFile + mesh + sitePark;
        Check(!Regex.IsMatch(all, @"void\s+(Update|LateUpdate|FixedUpdate|OnGUI)\s*\(") && !all.Contains("InvokeRepeating") && !all.Contains("yield return") && !all.Contains("StartCoroutine"),
            "car park sources: nothing of the car parks runs a frame: no Update, no coroutine; the hour, the day and the second's look are the director's own passes");
        Check(Regex.IsMatch(rules, @"UnityEngine|(?<![A-Za-z])Random\.|DateTime|Environment\.TickCount") == false, "car park sources: the rules are game-free and draw nothing from the clock or Unity's chance");
        Check(Regex.IsMatch(day, @"(?m)^\s+ParkHour\(day, hour\);") && Regex.IsMatch(day, @"(?m)^\s+ParkDay\(day\);") && Regex.IsMatch(director, @"(?m)^\s+ParkWorldSecond\(\);")
              && director.Contains("ScenePrewarm.Add(\"construction: the game's parking lane being found") && Regex.IsMatch(director, @"public void StandDown\(string why\)[\s\S]*?\n\s+ParkWorldsDown\(\);[\s\S]*?DropShowPlace\("),
            "car park sources: the hour's and the day's passes, the second's look and the lane found behind the loading screen are wired, and every car park is taken away when the game stands down");
        Check(settings.Contains("public static bool ConstructionParking = true;") && settings.Contains("ConstructionParking = Bool(modId, ConstructionParkingId, ConstructionParking);")
              && guide.Contains("AddToggle(Settings.ConstructionParkingId, \"reallife_park_option\"") && guide.Contains(".Owns(Settings.ConstructionParkingId)")
              && ModSource(@"Meta\OptionsTree.cs").Contains(".Opt(\"construction_parking\", \"construction_buildmode\")") && park.Contains("public bool ParkingOn => Running && Settings.ConstructionParking;"),
            "car park sources: its option, on by default, in the construction group of the options and the Guide; off, nothing is built, stood or run");
        Check(park.Contains("if (GoingPublic.InSession)") && npc.Contains("if (GoingPublic.InSession || !ParkingOn") && run.Contains("if (GoingPublic.InSession)") && world.Contains("(c.Owner == CarParkOwner.Player || !GoingPublic.InSession)")
              && world.Contains("!GoingPublic.InSession, 100 - CarParkRules.Occupied(c.Cars, c.Bays)"),
            "car park sources: a Going Public session builds, lets and leases nothing, the city's builders rest, and no lane of the game's parking is copied (the host's parked cars would be sent to the others)");
        Check(world.Contains("SceneFinds.Black && (CarParkGame.InView(site, layout) || CarParkGame.PlayerOn(site))") && world.Contains("ConstructionBridges.GroundGiven")
              && game.Contains("CleanupParkedVehicles(force: true)") && game.Contains("root.SetActive(false)") && game.Contains("root.SetActive(true)"),
            "car park sources: a car park stands out of view or at a black screen, whole (asleep, then woken together), the works' props come down with it, and its lanes give their cars back to the pool before they go");
        Check(game.Contains("ParkingSimulator.parkingQueueWorker.AddWork(lane)") && game.Contains("lane.chanceOfFreeSpot = freePercent") && game.Contains("ChangedBy = 12") && game.Contains("GameContract.LaneSideBySide.Field")
              && game.Contains("Set(SideField, lane, true)") && game.Contains("lane.isHandicapParking = false"),
            "car park sources: the bays are lanes of the game's parking set to park side by side, their cars drawn again by the game's own queue when the share of free bays moves a dozen points");
        Check(contract.Contains("LaneSideAngle = Field(CarParks") && contract.Contains("LaneInverseChance = Field(CarParks") && contract.Contains("LaneDepthOffset = Field(CarParks") && contract.Contains("LaneMinFree = Field(CarParks")
              && contract.Contains("LaneSideBySide = Field(On(BusStops, CarParks)") && contract.Contains("ClickCarPark") && contract.Contains("GamePart CarParks = ContractBook.Part(\"car_parks\""),
            "car park sources: every member of the game's parking lane and its calls to action a car park reads is named in the contract");
        Check(pointer.Contains("customOverlayHeaderKey = Header") && pointer.Contains("_list.Insert(0, _behaviour)") && pointer.Contains("entityController is CarParkPointer") && pointer.Contains("GameContract.CtaBehaviours.Field")
              && pointer.Contains("NavMesh.SamplePosition"),
            "car park sources: the pay machine is the game's own interaction: a pointer on the booth, a call to action first in the game's list for it alone, a walk up to it on the walking mesh");
        Check(desk.Contains("NavigationBlocker.SpecialEmployeeDialog") && desk.Contains("BuilderDeskGame.Hold()") && desk.Contains("CallDialogFactory.RegisterDialog") && !desk.Contains("new GameObject"),
            "car park sources: the machine's dialogue is the game's own panel, paused as the builders' desks pause it, and makes no object of its own");
        Check(projects.Contains("DecidePark(p);") && projects.Contains("StartParkSite(p, contractor, out why)") && projects.Contains("ParkOpened(p, day);") && projects.Contains("CarParkRules.PermitDays")
              && projects.Contains("return ParkPreview(p, out limit);") && projects.Contains("CarParkRules.Quote(p.Firm, sheet.Total)") && projects.Contains("ParkCovers(p)")
              && hire.Contains("p.Kind == ProjectKind.Parking") && sites.Contains("ParkPace(siteId, done, needed, hours, out pace)") && sites.Contains("_state.CarParkSite(siteId) != null") && sites.Contains("ParkSiteHanded(siteId);"),
            "car park sources: a car park is a project of the normal pipeline, made fast: a day of plans, City Hall's one working day, a builder hired or crews, and the keys open it; the city's own go by the programme's schedule");
        Check(join.Contains("ParkOf(firstId)") && projects.Contains("ParkOf(lotId) != null ? \"reallife_park_answer_stands\"") && ModSource(@"Construction\ConstructionDirector.Actions.cs").Contains("ParkUnderWayOn(lot)")
              && ModSource(@"Construction\ConstructionDirector.Market.cs").Contains("ParkUnderWayOn(lot.Id)") && day.Contains("ParkOf(l.Id) == null"),
            "car park sources: land under a car park is not built on, joined, split, taken by the city's other builders or taxed as empty");
        Check(window.Contains("Settings.ConstructionParking") && window.Contains("WriteParkManage(s);") && window.Contains("WritePark(s);") && window.Contains("ActGrow(id) || ActPark(id)") && window.Contains("WriteParkClose(s, park, demolish)")
              && buildMode.Contains("ParkRead();") && buildMode.Contains("_d.ParkOfColumn(column) == null") && window.Contains("!_parkMode && _parkPicked == null"),
            "car park sources: build mode's Place panel has the Car park button, the new car park's panel, the panel of one that stands and Bulldoze closing it");
        Check(mode.Contains("Ask(\"reallife_park_confirm\"") && mode.Contains("Ask(\"reallife_park_close_confirm\"") && mode.Contains("Ask(\"reallife_park_lease_confirm\"") && mode.Contains("Ask(\"reallife_park_confirm_public\""),
            "car park sources: every cost or loss in build mode is asked in the game's question box first");
        Check(run.Contains("\"reallife_transaction_park_income\"") && run.Contains("\"reallife_transaction_park_upkeep\"") && run.Contains("\"reallife_transaction_park_refund\"") && run.Contains("\"reallife_transaction_park_lease_income\"")
              && run.Contains("\"reallife_transaction_park_lease\"") && run.Contains("\"reallife_transaction_park_lease_back\""),
            "car park sources: the takings, the upkeep, the street rate paid back, the leases in and out and their refunds are lines of their own in the game's books");
        Check(npc.Contains("DevelopmentRules.Rival(") && npc.Contains("StartLotSite(") && npc.Contains("_state.CityLand += SitePrice(") && npc.Contains("CarParkRules.NpcWants(") && Regex.IsMatch(run, @"(?m)^\s+ParkNpcDay\(day\);")
              && npc.Contains("CarParkGame.PickMouth("), "car park sources: the city's builders buy free land the council zones, check the way in, set out the programme's site and open it, by their own wish");
        Check(bridges.Contains("public static PlotStart StartLot;") && bridges.Contains("public static Action<float, float, float, float> GroundGiven;") && wiring.Contains("ConstructionBridges.StartLot = StartLot;")
              && wiring.Contains("ConstructionBridges.GroundGiven = ParkGroundGiven;") && wiring.Contains("ConstructionBridges.StartLot = null;") && wiring.Contains("ConstructionBridges.GroundGiven = null;")
              && dresser.Contains("_dressed[i].Site.Kind == SiteKind.Parking") && dresser.Contains("case SiteKind.Parking: BuildPark(dressed); break;") && works.Contains("StartLot(") && catalogue.Contains("Parking"),
            "car park sources: the programme's site for the works and the stand-down of its props are reached through the bridges, wired and let go");
        Check(store.Contains("WritePark(t, s);") && store.Contains("ReadPark(") && state.Contains("t.Append(\"^j:\");") && buildCatalogue.Contains("Parking = 7") && store.Contains("CarParkRules.WorksKind"),
            "car park sources: the car parks are a part of the construction line, written only once there is something in it, and the project kind is saved as a number");
        foreach (string t in new[] { "TestParkBuildNow", "TestParkCarNow", "TestParkIncomeNow", "TestParkLeaseNow", "TestParkCityNow" })
        {
            Check(tests.Contains("public static bool " + t + "()") && guide.Contains("ConstructionDirector." + t + "()"), "car park sources: the test " + t + " is in Options > Tests");
        }
        foreach (string line in new[] { "a lot built on your land at once", "your car parked in a bay", "a day of public income", "a lease offered, accepted and paid", "a builder of the city puts up a car park" })
        {
            Check(tests.Contains("const string test = \"" + line + "\";") && guide.Contains("\"Parking check (" + line + ")\""), "car park sources: the check line \"Parking check (" + line + ")\" is what the test writes and what the run looks for");
        }
        Check(tests.Contains("ParkAssess(") && tests.Contains("Decide(p);") && tests.Contains("HireBuilder(p,") && tests.Contains("FinishSite(p.SiteId)") && tests.Contains("ParkStandNow(c)") && tests.Contains("ParkNpcBegin(day,")
              && tests.Contains("ParkAccounts(c,") && tests.Contains("ParkOffersFrom(c, day, r, near, true)") && tests.Contains("ParkLeasesDay(c, day)") && tests.Contains("WheelsOver("),
            "car park sources: the tests go through the real pipeline, the real world and the real accounts, not stand-ins of them");
    }

    // ------------------------------------------------------------------ the texts

    private static void ParkTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string file in new[]
                 {
                     @"Construction\CarParkGame.cs", @"Construction\CarParkPointer.cs", @"Construction\CarParkDesk.cs", @"Construction\CarParkRules.cs", @"Construction\ConstructionDirector.Park.cs",
                     @"Construction\ConstructionDirector.ParkRun.cs", @"Construction\ConstructionDirector.ParkNpc.cs", @"Construction\ConstructionDirector.ParkWorld.cs", @"Construction\ConstructionDirector.ParkMode.cs",
                     @"Construction\ConstructionDirector.ParkTests.cs", @"Construction\BuildMode.Park.cs", @"Construction\BuildMode.Window.cs", @"Construction\ConstructionApp.Park.cs",
                     @"Construction\ConstructionApp.Land.cs", @"Construction\ConstructionDirector.Projects.cs", @"Construction\ConstructionDirector.Join.cs", @"Construction\ConstructionGuide.cs"
                 })
        {
            foreach (Match m in Regex.Matches(ModSource(file), "\"(reallife_(?:park|transaction_park)_[a-z0-9_]*[a-z0-9])\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        foreach (string t in new[] { "build", "car", "income", "lease", "city" })
        {
            keys.Add("reallife_park_test_" + t);
        }
        keys.Add("reallife_opt_tip_construction_parking");
        keys.Add("reallife_transaction_park_upkeep_label");
        keys.Add("reallife_transaction_park_lease_label");
        keys.Add("reallife_transaction_park_lease_back_label");
        Check(keys.Count >= 120, "car park texts: their keys are found: " + keys.Count);
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("car park texts", words, codes))
        {
            return;
        }
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            string missing = null;
            foreach (string key in keys)
            {
                if (!words[l].TryGetValue(key, out string value) || value.Length == 0 || value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Contains(" - ")
                    || value.Contains("  "))
                {
                    missing = missing ?? key;
                }
                else if (l > 0 && words[0].TryGetValue(key, out string en) && NamesOf(en) != NamesOf(value))
                {
                    missing = missing ?? key + " (its names in curly brackets)";
                }
            }
            Check(missing == null, "car park texts: every key is in " + codes[l] + ".json, in the style, with the English names (" + (missing ?? "all") + ")");
        }
        foreach (string key in keys.Where(k => k.StartsWith("reallife_park_notice_", StringComparison.Ordinal) || k.StartsWith("reallife_park_answer_", StringComparison.Ordinal) || k == "reallife_park_closed"))
        {
            for (int l = 0; l < words.Length && l < 3; l++)
            {
                string text = words[l].TryGetValue(key, out string t) ? t : string.Empty;
                Check(text.Length > 0 && text.Length <= 240, "car park texts: " + key + " at most 240 characters in " + codes[l] + " (" + text.Length + ")");
            }
        }

        // The names a text asks for are the names the call gives it.
        string[] files =
        {
            @"Construction\CarParkDesk.cs", @"Construction\BuildMode.Park.cs", @"Construction\ConstructionApp.Park.cs", @"Construction\ConstructionDirector.ParkMode.cs", @"Construction\ConstructionDirector.Park.cs"
        };
        int calls = 0;
        var wrong = new List<string>();
        foreach (string file in files)
        {
            string source = ModSource(file);
            foreach (Match m in Regex.Matches(source, @"(?:ConstructionText\.)?(?:Fill|T)\(\s*""(reallife_park_[a-z0-9_]+)""\s*,"))
            {
                int open = source.IndexOf('(', m.Index + (m.Value.StartsWith("ConstructionText", StringComparison.Ordinal) ? "ConstructionText.".Length : 0));
                string args = BalancedArguments(source, open);
                List<string> parts = SplitTopLevel(args);
                var given = new List<string>();
                for (int i = 1; i + 1 < parts.Count; i += 2)
                {
                    Match name = Regex.Match(parts[i].Trim(), "^\"([a-z0-9_]+)\"$");
                    if (name.Success)
                    {
                        given.Add("{" + name.Groups[1].Value + "}");
                    }
                }
                given.Sort(StringComparer.Ordinal);
                if (words[0].TryGetValue(m.Groups[1].Value, out string text))
                {
                    calls++;
                    string want = NamesOf(text);
                    if (want != string.Join(",", given))
                    {
                        wrong.Add(m.Groups[1].Value + " (asks " + want + ", given " + string.Join(",", given) + ")");
                    }
                }
            }
        }
        Check(calls >= 30 && wrong.Count == 0, "car park texts: " + calls + " texts are filled with the names they ask for (" + (wrong.Count == 0 ? "all" : string.Join("; ", wrong.Take(3))) + ")");
        // The notices, papers and books take {name} (and {count}); the dictionaries built in the director give the rest.
        var fixedNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "reallife_park_notice_applied", "{name}" }, { "reallife_park_notice_granted", "{name}" }, { "reallife_park_notice_open", "{bays},{name},{price}" },
            { "reallife_park_notice_offer", "{amount},{bays},{name},{who}" }, { "reallife_park_notice_lease_over", "{bays},{name},{who}" }, { "reallife_park_notice_lease_unpaid", "{bays},{name},{who}" },
            { "reallife_park_paper_bought", "{count},{name}" }, { "reallife_park_paper_started", "{count},{name}" }, { "reallife_park_paper_open", "{count},{name}" }, { "reallife_park_paper_closed", "{name}" },
            { "reallife_transaction_park_income", "{name}" }, { "reallife_transaction_park_upkeep", "{name}" }, { "reallife_transaction_park_refund", "{name}" },
            { "reallife_transaction_park_lease_income", "{name}" }, { "reallife_transaction_park_lease", "{name}" }, { "reallife_transaction_park_lease_back", "{name}" },
            { "reallife_park_name", "{address}" }, { "reallife_park_mode_public", "{price}" }, { "reallife_park_lease_confirm_body", "{amount}" }, { "reallife_park_confirm_body", "{amount}" },
            { "reallife_park_confirm_public_amount", "{amount}" }, { "reallife_park_confirm_public_body", "{count}" }, { "reallife_park_desk_lease_n", "{count}" }
        };
        string fault = null;
        foreach (KeyValuePair<string, string> pair in fixedNames)
        {
            for (int l = 0; l < words.Length && l < 3; l++)
            {
                if (!words[l].TryGetValue(pair.Key, out string text) || NamesOf(text) != pair.Value)
                {
                    fault = fault ?? pair.Key + " in " + codes[l];
                }
            }
        }
        Check(fault == null, "car park texts: the notices, papers, books and questions carry the names the director gives them (" + (fault ?? "all") + ")");

        // The Guide section has every fact it asks for, in en, pt and zh-cn; the lines of each are the same.
        string[] facts =
        {
            "park_plans_days", "park_permit_days", "park_works_least", "park_works_most", "park_homes_bays", "park_near", "park_band_least", "park_band_most", "park_upkeep_public", "park_upkeep_leased",
            "park_reach", "park_lease_bays_most", "park_offer_days", "park_lease_days"
        };
        string guideSource = ModSource(@"Construction\ConstructionGuide.cs");
        var pageParts = new string[Math.Min(3, words.Length)][];
        for (int l = 0; l < pageParts.Length; l++)
        {
            string page = words[l].TryGetValue("reallife_guide_construction", out string g) ? g : string.Empty;
            int at = page.LastIndexOf("\n# ", StringComparison.Ordinal);
            string section = at >= 0 ? page.Substring(at) : string.Empty;
            pageParts[l] = section.Split('\n');
            bool named = facts.All(f => section.Contains("{" + f + "}"));
            Check(section.Length > 400 && named && (l != 0 || section.StartsWith("\n# Car parks\n", StringComparison.Ordinal)),
                "car park texts: the Guide's last section in " + codes[l] + " is the car parks', with its figures");
        }
        foreach (string f in facts)
        {
            Check(guideSource.Contains(".Fact(\"" + f + "\""), "car park texts: the Guide fact " + f + " is registered");
        }
        bool sameLines = pageParts.Length == 3 && pageParts[0].Length == pageParts[1].Length && pageParts[0].Length == pageParts[2].Length;
        for (int i = 0; sameLines && i < pageParts[0].Length; i++)
        {
            sameLines &= pageParts[0][i].StartsWith("- ", StringComparison.Ordinal) == pageParts[1][i].StartsWith("- ", StringComparison.Ordinal)
                         && pageParts[0][i].StartsWith("- ", StringComparison.Ordinal) == pageParts[2][i].StartsWith("- ", StringComparison.Ordinal) && NamesOf(pageParts[0][i]) == NamesOf(pageParts[1][i])
                         && NamesOf(pageParts[0][i]) == NamesOf(pageParts[2][i]);
        }
        Check(sameLines, "car park texts: the Guide section has the same lines, bullets and figures in en, pt and zh-cn");

        // What's new: the feature line once in the next row of every language, among the feature lines, before any fix.
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Car parks:" }, { "pt", ParkStart("pt") }, { "zh-cn", ParkStart("zh-cn") }, { "de", "Parkplätze:" }, { "fr", "Parkings :" }, { "es", "Aparcamientos:" }, { "ru", "Парковки:" },
            { "nl", "Parkeerterreinen:" }, { "tr", "Otoparklar:" }, { "pl", "Parkingi:" }, { "ko", "주차장:" }
        };
        string[] marks = { "Fixed: ", "Corrigido: ", "已修复：", "Behoben: ", "Corrigé : ", "Corregido: ", "Исправлено: ", "Opgelost: ", "Düzeltildi: ", "Naprawiono: ", "수정: " };
        for (int l = 0; l < words.Length; l++)
        {
            string start = starts.TryGetValue(codes[l], out string s) ? s : "?";
            bool has = words[l].TryGetValue("reallife_changes_next", out string next) && start.Length > 3 && next.Contains(start);
            Check(has, "car park texts: the next row of What's new has the car parks' line in " + codes[l]);
            if (has)
            {
                int at = next.IndexOf(start, StringComparison.Ordinal);
                bool fixBefore = marks.Any(mark => { int f = next.IndexOf(mark, StringComparison.Ordinal); return f >= 0 && f < at; });
                Check(next.IndexOf(start, at + 1, StringComparison.Ordinal) < 0 && !fixBefore && !next.Contains("  "),
                    "car park texts: its line once in the next row, among the feature lines (no fix line before it), no double space, in " + codes[l]);
            }
        }
    }

    private static string ParkStart(string code) => code == "pt" ? "Estacionamentos:" : code == "zh-cn" ? "停车场：" : "?";

    // The text between a call's brackets, from the opening one.
    private static string BalancedArguments(string source, int open)
    {
        int depth = 0;
        bool quoted = false;
        for (int i = open; i < source.Length; i++)
        {
            char c = source[i];
            if (c == '"' && (i == 0 || source[i - 1] != '\\'))
            {
                quoted = !quoted;
            }
            else if (!quoted && c == '(')
            {
                depth++;
            }
            else if (!quoted && c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(open + 1, i - open - 1);
                }
            }
        }
        return string.Empty;
    }

    // A call's arguments divided at the commas that are not inside brackets or quotes.
    private static List<string> SplitTopLevel(string args)
    {
        var parts = new List<string>();
        int depth = 0, from = 0;
        bool quoted = false;
        for (int i = 0; i < args.Length; i++)
        {
            char c = args[i];
            if (c == '"' && (i == 0 || args[i - 1] != '\\'))
            {
                quoted = !quoted;
            }
            else if (!quoted && (c == '(' || c == '{' || c == '['))
            {
                depth++;
            }
            else if (!quoted && (c == ')' || c == '}' || c == ']'))
            {
                depth--;
            }
            else if (!quoted && depth == 0 && c == ',')
            {
                parts.Add(args.Substring(from, i - from));
                from = i + 1;
            }
        }
        parts.Add(args.Substring(from));
        return parts;
    }
}

