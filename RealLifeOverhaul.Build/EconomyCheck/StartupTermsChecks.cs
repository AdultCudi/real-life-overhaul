using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;

// Term sheets with teeth (flag --startup-terms; Scripts/Tech/Venture/VentureTerms.cs, TermsStore.cs): the waterfall worked by hand for each term, anti-dilution, what the
// terms do to the board (information rights, the provision and its breach), the Law page's numbers against the waterfall, the skills, old saves, the save line and the
// exploit kept closed with hard terms. Game-free; the sources are read for what only the game can do.
internal static partial class Program
{
    private static void StartupTermsChecks()
    {
        TermsWaterfall();
        TermsAnti();
        TermsSetting();
        TermsBoard();
        TermsLaw();
        TermsSkills();
        TermsOldSaves();
        TermsSaveLine();
        TermsExploit();
        TermsWiring();
    }

    private static Holder TLine(HolderKind kind, long shares, long invested, int round, Terms t = null, long? pref = null)
    {
        return new Holder { Kind = (int)kind, Shares = shares, Invested = invested, Round = round, T = t, Preference = kind == HolderKind.Vc ? pref ?? invested * (t != null ? t.Pref : 1000) / 1000 : 0 };
    }

    private static long Sum(long[] pay)
    {
        long s = 0;
        foreach (long p in pay)
        {
            s += p;
        }
        return s;
    }

    private static void TermsWaterfall()
    {
        // 800 founder shares, an investor with 200 shares who put in 1,000,000
        var plain = new List<Holder> { TLine(HolderKind.Player, 800, 0, 0), TLine(HolderKind.Vc, 200, 1000000, 1) };
        long[] a = VentureRules.Waterfall(plain, 3000000), b = VentureRules.Waterfall(plain, 10000000), c = VentureRules.Waterfall(plain, 800000);
        Check(a[1] == 1000000 && a[0] == 2000000, "terms: 1x not participating, a sale for 3,000,000 pays the investor its 1,000,000 first and the founder 2,000,000 (" + a[1] + ", " + a[0] + ")");
        Check(b[1] == 2000000 && b[0] == 8000000, "terms: 1x not participating, a sale for 10,000,000 converts: 20% to the investor (" + b[1] + ")");
        Check(c[1] == 800000 && c[0] == 0, "terms: 1x, a sale under the preference pays the investor all of it");
        var hard = new List<Holder> { TLine(HolderKind.Player, 800, 0, 0), TLine(HolderKind.Vc, 200, 1000000, 1, new Terms { Pref = 2000, Participating = true }) };
        long[] d = VentureRules.Waterfall(hard, 10000000), e = VentureRules.Waterfall(hard, 1500000);
        Check(d[1] == 3600000 && d[0] == 6400000, "terms: 2x participating, a sale for 10,000,000 pays 2,000,000 first then 20% of the other 8,000,000: 3,600,000 (" + d[1] + ")");
        Check(e[1] == 1500000 && e[0] == 0, "terms: 2x participating, a sale for 1,500,000 is all the investor's");
        var twoX = new List<Holder> { TLine(HolderKind.Player, 800, 0, 0), TLine(HolderKind.Vc, 200, 1000000, 1, new Terms { Pref = 2000 }) };
        long[] f = VentureRules.Waterfall(twoX, 8000000), g = VentureRules.Waterfall(twoX, 10000000);
        Check(f[1] == 2000000 && f[0] == 6000000 && g[1] == 2000000, "terms: 2x not participating takes 2,000,000 when common would pay 1,600,000, and converts at 10,000,000 (same 2,000,000)");
        var three = new List<Holder> { TLine(HolderKind.Player, 750, 0, 0), TLine(HolderKind.Vc, 100, 500000, 1), TLine(HolderKind.Vc, 150, 3000000, 2, new Terms { Pref = 1500, Participating = true }) };
        long[] h = VentureRules.Waterfall(three, 5000000), i = VentureRules.Waterfall(three, 20000000);
        Check(h[2] == 4500000 && h[1] == 500000 && h[0] == 0, "terms: two rounds, the later round's 1.5x participating line is paid first, then the earlier 1x (" + h[2] + ", " + h[1] + ", " + h[0] + ")");
        Check(i[0] == 11625000 && i[1] == 1550000 && i[2] == 6825000 && Sum(i) == 20000000,
            "terms: two rounds in a big sale, the earlier line converts, the later takes 4,500,000 and 15% of the other 15,500,000: 6,825,000 (" + i[2] + ")");
        bool never = true;
        foreach (long price in new long[] { 1, 700000, 2500000, 4500000, 4999999, 7000000, 12000000, 99999999 })
        {
            never &= Sum(VentureRules.Waterfall(three, price)) == price && Sum(VentureRules.Waterfall(hard, price)) == price && Sum(VentureRules.Waterfall(twoX, price)) == price;
        }
        Check(never, "terms: the waterfall never pays out more or less than the sale, for any term");
        Check(VentureRules.Waterfall(plain, 0).Length == 2 && Sum(VentureRules.Waterfall(plain, 0)) == 0, "terms: Waterfall keeps its signature and pays nothing for nothing");
    }

