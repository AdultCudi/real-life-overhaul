using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;
using GamePart = RealLifeOverhaul.Tech.Part;

// The tech firm's figures (27 Nov 2026, a player's thread): every team line says how many people reach half and nine tenths of the most a role gives, and what one more
// adds (TeamNeedRules, SectorViews.Team), the way up explains its seven steps and lists what is met (TechPathViews), and "Game development pace" builds a game in two
// thirds or half of the days at the same quality, bugs and cost per game (StudioPace). Each check has a twin that feeds its comparison a wrong answer and wants it refused,
// so a check that can no longer fail shows itself. Run alone with --techplan29; prints the pace table with "print".
internal static partial class Program
{
    private static void TechPlan29()
    {
        TechPlanNeeds();
        TechPlanSupportAndTesters();
        TechPlanLines();
        TechPlanPath();
        TechPlanPaceRules();
        TechPlanPaceDays();
        TechPlanPaceEconomy();
        TechPlanPaceSaves();
        TechPlanSources();
        TechPlanTexts();
    }

    // ---------------------------------------------------------------- the people for a team figure

    // The least number of added people (each bringing `each`) whose figure reaches a target, by trying them one at a time: the definition the rule's turned-round curve must agree with.
    private static int TechPlanLeast(Func<long, int> figureOfAmount, long have, long each, int target)
    {
        for (int k = 0; k < 400000; k++)
        {
            if (figureOfAmount(have + k * each) >= target)
            {
                return k;
            }
        }
        return -1;
    }

    // Whether a TeamNeed's half and nine tenths people are the least that reach those figures, the nine tenths figure only reached by them and the one before it short, and Past/More agree.
    private static bool TechPlanNeedHolds(TeamNeed n, Func<long, int> figureOfAmount, int wrong = 0)
    {
        int half = TechPlanLeast(figureOfAmount, n.Amount, n.Each, n.HalfFigure), full = TechPlanLeast(figureOfAmount, n.Amount, n.Each, n.FullFigure);
        bool shortBefore = full <= 0 || figureOfAmount(n.Amount + (full - 1) * n.Each) < n.FullFigure;
        return half >= 0 && full >= 0 && n.HalfPeople + wrong == n.Have + half && n.FullPeople + wrong == n.Have + full && n.More == full && n.Past == (full == 0) && shortBefore
               && figureOfAmount(n.Amount) == n.Now && figureOfAmount(n.Amount + n.Each) - n.Now == n.EachFigure && n.Each > 0 && n.Top > n.FullFigure && n.FullFigure > n.HalfFigure;
    }

    private struct TechPlanCase
    {
        public Crew C;
        public SectorTeam T;
        public string Name;
    }

    private static List<TechPlanCase> TechPlanCases()
    {
        var list = new List<TechPlanCase>();
        // the player's own firm: an AI lab of 121 engineers and researchers with 1,923 points, 4 designers, no product people, 9 sellers, 1 reliability engineer, no security
        list.Add(new TechPlanCase
        {
            Name = "the thread's lab",
            C = new Crew { Engineering = 1923, Design = 32, Product = 0, Selling = 72, Testing = 8, Faults = 150, Support = 16, ReliabilityBrought = 150, ReliabilityWorking = 1, SecurityBrought = 0, SecurityWorking = 0 },
            T = new SectorTeam { People = 160, Builders = 121, Designers = 4, Product = 0, Sellers = 9, Reliability = 1, Security = 0, Testers = 1, Support = 16 }
        });
        // nobody at all
        list.Add(new TechPlanCase { Name = "nobody", C = new Crew { Faults = 150 }, T = new SectorTeam() });
        // a small studio
        list.Add(new TechPlanCase
        {
            Name = "a small studio",
            C = new Crew { Engineering = 64, Design = 16, Product = 8, Selling = 8, Testing = 8, Faults = 90, SecurityBrought = 350, SecurityWorking = 2, ReliabilityBrought = 300, ReliabilityWorking = 2 },
            T = new SectorTeam { People = 14, Builders = 8, Designers = 2, Product = 1, Sellers = 1, Reliability = 2, Security = 2, Testers = 1 }
        });
        // well past the nine tenths in every role
        list.Add(new TechPlanCase
        {
            Name = "well staffed",
            C = new Crew { Engineering = 80, Design = 4000, Product = 4000, Selling = 2000, Testing = 40, Faults = 60, SecurityBrought = 90000, SecurityWorking = 400, ReliabilityBrought = 90000, ReliabilityWorking = 400 },
            T = new SectorTeam { People = 900, Builders = 10, Designers = 500, Product = 500, Sellers = 250, Reliability = 400, Security = 400, Testers = 5 }
        });
        // product people and no engineers, designers with no engineers
        list.Add(new TechPlanCase
        {
            Name = "no engineers",
            C = new Crew { Engineering = 0, Design = 24, Product = 16, Selling = 0, Testing = 0, Faults = 150 },
            T = new SectorTeam { People = 5, Builders = 0, Designers = 3, Product = 2 }
        });
        // people who did not work yesterday: counted in the team, with nothing brought
        list.Add(new TechPlanCase
        {
            Name = "idle staff",
            C = new Crew { Engineering = 200, Design = 0, Product = 0, Selling = 0, Faults = 150, SecurityBrought = 0, SecurityWorking = 0 },
            T = new SectorTeam { People = 30, Builders = 25, Designers = 2, Product = 1, Sellers = 1, Security = 2, Reliability = 1 }
        });
        // a few spread cases for the brute-force loop
        for (int i = 1; i <= 8; i++)
        {
            list.Add(new TechPlanCase
            {
                Name = "case " + i,
                C = new Crew
                {
                    Engineering = 40 * i, Design = 6 * i * i, Product = 3 * i, Selling = 4 * i, Testing = 2 * i, Faults = 40 + 20 * i, SecurityBrought = 70 * i * i, SecurityWorking = i,
                    ReliabilityBrought = 55 * i * i, ReliabilityWorking = i
                },
                T = new SectorTeam { People = 10 * i, Builders = 5 * i, Designers = i, Product = i, Sellers = i, Reliability = i, Security = i, Testers = i }
            });
        }
        return list;
    }

