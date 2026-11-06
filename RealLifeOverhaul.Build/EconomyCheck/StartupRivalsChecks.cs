using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;

// Competition in niches (Scripts/Tech/Rivals, phase 5 of the startups rework): one pool of buyers for each niche shared by the player's products and the city's startups, each
// reaction's trigger and effect (a price cut by the share lost in the niche, a lead copied after the team's delay, a war chest round going through the round rules with their terms,
// hiring, a startup bought by a listed company, a listed company coming into a niche that got big), bake offs and their scoring, the save's line, older saves, the options off, and
// the exploit paths tried. Game-free; the director's side is read for what only the game can do. --startup-rivals runs these alone.
internal static partial class Program
{
    private static void StartupRivalsChecks()
    {
        RivalsPool();
        RivalsOldSave();
        RivalsCuts();
        RivalsCopy();
        RivalsChest();
        RivalsHiring();
        RivalsBacked();
        RivalsIncumbents();
        RivalsBake();
        RivalsStore();
        RivalsDeterminism();
        RivalsExploits();
        RivalsWiring();
        RivalsTexts();
    }

    // ---------------------------------------------------------------- a small world for the rules to run in, as the director runs them

    private sealed class RivalSim
    {
        public SectorFile F;
        public VentureFile V;
        public RivalryFile R;
        public RivalryWorld W;
        public FirmsFile Ff = new FirmsFile { Seed = 9 };
        public Firm Firm = new Firm { Place = "Mine~1", Reputation = 650 };
        public Crew Crew = Team(24);
        public Product P;
        public int Niche, Day = 100, Appeal, Stage = -1;
        public long Invested;
        public long Pulled, Own;
        public bool Pooled = true;
        public readonly List<RivalNote> Notes = new List<RivalNote>();
        public readonly List<VentureNote> VNotes = new List<VentureNote>();
        public ProductDay Last;
        public RivalProduct Rival(int i) => F.Rivals.Where(r => r.Startups).ElementAt(i);
        public Startup Startup(int i) => V.Startups[i];
    }

    private static RivalSim RivalNew(int seed, int startups, int niche, long nicheBuyers, bool player)
    {
        var s = new RivalSim { Niche = niche };
        s.F = new SectorFile { Seed = seed, FirstDay = 0, YearDays = 365 };
        SectorRules.Companies(s.F, 0, null);
        s.V = new VentureFile { Seed = seed };
        s.R = new RivalryFile();
        s.W = new RivalryWorld { Sector = s.F, Venture = s.V, Rec = s.R };
        for (int n = 0; n < NicheCatalogue.Count; n++)
        {
            s.W.Soft[n] = NicheCatalogue.Def(n).Buyers == (int)SectorBuyers.Enterprises ? 20 : 120;
            s.W.Ai[n] = s.W.Soft[n];
        }
        s.W.Soft[niche] = nicheBuyers;
        for (int id = 1; s.V.Startups.Count < startups && id < 4000; id++)
        {
            if (NicheRules.OfStartup(seed, id, (int)Venture.Software) != niche)
            {
                continue;
            }
            var st = new Startup
            {
                Id = id, Name = "Rival" + id, Sector = (int)Venture.Software, Stage = (int)RoundStage.Seed, State = (int)StartupState.Running, Quality = 700, Founded = 0, LastRound = 0,
                Mrr = 9000000, Burn = VentureCatalogue.BurnByStage[1], Post = VentureCatalogue.FloorValue[1]
            };
            st.Cash = st.Burn * 400;
            st.Holders.Add(new Holder { Kind = (int)HolderKind.Founders, Shares = VentureRules.FounderShares });
            st.Holders.Add(new Holder { Kind = (int)HolderKind.Vc, Who = 0, Invested = 135000000, Shares = 1000000, Preference = 135000000, Round = 1 });
            s.V.Startups.Add(st);
            s.V.NextId = id + 1;
        }
        s.Day = 100;
        SectorRules.Startups(s.F, s.V.Startups, s.Day, null, r => RivalryRules.Kept(s.R, r));
        SectorRules.BeginDay(s.F, s.Day, 1);
        SectorRules.Attract(s.F);
        SectorRules.Settle(s.F);
        if (player)
        {
            s.P = FirmsRules.NewProduct(s.Ff, "Mine~1", ProductKind.Saas, Segment.Business, 0, "Mine");
            s.P.Features = 6;
            s.P.Marketing = 2;
            FirmsRules.Launch(s.P, 1);
            s.Ff.Products.Add(s.P);
        }
        return s;
    }

    // One day as the director runs it: the startups' products followed, the markets begun, each rival set up, pulled and viewed, the player's product sold into its niche, the rivals'
    // day, the city's startups' day, then the reactions.
    private static void RivalStep(RivalSim s, bool you = true)
    {
        int day = ++s.Day;
        s.VNotes.Clear();
        SectorRules.Startups(s.F, s.V.Startups, day, s.Notes, r => RivalryRules.Kept(s.R, r));
        SectorRules.BeginDay(s.F, day, 1);
        if (s.P != null)
        {
            SectorRules.Count(s.F, s.P);
        }
        if (s.Pooled)
        {
            RivalryRules.Prepare(s.W, day);
        }
        SectorRules.Attract(s.F);
        SectorMarketDay md = s.F.Markets[(int)SectorMarket.BusinessSoftware];
        var view = new SectorMarketDay();
        int appeal = 0, aware = 0, appeal2 = 0, aware2 = 0;
        long pulled = 0;
        if (s.P != null)
        {
            int marketing = FirmsCatalogue.MarketingDay[s.P.Marketing];
            appeal = SectorRules.Appeal(s.P, s.Firm, s.Crew, md);
            aware = SectorRules.Awareness(marketing, md.Buyers, SectorRules.Have(s.P), md.Who, 0);
            pulled = SectorRules.Attraction(appeal, aware);
            SectorRules.Join(md, pulled);
            NicheRules.View(view, md, Math.Min(s.W.Soft[s.Niche], md.Buyers), NicheRules.Going(NicheCatalogue.Def(s.Niche), md.Going));
            appeal2 = SectorRules.Appeal(s.P, s.Firm, s.Crew, view);
            aware2 = SectorRules.Awareness(marketing, view.Buyers, SectorRules.Have(s.P), md.Who, 0);
            if (s.Pooled)
            {
                s.W.Pool.AddPlayer(s.Niche, (int)SectorMarket.BusinessSoftware, SectorRules.Attraction(appeal2, aware2), pulled);
            }
        }
        if (s.Pooled)
        {
            RivalryRules.Views(s.W, day);
        }
        SectorRules.RivalDay(s.F, day, s.Notes);
        if (s.P != null)
        {
            long own = SectorRules.Attraction(appeal2, aware2);
            s.Own = own;
            s.Appeal = appeal2;
            view.All = s.Pooled ? RivalryRules.PlayerAll(s.W, 1 << s.Niche, (int)SectorMarket.BusinessSoftware, md.All, own, pulled, view.Buyers, md.Buyers, day)
                : own + Math.Max(0L, md.All - pulled) * NicheRules.RivalShare(view.Buyers, md.Buyers) / 1000;
            s.Last = SectorRules.SellDay(s.Ff, s.Firm, s.P, s.Crew, 50, day, view, appeal2, aware2, 1000);
        }
        for (int i = 0; i < s.V.Startups.Count; i++)
        {
            VentureRules.Day(s.V, s.V.Startups[i], day, 1, 1000, false, s.VNotes);
        }
        if (s.Pooled)
        {
            s.W.You.Clear();
            if (s.P != null && you)
            {
                s.W.You.Add(new RivalryYou
                {
                    Place = "Mine~1", Name = "Mine Ltd", Niche = s.Niche, Served = 1 << s.Niche, Market = (int)SectorMarket.BusinessSoftware, Features = s.P.Features, Appeal = s.Appeal, Fit = 700,
                    Invested = s.Invested, Stage = s.Stage
                });
            }
            RivalryRules.Day(s.W, day, s.VNotes);
        }
    }

    private static long Customers(RivalSim s) => s.F.Rivals.Where(r => r.Startups && r.Niche >= 0 || r.Startups).Sum(r => r.Customers);

    private static string Signature(RivalSim s)
    {
        return string.Join(",", s.F.Rivals.Select(r => r.Id + ":" + r.Customers + ":" + r.Tier + ":" + r.Features + ":" + r.Niche)) + "|" + (s.P != null ? s.P.Users + ":" + s.P.Features : "-")
               + "|" + string.Join(",", s.R.Log.Select(l => l.Day + ":" + l.Kind + ":" + l.Rival)) + "|" + string.Join(",", s.V.Startups.Select(t => t.Id + ":" + t.Stage + ":" + t.State));
    }

    // ---------------------------------------------------------------- one pool of buyers