    private static void TermsAnti()
    {
        long shares(Terms t, long pre, long money)
        {
            var hs = new List<Holder> { TLine(HolderKind.Player, 800, 0, 0), TLine(HolderKind.Vc, 200, 1000000, 1, t) };
            VentureTerms.AntiDilute(hs, 1000, pre, money);
            return hs[1].Shares;
        }
        Check(shares(new Terms(), 2000000, 500000) == 200 && shares(new Terms { Anti = 0 }, 2000000, 500000) == 200, "terms: a down round with no anti-dilution leaves the investor's 200 shares");
        Check(shares(new Terms { Anti = TermsCatalogue.AntiRatchet }, 2000000, 500000) == 500, "terms: a down round from 5,000 to 2,000 a share, full ratchet: the investor is made whole at the new price, 500 shares");
        Check(shares(new Terms { Anti = TermsCatalogue.AntiBroad }, 2000000, 500000) == 227, "terms: the same down round, broad weighted average: 5,000 x 1,100 / 1,250 = 4,400 a share, 227 shares");
        Check(shares(new Terms { Anti = TermsCatalogue.AntiRatchet }, 6000000, 500000) == 200 && shares(new Terms { Anti = TermsCatalogue.AntiBroad }, 6000000, 500000) == 200,
            "terms: a round priced higher, or at the same price, moves nobody");
        Cap c = VentureRules.NewCap("Main~1");
        var first = VentureRules.Sheet(6, 400000000, 100000000, 700, 1000, 10);
        first.T = new Terms { Anti = TermsCatalogue.AntiRatchet };
        VentureRules.Accept(c, first, 500000, 10);
        long was = c.Holders[1].Shares;
        var second = VentureRules.Sheet(6, 100000000, 100000000, 700, 1000, 40);
        VentureRules.Accept(c, second, 500000, 40);
        Check(second.Pre < first.Pre && c.Holders[1].Shares > was, "terms: a firm of the player's taking a down round gives the earlier line with a ratchet more shares (" + was + " to " + c.Holders[1].Shares + ")");
        // a city startup's down round
        var f = new VentureFile { Seed = 11 };
        Startup s = VentureRules.Born(f, 1, 1000);
        s.Stage = (int)RoundStage.SeriesA;
        s.Post = 3000000000;
        VentureRules.Round(s, 3000000000, new[] { new Holder { Kind = (int)HolderKind.Vc, Who = 6, Invested = 100000000, T = new Terms { Anti = TermsCatalogue.AntiRatchet } } }, 5);
        Holder early = s.Holders[s.Holders.Count - 1];
        long before = early.Shares;
        VentureRules.Round(s, 500000000, new[] { new Holder { Kind = (int)HolderKind.Vc, Who = 0, Invested = 50000000 } }, 20);
        Check(early.Shares > before, "terms: the city's startups give anti-dilution shares in a down round too (" + before + " to " + early.Shares + ")");
    }

