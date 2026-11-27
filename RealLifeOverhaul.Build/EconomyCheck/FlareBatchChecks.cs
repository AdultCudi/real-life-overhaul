using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Flare;

// Answering Flare's reviews in bulk and by a manager (a player's report of 26 Nov 2026: "add a way to batch answer business reviews on Flare, or to automate the
// process"): one press answers every waiting review by the rule a hand answer follows, a manager given the duty does it each morning, and the words are written
// for the kind of business. Run alone with --flarebatch29.
internal static partial class Program
{
    private static void FlareBatchChecks()
    {
        FlareBatchTable();
        FlareBatchNeverStands();
        FlareBatchCaps();
        FlareBatchPlanIsWhatHappens();
        FlareBatchWords();
        FlareBatchTick();
        FlareBatchManagers();
        FlareBatchSaves();
        FlareBatchSources();
        FlareBatchTexts();
    }

    private static FlareStarRow BatchRow(int one, int two, int three, int four, int five)
    {
        return new FlareStarRow { Key = "k", One = one, Two = two, Three = three, Four = four, Five = five };
    }

    // The way each star count is answered, written out apart from the rule: thanks for four and five, a word for a three, sorry for two and one, a poor review out of line left.
    private static void FlareBatchTable()
    {
        bool ways = true, fits = true;
        for (int stars = 1; stars <= 5; stars++)
        {
            for (int u = 0; u < 2; u++)
            {
                bool unfair = u == 1;
                FlareBatchWay want = stars >= 4 ? FlareBatchWay.Thanks : stars == 3 ? FlareBatchWay.Mixed : unfair ? FlareBatchWay.Left : FlareBatchWay.Sorry;
                FlareBatchWay got = FlareBatchRules.Way(stars, unfair);
                ways &= got == want;
                if (got != FlareBatchWay.Left)
                {
                    FlareFit fit = FlareReplyRules.Fit(FlareBatchRules.Option(got), stars, unfair);
                    fits &= stars == 3 ? fit == FlareFit.Stands : fit == FlareFit.Fits;   // a good or a fair poor review is fitted, a three stands and costs nothing
                }
            }
        }
        Check(ways, "Flare bulk: thanks for 4 and 5 stars, a word for a 3, sorry for 2 and 1, and a poor review out of line is left, for every star count");
        Check(fits, "Flare bulk: every answer it chooses fits its review (a 3 stands): none costs");
        Check(FlareBatchRules.Way(0, false) == FlareBatchRules.Way(1, false) && FlareBatchRules.Way(9, false) == FlareBatchRules.Way(5, false), "Flare bulk: stars out of range count as 1 and 5");
        Check(FlareBatchRules.Option(FlareBatchWay.Thanks) == FlareReplyOption.Thanks && FlareBatchRules.Option(FlareBatchWay.Mixed) == FlareReplyOption.Thanks
              && FlareBatchRules.Option(FlareBatchWay.Sorry) == FlareReplyOption.Sorry, "Flare bulk: the options a way answers with");
    }

    // Standing your ground is never chosen, over many businesses and many reviews, and every choice is what the hand rules would have chosen at that moment.
    private static void FlareBatchNeverStands()
    {
        var rnd = new Random(29);
        int sequences = 0, defended = 0, costs = 0, wrong = 0, lefts = 0;
        for (int t = 0; t < 400; t++)
        {
            var row = BatchRow(rnd.Next(0, 40), rnd.Next(0, 40), rnd.Next(0, 40), rnd.Next(0, 80), rnd.Next(0, 250));
            int n = rnd.Next(0, 90);
            var stars = new List<int>();
            for (int i = 0; i < n; i++)
            {
                stars.Add(rnd.Next(1, 6));
            }
            var plan = new FlareBatchPlan();
            FlareBatchRules.Plan(row, stars, int.MaxValue, plan);
            // The same walk written again by hand over its own copy of the row.
            var copy = BatchRow(row.One, row.Two, row.Three, row.Four, row.Five);
            for (int i = 0; i < n; i++)
            {
                bool unfair = stars[i] <= 2 && copy.Total >= 5 && FlareReplyRules.Average(copy) >= 4000;
                FlareBatchWay want = stars[i] >= 4 ? FlareBatchWay.Thanks : stars[i] == 3 ? FlareBatchWay.Mixed : unfair ? FlareBatchWay.Left : FlareBatchWay.Sorry;
                wrong += plan.Ways[i] == want ? 0 : 1;
                lefts += want == FlareBatchWay.Left ? 1 : 0;
                if (want == FlareBatchWay.Left)
                {
                    continue;
                }
                FlareReplyOption option = want == FlareBatchWay.Sorry ? FlareReplyOption.Sorry : FlareReplyOption.Thanks;
                defended += FlareBatchRules.Option(plan.Ways[i]) == FlareReplyOption.Defend ? 1 : 0;
                costs += FlareReplyRules.Fit(option, stars[i], unfair) == FlareFit.Costs ? 1 : 0;
                FlareReplyRules.Answered(copy, stars[i], FlareReplyRules.Quality(option, stars[i], unfair));
            }
            sequences++;
        }
        Check(sequences == 400 && wrong == 0, "Flare bulk: over 400 businesses and their reviews every choice is the one the hand rules make at that moment (" + wrong + " differ)");
        Check(defended == 0, "Flare bulk: never stands its ground, over 400 runs (" + defended + " stood)");
        Check(costs == 0, "Flare bulk: no answer it makes costs, over 400 runs (" + costs + " cost)");
        Check(lefts > 20, "Flare bulk: poor reviews out of line were met and left (" + lefts + ")");
        // A rated business: every poor review is out of line, so every one of them is left and the rating does not move.
        var good = BatchRow(1, 2, 0, 20, 80);
        var poor = new List<int> { 1, 2, 1, 5, 4 };
        var p = new FlareBatchPlan();
        FlareBatchRules.Plan(good, poor, 60, p);
        Check(p.Left == 3 && p.Thanks == 2 && p.Sorry == 0 && p.After == p.Before, "Flare bulk: a business rated 4.0 or more has its poor reviews left for the player, and they move nothing");
    }