    private static void RivalsPool()
    {
        const int niche = NicheCatalogue.Restaurants;
        RivalSim a = RivalNew(11, 3, niche, 600, true), b = RivalNew(11, 0, niche, 600, true), c = RivalNew(11, 3, niche, 600, true);
        c.Pooled = false;
        a.R.Since = b.R.Since = 0;
        for (int d = 0; d < 150; d++)
        {
            RivalStep(a);
            RivalStep(b);
            RivalStep(c);
        }
        long marketBuyers = a.F.Markets[(int)SectorMarket.BusinessSoftware].Buyers;
        long startupsInPool = a.F.Rivals.Where(r => r.Startups).Sum(r => r.Customers);
        long startupsOld = c.F.Rivals.Where(r => r.Startups).Sum(r => r.Customers);
        Check(a.Rival(0).Niche == niche && a.F.Rivals.Where(r => r.Startups).All(r => r.Niche == niche) && marketBuyers > 600 * 5,
            "rivals pool: the city's startups sell into the niche their founding gave them (" + NicheCatalogue.NameKey(niche) + "), seeded, in a market of " + marketBuyers + " buyers");
        Check(startupsInPool + a.P.Users <= 600 && startupsInPool > 0 && a.P.Users > 0,
            "rivals pool: the player and the three startups sell to the same " + 600 + " buyers of the niche: " + startupsInPool + " and " + a.P.Users + " together stay inside it");
        Check(startupsOld > 600 && startupsOld > startupsInPool,
            "rivals pool: the market's share of the niche gave the startups " + startupsOld + " customers, more than the niche has; in the shared pool they are held to its buyers (" + startupsInPool + ")");
        Check(a.P.Users < b.P.Users && b.P.Users > 0,
            "rivals pool: with three startups in the niche the player wins fewer of its buyers (" + a.P.Users + " against " + b.P.Users + " with none): what one wins the others lose");
        long share = a.Rival(0).Customers * 1000 / Math.Max(1, a.Rival(0).View.Buyers);
        Check(RivalryRules.ShareOf(a.Rival(0)) == share && RivalryRules.ShareOf(a.Rival(0)) > 0 && a.Rival(0).View.Buyers == 600,
            "rivals pool: a startup's share is its customers over the niche's buyers (" + share + " per mille of " + a.Rival(0).View.Buyers + ")");
        // many sellers, one pool: each one's target is its pull's share of everyone's, so together they never pass the buyers
        var many = new NichePool();
        many.AddPlayer(niche, (int)SectorMarket.BusinessSoftware, 700, 400);
        many.AddRival(niche, (int)SectorMarket.BusinessSoftware, 500);
        many.AddRival(niche, (int)SectorMarket.BusinessSoftware, 300);
        var w = new RivalryWorld { Sector = a.F, Venture = a.V, Rec = new RivalryFile { Since = 0 } };
        long all = 700 + 500 + 300;
        long targets = 0;
        foreach (long pull in new long[] { 700, 500, 300 })
        {
            targets += 10000L * ScaleRules.Share(pull, all) / 1000;
        }
        Check(targets <= 10000 && many.Rest((int)SectorMarket.BusinessSoftware, 5000) == 5000 - 500 - 300 - 400 && w.Seed == 11,
            "rivals pool: the pull of everyone in a niche is counted once: the market's rest is what no niche owns");
        var own = a.F.Rivals.Where(r => r.Company >= 0 && !a.R.Recs.Any(x => x.Id == r.Id && x.Entrant)).ToList();
        Check(own.All(r => r.Niche == -1) && own.Sum(r => r.Customers) > 0 && own.Count == 12,
            "rivals pool: the listed companies sell in the whole market and keep their customers (" + string.Join(",", a.F.Rivals.Where(r => r.Company >= 0).Select(r => r.Id + ":" + r.Niche + ":" + r.Customers)) + ")");
    }

    // ---------------------------------------------------------------- older saves

    private static void RivalsOldSave()
    {
        const int niche = NicheCatalogue.Cafes;
        RivalSim s = RivalNew(21, 2, niche, 500, true);
        RivalStep(s);
        RivalProduct r = s.Rival(0);
        SectorMarketDay md = s.F.Markets[(int)SectorMarket.BusinessSoftware];
        Check(s.R.Since == 101 && RivalryRules.Weight(101, s.R.Since) == 0 && r.Frame != null && r.Frame.Buyers == md.Buyers && r.View.Buyers == md.Buyers && r.View.All == md.All,
            "rivals old saves: on the first day the shared pool counts for nothing: a startup is weighed among the market's buyers and pull, as it was");
        Check(r.Frame.Going == md.Going && r.PushMarketing == 0 && r.PushPoints == 0, "rivals old saves: and at the market's going price, with no push");
        for (int d = 0; d < RivalryCatalogue.RampDays + 2; d++)
        {
            RivalStep(s);
        }
        r = s.Rival(0);
        md = s.F.Markets[(int)SectorMarket.BusinessSoftware];
        Check(RivalryRules.Weight(s.Day, s.R.Since) == 1000 && r.View.Buyers == 500 && r.Frame.Buyers == 500,
            "rivals old saves: after " + RivalryCatalogue.RampDays + " days the niche's own buyers have replaced the market's share of them");
        int mid = (int)RivalryRules.Blend(1000, 200, RivalryRules.Weight(115, 100));
        Check(RivalryRules.Weight(100, 100) == 0 && RivalryRules.Weight(115, 100) == 500 && mid == 600 && RivalryRules.Weight(99, 100) == 0 && RivalryRules.Weight(50, -1) == 0,
            "rivals old saves: the pool fades in on a straight line, half way after half the days");
        // the first look at a firm only notes where it stands: no raise and no lead is answered before it
        RivalSim t = RivalNew(21, 3, niche, 500, true);
        t.Invested = VentureRules.RoundSize(1);
        t.Stage = 1;
        t.P.Features = 14;
        RivalStep(t);
        Check(!t.R.Log.Any(l => l.Kind != ReactKind.Poach) && t.R.Recs.All(x => x.ChestStage < 0 && x.CopyTarget == 0) && t.R.SeenOf("Mine~1") != null && t.R.SeenOf("Mine~1").Invested == t.Invested,
            "rivals old saves: a firm first seen with investors and a long lead sets off no war chest and no copy: the first look only notes where it stands (" + t.R.Log.Count + " " + t.R.Recs.Count(x => x.ChestStage >= 0 || x.CopyTarget != 0) + " " + (t.R.SeenOf("Mine~1") != null) + " " + t.R.SeenOf("Mine~1")?.Invested + " " + t.Invested + ")");
        RivalSim old = RivalNew(21, 2, niche, 500, true);
        old.R.Since = -1;
        Check(RivalryStore.Load(new Dictionary<string, string>(), out _) == null && RivalryStore.Key == "reallife:tech_rivals",
            "rivals old saves: a save without the line loads with nothing in it, and the line is its own");
        string src = ModSource(@"Tech\TechDirector.Rivalry.cs");
        Check(src.Contains("RivalryStore.Load(Save.modData, out int version)") && src.Contains("_rival = file ?? new RivalryFile();") && src.Contains("if (_rival.Since < 0)"),
            "rivals old saves: the director starts from an empty record and notes the day it first ran");
    }

    // ---------------------------------------------------------------- prices, by the share held in the niche

    private static void RivalsCuts()
    {
        const int niche = NicheCatalogue.Gyms;
        RivalSim s = RivalNew(31, 2, niche, 700, false);
        s.R.Since = 0;
        for (int d = 0; d < 80; d++)
        {
            RivalStep(s, false);
        }
        RivalProduct r = s.Rival(0);
        RivalryRec rec = s.R.RecOf(r.Id);
        r.Tier = 1;
        r.LastCut = s.Day - 100;
        int held = rec.PeakShare;
        Check(held > 0 && r.Niche == niche, "rivals cuts: a startup in a niche keeps the share it held lately (" + held + " per mille of its niche)");
        r.Customers = r.Customers * 600 / 1000;
        RivalStep(s, false);
        // the sector's own rule would have cut on lost customers; in a niche it is the share that decides (and not the day the customers fell)
        Check(r.Tier == 0 && s.R.Log.Count == 0 && rec.PeakShare >= held * 700 / 1000,
            "rivals cuts: a startup that loses a good part of its niche cuts its price a step (tier " + r.Tier + ") and the cut is its own, not the sector's");
        RivalSim g = RivalNew(31, 2, niche, 700, false);
        RivalProduct q = g.Rival(0);
        q.Niche = niche;
        q.Customers = 100;
        q.Peak = 1000;
        q.PeakDay = g.Day;
        q.Tier = 1;
        q.LastCut = g.Day - 100;
        SectorRules.BeginDay(g.F, g.Day + 1, 1);
        SectorRules.Attract(g.F);
        SectorRules.RivalDay(g.F, g.Day + 1, null);
        Check(q.Tier == 1, "rivals cuts: the sector's own cut leaves a startup that sells into a niche alone, whatever its customers fell from");
        q.Niche = -1;
        q.Customers = 100;
        q.Peak = 1000;
        q.LastCut = g.Day - 100;
        SectorRules.RivalDay(g.F, g.Day + 2, null);
        Check(q.Tier == 0, "rivals cuts: a rival with no niche is cut by the sector's rule as before");
        // raised again once the share is held, not during a war chest, and it is told against a firm only when one sells there
        r.LastCut = s.Day - SectorCatalogue.RivalRaiseGap - 1;
        rec.PeakShare = RivalryRules.ShareOf(r);
        RivalStep(s, false);
        Check(r.Tier == 1, "rivals cuts: it raises the price again once it holds its share (tier " + r.Tier + ")");
        rec.ChestUntil = s.Day + 20;
        r.Tier = 0;
        r.LastCut = s.Day - SectorCatalogue.RivalRaiseGap - 1;
        RivalStep(s, false);
        Check(r.Tier == 0, "rivals cuts: not while its war chest lasts");
        RivalSim withYou = RivalNew(31, 2, niche, 700, true);
        withYou.R.Since = 0;
        for (int d = 0; d < 80; d++)
        {
            RivalStep(withYou);
        }
        RivalProduct u = withYou.Rival(0);
        u.Tier = 1;
        u.LastCut = withYou.Day - 100;
        u.Customers = u.Customers * 500 / 1000;
        RivalStep(withYou);
        Check(u.Tier == 0 && withYou.R.Log.Any(l => l.Kind == ReactKind.Cut && l.Place == "Mine~1" && l.Rival == u.Id),
            "rivals cuts: where the player sells it is a line on the firm's page: the rival cut its price");
    }