    private static void TermsSetting()
    {
        // stricter firms ask harder terms: Ironbark Growth (850) against Old Mill Angels (250), over many days and the four moods
        int hardIron = 0, hardAngel = 0, partIron = 0, partAngel = 0, ratchetIron = 0, ratchetAngel = 0, n = 0;
        for (int day = 1; day <= 300; day++)
        {
            foreach (int mood in VentureCatalogue.MoodByCycle)
            {
                Terms ir = VentureTerms.Draw(6, 4, mood, 0, 7, 3, day), an = VentureTerms.Draw(0, 4, mood, 0, 7, 3, day);
                hardIron += ir.Pref;
                hardAngel += an.Pref;
                partIron += ir.Participating ? 1 : 0;
                partAngel += an.Participating ? 1 : 0;
                ratchetIron += ir.Anti == TermsCatalogue.AntiRatchet ? 1 : 0;
                ratchetAngel += an.Anti == TermsCatalogue.AntiRatchet ? 1 : 0;
                n++;
            }
        }
        Check(hardIron > hardAngel && partIron > partAngel && ratchetIron > ratchetAngel, "terms: a stricter firm asks a higher preference, participation and ratchets more often (" + hardIron / n + " against " + hardAngel / n + ")");
        int calm = VentureTerms.Strictness(600, 2, VentureCatalogue.MoodByCycle[0], 0, 500), bust = VentureTerms.Strictness(600, 2, VentureCatalogue.MoodByCycle[3], 0, 500);
        Check(bust > calm, "terms: the same firm asks harder in a poor market (" + bust + " against " + calm + ")");
        Check(VentureTerms.Strictness(600, 2, 1000, 900, 500) > VentureTerms.Strictness(600, 2, 1000, 0, 500) && VentureTerms.Strictness(600, 5, 1000, 0, 500) > VentureTerms.Strictness(600, 0, 1000, 0, 500),
            "terms: a firm doing badly and a later stage are asked harder");
        Terms x = VentureTerms.Draw(5, 3, 780, 300, 9, 4, 50), y = VentureTerms.Draw(5, 3, 780, 300, 9, 4, 50);
        Check(x.Pref == y.Pref && x.Participating == y.Participating && x.Anti == y.Anti && x.Vest == y.Vest && x.Info == y.Info && x.Provision == y.Provision, "terms: the same firm, day and book give the same terms (seeded)");
        Terms angel = null;
        for (int day = 1; day < 200 && angel == null; day++)
        {
            Terms t = VentureTerms.Draw(0, 0, 1250, 0, 7, 3, day);
            angel = t.Pref == 1000 && !t.Participating && t.Info == TermsCatalogue.InfoQuarterly && t.Provision == 0 ? t : null;
        }
        Check(angel != null, "terms: an easy angel in a good market can ask plain terms with a quarterly pack and no provision");
        Check(VentureTerms.Stamp(0, 0, 1250, 0, 7, 3, 1) == null || !VentureTerms.Stamp(0, 0, 1250, 0, 7, 3, 1).Standard, "terms: a plain round is stamped null, so it looks like an old one");
        // a funded round takes the terms
        Cap c = VentureRules.NewCap("Main~1");
        var sheet = VentureRules.Sheet(6, 400000000, 100000000, 700, 1000, 10);
        sheet.T = new Terms { Pref = 1500, Participating = true, Vest = 1, Info = TermsCatalogue.InfoDemand, Provision = 5000000 };
        VentureRules.Accept(c, sheet, 500000, 10);
        Holder lead = c.Holders[1];
        Check(lead.Preference == 150000000 && lead.Participating && lead.T == sheet.T && c.Acc.FounderDay == 10, "terms: a taken sheet gives the investor its 1.5x preference and its terms (" + lead.Preference + ")");
        c.Acc.FounderDay = 5;
        VentureRules.Accept(c, sheet, 500000, 60);
        Check(c.Acc.FounderDay == 60, "terms: a round whose vesting term restarts it moves the founder's day to the round");
        Cap keep = VentureRules.NewCap("Keep~1");
        var plainSheet = VentureRules.Sheet(6, 400000000, 100000000, 700, 1000, 10);
        VentureRules.Accept(keep, plainSheet, 500000, 10);
        keep.Acc.FounderDay = 3;
        VentureRules.Accept(keep, VentureRules.Sheet(6, 400000000, 100000000, 700, 1000, 60), 500000, 60);
        Check(keep.Acc.FounderDay == 3 && keep.Holders[1].Preference == 100000000, "terms: a sheet that keeps the vesting leaves the founder's day, and a plain one is 1x");
        // a city startup's round carries its lead's terms, and a sale pays by them
        var f = new VentureFile { Seed = 4 };
        Startup s = VentureRules.Born(f, 1, 1000);
        s.Stage = (int)RoundStage.Seed;
        VentureRules.Round(s, 1500000000, new[] { new Holder { Kind = (int)HolderKind.Vc, Who = 6, Invested = 500000000 } }, 5, 4, VentureCatalogue.MoodByCycle[3]);
        Holder sl = s.Holders[s.Holders.Count - 1];
        Check(sl.T != null && sl.Preference == sl.Invested * sl.T.Pref / 1000, "terms: a city startup's strict lead asks terms too, and its preference follows them (" + (sl.T != null ? sl.T.Pref : 0) + ")");
        sl.T = new Terms { Pref = 2000, Participating = true };
        sl.Preference = sl.Invested * 2;
        var sold = new List<VentureNote>();
        long price = 2000000000;
        long[] pay = VentureRules.Sell(f, s, price, VentureRules.NoteAcquired, sold);
        int idx = s.Holders.IndexOf(sl);
        long common = (price - sl.Preference) * sl.Shares / s.Total;
        Check(pay[idx] >= sl.Preference && pay[idx] <= sl.Preference + common + 2 && Sum(pay) == price, "terms: a city startup's sale pays its lead the 2x first and its share of the rest (" + pay[idx] + ")");
    }

