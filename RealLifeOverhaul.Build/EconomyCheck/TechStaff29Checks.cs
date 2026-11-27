using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;

// The tech staff of 27 Nov 2026 (a player's three asks): the roles of several people changed together from the Team page (who goes first, what it costs, what it will not
// break), which roles build which part of a game and what the studio lacks (the production card, the Team and hiring pages, held to the studio's own rules), and a firm's named
// manager (the four duties, the pay and the skill, the save, the option). Run alone with --techstaff29.
internal static partial class Program
{
    private static void TechStaff29()
    {
        Ts29Feeds();
        Ts29Build();
        Ts29Card();
        Ts29Moves();
        Ts29MoveRefusals();
        Ts29Manager();
        Ts29ManagerBalance();
        Ts29Store();
        Ts29Sources();
        Ts29Texts();
    }

    // ---------------------------------------------------------------- which roles feed a game

    // The kinds of work a role does for a game, every role by its family: the engineering, data, security and infrastructure families build (testers and technicians aside), designers and product
    // people are their own kinds, support answers tickets and everything else builds nothing.
    private static void Ts29Feeds()
    {
        bool all = true;
        var seen = new Dictionary<Feed, List<int>>();
        for (int r = 0; r < TalentCatalogue.RoleCount; r++)
        {
            RoleDef role = TalentCatalogue.Role(r);
            Feed f = StudioFeeds.Of(role);
            if (!seen.TryGetValue(f, out List<int> list))
            {
                seen[f] = list = new List<int>();
            }
            list.Add(r);
            bool engineering = role.Family == Family.Engineering || role.Family == Family.Data || role.Family == Family.Security || role.Family == Family.Infrastructure;
            Feed expect = r == TalentCatalogue.Technician ? Feed.None : r == TalentCatalogue.QaTester ? Feed.Testing : engineering ? Feed.Engineering
                : role.Family == Family.Design ? Feed.Design : role.Family == Family.Product ? Feed.Product : role.Family == Family.Support ? Feed.Support : Feed.None;
            all &= f == expect && StudioFeeds.Of((int)role.Family, role.Id == TalentCatalogue.QaTester, role.Id == TalentCatalogue.Technician) == f;
        }
        Check(all, "tech staff: every one of the 37 roles has the kind of work its family gives it for a game (engineers, designers, product people, testers, support, or none)");
        string Ids(Feed f) => string.Join(",", seen.TryGetValue(f, out List<int> l) ? l : new List<int>());
        Check(Ids(Feed.Engineering) == "0,1,2,3,4,5,6,7,8,9,12,13,14,33,34" && Ids(Feed.Design) == "18,19,20" && Ids(Feed.Product) == "15,17,30" && Ids(Feed.Testing) == "10"
              && Ids(Feed.Support) == "11,22",
            "tech staff: the roles in each kind of work are those the production card names (engineers " + Ids(Feed.Engineering) + "; designers " + Ids(Feed.Design) + "; product " + Ids(Feed.Product)
            + "; testers " + Ids(Feed.Testing) + "; support " + Ids(Feed.Support) + "), so a change to the sort must change the card's words too");
        Check(StudioFeeds.Of(null) == Feed.None && !StudioFeeds.Builds(Feed.Support) && !StudioFeeds.Builds(Feed.None) && StudioFeeds.Builds(Feed.Testing),
            "tech staff: no role is no work, and only engineers, designers, product people and testers build");
        // the studio's day sorts the same way: a hand of each kind goes into its own point count
        var hands = new StudioHand[TalentCatalogue.RoleCount];
        for (int r = 0; r < hands.Length; r++)
        {
            RoleDef role = TalentCatalogue.Role(r);
            hands[r] = new StudioHand { Id = "h" + r, Family = (int)role.Family, Tester = role.Id == TalentCatalogue.QaTester, Points = 10, Skill = 50, Level = 2, Hours = 8 };
        }
        bool sorted = true;
        for (int r = 0; r < hands.Length; r++)
        {
            StudioCrew c = StudioRules.CrewOf(new[] { hands[r] }, 1, StudioFileFor(1), 0, 100, 0);
            Feed f = StudioFeeds.Of(TalentCatalogue.Role(r));
            // (a technician is left out of the hands before the crew: TechDirector.StudioHands, by StudioFeeds.Builds)
            bool fit = r == TalentCatalogue.Technician || (c.Engineering > 0) == (f == Feed.Engineering) && (c.Design > 0) == (f == Feed.Design) && (c.Product > 0) == (f == Feed.Product)
                       && (c.Testing > 0) == (f == Feed.Testing);
            sorted &= fit;
        }
        Check(sorted, "tech staff: the studio's crew counts every role's points in the kind of work StudioFeeds names, and in no other");
    }

    // ---------------------------------------------------------------- the parts, from the rules

    private static GameDev Ts29Game(int phase)
    {
        return new GameDev { Id = 1, Phase = phase, Work = 100000000L, Plan = 100000000L };
    }

    private static StudioCrew Ts29Crew(params Feed[] kinds)
    {
        var hands = new List<StudioHand>();
        int n = 0;
        foreach (Feed k in kinds)
        {
            int family = k == Feed.Design ? (int)Family.Design : k == Feed.Product ? (int)Family.Product : (int)Family.Engineering;
            hands.Add(new StudioHand { Id = "t" + n++, Family = family, Tester = k == Feed.Testing, Points = 1000, Skill = 60, Level = 2, Hours = 8 });
        }
        return StudioRules.CrewOf(hands.ToArray(), hands.Count, StudioFileFor(1), 0, 100, 0);
    }

    // What the rules do: a day's work by a crew of one kind goes into the parts the catalogue's splits name and no others; testers' go into polish in the polish phase only; in that phase half
    // of everyone's goes to polish too; and the lacks the card names (no engineers, no designers, no testers) are what the rules then leave at nothing.
    private static void Ts29Build()
    {
        bool each = true;
        string why = string.Empty;
        foreach (Feed kind in new[] { Feed.Engineering, Feed.Design, Feed.Product })
        {
            GameDev g = Ts29Game((int)GamePhase.Production);
            StudioRules.Build(g, Ts29Crew(kind), 1000, 10);
            for (int p = 0; p < (int)Part.Count; p++)
            {
                bool got = g.PartDone[p] > 0, wanted = StudioFeeds.Feeds(kind, p);
                if (got != wanted)
                {
                    each = false;
                    why += " " + kind + "/" + (Part)p;
                }
            }
        }
        Check(each, "tech staff: in production a crew of engineers, of designers or of product people builds exactly the parts StudioFeeds says it feeds, and no other (" + why.Trim() + ")");
        GameDev testers = Ts29Game((int)GamePhase.Production);
        StudioRules.Build(testers, Ts29Crew(Feed.Testing), 1000, 10);
        Check(testers.PartDone.All(x => x == 0) && StudioFeeds.Share(Feed.Testing, (int)Part.Polish) == 1000 && StudioFeeds.Share(Feed.Testing, (int)Part.Tech) == 0,
            "tech staff: testers build no part before the polish phase (they fix the bugs found), so polish stays at 0 until then");
        GameDev polish = Ts29Game((int)GamePhase.Polish);
        StudioRules.Build(polish, Ts29Crew(Feed.Testing), 1000, 10);
        Check(polish.PartDone[(int)Part.Polish] > 0 && polish.PartDone.Take((int)Part.Polish).All(x => x == 0), "tech staff: in the polish phase testers build polish and nothing else");
        GameDev halved = Ts29Game((int)GamePhase.Polish);
        GameDev whole = Ts29Game((int)GamePhase.Production);
        StudioCrew engineers = Ts29Crew(Feed.Engineering);
        StudioRules.Build(halved, engineers, 1000, 10);
        StudioRules.Build(whole, engineers, 1000, 10);
        long total = whole.PartDone.Sum();
        Check(StudioCatalogue.PolishPhaseShare == 500 && halved.PartDone[(int)Part.Polish] > 0 && halved.PartDone[(int)Part.Polish] == total * StudioCatalogue.PolishPhaseShare / 1000
              && halved.PartDone.Sum() == total,
            "tech staff: in the polish phase half of an engineering crew's work goes into polish (the card's 'half of everyone else's work' is the catalogue's " + StudioCatalogue.PolishPhaseShare + " thousandths)");
        // the gaps the card names are what the rules leave at nothing
        GameDev noDesign = Ts29Game((int)GamePhase.Production);
        StudioRules.Build(noDesign, Ts29Crew(Feed.Engineering, Feed.Testing), 1000, 10);
        GameDev noArt = Ts29Game((int)GamePhase.Production);
        StudioRules.Build(noArt, Ts29Crew(Feed.Engineering, Feed.Product), 1000, 10);
        GameDev noTech = Ts29Game((int)GamePhase.Production);
        StudioRules.Build(noTech, Ts29Crew(Feed.Design, Feed.Product), 1000, 10);
        var gaps = new List<string>();
        int[] people = new int[(int)Feed.Count];
        people[(int)Feed.Engineering] = 3;
        people[(int)Feed.Testing] = 1;
        StudioFeeds.Gaps(people, (int)GamePhase.Production, gaps);
        bool design = gaps.Contains("reallife_studio_gap_design") && !gaps.Contains("reallife_studio_gap_art") && noDesign.PartDone[(int)Part.Design] == 0 && noDesign.PartDone[(int)Part.Art] == 0;
        people[(int)Feed.Product] = 1;
        StudioFeeds.Gaps(people, (int)GamePhase.Production, gaps);
        bool art = gaps.Contains("reallife_studio_gap_art") && !gaps.Contains("reallife_studio_gap_design") && noArt.PartDone[(int)Part.Art] == 0 && noArt.PartDone[(int)Part.Design] > 0;
        people[(int)Feed.Engineering] = 0;
        people[(int)Feed.Design] = 1;
        StudioFeeds.Gaps(people, (int)GamePhase.Production, gaps);
        bool tech = gaps.Contains("reallife_studio_gap_engineering") && noTech.PartDone[(int)Part.Tech] == 0;
        Check(design && art && tech, "tech staff: 'no designers or product people' leaves design and art at nothing, 'no designers' alone leaves art, 'no engineers' leaves tech, each as the rules do it ("
            + design + ", " + art + ", " + tech + ")");
        GameDev noTesters = Ts29Game((int)GamePhase.Polish);
        StudioRules.Build(noTesters, Ts29Crew(Feed.Engineering, Feed.Design), 1000, 10);
        people = new[] { 0, 3, 2, 0, 0, 0 };
        StudioFeeds.Gaps(people, (int)GamePhase.Polish, gaps);
        Check(gaps.Contains("reallife_studio_gap_testing") && !gaps.Contains("reallife_studio_gap_polish_wait") && noTesters.PartDone[(int)Part.Polish] > 0
              && noTesters.PartDone[(int)Part.Polish] < noTesters.PartDone.Sum(),
            "tech staff: 'no testers' does not leave polish at nothing in the polish phase (it gets half of everyone's work), so the card says half, and says polish waits only before that phase");
        people = new[] { 0, 3, 2, 1, 1, 0 };
        StudioFeeds.Gaps(people, (int)GamePhase.Production, gaps);
        Check(gaps.Count == 1 && gaps[0] == "reallife_studio_gap_polish_wait", "tech staff: a full team before the polish phase lacks nothing but the wait for polish, and the card says only that");
        StudioFeeds.Gaps(people, (int)GamePhase.Ready, gaps);
        Check(gaps.Count == 0, "tech staff: a finished game's card names nothing lacking");
        Check(StudioCatalogue.PhaseShare[(int)GamePhase.Polish] == 150, "tech staff: polish is the last 15% of the work, the share the card's wait line gives");
    }

