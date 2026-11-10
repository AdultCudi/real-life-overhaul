using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Schemes;
using SchemeOffice = RealLifeOverhaul.Schemes.Office;

// The schemes' long seeded simulation: a player who follows one plan for ten game years, over many saves, played by the
// shipped rules alone (SchemeRules, the insurer's CoverRules, the households' PurseRules, the court's own sum). What is
// counted is money only, and only what the schemes themselves move: what a scheme brought, what an inquiry took back, the
// surcharge, what was paid back, what was paid to people. A court's fine is counted apart and never needed for a
// result to hold, since a fine is bounded by the cash in hand (JusticeLaw.FineShareOfCash); a lawyer's fee, days in
// prison, the insurer's higher premium and a scandal are not counted at all. So every result here is the player's best case.
internal static partial class Program
{
    /// <summary>What a simulated player does: one scheme, a size, how often, and what they do when the letter comes.</summary>
    private sealed class SchemePlan
    {
        public string Name;
        public SchemeKind Kind;     // Claim, Expense, Books (a sale each time) or Club (whose cash the till then washes)
        public int Size = SchemeCatalogue.Small;
        public int Every;           // days between two acts; 0 for one act and no more
        public bool Repays;         // pays the file back when the letter comes
        public bool Buys;           // offers the investigator money when the letter comes
        public bool Tells;          // tells the investigators everything when the letter comes
        public int Wash;            // the till's rate for a club's cash, thousandths
    }

    /// <summary>One save of the simulation: the schemes' state, the insurer, the district's households, and the sums.</summary>
    private sealed class SchemeRun
    {
        public const string Place = "Elm~7";
        public const int Takings = 3000;        // an average day's takings of the one business
        public const int Tax = 20;              // the game's tax rate, per cent
        public const int First = 30;            // the day of the first act
        public const int SaleValue = 36000;     // what the business is honestly worth to a buyer
        public const int Counsel = 830;         // the best law firm (JusticeLaw.FirmQuality(5)), thousandths
        public const int Lawyer = 7;            // the one partner at a law firm the player knows

        public SchemeState S;
        public SchemeKind Kind;                 // the plan's scheme
        public readonly SchemeWorld W = new SchemeWorld();
        public readonly List<SchemeEvent> Events = new List<SchemeEvent>(16);
        public readonly CompanyState Company = new CompanyState();
        public readonly Policy Cover = new Policy();
        public PopulationState People;
        public long Wallet;                     // what the schemes moved in the player's wallet, signed: the result
        public long Gains, TakenBack, Surcharges, PaidBack, Bribes, Fines;
        public long PeopleOut;                  // what the district's households are out, whole money
        public long SavingsAtStart;             // what they had before anything was taken
        public long WashedOut, WashedIn;        // cash that left the hand for a till, and what reached the wallet of it
        public long Half;                       // the wallet at half time: what the second half added is the difference
        public int Acts, Opened, Found, Convicted, Cautions, Dropped, Priors, Walked, Sales, Refused;
        public int LostOwn, DroppedOwn;         // the investigator of an inquiry into the plan's own scheme was bought, and that inquiry was dropped
    }

    private static SchemeRun SchemeRunOf(int seed)
    {
        var r = new SchemeRun { S = new SchemeState { Seed = seed }, People = new PopulationState() };
        for (int id = 1; id <= 600; id++)
        {
            int slot = r.People.Households.Add(id);
            r.People.Households.District[slot] = 0;
            r.People.Households.Savings[slot] = 500 + SchemeRules.Roll(seed, 900, id, 0) * 20;
        }
        r.SavingsAtStart = SchemeSavings(r.People);
        return r;
    }

    private static long SchemeSavings(PopulationState people)
    {
        long sum = 0;
        for (int h = 0; h < people.Households.Count; h++)
        {
            sum += people.Households.Id[h] != 0 ? people.Households.Savings[h] : 0;
        }
        return sum;
    }

    // The claims on file that the insurer has not paid yet, as the director reads them from the company.
    private static void SchemeUnpaid(SchemeRun r, List<int> into)
    {
        into.Clear();
        for (int i = 0; i < r.S.Acts.Count; i++)
        {
            SchemeAct a = r.S.Acts[i];
            Claim c = a.Kind == SchemeKind.Claim && a.Inquiry == 0 ? r.Company.ClaimOf(a.Ref) : null;
            if (c != null && (c.Stage == ClaimStage.Open || c.Stage == ClaimStage.Filed))
            {
                into.Add(a.Ref);
            }
        }
    }

    // One day: the insurer pays what is due and renews or declines, the rules play the day, and what they did is carried out.
    private static void SchemeSimDay(SchemeRun r, int day, SchemePlan plan)
    {
        for (int i = 0; i < r.Company.Claims.Count; i++)
        {
            Claim c = r.Company.Claims[i];
            if (CoverRules.Day(c, day))
            {
                r.Wallet += c.Payout;
                r.Gains += c.Payout;
            }
        }
        if (CoverRules.TermOver(r.Cover, day))
        {
            CoverRules.Renew(r.Cover, 0, SchemeRun.Takings, 100, 1000, day, 60);
        }
        for (int insurer = 0; !r.Cover.Active && insurer < CompanyCatalogue.Insurers; insurer++)
        {
            if (CoverRules.WillWrite(r.Cover, insurer))
            {
                CoverRules.Buy(r.Cover, insurer, CoverRules.Premium(0, insurer, SchemeRun.Takings, 100, CoverRules.Carried(r.Cover), 1000), day, 60);
            }
        }
        SchemeWorld w = r.W;
        w.Clear();
        w.On = true;
        w.TaxPercent = SchemeRun.Tax;
        w.Tills.Add(new SchemeTill { Place = SchemeRun.Place, Average = SchemeRun.Takings, Cash = true });
        SchemeUnpaid(r, w.Unpaid);
        r.Events.Clear();
        SchemeRules.Day(r.S, day, w, r.Events);
        for (int i = 0; i < r.Events.Count; i++)
        {
            SchemeSimCarry(r, r.Events[i], day, plan);
        }
    }