    private static void TermsBoard()
    {
        Cap c = FundedCap(6);
        long burn = VentureCatalogue.BurnByStage[c.Stage] * 30;
        c.Holders[1].T = new Terms { Info = TermsCatalogue.InfoDemand, Provision = burn };
        Cap monthly = FundedCap(6), quarterly = FundedCap(6);
        quarterly.Holders[1].T = new Terms { Info = TermsCatalogue.InfoQuarterly };
        int tm = StartupMoney.ThresholdOf(monthly), td = StartupMoney.ThresholdOf(c), tq = StartupMoney.ThresholdOf(quarterly);
        Check(td < tm && tm < tq && td >= 31 && tq <= 58 && tm == StartupMoney.Threshold(StartupMoney.Diligence(monthly)),
            "terms: the right to look on demand makes the lead look at lower suspicion (" + td + "), a quarterly pack at higher (" + tq + "), a plain line as before (" + tm + ")");
        Check(StartupMoney.LookFindsOf(c) > StartupMoney.LookFindsOf(monthly) && StartupMoney.LookFindsOf(monthly) > StartupMoney.LookFindsOf(quarterly) && StartupMoney.LookFindsOf(c) <= 1000,
            "terms: stricter rights find more in a look (" + StartupMoney.LookFindsOf(c) + " against " + StartupMoney.LookFindsOf(monthly) + ")");
        StartupMoney.Misuse(c, Raise / 4 + Raise / 20, 50, false, "a");
        StartupMoney.Misuse(monthly, Raise / 4 + Raise / 20, 50, false, "a");
        int sc = StartupMoney.Suspicion(c, 50, 0, 0), sm = StartupMoney.Suspicion(monthly, 50, 0, 0);
        Check(sc == sm && StartupMoney.Next(c, 50, sc, StartupMoney.ThresholdOf(c), false || VentureTerms.OnDemand(c)) == StartupMoney.ActAsk
              && StartupMoney.Next(monthly, 50, sm, StartupMoney.ThresholdOf(monthly), false || VentureTerms.OnDemand(monthly)) == StartupMoney.ActNone
              && StartupMoney.Next(monthly, 50, sm, StartupMoney.ThresholdOf(monthly), true) == StartupMoney.ActAsk,
            "terms: with a look on demand the lead asks on any day, a plain lead only on a pack day (" + sc + ")");
        StartupMoney.Sent(c, 50);
        StartupMoney.Sent(quarterly, 50);
        StartupMoney.Sent(monthly, 50);
        Check(c.Acc.PackDay == 80 && monthly.Acc.PackDay == 80 && quarterly.Acc.PackDay == 140, "terms: the next pack is a month on, three months on with a quarterly right (" + quarterly.Acc.PackDay + ")");
        // the provision: a big spend asks the board first; the board can refuse; doing it anyway is a breach that adds suspicion
        Cap p = FundedCap(6);
        p.Holders[1].T = new Terms { Provision = burn };
        Check(VentureTerms.ProvisionOf(p) == burn && VentureTerms.Spend(p, burn, 20) && !VentureTerms.Spend(p, burn + 1, 20), "terms: spending up to the provision needs no board, one cent over does");
        p.Rt.Approved = burn * 4;
        p.Rt.ApprovedUntil = 30;
        Check(VentureTerms.Spend(p, burn * 2, 25) && p.Rt.Approved == burn * 2 && !VentureTerms.Spend(p, burn * 3, 25) && !VentureTerms.Spend(p, burn * 2, 31),
            "terms: the board's yes covers big spends until it runs out or its days end");
        int quiet = StartupMoney.Suspicion(p, 40, 0, 0);
        long taken = p.Acc.Taken, raised = p.Acc.Raised, own = p.Acc.Own;
        p.Rt.RefusedUntil = 60;
        VentureTerms.Breach(p, 41);
        int after = StartupMoney.Suspicion(p, 41, 0, 0);
        Check(after > quiet && p.Rt.Breaches == 2 && p.Acc.Taken == taken && p.Acc.Raised == raised && p.Acc.Own == own,
            "terms: a breach after a refusal counts twice and adds suspicion (" + quiet + " to " + after + ") without moving a cent of the account");
        Check(StartupMoney.Suspicion(p, 41 + TermsCatalogue.BreachDays, 0, 0) < after, "terms: a breach fades from the board's mind with time");
        Check(VentureTerms.VoteChance(p, 100, false) > VentureTerms.VoteChance(p, 0, false) && VentureTerms.VoteChance(p, 50, true) < VentureTerms.VoteChance(p, 50, false)
              && VentureTerms.VoteChance(FundedCap(0), 50, false) > VentureTerms.VoteChance(FundedCap(6), 50, false),
            "terms: Leadership carries the board's vote, a salary over the band is harder than a spend, a strict lead harder than an easy one");
        StartupMoney.Misuse(p, 1000, 45, false, "a");
        Check(VentureTerms.VoteChance(p, 100, false) == 0 && VentureTerms.VoteChance(p, 100, true) == 0, "terms: the board votes no to anything while money is counted as taken");
        // the salary over the band: asked of the board, drawn through the same account
        Cap sal = FundedCap(6);
        long band = sal.Acc.Band;
        sal.Acc.Band = band * TermsCatalogue.RaisePercent / 100;
        long drawn = StartupMoney.Draw(sal, band * 3, 11);
        Check(drawn == band * 3 / 2 && sal.Acc.Out == drawn && StartupMoney.Floor(sal, 11) == Raise - drawn && StartupMoney.Draw(sal, 1, 12) == 0,
            "terms: a raised band is drawn through the account: it lowers the floor by exactly what was drawn (" + drawn + ")");
    }