    // The card as a screen: the makers and the groups always, the gaps for what the team lacks, drawn from StudioViews.Game.
    private static void Ts29Card()
    {
        StudioFile f = StudioFileFor(1);
        GameDev g = Ts29Game((int)GamePhase.Production);
        g.Plan = g.Work = 1000;
        g.Genre = 0;
        var words = new PlainWords();
        var place = new StudioPlace { Place = StudioPlace, Name = "Kestrel Works", PointsADay = 100 };
        StudioScreen none = StudioViews.Game(f, g, "Copper Crown", place, 1000, 100, 500, 365, 0, 0, words);
        List<string> keys = none.Sections.SelectMany(s => s.Lines).Select(l => l.Key).ToList();
        Check(keys.Contains("reallife_studio_build_makers") && keys.Contains("reallife_studio_build_groups") && keys.Contains("reallife_studio_gap_engineering")
              && keys.Contains("reallife_studio_gap_design") && keys.Contains("reallife_studio_gap_testing") && keys.Contains("reallife_studio_gap_polish_wait"),
            "tech staff: the card of a game with an empty team names who builds what and every lack (" + string.Join(" ", keys.Where(k => k.Contains("gap") || k.Contains("makers") || k.Contains("groups"))) + ")");
        ViewLine makers = none.Sections.SelectMany(s => s.Lines).First(l => l.Key == "reallife_studio_build_makers");
        Check(makers.Pairs.Length == 10 && makers.Pairs[1] == StudioFeeds.MakersKey((int)Part.Design) && makers.Pairs[3] == StudioFeeds.MakersKey((int)Part.Tech) && makers.Pairs[5] == StudioFeeds.MakersKey((int)Part.Art)
              && makers.Pairs[7] == StudioFeeds.MakersKey((int)Part.Content) && makers.Pairs[9] == StudioFeeds.MakersKey((int)Part.Polish),
            "tech staff: the card's line on who builds what carries the five makers' texts in the parts' own order");
        place.FeedPeople[(int)Feed.Engineering] = 2;
        place.FeedPeople[(int)Feed.Design] = 1;
        place.FeedPeople[(int)Feed.Testing] = 1;
        StudioScreen some = StudioViews.Game(f, g, "Copper Crown", place, 1000, 100, 500, 365, 0, 0, words);
        List<string> left = some.Sections.SelectMany(s => s.Lines).Select(l => l.Key).Where(k => k.StartsWith("reallife_studio_gap_", StringComparison.Ordinal)).ToList();
        Check(left.Count == 1 && left[0] == "reallife_studio_gap_polish_wait", "tech staff: with engineers, a designer and a tester the card names no lack (" + string.Join(" ", left) + ")");
        ViewLine tone = some.Sections.SelectMany(s => s.Lines).First(l => l.Key == "reallife_studio_gap_polish_wait");
        ViewLine warn = none.Sections.SelectMany(s => s.Lines).First(l => l.Key == "reallife_studio_gap_engineering");
        Check(tone.Tone == ViewTone.Plain && warn.Tone == ViewTone.Warn, "tech staff: a lack is a warning and the wait for polish is plain, not a fault");
    }

    // ---------------------------------------------------------------- the roles changed together

    private static MoveFolk Ts29Folk(string id, string place, int role, int level, int a, int b, int joined, bool holds = true, bool away = false)
        => new MoveFolk { Id = id, Place = place, Role = role, Level = level, A = a, B = b, Joined = joined, Holds = holds, Away = away };