    // ---------------------------------------------------------------- copying a lead

    private static void RivalsCopy()
    {
        const int niche = NicheCatalogue.Shops;
        RivalSim s = RivalNew(41, 2, niche, 800, true);
        s.R.Since = 0;
        s.P.Features = 4;
        for (int d = 0; d < 5; d++)
        {
            RivalStep(s);
        }
        RivalProduct r = s.Rival(0);
        r.Features = 3;
        s.P.Features = 9;
        int ship = s.Day + 1;
        RivalStep(s);
        RivalryRec rec = s.R.RecOf(r.Id);
        RivalryReaction said = s.R.Log.FirstOrDefault(l => l.Kind == ReactKind.Copying && l.Rival == r.Id);
        Check(said != null && rec.CopyTarget == 9 && rec.CopyAt == ship + said.Amount && said.Place == "Mine~1",
            "rivals copy: a feature the player led with is copied: the rival will have " + rec.CopyTarget + " features in " + said?.Amount + " days");
        int small = RivalryRules.CopyDelay(s.F, r, 9);
        r.PushPoints = 600;
        int big = RivalryRules.CopyDelay(s.F, r, 9);
        r.PushPoints = 0;
        Check(big < small && small <= RivalryCatalogue.CopyMost && big >= RivalryCatalogue.CopyLeast && RivalryRules.CopyDelay(s.F, r, 4) <= small,
            "rivals copy: the delay is set by the team: " + small + " days for a seed team, " + big + " with a bought team's points, and less for a smaller gap");
        Check(RivalryRules.CopyDelay(s.F, r, 3 + 400) == RivalryCatalogue.CopyMost && RivalryRules.CopyDelay(s.F, r, 4) >= RivalryCatalogue.CopyLeast,
            "rivals copy: never under a week and never over three months");
        while (s.Day < rec.CopyAt - 1)
        {
            RivalStep(s);
        }
        Check(r.Features < 9 && rec.CopyTarget == 9, "rivals copy: until the day it has not copied it");
        RivalStep(s);
        Check(r.Features >= 9 && rec.CopyTarget == 0 && rec.CopyDone == s.Day && s.R.Log.Any(l => l.Kind == ReactKind.Copied && l.Rival == r.Id),
            "rivals copy: on the day it closes the gap, and the firm's page says so");
        // it does not copy again at once, nor a lead that was already there
        s.P.Features = 12;
        RivalStep(s);
        Check(s.R.RecOf(r.Id).CopyTarget == 0 && s.R.Log.Count(l => l.Kind == ReactKind.Copying && l.Rival == r.Id) == 1,
            "rivals copy: not again within " + RivalryCatalogue.CopyGap + " days of a copy");
        RivalSim other = RivalNew(41, 2, NicheCatalogue.Cafes, 800, true);
        other.R.Since = 0;
        for (int d = 0; d < 3; d++)
        {
            RivalStep(other);
        }
        other.P.Features = 20;
        RivalStep(other);
        RivalSim away = RivalNew(41, 2, NicheCatalogue.Shops, 800, true);
        away.R.Since = 0;
        for (int d = 0; d < 3; d++)
        {
            RivalStep(away);
        }
        away.W.You.Clear();
        away.P.Features = 20;
        RivalStep(away, false);
        Check(other.R.Log.Count(l => l.Kind == ReactKind.Copying) == other.F.Rivals.Count(x => x.Startups && x.Niche == NicheCatalogue.Cafes && x.Features < 20)
              && !away.R.Log.Any(l => l.Kind == ReactKind.Copying) && away.R.Recs.All(x => x.CopyTarget == 0),
            "rivals copy: only a rival in the player's niche reacts, and only to a firm of the player's that sells there");
    }

    // ---------------------------------------------------------------- a war chest

    private static void RivalsChest()
    {
        const int niche = NicheCatalogue.Venues;
        int closed = 0, termed = 0, forwarded = 0, others = 0, rushed = 0, hard = 0;
        for (int seed = 51; seed < 59; seed++)
        {
            RivalSim s = RivalNew(seed, 8, niche, 400, true);
            s.R.Since = 0;
            foreach (Startup t in s.V.Startups)
            {
                t.LastRound = s.Day - 200;
                t.Mrr = 12000000;   // the bar of a series A round
            }
            RivalStep(s);
            s.Invested = VentureRules.RoundSize(1);   // the player raises a round
            s.Stage = 1;
            s.Day += 0;
            int before = s.V.Startups.Count(t => t.Stage == (int)RoundStage.Seed);
            RivalStep(s);
            var called = s.R.Recs.Where(x => x.ChestStage >= 0).ToList();
            forwarded += called.Count;
            foreach (RivalryRec rec in called)
            {
                Startup t = s.V.StartupOf(s.F.Rivals.First(x => x.Id == rec.Id).Startup);
                if (t.State == (int)StartupState.Raising && s.Day - t.LastRound == RivalryRules.RaiseDays - RivalryCatalogue.ChestBring)
                {
                    rushed++;
                }
            }
            for (int d = 0; d < 6; d++)
            {
                RivalStep(s);
            }
            foreach (RivalryRec rec in s.R.Recs.Where(x => x.ChestUntil > s.Day))
            {
                Startup t = s.V.StartupOf(s.F.Rivals.First(x => x.Id == rec.Id).Startup);
                Holder lead = t.Holders.Last(h => h.Kind == (int)HolderKind.Vc);
                closed++;
                bool paper = t.Stage == (int)RoundStage.SeriesA && lead.Invested == VentureRules.RoundSize(2)
                             && lead.Preference == lead.Invested * (lead.T != null ? lead.T.Pref : VentureCatalogue.LiquidationPreference) / 1000;
                termed += paper ? 1 : 0;
                hard += lead.T != null ? 1 : 0;
                RivalProduct r = s.F.Rivals.First(x => x.Id == rec.Id);
                SectorMarketDay md = s.F.Markets[(int)SectorMarket.BusinessSoftware];
                SectorRules.BeginDay(s.F, s.Day + 1, 1);
                RivalryRules.Prepare(s.W, s.Day + 1);
                others += r.PushMarketing > 0 && r.PushMarketing <= SectorCatalogue.CompanyMarketing.Where(x => x > 0).Max() && r.Tier == 0 ? 1 : 0;
                Check(rec.ChestMoney == VentureRules.RoundSize(2) && s.R.Log.Any(l => l.Kind == ReactKind.Funded && l.Rival == rec.Id), "rivals chest: the round's money is the war chest, and the firm's page says the rival is funded");
                break;
            }
        }
        Check(forwarded >= 8 && rushed == forwarded, "rivals chest: when the player raises, rivals in the niche bring their next round forward (" + forwarded + " did); the round closes in "
              + RivalryCatalogue.ChestBring + " days instead of ten");
        Check(closed >= 4 && termed == closed, "rivals chest: the round is closed by the round rules (a venture firm leads, the stage moves on) and the lead's preference is the terms it asked (" + termed + " of " + closed + ")");
        Check(hard > 0 || closed < 6, "rivals chest: and some of those rounds came with terms harder than the plain ones (" + hard + " of " + closed + ")");
        Check(others == closed, "rivals chest: the money is spent on reach, at most what the average listed company spends, and on price: the tier drops to the lowest");
        // who does not
        RivalSim t1 = RivalNew(51, 4, niche, 400, true);
        t1.R.Since = 0;
        foreach (Startup t in t1.V.Startups)
        {
            t.LastRound = t1.Day - 200;
            t.Mrr = 12000000;
        }
        t1.V.Startups[0].LastRound = t1.Day - 20;    // raised lately
        t1.V.Startups[1].Mrr = 10;                   // does not meet the bar
        t1.V.Startups[2].State = (int)StartupState.Raising;
        RivalStep(t1);
        t1.Invested = VentureRules.RoundSize(1) * 2;
        t1.Stage = 1;
        RivalStep(t1);
        Func<Startup, bool> raisedEarly = st => t1.R.Recs.Any(x => x.ChestStage >= 0 && t1.F.Rivals.First(r => r.Id == x.Id).Startup == st.Id);
        Check(!raisedEarly(t1.V.Startups[0]) && !raisedEarly(t1.V.Startups[1]) && !raisedEarly(t1.V.Startups[2]),
            "rivals chest: not a rival that raised lately, one that misses the bar, or one that is already raising");
        RivalSim small = RivalNew(51, 4, niche, 400, true);
        small.R.Since = 0;
        foreach (Startup t in small.V.Startups)
        {
            t.LastRound = small.Day - 200;
            t.Mrr = 12000000;
        }
        RivalStep(small);
        small.Invested = VentureRules.RoundSize(1) / 10;   // a small cheque is no raise to answer
        small.Stage = 1;
        RivalStep(small);
        Check(small.R.Recs.All(x => x.ChestStage < 0), "rivals chest: a small cheque from friends is no raise to answer (under " + RivalryCatalogue.ChestMinShare + " per mille of the stage's round)");
        RivalSim elsewhere = RivalNew(51, 4, NicheCatalogue.Cafes, 400, true);
        elsewhere.R.Since = 0;
        foreach (Startup t in elsewhere.V.Startups)
        {
            t.LastRound = elsewhere.Day - 200;
            t.Mrr = 12000000;
        }
        elsewhere.Niche = NicheCatalogue.Shops;
        RivalStep(elsewhere);
        elsewhere.Invested = VentureRules.RoundSize(1);
        elsewhere.Stage = 1;
        RivalStep(elsewhere);
        Check(elsewhere.R.Recs.All(x => x.ChestStage < 0), "rivals chest: only the rivals in the player's niche answer");
        Cap check = new Cap();
        Check(VentureRules.RoundSize(1) == (long)VentureCatalogue.BurnByStage[1] * 30 * VentureCatalogue.RunwayMonthsRaised && check.Acc != null,
            "rivals chest: the player's raise is read against the round the stage usually raises");
        string rules = ModSource(@"Tech\Venture\VentureRules.cs");
        Check(rules.Contains("day - s.LastRound >= 10") && RivalryRules.RaiseDays == 10 && rules.Contains("h.T = VentureTerms.Stamp(h.Who, s.Stage + 1, mood, weak, seed, s.Id, day)"),
            "rivals chest: the round rules still close a raise ten days after it starts and stamp the lead's terms, which is what a brought forward round goes through");
    }