    // What the director does with an event, in money: the same sums, taken from the event the rules made.
    private static void SchemeSimCarry(SchemeRun r, SchemeEvent e, int day, SchemePlan plan)
    {
        switch (e.Kind)
        {
            case SchemeEventKind.Washed:
                r.Wallet += e.Net;
                r.Gains += e.Net;
                r.WashedOut += e.Amount;
                r.WashedIn += e.Net;
                break;
            case SchemeEventKind.Opened:
                r.Opened++;
                break;
            case SchemeEventKind.Letter:
                SchemeSimLetter(r, r.S.Inquiry(e.Inquiry), day, plan);
                break;
            case SchemeEventKind.Dropped:
                r.Dropped++;
                r.DroppedOwn += (SchemeKind)e.A == plan.Kind ? 1 : 0;
                break;
            case SchemeEventKind.Caution:
                r.Wallet -= e.Net;
                r.Surcharges += e.Net;
                r.Cautions++;
                break;
            case SchemeEventKind.Findings:
                SchemeSimFindings(r, e, day);
                break;
        }
    }

    private static void SchemeSimFindings(SchemeRun r, SchemeEvent e, int day)
    {
        SchemeInquiry q = r.S.Inquiry(e.Inquiry);
        long fromWallet = e.Amount - e.FromCash;
        r.Wallet -= fromWallet + e.Net;
        r.TakenBack += fromWallet;
        r.Surcharges += e.Net;
        r.Found++;
        if ((q.Kind == SchemeKind.Club || q.Kind == SchemeKind.Wash) && e.Amount > 0)
        {
            r.PeopleOut -= PurseRules.Give(r.People, 0, e.Amount);
        }
        for (int i = 0; q.Kind == SchemeKind.Claim && i < r.S.Acts.Count; i++)
        {
            Claim c = r.S.Acts[i].Kind == SchemeKind.Claim && r.S.Acts[i].Inquiry == q.Id ? r.Company.ClaimOf(r.S.Acts[i].Ref) : null;
            if (c == null)
            {
                continue;
            }
            if (c.Stage == ClaimStage.Open || c.Stage == ClaimStage.Filed)
            {
                c.Stage = ClaimStage.Lapsed;   // refused before it paid, as CompanyDirector.ClaimFoundOut does
            }
            r.Cover.Declined |= 1 << c.Insurer;
            if (r.Cover.Active && r.Cover.Insurer == c.Insurer)
            {
                CoverRules.End(r.Cover);
            }
        }
        bool convicted = SchemeRules.Tried(r.S.Seed ^ q.Id * 7919, q.Id, q.Evidence, r.Priors, SchemeRun.Counsel);
        if (convicted)
        {
            r.Fines += SchemeRules.FineOf(e.Text, r.Priors, 1000000f);
            r.Priors++;
            r.Convicted++;
        }
        SchemeRules.Close(r.S, q, convicted ? InquiryEnd.Convicted : InquiryEnd.Cleared, day);
    }

    // The letter has come: what a player of this plan does about it.
    private static void SchemeSimLetter(SchemeRun r, SchemeInquiry q, int day, SchemePlan plan)
    {
        if (q == null)
        {
            return;
        }
        if (plan.Buys && !q.Asked)
        {
            string contact = SchemeRules.Contact(SchemeRun.Lawyer);
            long price = SchemeRules.PriceOf(r.S, Favour.Investigator, q, day, 1000);
            bool cash = !r.S.Watched && r.S.Dirty >= price, carried = SchemeRules.TemperOf(r.S.Seed, SchemeOffice.Contact, contact) == Temper.Takes;
            q.Asked = true;
            OfferResult o = carried
                ? SchemeRules.Offer(r.S, Favour.Investigator, SchemeOffice.Investigator, SchemeRules.Investigator(q.Kind, r.S.Inquiries[(int)q.Kind]), price, cash, day)
                : SchemeRules.Offer(r.S, Favour.Investigator, SchemeOffice.Contact, contact, price, cash, day);
            if (o.Taken)
            {
                SchemeRules.LoseFile(q);
                r.Wallet -= cash ? 0 : price;
                r.Bribes += price;
                r.LostOwn += q.Kind == plan.Kind ? 1 : 0;
            }
            else
            {
                r.Refused++;
                q.Pressed += carried && o.Temper == Temper.Reports ? SchemeCatalogue.EvidenceReported : 0;
            }
        }
        if (plan.Repays)
        {
            SchemeUnpaid(r, r.W.Unpaid);
            long paid = SchemeRules.Repay(r.S, q.Kind, day, r.W.Unpaid, out long fromCash);
            r.Wallet -= paid - fromCash;
            r.PaidBack += paid - fromCash;
            if (q.Kind == SchemeKind.Club && paid > 0)
            {
                r.PeopleOut -= PurseRules.Give(r.People, 0, paid);
            }
            for (int i = 0; q.Kind == SchemeKind.Claim && paid > 0 && i < r.W.Unpaid.Count; i++)
            {
                r.Company.ClaimOf(r.W.Unpaid[i]).Stage = ClaimStage.Lapsed;   // withdrawn before it paid
            }
        }
        if (plan.Tells)
        {
            SchemeRules.Cooperate(q);
        }
    }

