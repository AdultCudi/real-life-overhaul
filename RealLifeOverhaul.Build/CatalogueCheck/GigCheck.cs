using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Offline checks of the gigs' pay table (JobCatalogue, CareerCatalogue) and of the offers drawn for them
    /// (GigOffers): the ranges are kept and the averages are the table's, over thousands of seeded draws.
    /// </summary>
    internal static class GigCheck
    {
        private const double Cashier = 25.0;      // what the game's own job pays an hour (WorkActivity)
        private const double VanJob = 100.0;      // the game's delivery van job, an hour
        private const int Days = 3000;

        public static void Run(Action<string> fail, Dictionary<string, string> locale)
        {
            Console.WriteLine("\n== Gigs: the pay table ==");
            JobDef[] gigs = JobCatalogue.All;
            var hours = new Dictionary<string, double>();
            foreach (JobDef def in gigs)
            {
                double shift = def.ShiftUnits > 0 ? def.UnitPay * def.ShiftUnits : def.UnitPay * GigOffers.TaxiShift + def.PayPerMetre * 600.0 * GigOffers.TaxiShift;
                if (def.PayPerMetre > 0 && def.ShiftUnits > 0)
                {
                    shift += def.PayPerMetre * 800.0;   // a round's distance
                }
                double hour = shift / 4.0;
                if (def.Id == JobCatalogue.Taxi)
                {
                    hour = (def.UnitPay + def.PayPerMetre * 600.0) / (80.0 / 60.0);   // a 600 m fare is about 80 s of driving, and a real minute is a game hour
                }
                hours[def.Id] = hour;
                Console.WriteLine($"  {def.Id,-8} shift {shift,6:0}  an hour of the shift {hour,5:0.0}");
                bool skilled = def.Id == JobCatalogue.Courier || def.Id == JobCatalogue.Taxi;
                if (def.Id != JobCatalogue.Busker && hour < Cashier * 1.5) fail($"{def.Id}: {hour:0.0} an hour is not clearly more than the game's cashier ({Cashier})");
                if (def.Id == JobCatalogue.Busker && hour < Cashier) fail($"busker: {hour:0.0} an hour is under the cashier");
                if (hour > VanJob * 1.1) fail($"{def.Id}: {hour:0.0} an hour is over the game's van job ({VanJob})");
                if (skilled && hour < 70) fail($"{def.Id}: a skilled gig pays only {hour:0.0} an hour");
                if (!locale.ContainsKey(def.PitchKey)) fail($"{def.Id}: pitch key missing");
            }
            double unskilledMax = gigs.Where(g => g.Needs == JobNeed.Outdoors && g.Id != JobCatalogue.Busker).Max(g => g.UnitPay * g.ShiftUnits) / 4.0;
            double skilledMin = gigs.Where(g => (g.Needs & JobNeed.Car) != 0).Min(g => hours[g.Id]);
            Console.WriteLine($"  best unskilled an hour {unskilledMax:0.0}, worst skilled an hour {skilledMin:0.0}");
            if (skilledMin <= unskilledMax) fail("a skilled gig (courier, ride-hail) pays no more an hour than the best unskilled one");
            double flyerUnit = gigs.Where(g => g.Needs == JobNeed.Outdoors).Max(g => g.UnitPay);
            foreach (JobDef desk in JobCatalogue.Career)
                if (desk.UnitPay <= flyerUnit) fail($"{desk.Id}: a desk unit ({desk.UnitPay}) pays no more than an unskilled gig's ({flyerUnit})");
            foreach (Employer employer in CareerCatalogue.All)
                foreach (CareerRank rank in employer.Ranks)
                    if (rank.Hourly > 0 && rank.Hourly * 4.0 > 400.0) fail($"{employer.Id}: a basic wage of {rank.Hourly} an hour is over the van job's per-shift scale");
            double cartNet = CareerCatalogue.CartTakings - CareerCatalogue.CartStock;
            double vendorUnit = JobCatalogue.Find(JobCatalogue.Vendor).UnitPay;
            Console.WriteLine($"  own cart nets {cartNet:0.0} a customer against the gig's {vendorUnit:0.0}; rent {CareerCatalogue.CartRent}");
            if (cartNet < vendorUnit || cartNet > vendorUnit * 1.4) fail("the own cart's net per customer is not about the gig's pay");
            if (CareerCatalogue.CartRent > cartNet * JobCatalogue.Find(JobCatalogue.Vendor).ShiftUnits) fail("the cart's daily rent is more than a shift of customers nets");

            Console.WriteLine("\n== Gigs: the day's board ==");
            int shut = 0, busyTotal = 0, offeredDays = 0;
            var busyCounts = new Dictionary<int, int>();
            for (int day = 1; day <= Days; day++)
            {
                int closed = 0, busy = 0;
                foreach (JobDef def in gigs)
                {
                    GigDay d = GigOffers.DayOf(def.Id, day);
                    if (d == GigDay.Closed) closed++;
                    if (d == GigDay.Busy) busy++;
                    if (d != GigOffers.DayOf(def.Id, day)) fail("the day's board changed between two asks");
                }
                if (closed > 1) fail($"day {day}: {closed} gigs have nothing on");
                if (busy < 2 || busy > 3) fail($"day {day}: {busy} busy gigs");
                shut += closed;
                busyTotal += busy;
                busyCounts[busy] = busyCounts.GetValueOrDefault(busy) + 1;
                offeredDays += gigs.Length - closed;
            }
            double busyAvg = busyTotal / (double)Days, shutAvg = shut / (double)Days;
            Console.WriteLine($"  over {Days} days: busy {busyAvg:0.000} a day (wanted {GigOffers.BusyPerDay}), nothing on {shutAvg:0.000} a day (wanted {GigOffers.ClosedPerDay}); busy days of 2: {busyCounts.GetValueOrDefault(2)}, of 3: {busyCounts.GetValueOrDefault(3)}");
            if (Math.Abs(busyAvg - GigOffers.BusyPerDay) > 0.06) fail("the average number of busy gigs a day is not 2.5");
            if (Math.Abs(shutAvg - GigOffers.ClosedPerDay) > 0.06) fail("the average number of gigs with nothing on is not 0.5");

            Console.WriteLine("\n== Gigs: the offers ==");
            double overallMultiplier = 0;
            int overallCount = 0;
            foreach (JobDef def in gigs)
            {
                GigEvents events = GigOffers.EventsOf(def.Id);
                int span = def.ShiftUnits > 0 ? def.ShiftUnits : GigOffers.TaxiShift;
                double client = 0, dayFactor = 0, units = 0, crowd = 0, total = 0, feeSum = 0, minFee = 9, maxFee = 0;
                int n = 0, tippers = 0, speeds = 0, redos = 0, busy = 0, quiet = 0, rush = 0, tight = 0, generous = 0;
                var fees = new HashSet<int>();
                for (int day = 1; day <= Days; day++)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        int hour = (day * 7 + k * 11) % 24;
                        GigOffer a = GigOffers.Draw(def, day, hour, GigWeather.Fair);
                        GigOffer b = GigOffers.Draw(def, day, hour, GigWeather.Fair);
                        if (a.Seed != b.Seed || a.Fee != b.Fee || a.Units != b.Units || a.TipperAt != b.TipperAt || a.RedoAt != b.RedoAt || a.Crowd != b.Crowd)
                            fail($"{def.Id}: the same day and hour gave two offers");
                        if (a.Day != GigOffers.DayOf(def.Id, day)) fail($"{def.Id}: the offer's day is not the board's");
                        if (!a.Open) continue;
                        n++;
                        client += a.ClientFactor;
                        dayFactor += a.DayFactor;
                        crowd += a.CrowdFactor;
                        units += a.Units;
                        feeSum += a.Fee;
                        minFee = Math.Min(minFee, a.Fee);
                        maxFee = Math.Max(maxFee, a.Fee);
                        fees.Add((int)(a.Fee * 1000));
                        if (a.ClientFactor < GigOffers.ClientLow - 1e-6 || a.ClientFactor > GigOffers.ClientHigh + 1e-6) fail($"{def.Id}: client factor {a.ClientFactor} out of range");
                        if (def.ShiftUnits > 0 && (a.Units < def.ShiftUnits - def.UnitsSpread || a.Units > def.ShiftUnits + def.UnitsSpread)) fail($"{def.Id}: size {a.Units} out of range");
                        if (def.ShiftUnits == 0 && a.Units != 0) fail($"{def.Id}: a gig with no set size got one");
                        int size = a.Units > 0 ? a.Units : GigOffers.TaxiShift;
                        if (a.TipperAt != 0 && ((events & GigEvents.Tipper) == 0 || a.TipperAt < 1 || a.TipperAt > size)) fail($"{def.Id}: tipper at {a.TipperAt}");
                        if (a.TipperAt != 0 && (a.TipperUnits < GigOffers.TipperLow - 1e-6 || a.TipperUnits > GigOffers.TipperHigh + 1e-6)) fail($"{def.Id}: tip of {a.TipperUnits} units");
                        if (a.SpeedUnits != 0 && ((events & GigEvents.Speed) == 0 || a.SpeedUnits < GigOffers.SpeedLow - 1e-6 || a.SpeedUnits > GigOffers.SpeedHigh + 1e-6)) fail($"{def.Id}: speed bonus {a.SpeedUnits}");
                        if (a.RedoAt != 0 && ((events & GigEvents.Redo) == 0 || a.RedoAt < 2 || a.RedoAt > Math.Max(2, size))) fail($"{def.Id}: redo at {a.RedoAt}");
                        if (a.Crowd != GigCrowd.Normal && (events & GigEvents.Crowd) == 0) fail($"{def.Id}: a crowd for a gig without one");
                        if (a.Weather != GigWeather.Fair) fail($"{def.Id}: weather pay in fair weather");
                        double extra = (a.TipperAt > 0 ? a.TipperUnits : 0) + a.SpeedUnits;
                        total += a.Fee * (size + extra) / span;   // the shift's pay as a multiple of the table's, the speed bonus taken as won
                        tippers += a.TipperAt > 0 ? 1 : 0;
                        speeds += a.SpeedUnits > 0 ? 1 : 0;
                        redos += a.RedoAt > 0 ? 1 : 0;
                        busy += a.Day == GigDay.Busy ? 1 : 0;
                        quiet += a.Crowd == GigCrowd.Quiet ? 1 : 0;
                        rush += a.Crowd == GigCrowd.Rush ? 1 : 0;
                        tight += a.Client == GigClient.Tight ? 1 : 0;
                        generous += a.Client == GigClient.Generous ? 1 : 0;
                    }
                }
                double tableUnits = def.ShiftUnits;
                Console.WriteLine($"  {def.Id,-8} {n} offers: fee {minFee:0.00} to {maxFee:0.00} (mean {feeSum / n:0.000}), client {client / n:0.000}, day {dayFactor / n:0.000}, crowd {crowd / n:0.000}, size {units / n:0.00} of {tableUnits}, whole shift {total / n:0.000} of the table");
                Console.WriteLine($"           tight {100.0 * tight / n:0}%, generous {100.0 * generous / n:0}%, busy {100.0 * busy / n:0}%, quiet {100.0 * quiet / n:0}%, rush {100.0 * rush / n:0}%, tipper {100.0 * tippers / n:0}%, speed {100.0 * speeds / n:0}%, redo {100.0 * redos / n:0}%, {fees.Count} distinct fees");
                if (Math.Abs(client / n - 1.0) > 0.015) fail($"{def.Id}: the client factor averages {client / n:0.000}, not 1");
                if (Math.Abs(dayFactor / n - 1.0) > 0.02) fail($"{def.Id}: the day factor averages {dayFactor / n:0.000}, not 1");
                if (Math.Abs(crowd / n - 1.0) > 0.015) fail($"{def.Id}: the crowd factor averages {crowd / n:0.000}, not 1");
                if (def.ShiftUnits > 0 && Math.Abs(units / n - def.ShiftUnits) > 0.03 * def.ShiftUnits) fail($"{def.Id}: the size averages {units / n:0.00}, not {def.ShiftUnits}");
                if (Math.Abs(total / n - 1.0) > 0.025) fail($"{def.Id}: a shift, events included, averages {total / n:0.000} of the table, not 1");
                if (fees.Count < 100) fail($"{def.Id}: only {fees.Count} different fees in {n} offers");
                if (def.ShiftUnits > 0 && def.UnitsSpread > 0 && units / n == def.ShiftUnits && n > 100) { /* exact mean is fine */ }
                overallMultiplier += total;
                overallCount += n;
            }
            Console.WriteLine($"  all gigs: a shift averages {overallMultiplier / overallCount:0.000} of the table's figure");
            if (Math.Abs(overallMultiplier / overallCount - 1.0) > 0.015) fail("over all gigs the pay does not average the table's figure");

            GigOffer career = GigOffers.Draw(JobCatalogue.Career[0], 5, 9, GigWeather.Fair);
            if (!career.Flat || career.Fee != 1f || career.Units != JobCatalogue.Career[0].ShiftUnits) fail("a career-only shift got an offer");
            GigOffer storm = GigOffers.Draw(JobCatalogue.Find(JobCatalogue.Flyers), 12, 14, GigWeather.Storm);
            GigOffer fair = GigOffers.Draw(JobCatalogue.Find(JobCatalogue.Flyers), 12, 14, GigWeather.Fair);
            if (Math.Abs(storm.Fee / fair.Fee - GigOffers.StormPay) > 1e-4) fail("storm pay is not the stated share more");
            GigOffer indoors = GigOffers.Draw(JobCatalogue.Find(JobCatalogue.Cleaner), 12, 14, GigWeather.Storm);
            if (indoors.Weather != GigWeather.Fair) fail("the cleaner, who works indoors, is paid weather pay");
            Console.WriteLine("  career shifts are flat; storm pay x" + GigOffers.StormPay + " out of doors only");

            foreach (string key in new[] { "reallife_gig_client_tight", "reallife_gig_client_fair", "reallife_gig_client_generous", "reallife_gig_day_busy", "reallife_gig_crowd_quiet",
                "reallife_gig_crowd_rush", "reallife_gig_weather_rain", "reallife_gig_weather_storm", "reallife_gig_weather_heat", "reallife_gig_weather_cold", "reallife_gig_weather_ice",
                "reallife_gig_quote", "reallife_gig_tipper", "reallife_gig_speed_offer", "reallife_gig_speed_paid", "reallife_gig_speed_missed", "reallife_gig_redo_cart",
                "reallife_gig_redo_clean", "reallife_job_no_today", "reallife_job_card_gig_offer", "reallife_job_slip_events", "reallife_guide_job_size" })
                if (!locale.TryGetValue(key, out string text) || string.IsNullOrWhiteSpace(text)) fail($"locale key {key} missing");
        }

        /// <summary>
        /// The booked set's venue fee paid as every job is, the jobs' refusals, the sign-on test's rota, and every prompt of
        /// the jobs and careers read by some source (Jobs/CareerCatalogue, and the sources as they ship).
        /// </summary>
        public static void Sources(Action<string> fail, Dictionary<string, string> locale, string src)
        {
            Console.WriteLine("\n== Gigs: the venue fee, the refusals, the test rota ==");
            int fee = CareerCatalogue.GigFee;
            if (CareerCatalogue.VenueFee(100, 0) != fee) fail($"venue fee: {CareerCatalogue.VenueFee(100, 0)} with no business at 100 %, wanted the table's {fee}");
            if (CareerCatalogue.VenueFee(100, 1) != (int)Math.Round(fee * 0.85f)) fail($"venue fee: {CareerCatalogue.VenueFee(100, 1)} for an owner of one business, wanted 85 % of {fee}");
            if (CareerCatalogue.VenueFee(100, 3) != (int)Math.Round(fee * 0.55f) || CareerCatalogue.VenueFee(100, 9) != CareerCatalogue.VenueFee(100, 3))
                fail("venue fee: an owner of three businesses or more is not paid 55 % of it");
            if (CareerCatalogue.VenueFee(200, 0) != fee * 2) fail("venue fee: the pay option at 200 % does not double it");
            if (CareerCatalogue.VenueFee(5, 0) != CareerCatalogue.VenueFee(25, 0) || CareerCatalogue.VenueFee(900, 0) != CareerCatalogue.VenueFee(300, 0))
                fail("venue fee: the pay option is not held to 25 to 300 %");
            if (CareerCatalogue.VenueFee(100, -1) != fee) fail("venue fee: a count of businesses under none is not read as none");

            for (int day = 1; day <= 14; day++)
            {
                CareerRota first = CareerCatalogue.Rotas[0];
                for (int i = CareerCatalogue.Rotas.Length - 1; i >= 0; i--)
                    if (CareerCatalogue.RotaDay(CareerCatalogue.Rotas[i].Days, day)) first = CareerCatalogue.Rotas[i];
                if (!ReferenceEquals(CareerCatalogue.FirstRotaOn(day), first)) fail($"test rota: day {day} gives a rota other than the first that works that day");
            }

            string jobs = Path.Combine(src, "Scripts", "Jobs");
            string gigs = File.ReadAllText(Path.Combine(jobs, "CareerDirector.Gigs.cs"));
            if (!gigs.Contains("CareerCatalogue.VenueFee(Settings.JobPay, BusinessGame.RevenueBusinesses())") || gigs.Contains("CareerCatalogue.GigFee"))
                fail("venue fee: a booked set's fee is not worked out through CareerCatalogue.VenueFee with the businesses run (CareerDirector.Gigs.cs)");
            string director = File.ReadAllText(Path.Combine(jobs, "JobDirector.cs"));
            int gig = director.IndexOf("public bool StartGig(", StringComparison.Ordinal);
            string startGig = gig >= 0 ? director.Substring(gig, Math.Min(700, director.Length - gig)) : "";
            int off = startGig.IndexOf("reallife_job_no_off", StringComparison.Ordinal);
            int busy = startGig.IndexOf("reallife_job_no_busy", StringComparison.Ordinal);
            if (off < 0 || busy < off || startGig.IndexOf("_job != null", StringComparison.Ordinal) < off)
                fail("refusals: a booked set with jobs off, faulted or not loaded is not refused as off, as a job and a rota shift are (JobDirector.StartGig)");
            string tests = File.ReadAllText(Path.Combine(jobs, "JobDirector.Tests.cs"));
            if (!tests.Contains("CareerCatalogue.FirstRotaOn(TimeHelper.CurrentDay + 1)"))
                fail("test rota: the sign-on test does not take the first rota that works tomorrow (JobDirector.Tests.cs)");

            // A prompt ({key}: ...) of the jobs and careers that no source names is a text nobody can see.
            var code = new System.Text.StringBuilder();
            foreach (string file in Directory.GetFiles(Path.Combine(src, "Scripts"), "*.cs", SearchOption.AllDirectories))
                code.Append(File.ReadAllText(file));
            string all = code.ToString();
            int prompts = 0;
            string unread = null;
            foreach (KeyValuePair<string, string> pair in locale)
            {
                if (!Regex.IsMatch(pair.Key, "^reallife_(job|career)_") || !pair.Value.StartsWith("{key}", StringComparison.Ordinal)) continue;
                prompts++;
                if (!all.Contains("\"" + pair.Key + "\"")) unread = (unread == null ? "" : unread + ", ") + pair.Key;
            }
            if (unread != null) fail($"prompts: {unread} named by no source");
            Console.WriteLine($"  venue fee {fee} at 100 % with no business; {prompts} job and career prompt(s), each named by a source");
        }
    }
}
