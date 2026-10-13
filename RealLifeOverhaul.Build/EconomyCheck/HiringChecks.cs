using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

// Checks for the labour market and hiring (Scripts/Tech/Hiring): the seekers, the shortage, the market's day with the
// rivals, applications to an ad, the interview, offers and answers, the recruitment office, search firms and the save line.
internal static partial class Program
{
    private static readonly HiringRules.RateOf FlatRate = (role, points) => 2000 + points * 30;

    private static void Hiring()
    {
        HiringSeekers();
        HiringShortage();
        HiringMarketDay();
        HiringApplications();
        HiringInterview();
        HiringOffers();
        HiringOffice();
        HiringSearch();
        HiringSave();
    }

    private static HiringFile NewLabour(int seed, int day, int cycle = 1)
    {
        var f = new HiringFile { Seed = seed, FirstDay = day };
        HiringRules.Day(f, day, cycle, false, HiringCatalogue.RivalFirms, FlatRate, new[] { "d1", "d2" }, null);
        return f;
    }

    private static void HiringSeekers()
    {
        Check(HiringRules.Target(0, false) < HiringRules.Target(3, false), "hiring: more look for work in a recession than in a boom");
        Check(HiringRules.Target(1, true) > HiringRules.Target(1, false), "hiring: graduates add to the market in the summer");
        Check(HiringRules.Target(3, true) <= HiringCatalogue.SeekersMost && HiringRules.Target(0, false) >= HiringCatalogue.SeekersLeast, "hiring: the market's size stays inside its bounds");
        Seeker a = HiringRules.NewSeeker(5, 10, 1, false, -1, -1, FlatRate, 1000, "d1"), b = HiringRules.NewSeeker(5, 10, 1, false, -1, -1, FlatRate, 1000, "d1");
        Check(a.P.Role == b.P.Role && a.Ask == b.Ask && a.Fit == b.Fit && a.Claim == b.Claim, "hiring: the same seed and id give the same seeker");
        int employedAsk = 0, idleAsk = 0, employed = 0, idle = 0, seniors = 0, juniors = 0, claims = 0;
        for (int id = 1; id <= 600; id++)
        {
            Seeker s = HiringRules.NewSeeker(9, id, 1, false, TalentCatalogue.SoftwareEngineer, TalentCatalogue.Mid, FlatRate, 1000, "d1");
            if (s.Employed)
            {
                employedAsk += s.Ask / 100;
                employed++;
            }
            else
            {
                idleAsk += s.Ask / 100;
                idle++;
            }
            Seeker any = HiringRules.NewSeeker(9, id + 1000, 1, false, -1, -1, FlatRate, 1000, "d1");
            seniors += any.P.Level >= TalentCatalogue.Senior ? 1 : 0;
            juniors += any.P.Level == TalentCatalogue.Junior ? 1 : 0;
            claims += any.Claim > 0 ? 1 : 0;
            if (any.Claim > HiringCatalogue.ClaimMost || any.Refs < 1 || any.Refs > 5)
            {
                Check(false, "hiring: a CV's claim and references stay in their bounds");
            }
        }
        Check(employed > 0 && idle > 0 && employedAsk / employed > idleAsk / idle, "hiring: seekers in work ask more than those out of it");
        Check(seniors < juniors, "hiring: seniors are fewer than juniors (" + seniors + " against " + juniors + ")");
        Check(claims > 200 && claims < 450, "hiring: about half of CVs stretch the truth (" + claims + " of 600)");
        int summerJuniors = 0;
        for (int id = 1; id <= 300; id++)
        {
            summerJuniors += HiringRules.NewSeeker(9, id, 1, true, -1, -1, FlatRate, 1000, "d1").P.Level == TalentCatalogue.Junior ? 1 : 0;
        }
        Check(summerJuniors > juniors / 2, "hiring: the summer brings more juniors");
        Check(HiringRules.RoleOf(Family.Built, 0) == 31 && HiringRules.RoleOf(Family.Built, 999) == 32, "hiring: a family's roles are drawn from the family");
    }