    // The plan's act of the day, each as the director does it: the wait first, then the rules' own record.
    private static void SchemeSimAct(SchemeRun r, int day, SchemePlan plan)
    {
        SchemeState s = r.S;
        SchemeShop shop = s.Shop(SchemeRun.Place, true);
        switch (plan.Kind)
        {
            case SchemeKind.Claim:
            {
                if (!CoverRules.Covers(r.Cover, day) || !SchemeRules.MayClaim(shop, day))
                {
                    return;
                }
                Claim c = CoverRules.Open(r.Company, r.Cover, SchemeRun.Place, "Shop", 0, SchemeRules.ClaimLoss(CompanyCatalogue.CoverMost[0], plan.Size), day);
                if (c == null)
                {
                    return;
                }
                CoverRules.File(c, r.Cover, day);
                SchemeRules.Claimed(s, SchemeRun.Place, day, c.Payout, plan.Size, c.Id);
                break;
            }
            case SchemeKind.Expense:
            {
                if (!SchemeRules.MayExpense(s, day))
                {
                    return;
                }
                long gain = SchemeRules.ExpenseGain(SchemeRules.ExpenseAmount(SchemeRun.Takings, plan.Size), SchemeRun.Tax);
                r.Wallet += gain;
                r.Gains += gain;
                SchemeRules.Expensed(s, day, gain, plan.Size);
                break;
            }
            case SchemeKind.Books:
            {
                int lift = SchemeRules.LiftStep(plan.Size >= SchemeCatalogue.Large ? SchemeCatalogue.LiftMost : SchemeCatalogue.LiftSteps[1]);
                shop.Lift = lift;
                int offer = r.Sales++;
                if (SchemeRules.SeenThrough(s.Seed, SchemeRun.Place, (offer + s.Seed) & 3, lift, offer))
                {
                    SchemeRules.Walked(s, SchemeRun.Place);
                    r.Walked++;
                    return;
                }
                SchemeAct sold = SchemeRules.Sold(s, SchemeRun.Place, (long)SchemeRules.Lifted(SchemeRun.SaleValue, lift), day);
                r.Wallet += sold.Gain;   // only what the lift made: the honest price is no scheme's
                r.Gains += sold.Gain;
                break;
            }
            case SchemeKind.Club:
            {
                if (!SchemeRules.MayClub(shop, day))
                {
                    return;
                }
                int seed = s.Seed, club = s.Acted;
                long taken = PurseRules.Take(r.People, 0, SchemeRules.ClubHouseholds(plan.Size), id => SchemeRules.ClubDraw(seed, club, id),
                    savings => SchemeRules.ClubStake(savings, 1000), out int joined);
                if (taken <= 0)
                {
                    return;
                }
                SchemeRules.ClubSold(s, SchemeRun.Place, taken, joined, plan.Size, day);
                r.PeopleOut += taken;
                shop.Wash = SchemeRules.WashStep(plan.Wash);
                break;
            }
        }
        r.Acts++;
    }

    /// <summary>One save played through a plan for so many days. The first day only sets the clock, as in the game.</summary>
    private static SchemeRun SchemePlay(int seed, SchemePlan plan, int days)
    {
        SchemeRun r = SchemeRunOf(seed);
        r.Kind = plan.Kind;
        for (int day = 1; day <= days; day++)
        {
            SchemeSimDay(r, day, plan);
            bool due = plan.Every > 0 ? day >= SchemeRun.First && (day - SchemeRun.First) % plan.Every == 0 : day == SchemeRun.First;
            if (due)
            {
                SchemeSimAct(r, day, plan);
            }
            if (day == days / 2)
            {
                r.Half = r.Wallet;
            }
        }
        return r;
    }

    /// <summary>A plan's result over many saves.</summary>
    private struct SchemeTally
    {
        public int Runs, Caught, Acted, CaughtActed, Ahead, Acts, Convicted, Cautions, Bought, BoughtDropped;
        public long Sum, Half, Least, Most, Gains, Fines, Bribes;
        public long Mean => Runs > 0 ? Sum / Runs : 0;
        public long MeanAtHalf => Runs > 0 ? Half / Runs : 0;            // the mean result at half time
        public int CaughtShare => Runs > 0 ? Caught * 1000 / Runs : 0;   // thousandths of the saves in which an inquiry made findings or gave a caution
        public int AheadShare => Runs > 0 ? Ahead * 1000 / Runs : 0;     // thousandths of the saves that ended with more than they began with
    }