    private static void TechPlanNeeds()
    {
        int cases = 0;
        bool all = true;
        foreach (TechPlanCase k in TechPlanCases())
        {
            Crew c = k.C;
            SectorTeam t = k.T;
            TeamNeed d = TeamNeedRules.Designers(c, t), s = TeamNeedRules.Sellers(c, t), r = TeamNeedRules.Reliability(c, t), x = TeamNeedRules.Security(c, t), p = TeamNeedRules.Product(c, t);
            bool ok = TechPlanNeedHolds(d, a => SectorRules.Lift(a, (long)c.Engineering + 1, SectorCatalogue.DesignGain))
                      && TechPlanNeedHolds(s, a => SectorRules.Lift(a, SectorCatalogue.SellingHalf, SectorCatalogue.SellingGain))
                      && TechPlanNeedHolds(r, a => SectorRules.Posture(FirmsCatalogue.ReliabilityBase, a, SectorCatalogue.ReliabilityHalf))
                      && TechPlanNeedHolds(x, a => SectorRules.Posture(FirmsCatalogue.SecurityBase, a, SectorCatalogue.SecurityHalf))
                      && TechPlanNeedHolds(p, a =>
                      {
                          Crew with = c;
                          with.Product = (int)a;
                          return SectorRules.ProductBoost(with);
                      });
            if (!ok)
            {
                Console.WriteLine("TECHPLAN need case failed: " + k.Name);
            }
            all &= ok;
            cases++;
        }
        Check(all && cases >= 14, "techplan29 needs: for every role with a curve (designers, sellers, reliability, security, product people) in " + cases + " teams (nobody, the thread's lab, a small studio, well past the top, no engineers), the people for half and for nine tenths of the most are the least whose figure reaches it, one fewer falls short, and what one more adds is the figure's own step");
        // a twin: the same comparison given a wrong answer refuses it
        TechPlanCase lab = TechPlanCases()[0];
        TeamNeed good = TeamNeedRules.Designers(lab.C, lab.T);
        Func<long, int> lift = a => SectorRules.Lift(a, (long)lab.C.Engineering + 1, SectorCatalogue.DesignGain);
        Check(TechPlanNeedHolds(good, lift) && !TechPlanNeedHolds(good, lift, 1), "techplan29 needs: a check of the people for a figure refuses a count one too many");
        // the numbers the player's picture gives
        TeamNeed lead = TeamNeedRules.Designers(lab.C, lab.T);
        Check(lead.Now == SectorRules.Lift(32, 1924, 400) && lead.Top == 400 && lead.FullFigure == 360 && lead.HalfFigure == 200 && lead.Have == 4 && lead.More > 1000 && lead.EachFigure > 0 && lead.EachFigure <= 2,
            "techplan29 needs: the thread's 4 designers beside 121 engineers lift appeal by " + lead.Now + " thousandths, and nine tenths of the most (+36%) takes " + lead.FullPeople + " designers, so the line says what each one adds instead (" + lead.EachFigure + " thousandths)");
        TeamNeed sell = TeamNeedRules.Sellers(lab.C, lab.T);
        Check(sell.HalfFigure == 1000 && sell.HalfPeople >= sell.Have && TeamNeedRules.AmountFor(1000, SectorCatalogue.SellingHalf, SectorCatalogue.SellingGain) == 40,
            "techplan29 needs: sellers reach +100% (half the most, the 55% the thread asked about more than doubled) at 40 points a day: " + sell.HalfPeople + " sellers of " + sell.Each + " points");
        // edges of the turned-round curve
        Check(TeamNeedRules.AmountFor(0, 40, 2000) == 0 && TeamNeedRules.AmountFor(2000, 40, 2000) == -1 && TeamNeedRules.AmountFor(1999, 40, 2000) == 79960 && SectorRules.Lift(79960, 40, 2000) == 1999 && SectorRules.Lift(79959, 40, 2000) == 1998,
            "techplan29 needs: the curve turned round: nothing for nothing, no amount reaches the top itself, and 1,999 of 2,000 takes exactly the amount whose lift is 1,999 and no less");
        Check(TeamNeedRules.PeopleToAdd(10, 10, 3) == 0 && TeamNeedRules.PeopleToAdd(10, 11, 3) == 1 && TeamNeedRules.PeopleToAdd(10, 19, 3) == 3 && TeamNeedRules.PeopleToAdd(10, 20, 0) == 10,
            "techplan29 needs: people to add are rounded up, none when already there, and never divide by a person who brings nothing");
        TeamNeed nobody = TeamNeedRules.Security(new Crew(), new SectorTeam());
        Check(nobody.Each == TeamNeedRules.DefaultSecurity && nobody.Each > 0 && nobody.Now == FirmsCatalogue.SecurityBase && nobody.More > 0,
            "techplan29 needs: with no security people the line counts a middle-level analyst (" + nobody.Each + " thousandths each), the posture is the base " + nobody.Now + ", and it takes " + nobody.More + " for nine tenths");
        TeamNeed none = TeamNeedRules.Product(new Crew { Engineering = 0 }, new SectorTeam());
        Check(none.FullPeople >= 1 && none.HalfPeople >= 1, "techplan29 needs: product people at a firm with no engineers still say one is needed, never none");
    }

    private static void TechPlanSupportAndTesters()
    {
        SupportNeed over = TeamNeedRules.Support(13, 16), under = TeamNeedRules.Support(13, 10), none = TeamNeedRules.Support(0, 3);
        Check(over.More == 0 && over.Spare == 3 && under.More == 3 && under.Spare == 0 && none.More == 0 && none.Need == 0,
            "techplan29 support: the thread's 16 of 13 is 3 to spare, 10 of 13 is 3 short, and nothing to answer needs nobody");
        // testers: the gap between what builders make and fix, over what a tester fixes, and enough testers close it
        bool ok = true;
        for (int focus = 0; focus < (int)Focus.Count; focus++)
        {
            for (int e = 40; e <= 2000; e *= 3)
            {
                var c = new Crew { Engineering = e, Faults = 150, Testing = e / 100, Product = 0 };
                var t = new SectorTeam { Builders = e / 8, Testers = 1 };
                TesterNeed n = TeamNeedRules.Testers(c, t, focus);
                int[] split = FirmsCatalogue.FocusSplit[focus];
                long made = (long)e * split[0] / 1000 * (1000 + SectorRules.ProductBoost(c)) / 1000 * 150 / 1000, self = (long)e * split[1] / 1000 / FirmsCatalogue.PointsPerFix;
                long each = c.Testing > 0 ? c.Testing : TeamNeedRules.DefaultPoints;
                bool closes = n.Points <= 0 ? n.More == 0 : (long)n.People * each >= n.Points && (n.More == 0 || (long)(n.People - 1) * each < n.Points);
                ok &= n.Made == made && n.Fixed == self && n.Points == Math.Max(0, made - self) && closes;
            }
        }
        Check(ok, "techplan29 testers: for each focus and size of firm, the testers that keep up are the bugs the builders make less those they fix as they go, over a tester's fixes, and one fewer would fall short");
        TesterNeed fixAll = TeamNeedRules.Testers(new Crew { Engineering = 100, Faults = 30 }, new SectorTeam(), (int)Focus.Quality);
        Check(fixAll.Points == 0 && fixAll.More == 0, "techplan29 testers: a firm whose builders fix as many bugs as they make needs no testers (quality focus, few faults)");
        // a twin: wrong claims are refused by the same arithmetic
        Check(!(TeamNeedRules.Support(13, 16).More == 1) && TeamNeedRules.Support(13, 10).More != 4, "techplan29 support: a check of the people short refuses a wrong count");
    }

    // ---------------------------------------------------------------- the lines on the page

    private static ViewSection TechPlanTeamSection(FirmKind kind, TechPlanCase k, long supportNeeded, long customers, int focus = 0)
    {
        var s = new SectorPlace { Place = "p", Name = "Lab", Kind = (int)kind, Crew = k.C, Team = k.T, SupportNeeded = supportNeeded, Customers = customers, Focus = focus };
        StudioScreen v = SectorViews.Firm(SectorFresh(3), new FirmsFile { Seed = 3 }, s, 10, new SectorWords());
        return v.Sections.Find(x => x.TitleKey == "reallife_tech_mkt_sec_team");
    }

    private static ViewLine TechPlanLine(ViewSection sec, string key) => sec.Lines.Find(l => l.Key == key);

    private static string TechPlanPair(ViewLine l, string name)
    {
        for (int i = 0; l != null && i + 1 < l.Pairs.Length; i += 2)
        {
            if (l.Pairs[i] == name)
            {
                return l.Pairs[i + 1];
            }
        }
        return null;
    }