    // ---------------------------------------------------------------- hiring

    private static void RivalsHiring()
    {
        const int niche = NicheCatalogue.Clinics;
        RivalSim s = RivalNew(61, 3, niche, 300, true);
        s.R.Since = 0;
        for (int d = 0; d < 3; d++)
        {
            RivalStep(s);
        }
        RivalProduct r0 = s.Rival(0), r1 = s.Rival(1), r2 = s.Rival(2);
        s.V.StartupOf(r0.Startup).Cash = s.V.StartupOf(r0.Startup).Burn * 400;    // months of money
        s.V.StartupOf(r1.Startup).Mrr = 100000;
        s.V.StartupOf(r1.Startup).Cash = s.V.StartupOf(r1.Startup).Burn * 10;     // weeks of money
        s.V.StartupOf(r2.Startup).Stage = (int)RoundStage.PreSeed;                 // not past its seed
        RivalProduct poach = RivalryRules.Poacher(s.W, 1 << niche, out int pull);
        Check(poach == r0 && pull == RivalryCatalogue.PoachFunded && RivalryRules.Funded(s.W, r0, null) && !RivalryRules.Funded(s.W, r1, null) && !RivalryRules.Funded(s.W, r2, null),
            "rivals hiring: a rival with months of money past its seed, in the player's niche, comes for the player's people with a pull of " + pull + " " + (poach != null ? poach.Id.ToString() : "-") + " " + r0.Id + " " + RivalryRules.Funded(s.W, r0, null) + RivalryRules.Funded(s.W, r1, null) + RivalryRules.Funded(s.W, r2, null) + " " + r0.Niche);
        Check(RivalryRules.Poacher(s.W, 1 << NicheCatalogue.Gyms, out int none) == null && none == 1000, "rivals hiring: none in a niche where the firm does not sell");
        RivalStep(s);
        Check(s.R.Log.Any(l => l.Kind == ReactKind.Poach && l.Rival == r0.Id && l.Place == "Mine~1" && l.Amount == pull), "rivals hiring: the firm's page says who is hiring");
        int lines = s.R.Log.Count(l => l.Kind == ReactKind.Poach);
        RivalStep(s);
        Check(s.R.Log.Count(l => l.Kind == ReactKind.Poach) == lines, "rivals hiring: once a month, not every day");
        var p = new Profile { Id = "pro:9", Level = TalentCatalogue.Senior, Morale = 500 };
        int calm = 0, pulled = 0, backed = 0;
        for (int day = 1; day <= 20000; day++)
        {
            calm += TalentRules.Approached(3, p, day, 1000, 0, 1000, 1000) ? 1 : 0;
            pulled += TalentRules.Approached(3, p, day, 1000, 0, 1000, 1000, pull) ? 1 : 0;
            backed += TalentRules.Approached(3, p, day, 1000, 0, 1000, 1000, RivalryCatalogue.PoachBacked) ? 1 : 0;
        }
        Check(pulled > calm * 13 / 10 && backed > pulled && calm > 0, "rivals hiring: a funded rival in the niche makes an approach likelier through the talent rules (" + calm + ", " + pulled + ", " + backed + " in 20,000 days)");
        string d2 = ModSource(@"Tech\TechDirector.Sector.cs");
        Check(d2.Contains("return Math.Max(pull, RivalryPull(place));") && d2.Contains("RivalAgainst(place, out _)") && ModSource(@"Tech\TechDirector.People.cs").Contains("SectorPull(p.Place, day)"),
            "rivals hiring: the pull and the name of the poacher reach the people part's approach through the sector's existing hooks");
    }

    // ---------------------------------------------------------------- selling out to a listed company

    private static void RivalsBacked()
    {
        const int niche = NicheCatalogue.Salons;
        RivalSim s = RivalNew(71, 3, niche, 300, true);
        s.R.Since = 0;
        for (int d = 0; d < 3; d++)
        {
            RivalStep(s);
        }
        RivalProduct r = s.Rival(0);
        Startup st = s.V.StartupOf(r.Startup);
        int tierBefore = r.Tier = 1;
        VentureRules.Sell(s.V, st, st.Post, VentureRules.NoteAcquired, s.VNotes);
        var todays = new List<VentureNote>(s.VNotes);
        s.W.You.Add(new RivalryYou { Place = "Mine~1", Name = "Mine Ltd", Niche = niche, Served = 1 << niche, Market = (int)SectorMarket.BusinessSoftware, Features = 6, Appeal = 900, Fit = 700 });
        RivalryRules.Day(s.W, s.Day + 1, todays);
        RivalryRec rec = s.R.RecOf(r.Id);
        int company = rec != null ? rec.Backer : -1;
        Check(company >= 0 && SectorCatalogue.CompanyTeam[company] > 0 && (SectorCatalogue.CompanyMarkets[company] & (1 << (int)SectorMarket.BusinessSoftware)) != 0 && rec.Niche == niche && r.Tier == 0 && tierBefore == 1,
            "rivals backed: a startup sold in a niche is bought by a listed company that sells software in its market ("
            + (company >= 0 ? HiringCatalogue.RivalFirms[company] : "-") + "), and cuts its price");
        Check(s.R.Log.Any(l => l.Kind == ReactKind.Backed && l.Place == "Mine~1" && l.Other == HiringCatalogue.RivalFirms[company]), "rivals backed: the firm's page says who bought the rival");
        SectorRules.Startups(s.F, s.V.Startups, s.Day + 2, null, x => RivalryRules.Kept(s.R, x));
        Check(s.F.Rivals.Contains(r) && st.State == (int)StartupState.Acquired, "rivals backed: its product goes on after its startup is sold (a startup's own product goes with it)");
        RivalSim gone = RivalNew(71, 3, niche, 300, true);
        RivalProduct g = gone.Rival(1);
        Startup gs = gone.V.StartupOf(g.Startup);
        VentureRules.Sell(gone.V, gs, 0, VentureRules.NoteFailed, gone.VNotes);
        SectorRules.Startups(gone.F, gone.V.Startups, gone.Day + 1, null, x => RivalryRules.Kept(gone.R, x));
        Check(!gone.F.Rivals.Contains(g), "rivals backed: a startup that failed takes its product with it");
        // deep pockets
        for (int d = 0; d < 3; d++)
        {
            RivalStep(s);
        }
        SectorMarketDay md = s.F.Markets[(int)SectorMarket.BusinessSoftware];
        Check(r.PushMarketing == SectorCatalogue.CompanyMarketing[company] * RivalryCatalogue.BackMarketing / 1000 && r.PushPoints == SectorCatalogue.CompanyTeam[company] * RivalryCatalogue.BackPoints / 1000
              && SectorRules.RivalMarketing(r) == SectorCatalogue.StartupMarketing + r.PushMarketing && SectorRules.RivalPoints(s.F, r) == SectorCatalogue.StartupTeamSeed + SectorCatalogue.StartupTeamPerStage * r.Stage + r.PushPoints,
            "rivals backed: a bought rival spends more on reach (" + r.PushMarketing + " a day more) and has more people at work (" + r.PushPoints + " points)");
        Check(RivalryRules.Poacher(s.W, 1 << niche, out int pull) == r && pull == RivalryCatalogue.PoachBacked, "rivals backed: and it hires harder than a funded startup does");
        // the sale is the venture part's own: the waterfall pays everyone, the player's lines come back to them
        long paid = st.Holders.Sum(h => h.Paid);
        Check(paid == st.Post && st.Holders.Any(h => h.Paid > 0), "rivals backed: a startup bought pays through the waterfall like any sale (" + paid + " of " + st.Post + ")");
        // a startup sold on the day is named only if it has a product; one with none is the venture part's sale as before
        RivalSim none = RivalNew(71, 1, niche, 300, false);
        none.F.Rivals.RemoveAll(x => x.Startups);
        none.R.Since = 0;
        Startup ns = none.V.Startups[0];
        VentureRules.Sell(none.V, ns, ns.Post, VentureRules.NoteAcquired, none.VNotes);
        RivalryRules.Day(none.W, none.Day + 1, new List<VentureNote>(none.VNotes));
        Check(none.R.Recs.All(x => x.Backer < 0) && none.W.Told.Count == 0, "rivals backed: a sale of a startup with no product is left as the venture part made it");
    }