    private static SchemeTally SchemeTallyOf(SchemePlan plan, int saves, int days, Action<SchemeRun> each = null)
    {
        var t = new SchemeTally { Least = long.MaxValue, Most = long.MinValue };
        for (int i = 0; i < saves; i++)
        {
            SchemeRun r = SchemePlay(SchemeRules.SeedOf("save " + i.ToString(CultureInfo.InvariantCulture)), plan, days);
            bool caught = r.Found + r.Cautions > 0;
            t.Runs++;
            t.Sum += r.Wallet;
            t.Half += r.Half;
            t.Least = Math.Min(t.Least, r.Wallet);
            t.Most = Math.Max(t.Most, r.Wallet);
            t.Caught += caught ? 1 : 0;
            t.Acted += r.Acts > 0 ? 1 : 0;
            t.CaughtActed += r.Acts > 0 && caught ? 1 : 0;
            t.Ahead += r.Wallet > 0 ? 1 : 0;
            t.Acts += r.Acts;
            t.Convicted += r.Convicted;
            t.Cautions += r.Cautions;
            t.Gains += r.Gains;
            t.Fines += r.Fines;
            t.Bribes += r.Bribes;
            t.Bought += r.LostOwn > 0 ? 1 : 0;
            t.BoughtDropped += r.LostOwn > 0 && r.DroppedOwn > 0 ? 1 : 0;
            each?.Invoke(r);
        }
        return t;
    }

    private static string SchemeRow(SchemePlan plan, SchemeTally t, int days)
    {
        return string.Format(CultureInfo.InvariantCulture, "  {0,-46} {1,5} days  acts {2,6:0.0}  caught {3,5:0.0}%  ahead {4,5:0.0}%  mean {5,9}  worst {6,9}  best {7,8}  brought {8,8}  fines {9,8}",
            plan.Name, days, t.Acts / (double)t.Runs, t.CaughtShare / 10.0, t.AheadShare / 10.0, t.Mean, t.Least, t.Most, t.Gains / t.Runs, t.Fines / t.Runs);
    }

    private static readonly SchemeKind[] SchemeMoneyKinds = { SchemeKind.Claim, SchemeKind.Expense, SchemeKind.Books, SchemeKind.Club };

    // The wait the rules put between two acts of a kind: "as often as allowed" is an act at every one. A business is not sold more often than a club is.
    private static int SchemeGap(SchemeKind kind)
    {
        return kind == SchemeKind.Claim ? SchemeCatalogue.ClaimGapDays : kind == SchemeKind.Expense ? SchemeCatalogue.ExpenseGapDays : SchemeCatalogue.ClubGapDays;
    }

    private static SchemePlan SchemePlanOf(SchemeKind kind, string how, int size, int every, bool repays = false, bool buys = false)
    {
        return new SchemePlan
        {
            Name = kind + ", " + (size >= SchemeCatalogue.Large ? "large" : "small") + ", " + how + (repays ? ", pays back" : string.Empty) + (buys ? ", buys the investigator" : string.Empty),
            Kind = kind, Size = size, Every = every, Repays = repays, Buys = buys, Wash = size >= SchemeCatalogue.Large ? SchemeCatalogue.WashSteps[2] : SchemeCatalogue.WashSteps[1]
        };
    }

    // What a scheme's page says of a first small one: the odds by the rules' own sum, with what the director records it with.
    private static int SchemeSaidChance(SchemeKind kind)
    {
        switch (kind)
        {
            case SchemeKind.Books:
                return SchemeRules.FirstChance(kind, SchemeCatalogue.LiftSteps[1] * 1000 / SchemeCatalogue.LiftMost, 0, true);
            case SchemeKind.Club:
                return SchemeRules.FirstChance(kind, SchemeCatalogue.Small, SchemeCatalogue.ClubSmall / SchemeCatalogue.ClubKnowsEvery, false);
            default:
                return SchemeRules.FirstChance(kind, SchemeCatalogue.Small, 0, true);
        }
    }

    // What is wrong with a save at its end, or null: the cash in hand, the households' money to the unit, the till's loss, one inquiry a kind.
    private static string SchemeUnsound(SchemeRun r)
    {
        SchemeState s = r.S;
        if (s.Dirty < 0)
        {
            return "cash in hand under nothing (" + s.Dirty + ")";
        }
        if (r.PeopleOut != s.ClubTaken - s.ClubBack)
        {
            return "the households are out " + r.PeopleOut + " but the clubs' own ledger says " + (s.ClubTaken - s.ClubBack);
        }
        if (SchemeSavings(r.People) + r.PeopleOut != r.SavingsAtStart)
        {
            return "the households' savings and what they are out do not add up to what they began with";
        }
        if (r.WashedIn > SchemeRules.WashNet(r.WashedOut, SchemeRun.Tax) || (r.WashedOut > 0 && r.WashedIn >= r.WashedOut))
        {
            return "the till gave back " + r.WashedIn + " of " + r.WashedOut + ": more than the cut and the tax leave";
        }
        if (r.Kind == SchemeKind.Club && r.Wallet > r.PeopleOut)
        {
            return "the player is ahead by " + r.Wallet + " from a club, more than its households are out (" + r.PeopleOut + ")";
        }
        for (int k = 0; k < SchemeCatalogue.Kinds; k++)
        {
            int open = 0, acts = 0;
            for (int i = 0; i < s.Cases.Count; i++)
            {
                open += s.Cases[i].Open && (int)s.Cases[i].Kind == k ? 1 : 0;
            }
            for (int i = 0; i < s.Acts.Count; i++)
            {
                acts += (int)s.Acts[i].Kind == k ? 1 : 0;
            }
            if (open > 1 || acts > SchemeCatalogue.ActsMost)
            {
                return open + " inquiries open and " + acts + " acts kept for " + (SchemeKind)k;
            }
        }
        return null;
    }