    private static void TechPlanLines()
    {
        TechPlanCase lab = TechPlanCases()[0];
        ViewSection sec = TechPlanTeamSection(FirmKind.AiLab, lab, 13, 5000, (int)Focus.Features);
        string[] order = sec.Lines.ConvertAll(l => l.Key).ToArray();
        // each existing line is still there, with the need line straight after it
        string[][] pairs =
        {
            new[] { "reallife_tech_mkt_team_builders_ailab", "reallife_tech_mkt_need_builders" },
            new[] { "reallife_tech_mkt_team_testers", "reallife_tech_mkt_need_testers_more" },
            new[] { "reallife_tech_mkt_team_designers", "reallife_tech_mkt_need_gain" },
            new[] { "reallife_tech_mkt_team_product", "reallife_tech_mkt_need_gain" },
            new[] { "reallife_tech_mkt_team_support", "reallife_tech_mkt_need_support_met" },
            new[] { "reallife_tech_mkt_team_reliability", "reallife_tech_mkt_need_gain" },
            new[] { "reallife_tech_mkt_team_security", "reallife_tech_mkt_need_gain" },
            new[] { "reallife_tech_mkt_team_sellers", "reallife_tech_mkt_need_gain" }
        };
        bool straight = true;
        foreach (string[] pair in pairs)
        {
            int at = Array.IndexOf(order, pair[0]);
            straight &= at >= 0 && at + 1 < order.Length && order[at + 1] == pair[1];
        }
        Check(straight, "techplan29 lines: every team line of the thread's lab (engineers, testers, designers, product people, support, reliability, security, sellers) has its need line straight after it");
        // the figures in a line are the rule's
        TeamNeed d = TeamNeedRules.Designers(lab.C, lab.T), x = TeamNeedRules.Security(lab.C, lab.T);
        ViewLine dl = sec.Lines[Array.IndexOf(order, "reallife_tech_mkt_team_designers") + 1], sl = sec.Lines[Array.IndexOf(order, "reallife_tech_mkt_team_security") + 1];
        Check(TechPlanPair(dl, "half_count") == d.HalfPeople.ToString() && TechPlanPair(dl, "full_count") == d.FullPeople.ToString() && TechPlanPair(dl, "more") == d.More.ToString()
              && TechPlanPair(dl, "top") == "+40%" && TechPlanPair(dl, "full_fig") == "+36%" && TechPlanPair(dl, "half_fig") == "+20%"
              && TechPlanPair(sl, "half_count") == x.HalfPeople.ToString() && TechPlanPair(sl, "full_count") == x.FullPeople.ToString() && TechPlanPair(sl, "top") == "100%"
              && TechPlanPair(sl, "full_fig") == "92%" && TechPlanPair(sl, "half_fig") == "60%",
            "techplan29 lines: the designers' line says +20% and +36% of at most +40% with " + d.HalfPeople + " and " + d.FullPeople + " designers, security's 60% and 92% of at most 100% with " + x.HalfPeople + " and " + x.FullPeople + " analysts");
        Check(TechPlanPair(sl, "each") != null && TechPlanPair(dl, "each") != null && TechPlanPair(dl, "each").EndsWith("%") && TechPlanPair(dl, "each") != "less than 0.1%",
            "techplan29 lines: a team line with no ceiling to reach tells what one more person adds (" + TechPlanPair(dl, "each") + ")");
        // past the top, already above: the other text, with the people they have
        TechPlanCase rich = TechPlanCases()[3];
        ViewSection full = TechPlanTeamSection(FirmKind.Software, rich, 4, 3000);
        int pastLines = full.Lines.FindAll(l => l.Key == "reallife_tech_mkt_need_gain_past").Count;
        Check(pastLines == 5 && TechPlanLine(full, "reallife_tech_mkt_need_gain") == null, "techplan29 lines: a firm past nine tenths in every role is told so by five lines and none of the other text (" + pastLines + ")");
        // builders have no ceiling and say how much each adds; support says the ceiling
        Check(TechPlanPair(TechPlanLine(sec, "reallife_tech_mkt_need_builders"), "each") == "16" && TechPlanLine(sec, "reallife_tech_mkt_need_support_met") != null && TechPlanPair(TechPlanLine(sec, "reallife_tech_mkt_need_support_met"), "spare") == "3",
            "techplan29 lines: 121 engineers with 1,923 points add about 16 points a day each, and 16 of 13 support people is 3 to spare");
        ViewSection short1 = TechPlanTeamSection(FirmKind.Software, TechPlanCases()[2], 9, 5000);
        ViewLine sup = TechPlanLine(short1, "reallife_tech_mkt_need_support_more");
        Check(sup != null && TechPlanPair(sup, "more") == "9" && sup.Tone == ViewTone.Warn, "techplan29 lines: 9 tickets' people needed and none on the team says 9 more, as a warning");
        // a firm that makes no product has no tester, designer or support lines to explain
        ViewSection agency = TechPlanTeamSection(FirmKind.Design, TechPlanCases()[2], 0, 0);
        Check(TechPlanLine(agency, "reallife_tech_mkt_need_support_none") == null && TechPlanLine(agency, "reallife_tech_mkt_need_testers_more") == null && TechPlanLine(agency, "reallife_tech_mkt_need_builders") != null
              && TechPlanLine(agency, "reallife_tech_mkt_need_gain") != null,
            "techplan29 lines: a design studio, which builds no product, gets the need of its builders, security and sellers and none for testers or support");
        // a twin: the line check refuses a line whose figure was changed
        Check(!(TechPlanPair(dl, "full_fig") == "+37%"), "techplan29 lines: a check of a line's figure refuses another figure");
        // the options of a firm: the focus given to the page changes the testers' need
        ViewSection f0 = TechPlanTeamSection(FirmKind.Software, lab, 0, 0, (int)Focus.Features), f1 = TechPlanTeamSection(FirmKind.Software, lab, 0, 0, (int)Focus.Quality);
        ViewLine t0 = f0.Lines.Find(l => l.Key.StartsWith("reallife_tech_mkt_need_testers", StringComparison.Ordinal)), t1 = f1.Lines.Find(l => l.Key.StartsWith("reallife_tech_mkt_need_testers", StringComparison.Ordinal));
        Check(t0 != null && t1 != null && t0.Key == "reallife_tech_mkt_need_testers_more" && t1.Key == "reallife_tech_mkt_need_testers_none",
            "techplan29 lines: the testers' need reads the firm's own focus (a features focus needs testers, a quality focus has the builders fixing all they make)");
    }

    // ---------------------------------------------------------------- the way up

    private static PathFacts TechPlanFacts(IList<DataCentre> centres, int engineers, bool cto, int staff, long cash, int share)
    {
        // counted as TechDirector.PathNow counts them
        var f = new PathFacts { Share = share, Engineers = engineers, Cto = cto, StaffInfra = staff, Cash = cash };
        var districts = new HashSet<string>();
        for (int i = 0; i < centres.Count; i++)
        {
            DataCentre c = centres[i];
            f.Centres++;
            f.Racks += c.Racks;
            f.RacksAll += c.Racks + c.GpuRacks;
            f.TierTwo += c.Tier >= InfraCatalogue.CloudTierLeast ? 1 : 0;
            f.Net += c.Net ? 1 : 0;
            if (c.Racks + c.GpuRacks > 0 && c.District.Length > 0)
            {
                districts.Add(c.District);
            }
        }
        f.Districts = districts.Count;
        return f;
    }

    private static string TechPlanMet(TechPathStep[] steps)
    {
        var sb = new System.Text.StringBuilder();
        foreach (TechPathStep s in steps)
        {
            sb.Append(s.Done ? 'D' : '-').Append(':');
            foreach (TechPathReq r in s.Reqs)
            {
                sb.Append(r.Met ? '1' : '0');
            }
            sb.Append(' ');
        }
        return sb.ToString();
    }