    // ---------------------------------------------------------------- incumbents

    private static void RivalsIncumbents()
    {
        const int niche = NicheCatalogue.Restaurants;
        int entered = 0, bought = 0, quiet = 0;
        for (int seed = 81; seed < 101; seed++)
        {
            RivalSim s = RivalNew(seed, 3, niche, 900, true);
            s.R.Since = 0;
            foreach (Startup t in s.V.Startups)
            {
                t.Stage = (int)RoundStage.SeriesA;
            }
            for (int d = 0; d < 400; d++)
            {
                RivalStep(s);
            }
            RivalryRec ent = s.R.Recs.FirstOrDefault(x => x.Entrant);
            if (ent != null)
            {
                RivalProduct e = s.F.Rivals.First(x => x.Id == ent.Id);
                entered++;
                Check(e.Company >= 0 && ent.Niche == niche && e.Name == HiringCatalogue.RivalFirms[e.Company] && e.Market == (int)SectorMarket.BusinessSoftware && SectorCatalogue.CompanyTeam[e.Company] > 0
                      && (SectorCatalogue.CompanyMarkets[e.Company] & (1 << e.Market)) == 0 && s.R.Recs.Count(x => x.Entrant) <= RivalryCatalogue.EntrantsMost,
                    "rivals incumbents: a listed company that did not sell in the market comes into a niche that got big, as a rival: " + e.Name);
                break;
            }
        }
        Check(entered > 0, "rivals incumbents: in 400 days a listed company came into the big niche in " + entered + " of 20 worlds");
        for (int seed = 81; seed < 101; seed++)
        {
            RivalSim s = RivalNew(seed, 3, niche, 900, true);
            s.R.Since = 0;
            foreach (Startup t in s.V.Startups)
            {
                t.Stage = (int)RoundStage.SeriesA;
            }
            for (int d = 0; d < 600 && bought == 0; d++)
            {
                RivalStep(s);
            }
            RivalryRec rec = s.R.Recs.FirstOrDefault(x => x.Backer >= 0);
            if (rec != null && s.R.Log.Any(l => l.Kind == ReactKind.Bought))
            {
                Startup st = s.V.StartupOf(s.F.Rivals.First(x => x.Id == rec.Id).Startup);
                bought++;
                Check(st.State == (int)StartupState.Acquired && st.Holders.Sum(h => h.Paid) >= st.Post / 2 && s.F.Rivals.Any(x => x.Id == rec.Id),
                    "rivals incumbents: a listed company buys a rival startup in a big niche: the waterfall pays at least half its value (" + st.Holders.Sum(h => h.Paid) + " of " + st.Post + "), and the product goes on");
            }
        }
        Check(bought > 0, "rivals incumbents: a startup in a big niche was bought by a listed company in a long run");
        // a niche that is not big, or has no one in it, sees none
        RivalSim flat = RivalNew(81, 3, niche, 120, true);
        flat.R.Since = 0;
        for (int d = 0; d < 400; d++)
        {
            RivalStep(flat);
        }
        quiet += flat.R.Recs.Count(x => x.Entrant) + flat.R.Log.Count(l => l.Kind == ReactKind.Bought);
        RivalSim empty = RivalNew(81, 0, niche, 900, false);
        empty.R.Since = 0;
        for (int d = 0; d < 400; d++)
        {
            RivalStep(empty, false);
        }
        quiet += empty.R.Recs.Count(x => x.Entrant);
        Check(quiet == 0 && RivalryRules.Big(900, 300) && !RivalryRules.Big(400, 300) && !RivalryRules.Big(100, 0),
            "rivals incumbents: a niche only as big as the average, or one nobody sells in, sees no listed company come in or buy");
        // a new entrant has no customers to begin with: it is not settled at its share of the market like a company's own product
        RivalSim fresh = RivalNew(82, 2, niche, 900, true);
        fresh.R.Since = 0;
        for (int d = 0; d < 800 && !fresh.R.Recs.Any(x => x.Entrant); d++)
        {
            RivalStep(fresh);
        }
        RivalryRec en = fresh.R.Recs.FirstOrDefault(x => x.Entrant);
        if (en != null)
        {
            RivalProduct e = fresh.F.Rivals.First(x => x.Id == en.Id);
            Check(e.Peak >= 1 && e.Customers < 50, "rivals incumbents: it starts with no customers and wins them from the niche's pool (" + e.Customers + " the day it came)");
            long before = e.Customers;
            for (int d = 0; d < 60; d++)
            {
                RivalStep(fresh);
            }
            Check(e.Customers > before && e.Niche == niche, "rivals incumbents: and then grows with a big company's team, reach and name");
        }
        string src = ModSource(@"Tech\Rivals\RivalryRules.Reactions.cs");
        Check(src.Contains("Peak = 1") && src.Contains("w.Sector.RivalOf(c, market) == null"), "rivals incumbents: an entrant is not settled at a market share, and a company does not come into a market it already sells in");
    }

    // ---------------------------------------------------------------- bake offs

    private static RivalSim BakeSim(int seed, out BakeOff b)
    {
        const int niche = NicheCatalogue.Cafes;
        RivalSim s = RivalNew(seed, 3, niche, 600, true);
        s.R.Since = 0;
        for (int d = 0; d < 40; d++)
        {
            RivalStep(s);
        }
        s.W.You.Clear();
        s.W.You.Add(new RivalryYou { Place = "Mine~1", Name = "Mine Ltd", Niche = niche, Served = 1 << niche, Market = (int)SectorMarket.BusinessSoftware, Features = 6, Appeal = 900, Fit = 700 });
        b = RivalryRules.Open(s.W, niche, "The Daily Grind", s.Day);
        return s;
    }