    private static void HiringShortage()
    {
        HiringFile f = NewLabour(3, 10);
        int before = HiringRules.Shortage(f, Family.Engineering, 10);
        for (int i = 0; i < 20; i++)
        {
            f.RivalAds.Add(new RivalAd { Rival = "X", Family = (int)Family.Engineering, Level = 1, To = 30 });
        }
        int after = HiringRules.Shortage(f, Family.Engineering, 10);
        Check(after > before, "hiring: more open jobs make a family shorter (" + before + " to " + after + ")");
        Check(after <= HiringCatalogue.ShortageMost && HiringRules.Shortage(new HiringFile(), Family.Support, 1) >= HiringCatalogue.ShortageLeast, "hiring: the shortage stays inside its bounds");
        Check(HiringRules.Shortage(new HiringFile(), Family.Security, 1) > HiringRules.Shortage(new HiringFile(), Family.Support, 1), "hiring: security people are scarcer than support staff");
    }

    private static void HiringMarketDay()
    {
        HiringFile f = NewLabour(4, 1);
        Check(HiringRules.Open(f) >= HiringRules.Target(1, false) - 1 && f.Seekers.Count <= HiringCatalogue.SeekersMost, "hiring: the first day fills the market");
        Check(f.RivalAds.Count == HiringCatalogue.RivalAdsByCycle[1], "hiring: the rivals post as many ads as the cycle brings");
        bool bounded = true;
        int names = 0;
        for (int day = 2; day < 200; day++)
        {
            HiringRules.Day(f, day, day % 4, day % 60 > 30, HiringCatalogue.RivalFirms, FlatRate, new[] { "d1" }, null);
            bounded &= f.Seekers.Count <= HiringCatalogue.SeekersMost && f.RivalAds.Count <= HiringCatalogue.RivalAdsMost;
        }
        for (int i = 0; i < f.Seekers.Count; i++)
        {
            names += f.Seekers[i].District == "d1" ? 1 : 0;
        }
        Check(bounded, "hiring: two hundred days keep the market inside its bounds");
        Check(names == f.Seekers.Count, "hiring: every seeker has a district of the city");
        // a rival takes a seeker who has applied: the application is withdrawn
        HiringFile g = NewLabour(6, 1);
        Seeker s = g.Seekers[0];
        g.Applicants.Add(new Applicant { Id = g.Next(), Seeker = s.Id, Ad = 99, Day = 1 });
        s.Until = 2;
        var notes = new List<MarketNote>();
        HiringRules.Day(g, 2, 1, false, HiringCatalogue.RivalFirms, FlatRate, null, notes);
        Check(g.Applicants[0].At == ApplyStage.Withdrawn && notes.Count > 0 && notes[0].Kind == HiringRules.WithdrewNote, "hiring: a seeker who finds work elsewhere withdraws, and the player is told");
        // one accepted is not taken by a rival nor dropped
        HiringFile h = NewLabour(7, 1);
        Seeker t = h.Seekers[1];
        h.Applicants.Add(new Applicant { Id = h.Next(), Seeker = t.Id, Ad = 99, Day = 1, Stage = (int)ApplyStage.Accepted, Start = 30 });
        t.Until = 2;
        for (int day = 2; day < 20; day++)
        {
            HiringRules.Day(h, day, 0, false, HiringCatalogue.RivalFirms, FlatRate, null, null);
        }
        Check(h.SeekerOf(t.Id) != null && h.Applicants[0].At == ApplyStage.Accepted, "hiring: a seeker who has accepted waits for their start");
    }

    private static JobAd Ad(HiringFile f, int role, int level, int payMost, bool boost, int work = 1)
    {
        var ad = new JobAd { Id = f.Next(), Place = "p", Role = role, Level = level, PayLeast = payMost * 85 / 100, PayMost = payMost, Work = work, From = 1, To = 31, Boost = boost };
        f.Ads.Add(ad);
        return ad;
    }