    private static void TechPlanPath()
    {
        var fmt = new SectorWords();
        var rng = new Random(2911);
        bool cloudAgree = true, hyperAgree = true, bitsAgree = true;
        int scenarios = 0;
        for (int i = 0; i < 600; i++)
        {
            int n = rng.Next(0, 5);
            var centres = new List<DataCentre>();
            for (int c = 0; c < n; c++)
            {
                centres.Add(new DataCentre { Racks = rng.Next(0, 30), GpuRacks = rng.Next(0, 4) * 20, Tier = rng.Next(0, 3), Net = rng.Next(0, 3) > 0, District = rng.Next(0, 5) == 0 ? string.Empty : "d" + rng.Next(0, 5) });
            }
            int engineers = rng.Next(0, 5), staff = rng.Next(0, 4), share = rng.Next(0, 2) * EndScaleRules.Full;
            bool cto = rng.Next(0, 2) == 0;
            long cash = rng.Next(0, 4) * InfraRules.CloudCashLeast(share) / 2 + rng.Next(0, 3) * InfraRules.HyperCash(share) / 2;
            PathFacts f = TechPlanFacts(centres, engineers, cto, staff, cash, share);
            f.Firms = 1;
            f.LiveProducts = 1;
            f.People = 20;
            f.Monthly = TechPath.ScaleUpMonthly + 1;
            TechPathStep[] steps = TechPathViews.Build(f, fmt);
            bool net = centres.Count > 0 && centres.TrueForAll(c => c.Net);
            int cloudDay = InfraRules.CloudNeeds(centres, engineers, cash, net, share), hyperDay = InfraRules.HyperNeeds(centres, InfraRules.HasChiefs(cto, staff), cash, share);
            bool cloudAll = steps[4].Reqs.TrueForAll(r => r.Met), hyperAll = steps[5].Reqs.TrueForAll(r => r.Met);
            cloudAgree &= cloudAll == (cloudDay == 0) && TechPathViews.CloudBits(f) == cloudDay;
            hyperAgree &= hyperAll == (hyperDay == 0) && TechPathViews.HyperBits(f) == hyperDay;
            // each need's bit is its line's unmet state
            bool bits = true;
            for (int b = 0; b < steps[4].Reqs.Count; b++)
            {
                bits &= steps[4].Reqs[b].Met == ((cloudDay & (1 << b)) == 0);
            }
            bits &= steps[5].Reqs[0].Met == ((hyperDay & 1) == 0) && steps[5].Reqs[1].Met == ((hyperDay & 2) == 0) && steps[5].Reqs[4].Met == ((hyperDay & 8) == 0)
                    && (steps[5].Reqs[2].Met && steps[5].Reqs[3].Met) == ((hyperDay & 4) == 0);
            bitsAgree &= bits;
            scenarios++;
        }
        Check(cloudAgree && hyperAgree && bitsAgree, "techplan29 path: in " + scenarios + " made-up businesses (no data centre to five, at both scales) a cloud's and a hyperscaler's lines are all met exactly when the day's own judging (InfraRules.CloudNeeds, HyperNeeds) needs nothing, and each line is the bit it stands for");
        // a twin: judging with one need changed is refused
        var one = new List<DataCentre> { new DataCentre { Racks = 20, Tier = 1, Net = true, District = "a" } };
        PathFacts good = TechPlanFacts(one, 2, false, 0, InfraRules.CloudCashLeast(0), 0);
        TechPathStep[] now = TechPathViews.Build(good, fmt);
        Check(now[4].Reqs.TrueForAll(r => r.Met) && TechPathViews.CloudBits(good) == 0, "techplan29 path: 20 racks, tier II, two engineers, the cash and the connection meet every need of a cloud");
        good.Engineers = 1;
        TechPathStep[] short1 = TechPathViews.Build(good, fmt);
        Check(!short1[4].Reqs.TrueForAll(r => r.Met) && !short1[4].Reqs[2].Met && short1[4].Reqs[2].Pairs[1] == "1" && short1[4].Reqs[2].Pairs[3] == "2" && TechPathViews.CloudBits(good) == 4,
            "techplan29 path: one engineer of the two needed leaves exactly that line unmet, saying 1 of 2");
        // the thread's hyperscaler: 2 of 3 districts says 2 of 3 and every other line stands as it is
        var three = new List<DataCentre>
        {
            new DataCentre { Racks = 160, Tier = 1, Net = true, District = "a" }, new DataCentre { Racks = 160, Tier = 1, Net = true, District = "b" }
        };
        PathFacts hy = TechPlanFacts(three, 3, true, 2, InfraRules.HyperCash(0), 0);
        hy.CloudOpen = true;
        TechPathStep[] hs = TechPathViews.Build(hy, fmt);
        Check(!hs[5].Done && !hs[5].Reqs[0].Met && hs[5].Reqs[0].Pairs[1] == "2" && hs[5].Reqs[0].Pairs[3] == "3" && hs[5].Reqs[1].Met && hs[5].Reqs[2].Met && hs[5].Reqs[3].Met && hs[5].Reqs[4].Met && hs[4].Done,
            "techplan29 path: a firm with data centres in 2 districts of the 3 a hyperscaler needs reads 2 of 3, and the racks, the chief, the staff people and the cash are met");
        hy.Hyper = true;
        Check(TechPathViews.Build(hy, fmt)[5].Done, "techplan29 path: a hyperscaler the day has made reads done");
        // the firms' own steps
        var young = new PathFacts { Firms = 1, LiveProducts = 0, People = 14, Monthly = TechPath.ScaleUpMonthly };
        TechPathStep[] ys = TechPathViews.Build(young, fmt);
        Check(ys[0].Done && !ys[1].Done && !ys[2].Done && ys[2].Reqs[0].Met && !ys[2].Reqs[1].Met && ys[2].Reqs[1].Pairs[1] == "14" && ys[2].Reqs[1].Pairs[3] == TechPath.ScaleUpPeople.ToString(),
            "techplan29 path: a firm with 14 of the " + TechPath.ScaleUpPeople + " professionals and the revenue a scale-up needs is a tech firm, not yet a scale-up, and says 14 of " + TechPath.ScaleUpPeople);
        var scaled = new PathFacts { Firms = 1, LiveProducts = 1, People = TechPath.ScaleUpPeople, Monthly = TechPath.ScaleUpMonthly };
        Check(TechPathViews.Build(scaled, fmt)[2].Done && !TechPathViews.Build(new PathFacts { Firms = 1, LiveProducts = 1, People = TechPath.ScaleUpPeople, Monthly = TechPath.ScaleUpMonthly - 1 }, fmt)[2].Done,
            "techplan29 path: the scale-up needs the revenue to the cent and the people to the head");
        TechPathStep[] empty = TechPathViews.Build(new PathFacts(), fmt);
        bool none = true;
        foreach (TechPathStep s in empty)
        {
            none &= !s.Done && s.Reqs.Count > 0 && !s.Reqs.Exists(r => r.Met);
        }
        Check(none && TechPlanMet(empty).StartsWith("-:0 -:0 -:00 -:0 -:00000 -:00000 -:0 "), "techplan29 path: a new player starts with nothing done and every line unmet (" + TechPlanMet(empty).Trim() + ")");
        // every step has its three texts, and the model's keys are whole
        var en = LocaleFile("en");
        bool texts = true;
        foreach (TechPathStep s in TechPathViews.Build(TechPlanFacts(one, 2, false, 0, 0, 0), fmt))
        {
            texts &= en.ContainsKey(s.TitleKey) && en.ContainsKey(s.WhatKey) && en.ContainsKey(s.GaveKey);
            foreach (TechPathReq r in s.Reqs)
            {
                texts &= en.ContainsKey(r.Key) && TechPlanHolders(en[r.Key]).TrueForAll(h => Array.IndexOf(r.Pairs, h) >= 0);
            }
        }
        Check(texts && TechPathViews.Steps == 7 && en.ContainsKey("reallife_tech_path_req_met") && en.ContainsKey("reallife_tech_path_req_unmet"),
            "techplan29 path: each of the seven steps has its title, what it is and where to do it, what it gave, and every need's text with the figures it holds");
        // a twin: a different scale or count is refused by the comparison
        Check(!(TechPlanMet(TechPathViews.Build(young, fmt)) == TechPlanMet(TechPathViews.Build(scaled, fmt))), "techplan29 path: the state strings of two different businesses differ");
        // the zh-cn cloud step names a service, not a building (the thread's "Cloud Center")
        var zh = LocaleFile("zh-cn");
        string cloud = zh.TryGetValue("reallife_tech_path_cloud", out string c1) ? c1 : string.Empty, centre = zh.TryGetValue("reallife_tech_path_centre", out string c2) ? c2 : string.Empty;
        Check(cloud.Length > 0 && !cloud.Contains("中心") && !cloud.Contains("站") && cloud.Contains("云") && centre.Contains("数据中心"),
            "techplan29 path: in Chinese the cloud step reads as a service of your own (" + cloud + ") and only the data centre step is the building (" + centre + ")");
    }