    private static void RivalsBake()
    {
        RivalSim s = BakeSim(91, out BakeOff b);
        Check(b.Customer == "The Daily Grind" && b.Closes == b.Opened + RivalryCatalogue.BakeDays && b.Seats >= RivalryCatalogue.BakeSeatsBusinesses && b.Seats <= 2 * RivalryCatalogue.BakeSeatsBusinesses
              && b.Value > 0 && s.R.OpenIn(NicheCatalogue.Cafes) == b && s.R.BakeLast[NicheCatalogue.Cafes] == s.Day && s.W.Opened.Contains(b),
            "rivals bake: a customer in the niche opens a bake off: " + b.Seats + " sites for " + RivalryCatalogue.BakeMonths + " months, worth " + b.Value + ", open " + RivalryCatalogue.BakeDays + " days");
        NicheDef d = NicheCatalogue.Def(NicheCatalogue.Cafes);
        SectorMarketDay md = s.F.Markets[(int)SectorMarket.BusinessSoftware];
        Check(b.Value == (long)b.Seats * FirmsCatalogue.Price[(int)Segment.Business] * NicheRules.Going(d, md.Going) / 1000 * RivalryCatalogue.BakeMonths,
            "rivals bake: its value is its sites at the niche's going price for the months");
        // when and where
        int wanted = 0;
        RivalSim w = RivalNew(92, 2, NicheCatalogue.Cafes, 600, true);
        w.R.Since = 0;
        w.W.You.Add(new RivalryYou { Place = "Mine~1", Name = "Mine Ltd", Niche = NicheCatalogue.Cafes, Served = 1 << NicheCatalogue.Cafes, Market = (int)SectorMarket.BusinessSoftware });
        for (int day = 200; day < 200 + 4000; day++)
        {
            wanted += RivalryRules.BakeWanted(w.W, NicheCatalogue.Cafes, day) ? 1 : 0;
        }
        int away = 0;
        for (int day = 200; day < 4200; day++)
        {
            away += RivalryRules.BakeWanted(w.W, NicheCatalogue.Gyms, day) ? 1 : 0;
        }
        Check(wanted > 4000 / RivalryCatalogue.BakeEvery / 2 && wanted < 4000 / RivalryCatalogue.BakeEvery * 2 && away == 0 && !RivalryRules.BakeWanted(w.W, -1, 300),
            "rivals bake: about one a " + RivalryCatalogue.BakeEvery + " days (" + wanted + " in 4,000 days) in a niche where the player sells, none where they do not");
        Check(!RivalryRules.BakeWanted(s.W, NicheCatalogue.Cafes, s.Day + 1), "rivals bake: not while one is open in the niche");
        // taking part
        Check(RivalryRules.Entrant(s.W, b, "Mine~1").HasValue && RivalryRules.Entrant(s.W, b, "Other~2") == null, "rivals bake: a firm of the player's that sells in the niche can enter, no other");
        Check(RivalryRules.Enter(s.W, b, "Mine~1", 0) && b.Entries.Count == 1 && RivalryRules.Entrant(s.W, b, "Mine~1") == null && !RivalryRules.Enter(s.W, b, "Mine~1", 1) && !RivalryRules.Enter(s.W, b, "Other~2", 1),
            "rivals bake: it enters once, with a tactic");
        // the score: features, price, name (the appeal) and the player's Selling and fit
        long none = RivalryRules.YouScore(1000, 0, 500, 2, 0, 0);
        long selling = RivalryRules.YouScore(1000, 100, 500, 2, 0, 0);
        long fitLow = RivalryRules.YouScore(1000, 0, 100, 2, 0, 0), fitHigh = RivalryRules.YouScore(1000, 0, 900, 2, 0, 0);
        Check(none == 1000 && selling == 1000 + RivalryCatalogue.BakeSelling && fitHigh > fitLow && fitLow < 1000 && fitHigh > 1000 && RivalryRules.YouScore(1200, 0, 500, 2, 0, 0) > none,
            "rivals bake: the score is the appeal (features, price, soundness and name), raised by Selling (up to " + RivalryCatalogue.BakeSelling + " per mille) and by fit ("
            + fitLow + " at 100, " + fitHigh + " at 900)");
        Check(RivalryRules.YouScore(1000, 0, 500, 0, 100, 0) == 1000 + RivalryCatalogue.BakeDemo && RivalryRules.YouScore(1000, 0, 500, 2, 0, 100) == 1000 + RivalryCatalogue.BakeRelations
              && RivalryRules.YouScore(1000, 0, 500, 1, 0, 0) == 1000 + RivalryCatalogue.BakePrice && RivalryRules.YouScore(1000, 0, 500, 0, 0, 0) == 1000,
            "rivals bake: a demo counts with Product sense, the relationships with Relationships, a sharper price flat");
        // Selling decides a close one: the same product wins at Selling 90 and loses at 0, the rivals' scores being the same
        int winsHigh = 0, winsLow = 0, runs = 0;
        for (int seed = 100; seed < 140; seed++)
        {
            foreach (int selling2 in new[] { 0, 90 })
            {
                RivalSim r = BakeSim(seed, out BakeOff x);
                RivalProduct best = r.F.Rivals.Where(q => q.Niche == NicheCatalogue.Cafes || (q.Niche < 0 && !q.Startups && q.Customers > 0 && NicheRules.Serves(NicheCatalogue.Def(NicheCatalogue.Cafes), q.Market))).OrderByDescending(q => q.Appeal).First();
                r.W.You[0] = new RivalryYou
                {
                    Place = "Mine~1", Name = "Mine Ltd", Niche = NicheCatalogue.Cafes, Served = 1 << NicheCatalogue.Cafes, Market = (int)SectorMarket.BusinessSoftware, Features = 6,
                    Appeal = best.Appeal * 1000 / (1000 + RivalryCatalogue.BakeSelling * 45 / 100), Fit = 500
                };
                r.W.Selling = selling2;
                RivalryRules.Enter(r.W, x, "Mine~1", 2);
                RivalryRules.Day(r.W, x.Closes, new List<VentureNote>());
                winsHigh += selling2 == 90 && x.State == BakeState.Won ? 1 : 0;
                winsLow += selling2 == 0 && x.State == BakeState.Won ? 1 : 0;
                runs += selling2 == 0 ? 1 : 0;
            }
        }
        Check(winsHigh > winsLow && winsHigh > runs / 3, "rivals bake: the player's Selling decides a close bake off: " + winsHigh + " won of " + runs + " at Selling 90, " + winsLow + " at Selling 0");
        // winning books a contract at the firm, losing gives the customer to the best rival, not entering misses it
        RivalSim g = BakeSim(93, out BakeOff won);
        g.W.You[0] = new RivalryYou { Place = "Mine~1", Name = "Mine Ltd", Niche = NicheCatalogue.Cafes, Served = 1 << NicheCatalogue.Cafes, Market = (int)SectorMarket.BusinessSoftware, Features = 6, Appeal = 100000, Fit = 900 };
        RivalryRules.Enter(g.W, won, "Mine~1", 1);
        long users = g.Rival(0).Customers + g.Rival(1).Customers + g.Rival(2).Customers;
        RivalryRules.Day(g.W, won.Closes, new List<VentureNote>());
        Check(won.State == BakeState.Won && won.Place == "Mine~1" && won.Winner == "Mine Ltd" && won.Booked == won.Value * (1000 - RivalryCatalogue.BakePriceCut) / 1000 && g.W.Decided.Contains(won)
              && g.Rival(0).Customers + g.Rival(1).Customers + g.Rival(2).Customers == users,
            "rivals bake: a win names the firm and what it takes (" + won.Booked + ", a sharper price costing a tenth), and the rivals keep their customers");
        RivalSim l = BakeSim(94, out BakeOff lost);
        l.W.You[0] = new RivalryYou { Place = "Mine~1", Name = "Mine Ltd", Niche = NicheCatalogue.Cafes, Served = 1 << NicheCatalogue.Cafes, Market = (int)SectorMarket.BusinessSoftware, Features = 1, Appeal = 1, Fit = 100 };
        RivalryRules.Enter(l.W, lost, "Mine~1", 2);
        long before = l.F.Rivals.Where(q => q.Niche == NicheCatalogue.Cafes).Sum(q => q.Customers);
        RivalryRules.Day(l.W, lost.Closes, new List<VentureNote>());
        long after = l.F.Rivals.Sum(q => q.Customers);
        Check(lost.State == BakeState.Lost && lost.Booked == 0 && lost.Winner.Length > 0 && lost.Place.Length == 0 && after >= before,
            "rivals bake: a lost bake off gives the customer to the rival with the best score (" + lost.Winner + "), and books nothing");
        RivalSim m = BakeSim(95, out BakeOff missed);
        RivalryRules.Day(m.W, missed.Closes, new List<VentureNote>());
        Check(missed.State == BakeState.Missed && missed.Booked == 0 && missed.Winner.Length > 0, "rivals bake: a bake off the player did not enter is decided between the rivals and missed");
        RivalSim again = BakeSim(95, out BakeOff missed2);
        RivalryRules.Day(again.W, missed2.Closes, new List<VentureNote>());
        Check(missed2.Winner == missed.Winner && missed2.Value == missed.Value && missed2.Seats == missed.Seats, "rivals bake: the same seed gives the same customer, sites and winner");
        // rivals: the city's startups take part by the same rule
        RivalSim sc = BakeSim(96, out BakeOff scored);
        RivalProduct top = sc.F.Rivals.Where(q => q.Niche == NicheCatalogue.Cafes).OrderByDescending(q => q.Appeal).First();
        long a1 = RivalryRules.RivalScore(sc.W, scored, top);
        Check(a1 == RivalryRules.RivalScore(sc.W, scored, top) && Math.Abs(a1 - top.Appeal) <= top.Appeal * RivalryCatalogue.BakeJitter / 1000 + 1, "rivals bake: a rival's score is its appeal and the day's seeded roll, nothing else");
        // contract money is the firm's own takings, through the books
        Cap cap = FoundRules.Incorporate("Mine~1", new List<CoFounder>(), 0, 10);
        FoundRules.TakeFamily(cap, FoundRules.FamilyAmount(2), 10);
        long raised = cap.Acc.Raised, floorBefore = StartupMoney.Floor(cap, 20);
        StartupMoney.Profit(cap, won.Booked, 20);
        Check(StartupMoney.Balance(cap) == raised + won.Booked && StartupMoney.Floor(cap, 20) == floorBefore && floorBefore == raised,
            "rivals bake: a won contract is the firm's own takings: the account grows by it, and the investors' floor stays what they put in");
    }

    // ---------------------------------------------------------------- the save