    // Ten game years and more of every plan: what pays, what does not, and that nothing pays for being repeated.
    private static void SchemeSimChecks()
    {
        var watch = Stopwatch.StartNew();
        var rows = new List<string>();
        string unsound = null;
        Action<SchemeRun> sound = r => unsound = unsound ?? SchemeUnsound(r);
        const int longDays = 3600, days = 1800, onceDays = 400;
        foreach (SchemeKind kind in SchemeMoneyKinds)
        {
            string name = kind.ToString().ToLowerInvariant();
            SchemePlan plan = SchemePlanOf(kind, "once", SchemeCatalogue.Small, 0);
            SchemeTally small = SchemeTallyOf(plan, 400, onceDays, sound);
            rows.Add(SchemeRow(plan, small, onceDays));
            int said = SchemeSaidChance(kind);
            Check(small.CaughtShare >= 80 && small.CaughtShare <= 450 && small.Mean > 0 && small.AheadShare >= 550,
                "schemes sim (" + name + "): one small one is looked into now and then and pays on average: caught in " + small.CaughtShare / 10.0 + "% of saves, mean " + small.Mean);
            Check(Math.Abs(small.CaughtShare - said * 10) <= 70,
                "schemes sim (" + name + "): the odds its page says before the player commits (" + said + "%) are what happens over 400 saves (" + small.CaughtShare / 10.0 + "%)");
            long one = small.Most;   // what one small act brings when nobody looks

            plan = SchemePlanOf(kind, "once", SchemeCatalogue.Large, 0);
            SchemeTally large = SchemeTallyOf(plan, 200, onceDays, sound);
            rows.Add(SchemeRow(plan, large, onceDays));
            Check(large.Acted > 0 && large.CaughtActed == large.Acted && large.Ahead == 0 && large.Mean < 0,
                "schemes sim (" + name + "): a large one is always looked into and never ends ahead: " + large.CaughtActed + " of " + large.Acted + " caught, mean " + large.Mean);

            foreach (bool repays in new[] { false, true })
            {
                foreach (int every in new[] { 721, 360 })
                {
                    plan = SchemePlanOf(kind, "every " + every + " days", SchemeCatalogue.Small, every, repays);
                    SchemeTally t = SchemeTallyOf(plan, 100, longDays, sound);
                    rows.Add(SchemeRow(plan, t, longDays));
                    Check(t.Mean * SchemeCatalogue.TrailDays <= one * longDays,
                        "schemes sim (" + name + "): " + plan.Name + " keeps no more on average than one small act's worth (" + one + ") for each stretch a file reaches back: mean "
                        + t.Mean + " over " + longDays + " days");
                }
                plan = SchemePlanOf(kind, "every 180 days", SchemeCatalogue.Small, 180, repays);
                SchemeTally half = SchemeTallyOf(plan, 100, days, sound);
                rows.Add(SchemeRow(plan, half, days));
                Check(half.Caught == half.Runs && half.Mean <= half.MeanAtHalf && half.Mean * 10 <= half.Gains / half.Runs && (repays || half.Mean < 0),
                    "schemes sim (" + name + "): " + plan.Name + " is always found out, and going on with it adds nothing: mean " + half.MeanAtHalf + " at half time, " + half.Mean
                    + " at the end, of " + half.Gains / half.Runs + " brought");

                var often = new List<SchemePlan> { SchemePlanOf(kind, "every 91 days", SchemeCatalogue.Small, 91, repays) };
                if (SchemeGap(kind) != 30)
                {
                    often.Add(SchemePlanOf(kind, "every 30 days", SchemeCatalogue.Small, 30, repays));
                }
                often.Add(SchemePlanOf(kind, "as often as allowed", SchemeCatalogue.Small, SchemeGap(kind), repays));
                often.Add(SchemePlanOf(kind, "as often as allowed", SchemeCatalogue.Large, SchemeGap(kind), repays));
                foreach (SchemePlan p in often)
                {
                    SchemeTally t = SchemeTallyOf(p, 60, days, sound);
                    rows.Add(SchemeRow(p, t, days));
                    Check(t.Caught == t.Runs && t.Mean < 0 && t.Mean <= t.MeanAtHalf && (p.Every > 30 || t.Ahead == 0),
                        "schemes sim (" + name + "): " + p.Name + " is always found out and loses, and going on never makes it better: mean " + t.MeanAtHalf + " at half time, " + t.Mean
                        + " at the end, ahead in " + t.AheadShare / 10.0 + "% of saves");
                }
            }
        }

        // Buying the investigator: who takes money is two people's own temper, so it works in few saves, saves one act at most, and never a pattern.
        SchemePlan buys = SchemePlanOf(SchemeKind.Expense, "once", SchemeCatalogue.Large, 0, false, true);
        SchemeTally bought = SchemeTallyOf(buys, 400, onceDays, sound);
        rows.Add(SchemeRow(buys, bought, onceDays));
        int wanted = SchemeCatalogue.TakesPermille[(int)SchemeOffice.Contact] * SchemeCatalogue.TakesPermille[(int)SchemeOffice.Investigator] / 1000;
        Check(Math.Abs(bought.Bought * 1000 / bought.Runs - wanted) <= 50 && bought.BoughtDropped == bought.Bought && bought.Mean < 0,
            "schemes sim: an investigator is bought in about " + wanted / 10.0 + "% of saves (both must be takers): " + bought.Bought * 100.0 / bought.Runs + "%, the inquiry then dropped in "
            + bought.BoughtDropped + " of " + bought.Bought + ", and the plan still loses on average (" + bought.Mean + ")");
        buys = SchemePlanOf(SchemeKind.Expense, "every 30 days", SchemeCatalogue.Small, 30, false, true);
        SchemeTally pattern = SchemeTallyOf(buys, 100, days, sound);
        rows.Add(SchemeRow(buys, pattern, days));
        Check(pattern.Caught == pattern.Runs && pattern.Mean < 0 && pattern.Ahead == 0 && pattern.Mean <= pattern.MeanAtHalf,
            "schemes sim: a bought investigator loses one act's worth of a file, never a pattern's: a monthly plan with one is found out in every save and loses (mean " + pattern.Mean + ")");

        Check(unsound == null, "schemes sim: in every save of every plan the cash in hand, the households' money and the till's loss add up, and one inquiry a kind is open at most ("
                               + (unsound ?? "all sound") + ")");
        watch.Stop();
        if (_schemeTable)
        {
            Console.WriteLine("The schemes' simulation (money the schemes moved only; a court's fine apart, for a wallet of 1,000,000):");
            foreach (string row in rows)
            {
                Console.WriteLine(row);
            }
            Console.WriteLine("  (" + rows.Count + " plans: " + watch.ElapsedMilliseconds + " ms)");
        }
    }