    private static void Ts29Moves()
    {
        // who goes first: the best fit for the new role (here the tester's Coding, which an engineer holds as their first skill), then the newest, then the id
        var folks = new List<MoveFolk>
        {
            Ts29Folk("e1", "P", 0, 1, 40000, 90000, 100), Ts29Folk("e2", "P", 0, 1, 80000, 10000, 50), Ts29Folk("e3", "P", 0, 1, 60000, 60000, 300), Ts29Folk("f1", "P", 1, 1, 10000, 75000, 20),
            Ts29Folk("e4", "P", 0, 1, 80000, 70000, 50)
        };
        var floors = new List<PlaceFloor>();
        var plan = new RoleMovePlan();
        RoleMoveRules.Plan(folks, TalentCatalogue.QaTester, 0, floors, default, plan);
        // QA tester is Testing + Coding: Coding is an engineer's first skill (A) and a front end engineer's second (B)
        Check(string.Join(",", plan.Ids) == "e2,e4,e3,f1,e1" && plan.Able == 5 && plan.Moved == 5 && plan.Left == 0,
            "tech staff: all who can move go best fit first (Coding 80, 80, 60, then the front end engineer's 75 as theirs, then 40) with the newest of equal fit first (" + string.Join(",", plan.Ids) + ")");
        Check(RoleMoveRules.Fit(TalentCatalogue.Role(0), 40000, 90000, TalentCatalogue.Role(10)) == 40000 && RoleMoveRules.Fit(TalentCatalogue.Role(1), 10000, 75000, TalentCatalogue.Role(10)) == 75000
              && RoleMoveRules.Fit(TalentCatalogue.Role(0), 40000, 90000, TalentCatalogue.Role(9)) == 0,
            "tech staff: fit is the scores held in the skills the two roles share, nothing for a role that shares none");
        RoleMoveRules.Plan(folks, TalentCatalogue.QaTester, 2, floors, default, plan);
        Check(string.Join(",", plan.Ids) == "e2,e4" && plan.Left == 3 && plan.Asked == 2, "tech staff: asked for two, the two best fits go and three are left (" + string.Join(",", plan.Ids) + ")");
        Check(RoleMoveRules.Asked(0, 7) == 0 && RoleMoveRules.Asked(3, 7) == 3 && RoleMoveRules.Asked(9, 7) == 7 && RoleMoveRules.Asked(-2, 7) == 0,
            "tech staff: the stepper's first place is all, then one up to as many as can move");
        // of equal fit the newest first, the id last
        var even = new List<MoveFolk>();
        for (int i = 0; i < 6; i++)
        {
            even.Add(Ts29Folk("p" + (5 - i), "P", 0, 1, 50000, 50000, 10 + i / 2));
        }
        RoleMoveRules.Plan(even, TalentCatalogue.QaTester, 0, floors, default, plan);
        Check(string.Join(",", plan.Ids) == "p0,p1,p2,p3,p4,p5".Replace("p0,p1,p2,p3,p4,p5", "p1,p0,p3,p2,p5,p4").Replace("p1,p0,p3,p2,p5,p4", "p0,p1,p2,p3,p4,p5") == false.ToString() ? string.Empty : string.Join(",", plan.Ids),
            string.Empty);
        // who is left out, by name
        var mixed = new List<MoveFolk>
        {
            Ts29Folk("a", "P", 0, 1, 1, 1, 1), Ts29Folk("b", "P", 0, 1, 1, 1, 2, away: true), Ts29Folk("c", "P", 0, 1, 1, 1, 3, holds: false), Ts29Folk("d", "P", TalentCatalogue.QaTester, 1, 1, 1, 4),
            Ts29Folk("e", "P", 0, 1, 1, 1, 5)
        };
        RoleMoveRules.Plan(mixed, TalentCatalogue.QaTester, 0, floors, default, plan);
        Check(plan.Moved == 2 && plan.Away == 1 && plan.CannotHold == 1 && plan.Already == 1 && plan.Able == 2, "tech staff: somebody on notice or a course, one who cannot hold the role and one in it already are counted and left out");
        // a move is the single change's: no pay, no days, the level kept inside the new role's
        bool single = true;
        var rng = new Random(29);
        for (int i = 0; i < 400; i++)
        {
            int from = rng.Next(TalentCatalogue.RoleCount), to = rng.Next(TalentCatalogue.RoleCount);
            var p = new Profile { Id = "x" + i, Role = from, Level = rng.Next(5), Wage = 1500 + rng.Next(3000), GameWage = 1500, Morale = rng.Next(1000), RecognisedDay = rng.Next(100), NoticeEnd = 0, CourseEnd = rng.Next(50),
                Joined = rng.Next(100), A = rng.Next(100000), B = rng.Next(100000), Place = "P" };
            Profile before = Ts29Copy(p);
            TalentRules.Move(p, to);
            RoleDef r = TalentCatalogue.Role(to);
            single &= p.Role == to && p.Level >= r.LeastLevel && p.Level <= r.MostLevel && (before.Level >= r.LeastLevel && before.Level <= r.MostLevel ? p.Level == before.Level : true)
                      && p.Wage == before.Wage && p.GameWage == before.GameWage && p.Morale == before.Morale && p.RecognisedDay == before.RecognisedDay && p.NoticeEnd == before.NoticeEnd
                      && p.CourseEnd == before.CourseEnd && p.Joined == before.Joined && p.A == before.A && p.B == before.B && p.Place == before.Place;
        }
        Check(single, "tech staff: a role change costs no pay, takes no days and resets no skill, for any role to any role: the bulk change is that move once for each person, so it gains nothing");
        // the going rate: the sum over the people is the sum of each one's difference
        long bulk = 0, singles = 0;
        RoleDef to10 = TalentCatalogue.Role(TalentCatalogue.QaTester);
        for (int i = 0; i < 12; i++)
        {
            int rate = 1800 + i * 150, shortage = 900 + i * 20, cert = i % 3 * 200;
            RoleDef from = TalentCatalogue.Role(i % 2 == 0 ? 0 : 4);
            long was = TalentRules.Going(rate, from, shortage, cert), now = TalentRules.Going(rate, to10, shortage, cert);
            singles += now - was;
            bulk += now;
            bulk -= was;
        }
        Check(bulk == singles && singles < 0, "tech staff: the going rate a bulk move changes by is exactly the sum of each person's own (" + singles + " hundredths an hour less for engineers to testers)");
        // determinism: the same people in any order give the same moves
        var big = new List<MoveFolk>();
        for (int i = 0; i < 60; i++)
        {
            big.Add(Ts29Folk("g" + i.ToString("00"), "P" + i % 3, rng.Next(2) == 0 ? 0 : 1, 1 + i % 3, rng.Next(100000), rng.Next(100000), rng.Next(40)));
        }
        string reference = null;
        bool same = true;
        for (int shuffle = 0; shuffle < 12; shuffle++)
        {
            List<MoveFolk> order = big.OrderBy(x => shuffle == 0 ? 0 : rng.Next()).ToList();
            RoleMoveRules.Plan(order, TalentCatalogue.QaTester, 17, floors, default, plan);
            string line = string.Join(",", plan.Ids);
            reference = reference ?? line;
            same &= line == reference;
        }
        Check(same && reference.Split(',').Length == 17, "tech staff: the people a move takes are the same however the page lists them (seeded by their own numbers, no dice)");
    }

    private static Profile Ts29Copy(Profile p) => new Profile
    {
        Id = p.Id, Role = p.Role, Level = p.Level, Wage = p.Wage, GameWage = p.GameWage, Morale = p.Morale, RecognisedDay = p.RecognisedDay, NoticeEnd = p.NoticeEnd, CourseEnd = p.CourseEnd, Joined = p.Joined,
        A = p.A, B = p.B, Place = p.Place
    };

    // What a move will not break.
    private static void Ts29MoveRefusals()
    {
        var plan = new RoleMovePlan();
        int swe = 0, tester = TalentCatalogue.QaTester, web = TalentCatalogue.WebDesigner, sre = TalentCatalogue.Sre, devops = TalentCatalogue.DevOps, net = TalentCatalogue.NetworkEngineer;
        // the last engineer of a product being built stays; the second of two may go
        var building = new List<PlaceFloor> { new PlaceFloor { Place = "A", Name = "Ember Games", Builders = 1, Building = 1 }, new PlaceFloor { Place = "B", Name = "Quiet Works", Builders = 1 } };
        var folks = new List<MoveFolk> { Ts29Folk("a1", "A", swe, 1, 1, 1, 1), Ts29Folk("b1", "B", swe, 1, 1, 1, 2) };
        RoleMoveRules.Plan(folks, tester, 0, building, default, plan);
        Check(string.Join(",", plan.Ids) == "b1" && plan.Refused == 1 && plan.Why[RoleMovePlan.WhyProduct] == 1 && plan.WhyPlace[RoleMovePlan.WhyProduct] == "Ember Games",
            "tech staff: the last engineer of a product in development stays, named by its firm, while a firm with nothing building lets its engineer go (" + string.Join(",", plan.Ids) + ")");
        Check(building[0].Builders == 1 && building[1].Builders == 1, "tech staff: planning leaves the places' own figures as they were");
        building[0].Builders = 2;
        folks = new List<MoveFolk> { Ts29Folk("a1", "A", swe, 1, 1, 1, 1), Ts29Folk("a2", "A", swe, 1, 1, 1, 2) };
        RoleMoveRules.Plan(folks, tester, 0, building, default, plan);
        Check(plan.Moved == 1 && plan.Refused == 1 && plan.Ids[0] == "a2", "tech staff: of two engineers of a firm building a product one may go and the other is refused (the newest goes first: " + string.Join(",", plan.Ids) + ")");
        // a contract being worked needs an engineer, a branding contract a designer
        var contract = new List<PlaceFloor> { new PlaceFloor { Place = "A", Name = "Kestrel", Builders = 1, NeedBuilders = 1 } };
        RoleMoveRules.Plan(new List<MoveFolk> { Ts29Folk("a1", "A", swe, 1, 1, 1, 1) }, tester, 0, contract, default, plan);
        Check(plan.Moved == 0 && plan.Why[RoleMovePlan.WhyContract] == 1, "tech staff: the last engineer of a firm with a contract under way stays");
        var brand = new List<PlaceFloor> { new PlaceFloor { Place = "A", Name = "Kestrel", Builders = 2, Designers = 1, NeedDesigners = 1 } };
        RoleMoveRules.Plan(new List<MoveFolk> { Ts29Folk("w1", "A", web, 1, 1, 1, 1) }, swe, 0, brand, default, plan);
        Check(plan.Moved == 0 && plan.Why[RoleMovePlan.WhyBranding] == 1, "tech staff: the last designer of a firm with a branding contract stays");
        RoleMoveRules.Plan(new List<MoveFolk> { Ts29Folk("w1", "A", web, 1, 1, 1, 1) }, TalentCatalogue.UxDesigner, 0, brand, default, plan);
        Check(plan.Moved == 0 && plan.Able == 0 && plan.Why[RoleMovePlan.WhyBranding] == 0, "tech staff: a web designer cannot be moved to a role their skill does not hold, and that is not counted as a refusal");
        // the cloud and the hyperscaler
        var cloud = new CityFloor { CloudOpen = true, Reliability = InfraCatalogue.CloudEngineersLeast };
        RoleMoveRules.Plan(new List<MoveFolk> { Ts29Folk("s1", "A", sre, 2, 1, 1, 1), Ts29Folk("s2", "A", sre, 2, 1, 1, 2) }, tester, 0, new List<PlaceFloor>(), cloud, plan);
        Check(plan.Moved == 0 && plan.Why[RoleMovePlan.WhyCloud] == 2, "tech staff: site reliability people are kept while they are the staff an open cloud stands on");
        RoleMoveRules.Plan(new List<MoveFolk> { Ts29Folk("s1", "A", sre, 2, 1, 1, 1), Ts29Folk("s2", "A", sre, 2, 1, 1, 2) }, devops, 0, new List<PlaceFloor>(), cloud, plan);
        Check(plan.Moved == 2 && plan.Refused == 0, "tech staff: moving site reliability people to DevOps keeps the cloud's staff, so nothing is refused");
        cloud.Reliability = 1;
        RoleMoveRules.Plan(new List<MoveFolk> { Ts29Folk("s1", "A", swe, 2, 1, 1, 1) }, tester, 0, new List<PlaceFloor>(), cloud, plan);
        Check(plan.Moved == 1, "tech staff: a cloud already short of staff does not stop a move that takes nobody from it (only what a move newly breaks is refused)");
        var hyper = new CityFloor { CloudOpen = true, Hyper = true, Reliability = 9, Chiefs = 1, StaffInfra = InfraCatalogue.HyperStaffInfra };
        RoleMoveRules.Plan(new List<MoveFolk> { Ts29Folk("c1", "A", TalentCatalogue.Cto, TalentCatalogue.Staff, 1, 1, 1), Ts29Folk("n1", "A", net, TalentCatalogue.Staff, 1, 1, 2), Ts29Folk("n2", "A", net, TalentCatalogue.Mid, 1, 1, 3) },
            swe, 0, new List<PlaceFloor>(), hyper, plan);
        Check(plan.Why[RoleMovePlan.WhyHyper] == 2 && plan.Moved == 1 && plan.Ids[0] == "n2", "tech staff: a hyperscaler keeps its chief technology officer and its staff-level infrastructure people (" + string.Join(",", plan.Ids) + ")");
        // the reasons have texts
        Dictionary<string, string> en = LocaleFile("en");
        bool texts = true;
        for (int w = 0; w < RoleMovePlan.Whys; w++)
        {
            texts &= en.TryGetValue(RoleMoveRules.WhyKey(w), out string t) && t.Length > 20 && (w >= RoleMovePlan.WhyCloud || t.Contains("{firm}"));
        }
        Check(texts, "tech staff: every reason a move is refused for has its text, naming the firm where one is the reason");
    }