    private static List<string> TechPlanHolders(string text)
    {
        var list = new List<string>();
        foreach (Match m in Regex.Matches(text, @"\{([a-z_]+)\}"))
        {
            list.Add(m.Groups[1].Value);
        }
        return list;
    }

    // ---------------------------------------------------------------- the pace

    private static void TechPlanPaceRules()
    {
        Check(StudioPace.SpeedOf(StudioPace.Normal) == 1000 && StudioPace.SpeedOf(StudioPace.Brisk) == 1500 && StudioPace.SpeedOf(StudioPace.Fast) == 2000
              && StudioPace.Clamp(-4) == StudioPace.Normal && StudioPace.Clamp(9) == StudioPace.Fast && StudioPace.SpeedOf(77) == 2000,
            "techplan29 pace: Normal builds at 1,000, Brisk at 1,500 and Fast at 2,000 thousandths of Normal's points a day, and a saved value out of range is read safely");
        Check(StudioPace.TakesShare(StudioPace.Brisk) == 666 && StudioPace.TakesShare(StudioPace.Fast) == 500 && StudioPace.TakesShare(StudioPace.Normal) == 1000,
            "techplan29 pace: Brisk takes 666 of every 1,000 days Normal takes (a third shorter) and Fast 500 (half)");
        Check(StudioPace.CrunchDays(0) == StudioCatalogue.CrunchDays && StudioPace.CrunchDays(1) == 9 && StudioPace.CrunchDays(2) == 7 && StudioPace.BoostDays(0) == StudioCatalogue.BoostDays
              && StudioPace.BoostDays(1) == 20 && StudioPace.BoostDays(2) == 15 && StudioPace.CrunchBurnout(0) == StudioCatalogue.CrunchBurnout,
            "techplan29 pace: a crunch lasts 14, 9 and 7 days and a month of contractors 30, 20 and 15 at Normal, Brisk and Fast, and Normal's burnout a day is as it was");
        bool whole = true;
        for (int p = 0; p < StudioPace.Count; p++)
        {
            int total = StudioPace.CrunchBurnout(p) * StudioPace.CrunchDays(p), was = StudioCatalogue.CrunchBurnout * StudioCatalogue.CrunchDays;
            whole &= Math.Abs(total - was) * 100 <= was * 5;
        }
        Check(whole, "techplan29 pace: a crunch's whole burnout stays within 5% of the 210 it cost at Normal at every pace");
        // a crunch applied by a milestone ends on the pace's day
        var g = new GameDev();
        StudioRules.ApplyMilestone(g, StudioRules.Hold, StudioRules.Crunch, 100);
        int normal = g.CrunchUntil;
        StudioRules.ApplyMilestone(g, StudioRules.Hold, StudioRules.Crunch, 100, StudioPace.Brisk);
        int brisk = g.CrunchUntil;
        StudioRules.ApplyMilestone(g, StudioRules.Hold, StudioRules.AddBudget, 100, StudioPace.Fast);
        Check(normal == 114 && brisk == 109 && g.BoostUntil == 115, "techplan29 pace: a crunch picked at a milestone on day 100 ends on day 114, 109 and a month of contractors at Fast on day 115");
        Check(StudioPace.Points(1000, 0) == 1000 && StudioPace.Points(1000, 1) == 1500 && StudioPace.Points(1000, 2) == 2000 && StudioPace.Points(0, 2) == 0 && StudioPace.Days(1000, 0, 1) == 0,
            "techplan29 pace: a day's points are multiplied by the speed, and with no points there are no days");
        Check(StudioPace.Overhead(1000, 0) == 0 && StudioPace.Overhead(1000, 1) == 500 && StudioPace.Overhead(1000, 2) == 1000 && StudioPace.Overhead(-5, 1) == 0 && StudioPace.DearerBy(1) == 500,
            "techplan29 pace: a day's pay carries nothing extra at Normal, half again at Brisk and the same again at Fast");
        Check(StudioPace.Years(137, 80) == "1.7" && StudioPace.Years(232, 80) == "2.9" && StudioPace.Years(0, 60) == "0.0" && StudioPace.Years(300, 60) == "5.0",
            "techplan29 pace: days are told as game years of the save's own length (137 days at 80 a year is 1.7)");
    }

    // The days a game takes to build with a crew, counted by building it a day at a time as the director does (Build then Advance, a milestone held at once).
    private static int TechPlanBuildDays(GameDev g, StudioCrew crew, int pace, out int dayAfterHalf)
    {
        int day = 0;
        dayAfterHalf = -1;
        while (g.Phase <= (int)GamePhase.Polish && day < 20000)
        {
            StudioRules.Build(g, crew, 1000, day, pace);
            if (StudioRules.Advance(g, day) == 1)
            {
                StudioRules.ApplyMilestone(g, StudioRules.Hold, StudioRules.Normal, day, pace);
            }
            day++;
            if (dayAfterHalf < 0 && StudioRules.Progress(g) >= 500)
            {
                dayAfterHalf = day;
            }
        }
        return day;
    }

    private static StudioCrew TechPlanCrew(long points) => new StudioCrew
    {
        Engineering = (int)(points * 60 / 100), Design = (int)(points * 25 / 100), Product = (int)(points - points * 60 / 100 - points * 25 / 100), Testing = 1,
        CraftEngineering = 62, CraftDesign = 58, CraftProduct = 55, CraftTesting = 60, Faults = 150
    };

    private static GameDev TechPlanGame(long budget)
    {
        var d = new GameDraft { Place = StudioPlace, Genre = (int)Genre.Adventure, Platform = (int)Platform.Pc, Budget = BudgetStep(budget), Price = 2 };
        return StudioRules.Greenlight(d, 7, new[] { StudioRules.AsPlanned, StudioRules.SelfPublish, StudioRules.NoDrm }, false, 0);
    }