    private static void HiringApplications()
    {
        HiringFile f = NewLabour(8, 1, 3);
        JobAd ad = Ad(f, TalentCatalogue.SoftwareEngineer, TalentCatalogue.Mid, 99999, false);
        int week1 = 0;
        for (int day = 1; day <= 7; day++)
        {
            week1 += HiringRules.Apply(f, day, 0);
        }
        Check(week1 <= HiringCatalogue.AdWeekMost && ad.Came == week1, "hiring: a week's ad brings at most three (" + week1 + ")");
        HiringFile g = NewLabour(8, 1, 3);
        JobAd boosted = Ad(g, TalentCatalogue.SoftwareEngineer, TalentCatalogue.Mid, 99999, true);
        for (int day = 1; day <= 7; day++)
        {
            HiringRules.Apply(g, day, 0);
        }
        Check(boosted.Came <= HiringCatalogue.AdWeekMostBoosted, "hiring: a boosted week brings at most four (" + boosted.Came + ")");
        Check(HiringRules.OfficeBonus(1) + 1 > HiringCatalogue.AdWeekMostBoosted, "hiring: the recruitment office brings more than a boosted week of an ad, whatever is asked of it");
        Seeker s = HiringRules.NewSeeker(2, 5, 1, false, 0, 1, FlatRate, 1000, "d");
        s.Want = (int)Arrangement.Hybrid;
        var rich = new JobAd { Role = 0, Level = 1, PayMost = s.Ask * 2, Work = 1 };
        var poor = new JobAd { Role = 0, Level = 1, PayMost = s.Ask * 7 / 10, Work = 1 };
        Check(HiringRules.ApplyChance(s, rich, 0, 0) > HiringRules.ApplyChance(s, poor, 0, 0) * 2, "hiring: pay under the ask puts applicants off");
        Check(HiringRules.ApplyChance(s, rich, 10, 0) > HiringRules.ApplyChance(s, rich, -10, 0), "hiring: a good employer gets more applicants");
        Check(HiringRules.ApplyChance(s, rich, 0, 0) > HiringRules.ApplyChance(s, rich, 0, 6), "hiring: rivals hiring in the family take applicants away");
        var boostedAd = new JobAd { Role = 0, Level = 1, PayMost = s.Ask * 2, Work = 1, Boost = true };
        Check(HiringRules.ApplyChance(s, boostedAd, 0, 0) > HiringRules.ApplyChance(s, rich, 0, 0), "hiring: a boosted ad reaches more");
        var strict = new JobAd { Role = 0, Level = 1, PayMost = s.Ask * 2, Work = 1, MinSkill = 100 };
        Check(HiringRules.ApplyChance(s, strict, 0, 0) < HiringRules.ApplyChance(s, rich, 0, 0) / 2, "hiring: few apply who are short of the skill asked");
        var other = new JobAd { Role = TalentCatalogue.Accountant, Level = 1 };
        Check(!HiringRules.Matches(s, other) && HiringRules.Matches(s, rich), "hiring: only the same family applies");
        Check(HiringRules.AdCost(14, true) == HiringRules.AdCost(14, false) * HiringCatalogue.BoostCostBp / 10000, "hiring: a boosted ad costs two and a half times as much");
    }

    private static void HiringInterview()
    {
        Seeker s = HiringRules.NewSeeker(2, 6, 1, false, 0, 2, FlatRate, 1000, "d");
        s.Fit = 800;
        var a = new Applicant { Interest = 500 };
        HiringRules.Ask(a, s, HiringRules.TopicTechnical);
        HiringRules.Ask(a, s, HiringRules.TopicPay);
        Check((a.Revealed & HiringRules.RevealSkill) != 0 && (a.Revealed & HiringRules.RevealAsk) != 0 && (a.Revealed & HiringRules.RevealFit) == 0 && a.Interviewed && a.At == ApplyStage.Interviewed,
            "hiring: each question finds out its own thing");
        int keen = a.Interest;
        HiringRules.Ask(a, s, HiringRules.TopicMotive);
        Check(a.Interest > keen, "hiring: a well-suited applicant is keener after the question about the job");
    }