    private static void TermsLaw()
    {
        Cap c = FundedCap(6);
        long amount = 100000000, pre = 400000000;
        var offer = new Terms { Pref = 2000, Participating = true, Anti = TermsCatalogue.AntiRatchet, Vest = 1, Info = TermsCatalogue.InfoDemand, Provision = 3000000 };
        TermsCosts k = VentureTerms.Cost(c.Holders, offer, 3, amount, pre, 1, 80000000);
        // worked by hand with the waterfall on the cap table and the new investor
        long total = c.Total;
        Holder Inv(Terms t) => new Holder { Kind = (int)HolderKind.Vc, Who = 3, Invested = amount, Round = 1, Shares = (long)((double)amount * total / pre), Preference = amount * t.Pref / 1000, T = t };
        long Mine(Terms t, long price)
        {
            var hs = new List<Holder>();
            foreach (Holder h in c.Holders)
            {
                hs.Add(new Holder { Kind = h.Kind, Who = h.Who, Shares = h.Shares, Invested = h.Invested, Preference = h.Preference, Round = h.Round, T = h.T });
            }
            hs.Add(Inv(t));
            long[] pay = VentureRules.Waterfall(hs, price);
            long m = 0;
            for (int i = 0; i < hs.Count; i++)
            {
                m += hs[i].Players ? pay[i] : 0;
            }
            return m;
        }
        long v = pre + amount, v3 = v * 3;
        long plain1 = Mine(new Terms(), v), plain3 = Mine(new Terms(), v3), mult1 = Mine(new Terms { Pref = 2000 }, v), mult3 = Mine(new Terms { Pref = 2000 }, v3);
        long both1 = Mine(new Terms { Pref = 2000, Participating = true }, v), both3 = Mine(new Terms { Pref = 2000, Participating = true }, v3);
        Check(k.Value == v && k.Value3 == v3 && k.Plain == plain1 && k.Plain3 == plain3, "terms: the Law page works at the round's value and at three times it (" + v + ", " + v3 + ")");
        Check(k.Pref == plain1 - mult1 && k.Pref3 == plain3 - mult3 && k.Part == mult1 - both1 && k.Part3 == mult3 - both3 && k.Total == plain1 - both1 && k.Total3 == plain3 - both3,
            "terms: the cost of the multiple and of participating match the waterfall worked by hand (" + k.Pref + " and " + k.Part + " at the round, " + k.Pref3 + " and " + k.Part3 + " at three times)");
        Check(k.Pref > 0 && k.Part > 0 && k.Pref3 >= 0 && k.ShareDownAnti < k.ShareDown, "terms: a 2x participating ratchet costs the player money in a sale and share in a later down round (" + k.ShareDown + " to " + k.ShareDownAnti + ")");
        Check(k.HardPref && k.HardPart && k.HardAnti && k.HardVest && k.HardInfo && k.HardProv, "terms: every hard term is flagged");
        TermsCosts easy = VentureTerms.Cost(c.Holders, new Terms(), 3, amount, pre, 1, 80000000);
        Check(easy.Total == 0 && easy.Total3 == 0 && !easy.HardPref && !easy.HardPart && !easy.HardAnti && !easy.HardVest && !easy.HardInfo && !easy.HardProv, "terms: plain terms cost nothing and flag nothing");
        string view = ModSource(@"Tech\Venture\TermsView.cs"), director = ModSource(@"Tech\TechDirector.Terms.cs");
        Check(director.Contains("VentureTerms.Cost(c.Holders, t.T") && view.Contains("k.Pref") && view.Contains("k.Part3") && view.Contains("TermsCatalogue.LawToRead")
              && TermsCatalogue.LawToRead == 40, "terms: the page's numbers are VentureTerms.Cost's, and they are shown from a Law of 40; under it the terms are listed but not what they cost");
        Check(ModSource(@"Ui\Tech\TechLive.Terms.cs").Contains("bool reads = k != null && TermsView.Reads") && ModSource(@"Tech\Venture\TechApp.Terms.cs").Contains("bool reads = k != null && TermsView.Reads")
              && ModSource(@"Ui\Tech\TechLive.Terms.cs").Contains("reallife_vc_terms_law_hint"), "terms: both the kit's page and the older page hold the costs back under the Law needed");
    }

    private static void TermsSkills()
    {
        Check(VentureTerms.PushChance(100, 0, 0, 500, 600, 1000) > VentureTerms.PushChance(0, 0, 0, 500, 600, 1000) && VentureTerms.PushChance(0, 100, 0, 500, 600, 1000) > VentureTerms.PushChance(0, 0, 0, 500, 600, 1000)
              && VentureTerms.PushChance(0, 0, 100, 500, 600, 1000) > VentureTerms.PushChance(0, 0, 0, 500, 600, 1000), "terms: Law, Selling and Relationships each raise the chance a firm gives way");
        Check(VentureTerms.PushChance(50, 50, 50, 900, 600, 1000) > VentureTerms.PushChance(50, 50, 50, 300, 600, 1000) && VentureTerms.PushChance(50, 50, 50, 500, 200, 1000) > VentureTerms.PushChance(50, 50, 50, 500, 850, 1000)
              && VentureTerms.PushChance(50, 50, 50, 500, 600, 1250) > VentureTerms.PushChance(50, 50, 50, 500, 600, 560), "terms: a warm meeting, an easy firm and a good market help the push");
        var t = new Terms { Pref = 2000, Participating = true, Anti = 2, Vest = 1, Info = 2, Provision = 100 };
        for (int term = 0; term < TermsCatalogue.Count; term++)
        {
            Check(VentureTerms.Pushable(t, term), "terms: term " + term + " can be pushed on while it is hard");
        }
        VentureTerms.Relax(t, TermsCatalogue.TPref);
        VentureTerms.Relax(t, TermsCatalogue.TPart);
        VentureTerms.Relax(t, TermsCatalogue.TAnti);
        VentureTerms.Relax(t, TermsCatalogue.TVest);
        VentureTerms.Relax(t, TermsCatalogue.TInfo);
        VentureTerms.Relax(t, TermsCatalogue.TProv);
        Check(t.Pref == 1500 && !t.Participating && t.Anti == 1 && t.Vest == 0 && t.Info == 0 && t.Provision == 200 && !VentureTerms.Pushable(t, TermsCatalogue.TVest), "terms: a won push gives a term one step");
        Check(VentureTerms.Warmth(100, 100) == 60 && VentureTerms.Warmth(0, 0) == 0, "terms: Selling and Relationships warm a firm by up to 60 points of the meeting's score");
        Check(VentureTerms.SlipChance(0) > VentureTerms.SlipChance(60) && VentureTerms.SlipChance(100) >= 20 && VentureTerms.SlipChance(100) < VentureTerms.SlipChance(10), "terms: good Accounting cuts the honest slips of a board pack");
        Check(!VentureTerms.Stretched(0, 0, 0, 1500) && VentureTerms.Stretched(100, 0, 0, 600) && VentureTerms.Stretched(0, 100, 100, 600) && !VentureTerms.Stretched(100, 0, 0, 300),
            "terms: Accounting, or Analysis and Statistics, spot a stretched deck, the more the better the skill");
        string d = ModSource(@"Tech\TechDirector.Terms.cs"), meet = ModSource(@"Tech\Venture\VentureMeeting.cs");
        Check(d.Contains("TechBridges.LearnNow(skill,") && d.Contains("Grow(TalentCatalogue.Law, TermsCatalogue.GrowPush)") && d.Contains("Grow(TalentCatalogue.Selling, TermsCatalogue.GrowPitch)")
              && d.Contains("Grow(TalentCatalogue.Leadership, TermsCatalogue.GrowBoard)") && d.Contains("Grow(TalentCatalogue.Accounting, TermsCatalogue.GrowPack)") && meet.Contains("_director.PitchDone()"),
            "terms: skills grow by pitching, pushing a term, voting at the board and sending a pack");
        Check(ModSource(@"Wiring\TechLinks.cs").Contains("Careers.StudyRules.Learn(f, skill, thousandths") && ModSource(@"Tech\TechBridges.cs").Contains("Learn = null"),
            "terms: they grow through Careers' own StudyRules.Learn, behind a bridge that is let go with the rest");
        Check(meet.Contains("VentureTerms.Warmth(") && meet.Contains("PushBase + term") && d.Contains("t.Pushed = true") && d.Contains("t.Pushed ||") && d.Contains("TechRandom.Mille(_venture.Seed, StartupMoney.Hash(place), day, 96"),
            "terms: the pitch pushes on one term per offer with a seeded roll, from the meeting's summary page");
        Check(d.Contains("t.Lapse = Math.Min(t.Lapse, day + TermsCatalogue.SoftenDays)") && d.Contains("t.Pre = Math.Max(") && d.Contains("result = 3"), "terms: a failed push shortens the offer and a bad one sours it");
        Check(d.Contains("DeckStretched") && d.Contains("GoingPrice") && TermsCatalogue.AnalysisToSee == 35, "terms: Analysis and Statistics see an offer against the market, and a stretched deck is spotted");
    }