    // The days at each pace for each scale with the ordinary team of the money scale (the points that build the scope's typical budget in its DevDays).
    private static void TechPlanPaceDays()
    {
        bool print = Environment.GetCommandLineArgs().Length > 2 && Environment.GetCommandLineArgs()[2] == "print";
        bool ok = true;
        var seen = new List<string>();
        for (int s = 0; s < (int)Scope.Count; s++)
        {
            long budget = ScaleCatalogue.BudgetTypical[s];
            long work = StudioRules.WorkFor(budget, StudioRules.AsPlanned, false);
            long points = (work + ScaleCatalogue.DevDays[s] - 1) / ScaleCatalogue.DevDays[s];
            int[] days = new int[StudioPace.Count];
            for (int p = 0; p < StudioPace.Count; p++)
            {
                GameDev g = TechPlanGame(budget);
                days[p] = TechPlanBuildDays(g, TechPlanCrew(points), p, out _);
                int forecast = StudioPace.Days(g.Work, points, p);
                ok &= Math.Abs(days[p] - forecast) * 100 <= Math.Max(300, forecast * 8);   // the card's forecast is the work over the points; the parts' rounding is the difference
            }
            ok &= Math.Abs(days[1] * 3 - days[0] * 2) <= Math.Max(3, days[0] / 50 * 3) && Math.Abs(days[2] * 2 - days[0]) <= Math.Max(2, days[0] / 50 * 2);
            seen.Add(((Scope)s) + " " + days[0] + "/" + days[1] + "/" + days[2]);
            if (print)
            {
                foreach (int year in new[] { 60, 80 })
                {
                    Console.WriteLine("  pace table " + (Scope)s + " (budget " + budget / 100 + ", " + work + " points, ordinary team " + points + " a day, year of " + year + " days): Normal " + days[0] + " days ("
                                      + StudioPace.Years(days[0], year) + " years), Brisk " + days[1] + " (" + StudioPace.Years(days[1], year) + "), Fast " + days[2] + " (" + StudioPace.Years(days[2], year) + ")");
                }
            }
        }
        Check(ok, "techplan29 pace: with the ordinary team of each scope a game takes about two thirds of Normal's days at Brisk and half at Fast, counted by building it a day at a time (" + string.Join(", ", seen) + ")");
        // the thread's game: 24,320,000 budget, 2,177 points a day
        long threadWork = StudioRules.WorkFor(2432000000L, StudioRules.AsPlanned, false);
        int n = StudioPace.Days(threadWork, 2177, 0), b = StudioPace.Days(threadWork, 2177, 1), f = StudioPace.Days(threadWork, 2177, 2);
        if (print)
        {
            foreach (int year in new[] { 60, 80 })
            {
                Console.WriteLine("  pace table the thread's game (budget 24,320,000, " + threadWork + " points, 2,177 a day, year of " + year + " days): Normal " + n + " days (" + StudioPace.Years(n, year) + " years), Brisk " + b + " ("
                                  + StudioPace.Years(b, year) + "), Fast " + f + " (" + StudioPace.Years(f, year) + ")");
            }
        }
        Check(n > 200 && Math.Abs(b * 3 - n * 2) <= 3 && Math.Abs(f * 2 - n) <= 2, "techplan29 pace: the thread's game of 24,320,000 with 2,177 points a day takes " + n + " days at Normal, " + b + " at Brisk and " + f + " at Fast");
        // a twin: a pace of the wrong speed is refused by the same comparison
        Check(!(Math.Abs(n * 3 / 2 * 3 - n * 2) <= 3), "techplan29 pace: a check of the days refuses a pace that is a third slower than it should be");
        // Normal is exactly today: the day's built points are the crew's points with no pace, and the same with the pace given as Normal
        var plain = TechPlanGame(ScaleCatalogue.BudgetTypical[1]);
        var named = TechPlanGame(ScaleCatalogue.BudgetTypical[1]);
        StudioCrew crew = new StudioCrew { Engineering = 1000, Design = 400, Product = 100, Testing = 50, CraftEngineering = 60, CraftDesign = 60, CraftProduct = 60, CraftTesting = 60, Faults = 150 };
        long a = StudioRules.Build(plain, crew, 1000, 5), c = StudioRules.Build(named, crew, 1000, 5, StudioPace.Normal), br = StudioRules.Build(TechPlanGame(ScaleCatalogue.BudgetTypical[1]), crew, 1000, 5, StudioPace.Brisk),
            fa = StudioRules.Build(TechPlanGame(ScaleCatalogue.BudgetTypical[1]), crew, 1000, 5, StudioPace.Fast);
        bool same = a == c && plain.Done == named.Done && plain.Hidden == named.Hidden && plain.BugCarry == named.BugCarry;
        for (int p = 0; p < (int)GamePart.Count; p++)
        {
            same &= plain.PartDone[p] == named.PartDone[p] && plain.PartCraft[p] == named.PartCraft[p];
        }
        Check(same && a == 1500 && br == 2250 && fa == 3000, "techplan29 pace: Normal builds exactly the crew's 1,500 points a day it always did (1,000 engineering, 400 design, 100 product), Brisk 2,250 and Fast 3,000, and every part, bug and carry is the same");
        Check(!(a == 1501), "techplan29 pace: a check of Normal's day refuses a day of 1,501");
    }

    // Quality, bugs and cost per game: the same game built at each pace.
    private static void TechPlanPaceEconomy()
    {
        bool quality = true, bugs = true, cost = true;
        var seen = new List<string>();
        for (int s = 0; s < (int)Scope.Count; s++)
        {
            long budget = ScaleCatalogue.BudgetTypical[s];
            long work = StudioRules.WorkFor(budget, StudioRules.AsPlanned, false);
            long points = (work + ScaleCatalogue.DevDays[s] - 1) / ScaleCatalogue.DevDays[s];
            int q0 = 0, b0 = 0;
            long c0 = 0;
            const long Wage = 1000000;   // the team's day of pay, cents at the start's prices: only the proportion matters
            for (int p = 0; p < StudioPace.Count; p++)
            {
                GameDev g = TechPlanGame(budget);
                int days = TechPlanBuildDays(g, TechPlanCrew(points), p, out _);
                int q = StudioRules.Quality(g), bugCount = g.Hidden + g.Found + g.Reported;
                long spent = days * Wage + days * StudioPace.Overhead(Wage, p);
                if (p == 0)
                {
                    q0 = q;
                    b0 = bugCount;
                    c0 = spent;
                }
                quality &= Math.Abs(q - q0) <= 1;
                bugs &= Math.Abs(bugCount - b0) * 50 <= Math.Max(b0, 50);
                cost &= Math.Abs(spent - c0) * 100 <= c0 * 3;
                seen.Add((Scope)s + " pace " + p + ": quality " + q + ", bugs " + bugCount + ", cost " + spent / 100);
            }
        }
        Check(quality, "techplan29 economy: the same game and team build to the same quality at Normal, Brisk and Fast (within a point) for every scope");
        Check(bugs, "techplan29 economy: and with the same bugs (within 2%), since a point of work hides what it did");
        Check(cost, "techplan29 economy: and cost the same to build (the pay for the days taken with the pace's overhead on each, within 3%: whole days) (" + string.Join("; ", seen) + ")");
        // a twin: without the overhead a brisk game is cheaper by a third, which the same comparison refuses
        long normalDays = 300, briskDays = 200;
        Check(Math.Abs(briskDays * 1000000L - normalDays * 1000000L) * 100 > normalDays * 1000000L * 2, "techplan29 economy: the cost check refuses a brisk game that pays for fewer days and nothing more");
        // a crunch keeps its share of a game: its extra work over the game's work, at each pace, within 5%
        bool share = true;
        for (int p = 0; p < StudioPace.Count; p++)
        {
            long extra = (long)StudioPace.CrunchDays(p) * StudioCatalogue.CrunchOutput * StudioPace.SpeedOf(p) / 1000, was = (long)StudioCatalogue.CrunchDays * StudioCatalogue.CrunchOutput;
            share &= Math.Abs(extra - was) * 100 <= was * 5;
            long contractors = (long)StudioPace.BoostDays(p) * StudioCatalogue.BoostOutput * StudioPace.SpeedOf(p) / 1000, boosted = (long)StudioCatalogue.BoostDays * StudioCatalogue.BoostOutput;
            share &= Math.Abs(contractors - boosted) * 100 <= boosted * 5;
        }
        Check(share, "techplan29 economy: a crunch and a month of contractors add the same share of a game's work at every pace (within 5%)");
        // the money's line: booked as a cost, and the day's overhead is the pay for the days the pace saves
        long wagesShare = 777777;
        long paid = 0;
        int daysAt = 150;
        for (int d = 0; d < daysAt; d++)
        {
            paid += wagesShare + StudioPace.Overhead(wagesShare, StudioPace.Brisk);
        }
        Check(Math.Abs(paid - (long)daysAt * wagesShare * 3 / 2) <= daysAt, "techplan29 economy: 150 brisk days at a day's pay of 777,777 pay what 225 normal days would");
    }