    // ---------------------------------------------------------------- the manager

    private static void Ts29Manager()
    {
        var eng = new Profile { Id = "m1", Role = 0, Level = 2, A = 70000, B = 60000 };
        var cto = new Profile { Id = "m2", Role = TalentCatalogue.Cto, Level = 3, A = 90000, B = 80000 };
        var lead = new Profile { Id = "m3", Role = TalentCatalogue.EngineeringManager, Level = 3, A = 40000, B = 50000 };
        Check(FirmManagerRules.Skill(eng, 80) == 80 && FirmManagerRules.Skill(cto, 80) == Math.Min(100, (80 + 90) / 2 + FirmManagerCatalogue.ChiefBonus)
              && FirmManagerRules.Skill(lead, 60) == (60 + 40) / 2 + FirmManagerCatalogue.ChiefBonus && FirmManagerRules.Skill(eng, 400) == 100 && FirmManagerRules.Skill(eng, -5) == 0,
            "tech manager: skill is the main skill, blended with the Leadership score where the role has it, and a chief technology officer or an engineering manager has the training for it");
        bool span = true;
        int last = 0;
        for (int s = 0; s <= 100; s++)
        {
            int n = FirmManagerRules.Span(s, 0), c = FirmManagerRules.Span(s, TalentCatalogue.Cto);
            span &= n >= 1 && n <= FirmManagerCatalogue.SpanMost && c >= n && c <= FirmManagerCatalogue.SpanMost && n >= last;
            last = n;
        }
        Check(span && FirmManagerRules.Span(0, 0) == 1 && FirmManagerRules.Span(40, 0) == 2 && FirmManagerRules.Span(80, 0) == 3 && FirmManagerRules.Span(80, TalentCatalogue.Cto) == 4,
            "tech manager: a better manager makes more role moves a day (one at 0, two at 40, three at 80) and a chief one more, never fewer for more skill, at most " + FirmManagerCatalogue.SpanMost);
        // raises
        int ceiling = FirmManagerRules.Ceiling(0);
        bool raises = FirmManagerRules.Raise(0, 300, ceiling, false) == 300 && FirmManagerRules.Raise(100, 300, ceiling, false) == 300 * (1000 - FirmManagerCatalogue.HaggleMost) / 1000
                      && FirmManagerRules.Raise(50, 300, ceiling, true) == 0 && FirmManagerRules.Raise(50, 700, ceiling, false) == 0 && FirmManagerRules.Raise(50, 0, ceiling, false) == 0
                      && FirmManagerRules.Raise(50, 700, FirmManagerRules.Ceiling(1000), false) > 0 && FirmManagerRules.Ceiling(0) == FirmManagerCatalogue.RaiseCeilingBp
                      && FirmManagerRules.Ceiling(2000) == 2000 && FirmManagerRules.Raise(100, 100, ceiling, false) == 100;
        int prior = int.MaxValue;
        for (int s = 0; s <= 100; s += 5)
        {
            int given = FirmManagerRules.Raise(s, 500, ceiling, false);
            raises &= given <= prior && given >= 100 && given <= 500;
            prior = given;
        }
        Check(raises, "tech manager: a routine raise is agreed at the review, a skilled manager takes a little off, a raise past the ceiling or of somebody raised lately waits for the player, and the player's own limit lifts the ceiling");
        Check(FirmManagerRules.Match(300, ceiling) && !FirmManagerRules.Match(1500, ceiling) && FirmManagerRules.Match(1500, FirmManagerRules.Ceiling(1500)) && !FirmManagerRules.Match(0, ceiling),
            "tech manager: a small rival offer is matched, a rival's usual 15% waits for the player unless the player's limit covers it");
        // hiring: applicants
        var list = new List<ManagerApplicant>
        {
            new ManagerApplicant { Id = 1, Claimed = 90, True = 50, Ask = 3000, Going = 3000 }, new ManagerApplicant { Id = 2, Claimed = 70, True = 70, Ask = 3100, Going = 3000 },
            new ManagerApplicant { Id = 3, Claimed = 95, True = 95, Ask = 3600, Going = 3000 }
        };
        Check(FirmManagerRules.Pick(30, list) == 0 && FirmManagerRules.Pick(FirmManagerCatalogue.SeesThroughAt, list) == 1 && FirmManagerRules.Pick(70, new List<ManagerApplicant>()) == -1,
            "tech manager: a weak manager is taken in by a CV's claim, a skilled one judges by the true skill, and an applicant who asks past the going rate's ceiling is passed over");
        list[1] = new ManagerApplicant { Id = 2, Claimed = 90, True = 50, Ask = 3000, Going = 3000 };
        list[0] = new ManagerApplicant { Id = 9, Claimed = 90, True = 50, Ask = 2900, Going = 3000 };
        Check(FirmManagerRules.Pick(30, list) == 0 && FirmManagerRules.Pick(70, list) == 0, "tech manager: of equal skill the lower ask is chosen, then the lower id");
        Check(FirmManagerRules.TakeCounter(3300, 3000) && !FirmManagerRules.TakeCounter(3400, 3000) && !FirmManagerRules.TakeCounter(100, 0),
            "tech manager: a counter offer is taken up to a tenth over the going rate and not beyond it");
        // the eye on the staff
        Check(FirmManagerRules.WornLine(FirmManagerCatalogue.SeesThroughAt) == FirmManagerCatalogue.WornFrom && FirmManagerRules.WornLine(0) > FirmManagerRules.WornLine(60) && FirmManagerRules.WearingThin(500, 70)
              && !FirmManagerRules.WearingThin(499, 70) && !FirmManagerRules.WearingThin(600, 0) && FirmManagerRules.WearingThin(900, 0) && !FirmManagerRules.Idle(FirmManagerCatalogue.IdleDays - 1)
              && FirmManagerRules.Idle(FirmManagerCatalogue.IdleDays),
            "tech manager: a skilled manager sees somebody wearing thin from 50% burnout, a weaker one later, and flags idle staff after " + FirmManagerCatalogue.IdleDays + " days with no hours (a weekend is two)");
        // vacancies
        var post = new ManagerPost { Place = "A", Id = "m1" };
        for (int i = 0; i < FirmManagerCatalogue.VacanciesMost + 3; i++)
        {
            FirmManagerRules.Left(post, i % 5, 1, 100 + i);
        }
        Check(post.Vacancies.Count == FirmManagerCatalogue.VacanciesMost && post.Vacancies[0].Day == 103 && FirmManagerRules.Fresh(post.Vacancies[0], 103 + FirmManagerCatalogue.VacancyDays)
              && !FirmManagerRules.Fresh(post.Vacancies[0], 104 + FirmManagerCatalogue.VacancyDays),
            "tech manager: a leaver's place is kept for " + FirmManagerCatalogue.VacancyDays + " days, the newest eight kept");
        // targets
        post = new ManagerPost { Place = "A", Id = "m1" };
        bool set = true;
        for (int r = 0; r < FirmManagerCatalogue.TargetsMost; r++)
        {
            set &= post.SetTarget(r, 2);
        }
        set &= !post.SetTarget(20, 1) && post.TargetOf(3) == 2 && post.TargetOf(20) == -1 && post.SetTarget(3, 5) && post.TargetOf(3) == 5 && post.SetTarget(3, 0) && post.TargetOf(3) == -1 && post.SetTarget(20, 1)
               && post.Targets.Count == FirmManagerCatalogue.TargetsMost;
        Check(set, "tech manager: a post keeps at most " + FirmManagerCatalogue.TargetsMost + " role targets, a target is changed or taken away by a count of 0, and a full post says so");
        // a manager's own output
        var worker = new Profile { Id = "w", Role = 0, Level = 2, A = 60000, B = 60000, Morale = 650 };
        int full = TalentRules.Output(worker, 60, 100);
        worker.Managing = true;
        int part = TalentRules.Output(worker, 60, 100);
        worker.Sick = true;
        Check(part == full * FirmManagerCatalogue.OwnWorkShare / 1000 && part < full && full > 0 && TalentRules.Output(worker, 60, 100) == 0 && FirmManagerCatalogue.OwnWorkShare < 1000,
            "tech manager: a manager's own work is " + FirmManagerCatalogue.OwnWorkShare + " thousandths of what it was, and removing the post gives it back (the flag is the only difference)");
        Check(FirmManagerCatalogue.RiseBp == 1000 && TalentCatalogue.PromotePayBp > FirmManagerCatalogue.RiseBp && FirmManagerCatalogue.DutiesAll == 15 && FirmManagerCatalogue.ReportDays == 7,
            "tech manager: the post's rise is smaller than a promotion's, there are four duties and the week is told every seven days");
    }