    // ------------------------------------------------------------------------------------------------- the rivals and their fund

    /// <summary>What a simulated player does with a rival's fund.</summary>
    private enum SchemeFundPlan
    {
        Never,
        Always,          // puts the money in, whatever it is
        Looks,           // has it looked at first, and puts money only into an honest one
        Reports          // puts the money in, and reports a sham to the police
    }

    /// <summary>One save of the rivals' side: three tycoons, the council, a player of some worth, and what came of it.</summary>
    private sealed class SchemeCity
    {
        public const long PlayerWorth = 600000;
        public SchemeState S;
        public readonly SchemeWorld W = new SchemeWorld();
        public readonly List<SchemeEvent> Events = new List<SchemeEvent>(16);
        public readonly long[] Worth = { 40000000L, 60000000L, 90000000L };
        public readonly long[] Net = new long[SchemeState.Rivals];          // what their schemes moved in their worth, signed
        public readonly int[] Acts = new int[SchemeState.Rivals], Found = new int[SchemeState.Rivals], Convicted = new int[SchemeState.Rivals];
        public readonly int[] Cleared = new int[SchemeState.Rivals], Dropped = new int[SchemeState.Rivals], Letters = new int[SchemeState.Rivals];
        public readonly int[] LetterDay = new int[SchemeState.Rivals], FoundDay = new int[SchemeState.Rivals];
        public long Wallet, Fees;
        public int Offers, Shams, Placed, Paid, Lost, Recovered, Lapsed, Declined, Stream;
        public bool Timely = true;       // every rival's findings came the same days after the letter as a player's, and the trial as the court sets it
        public int LostDay, WaitMost;    // the day the last sham failed, and the longest wait from there to the money coming back
    }

    private static readonly string[] SchemeSeats = { "seat 0 party 1 term 1", "seat 1 party 2 term 1", "seat 2 party 1 term 1", "seat 3 party 3 term 1", "seat 4 party 2 term 1" };

    private static SchemeCity SchemeCityPlay(int seed, SchemeFundPlan plan, int days, bool session = false)
    {
        var c = new SchemeCity { S = new SchemeState { Seed = seed } };
        for (int day = 1; day <= days; day++)
        {
            SchemeWorld w = c.W;
            w.Clear();
            w.On = true;
            w.Session = session;
            w.Worth = SchemeCity.PlayerWorth;
            for (int i = 0; i < SchemeState.Rivals; i++)
            {
                w.RivalActive[i] = true;
                w.RivalWorth[i] = c.Worth[i];
            }
            w.Council.AddRange(SchemeSeats);
            c.Events.Clear();
            SchemeRules.Day(c.S, day, w, c.Events);
            for (int i = 0; i < c.Events.Count; i++)
            {
                SchemeCityCarry(c, c.Events[i], day, plan);
            }
        }
        c.Wallet += c.S.Fund.State == FundState.Placed ? c.S.Fund.Amount : 0;   // money still placed at the end is not lost: it is counted at what was put in
        return c;
    }