    // However many reviews are answered the rating moves by MostLift at the most from the plain mean, and the cap really binds on a business with many poor reviews.
    private static void FlareBatchCaps()
    {
        var many = BatchRow(40, 0, 0, 0, 10);
        var stars = new List<int>();
        for (int i = 0; i < 40; i++)
        {
            stars.Add(1);
        }
        var plan = new FlareBatchPlan();
        FlareBatchRules.Plan(many, stars, int.MaxValue, plan);
        int plain = FlareReplyRules.Plain(many);
        Check(plan.Sorry == 40 && plain == 1800 && plan.After == plain + FlareReplyRules.MostLift, "Flare bulk: forty poor reviews answered lift a rating of " + plain + " by the cap and no more (" + plan.After + ")");
        var rnd = new Random(291);
        int over = 0, biggest = 0, runs = 0, tooMany = 0;
        for (int t = 0; t < 300; t++)
        {
            var row = BatchRow(rnd.Next(0, 120), rnd.Next(0, 60), rnd.Next(0, 60), rnd.Next(0, 100), rnd.Next(0, 300));
            var waiting = new List<int>();
            int n = rnd.Next(0, 250);
            for (int i = 0; i < n; i++)
            {
                waiting.Add(rnd.Next(1, 6));
            }
            // Presses of the most one press answers, until nothing waits: the copy is counted into with each press as the director counts the real row.
            var live = BatchRow(row.One, row.Two, row.Three, row.Four, row.Five);
            var rest = new List<int>(waiting);
            while (rest.Count > 0)
            {
                FlareBatchRules.Plan(live, rest, FlareBatchRules.Most, plan);
                tooMany += plan.Ways.Count > FlareBatchRules.Most || plan.Ways.Count == 0 ? 1 : 0;
                for (int i = 0; i < plan.Ways.Count; i++)
                {
                    if (plan.Ways[i] == FlareBatchWay.Left)
                    {
                        continue;
                    }
                    FlareReplyRules.Answered(live, rest[i], FlareReplyRules.Quality(FlareBatchRules.Option(plan.Ways[i]), rest[i], false));
                }
                rest.RemoveRange(0, plan.Ways.Count);
            }
            int lift = Math.Abs(FlareReplyRules.Average(live) - FlareReplyRules.Plain(live));
            biggest = Math.Max(biggest, lift);
            over += lift > FlareReplyRules.MostLift ? 1 : 0;
            runs++;
        }
        Check(tooMany == 0, "Flare bulk: a press makes at most " + FlareBatchRules.Most + " answers, and always makes progress (" + tooMany + " presses out of bounds)");
        Check(runs == 300 && over == 0, "Flare bulk: over 300 businesses with up to 250 waiting reviews the rating never leaves the plain mean by more than " + FlareReplyRules.MostLift + " (most " + biggest + ")");
    }

    // The preview is what the press does: its counts and its rating after are those of the answers made one by one, and what is past the most is counted, not answered.
    private static void FlareBatchPlanIsWhatHappens()
    {
        var row = BatchRow(6, 4, 5, 30, 40);
        var stars = new List<int>();
        for (int i = 0; i < 70; i++)
        {
            stars.Add(new[] { 5, 4, 3, 2, 1, 5, 4 }[i % 7]);
        }
        var plan = new FlareBatchPlan();
        FlareBatchRules.Plan(row, stars, FlareBatchRules.Most, plan);
        Check(plan.Ways.Count == FlareBatchRules.Most && plan.Beyond == 10 && plan.Answers + plan.Left == plan.Ways.Count, "Flare bulk: a plan answers the most a press can and counts the rest as waiting (" + plan.Beyond + ")");
        var copy = BatchRow(row.One, row.Two, row.Three, row.Four, row.Five);
        int before = FlareReplyRules.Average(copy);
        for (int i = 0; i < plan.Ways.Count; i++)
        {
            if (plan.Ways[i] != FlareBatchWay.Left)
            {
                FlareReplyRules.Answered(copy, stars[i], FlareReplyRules.Quality(FlareBatchRules.Option(plan.Ways[i]), stars[i], false));
            }
        }
        Check(plan.Before == before && plan.After == FlareReplyRules.Average(copy), "Flare bulk: the rating before and after in the preview are those the answers leave (" + plan.Before + " to " + plan.After + ")");
        int thanks = 0, sorry = 0, mixed = 0;
        for (int i = 0; i < plan.Ways.Count; i++)
        {
            thanks += stars[i] >= 4 ? 1 : 0;
            mixed += stars[i] == 3 ? 1 : 0;
            sorry += stars[i] <= 2 ? 1 : 0;
        }
        Check(plan.Thanks + plan.Left * 0 == thanks && plan.Mixed == mixed && plan.Sorry + plan.Left == sorry, "Flare bulk: the counts of a plan add up to the reviews it covers");
        var none = new FlareBatchPlan();
        FlareBatchRules.Plan(null, null, 60, none);
        Check(none.Ways.Count == 0 && none.Before == 0 && none.After == 0, "Flare bulk: a business with no stars and no reviews has an empty plan");
        var sum = new FlareBatchSummary();
        sum.Add(plan, "A");
        sum.Add(none, "B");
        Check(sum.Businesses == 1 && sum.Name == "A" && sum.Answers == plan.Answers, "Flare bulk: a summary counts only the businesses that had reviews, and names the one");
        var second = new FlareBatchPlan();
        FlareBatchRules.Plan(BatchRow(0, 0, 0, 5, 5), new List<int> { 5 }, 60, second);
        sum.Add(second, "C");
        Check(sum.Businesses == 2 && sum.Name == null && sum.Before == (plan.Before + second.Before) / 2, "Flare bulk: across businesses the rating is the average of theirs");
    }