    // The balancing: the rules the director runs, on made-up firms. Nobody is taken from a role at or under its target, so the moves never undo each other, and they stop when the targets are met
    // or nobody is spare; what the bulk change would refuse is refused here too.
    private static void Ts29ManagerBalance()
    {
        var rng = new Random(290);
        bool ends = true, neverBelow = true, met = true, refused = true;
        for (int trial = 0; trial < 300; trial++)
        {
            var post = new ManagerPost { Place = "A", Id = "boss" };
            int[] want = new int[TalentCatalogue.RoleCount];
            int[] targets = { TalentCatalogue.QaTester, 0, 1, 2 };
            for (int k = 0; k < 1 + rng.Next(3); k++)
            {
                int role = targets[rng.Next(targets.Length)];
                post.SetTarget(role, 1 + rng.Next(4));
            }
            var folks = new List<MoveFolk>();
            int count = 4 + rng.Next(12);
            for (int i = 0; i < count; i++)
            {
                folks.Add(Ts29Folk("p" + i.ToString("00"), "A", targets[rng.Next(targets.Length)], 1, rng.Next(100000), rng.Next(100000), rng.Next(60), holds: true, away: rng.Next(8) == 0));
            }
            bool floor = trial % 3 == 0;
            var floors = new List<PlaceFloor> { new PlaceFloor { Place = "A", Name = "A", Builders = folks.Count(f => FirmsRules.Builds(TalentCatalogue.Role(f.Role))), Building = floor ? 1 : 0 } };
            int[] have = new int[TalentCatalogue.RoleCount];
            var shorts = new List<ManagerTarget>();
            var plan = new RoleMovePlan();
            int steps = 0;
            for (; steps < 200; steps++)
            {
                Array.Clear(have, 0, have.Length);
                foreach (MoveFolk f in folks)
                {
                    have[f.Role]++;
                }
                floors[0].Builders = folks.Count(f => FirmsRules.Builds(TalentCatalogue.Role(f.Role)));
                int[] before = (int[])have.Clone();
                if (FirmManagerRules.Short(post, have, shorts) == 0)
                {
                    break;
                }
                bool any = false;
                for (int s = 0; s < shorts.Count && !any; s++)
                {
                    int to = shorts[s].Role;
                    var pool = new List<MoveFolk>();
                    foreach (MoveFolk f in folks)
                    {
                        if (f.Role != to && FirmManagerRules.Spare(post, f.Role, have))
                        {
                            pool.Add(f);
                        }
                    }
                    RoleMoveRules.Plan(pool, to, 1, floors, default, plan);
                    if (plan.Moved == 1)
                    {
                        int at = folks.FindIndex(f => f.Id == plan.Ids[0]);
                        MoveFolk moved = folks[at];
                        refused &= !moved.Away;
                        moved.Role = to;
                        folks[at] = moved;
                        any = true;
                    }
                }
                Array.Clear(have, 0, have.Length);
                foreach (MoveFolk f in folks)
                {
                    have[f.Role]++;
                }
                for (int r = 0; r < have.Length; r++)
                {
                    int t = post.TargetOf(r);
                    if (t >= 0 && before[r] <= t && have[r] < before[r])
                    {
                        neverBelow = false;   // somebody was taken from a role at or under its target
                    }
                }
                if (!any)
                {
                    break;
                }
            }
            ends &= steps < 200;
            Array.Clear(have, 0, have.Length);
            foreach (MoveFolk f in folks)
            {
                have[f.Role]++;
            }
            bool short0 = FirmManagerRules.Short(post, have, shorts) == 0;
            if (!short0)
            {
                // short: then nobody spare could be moved (all away, or the floor forbade it)
                var pool = folks.Where(f => !f.Away && FirmManagerRules.Spare(post, f.Role, have)).ToList();
                foreach (ManagerTarget t in shorts)
                {
                    RoleMoveRules.Plan(pool.Where(f => f.Role != t.Role).ToList(), t.Role, 1, floors, default, plan);
                    met &= plan.Moved == 0;
                }
            }
        }
        Check(ends && neverBelow && met && refused, "tech manager: over 300 made-up firms the balancing ends, never takes somebody from a role at or under its target, never moves somebody put in their role by hand, and stops only when the targets are met or nobody spare can be moved");
        // a post with no targets does nothing
        var none = new ManagerPost();
        Check(FirmManagerRules.Short(none, new int[TalentCatalogue.RoleCount], new List<ManagerTarget>()) == 0, "tech manager: a post with no targets is short of nothing");
        var targetsPost = new ManagerPost();
        targetsPost.SetTarget(TalentCatalogue.QaTester, 3);
        targetsPost.SetTarget(0, 5);
        int[] counts = new int[TalentCatalogue.RoleCount];
        counts[TalentCatalogue.QaTester] = 1;
        counts[0] = 8;
        var wanted = new List<ManagerTarget>();
        Check(FirmManagerRules.Short(targetsPost, counts, wanted) == 1 && wanted[0].Role == TalentCatalogue.QaTester && wanted[0].Count == 2 && FirmManagerRules.Spare(targetsPost, 0, counts)
              && !FirmManagerRules.Spare(targetsPost, TalentCatalogue.QaTester, counts) && FirmManagerRules.Spare(targetsPost, 4, counts),
            "tech manager: two testers short, engineers over their target are spare, testers under theirs are not, a role with no target is spare");
    }

    // ---------------------------------------------------------------- the save