    private static void HiringOffers()
    {
        HiringFile f = NewLabour(10, 1);
        Seeker s = HiringRules.NewSeeker(10, 900, 1, false, TalentCatalogue.ItSupport, 1, FlatRate, 1000, "d");
        s.Employed = false;
        f.Seekers.Add(s);
        var a = new Applicant { Id = 1, Seeker = s.Id, Interest = 500 };
        HiringRules.Offer(a, s.Ask, 5);
        Check(HiringRules.Answer(f, a, s, 6, 0, 900) == ApplyStage.Accepted && a.Start >= 6 + HiringCatalogue.StartLeast && a.Start <= 6 + HiringCatalogue.StartUnemployedMost,
            "hiring: an offer at the ask is taken, and someone out of work starts within days");
        var b = new Applicant { Id = 2, Seeker = s.Id, Interest = 500 };
        HiringRules.Offer(b, s.Ask * 92 / 100, 5);
        Check(HiringRules.Answer(f, b, s, 6, 0, 900) == ApplyStage.Countered && b.Counter == s.Ask, "hiring: an offer a little short gets their ask back");
        var c = new Applicant { Id = 3, Seeker = s.Id, Interest = 500 };
        HiringRules.Offer(c, s.Ask * 70 / 100, 5);
        Check(HiringRules.Answer(f, c, s, 6, 0, 900) == ApplyStage.Declined, "hiring: an offer far short is turned down");
        var keen = new Applicant { Interest = 1000 };
        var cool = new Applicant { Interest = 0 };
        Check(HiringRules.Threshold(keen, 10) < HiringRules.Threshold(cool, -10), "hiring: a keen applicant and a good employer settle for less");
        Seeker w = HiringRules.NewSeeker(10, 901, 1, false, 0, 2, FlatRate, 1000, "d");
        w.Employed = true;
        w.Notice = 17;
        f.Seekers.Add(w);
        var d = new Applicant { Id = 4, Seeker = w.Id, Interest = 500 };
        HiringRules.Offer(d, w.Ask * 2, 5);
        HiringRules.Answer(f, d, w, 6, 0, 900);
        Check(d.At == ApplyStage.Accepted && d.Start == 6 + 17, "hiring: someone in work starts after their notice");
        int rivals = 0;
        for (int i = 0; i < 400; i++)
        {
            var e = new Applicant { Id = 100 + i, Seeker = w.Id, Interest = 500 };
            HiringRules.Offer(e, w.Ask, 5);
            rivals += HiringRules.Answer(f, e, w, 6, 0, 1300) == ApplyStage.Countered && e.RivalBid > 0 ? 1 : 0;
        }
        Check(rivals > 50 && rivals < 160, "hiring: in a scarce family about one in four has another offer (" + rivals + " of 400)");
        var lapse = new List<MarketNote>();
        var g = new HiringFile();
        g.Seekers.Add(w);
        g.Applicants.Add(new Applicant { Id = 1, Seeker = w.Id, Stage = (int)ApplyStage.Countered, OfferDay = 1 });
        g.Applicants.Add(new Applicant { Id = 2, Seeker = w.Id, Stage = (int)ApplyStage.New, Day = 1 });
        HiringRules.Answers(g, 2 + HiringCatalogue.ApplicantDays, 0, fam => 1000, lapse);
        Check(g.Applicants[0].At == ApplyStage.Declined && g.Applicants[1].At == ApplyStage.Lapsed, "hiring: a counter left unanswered is a no, and an old application lapses");
    }

    private static void HiringOffice()
    {
        Check(HiringRules.OfficeBonus(2) == HiringCatalogue.OfficeBonusLeast && HiringRules.OfficeBonus(20) == 10, "hiring: the office brings four more at the least, or half as many again");
        HiringFile f = new HiringFile { Seed = 1 };
        Seeker web = HiringRules.NewSeeker(1, 1, 1, false, TalentCatalogue.WebDesigner, 1, FlatRate, 1000, "d");
        Seeker back = HiringRules.NewSeeker(1, 2, 1, false, 2, 3, FlatRate, 1000, "d");
        Seeker lawyer = HiringRules.NewSeeker(1, 3, 1, false, 27, 3, FlatRate, 1000, "d");
        web.P.Main = 40000;
        back.P.Main = 90000;
        f.Seekers.Add(web);
        f.Seekers.Add(back);
        f.Seekers.Add(lawyer);
        int[] wanted = { TalentCatalogue.WebDesigner };
        Check(HiringRules.ForOffice(f, TalentCatalogue.Programmer, wanted, 1) == web, "hiring: the office puts forward the role the business needs before an abler one");
        Check(HiringRules.ForOffice(f, TalentCatalogue.Programmer, null, 0) == back, "hiring: with no role wanted, the ablest");
        web.Taken = true;
        back.Taken = true;
        Check(HiringRules.ForOffice(f, TalentCatalogue.Programmer, wanted, 1) == null, "hiring: nobody who is taken, and no lawyer for a programmer's job");
    }