    private static void TermsOldSaves()
    {
        VentureFile f = SavedMoneyFile();
        string line = VentureStore.Write(f);
        VentureFile back = VentureStore.Read(line, out _);
        TermsStore.Apply(new Dictionary<string, string>(), back);
        Cap c = back.CapOf("Main~1");
        bool plain = true;
        foreach (Holder h in c.Holders)
        {
            plain &= h.T == null && !h.Participating && (h.Kind != (int)HolderKind.Vc || h.Preference == h.Invested);
        }
        Check(plain, "terms: a save with no terms line has every line at 1x, not participating");
        Check(VentureTerms.InfoOf(c) == TermsCatalogue.InfoMonthly && VentureTerms.ProvisionOf(c) == 0 && VentureTerms.PackEvery(c) == MoneyCatalogue.PackDays && VentureTerms.Extra(c, 100) == 0
              && !VentureTerms.OnDemand(c) && StartupMoney.ThresholdOf(c) == StartupMoney.Threshold(StartupMoney.Diligence(c)) && StartupMoney.LookFindsOf(c) == StartupMoney.LookFinds(StartupMoney.Diligence(c)),
            "terms: an old cap has a monthly pack, no provision, the lead's own threshold and no breach");
        var old = new List<Holder> { TLine(HolderKind.Player, 800, 0, 0), new Holder { Kind = (int)HolderKind.Vc, Shares = 200, Invested = 1000000, Preference = 1000000, Round = 1 } };
        long[] a = VentureRules.Waterfall(old, 3000000), b = VentureRules.Waterfall(old, 10000000);
        Check(a[1] == 1000000 && a[0] == 2000000 && b[1] == 2000000 && b[0] == 8000000, "terms: an old cap's lines pay in a sale exactly as before");
        long vest = VentureRules.Vested(10, 400, 360);
        Check(c.Acc.FounderDay == f.CapOf("Main~1").Acc.FounderDay && StartupMoney.BadLeaver(c, 400, 360) >= 0 && vest == VentureRules.Vested(10, 400, 360), "terms: an old cap's founder vesting is untouched");
        Check(TermsStore.Write(back) == null, "terms: an old cap writes no terms line");
        var fresh = new Cap();
        Check(fresh.Rt.RaiseStage == -2 && fresh.Rt.Breaches == 0, "terms: a cap starts with no board history");
        Check(Regex.IsMatch(ModSource(@"Tech\TechDirector.Venture.cs"), @"LoadTerms\(\);\s*CatchUpMoves") && ModSource(@"Tech\TechDirector.Terms.cs").Contains("data.Remove(TermsStore.Key)"),
            "terms: the line is read when the venture part starts, and an all-plain book leaves none");
    }