    // The words: every variant is written, differs, carries the reviewer and the business, and a run of answers does not repeat.
    private static void FlareBatchWords()
    {
        Dictionary<string, string> en = LocaleFile("en");
        var all = new HashSet<string>();
        int missing = 0, braces = 0, tooLong = 0, unnamed = 0, total = 0;
        string[][][] tables = { FlareReplyWords.Thanks, FlareReplyWords.Sorry, FlareReplyWords.Mixed };
        int[] per = { FlareReplyWords.ThanksPer, FlareReplyWords.SorryPer, FlareReplyWords.MixedPer };
        for (int k = 0; k < tables.Length; k++)
        {
            Check(tables[k].Length == FlareReplyWords.Sets, "Flare words: every kind has a row of variants for each of the " + FlareReplyWords.Sets + " sets of businesses");
            for (int s = 0; s < tables[k].Length; s++)
            {
                Check(tables[k][s].Length == per[k], "Flare words: set " + s + " has " + per[k] + " variants of kind " + k);
                foreach (string key in tables[k][s])
                {
                    all.Add(key);
                    total++;
                    if (!en.TryGetValue(key, out string text) || string.IsNullOrWhiteSpace(text))
                    {
                        missing++;
                        continue;
                    }
                    string filled = text.Replace("{name}", "Bartholomew").Replace("{business}", "The Original Corner Cafe And Bakery Of Midtown");
                    braces += filled.Contains("{") || filled.Contains("}") ? 1 : 0;
                    tooLong += filled.Length > FlareLiveRules.TextMost ? 1 : 0;
                    unnamed += text.Contains("{name}") || text.Contains("{business}") ? 0 : 1;
                }
            }
        }
        Check(all.Count == total && total == 105 && missing == 0, "Flare words: 105 different variants, every one written (" + missing + " missing, " + all.Count + " distinct keys)");
        Check(braces == 0 && tooLong == 0, "Flare words: each variant fills in whole and fits in a reply of " + FlareLiveRules.TextMost + " characters, with a long business name (" + braces + " unfilled, " + tooLong + " too long)");
        Check(unnamed * 10 <= total * 3, "Flare words: at least seven in ten variants use the reviewer's or the business's name (" + unnamed + " of " + total + " use neither)");
        string[] markers = { "kitchen", "stitch", "weights", "dance floor", "ticket", "professional" };
        bool own = true, other = false;
        for (int s = 0; s < markers.Length; s++)
        {
            for (int t = 0; t < FlareReplyWords.Sets; t++)
            {
                string joined = string.Empty;
                foreach (string key in FlareReplyWords.Thanks[t])
                {
                    joined += en[key] + " ";
                }
                foreach (string key in FlareReplyWords.Sorry[t])
                {
                    joined += en[key] + " ";
                }
                foreach (string key in FlareReplyWords.Mixed[t])
                {
                    joined += en[key] + " ";
                }
                bool has = joined.Contains(markers[s]);
                if (t == s)
                {
                    own &= has;
                }
                else
                {
                    other |= has;
                }
            }
        }
        Check(own && !other, "Flare words: a bakery's thanks, a law office's and a nightclub's read in their own words: each set has its own vocabulary and no other set has it");
        Check(FlareReplyWords.SetOf(FlareNiche.Food) != FlareReplyWords.SetOf(FlareNiche.Business) && FlareReplyWords.SetOf(FlareNiche.Nightlife) != FlareReplyWords.SetOf(FlareNiche.Business)
              && FlareReplyWords.SetOf(FlareNiche.Food) != FlareReplyWords.SetOf(FlareNiche.Nightlife) && FlareReplyWords.SetOf(FlareNiche.Fashion) != FlareReplyWords.SetOf(FlareNiche.Fitness)
              && FlareReplyWords.SetOf(FlareNiche.Tech) != FlareReplyWords.SetOf(FlareNiche.LocalNews), "Flare words: the niches of a business map to their own sets, and the local news niche to the shop's");
        bool inRange = true;
        foreach (FlareNiche niche in Enum.GetValues(typeof(FlareNiche)))
        {
            inRange &= FlareReplyWords.SetOf(niche) >= 0 && FlareReplyWords.SetOf(niche) < FlareReplyWords.Sets;
        }
        Check(inRange, "Flare words: every niche has a set");
        // The variant: all of them come up, none is rare, and a run never says the same twice running.
        FlareBatchWay[] kinds = { FlareBatchWay.Thanks, FlareBatchWay.Sorry, FlareBatchWay.Mixed };
        bool spread = true, repeats = false, deterministic = true;
        foreach (FlareBatchWay way in kinds)
        {
            int n = FlareReplyWords.Count(way);
            var seen = new int[n];
            int previous = -1;
            for (int i = 0; i < 3000; i++)
            {
                int v = FlareReplyWords.Variant(way, 77, i * 31 + 5, previous);
                deterministic &= v == FlareReplyWords.Variant(way, 77, i * 31 + 5, previous);
                repeats |= v == previous;
                seen[v]++;
                previous = v;
            }
            for (int v = 0; v < n; v++)
            {
                spread &= seen[v] > 3000 / n / 2 && seen[v] < 3000 / n * 2;
            }
        }
        Check(spread && !repeats && deterministic, "Flare words: every variant of every kind comes up in a fair share, none twice running, the same seed the same word");
        Check(FlareReplyWords.Tone(FlareBatchWay.Thanks, 0) == FlareTone.Cheerful && FlareReplyWords.Tone(FlareBatchWay.Thanks, 2) == FlareTone.Cheerful
              && FlareReplyWords.Tone(FlareBatchWay.Thanks, 3) == FlareTone.Proud && FlareReplyWords.Tone(FlareBatchWay.Thanks, 5) == FlareTone.Proud
              && FlareReplyWords.Tone(FlareBatchWay.Sorry, 0) == FlareTone.Honest && FlareReplyWords.Tone(FlareBatchWay.Mixed, 1) == FlareTone.Honest
              && FlareReplyRules.OptionOf(FlareReplyWords.Tone(FlareBatchWay.Thanks, 4)) == FlareReplyOption.Thanks && FlareReplyRules.OptionOf(FlareReplyWords.Tone(FlareBatchWay.Sorry, 1)) == FlareReplyOption.Sorry,
            "Flare words: thanks come in a cheerful and a proud voice, sorry and the word for a three in the honest one, and each tone reads as the option it answers with");
        Check(FlareBatchRules.FirstName("Dana Cole") == "Dana" && FlareBatchRules.FirstName("@dana_k") == "dana_k" && FlareBatchRules.FirstName(null) == "there"
              && FlareBatchRules.FirstName("?") == "there" && FlareBatchRules.FirstName("  Omar ") == "Omar", "Flare words: a reply says the reviewer's first name, and 'there' when none is known");
    }