    // What the director does with a rival's or a fund's event, in money.
    private static void SchemeCityCarry(SchemeCity c, SchemeEvent e, int day, SchemeFundPlan plan)
    {
        int t = e.Tycoon;
        c.Stream = unchecked(c.Stream * 31 + (int)e.Kind * 7 + t * 3 + (int)e.Amount + day);
        switch (e.Kind)
        {
            case SchemeEventKind.RivalActed:
                c.Worth[t] += e.Amount;
                c.Net[t] += e.Amount;
                c.Acts[t]++;
                break;
            case SchemeEventKind.RivalLetter:
                c.Letters[t]++;
                c.LetterDay[t] = day;
                break;
            case SchemeEventKind.RivalFound:
                c.Worth[t] = Math.Max(0L, c.Worth[t] - e.Amount);
                c.Net[t] -= e.Amount;
                c.Found[t]++;
                c.Timely &= day - c.LetterDay[t] == SchemeCatalogue.ChargeAfter - SchemeCatalogue.LetterAfter && c.S.Rival[t].TrialDay == day + JusticeLaw.DaysToCourt(ChargeClass.Felony);
                c.FoundDay[t] = day;
                break;
            case SchemeEventKind.RivalDropped:
                c.Dropped[t]++;
                break;
            case SchemeEventKind.RivalTried:
                c.Timely &= day == c.FoundDay[t] + JusticeLaw.DaysToCourt(ChargeClass.Felony);
                if (e.B != 0)
                {
                    string charge = (SchemeKind)e.A == SchemeKind.Graft ? JusticeLaw.Bribery : (SchemeKind)e.A == SchemeKind.Expense ? JusticeLaw.TaxEvasion : JusticeLaw.Fraud;
                    int fine = SchemeRules.FineOf(charge, Math.Max(0, c.S.Rival[t].Convictions - 1), c.Worth[t]);
                    c.Worth[t] = Math.Max(0L, c.Worth[t] - fine);
                    c.Net[t] -= fine;
                    c.Convicted[t]++;
                }
                else
                {
                    c.Cleared[t]++;
                }
                break;
            case SchemeEventKind.FundOffered:
                c.Offers++;
                c.Shams += c.S.Fund.Sham ? 1 : 0;
                if (plan == SchemeFundPlan.Looks && SchemeRules.FundCheck(c.S))
                {
                    long fee = SchemeRules.FundFee(e.Amount, 1000);
                    c.Wallet -= fee;
                    c.Fees += fee;
                }
                if (plan == SchemeFundPlan.Always || plan == SchemeFundPlan.Reports || (plan == SchemeFundPlan.Looks && !c.S.Fund.Sham))
                {
                    if (SchemeRules.FundStands(c.S, day))
                    {
                        c.Wallet -= e.Amount;
                        c.Worth[t] += e.Amount;
                        SchemeRules.FundPlace(c.S, day);
                        c.Placed++;
                    }
                }
                else if (SchemeRules.FundDecline(c.S))
                {
                    c.Declined++;
                }
                break;
            case SchemeEventKind.FundLapsed:
                c.Lapsed++;
                break;
            case SchemeEventKind.FundPaid:
                c.Wallet += e.Net;
                c.Worth[t] = Math.Max(0L, c.Worth[t] - e.Net);
                c.Paid++;
                break;
            case SchemeEventKind.FundLost:
                c.Lost++;
                c.LostDay = day;
                if (plan == SchemeFundPlan.Reports)
                {
                    SchemeRules.FundReport(c.S);
                }
                break;
            case SchemeEventKind.FundRecovered:
                c.Wallet += e.Amount;
                c.Recovered++;
                c.WaitMost = Math.Max(c.WaitMost, day - c.LostDay);
                break;
        }
    }