    // A game in progress keeps its progress and its parts when the pace changes; an old save has nothing new to read.
    private static void TechPlanPaceSaves()
    {
        long budget = ScaleCatalogue.BudgetTypical[1];
        GameDev g = TechPlanGame(budget);
        long points = (g.Work + 179) / 180;
        StudioCrew crew = TechPlanCrew(points);
        for (int day = 0; day < 60; day++)
        {
            StudioRules.Build(g, crew, 1000, day);
            if (StudioRules.Advance(g, day) == 1)
            {
                StudioRules.ApplyMilestone(g, StudioRules.Hold, StudioRules.Normal, day);
            }
        }
        int before = StudioRules.Progress(g);
        long doneBefore = g.Done, workBefore = g.Work;
        long[] parts = (long[])g.PartDone.Clone();
        // saved at Normal, read back, then built on at Brisk
        var f = new StudioFile { Seed = 5, FirstDay = 0, YearDays = 60 };
        f.Games.Add(g);
        string line = StudioStore.Write(f);
        StudioFile back = StudioStore.Read(line, out int version);
        GameDev h = back.Games[0];
        bool kept = version == StudioStore.Version && StudioStore.Write(back) == line && h.Done == doneBefore && h.Work == workBefore && StudioRules.Progress(h) == before;
        for (int p = 0; p < (int)GamePart.Count; p++)
        {
            kept &= h.PartDone[p] == parts[p];
        }
        Check(kept && before > 100 && before < 900, "techplan29 saves: a game 60 days into its build (" + before + " of 1,000 built) is saved and read back with its work, progress and parts as they were, and the save's line holds nothing of the pace");
        // the next day at each pace: nothing jumps, and the day's gain is the crew's points times the speed
        bool noJump = true;
        for (int p = 0; p < StudioPace.Count; p++)
        {
            GameDev x = StudioStore.Read(line, out _).Games[0];
            long at = x.Done;
            long gained = StudioRules.Build(x, crew, 1000, 60, p);
            noJump &= x.Work == workBefore && x.Done - at == gained && gained >= (crew.Engineering + crew.Design + crew.Product) * StudioPace.SpeedOf(p) / 1000 * 80 / 100 && gained <= (crew.Engineering + crew.Design + crew.Product + crew.Testing) * StudioPace.SpeedOf(p) / 1000;
            noJump &= StudioRules.Progress(x) >= before && StudioRules.Progress(x) - before <= (int)(1000 * gained / workBefore) + 2;
        }
        Check(noJump, "techplan29 saves: from the same saved game the next day adds the crew's points times the pace's speed at each pace, the game's work is unchanged, and no progress is lost or gained at the change");
        // the remaining days after a change are two thirds of Normal's, and the total is the half done at Normal plus them
        GameDev n0 = StudioStore.Read(line, out _).Games[0], n1 = StudioStore.Read(line, out _).Games[0];
        int rest0 = 0, rest1 = 0;
        for (int day = 60; n0.Phase <= (int)GamePhase.Polish && day < 20000; day++)
        {
            StudioRules.Build(n0, crew, 1000, day, 0);
            StudioRules.Advance(n0, day);
            rest0++;
        }
        for (int day = 60; n1.Phase <= (int)GamePhase.Polish && day < 20000; day++)
        {
            StudioRules.Build(n1, crew, 1000, day, 1);
            StudioRules.Advance(n1, day);
            rest1++;
        }
        Check(Math.Abs(rest1 * 3 - rest0 * 2) <= 4 && rest0 > 30, "techplan29 saves: the " + rest0 + " days left at Normal become " + rest1 + " at Brisk from the next day on");
        Check(!(Math.Abs(rest1 * 3 - rest0 * 3) <= 4), "techplan29 saves: a check of the days left refuses a pace that changed nothing");
        // an old save: a game and a studio written by the code before this change read the same (the codec has no pace to default)
        string oldLine = "v=1~h:5|7~g:9|" + StudioPlace.Replace("~", "%7E") + "|2";
        StudioFile thin = StudioStore.Read(oldLine, out _);
        Check(thin != null && StudioStore.Write(thin).Length > 0 && !ModSource("Tech/Studio/StudioStore.cs").Contains("Pace") && !ModSource("Tech/Studio/StudioFile.cs").Contains("Pace"),
            "techplan29 saves: an older save's studio line still reads, and neither the studio's save nor its records know of the pace, which is the option's alone");
    }

    // ---------------------------------------------------------------- the code and the texts

    private static void TechPlanSources()
    {
        string settings = ModSource("Core/Settings.cs"), mod = ModSource("Core/RealLifeOverhaulMod.cs"), tree = ModSource("Meta/OptionsTree.cs"), guide = ModSource("Tech/Studio/StudioGuide.cs");
        string studio = ModSource("Tech/TechDirector.Studio.cs"), desk = ModSource("Tech/TechDirector.StudioDesk.cs"), views = ModSource("Tech/Studio/StudioViews.cs"), game = ModSource("Tech/Studio/StudioGame.cs");
        Check(settings.Contains("TechGamePaceId = \"tech_game_pace\"") && settings.Contains("public static int TechGamePace = 1;") && settings.Contains("TechGamePace = System.Math.Max(0, System.Math.Min(2, Int(modId, TechGamePaceId, TechGamePace)))"),
            "techplan29 option: Game development pace is an int option, Brisk (1) by default, read back clamped to the three paces");
        Check(mod.Contains("AddDropdown(Settings.TechGamePaceId, \"reallife_game_pace\", new[] { \"reallife_game_pace_0\", \"reallife_game_pace_1\", \"reallife_game_pace_2\" }, Settings.TechGamePace")
              && tree.Contains(".Opt(\"tech_game_pace\", \"living_studio\")") && guide.Contains("Owns(Settings.LivingStudioId, Settings.TechGamePaceId)"),
            "techplan29 option: the drop-down is registered with its three names, sits under the game studio in the options tree and belongs to the studio's entry in the feature list");
        Check(studio.Contains("internal static int GamePace => StudioWanted ? StudioPace.Clamp(Settings.TechGamePace) : StudioPace.Normal;") && studio.Contains("StudioRules.Build(g, crew, share, day, pace)")
              && studio.Contains("StudioPace.Overhead(wages * share / 1000, pace)") && studio.Contains("StudioGame.PaceKey") && studio.Contains("StudioPace.Points(points * share / 1000, pace)")
              && studio.Contains("StudioPace.CrunchBurnout(GamePace)"),
            "techplan29 option: the studio's day builds at the pace, charges its overhead under its own money key, reads the launch forecast and the crunch's burnout by it, and Normal while the studio is off");
        Check(desk.Contains("StudioPace.Points(StudioPointsAt(place), GamePace)") && desk.Contains("day, GamePace);") && desk.Contains("StudioPace.Days(g.Work,") && desk.Contains("s.Pace = GamePace;")
              && desk.Contains("StudioPace.Points(points > 0 ? points"), "techplan29 option: the milestone's pick, the crunch's end, the pitch's days and every page's days left read the pace");
        Check(views.Contains("P + \"build_pace\"") && views.Contains("P + \"build_pace_cost\"") && views.Contains("StudioPace.Years(days, yearDays)"), "techplan29 option: the production card says the pace, the years left and what a faster day costs");
        Check(game.Contains("PaceKey = \"reallife_transaction_tech_game_pace\"") && ModSource("Economy/BizLines.cs").Contains("P + \"tech_game_pace\""),
            "techplan29 option: the overhead is booked under its own key among the business's costs");
        string infra = ModSource("Tech/TechDirector.Infra.cs"), app = ModSource("Tech/Firms/TechApp.Pages.cs"), kit = ModSource("Ui/Tech/TechLive.Firms.cs");
        Check(infra.Contains("f.Racks += c.Racks;") && infra.Contains("f.RacksAll += c.Racks + c.GpuRacks;") && infra.Contains("f.TierTwo += c.Tier >= InfraCatalogue.CloudTierLeast ? 1 : 0;")
              && infra.Contains("if (c.Racks + c.GpuRacks > 0 && c.District.Length > 0)") && infra.Contains("f.Cto |= p.Role == TalentCatalogue.Cto;")
              && infra.Contains("f.StaffInfra += p.Def.Family == Family.Infrastructure && p.Level >= InfraCatalogue.HyperStaffLevel ? 1 : 0;") && infra.Contains("f.Cash = VentureGame.Cash * 1000 / Math.Max(1, Prices.Level);"),
            "techplan29 path: the director counts the way up's facts as this check's replica does");
        Check(app.Contains("TechPathViews.Build(_director.PathNow(), _studioWords)") && kit.Contains("TechPathViews.Build(_d.PathNow(), W)") && kit.Contains("PathLines(card);") && kit.Contains("PathLines(way);"),
            "techplan29 path: the Tech app and the RLO interface draw the same list, the interface beside the firms' table with or without the league");
        string pages = ModSource("Tech/Sector/SectorViews.cs");
        Check(pages.Contains("NeedCurve(sec, TeamNeedRules.Security(s.Crew, t), false, fmt);") && pages.Contains("NeedSupport(sec, TeamNeedRules.Support(s.SupportNeeded, t.Support), fmt);") && pages.Contains("NeedTesters(sec, s, fmt);"),
            "techplan29 lines: the firm's page writes a need line under the designers', product, support, reliability, security, sellers' and testers' lines, which both interfaces draw from the same sections");
    }