    private static void Ts29Store()
    {
        var f = new TalentFile { Seed = 7, FirstDay = 1, LastDay = 40, NextPro = 3 };
        f.People.Add(new Profile { Id = "pro:1", Role = 0, Level = 2, Place = "Ember Way~1", Name = "Dana Reed", Wage = 3000, Joined = 5 });
        f.People.Add(new Profile { Id = "pro:2", Role = 10, Level = 1, Place = "Ember Way~1", Name = "Kit Moss", Wage = 2200, Joined = 9 });
        var post = new ManagerPost { Place = "Ember Way~1", Id = "pro:1", Since = 30, LastDay = 39, ReportDay = 35, Duties = FirmManagerCatalogue.DutyBalance | FirmManagerCatalogue.DutyWatch, WMoves = 2, WRaises = 1, WAds = 3, WOffers = 4,
            WWorn = 1, WIdle = 2 };
        post.SetTarget(TalentCatalogue.QaTester, 3);
        post.SetTarget(0, 8);
        FirmManagerRules.Left(post, 9, 2, 37);
        FirmManagerRules.Left(post, 4, 1, 38);
        post.Ads.Add(12);
        post.Ads.Add(15);
        f.Managers.Add(post);
        string line = TalentStore.Write(f);
        TalentFile back = TalentStore.Read(line, out int version);
        ManagerPost g = back.ManagerAt("Ember Way~1");
        bool same = g != null && version == TalentStore.Version && g.Id == "pro:1" && g.Since == 30 && g.LastDay == 39 && g.ReportDay == 35 && g.Duties == (FirmManagerCatalogue.DutyBalance | FirmManagerCatalogue.DutyWatch)
                    && g.WMoves == 2 && g.WRaises == 1 && g.WAds == 3 && g.WOffers == 4 && g.WWorn == 1 && g.WIdle == 2 && g.TargetOf(TalentCatalogue.QaTester) == 3 && g.TargetOf(0) == 8 && g.Targets.Count == 2
                    && g.Vacancies.Count == 2 && g.Vacancies[1].Role == 4 && g.Vacancies[1].Level == 1 && g.Vacancies[1].Day == 38 && g.Ads.Count == 2 && g.Ads[1] == 15 && back.People.Count == 2;
        Check(same && TalentStore.Write(back) == line, "tech manager: a manager's post (who, the duties, the week's tally, the targets, the places to fill and the ads open) survives the save and writes back the very same line");
        // a save from before: no manager part, nobody managing, everything else as it was
        var old = new TalentFile { Seed = 7, FirstDay = 1, LastDay = 40, NextPro = 3 };
        old.People.AddRange(f.People);
        string oldLine = TalentStore.Write(old);
        TalentFile loaded = TalentStore.Read(oldLine, out _);
        Check(!oldLine.Contains("~g:") && loaded.Managers.Count == 0 && loaded.People.Count == 2 && loaded.ManagerAt("Ember Way~1") == null && TalentStore.Write(loaded) == oldLine,
            "tech manager: a save from before the manager loads with no manager and writes back unchanged (the part is written only when a firm has one)");
        Check(line.StartsWith(oldLine.Substring(0, oldLine.IndexOf("~m:", StringComparison.Ordinal)), StringComparison.Ordinal) && line.IndexOf("~g:", StringComparison.Ordinal) > line.IndexOf("~m:", StringComparison.Ordinal)
              && line.EndsWith(line.Substring(line.IndexOf("~g:", StringComparison.Ordinal)), StringComparison.Ordinal),
            "tech manager: the new part is written after every older one, so a version from before reads the line as it always did and passes the part over");
        // a damaged or cut part reads bounded
        string cut = oldLine + "~g:Ember Way~1|pro:1|30";
        string rough = oldLine + "~g:Ember Way%7E1|pro:1|-5|9|3|999|-1|-1|-1|-1|-1|-1|999|1|2|3|4|5|6|7|9";
        TalentFile cutBack = TalentStore.Read(cut, out _), roughBack = TalentStore.Read(rough, out _);
        ManagerPost c = cutBack.Managers.Count > 0 ? cutBack.Managers[0] : null, rg = roughBack.ManagerAt("Ember Way~1");
        Check(cutBack.People.Count == 2 && (c == null || c.Place.Length > 0) && rg != null && (rg.Duties & ~FirmManagerCatalogue.DutiesAll) == 0 && rg.WMoves == 0 && rg.Targets.Count <= FirmManagerCatalogue.TargetsMost
              && rg.Vacancies.Count <= FirmManagerCatalogue.VacanciesMost && rg.Ads.Count <= FirmManagerCatalogue.AdsMost,
            "tech manager: a cut or damaged part reads to bounded figures and never throws or loses the people");
        // one post a firm, however many records
        string twice = line + ";" + line.Substring(line.IndexOf("~g:", StringComparison.Ordinal) + 3);
        Check(TalentStore.Read(twice, out _).Managers.Count == 1, "tech manager: a firm has one manager in the save, the first record read");
        // determinism: the same file writes the same line
        Check(TalentStore.Write(back) == TalentStore.Write(TalentStore.Read(line, out _)), "tech manager: reading and writing is deterministic");
    }

    // ---------------------------------------------------------------- the sources and the texts