    // The rivals by the same rules: who schemes and who never does, that it is found, tried by the court's own sum and loses them money; and the fund they offer.
    private static void SchemeRivalChecks()
    {
        const int saves = 150, days = 1800;
        int clean = 0, bent = 0, cleanActed = 0, bentFound = 0, bentAhead = 0, found = 0, tried = 0, convicted = 0, dropped = 0, acts = 0;
        long bentNet = 0;
        bool timely = true, same = true;
        var totals = new int[4, 6];      // by plan: offers, shams, placed, lost, recovered, paid
        var waits = new int[4];          // by plan: the longest wait for a sham's money
        var wallets = new long[4];
        var fees = new long[4];
        for (int i = 0; i < saves; i++)
        {
            int seed = SchemeRules.SeedOf("city " + i.ToString(CultureInfo.InvariantCulture));
            for (int p = 0; p < 4; p++)
            {
                SchemeCity c = SchemeCityPlay(seed, (SchemeFundPlan)p, days);
                totals[p, 0] += c.Offers;
                totals[p, 1] += c.Shams;
                totals[p, 2] += c.Placed;
                totals[p, 3] += c.Lost;
                totals[p, 4] += c.Recovered;
                totals[p, 5] += c.Paid;
                wallets[p] += c.Wallet;
                waits[p] = Math.Max(waits[p], c.WaitMost);
                fees[p] += c.Fees;
                if (p != 0)
                {
                    continue;
                }
                same &= i >= 20 || SchemeCityPlay(seed, SchemeFundPlan.Never, days).Stream == c.Stream;
                timely &= c.Timely;
                for (int t = 0; t < SchemeState.Rivals; t++)
                {
                    bool isClean = SchemeRules.Bent(seed, t) == 0;
                    clean += isClean ? 1 : 0;
                    bent += isClean ? 0 : 1;
                    cleanActed += isClean && c.Acts[t] > 0 ? 1 : 0;
                    bentFound += !isClean && c.Found[t] > 0 ? 1 : 0;
                    bentAhead += !isClean && c.Net[t] > 0 ? 1 : 0;
                    bentNet += isClean ? 0 : c.Net[t];
                    found += c.Found[t];
                    tried += c.Convicted[t] + c.Cleared[t];
                    convicted += c.Convicted[t];
                    dropped += c.Dropped[t];
                    acts += c.Acts[t];
                }
            }
        }
        int rivals = saves * SchemeState.Rivals;
        Check(Math.Abs(clean * 1000 / rivals - SchemeCatalogue.RivalCleanPermille) <= 80 && cleanActed == 0,
            "schemes rivals: about " + SchemeCatalogue.RivalCleanPermille / 10 + "% of the rivals never scheme, by a trait of their own: " + clean + " of " + rivals + ", none of whom ever did");
        Check(bent > 0 && acts > bent && bentFound * 100 >= bent * 85,
            "schemes rivals: a rival who schemes is found out as a player would be: " + bentFound + " of " + bent + " at least once in " + days + " days, over " + acts + " schemes");
        Check(bentNet < 0 && bentAhead * 100 <= bent * 35,
            "schemes rivals: scheming loses a rival money on average, as it does a player who repeats it: mean " + (bent > 0 ? bentNet / bent : 0) + ", ahead " + bentAhead + " of " + bent);
        Check(found > 0 && tried >= found - rivals && tried <= found && convicted * 100 >= tried * 30 && convicted * 100 <= tried * 90,
            "schemes rivals: every rival found out is tried by the court's own sum, and not every one is convicted: " + found + " found, " + tried + " tried, " + convicted + " convicted");
        Check(timely, "schemes rivals: a rival's findings come the same days after the letter as a player's, and the trial when the court sets a felony's");
        Check(same, "schemes rivals: the same save played twice gives the same events on the same days");
        Check(dropped == 0, "schemes rivals: an inquiry into a rival with schemes on file is never dropped, as a player's is not (" + dropped + ")");

        int never = (int)SchemeFundPlan.Never, always = (int)SchemeFundPlan.Always, looks = (int)SchemeFundPlan.Looks, reports = (int)SchemeFundPlan.Reports;
        int shamShare = totals[never, 0] > 0 ? totals[never, 1] * 1000 / totals[never, 0] : 0;
        Check(totals[never, 0] >= saves * (days / SchemeCatalogue.FundEveryDays) / 2 && totals[never, 0] <= saves * (days / SchemeCatalogue.FundEveryDays + 1) && wallets[never] == 0
              && totals[never, 2] == 0,
            "schemes fund: a fund is offered every " + SchemeCatalogue.FundEveryDays + " days at most, and one nobody answers costs nothing: " + totals[never, 0] + " offers in " + saves + " saves");
        Check(shamShare >= 150 && shamShare <= 550, "schemes fund: some funds are shams, by how bent the rival who offers is: " + shamShare / 10.0 + "% of the offers");
        Check(wallets[always] < 0 && totals[always, 3] > 0 && totals[always, 4] <= totals[always, 3],
            "schemes fund: putting money into every fund loses on average: " + wallets[always] / saves + " a save, " + totals[always, 3] + " shams, " + totals[always, 4] + " of them partly recovered");
        long best = (long)totals[looks, 0] * (SchemeRules.FundBack(SchemeRules.FundAmount(SchemeCity.PlayerWorth, 1000)) - SchemeRules.FundAmount(SchemeCity.PlayerWorth, 1000));
        Check(wallets[looks] > 0 && totals[looks, 3] == 0 && fees[looks] > 0 && wallets[looks] < best,
            "schemes fund: having each fund looked at first never loses the stake, costs its fee, and pays a little: " + wallets[looks] / saves + " a save, fees " + fees[looks] / saves
            + ", no sham entered (" + totals[looks, 3] + ")");
        int wait = SchemeCatalogue.ChargeAfter + JusticeLaw.DaysToCourt(ChargeClass.Felony) + SchemeCatalogue.ChargeAfter + 2;   // at worst they are before the court when it is reported
        Check(totals[reports, 3] > 0 && totals[reports, 4] >= totals[reports, 3] - saves && waits[reports] <= wait && totals[always, 4] < totals[always, 3]
              && totals[reports, 4] * totals[always, 3] > totals[always, 4] * totals[reports, 3] && wallets[reports] < 0 && wallets[reports] < wallets[looks],
            "schemes fund: a sham reported to the police is found out within " + wait + " days (longest " + waits[reports] + ") and " + SchemeCatalogue.FundRecoverPermille / 10
            + "% of it comes back: " + totals[reports, 4] + " of " + totals[reports, 3] + ", against " + totals[always, 4] + " of " + totals[always, 3] + " unreported; still "
            + wallets[reports] / saves + " a save, so looking first is the way");
        if (_schemeTable)
        {
            Console.WriteLine("The rivals over " + days + " days in " + saves + " saves: " + bent + " who scheme (" + acts + " schemes, " + found + " found, " + convicted + " convicted of " + tried
                              + " tried), mean result " + (bent > 0 ? bentNet / bent : 0) + "; " + clean + " who never do.");
            Console.WriteLine("Their fund (" + totals[never, 0] / (double)saves + " offers a save, " + shamShare / 10.0 + "% shams): always " + wallets[always] / saves + ", looked at first "
                              + wallets[looks] / saves + ", always and reported " + wallets[reports] / saves + " a save.");
        }

        // A session: the rivals rest, and nothing of theirs moves.
        SchemeCity rest = SchemeCityPlay(SchemeRules.SeedOf("city 3"), SchemeFundPlan.Always, 400, true);
        Check(rest.Offers == 0 && rest.Stream == 0 && rest.Wallet == 0, "schemes rivals: in a Going Public session the rivals rest: no scheme, no inquiry, no fund (" + rest.Offers + " offers)");
    }
}