    private static void TermsSaveLine()
    {
        VentureFile f = SavedMoneyFile();
        Cap c = f.CapOf("Main~1");
        string without = VentureStore.Write(f);
        c.Holders[1].T = new Terms { Pref = 1500, Participating = true, Anti = 2, Vest = 1, Info = 2, Provision = 4242 };
        c.Sheets.Add(new TermSheet { Vc = 3, Amount = 5, Pre = 6, Lapse = 99, T = new Terms { Pref = 2000, Provision = 7 }, Pushed = true });
        c.Rt.Breaches = 3;
        c.Rt.BreachDay = 40;
        c.Rt.Approved = 777;
        c.Rt.ApprovedUntil = 55;
        c.Rt.RefusedUntil = 66;
        c.Rt.RaiseStage = 1;
        c.Rt.SlipDay = 30;
        Startup s = VentureRules.Born(f, 1, 1000);
        s.Holders.Add(new Holder { Kind = (int)HolderKind.Vc, Who = 2, Shares = 10, Invested = 10, Preference = 20, T = new Terms { Pref = 2000 } });
        f.Startups.Add(s);
        string withTerms = VentureStore.Write(f);
        Check(Regex.Replace(withTerms, "~[hsat]:[^~]*", string.Empty) == Regex.Replace(without, "~[hsat]:[^~]*", string.Empty) && withTerms.StartsWith("v=2") && VentureStore.Version == 2,
            "terms: reallife:tech_ventures keeps its version and the fields it had; terms never enter it");
        string line = TermsStore.Write(f);
        Check(line != null && TermsStore.Key == "reallife:tech_terms" && line.StartsWith("v=1"), "terms: the terms have a line of their own, reallife:tech_terms");
        VentureFile back = VentureStore.Read(withTerms, out _);
        TermsStore.Apply(new Dictionary<string, string> { { TermsStore.Key, line } }, back);
        Cap r = back.CapOf("Main~1");
        Terms t = r.Holders[1].T;
        Check(t != null && t.Pref == 1500 && t.Participating && t.Anti == 2 && t.Vest == 1 && t.Info == 2 && t.Provision == 4242 && r.Holders[0].T == null, "terms: a holder's terms come back from the line");
        Check(r.Sheets.Count == 1 && r.Sheets[0].T != null && r.Sheets[0].T.Pref == 2000 && r.Sheets[0].T.Provision == 7 && r.Sheets[0].Pushed, "terms: an offer's terms and its push come back");
        Check(r.Rt.Breaches == 3 && r.Rt.BreachDay == 40 && r.Rt.Approved == 777 && r.Rt.ApprovedUntil == 55 && r.Rt.RefusedUntil == 66 && r.Rt.RaiseStage == 1 && r.Rt.SlipDay == 30, "terms: the board's say comes back");
        Startup sb = back.StartupOf(s.Id);
        Check(sb != null && sb.Holders[sb.Holders.Count - 1].T != null && sb.Holders[sb.Holders.Count - 1].T.Pref == 2000, "terms: a city startup's terms come back");
        VentureFile again = VentureStore.Read(withTerms, out _);
        TermsStore.Apply(new Dictionary<string, string> { { TermsStore.Key, line } }, again);
        Check(TermsStore.Write(again) == line, "terms: the line is the same after a round trip");
        VentureFile damaged = VentureStore.Read(without, out _);
        TermsStore.Apply(new Dictionary<string, string> { { TermsStore.Key, "garbage" } }, damaged);
        Check(damaged.Caps[0].Holders[1].T == null, "terms: a damaged line is left as it is and the cap table stays plain");
    }

    private static void TermsExploit()
    {
        // Murphy's exploit with the hardest terms: the investors are still paid first by their terms, and no cent leaves uncounted
        Cap c = FundedCap(6);
        c.Holders[1].T = new Terms { Pref = 2000, Participating = true };
        c.Holders[1].Preference = Raise * 2;
        var caps = new List<Cap> { c };
        long counted = StartupMoney.Shortfall(caps, 0, Raise, 12, false, "ba:transaction_boatpurchase");
        StartupMoney.Wind w = StartupMoney.Close(c, 0, 30);
        Check(counted == Raise && c.Acc.Taken == Raise && w.Owed >= Raise && w.Gap == w.Owed && w.Fraud, "terms: the exploit with a 2x participating lead still ends in a claim and the charge (" + w.Gap + ")");
        Cap honest = FundedCap(6);
        honest.Holders[1].T = new Terms { Pref = 2000, Participating = true };
        honest.Holders[1].Preference = Raise * 2;
        StartupMoney.Profit(honest, -Raise * 6 / 10, 40);
        StartupMoney.Wind part = StartupMoney.Close(honest, Raise, 45);
        Check(part.Owed == Raise * 4 / 10 && part.Gap == 0 && !part.Fraud, "terms: an honest firm that kept 40% pays the investors by their terms (all of it, the preference being more) and owes nothing more (" + part.Owed + ")");
        Cap dry = FundedCap(6);
        dry.Holders[1].T = new Terms { Pref = 2000, Participating = true };
        dry.Holders[1].Preference = Raise * 2;
        StartupMoney.Profit(dry, -Raise, 40);
        Check(StartupMoney.Close(dry, 0, 45).Dry && StartupMoney.Close(dry, 0, 45).Owed == 0, "terms: hard terms do not make an honest failure cost the player anything");
        Cap shares = FundedCap(2);
        shares.Holders[1].T = new Terms { Pref = 1500, Participating = true };
        shares.Holders[1].Preference = Raise * 3 / 2;
        long[] pay = VentureRules.Waterfall(shares.Holders, StartupMoney.Balance(shares));
        Check(Sum(pay) == StartupMoney.Balance(shares), "terms: a wind down pays out exactly what the firm holds under any terms");
        // no new path moves money
        string d = ModSource(@"Tech\TechDirector.Terms.cs");
        bool moves = Regex.IsMatch(d, @"VentureGame\.(Give|Pay)\(|BizBooks|\.Book\(|Save\.Money|FirmsGame\.");
        Check(!moves, "terms: nothing in the terms code books a cent, so no new path takes money out of a funded firm");
        Check(d.Contains("Spend(own, Base(dropCents), save.Day)") && d.Contains("VentureTerms.Breach(own, save.Day)") && d.Contains("StatementLine(t)"),
            "terms: a spend at the firm's address over the provision with no approval is a breach, the firm's own running costs never are");
        string hook = MemberText(ModSource(@"Tech\TechDirector.Money.cs"), "private void OnMoneyEvent(string gameEvent)");
        int provision = hook.IndexOf("WatchProvision(save", StringComparison.Ordinal), floor = hook.IndexOf("_floorNow <= 0 || now >= was", StringComparison.Ordinal);
        Check(provision > 0 && hook.Contains("_provisionLeast > 0 && now < was") && provision < floor,
            "terms: the provision is watched on a money move only when the move is as big as the smallest provision, and the floor check stays as it was");
        string money = ModSource(@"Tech\TechDirector.Money.cs");
        check_related(money);
        Check(!Regex.IsMatch(d, @"\b(Update|LateUpdate|FixedUpdate|OnGUI|StartCoroutine)\(") && d.Contains("_provisionLeast"), "terms: nothing per frame");
    }