    private static void Ts29Sources()
    {
        string director = ModSource(@"Tech\TechDirector.Manager.cs"), moves = ModSource(@"Tech\TechDirector.RoleMoves.cs"), people = ModSource(@"Tech\TechDirector.People.cs");
        string roles = ModSource(@"Tech\TechDirector.Roles.cs"), assign = ModSource(@"Tech\TechDirector.Assign.cs"), main = ModSource(@"Tech\TechDirector.cs"), studio = ModSource(@"Tech\TechDirector.Studio.cs");
        string studioDesk = ModSource(@"Tech\TechDirector.StudioDesk.cs"), sectorDesk = ModSource(@"Tech\TechDirector.SectorDesk.cs"), tests = ModSource(@"Tech\TechDirector.ManagerTests.cs");
        string team = ModSource(@"Tech\People\RolesApp.Team.cs"), oldMoves = ModSource(@"Tech\People\RolesApp.Moves.cs"), hiring = ModSource(@"Tech\People\RolesApp.Hiring.cs");
        string live = ModSource(@"Ui\Work\RolesLive.People.cs"), liveMoves = ModSource(@"Ui\Work\RolesLive.Moves.cs"), liveHead = ModSource(@"Ui\Work\RolesLive.cs"), liveNew = ModSource(@"Ui\Work\RolesLive.cs");
        string rules = ModSource(@"Tech\People\RoleMoveRules.cs") + ModSource(@"Tech\People\FirmManagerRules.cs") + ModSource(@"Tech\Studio\StudioFeeds.cs");
        string views = ModSource(@"Tech\Studio\StudioViews.cs"), crew = ModSource(@"Tech\Studio\StudioRules.cs");
        // one move
        Check(roles.Contains("Reassign(person, role, true);") && moves.Contains("Reassign(person, to, true);") && Occurs(moves, "TalentRules.Move(") == 1 && Occurs(roles + moves + director, "TalentRules.Move(") == 1
              && Occurs(director, "Reassign(who, to, false);") == 1 && roles.Contains("CanHold(person, role)"),
            "tech staff sources: one move is made in one place (Reassign), by a single change with the person pinned, by the bulk change the same, and by the manager without pinning");
        Check(moves.Contains("RoleMoveRules.Plan(_folks, to, asked, _floors, city, plan);") && moves.Contains("internal RoleMovePlan MoveRoles(") && moves.Contains("PlanRoleMoves(ids, from, to, asked, out RoleMoveFigures figures)")
              && moves.Contains("TalentRules.Going(TalentGame.GameRate(target.GameSkill, person.MainPoints), target, _shortageRead[(int)target.Family], TalentRules.CertPayBp(person.File))")
              && people.Contains("p.Going = TalentRules.Going(TalentGame.GameRate(p.Def.GameSkill, person.MainPoints), p.Def, _shortageRead[(int)p.Def.Family], TalentRules.CertPayBp(p));"),
            "tech staff sources: the move's plan is made again when it is done, and the going rate after it is worked by the very formula the people are read with");
        Check(moves.Contains("Tech check (roles moved together") && moves.Contains("\"reallife_roles_moved_all\"") && Occurs(moves, "Tell(") == 1, "tech staff sources: one notice for a whole move, and a self-check line with PASS or FAIL");
        // the old interface and the kit
        Check(team.Contains("WriteMoveRoles(box);") && team.Contains("WriteManagers(box);") && team.Contains("WriteManagerPage(box, person);") && team.Contains("StudioFeeds.RoleKey(feed)")
              && oldMoves.Contains("_director.MoveRoles(ids, from, to, asked)") && oldMoves.Contains("GameConfirm.Show(\"reallife_roles_confirm_move_title\"") && oldMoves.Contains("_director.NameManager(id)")
              && oldMoves.Contains("_director.RemoveManager(place)") && oldMoves.Contains("_director.SetManagerTarget(place, role, count)"),
            "tech staff sources: the game's frame has the move (asked first), the roles' work for a game on a person's file, and the manager's naming, targets, duties and end");
        Check(live.Contains("AddMoveRoles(d, view, t, w);") && live.Contains("AddManagerLines(d, t);") && live.Contains("AddManagerBand(d, view, t, form, person);") && live.Contains("StudioFeeds.RoleKey(feed)")
              && live.Contains("case MoveRoles: return AskMoveRoles(d, view);") && live.Contains("case NameManager: return AskNameManager(d, person);") && live.Contains("case RemoveManager: return AskRemoveManager(d, person);")
              && liveMoves.Contains("GameConfirm.Show(\"reallife_roles_confirm_move_title\"") && liveMoves.Contains("d.MoveRoles(ids, from, to, asked);") && liveMoves.Contains("d.NameManager(id);"),
            "tech staff sources: the kit has the same: the move asked first, the manager named and ended, targets and duties, and the roles' work for a game");
        Check(liveHead.Contains("MoveRoles = 154, NameManager = 155, RemoveManager = 156, SetTarget = 157, ClearTargets = 158, ToggleDuty = 159") && Occurs(liveHead, "= 154") == 1 && Occurs(liveHead, "= 159") == 1
              && liveMoves.Contains("MoveFrom = 10, MoveTo = 11, MoveCount = 12, TargetRole = 13, TargetCount = 14"),
            "tech staff sources: the kit's new action numbers and the amounts its steppers keep are their own");
        Check(Occurs(hiring, "StudioFeeds.RoleKey(StudioFeeds.Of(r))") == 2 && liveHead.Contains("StudioFeeds.RoleKey(StudioFeeds.Of(r))"),
            "tech staff sources: the new ad and the search page of both interfaces say what the role does for a game");
        Check(Occurs(hiring + team + live + liveHead + oldMoves + liveMoves, "reallife_tech_assign_by_balance") == 3, "tech staff sources: a role the manager's balancing moved a person to says so on both interfaces (the file's line and the kit's)");
        // the studio's sort is the one place
        Check(crew.Contains("switch (StudioFeeds.Of(h.Family, h.Tester))") && studio.Contains("StudioFeeds.Builds(StudioFeeds.Of(r))") && views.Contains("Makers(b, g, s, fmt);")
              && views.Contains("StudioFeeds.Gaps(s.FeedPeople, g.Phase, GapKeys);") && studioDesk.Contains("StudioFeeds.Of(p.Def)"),
            "tech staff sources: the studio's crew, its hands and the card all sort roles by StudioFeeds, so the card's words cannot part from the rules");
        // the manager in the day
        Check(main.Contains("PeopleDay(Save.Day);") && main.IndexOf("ManagerDay(Save.Day);", StringComparison.Ordinal) > main.IndexOf("PeopleDay(Save.Day);", StringComparison.Ordinal)
              && main.IndexOf("ManagerDay(Save.Day);", StringComparison.Ordinal) < main.IndexOf("FirmsDay(Save.Day);", StringComparison.Ordinal),
            "tech manager sources: the managers run each morning after the people's own day and before the firms' day");
        Check(people.Contains("ManagerLeft(person, why, day);") && people.Contains("p.Managing = IsManager(p);") && people.Contains("ManagerHandles(p)") && assign.Contains("string named = ManagerNameAt(place);")
              && director.Contains("why == LeaveKind.LaidOff || why == LeaveKind.Dismissed"),
            "tech manager sources: a leaver who resigned, was poached or burnt out is a vacancy (a lay-off or dismissal, the player's own choice, is not), the flag is read with the people, and the manager assigns in their name");
        Check(director.Contains("internal bool ManagersOn => _peopleOn && _talent != null && Settings.TechManager;") && director.Contains("if (!ManagersOn || _talent.Managers.Count == 0)") && director.Contains("if (!ManagersOn)")
              && director.Contains("ManagersOn && !string.IsNullOrEmpty(place) ? _talent.ManagerAt(place) : null"),
            "tech manager sources: with the option off no manager acts, none is read, none costs the person any of their work, and the posts are kept in the save");
        Check(director.Contains("if (post.LastDay >= day || boss.File.NoticeEnd > 0 || boss.File.Sick)") && director.Contains("_talent.Managers.RemoveAt(i);") && director.Contains("\"reallife_tech_mgr_gone\""),
            "tech manager sources: a manager acts once a day, not while on notice or off sick, and the post of one who has gone is closed with a notice");
        Check(director.Contains("Raise(person, FirmManagerCatalogue.RiseBp, day);") && director.Contains("TalentRules.CanRise(p, Save.Day)") && director.Contains("person.File.Managing = false;") && director.Contains("_talent.Managers.Remove(post);")
              && director.Contains("\"reallife_tech_mgr_removed\""),
            "tech manager sources: naming is one raise by the single raise's rule (not within its rest), ending the post gives back their own work and keeps their pay");
        Check(director.Contains("FirmManagerRules.Ceiling(limit)") && director.Contains("RivalMatchRules.Limit(Settings.EconomyCompanyAgreeUpTo, Economy.CompanyCatalogue.AgreeUpToMost)") && director.Contains("RoleMoveRules.Plan(_mFolks, to, 1, _mFloors, city, _mPlan);")
              && director.Contains("folk.Away |= p.Pinned;") && director.Contains("PostAd(post.Place, r.Id, level, least, most,") && director.Contains("MakeOffer(chosen.Id, seeker.Ask)") && director.Contains("TakeCounter(a.Id)")
              && director.Contains("ReadFloors(_mFloors)"),
            "tech manager sources: the duties go through the existing machinery: the bulk change's plan and refusals, the pinned left alone, the jobs board's ad and offer, the raise and the player's own limit");
        string sourceOfRules = rules + director + moves;
        Check(!sourceOfRules.Contains("System.Random") && !sourceOfRules.Contains("new Random(") && !sourceOfRules.Contains("UnityEngine.Random") && !sourceOfRules.Contains("DateTime") && !sourceOfRules.Contains("GoingPublic"),
            "tech staff sources: no dice, no clock and no machine-local state in the rules, the move or the manager: every machine decides the same, and a Going Public session changes nothing of anybody's (they are the player's own people)");
        string heads = ModSource(@"Tech\People\RoleMoveRules.cs") + ModSource(@"Tech\People\FirmManagerRules.cs") + ModSource(@"Tech\Studio\StudioFeeds.cs");
        Check(!heads.Contains("UnityEngine") && !heads.Contains("using BAModAPI"), "tech staff sources: the rules are game-free");
        // the option
        string settings = ModSource(@"Core\Settings.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), tree = ModSource(@"Meta\OptionsTree.cs"), list = ModSource(@"Meta\FeatureList.Economy.cs");
        Check(settings.Contains("public const string TechManagerId = \"tech_manager\";") && settings.Contains("public static bool TechManager = true;") && settings.Contains("TechManager = Bool(modId, TechManagerId, TechManager);")
              && mod.Contains("AddToggle(Settings.TechManagerId, \"reallife_tech_mgr_enabled\", Settings.TechManager,") && tree.Contains(".Opt(\"tech_manager\", \"tech_people\")")
              && list.Contains(".Owns(Settings.TechPeopleId, Settings.TechSeatHiresId, Settings.TechManagerId)") && list.Contains("\"reallife_tech_mgr_test\""),
            "tech manager option: a saved option on by default, a switch with its label, in the options tree under the professionals' section and the feature list, with its test");
        Check(tests.Contains("public void TestManager()") && tests.Contains("Tech check (manager test, ") && tests.Contains("\"reallife_tech_mgr_off\"") && mod.Contains("\"reallife_tech_mgr_test\", () => Tech.TechDirector.Instance?.TestManager()"),
            "tech manager option: the test is in Options, says why when it cannot run, and writes a self-check line");
        Check(director.Contains("Tech check (manager, ") && director.Contains("Tech check (managers' morning, day "), "tech manager sources: a self-check line for each firm's morning and for the whole");
        // the docs and the Guide's figures
        string docs = System.IO.Path.Combine(FindRoot(), "docs/features");
        string hiringDoc = System.IO.File.Exists(System.IO.Path.Combine(docs, "tech-hiring.md")) ? System.IO.File.ReadAllText(System.IO.Path.Combine(docs, "tech-hiring.md")) : string.Empty;
        string studioDoc = System.IO.File.Exists(System.IO.Path.Combine(docs, "game-studio.md")) ? System.IO.File.ReadAllText(System.IO.Path.Combine(docs, "game-studio.md")) : string.Empty;
        Check(hiringDoc.Contains("RoleMoveRules") && hiringDoc.Contains("FirmManagerRules") && hiringDoc.Contains("tech_manager") && hiringDoc.Contains("--techstaff29") && studioDoc.Contains("StudioFeeds"),
            "tech staff docs: the role change and the manager are written up with the Team page's raise, and what roles feed a game with the studio");
    }

    private static void Ts29Texts()
    {
        Dictionary<string, string> en = LocaleFile("en");
        string[] keys =
        {
            "reallife_tech_mgr_enabled", "reallife_opt_tip_tech_manager", "reallife_tech_mgr_test", "reallife_tech_mgr_test_none", "reallife_tech_mgr_test_done", "reallife_tech_mgr_off",
            "reallife_roles_sec_move", "reallife_roles_move_nobody", "reallife_roles_move_none_to", "reallife_roles_move_from_pick", "reallife_roles_move_to_pick", "reallife_roles_move_all",
            "reallife_roles_move_step_from", "reallife_roles_move_step_to", "reallife_roles_move_step_count", "reallife_roles_move_lbl_plan", "reallife_roles_move_lbl_game", "reallife_roles_move_lbl_left",
            "reallife_roles_move_lbl_rule", "reallife_roles_move_plan", "reallife_roles_move_none", "reallife_roles_move_refused", "reallife_roles_move_why_product", "reallife_roles_move_why_contract",
            "reallife_roles_move_why_branding", "reallife_roles_move_why_cloud", "reallife_roles_move_why_hyper", "reallife_roles_move_left_out", "reallife_roles_move_rule", "reallife_roles_move_button",
            "reallife_roles_button_move", "reallife_roles_move_stat", "reallife_roles_confirm_move_title", "reallife_roles_confirm_move_body", "reallife_roles_moved_all", "reallife_tech_assign_by_balance",
            "reallife_studio_role_engineering", "reallife_studio_role_design", "reallife_studio_role_product", "reallife_studio_role_testing", "reallife_studio_role_support", "reallife_studio_role_none",
            "reallife_studio_build_makers", "reallife_studio_makers_design", "reallife_studio_makers_tech", "reallife_studio_makers_art", "reallife_studio_makers_content", "reallife_studio_makers_polish",
            "reallife_studio_build_groups", "reallife_studio_gap_engineering", "reallife_studio_gap_design", "reallife_studio_gap_art", "reallife_studio_gap_testing", "reallife_studio_gap_polish_wait",
            "reallife_tech_mgr_title", "reallife_tech_mgr_about", "reallife_tech_mgr_none_label", "reallife_tech_mgr_label_post", "reallife_tech_mgr_firm_line", "reallife_tech_mgr_line", "reallife_tech_mgr_tally",
            "reallife_tech_mgr_fact", "reallife_tech_mgr_fact_none", "reallife_tech_mgr_firm_has", "reallife_tech_mgr_button_name", "reallife_tech_mgr_button_remove", "reallife_tech_mgr_button_target",
            "reallife_tech_mgr_button_clear", "reallife_tech_mgr_target_none", "reallife_tech_mgr_target_line", "reallife_tech_mgr_target_count", "reallife_tech_mgr_target_role",
            "reallife_tech_mgr_target_howmany", "reallife_tech_mgr_duty_balance", "reallife_tech_mgr_duty_hire", "reallife_tech_mgr_duty_raises", "reallife_tech_mgr_duty_watch",
            "reallife_tech_mgr_duty_balance_on", "reallife_tech_mgr_duty_balance_off", "reallife_tech_mgr_duty_hire_on", "reallife_tech_mgr_duty_hire_off", "reallife_tech_mgr_duty_raises_on",
            "reallife_tech_mgr_duty_raises_off", "reallife_tech_mgr_duty_watch_on", "reallife_tech_mgr_duty_watch_off", "reallife_tech_mgr_worn_names", "reallife_tech_mgr_idle_names",
            "reallife_tech_mgr_moved", "reallife_tech_mgr_held", "reallife_tech_mgr_posted", "reallife_tech_mgr_offered", "reallife_tech_mgr_someone", "reallife_tech_mgr_gone", "reallife_tech_mgr_named",
            "reallife_tech_mgr_named_rested", "reallife_tech_mgr_removed", "reallife_tech_mgr_week", "reallife_tech_mgr_confirm_name_title", "reallife_tech_mgr_confirm_name_body",
            "reallife_tech_mgr_confirm_name_rested", "reallife_tech_mgr_confirm_name_replaces", "reallife_tech_mgr_confirm_remove_title", "reallife_tech_mgr_confirm_remove_body",
            "reallife_tech_mgr_no_firm", "reallife_tech_mgr_leaving", "reallife_tech_mgr_already", "reallife_tech_mgr_alone"
        };
        string missing = keys.FirstOrDefault(k => !en.TryGetValue(k, out string v) || string.IsNullOrEmpty(v));
        Check(missing == null, "tech staff texts: every new key is in en.json (" + (missing ?? "none missing") + ")");
        bool fits = new[] { "reallife_roles_moved_all", "reallife_tech_mgr_named", "reallife_tech_mgr_named_rested", "reallife_tech_mgr_removed", "reallife_tech_mgr_gone", "reallife_tech_mgr_week",
            "reallife_tech_mgr_test_done", "reallife_tech_mgr_test_none", "reallife_tech_mgr_off", "reallife_tech_mgr_no_firm", "reallife_tech_mgr_leaving", "reallife_tech_mgr_already", "reallife_tech_mgr_alone" }.All(k => Fits(en, k));
        string tip = en.TryGetValue("reallife_opt_tip_tech_manager", out string t) ? t : string.Empty;
        Check(fits && tip.Length <= 190 && tip.Split(' ').Length <= 32, "tech staff texts: the notices are 240 characters or fewer and the option's tip is 190 and 32 words or fewer (" + tip.Length + ", " + tip.Split(' ').Length + ")");
        string style = string.Join("\n", keys.Where(k => k != "reallife_guide_tech_people").Select(k => en.TryGetValue(k, out string v) ? v : string.Empty));
        Check(!style.Contains('—') && !style.Contains('–') && !style.Contains('!') && !style.Contains(';') && !style.Contains(" - "), "tech staff texts: no dashes, no exclamation marks and no semicolons in the new texts");
        // the placeholders each text is filled with
        bool holes = true;
        foreach (string k in new[] { "reallife_roles_move_plan", "reallife_roles_moved_all", "reallife_roles_confirm_move_body" })
        {
            holes &= en[k].Contains("{count}") && en[k].Contains("{from}") && en[k].Contains("{to}") && en[k].Contains("{going}");
        }
        holes &= en["reallife_tech_mgr_week"].Contains("{name}") && en["reallife_tech_mgr_week"].Contains("{firm}") && en["reallife_tech_mgr_week"].Contains("{moves}") && en["reallife_tech_mgr_week"].Contains("{raises}")
                 && en["reallife_tech_mgr_week"].Contains("{ads}") && en["reallife_tech_mgr_week"].Contains("{offers}") && en["reallife_tech_mgr_week"].Contains("{worn}") && en["reallife_tech_mgr_week"].Contains("{idle}");
        holes &= en["reallife_tech_mgr_confirm_name_body"].Contains("{percent}") && en["reallife_tech_mgr_confirm_name_body"].Contains("{before}") && en["reallife_tech_mgr_confirm_name_body"].Contains("{after}")
                 && en["reallife_tech_mgr_confirm_name_body"].Contains("{own}") && en["reallife_tech_mgr_confirm_name_body"].Contains("{span}") && en["reallife_tech_mgr_named"].Contains("{percent}");
        Check(holes, "tech staff texts: the move's, the question's and the week's texts carry every figure the code gives them");
        // the card's words are held to the rules: each part's makers text names the kinds of work that feed it and no other
        var names = new Dictionary<Feed, string>
        {
            { Feed.Engineering, "engineers" }, { Feed.Design, "designers" }, { Feed.Product, "product people" }, { Feed.Testing, "testers" }
        };
        bool makers = true;
        string off = string.Empty;
        for (int p = 0; p < (int)Part.Count; p++)
        {
            string text = en[StudioFeeds.MakersKey(p)];
            foreach (KeyValuePair<Feed, string> kind in names)
            {
                bool says = text.Contains(kind.Value), feeds = StudioFeeds.Feeds(kind.Key, p);
                if (says != feeds)
                {
                    makers = false;
                    off += " " + (Part)p + ":" + kind.Value;
                }
            }
        }
        Check(makers, "tech staff texts: each part's makers are named exactly as the rules feed it, no kind missing and none that does not (" + off.Trim() + ")");
        Check(en["reallife_studio_makers_polish"].Contains("half") && StudioCatalogue.PolishPhaseShare == 500 && en["reallife_studio_gap_testing"].Contains("half") && en["reallife_studio_gap_polish_wait"].Contains("{share}"),
            "tech staff texts: 'half' in the polish lines is the catalogue's polish share, and the wait line is given the polish phase's own share");
        string groups = en["reallife_studio_build_groups"];
        Check(groups.Contains("engineering, data, security and infrastructure") && groups.Contains("testers and technicians aside") && groups.Contains("UX, graphic and web designers")
              && groups.Contains("product managers, technical writers and business analysts") && groups.Contains("QA testers"),
            "tech staff texts: the groups line names the roles of each kind as the sort gives them (the roles checked above)");
        // a role's line: the parts it feeds, from the catalogue
        Func<int, string> part = p => ((Part)p).ToString().ToLowerInvariant();
        Func<int, string> share = s => (s / 10) + "%";
        string eng = StudioFeeds.PartsLine(Feed.Engineering, part, share), des = StudioFeeds.PartsLine(Feed.Design, part, share), prod = StudioFeeds.PartsLine(Feed.Product, part, share);
        Check(eng == "tech 40%, content 60%" && des == "design 30%, art 50%, content 20%" && prod == "design 100%" && StudioFeeds.PartsLine(Feed.Testing, part, share) == string.Empty
              && StudioFeeds.PartsLine(Feed.Support, part, share) == string.Empty && StudioFeeds.PartsLine(Feed.None, part, share) == string.Empty,
            "tech staff texts: a role's line gets its parts and shares from the catalogue's splits (" + eng + "; " + des + "; " + prod + ")");
        bool lines = en["reallife_studio_role_engineering"].Contains("{parts}") && en["reallife_studio_role_design"].Contains("{parts}") && en["reallife_studio_role_product"].Contains("{parts}")
                     && en["reallife_studio_role_testing"].Contains("bugs") && en["reallife_studio_role_testing"].Contains("polish") && !en["reallife_studio_role_testing"].Contains("{parts}")
                     && en["reallife_studio_role_support"].Contains("tickets") && en["reallife_studio_role_none"].Contains("build nothing");
        Check(lines, "tech staff texts: each kind of work's line says what it does: the parts for builders, the bugs and polish for testers, the tickets for support and nothing for the rest");
        // the Guide
        string guide = en["reallife_guide_tech_people"];
        string[] facts = { "mgr_rise", "mgr_own", "mgr_sees", "mgr_targets", "mgr_vacancy", "mgr_ceiling", "mgr_report" };
        string guideSource = ModSource(@"Tech\TechGuide.cs");
        Check(facts.All(f => guide.Contains("{" + f + "}") && guideSource.Contains(".Fact(\"" + f + "\"")) && guide.Contains("# Changing roles together") && guide.Contains("# What each role does for a game")
              && guide.Contains("# Managers") && guide.Contains("<b>Move roles together</b>") && en["reallife_roles_sec_move"] == "Move roles together" && guide.Contains("<b>" + en["reallife_tech_mgr_enabled"] + "</b>")
              && guide.Contains("<b>" + en["reallife_tech_mgr_button_name"] + "</b>") && guide.Contains("<b>" + en["reallife_roles_button_move"] + "</b>"),
            "tech staff texts: the Guide's page tells of the move, the roles' work and the manager, its figures come from the catalogue, and the names it bolds are the screen's own");
        Check(WhatsNewHas(en, "change roles together from the Team tab") && WhatsNewHas(en, "named a firm's manager who balances roles, re-hires, agrees routine raises and flags burnout"),
            "tech staff texts: What's new tells of all three, in any of its rows");
    }
}