    // The daily round as a manager makes it, on a small model of the waiting reviews: only the new ones, none twice, none too old, and the most a round answers.
    private sealed class BatchReview
    {
        public int Stars, Day;
        public bool Answered;
    }

    private static int BatchTick(int today, List<BatchReview> reviews, FlareStarRow row)
    {
        var waiting = new List<BatchReview>();
        var stars = new List<int>();
        foreach (BatchReview r in reviews)
        {
            if (!r.Answered && FlareBatchRules.Fresh(r.Day, today))
            {
                waiting.Add(r);
                stars.Add(r.Stars);
            }
        }
        var plan = new FlareBatchPlan();
        FlareBatchRules.Plan(row, stars, FlareBatchRules.Most, plan);
        int made = 0;
        for (int i = 0; i < plan.Ways.Count; i++)
        {
            if (plan.Ways[i] == FlareBatchWay.Left)
            {
                continue;
            }
            waiting[i].Answered = true;
            FlareReplyRules.Answered(row, stars[i], FlareReplyRules.Quality(FlareBatchRules.Option(plan.Ways[i]), stars[i], false));
            made++;
        }
        return made;
    }

    private static void FlareBatchTick()
    {
        var row = BatchRow(0, 0, 0, 0, 0);
        var reviews = new List<BatchReview>();
        int[] first = { 5, 5, 4, 3, 1 };
        foreach (int s in first)
        {
            reviews.Add(new BatchReview { Stars = s, Day = 10 });
            FlareReviewBook.Add(row, s);
        }
        Check(BatchTick(10, reviews, row) == 5, "Flare tick: the morning's round answers the five new reviews");
        Check(BatchTick(11, reviews, row) == 0, "Flare tick: the next day, with nothing new, answers nothing: no review is answered twice");
        reviews.Add(new BatchReview { Stars = 2, Day = 12 });
        reviews.Add(new BatchReview { Stars = 5, Day = 12 });
        FlareReviewBook.Add(row, 2);
        FlareReviewBook.Add(row, 5);
        int answeredBefore = reviews.FindAll(r => r.Answered).Count;
        Check(BatchTick(12, reviews, row) == 2 && reviews.FindAll(r => r.Answered).Count == answeredBefore + 2, "Flare tick: a day later the two new reviews are answered and only those");
        reviews.Add(new BatchReview { Stars = 1, Day = 12 - FlareBatchRules.AutoDays - 1 });
        FlareReviewBook.Add(row, 1);
        Check(BatchTick(12, reviews, row) == 0 && !reviews[reviews.Count - 1].Answered, "Flare tick: a review older than " + FlareBatchRules.AutoDays + " days is left to Answer all");
        // A busy day: more new reviews than a round answers; the rest are answered the next morning, none lost.
        var busy = new List<BatchReview>();
        var busyRow = BatchRow(0, 0, 0, 0, 0);
        for (int i = 0; i < 100; i++)
        {
            busy.Add(new BatchReview { Stars = 4 + i % 2, Day = 20 });
            FlareReviewBook.Add(busyRow, 4 + i % 2);
        }
        int one = BatchTick(20, busy, busyRow), two = BatchTick(21, busy, busyRow), three = BatchTick(22, busy, busyRow);
        Check(one == FlareBatchRules.Most && two == 100 - FlareBatchRules.Most && three == 0, "Flare tick: a busy day's reviews are answered across two mornings, " + FlareBatchRules.Most + " and then the rest (" + one + ", " + two + ")");
        // Unfair reviews wait without being answered, however many mornings pass.
        var proud = BatchRow(0, 0, 0, 20, 80);
        var calm = new List<BatchReview> { new BatchReview { Stars = 1, Day = 30 } };
        FlareReviewBook.Add(proud, 1);
        int days = 0;
        for (int d = 30; d < 34; d++)
        {
            days += BatchTick(d, calm, proud);
        }
        Check(days == 0 && !calm[0].Answered, "Flare tick: a poor review out of line with a business rated well is left for the player on every morning");
    }