    private static void TechPlanTexts()
    {
        string[] keys =
        {
            "reallife_game_pace", "reallife_game_pace_0", "reallife_game_pace_1", "reallife_game_pace_2", "reallife_opt_tip_tech_game_pace", "reallife_transaction_tech_game_pace",
            "reallife_transaction_tech_game_pace_label", "reallife_studio_build_pace", "reallife_studio_build_pace_cost", "reallife_tech_mkt_need_builders", "reallife_tech_mkt_need_tiny",
            "reallife_tech_mkt_need_who_designers", "reallife_tech_mkt_need_who_product", "reallife_tech_mkt_need_who_sellers", "reallife_tech_mkt_need_who_reliability", "reallife_tech_mkt_need_who_security",
            "reallife_tech_mkt_need_gain", "reallife_tech_mkt_need_gain_past", "reallife_tech_mkt_need_support_none", "reallife_tech_mkt_need_support_more", "reallife_tech_mkt_need_support_met",
            "reallife_tech_mkt_need_testers_none", "reallife_tech_mkt_need_testers_more", "reallife_tech_mkt_need_testers_met", "reallife_tech_path_req_met", "reallife_tech_path_req_unmet",
            "reallife_tech_path_studio_what", "reallife_tech_path_studio_gave", "reallife_tech_path_product_what", "reallife_tech_path_product_gave", "reallife_tech_path_scale_what", "reallife_tech_path_scale_gave",
            "reallife_tech_path_centre_what", "reallife_tech_path_centre_gave", "reallife_tech_path_cloud_what", "reallife_tech_path_cloud_gave", "reallife_tech_path_hyper_what", "reallife_tech_path_hyper_gave",
            "reallife_tech_path_ai_what", "reallife_tech_path_ai_gave", "reallife_tech_path_req_studio", "reallife_tech_path_req_product", "reallife_tech_path_req_scale_monthly",
            "reallife_tech_path_req_scale_people", "reallife_tech_path_req_centre", "reallife_tech_path_req_cloud_racks", "reallife_tech_path_req_cloud_tier", "reallife_tech_path_req_cloud_engineers",
            "reallife_tech_path_req_cloud_cash", "reallife_tech_path_req_cloud_net", "reallife_tech_path_req_hyper_regions", "reallife_tech_path_req_hyper_racks", "reallife_tech_path_req_hyper_cto",
            "reallife_tech_path_req_hyper_staff", "reallife_tech_path_req_hyper_cash", "reallife_tech_path_req_ai"
        };
        Dictionary<string, string> en = LocaleFile("en"), pt = LocaleFile("pt"), zh = LocaleFile("zh-cn");
        bool present = true, holders = true, style = true;
        foreach (string k in keys)
        {
            bool has = en.TryGetValue(k, out string e) && pt.TryGetValue(k, out string p) && zh.TryGetValue(k, out string z);
            present &= has;
            if (!has)
            {
                Console.WriteLine("TECHPLAN missing text " + k);
                continue;
            }
            List<string> h = TechPlanHolders(en[k]);
            h.Sort();
            List<string> hp = TechPlanHolders(pt[k]), hz = TechPlanHolders(zh[k]);
            hp.Sort();
            hz.Sort();
            holders &= string.Join(",", h) == string.Join(",", hp) && string.Join(",", h) == string.Join(",", hz);
            style &= !Regex.IsMatch(en[k], "[—–;!]| - ");
        }
        Check(present && holders, "techplan29 texts: all " + keys.Length + " new texts are in English, Portuguese and Chinese with the same {names}");
        Check(style, "techplan29 texts: none of the new English texts has a dash, a semicolon or an exclamation mark");
        Check(en["reallife_opt_tip_tech_game_pace"].Split(' ').Length <= 32 && en["reallife_opt_tip_tech_game_pace"].Length <= 190 && en["reallife_game_pace"].Length <= 30,
            "techplan29 texts: the option's label is short and its tip is within 32 words and 190 characters");
        // the old keys that changed text and the guide's page
        Check(en["reallife_studio_milestone_crunch_trade"].Contains("less at a faster pace") && en["reallife_studio_milestone_budget_trade"].Contains("less at a faster pace")
              && en["reallife_guide_studio"].Contains("{studio_pace_brisk}") && pt["reallife_guide_studio"].Contains("{studio_pace_brisk}") && zh["reallife_guide_studio"].Contains("{studio_pace_brisk}"),
            "techplan29 texts: the crunch's and the contractors' trade lines say a faster pace shortens them, and the studio's Guide page tells of the option in three languages");
        // What's new
        string[] lines = { "team lines now say how many people", "The way up now explains each of its seven steps", "A new option, Game development pace" };
        string[] ptLines = { "As linhas da equipe do app Tech agora dizem", "O caminho de crescimento agora explica", "Ritmo de desenvolvimento dos jogos" };
        string[] zhLines = { "团队各行现在会说明", "向上发展的路径现在会解释", "游戏开发节奏" };
        bool news = true;
        for (int i = 0; i < lines.Length; i++)
        {
            news &= WhatsNewHas(en, lines[i]) && WhatsNewHas(pt, ptLines[i]) && WhatsNewHas(zh, zhLines[i]);
        }
        Check(news, "techplan29 texts: What's new tells of the team lines, the way up and the pace in English, Portuguese and Chinese");
        // the guide's facts
        Check(TechPlanGuideFacts(ModSource("Tech/Studio/StudioGuide.cs")), "techplan29 texts: the Guide page's four pace figures are filled by the studio's entry");
    }

    private static bool TechPlanGuideFacts(string source) =>
        source.Contains("\"studio_pace_brisk\"") && source.Contains("\"studio_pace_fast\"") && source.Contains("\"studio_pace_brisk_dear\"") && source.Contains("\"studio_pace_fast_dear\"");
}