    private static void HiringSearch()
    {
        Check(HiringRules.SearchFee(5000) == 5000 * TalentCatalogue.YearHours / 5, "hiring: a search costs a fifth of a year's pay");
        Check(HiringRules.Retainer(30000) == 9990, "hiring: a third of it when the search starts");
        HiringFile f = NewLabour(12, 1);
        var search = new JobAd { Id = f.Next(), Role = TalentCatalogue.Sre, Level = TalentCatalogue.Senior, Search = true, From = 1, To = 30, Due = 5 };
        f.Ads.Add(search);
        int open = HiringRules.Open(f);
        int made = HiringRules.Deliver(f, search, 5, FlatRate, 1100, null);
        bool senior = true, passive = true;
        for (int i = 0; i < f.Applicants.Count; i++)
        {
            Seeker s = f.SeekerOf(f.Applicants[i].Seeker);
            senior &= s.P.Level >= TalentCatalogue.Senior && s.P.Role == TalentCatalogue.Sre;
            passive &= s.Passive && s.Employed;
        }
        Check(made == HiringCatalogue.SearchCandidates && f.Applicants.Count == made && search.Came == made, "hiring: a search delivers its candidates");
        Check(senior && passive && HiringRules.Open(f) == open, "hiring: they are senior, in work and not on the open market");
        HiringRules.Day(f, 6, 1, false, HiringCatalogue.RivalFirms, FlatRate, null, null);
        Check(f.Applicants.Count == made && f.SeekerOf(f.Applicants[0].Seeker) != null, "hiring: a search's candidates stay while their application is live");
    }

    private static void HiringSave()
    {
        HiringFile f = NewLabour(13, 3);
        JobAd ad = Ad(f, TalentCatalogue.ProductManager, 2, 6000, true, 2);
        ad.Place = "Main%20St~4";
        f.Applicants.Add(new Applicant { Id = f.Next(), Seeker = f.Seekers[0].Id, Ad = ad.Id, Day = 3, Stage = 3, Offer = 5500, Revealed = 9, RivalBy = "R|x" });
        f.Campaigns.Add(new OfficeCampaign { Key = "a~1#b~2#ba:skill_programmer#5#100", Place = "b~2", Agency = "a~1", Skill = "ba:skill_programmer", From = 4, To = 9, Planned = 4, Delivered = 1 });
        var cf = new CandidateFile { Id = "Zz9+/=", Day = 3 };
        cf.P.Role = 7;
        cf.P.A = 51000;
        f.Candidates.Add(cf);
        int version;
        HiringFile back = HiringStore.Read(HiringStore.Write(f), out version);
        Check(back.Seed == 13 && back.Seekers.Count == f.Seekers.Count && back.RivalAds.Count == f.RivalAds.Count && back.NextId == f.NextId, "hiring: the market comes back as written");
        JobAd ad2 = back.AdOf(ad.Id);
        Check(ad2 != null && ad2.Place == "Main%20St~4" && ad2.Boost && ad2.Work == 2 && ad2.PayMost == 6000, "hiring: an ad comes back with its place and terms");
        Applicant a2 = back.Applicants[0];
        Check(a2.Offer == 5500 && a2.Revealed == 9 && a2.Stage == 3, "hiring: an application comes back with what was found out");
        Check(back.Campaigns.Count == 1 && back.Campaigns[0].Key == f.Campaigns[0].Key && back.Campaigns[0].Delivered == 1, "hiring: the office's campaigns come back by their key");
        Check(back.CandidateOf("Zz9+/=") != null && back.CandidateOf("Zz9+/=").P.A == 51000, "hiring: a candidate's file comes back under the game's id");
        Seeker s0 = f.Seekers[0], b0 = back.SeekerOf(s0.Id);
        Check(b0 != null && b0.Ask == s0.Ask && b0.P.Main == s0.P.Main && b0.Fit == s0.Fit && b0.Claim == s0.Claim && b0.District == s0.District, "hiring: a seeker comes back as written");
    }
}