    // The manager's duty as the rules have it: a shop's, at the staff hour every day, switched by the manager's own bit and the option, nothing for an old manager until it is given.
    private static void FlareBatchManagers()
    {
        Check((int)ManagerTask.Leases == 9 && (int)ManagerTask.Reviews == 10 && (int)ManagerTask.Count == 11, "Flare duty: the new kind is the eleventh, after the ten saved bits that keep their numbers");
        Check(ManagerRules.Has(ManagerRules.Kinds(ManagedKind.Shop), ManagerTask.Reviews) && !ManagerRules.Has(ManagerRules.Kinds(ManagedKind.Works), ManagerTask.Reviews)
              && !ManagerRules.Has(ManagerRules.Kinds(ManagedKind.Warehouse), ManagerTask.Reviews), "Flare duty: only a business with customers has it");
        int rounds = 0, other = 0, offRounds = 0, switchedOff = 0;
        int all = ManagerRules.Kinds(ManagedKind.Shop), without = all & ~ManagerRules.Bit(ManagerTask.Reviews);
        for (int day = 1; day <= 28; day++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                bool due = ManagerRules.Due(day, hour, ManagerTask.Reviews);
                rounds += due && ManagerRules.Runs(all, ManagedKind.Shop, ManagerTask.Reviews) && ManagerReviews.May(true, true) ? 1 : 0;
                other += due && hour != ManagerCatalogue.StaffHour ? 1 : 0;
                offRounds += due && ManagerRules.Runs(all, ManagedKind.Shop, ManagerTask.Reviews) && ManagerReviews.May(false, true) ? 1 : 0;
                offRounds += due && ManagerRules.Runs(all, ManagedKind.Shop, ManagerTask.Reviews) && ManagerReviews.May(true, false) ? 1 : 0;
                switchedOff += due && ManagerRules.Runs(without, ManagedKind.Shop, ManagerTask.Reviews) && ManagerReviews.May(true, true) ? 1 : 0;
            }
        }
        Check(rounds == 28 && other == 0, "Flare duty: a round every day at " + ManagerCatalogue.StaffHour + ":00 and never another hour, four weeks (" + rounds + ")");
        Check(offRounds == 0, "Flare duty: with the option off, or Flare off, nothing is answered in four weeks (" + offRounds + ")");
        Check(switchedOff == 0, "Flare duty: with the manager's own switch off, no round in four weeks");
        Check(!ManagerReviews.May(false, false) && !ManagerReviews.May(false, true) && !ManagerReviews.May(true, false) && ManagerReviews.May(true, true), "Flare duty: it runs only with its option on and Flare running");
        int old = (1 << 10) - 1;   // a manager saved before the duty has the ten kinds
        Check(!ManagerRules.Has(old, ManagerTask.Reviews) && !ManagerRules.Runs(old, ManagedKind.Shop, ManagerTask.Reviews), "Flare duty: a manager from an older save does not have the new duty until it is given");
        Check(ManagerRules.Has(ManagerRules.Kinds(ManagedKind.Shop), ManagerTask.Reviews), "Flare duty: a newly appointed manager of a business has it with the others");
        // The week's record.
        var w = new ReviewWeek();
        ManagerReviews.Record(w, 20, "Elm~7", 3800, 4, 1, 0);
        ManagerReviews.Record(w, 21, "Elm~7", 3900, 2, 0, 1);
        ManagerReviews.Record(w, 21, "Oak~3", 4200, 1, 0, 0);
        ManagerReviews.Record(w, 22, "Pine~1", 4000, 0, 0, 0);
        Check(w.From == 20 && w.Thanks == 7 && w.Sorry == 1 && w.Mixed == 1 && w.Answered == 9 && w.Places.Count == 2, "Flare duty: the week's answers are counted and each business is listed once");
        Check(w.Places[0].Before == 3800 && ManagerReviews.Before(w) == 4000, "Flare duty: the rating then is each business's at its first answer of the week, and the mean of them");
        ManagerReviews.Clear(w);
        Check(w.From == 0 && w.Answered == 0 && w.Places.Count == 0 && ManagerReviews.Before(w) == 0, "Flare duty: the week's record is cleared after its notice");
        int sundays = 0, evenings = 0;
        for (int day = 1; day <= 28; day++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                if (ManagerReviews.Due(day, hour))
                {
                    sundays++;
                    evenings += hour == ManagerCatalogue.DigestHour && ManagerRules.Weekday(day) == ManagerCatalogue.Sunday ? 1 : 0;
                }
            }
        }
        Check(sundays == 4 && evenings == 4, "Flare duty: the weekly notice is due once a week, on Sunday at " + ManagerCatalogue.DigestHour + ":00");
        var many = new ReviewWeek();
        for (int i = 0; i < ManagerReviews.PlacesMost + 10; i++)
        {
            ManagerReviews.Record(many, 5, "P" + i, 3000, 1, 0, 0);
        }
        Check(many.Places.Count == ManagerReviews.PlacesMost && many.Thanks == ManagerReviews.PlacesMost + 10, "Flare duty: the record follows at most " + ManagerReviews.PlacesMost + " businesses and still counts every answer");
        Check(ManagerRules.Allowance(ManagerCatalogue.RolePlace, 60, 100) == ManagerCatalogue.AllowanceBase[0] + ManagerCatalogue.AllowancePerSkill[0] * 60, "Flare duty: the manager's allowance is what it was, the duty adds no fee");
    }

    // The save: a line from before loads as it was and writes back the same, the week's part comes after and is skipped by what does not know it, a damaged one is ignored.
    private static void FlareBatchSaves()
    {
        var s = new ManagerState { MonthFrom = 33 };
        s.Managers.Add(new ManagerFile { Place = "Elm~7", Id = "m1", Name = "Mia Cole", Role = ManagerCatalogue.RolePlace, From = 3, Skill = 60, Tasks = (1 << 10) - 1 });
        Decision d = s.Add(34, 9, "Elm~7", ManagerTask.Leases, string.Empty, "Mia Cole");
        d.A = 1;
        string before = ManagerStore.Write(s);
        Check(!before.Contains("^w:"), "Flare save: a state with no week writes no week part, the very line an older version wrote");
        ManagerState back = ManagerStore.Read(before, out int version);
        Check(back != null && version == 1 && back.Managers[0].Tasks == (1 << 10) - 1 && !ManagerRules.Has(back.Managers[0].Tasks, ManagerTask.Reviews) && back.Week.From == 0 && back.Week.Answered == 0
              && ManagerStore.Write(back) == before, "Flare save: a line from before the duty loads with the duty not given, no week, and writes back unchanged");
        ManagerReviews.Record(s.Week, 35, "Elm~7", 3750, 5, 2, 1);
        ManagerReviews.Record(s.Week, 36, "Oak~3", 4100, 1, 0, 0);
        string line = ManagerStore.Write(s);
        Check(line.StartsWith(before, StringComparison.Ordinal) && line.Contains("^w:"), "Flare save: the week is a part of its own after every part an older reader knows");
        ManagerState round = ManagerStore.Read(line, out _);
        Check(round != null && round.Week.From == 35 && round.Week.Thanks == 6 && round.Week.Sorry == 2 && round.Week.Mixed == 1 && round.Week.Places.Count == 2 && round.Week.Places[1].Before == 4100
              && round.Managers.Count == 1 && round.Decisions.Count == 1 && ManagerStore.Write(round) == line, "Flare save: the week's record comes back as it was written");
        ManagerState damaged = ManagerStore.Read(before + "^w:x|y|-4|99999999999|zz*q/*3/ok*5", out _);
        Check(damaged != null && damaged.Managers.Count == 1 && damaged.Decisions.Count == 1 && damaged.Week.Thanks >= 0 && damaged.Week.Sorry >= 0 && damaged.Week.Places.Count <= 1,
            "Flare save: a damaged week part is bounded and the managers and decisions are still read");
        var big = new ManagerState();
        big.Managers.Add(new ManagerFile { Place = "Elm~7", Id = "m1", Name = "Mia" });
        for (int i = 0; i < 40; i++)
        {
            ManagerReviews.Record(big.Week, 5, "P" + i, 3000 + i, 1, 0, 0);
        }
        ManagerState bigBack = ManagerStore.Read(ManagerStore.Write(big), out _);
        Check(bigBack != null && bigBack.Week.Places.Count == ManagerReviews.PlacesMost, "Flare save: a week that followed many businesses keeps no more than " + ManagerReviews.PlacesMost);
        ManagerState decided = ManagerStore.Read(ManagerStore.Write(WithReviewDecision()), out _);
        Check(decided != null && decided.Decisions.Count == 1 && decided.Decisions[0].Task == (int)ManagerTask.Reviews && decided.Decisions[0].A == 6 && decided.Decisions[0].B == 2 && decided.Decisions[0].C == 1
              && decided.Decisions[0].D == 3 && decided.Decisions[0].E == 3800 && decided.Decisions[0].F == 3900 && decided.Decisions[0].G == 4 && decided.Decisions[0].State == DecisionState.Closed,
            "Flare save: a decision of the new duty keeps its figures (thanks, sorry, in between, left, rating before and after, not got to) in a round trip");
    }

    private static ManagerState WithReviewDecision()
    {
        var s = new ManagerState();
        s.Managers.Add(new ManagerFile { Place = "Elm~7", Id = "m1", Name = "Mia" });
        Decision d = s.Add(40, 9, "Elm~7", ManagerTask.Reviews, string.Empty, "Mia");
        d.A = 6;
        d.B = 2;
        d.C = 1;
        d.D = 3;
        d.E = 3800;
        d.F = 3900;
        d.G = 4;
        d.State = DecisionState.Closed;
        return s;
    }

    // What the code is made of: one rule for both ways, every answer by the path of a hand answer, only the player's businesses, nothing per frame, nothing sent.
    private static void FlareBatchSources()
    {
        string rules = ModSource(@"Social\Flare\FlareBatchRules.cs"), batch = ModSource(@"Social\Flare\FlareDirector.Batch.cs"), replies = ModSource(@"Social\Flare\FlareDirector.Replies.cs");
        string kit = ModSource(@"Social\Flare\FlareDirector.Kit.cs"), duty = ModSource(@"Economy\Company\CompanyDirector.Reviews.cs"), managers = ModSource(@"Economy\Company\CompanyDirector.Managers.cs");
        Check(!rules.Contains("Defend") && !batch.Contains("Defend") && !duty.Contains("Defend"), "Flare bulk: nothing in the bulk, the manager's or the plan's code names standing your ground");
        string round = FlareMethod(batch, "private int BatchRound(");
        Check(round.Contains("ReplyToNote(it.Note, option, key)") && round.Contains("ReplyToReview(it.Post.Id, option, words,") && round.Contains("FlareBatchRules.Plan(") && round.Contains("FlareBatchRules.Option(way)"),
            "Flare bulk: each answer goes through ReplyToNote or ReplyToReview, the paths a hand answer takes, with the option the rule chose");
        Check(FlareMethod(kit, "public bool ReplyToNote(").Contains("FlareReplyRules.Quality(option, stars, FlareReplyRules.Unfair(stars, shown))")
              && FlareMethod(kit, "public bool ReplyToReview(").Contains("QualityFor(option, stars, review.Place)"), "Flare bulk: those two paths price an answer by FlareReplyRules, the caps included, as before");
        Check(FlareMethod(replies, "private void Told(").Contains("if (_batchQuiet)") && FlareMethod(batch, "private void BatchTold(").Contains("Tell(NotificationType.Info"), "Flare bulk: a bulk answer holds back the notice of each answer and tells once");
        Check(FlareMethod(batch, "public FlareBatchSummary BatchPreview(").Contains("FlareBatchRules.Plan(") && !FlareMethod(batch, "public FlareBatchSummary BatchPreview(").Contains("ReplyTo"),
            "Flare bulk: the preview works out the plan and answers nothing");
        string collect = FlareMethod(batch, "private int BatchCollect(");
        Check(collect.Contains("string.IsNullOrEmpty(n.Answer)") && collect.Contains("!_batchAnswered.Contains(p.Id)") && collect.Contains("FlareReviewBook.Counts(_file, p, now)") && collect.Contains("p.Day >= fromDay") && collect.Contains("n.Day >= fromDay"),
            "Flare bulk: only reviews not yet answered are gathered, and a manager's round only those young enough");
        string asks = FlareMethod(batch, "internal bool ManagerAnswers(");
        Check(asks.Contains("Save.Day - FlareBatchRules.AutoDays") && asks.Contains("ShopAt(place)") && asks.Contains("shop.Place == null"), "Flare duty: a manager's round reads the reviews of ten days, and only at a business of the player's");
        Check(FlareMethod(batch, "private int BatchRound(").Contains("ReplyToNote") && batch.Contains("MyShops()") && !batch.Contains("SharedCity") && !batch.Contains("_outbox") && !batch.Contains("GoingPublic"),
            "Flare bulk: only the player's own businesses are walked, and the bulk code sends nothing of its own over the network");
        Check(!duty.Contains("Update(") && !duty.Contains("LateUpdate") && managers.Contains("ManagerRules.Due(day, hour, (ManagerTask)t)") && managers.Contains("case ManagerTask.Reviews: DecideReviews("),
            "Flare duty: it is one of the managers' hourly rounds, at its own hour, never per frame");
        Check(duty.Contains("Slips(skill, review, day, place, ManagerTask.Reviews") && duty.Contains("ManagerReviews.May(Settings.FlareManagerReplies, CompanyBridges.ReviewsReady())") && duty.Contains("d.State = DecisionState.Closed"),
            "Flare duty: a manager's mistake is a day of reviews not got to, the option and Flare gate it, and a decision is written down");
        Check(!duty.Contains("Cost(") && !duty.Contains("Pay("), "Flare duty: the duty books no money of its own: the manager's allowance is its cost");
        Check(managers.Contains("ManagerReviews.May(Settings.FlareManagerReplies, CompanyBridges.ReviewsReady())") && managers.Contains("blocked |= ManagerRules.Bit(ManagerTask.Reviews)"), "Flare duty: the manager's panel says the duty waits when Flare or its option is off");
        string face = ModSource(@"Ui\Flare\FlareFace.cs"), desk = ModSource(@"Ui\Flare\FlareFace.Desk.cs"), screen = ModSource(@"Ui\Flare\FlareScreen.cs"), sheet = ModSource(@"Ui\Flare\FlareScreen.Sheet.cs");
        string kitBatch = ModSource(@"Ui\Flare\FlareScreen.Batch.cs"), app = ModSource(@"Social\Flare\FlareApp.Clicks.cs"), appBusiness = ModSource(@"Social\Flare\FlareApp.Business.cs");
        Check(face.Contains("AnswerAllCommand = 346") && face.Contains("ProfileActCommand, AnswerAllCommand") && desk.Contains("AnswerAllCommand, b.Chosen") && screen.Contains("case FlareFace.AnswerAllCommand: return OpenBatch(arg);")
              && sheet.Contains("case FlareFace.BatchSheet: return BatchNow();") && sheet.Contains("case FlareFace.BatchSheet: BatchSheet(k); break;") && sheet.Contains("case GroupBatch:"),
            "Flare bulk: the kit's Business page has the Answer all button and its sheet, pressed through the director");
        Check(kitBatch.Contains("_flare.BatchPreview(place)") && kitBatch.Contains("_flare.BatchAnswer(place)") && kitBatch.Contains("reallife_flare_kit_batch_all"), "Flare bulk: the sheet previews first, answers one business or all of them, and tells why when it cannot");
        Check(app.Contains("_director.BatchPreview(place)") && app.Contains("_director.BatchAnswer(place)") && app.Contains("GameConfirm.Show(") && appBusiness.Contains("\"answerall\"") && appBusiness.Contains("reallife_flare_answer_all_every"),
            "Flare bulk: the old panel's Business screen has the same, behind the game's own question box");
        Check(ModSource(@"Ui\Flare\FlareScreen.Desk.cs").Contains("FlareBatchRules.FirstName(r.Name)"), "Flare bulk: a kept answer is shown with the reviewer's and the business's names filled in");
    }

    private static void FlareBatchTexts()
    {
        Dictionary<string, string> en = LocaleFile("en");
        string[] keys =
        {
            "reallife_flare_kit_answer_all", "reallife_flare_kit_batch_title", "reallife_flare_kit_batch_line", "reallife_flare_kit_batch_scope", "reallife_flare_kit_batch_all", "reallife_flare_kit_batch_note",
            "reallife_flare_kit_batch_confirm", "reallife_flare_kit_batch_confirm_none", "reallife_flare_kit_batch_failed", "reallife_flare_batch_none", "reallife_flare_batch_p_thanks", "reallife_flare_batch_p_sorry",
            "reallife_flare_batch_p_mixed", "reallife_flare_batch_p_left", "reallife_flare_batch_left", "reallife_flare_batch_left_one", "reallife_flare_batch_beyond", "reallife_flare_batch_rating_all_moved",
            "reallife_flare_batch_rating_all_same", "reallife_flare_batch_done", "reallife_flare_batch_done_all", "reallife_flare_answer_all", "reallife_flare_answer_all_every", "reallife_flare_batch_ask_title",
            "reallife_flare_batch_ask_body", "reallife_flare_batch_ask_rating", "reallife_flare_test_reviews_many", "reallife_flare_test_reviews_many_up", "reallife_flare_opt_manager_replies",
            "reallife_opt_tip_flare_manager_replies", "reallife_mgr_task_reviews", "reallife_mgr_task_reviews_what", "reallife_mgr_task_reviews_waits", "reallife_mgr_limit_reviews_value",
            "reallife_mgr_did_reviews", "reallife_mgr_did_reviews_left", "reallife_mgr_did_reviews_missed", "reallife_mgr_note_reviews", "reallife_mgr_note_reviews_left"
        };
        int missing = 0, bad = 0;
        foreach (string key in keys)
        {
            if (!en.TryGetValue(key, out string text) || string.IsNullOrWhiteSpace(text))
            {
                missing++;
                continue;
            }
            bad += text.Contains("!") || text.Contains(";") || text.Contains("—") || text.Contains("–") || text.Contains(" - ") ? 1 : 0;
        }
        Check(missing == 0 && bad == 0, "Flare bulk texts: every text the feature shows is written, with no exclamation mark, semicolon or dash (" + missing + " missing, " + bad + " bad)");
        string name = "Corner Cafe And Bakery Number One";
        string parts = en["reallife_flare_batch_p_thanks"].Replace("{count}", "120") + ", " + en["reallife_flare_batch_p_sorry"].Replace("{count}", "120") + ", " + en["reallife_flare_batch_p_mixed"].Replace("{count}", "120");
        string rating = en["reallife_flare_reply_rating_moved"].Replace("{before}", "3.50").Replace("{after}", "3.90");
        string left = en["reallife_flare_batch_left"].Replace("{count}", "120");
        int longest = 0;
        foreach (string key in new[] { "reallife_flare_batch_done", "reallife_flare_batch_done_all" })
        {
            longest = Math.Max(longest, en[key].Replace("{business}", name).Replace("{count}", "120").Replace("{parts}", parts).Replace("{rating}", rating).Replace("{left}", left).Length);
        }
        string week = en["reallife_mgr_note_reviews"].Replace("{count}", "120").Replace("{parts}", parts).Replace("{rating}", en["reallife_flare_batch_rating_all_moved"].Replace("{before}", "3.50").Replace("{after}", "3.90")).Replace("{left}", left);
        Check(en["reallife_flare_batch_done"].Length <= 240 && en["reallife_flare_batch_done_all"].Length <= 240 && en["reallife_mgr_note_reviews"].Length <= 240 && en["reallife_mgr_note_reviews_left"].Length <= 240,
            "Flare bulk texts: each notice's own text fits in 240 characters (" + longest + " and " + week.Length + " filled with the longest figures)");
        string tip = en["reallife_opt_tip_flare_manager_replies"];
        Check(tip.Length <= 190 && tip.Split(' ').Length <= 32 && en["reallife_flare_opt_manager_replies"].Length <= 30, "Flare bulk texts: the option's label is short and its tip is at most 32 words and 190 characters (" + tip.Split(' ').Length + ", " + tip.Length + ")");
        string guide = en["reallife_guide_flare"], mgr = en["reallife_guide_economy_managers"];
        Check(guide.Contains("# Answering all at once") && guide.Contains("{flare_batch_most}") && guide.Contains("{flare_auto_days}") && guide.Contains("{flare_manager_hour}") && guide.Contains("<b>Answers reviews</b>")
              && guide.Contains("# Answering a review") && mgr.Contains("<b>Answers reviews</b>, at {mgr_staff_hour}:00") && mgr.Contains("<b>Managers answer reviews</b>"), "Flare bulk texts: the Guide's Flare and Managers pages say what Answer all and the duty do");
        Check(WhatsNewHas(en, "Answer all on Flare's Business page, and in the old Flare app, answers every waiting review") && WhatsNewHas(en, "A manager can take the new duty Answers reviews"), "Flare bulk texts: What's new tells of both");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), settings = ModSource(@"Core\Settings.cs"), features = ModSource(@"Meta\FeatureList.Life.cs"), tree = ModSource(@"Meta\OptionsTree.cs");
        Check(mod.Contains("Settings.FlareManagerRepliesId, \"reallife_flare_opt_manager_replies\"") && settings.Contains("public static bool FlareManagerReplies = true;") && settings.Contains("FlareManagerReplies = Bool(modId, FlareManagerRepliesId, FlareManagerReplies);")
              && features.Contains("Settings.FlareCityPhotosId, Settings.FlareManagerRepliesId)") && tree.Contains("\"flare_city_photos\", \"flare_manager_replies\"") && mod.Contains("reallife_flare_test_reviews_many"),
            "Flare bulk texts: the option has its label, tip, place on the options page, in the tree and in the feature list, on by default, and its test button");
        string docs = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\docs\features\flare-city.md")));
        string mdocs = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\docs\features\economy-managers.md")));
        Check(docs.Contains("## Answering all at once") && docs.Contains("FlareBatchRules") && docs.Contains("FlareReplyWords") && mdocs.Contains("Answers reviews") && mdocs.Contains("ManagerReviews"), "Flare bulk texts: docs/features hold the rules of both pages");
    }
}