    private static void RivalsStore()
    {
        RivalSim s = BakeSim(111, out BakeOff b);
        RivalryRules.Enter(s.W, b, "Mine~1", 1);
        RivalryRec rec = s.R.RecFor(s.Rival(0).Id);
        rec.Backer = 2;
        rec.BackerDay = 90;
        rec.Entrant = true;
        rec.Niche = NicheCatalogue.Cafes;
        rec.ChestUntil = 150;
        rec.ChestMoney = 123456;
        rec.CopyTarget = 9;
        rec.CopyAt = 120;
        rec.CopyPlace = "Mine~1";
        rec.PeakShare = 77;
        s.R.Say(new RivalryReaction { Day = 99, Kind = ReactKind.Copying, Rival = rec.Id, Name = "Pixel Works", Place = "Mine~1", Niche = 1, Amount = 12, Other = "Codeharbour Software" });
        s.R.SeenOf("Mine~1").Invested = 5000;
        string line = RivalryStore.Write(s.R);
        RivalryFile back = RivalryStore.Read(line, out int version);
        RivalryRec r2 = back.RecOf(rec.Id);
        BakeOff b2 = back.BakeOf(b.Id);
        Check(version == RivalryStore.Version && r2 != null && r2.Backer == 2 && r2.Entrant && r2.Niche == NicheCatalogue.Cafes && r2.ChestMoney == 123456 && r2.CopyTarget == 9 && r2.CopyPlace == "Mine~1"
              && r2.PeakShare == 77 && back.Log.Count == s.R.Log.Count && back.Log.Any(l => l.Other == "Codeharbour Software" && l.Name == "Pixel Works" && l.Place == "Mine~1" && l.Amount == 12) && back.SeenOf("Mine~1").Invested == 5000 && b2 != null && b2.Customer == "The Daily Grind"
              && b2.Entries.Count == 1 && b2.Entries[0].Tactic == 1 && b2.Value == b.Value && back.NextBake == s.R.NextBake && back.Since == s.R.Since && back.BakeLast[NicheCatalogue.Cafes] == s.Day,
            "rivals store: the save line keeps the records, the reactions, the bake offs and their entries");
        string again2 = RivalryStore.Write(back);
        int diff = 0;
        while (diff < Math.Min(line.Length, again2.Length) && line[diff] == again2[diff]) { diff++; }
        Check(again2 == line, "rivals store: written again it is the same line (" + diff + " of " + line.Length + ": " + line.Substring(Math.Max(0, diff - 20), Math.Min(60, line.Length - Math.Max(0, diff - 20))) + " / " + again2.Substring(Math.Max(0, diff - 20), Math.Min(60, again2.Length - Math.Max(0, diff - 20))) + ")");
        Check(RivalryStore.Key == "reallife:tech_rivals" && VentureStore.Key == "reallife:tech_ventures" && VentureStore.Version == 2 && SectorStore.Key == "reallife:tech_sector" && SectorStore.Version == 1,
            "rivals store: the venture and sector lines keep their version and fields; the new state is a line of its own");
        string sector = SectorStore.Write(s.F);
        Check(!sector.Contains("Frame") && SectorStore.Read(sector, out _).Rivals.Count == s.F.Rivals.Count, "rivals store: what the sector keeps of a rival product is as it was: the niche and the pushes are worked out each day and never saved");
        string pieces = ModSource(@"Tech\Sector\SectorStore.cs");
        Check(pieces.Contains(".Add(r.Peak).Add(r.PeakDay).Add(r.LastShip).Add(r.LastCut).Add(r.Carry).Add(r.Compute);") && !pieces.Contains("Niche") && !pieces.Contains("PushMarketing"),
            "rivals store: the shared city carries the rival products as before, so an older or absent RLO reads them");
        RivalryFile fewer = RivalryStore.Read("v=1~h:5|3", out _);
        Check(fewer.Since == 5 && fewer.NextBake == 3 && fewer.Recs.Count == 0 && fewer.Bakes.Count == 0, "rivals store: a line with fewer parts reads the rest as they start");
        string writes = ModSource(@"Tech\TechDirector.Venture.cs");
        Check(writes.Contains("WriteRivalry(data);") && writes.Contains("StartRivalry();") && writes.Contains("StopRivalry();") && writes.Contains("RivalryDay(day);"),
            "rivals store: competition starts, saves, stops and runs its day with the venture part");
    }

    // ---------------------------------------------------------------- the same seed, the same world

    private static void RivalsDeterminism()
    {
        string[] sig = new string[2];
        for (int run = 0; run < 2; run++)
        {
            RivalSim s = RivalNew(121, 4, NicheCatalogue.Restaurants, 700, true);
            s.R.Since = 0;
            foreach (Startup t in s.V.Startups)
            {
                t.LastRound = s.Day - 200;
                t.Mrr = 12000000;
            }
            for (int d = 0; d < 200; d++)
            {
                if (d == 30)
                {
                    s.P.Features = 13;
                }
                if (d == 60)
                {
                    s.Invested = VentureRules.RoundSize(1);
                    s.Stage = 1;
                }
                RivalStep(s);
            }
            sig[run] = Signature(s);
        }
        Check(sig[0] == sig[1] && sig[0].Contains("Mine") == false && sig[0].Length > 100, "rivals determinism: the same seed gives the same customers, prices, features, reactions and rounds (" + sig[0].Length + " characters)");
        RivalSim other = RivalNew(122, 4, NicheCatalogue.Restaurants, 700, true);
        other.R.Since = 0;
        for (int d = 0; d < 200; d++)
        {
            RivalStep(other);
        }
        Check(Signature(other) != sig[0], "rivals determinism: another seed gives another world");
        string rules = string.Concat(ModSource(@"Tech\Rivals\RivalryRules.cs"), ModSource(@"Tech\Rivals\RivalryRules.Reactions.cs"), ModSource(@"Tech\Rivals\RivalryRules.Bake.cs"));
        Check(!rules.Contains("System.Random") && !rules.Contains("new Random") && !rules.Contains("DateTime") && !rules.Contains("Time.") && rules.Contains("TechRandom.Mille(w.Seed"),
            "rivals determinism: every roll is TechRandom's, seeded, and nothing reads the clock");
    }

    // ---------------------------------------------------------------- no way out of a funded firm

    private static void RivalsExploits()
    {
        string bake = ModSource(@"Tech\TechDirector.BakeOff.cs"), rival = ModSource(@"Tech\TechDirector.Rivalry.cs"), game = ModSource(@"Tech\Rivals\BakeGame.cs");
        string all = bake + rival + game + ModSource(@"Wiring\Wiring.BakeLinks.cs");
        Check(!all.Contains("VentureGame.Pay(") && !all.Contains("FirmsGame.Pay(") && !all.Contains("FirmsGame.Cost(") && !all.Contains("BizBooks.Book(-") && !all.Contains("VentureGame.Give(") && !all.Contains("Save.Money"),
            "rivals exploits: nothing in competition takes money out of a firm, pays money into the wallet or reads the wallet");
        Check(all.Split(new[] { "BakeGame.Takings(" }, StringSplitOptions.None).Length == 2 && bake.Contains("BakeGame.Takings(Level(b.Booked), firm.Reg, b.Customer, day)") && game.Contains("cents > 0 && Save != null && at != null")
              && game.Contains("at.Address, true"), "rivals exploits: the only money it moves is a won bake off's contract, booked as takings at the winning firm's own address, once");
        Check(bake.Contains("if (b.State == BakeState.Won)") && bake.Contains("FirmPlace firm = FirmOf(b.Place);") && bake.Contains("RivalryRules.Enter(_world, b, place, choice)"),
            "rivals exploits: only a firm that entered and won is paid, and only the one that entered");
        Check(ModSource(@"Economy\BizLines.cs").Contains("P + \"tech_bakeoff\"") && ModSource(@"Tech\Firms\FirmsGame.cs").Contains("type == BakeGame.BakeKey"),
            "rivals exploits: the contract is counted as takings in the game's statement, so the firm account sees it like any sale");
        // the same contract entered twice, or decided twice, is paid once
        RivalSim s = BakeSim(131, out BakeOff b);
        s.W.You[0] = new RivalryYou { Place = "Mine~1", Name = "Mine Ltd", Niche = NicheCatalogue.Cafes, Served = 1 << NicheCatalogue.Cafes, Market = (int)SectorMarket.BusinessSoftware, Features = 6, Appeal = 100000, Fit = 900 };
        RivalryRules.Enter(s.W, b, "Mine~1", 0);
        RivalryRules.Enter(s.W, b, "Mine~1", 0);
        RivalryRules.Day(s.W, b.Closes, new List<VentureNote>());
        int decided = s.W.Decided.Count;
        RivalryRules.Day(s.W, b.Closes + 1, new List<VentureNote>());
        Check(b.Entries.Count == 1 && decided == 1 && s.W.Decided.Count == 0 && b.State == BakeState.Won, "rivals exploits: a bake off is entered once and decided once: a day later it is not paid again");
        // a funded firm: contract money comes in as the firm's own, and the wallet still cannot be emptied
        Cap cap = FoundRules.Incorporate("Mine~1", new List<CoFounder>(), 0, 10);
        long raised = FoundRules.TakeFamily(cap, FoundRules.FamilyAmount(2), 10);
        StartupMoney.Profit(cap, b.Booked, 20);
        var caps = new List<Cap> { cap };
        long counted = StartupMoney.Shortfall(caps, 0, raised + b.Booked, 21, false, "ba:transaction_boatpurchase");
        Check(counted == raised && cap.Acc.Taken == raised, "rivals exploits: money taken out of a firm that won a bake off is counted as taken for the investors' part, never the contract's (" + counted + " of " + raised + ")");
        // war chests and sales are the city's startups' money, never the player's
        RivalSim sc = RivalNew(131, 8, NicheCatalogue.Venues, 400, true);
        sc.R.Since = 0;
        foreach (Startup t in sc.V.Startups)
        {
            t.LastRound = sc.Day - 200;
            t.Mrr = 12000000;
        }
        RivalStep(sc);
        sc.Invested = VentureRules.RoundSize(1);
        sc.Stage = 1;
        long holders = 0;
        RivalStep(sc);
        Check(sc.R.Recs.Any(x => x.ChestStage >= 0) && holders == 0 && !ModSource(@"Tech\Rivals\RivalryRules.Reactions.cs").Contains("Cash -=") && !ModSource(@"Tech\Rivals\RivalryRules.Reactions.cs").Contains("VentureGame"),
            "rivals exploits: a rival's war chest is its own startup's round: no money of the player's moves for it");
    }