    private static void check_related(string money)
    {
        int allowed = money.IndexOf("RelatedAllowed(c, firm, paid, Save.Day)", StringComparison.Ordinal), book = money.IndexOf("VentureGame.Give(Level(paid), VentureGame.RelatedTakingsKey", StringComparison.Ordinal);
        Check(allowed > 0 && book > allowed, "terms: a related party deal over the provision needs the board before anything is booked");
    }

    private static void TermsWiring()
    {
        string money = ModSource(@"Tech\TechDirector.Money.cs");
        Check(money.Contains("StartupMoney.ThresholdOf(c)") && money.Contains("packDay || VentureTerms.OnDemand(c)") && money.Contains("StartupMoney.LookFindsOf(c)") && money.Contains("PackSlip(c, firm, day)"),
            "terms: the board uses each firm's information rights when it looks, how much it finds and how often it writes");
        string firm = ModSource(@"Tech\Venture\FirmAccount.cs");
        Check(firm.Contains("VentureTerms.PackEvery(c)") && firm.Contains("VentureTerms.Extra(c, day)"), "terms: the pack's interval and the breach weigh in the firm account");
        string d = ModSource(@"Tech\TechDirector.Terms.cs");
        Check(d.Contains("CapFor(place, false)") && !d.Contains("_venture = ") && ModSource(@"Tech\TechDirector.Venture.cs").Contains("WriteTerms(data)") && ModSource(@"Tech\TechDirector.Deals.cs").Contains("t.T = TermsFor(c, firm, vc, Save.Day)"),
            "terms: with Startups off nothing runs: the asks go through CapFor, which is null then, and the line is written and read only with the venture part");
        Check(ModSource(@"Tech\TechDirector.Venture.cs").Contains("CapsDay(day, mood)") && !ModSource(@"Tech\Venture\TermsStore.cs").Contains("Random"), "terms: the day runs as before and the store holds no dice");
        string[] pages = { @"Ui\Tech\TechLive.Terms.cs", @"Tech\Venture\TechApp.Terms.cs" };
        foreach (string page in pages)
        {
            string text = ModSource(page);
            Check(!text.Contains(".rootCanvas") && text.Contains("TermsView."), "terms: " + page + " builds on the kit, with no rootCanvas");
        }
        string live = ModSource(@"Ui\Tech\TechLive.Venture.cs") + ModSource(@"Ui\Tech\TechLive.Money.cs");
        Check(live.Contains("OfferCards(firm, c)") && live.Contains("HeldCards(c)") && live.Contains("StartupTerms(card, s)") && live.Contains("RightsLines(books, c)") && live.Contains("RightsButtons(out1, place, c)"),
            "terms: the offer, the firm's page, the startup's page and the firm account show the terms and the two asks");
        string app = ModSource(@"Tech\Venture\TechApp.Venture.cs") + ModSource(@"Tech\Venture\TechApp.Money.cs");
        Check(app.Contains("WriteOffer(box, firm, c, t)") && app.Contains("WriteHeld(box, c)") && app.Contains("WriteStartupTerms(box, s)") && app.Contains("WriteRights(box, place, c, day, out2)"), "terms: the older pages show them as well");
        string screens = ModSource(@"Ui\UiScreens.Tech.cs");
        foreach (string key in new[] { "reallife_vc_terms_label_pref", "reallife_vc_terms_cost_pref", "reallife_vc_terms_market", "reallife_vc_acct_provision", "reallife_vc_button_ask_spend", "reallife_vc_button_ask_raise", "reallife_vc_deck_stretched" })
        {
            Check(screens.Contains("\"" + key + "\""), "terms: the Tech screen lists " + key);
        }
        string options = ModSource(@"Core\RealLifeOverhaulMod.cs");
        Check(options.Contains("reallife_vc_test_terms") && !"Give my funded firm hard terms".StartsWith("Test"), "terms: the options page has a button to try hard terms, its label not beginning with Test");
        foreach (string file in new[] { @"Tech\Venture\VentureTerms.cs", @"Tech\Venture\TermsStore.cs", @"Tech\Venture\TermsView.cs", @"Tech\TechDirector.Terms.cs", @"Ui\Tech\TechLive.Terms.cs", @"Tech\Venture\TechApp.Terms.cs" })
        {
            string text = ModSource(file);
            int longest = 0;
            foreach (string l in text.Split('\n'))
            {
                longest = Math.Max(longest, l.TrimEnd('\r').Length);
            }
            Check(longest < 200, "terms: " + file + " keeps its lines under 200 columns (" + longest + ")");
        }
    }
}