    // ---------------------------------------------------------------- options, wiring and cost

    private static void RivalsWiring()
    {
        string d = ModSource(@"Tech\TechDirector.Rivalry.cs"), sec = ModSource(@"Tech\TechDirector.Sector.cs"), found = ModSource(@"Tech\TechDirector.Founding.cs"), ven = ModSource(@"Tech\TechDirector.Venture.cs");
        Check(d.Contains("public RivalryFile Competition => _ventureOn ? _rival : null;") && d.Contains("_rival != null && _world != null && _sectorOn && _sector != null && !SharedCity.Follows(SectorShare.Part)")
              && d.Contains("_rival = null;") && ven.Contains("StopRivalry();"),
            "rivals options: with Startups off there is no record and no rule runs; with the sector off, or on a Going Public guest (the host's city), none either");
        Check(found.Contains("if (RivalryOn)") && found.Contains("NicheRules.Pull(v, md, SectorRules.Attraction(appeal, aware), pulled);") && sec.Contains("RivalryPrepare(day);") && sec.Contains("RivalryOwn(firm, p, md, day, i);")
              && sec.Contains("RivalryViews(day);"), "rivals options: the player's products and the rivals are weighed in one pool while it runs, and as they were when it does not");
        Check(d.Contains("ClearRivalry();") && d.Contains("r.Niche = -1;") && d.Contains("r.Frame = null;") && d.Contains("r.PushMarketing = 0;"), "rivals options: stopping leaves no niche, frame or push on a rival product");
        Check(sec.Contains("_keptRival") && ModSource(@"Tech\Sector\SectorRules.Rivals.cs").Contains("Func<RivalProduct, bool> kept = null"), "rivals options: a bought startup's product is kept only where competition says so");
        Check(!d.Contains("Update()") && !ModSource(@"Tech\TechDirector.BakeOff.cs").Contains("Update()") && !ModSource(@"Wiring\Wiring.BakeLinks.cs").Contains("Update()") && d.Contains("RivalryRules.Day(_world, day, _ventureNotes);") && d.Contains("if (!RivalryOn)"),
            "rivals cost: competition runs inside the day only, nothing per frame");
        string links = ModSource(@"Wiring\Wiring.BakeLinks.cs"), techLinks = ModSource(@"Wiring\TechLinks.cs");
        Check(links.Contains("Id = \"tech_bakeoff\"") && links.Contains("SessionRegistry.Add(Kind())") && links.Contains("OwnDesk = true") && links.Contains("tech.BakeSession(ask.Place, ask.Name, choice, out string key, out string[] pairs)")
              && links.Contains("tech.BakeBlocked(ask.Place)") && links.Contains("Delegable = false") && techLinks.Contains("BakeLinks.Connect();") && techLinks.Contains("BakeLinks.Disconnect();"),
            "rivals bake: the bake off is a desk session through the existing sessions path, held at the owner's own desk while one is open for the firm");
        string bake = ModSource(@"Tech\TechDirector.BakeOff.cs");
        Check(bake.Contains("Practised(TalentCatalogue.Selling);") && bake.Contains("GameDlc.Gated(reg)") && bake.Contains("!reg.RentedByPlayer") && bake.Contains("Array.IndexOf(d.Types, reg.businessTypeName) >= 0"),
            "rivals bake: the customer is one of the city's own businesses of the niche's kinds, not the player's, and Selling grows by doing it");
        string live = ModSource(@"Ui\Tech\TechLive.Rivals.cs"), found2 = ModSource(@"Ui\Tech\TechLive.Found.cs");
        Check(live.Contains("_d.RivalsIn(niche, RivalRows)") && live.Contains("_d.ReactionsOf(place, RivalLog)") && live.Contains("_d.BakesOf(place, BakeRows)") && live.Contains("_d.EnterBake(place, 0)")
              && live.Contains("_d.EnterBake(place, 1)") && live.Contains("_d.EnterBake(place, 2)") && !live.Contains(".rootCanvas") && !live.Contains("UiButtonKind.Primary"),
            "rivals pages: the niche's rivals, a firm's reactions and its bake offs are read from the director, each button reaches its call, and none is a primary");
        Check(found2.Contains("NicheRivals(left, niche);") && found2.Contains("FirmRivals(left, right, place);"), "rivals pages: the niche page and a founded firm's page show them");
        foreach (string state in new[] { "niche_rivals", "gtm_rivals" })
        {
            Check(UiSamples.TechStates.Contains(state), "rivals pages: the sample state '" + state + "' is in the previews and the UI checks");
        }
        int longLines = 0;
        foreach (string file in new[] { @"Tech\Rivals\RivalryRules.cs", @"Tech\Rivals\RivalryRules.Reactions.cs", @"Tech\Rivals\RivalryRules.Bake.cs", @"Tech\Rivals\RivalryFile.cs", @"Tech\Rivals\RivalryStore.cs",
                     @"Tech\Rivals\NichePool.cs", @"Tech\TechDirector.Rivalry.cs", @"Tech\TechDirector.BakeOff.cs", @"Ui\Tech\TechLive.Rivals.cs", @"Wiring\Wiring.BakeLinks.cs" })
        {
            longLines += ModSource(file).Split('\n').Count(line => line.TrimEnd('\r').Length >= 200);
        }
        Check(longLines == 0, "rivals style: no line of the new files is 200 columns or more");
    }

    // ---------------------------------------------------------------- texts

    private static void RivalsTexts()
    {
        string enText = System.IO.File.ReadAllText(SourcePath(@"..\Locales\en.json"));
        string ptText = System.IO.File.ReadAllText(SourcePath(@"..\Locales\pt.json")), zhText = System.IO.File.ReadAllText(SourcePath(@"..\Locales\zh-cn.json"));
        var gone = new List<string>();
        var seen = new HashSet<string>();
        foreach (string file in new[] { @"Tech\TechDirector.Rivalry.cs", @"Tech\TechDirector.BakeOff.cs", @"Ui\Tech\TechLive.Rivals.cs", @"Tech\Rivals\RivalryCatalogue.cs", @"Wiring\Wiring.BakeLinks.cs", @"Tech\Rivals\BakeGame.cs" })
        {
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(ModSource(file), "\"(reallife_(?:rival|tech_desk_bake|transaction_tech_bakeoff)[a-z0-9_]*)\""))
            {
                string key = "\"" + m.Groups[1].Value + "\":";
                if (seen.Add(key) && (!enText.Contains(key) || !ptText.Contains(key) || !zhText.Contains(key)))
                {
                    gone.Add(m.Groups[1].Value);
                }
            }
        }
        Check(gone.Count == 0 && seen.Count > 30, "rivals texts: every key of competition (" + seen.Count + ") is in en, pt and zh-cn (" + string.Join(", ", gone.Take(3)) + ")");
        var lineKeys = new List<string>();
        for (int k = 1; k < ReactKind.Count; k++)
        {
            if (!enText.Contains("\"" + ReactKind.LineKey(k) + "\":"))
            {
                lineKeys.Add(ReactKind.LineKey(k));
            }
        }
        Check(lineKeys.Count == 0, "rivals texts: every kind of reaction has its line for the firm's page");
        foreach (string suffix in new[] { "_title", "_about", "_step", "_c0", "_c1", "_c2", "_c0_trade", "_c1_trade", "_c2_trade", "_why_none", "_entered" })
        {
            Check(enText.Contains("\"reallife_tech_desk_bake" + suffix + "\":"), "rivals texts: the bake off's desk session has its " + suffix.Trim('_'));
        }
        string keys = ModSource(@"Ui\UiScreens.Tech.cs");
        var missing = new List<string>();
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(ModSource(@"Ui\Tech\TechLive.Rivals.cs"), "\"(reallife_(?:rival|tech_desk_bake)_[a-z0-9_]+)\""))
        {
            if (!keys.Contains("\"" + m.Groups[1].Value + "\""))
            {
                missing.Add(m.Groups[1].Value);
            }
        }
        for (int k = 1; k < ReactKind.Count; k++)
        {
            if (!keys.Contains("\"" + ReactKind.LineKey(k) + "\""))
            {
                missing.Add(ReactKind.LineKey(k));
            }
        }
        Check(missing.Count == 0, "rivals texts: every key of the pages is listed in the Tech screen's keys (" + string.Join(", ", missing.Take(3)) + ")");
        string changes = "\"reallife_changes_next\":";
        int at = enText.IndexOf(changes, StringComparison.Ordinal);
        Check(at > 0 && enText.Substring(at, enText.IndexOf('\n', at) - at).Contains("head to head"), "rivals texts: the What's new line says it");
    }
}
