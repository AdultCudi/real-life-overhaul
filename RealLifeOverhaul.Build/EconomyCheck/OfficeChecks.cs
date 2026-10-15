using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;

// Running for office (Scripts/Civic/OfficeRules.cs, OfficeState.cs, and what it added to CivicState.cs): when papers may be
// filed and who may file them, the law on donors and the odds of money taken against it coming out, the vote model (shares,
// a district's shares from the city's, turnout, votes), what Standing, money, the economy and a district of one's own are worth
// to it, doors, rallies, the debate and attacks, the council's odds and conflicts of interest, the city's budget, approval in
// office and recall, the polls moving day by day, and the save's lines (the player's own, and the government's new fields
// read from an older line).
internal static partial class Program
{
    private static void Office()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        OfficeCalendar();
        OfficeWhoMayStand();
        OfficeDonors();
        OfficeVoteModel();
        OfficeTerms();
        OfficeDoorsRalliesDebate();
        OfficeCouncil();
        OfficeBudget();
        OfficeApproval();
        OfficePolls();
        OfficeSaved();
        OfficeGovLine();
        OfficeWords();
        Console.WriteLine("  Running for office: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static Candidate OfficeCandidate(int lead, params int[] planks)
    {
        var c = new Candidate { Lead = lead, Slogan = lead * 3, Party = 0 };
        for (int i = 0; i < planks.Length && i < c.Platform.Length; i++)
        {
            c.Platform[i] = planks[i];
        }
        c.Planks = Math.Min(planks.Length, c.Platform.Length);
        return c;
    }

    private static void OfficeCalendar()
    {
        const int ed = 100, cd = CivicCatalogue.CampaignDays;
        Check(OfficeRules.NominationsOpenOn(ed - OfficeRules.NominationsOpen, ed, false, cd) && !OfficeRules.NominationsOpenOn(ed - OfficeRules.NominationsOpen - 1, ed, false, cd),
            "office: nominations open 14 days before election day, not before");
        Check(OfficeRules.NominationsOpenOn(ed - 7, ed, true, cd) && OfficeRules.NominationsOpenOn(ed - 5, ed, true, cd) && !OfficeRules.NominationsOpenOn(ed - 4, ed, true, cd),
            "office: papers may still be filed in the campaign's first days (to its second day's end), not later");
        Check(OfficeRules.NominationsClose(ed, cd) == ed - 5, "office: the last day to file is the campaign's second day (" + OfficeRules.NominationsClose(ed, cd) + ")");
        Check(!OfficeRules.NominationsOpenOn(ed, ed, true, cd) && !OfficeRules.NominationsOpenOn(ed + 1, ed, false, cd) && !OfficeRules.NominationsOpenOn(5, 0, false, cd),
            "office: never on election day, after it, or with no election set");
        bool closeInside = true;
        for (int e = 20; e < 200; e += 7)
        {
            int close = OfficeRules.NominationsClose(e, cd);
            closeInside &= OfficeRules.NominationsOpenOn(close, e, true, cd) && !OfficeRules.NominationsOpenOn(close + 1, e, true, cd);
        }
        Check(closeInside, "office: the closing day is the last day open, whatever the election day");
    }

    private static void OfficeWhoMayStand()
    {
        Check(OfficeRules.MayStand(true, false, false, false, true, false) == OfficeRefusal.None, "office: a resident with a clean record may stand while nominations are open");
        Check(OfficeRules.MayStand(false, false, false, false, true, false) == OfficeRefusal.NoHome, "office: nobody without a home in the city");
        Check(OfficeRules.MayStand(true, true, false, false, true, false) == OfficeRefusal.Conviction, "office: not with a conviction that counts");
        Check(OfficeRules.MayStand(true, false, true, false, true, false) == OfficeRefusal.CaseOpen, "office: not with a case before the court");
        Check(OfficeRules.MayStand(true, false, false, false, false, false) == OfficeRefusal.Closed, "office: not while nominations are closed");
        Check(OfficeRules.MayStand(true, false, false, true, true, false) == OfficeRefusal.Session && OfficeRules.MayStand(true, false, false, false, true, true) == OfficeRefusal.Standing,
            "office: not in a Going Public session, and not twice in one election");
        int allowed = 0;
        for (int bits = 0; bits < 64; bits++)
        {
            allowed += OfficeRules.MayStand((bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0, (bits & 8) != 0, (bits & 16) != 0, (bits & 32) != 0) == OfficeRefusal.None ? 1 : 0;
        }
        Check(allowed == 1, "office: of the 64 cases exactly one may stand (" + allowed + ")");

        var empty = new Candidate { Party = CivicCatalogue.Independent };
        Check(OfficeRules.MayFile(empty, OfficeRace.Council, 999, false, true, true) == OfficeRefusal.NoPlatform, "office: papers need a platform");
        Candidate business = OfficeCandidate((int)Issue.Business, CivicCatalogue.IndexOf(CivicCatalogue.Find(Lever.BusinessTax, -1)));
        business.Party = 2;   // party 2 stands for business (PartyIssue: party % issues)
        Check(OfficeRules.PartyIssue(2) == (int)Issue.Business && OfficeRules.PartyIssue(CivicCatalogue.Independent) == -1, "office: a party has its issue; an independent none");
        Check(OfficeRules.MayFile(business, OfficeRace.Council, OfficeRules.SignaturesCouncil, false, false, true) == OfficeRefusal.None, "office: the party's issue in the platform and the signatures: filed");
        business.Party = 0;   // safety
        Check(OfficeRules.MayFile(business, OfficeRace.Council, OfficeRules.SignaturesCouncil, false, false, true) == OfficeRefusal.PartyLine, "office: a party wants a plank on its own issue");
        business.Party = CivicCatalogue.Independent;
        Check(OfficeRules.MayFile(business, OfficeRace.Mayor, OfficeRules.SignaturesCouncil, false, false, true) == OfficeRefusal.Requirements, "office: the council's signatures are too few for mayor, without the deposit");
        Check(OfficeRules.MayFile(business, OfficeRace.Mayor, 0, false, true, true) == OfficeRefusal.None && OfficeRules.MayFile(business, OfficeRace.Mayor, 0, false, true, false) == OfficeRefusal.Broke,
            "office: the deposit instead of the signatures, if it can be paid");
        Check(OfficeRules.MayFile(business, OfficeRace.Mayor, 0, true, false, true) == OfficeRefusal.None, "office: somebody standing again for their own office needs neither");
        Check(OfficeRules.SignaturesNeeded(OfficeRace.Mayor) > OfficeRules.SignaturesNeeded(OfficeRace.Council) && OfficeRules.Deposit(OfficeRace.Mayor) > OfficeRules.Deposit(OfficeRace.Council),
            "office: standing for mayor asks more than for the council");
        Check(OfficeRules.PartyGrant(OfficeRace.Mayor, 3) == OfficeRules.GrantMayor && OfficeRules.PartyGrant(OfficeRace.Council, CivicCatalogue.Independent) == 0, "office: a party's grant; none for an independent");
        Check(OfficeRules.SheetSignatures(0, 0) == 0 && OfficeRules.SheetSignatures(50, 100) == OfficeRules.SheetsPerBusiness * OfficeRules.SheetsBusinesses + 50,
            "office: the businesses' sheets count ten businesses at most");
        Check(OfficeRules.SheetSignatures(OfficeRules.SheetsBusinesses, 100) < OfficeRules.SignaturesMayor, "office: the sheets alone are not enough to stand for mayor (doors or the deposit are needed)");
    }

    private static void OfficeDonors()
    {
        int limit = OfficeRules.DonorLimitMayor;
        Check(OfficeRules.Legal(OfficeRules.OfferPerson, limit, limit) && OfficeRules.Legal(OfficeRules.OfferBusiness, 50, limit), "office: a person or a business within the limit is legal");
        Check(!OfficeRules.Legal(OfficeRules.OfferPerson, limit + 1, limit) && !OfficeRules.Legal(OfficeRules.OfferCash, 10, limit) && !OfficeRules.Legal(OfficeRules.OfferStraw, 10, limit)
              && !OfficeRules.Legal(OfficeRules.OfferOverLimit, 10, limit), "office: over the limit, cash from nobody, or straw donors are against the law");
        int offers = 0, illegal = 0, lowKnown = 0, highKnown = 0;
        bool amountsOk = true, legalInLimit = true, kinds = true;
        var seen = new HashSet<int>();
        for (int i = 0; i < 20000; i++)
        {
            int r1 = (i * 7919) % 1000, r2 = (i * 104729 + 13) % 1000;
            if (OfficeRules.Offer(r1, r2, 0, 0.2f, limit, out int kind, out int amount))
            {
                offers++;
                lowKnown++;
                seen.Add(kind);
                amountsOk &= amount >= 50 && amount <= limit * 7;
                bool legal = OfficeRules.Legal(kind, amount, limit);
                illegal += legal ? 0 : 1;
                legalInLimit &= kind > OfficeRules.OfferBusiness || legal;
                kinds &= kind >= OfficeRules.OfferPerson && kind <= OfficeRules.OfferStraw;
            }
            if (OfficeRules.Offer(r1, r2, 90, 0.4f, limit, out _, out _))
            {
                highKnown++;
            }
        }
        Check(amountsOk && kinds && seen.Count == 5, "office: every kind of offer comes, each for a sensible amount (" + seen.Count + " kinds)");
        Check(legalInLimit, "office: an offer from a person or a business is always within the limit");
        Check(illegal > offers / 10 && illegal < offers / 2, "office: some offers are against the law, most are not (" + illegal + " of " + offers + ")");
        Check(highKnown > lowKnown && lowKnown > 3000, "office: somebody well known and well placed gets more offers (" + highKnown + " against " + lowKnown + ")");
        Check(OfficeRules.DirtyOdds(0, limit, 100) == 0, "office: nothing comes out of nothing");
        bool rising = true;
        int last = 0;
        for (int dirty = 100; dirty <= limit * 10; dirty += 100)
        {
            int odds = OfficeRules.DirtyOdds(dirty, limit, 30);
            rising &= odds >= last && odds <= 120;
            last = odds;
        }
        Check(rising && OfficeRules.DirtyOdds(limit, limit, 100) > OfficeRules.DirtyOdds(limit, limit, 0), "office: the more taken (and the better known), the likelier it comes out, never past 12% a day");
    }

    private static void OfficeVoteModel()
    {
        var scores = new float[4];
        var shares = new float[4];
        var shifted = new float[4];
        var rng = new Random(7);
        bool sum = true, positive = true, order = true, shift = true;
        for (int t = 0; t < 2000; t++)
        {
            int n = 2 + t % 3;
            for (int i = 0; i < 4; i++)
            {
                scores[i] = (float)(rng.NextDouble() * 3.0 - 1.5);
            }
            OfficeRules.Shares(scores, n, shares);
            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                total += shares[i];
                positive &= shares[i] > 0f;
                for (int j = 0; j < n; j++)
                {
                    order &= scores[i] <= scores[j] || shares[i] >= shares[j];
                }
            }
            sum &= Math.Abs(total - 1f) < 1e-4f && shares[3] == 0f || n == 4 && Math.Abs(total - 1f) < 1e-4f;
            for (int i = 0; i < 4; i++)
            {
                scores[i] += 5f;
            }
            OfficeRules.Shares(scores, n, shifted);
            for (int i = 0; i < n; i++)
            {
                shift &= Math.Abs(shifted[i] - shares[i]) < 1e-4f;
            }
        }
        Check(sum && positive, "office: shares are all above nothing and add up to one");
        Check(order, "office: a better score never has a smaller share");
        Check(shift, "office: only the differences between scores count");

        float[] final = { 0.4f, 0.35f, 0.25f, 0f }, cityFit = { 1f, 1f, 1f, 0f }, districtFit = { 1f, 1f, 1f, 0f }, local = new float[4], into = new float[4];
        OfficeRules.DistrictShares(final, cityFit, districtFit, local, 3, into);
        Check(Math.Abs(into[0] - 0.4f) < 1e-4f && Math.Abs(into[2] - 0.25f) < 1e-4f && into[3] == 0f, "office: a district that thinks as the city does votes as the city");
        local[1] = 0.3f;
        OfficeRules.DistrictShares(final, cityFit, districtFit, local, 3, into);
        Check(into[1] > 0.35f && Math.Abs(into[0] + into[1] + into[2] - 1f) < 1e-4f, "office: a candidate of the district's own does better there, and the shares still add up");
        districtFit[0] = 1.3f;
        OfficeRules.DistrictShares(final, cityFit, districtFit, new float[4], 3, into);
        Check(into[0] > 0.4f, "office: a platform that fits a district better than the city wins more of it");

        bool turnout = true;
        for (int i = 0; i <= 100; i++)
        {
            float t1 = OfficeRules.Turnout(i / 100f, 0), t2 = OfficeRules.Turnout(i / 100f, 5000);
            turnout &= t1 >= 0.34f && t1 <= 0.56f + 1e-5f && t2 > t1 && t2 <= t1 + 0.06f + 1e-5f;
        }
        Check(turnout, "office: turnout is 34 to 56%, six points more at most where the campaign has knocked");
        Check(OfficeRules.Votes(10000, 0.5f, 0.4f) == 2000 && OfficeRules.Votes(10000, 2f, 2f) == 10000 && OfficeRules.Votes(-5, 0.5f, 0.5f) == 0, "office: votes are the electorate by turnout by share, never more than the electorate");
        Candidate none = new Candidate();
        Check(OfficeRules.Fit(none, null) == 1f, "office: with no weights a platform fits as an ordinary one");
        Candidate safety = OfficeCandidate((int)Issue.Safety, CivicCatalogue.IndexOf(CivicCatalogue.Find(Lever.Policing, 1)));
        Check(OfficeRules.Fit(safety, new float[] { 2f, 1f, 1f, 1f, 1f, 1f }) > OfficeRules.Fit(safety, new float[] { 1f, 1f, 1f, 1f, 1f, 1f }), "office: a platform on what matters now fits better");
    }

    private static void OfficeTerms()
    {
        bool bounded = true;
        for (int r = -50; r <= 150; r += 10)
        {
            for (int p = -150; p <= 150; p += 25)
            {
                for (int i = -20; i <= 120; i += 20)
                {
                    float t = OfficeRules.StandingTerm(r, p, i);
                    bounded &= t >= -0.4f && t <= 0.4f;
                }
            }
        }
        Check(bounded && OfficeRules.StandingTerm(90, 50, 70) > OfficeRules.StandingTerm(10, 50, 70) && OfficeRules.StandingTerm(50, -80, 50) < OfficeRules.StandingTerm(50, 80, 50),
            "office: being known and liked helps, never by more than 0.4 either way");
        Check(Math.Abs(OfficeRules.SpendingTerm(5000, 5000, 1000)) < 1e-6f && OfficeRules.SpendingTerm(8000, 1000, 1000) > 0f
              && Math.Abs(OfficeRules.SpendingTerm(8000, 1000, 1000) + OfficeRules.SpendingTerm(1000, 8000, 1000)) < 1e-5f && OfficeRules.SpendingTerm(100000000, 0, 1000) <= 0.3f,
            "office: money spent counts against the other side's, both ways the same, a third of a point at most");
        Check(OfficeRules.SpendingTerm(4000, 0, 1000) - OfficeRules.SpendingTerm(2000, 0, 1000) < OfficeRules.SpendingTerm(2000, 0, 1000), "office: each dollar buys less than the one before");
        Check(OfficeRules.EconomyTerm(0, true) > 0f && OfficeRules.EconomyTerm(3, true) < 0f && OfficeRules.EconomyTerm(3, false) > 0f && OfficeRules.EconomyTerm(0, false) < 0f
              && Math.Abs(OfficeRules.EconomyTerm(3, false)) < Math.Abs(OfficeRules.EconomyTerm(3, true)), "office: a boom helps whoever holds office, a recession whoever wants it (by half)");
        Check(OfficeRules.LocalTerm(true, 2, 0.1f) > OfficeRules.LocalTerm(false, 2, 0.1f) && OfficeRules.LocalTerm(false, 99, 9f) <= 0.2f + OfficeRules.MostPush + 1e-6f,
            "office: a home, businesses and doors in a district count there, within bounds");
        Check(OfficeRules.OutsiderTerm(true, 20f) > 0f && OfficeRules.OutsiderTerm(true, 60f) == 0f && OfficeRules.OutsiderTerm(false, 10f) == 0f, "office: an independent gains only while the mayor is unpopular");
    }

    private static void OfficeDoorsRalliesDebate()
    {
        OfficeGain good = OfficeRules.Door(2, OfficeAnswer.Plan, true, true, true, 0);
        OfficeGain bad = OfficeRules.Door(0, OfficeAnswer.Ask, false, false, false, 0);
        Check(good.Tone == 1 && good.Push > 0f && good.Signatures > 0, "office: a friendly door, their worry and a plank on it: support and signatures");
        Check(bad.Tone == -1 && bad.Push < 0f && bad.Signatures == 0 && bad.Donation == 0, "office: a hostile door asked straight away: it costs a little");
        bool bounded = true, signsOnlyFiling = true, giftsOnlyAfter = true;
        for (int mood = 0; mood < 3; mood++)
        {
            for (int a = 0; a <= (int)OfficeAnswer.Ask; a++)
            {
                for (int roll = 0; roll < 1000; roll += 37)
                {
                    for (int f = 0; f < 2; f++)
                    {
                        OfficeGain g = OfficeRules.Door(mood, (OfficeAnswer)a, roll % 2 == 0, roll % 3 == 0, f == 1, roll);
                        bounded &= g.Push >= -0.003f && g.Push <= 0.008f && g.Donation <= 100;
                        signsOnlyFiling &= f == 1 || g.Signatures == 0;
                        giftsOnlyAfter &= f == 0 || g.Donation == 0;
                    }
                }
            }
        }
        Check(bounded && signsOnlyFiling && giftsOnlyAfter, "office: a door is worth little each, signatures only before filing, small gifts only after");
        OfficeGain strong = OfficeRules.Rally(OfficeAnswer.Plan, true, true, 3, 80, 0), weak = OfficeRules.Rally(OfficeAnswer.Plan, false, false, 2, 0, 0);
        Check(strong.Push > weak.Push && strong.Push <= 0.07f && weak.Push >= 0f, "office: a rally on a plank of one's own with a promise does best, within bounds");
        Check(OfficeRules.DebateAnswer(OfficeAnswer.Plan, true, false, false, 0) > OfficeRules.DebateAnswer(OfficeAnswer.Pivot, false, false, false, 0)
              && OfficeRules.DebateAnswer(OfficeAnswer.Pivot, false, false, false, 0) > OfficeRules.DebateAnswer(OfficeAnswer.Dodge, false, false, false, 0),
            "office: at the debate a plan beats a pivot, which beats a dodge");
        Check(OfficeRules.DebateAnswer(OfficeAnswer.Attack, false, true, false, 0) < 0f && OfficeRules.DebateAnswer(OfficeAnswer.Attack, false, false, false, 0) > 0f,
            "office: attacking a rival on what they stand on falls flat");
        Check(OfficeRules.DebateAnswer(OfficeAnswer.Straight, false, false, false, 0) > OfficeRules.DebateAnswer(OfficeAnswer.Straight, false, false, true, 0),
            "office: a plain answer works better to an attack with nothing in it");
        Check(OfficeRules.DebateReach(100) > OfficeRules.DebateReach(0) && OfficeRules.DebateReach(0) > 0f, "office: the better known, the more the debate reaches");
        OfficeRules.Answer(OfficeAnswer.Straight, false, out float mine, out float theirs);
        Check(mine + OfficeRules.AttackHit(false) > 0f && theirs < 0f, "office: rebutting an attack with nothing in it wins more back than it took, and costs them");
        OfficeRules.Answer(OfficeAnswer.Straight, true, out mine, out _);
        Check(mine < 0f && OfficeRules.AttackHit(true) < OfficeRules.AttackHit(false), "office: denying something true costs more; a true attack hurts more");
        OfficeRules.Answer(OfficeAnswer.Dodge, true, out mine, out theirs);
        Check(mine == 0f && theirs == 0f, "office: letting it go changes nothing");
        Check(OfficeRules.AttackAttempt(0) == 0 && OfficeRules.AttackAttempt(2) > OfficeRules.AttackAttempt(6) && OfficeRules.AttackAttempt(6) == OfficeRules.AttackOdds, "office: attacks come more often near the vote, none on the day");
        Candidate c = OfficeCandidate((int)Issue.Business, CivicCatalogue.IndexOf(CivicCatalogue.Find(Lever.BusinessTax, -1)), CivicCatalogue.IndexOf(CivicCatalogue.Find(Lever.Grants, 1)));
        Check(OfficeRules.EndorserFit(0, c, 50) > OfficeRules.EndorserFit(1, c, 50) && OfficeRules.EndorserFit(6, c, 90) > OfficeRules.EndorserFit(6, c, 10),
            "office: a body likes a platform on its issue; the paper's editorial likes integrity");
        Check(OfficeRules.AskOdds(2f, 0f, 100) > OfficeRules.AskOdds(0f, 2f, 0) && OfficeRules.AskOdds(-99f, 99f, 0) == 50 && OfficeRules.AskOdds(99f, -99f, 100) == 900,
            "office: asking for an endorsement works better with the better fit and more influence, never certain either way");
    }

    private static void OfficeCouncil()
    {
        bool bounded = true, rising = true;
        float last = 0f;
        for (int s = 0; s <= 10; s++)
        {
            float odds = OfficeRules.MemberOdds(s / 10f, false, false, false, false, 0);
            rising &= odds >= last;
            last = odds;
            for (int bits = 0; bits < 16; bits++)
            {
                float o = OfficeRules.MemberOdds(s / 10f, (bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0, (bits & 8) != 0, 100);
                bounded &= o >= 0.05f && o <= 0.95f;
            }
        }
        Check(bounded && rising, "office: a member's odds rise with what matters, never certain");
        Check(OfficeRules.MemberOdds(0.5f, false, true, false, false, 0) > OfficeRules.MemberOdds(0.5f, false, false, false, false, 0)
              && OfficeRules.MemberOdds(0.5f, false, false, false, true, 100) > OfficeRules.MemberOdds(0.5f, false, false, false, true, 0),
            "office: the mayor's own party, and the player's influence on a motion they tabled, count");
        Check(OfficeRules.Passes(3, 2) && !OfficeRules.Passes(2, 2) && !OfficeRules.Passes(1, 3), "office: more ayes than noes carries it; a tie does not");
        Check(OfficeRules.Conflicted(Lever.BusinessTax, 1, false) && !OfficeRules.Conflicted(Lever.BusinessTax, 0, false) && OfficeRules.Conflicted(Lever.Rent, 0, true)
              && !OfficeRules.Conflicted(Lever.Policing, 5, true) && !OfficeRules.Conflicted(Lever.Cleaning, 5, true), "office: a conflict of interest is a vote on what the player earns from");
        Check(OfficeRules.UndeclaredOdds(true, 50, true) > OfficeRules.UndeclaredOdds(false, 50, true) && OfficeRules.UndeclaredOdds(false, 50, false) == 2 * OfficeRules.UndeclaredOdds(false, 50, true),
            "office: a deciding vote not declared comes out sooner, and twice as soon with no register filed");
        Check(OfficeRules.ByOrder(Lever.Cleaning) && OfficeRules.ByOrder(Lever.Transit) && !OfficeRules.ByOrder(Lever.BusinessTax) && !OfficeRules.ByOrder(Lever.Rent),
            "office: a mayor may order how the city is kept, not what it charges");
        Check(OfficeRules.Salary(OfficeRace.Mayor) > OfficeRules.Salary(OfficeRace.Council) && OfficeRules.Salary(OfficeRace.None) == 0, "office: a mayor is paid more than a councillor; nobody else");
    }

    private static void OfficeBudget()
    {
        var levers = new int[CivicCatalogue.Levers];
        var levels = new int[4];
        BudgetWeek w = OfficeRules.Week(60000, levers, levels);
        Check(Math.Abs(w.Balance) <= 2 && w.Revenue == 36000, "office: the city as it came balances its budget (" + w.Revenue + " against " + w.Spending + ")");
        levers[(int)Lever.BusinessTax] = 1;
        Check(OfficeRules.Week(60000, levers, levels).Revenue == 39600, "office: a higher business tax brings a tenth more in");
        levers[(int)Lever.BusinessTax] = 0;
        int[] up = { 2, 2, 2, 2 }, down = { -2, -2, -2, -2 };
        Check(OfficeRules.Week(60000, levers, up).Balance < 0 && OfficeRules.Week(60000, levers, down).Balance > 0, "office: spending more runs a deficit, less a surplus");
        Check(OfficeRules.Week(60000, levers, new[] { 9, 9, 9, 9 }).Spending == OfficeRules.Week(60000, levers, up).Spending, "office: a department goes two steps at most");
        float[] weights = { 1f, 1f, 1f, 1f, 1f, 1f };
        Check(OfficeRules.BudgetApproval(up, weights, 100000, 36000) > OfficeRules.BudgetApproval(levels, weights, 100000, 36000), "office: services people see are liked");
        Check(OfficeRules.BudgetApproval(levels, weights, -10000000, 36000) == -10f, "office: a deficit costs approval, ten points at most");
        Check(OfficeRules.HeatScale(0) == 1f && OfficeRules.HeatScale(2) < 1f && OfficeRules.HeatScale(-2) > 1f && OfficeRules.HeatScale(9) == OfficeRules.HeatScale(2),
            "office: more police and streets, less crime and fewer accidents counted, two steps at most");
        var mine = new int[4];
        OfficeRules.CityBudget((int)Issue.Safety, mine);
        Check(mine[0] == 1 && mine[2] == -1, "office: a mayor of the city's own spends more on what they lead on and less elsewhere");
        OfficeRules.CityBudget((int)Issue.Growth, mine);
        Check(mine[0] == 0 && mine[1] == 0 && mine[2] == 0 && mine[3] == 0, "office: and leaves it as it was for an issue no department answers");
        Check(OfficeRules.StartReserve(60000) > 0, "office: the city starts with something in reserve");
    }

    private static void OfficeApproval()
    {
        bool bounded = true;
        for (int a = 0; a <= 100; a += 10)
        {
            for (int s = 0; s < 5; s++)
            {
                float t = OfficeRules.CouncillorTarget(a, s, 4 - s, s * 5 - 10, s, s, s % 4);
                bounded &= t >= 5f && t <= 90f;
            }
        }
        Check(bounded, "office: a councillor's approval heads for 5 to 90");
        Check(OfficeRules.CouncillorTarget(50, 3, 0, 0, 0, 0, 1) > OfficeRules.CouncillorTarget(50, 0, 3, 0, 0, 0, 1)
              && OfficeRules.CouncillorTarget(50, 0, 0, 0, 2, 0, 1) < OfficeRules.CouncillorTarget(50, 0, 0, 0, 0, 0, 1)
              && OfficeRules.CouncillorTarget(50, 0, 0, 0, 0, 3, 1) < OfficeRules.CouncillorTarget(50, 0, 0, 0, 0, 0, 1), "office: promises kept help, scandals and missed meetings hurt");
        float approval = 30f;
        for (int d = 0; d < 60; d++)
        {
            approval = OfficeRules.Approach(approval, 70f, 0f);
        }
        Check(approval > 69f && approval <= 70f && OfficeRules.Approach(50f, 999f, 999f) == 95f, "office: approval moves to its target over weeks, within 3 to 95");
        Check(OfficeRules.RecallKeep(80f, 0f) > 0.5f && OfficeRules.RecallKeep(10f, 0f) < 0.5f && OfficeRules.RecallKeep(10f, 0.3f) > OfficeRules.RecallKeep(10f, 0f),
            "office: a recall keeps the popular and removes the unpopular, and a campaign against it helps");
        Check(OfficeRules.StartApproval(0.6f) > OfficeRules.StartApproval(0.3f) && OfficeRules.StartApproval(0f) >= 40f && OfficeRules.StartApproval(1f) <= 72f,
            "office: a bigger win starts with more approval, 40 to 72");
    }

    private static void OfficePolls()
    {
        // A race of three over a week: each day a third of the way to where the city stands; the shares always add up.
        var scores = new float[] { 0.9f, 0.3f, 0.1f };
        var target = new float[3];
        OfficeRules.Shares(scores, 3, target);
        var polls = new float[] { 0.2f, 0.5f, 0.3f };
        float first = Math.Abs(polls[0] - target[0]);
        bool adds = true;
        for (int d = 0; d < 7; d++)
        {
            for (int i = 0; i < 3; i++)
            {
                polls[i] = OfficeRules.PollStep(polls[i], target[i], 0.34f, (d % 2 == 0 ? 0.01f : -0.01f) * (i - 1));
            }
            OfficeRules.Normalise(polls, 3);
            adds &= Math.Abs(polls[0] + polls[1] + polls[2] - 1f) < 1e-4f && polls[0] > 0f && polls[1] > 0f && polls[2] > 0f;
        }
        Check(adds, "office: the polls add up to one every day");
        Check(Math.Abs(polls[0] - target[0]) < first * 0.2f, "office: in a week the polls come most of the way to where the city stands");
        Check(OfficeRules.PollStep(0f, 0f, 0.34f, -1f) == 0.02f, "office: nobody polls under 2%");
        var flat = new float[] { 0f, 0f, 0f };
        OfficeRules.Normalise(flat, 3);
        Check(Math.Abs(flat[0] - 1f / 3f) < 1e-5f, "office: polls of nothing are shared out evenly");
    }

    private static void OfficeSaved()
    {
        var s = new OfficeState();
        s.Race = OfficeRace.Council;
        s.Filed = true;
        s.ForDay = 77;
        s.District = "ba:neighborhood_midtown";
        s.Me.Party = CivicCatalogue.Independent;
        s.Me.Lead = 3;
        s.Me.Slogan = 10;
        s.Me.Planks = 2;
        s.Me.Platform[0] = 11;
        s.Me.Platform[1] = 24;
        s.Signatures = 61;
        s.Deposit = true;
        s.Fund = 1234;
        s.Raised = 3000;
        s.Spent = 1766;
        s.Dirty = 900;
        s.DirtyDay = 70;
        s.OfferKind = OfficeRules.OfferStraw;
        s.OfferAmount = 4500;
        s.PostersHeld = 7;
        s.PostersUp = 5;
        s.DistrictPush[2] = 0.125f;
        s.DistrictDoors[2] = 44;
        s.Backs[3] = 0;
        s.Backs[6] = 2;
        s.Endorsed = 8;
        s.AttackKind = (int)AttackKind.Money;
        s.AttackTrue = true;
        s.Debate = 1;
        s.DebateGain = 0.05f;
        s.RivalCount = 2;
        s.Rival[1].Party = 4;
        s.Rival[1].Incumbent = true;
        s.RacePolls[0] = 0.41f;
        s.RaceVotes[2] = 1777;
        s.Result = 1;
        s.ResultRace = OfficeRace.Council;
        s.ResultShare = 412;
        s.ResultShown = false;
        s.Holds = OfficeRace.Council;
        s.Seat = 4;
        s.Approval = 47.5f;
        s.Registered = true;
        s.NextMeeting = 84;
        var m = new OfficeMotion { Kind = OfficeMotion.KindBudget, By = OfficeMotion.ByMayor, Day = 80 };
        m.Budget[1] = 2;
        m.Budget[3] = -1;
        s.Agenda.Add(m);
        s.Agenda.Add(new OfficeMotion { Kind = OfficeMotion.KindPolicy, Policy = 5, By = OfficeMotion.ByPlayer, Day = 81, Undeclared = true });
        s.Secrets.Add(new OfficeSecret { Day = 79, Policy = 5, Decisive = true });
        s.Budget[0] = -2;
        s.Reserve = -123456789012L;
        s.ReserveSet = true;
        s.Story(78, "reallife_civic_news_office_attack", "A | name", "x");
        s.Runs = 3;
        s.Wins = 2;
        s.Terms = 1;
        s.Held.Planks = 1;
        s.Held.Platform[0] = 9;
        string line = s.Write();
        var back = new OfficeState();
        Check(back.Read(line) && back.Write() == line, "office: the player's line reads back to exactly the same");
        Check(back.Race == OfficeRace.Council && back.Filed && back.District == "ba:neighborhood_midtown" && back.Me.Party == CivicCatalogue.Independent && back.Me.Platform[1] == 24 && back.Me.Player,
            "office: the run, the district, the party (independent) and the platform come back");
        Check(back.Dirty == 900 && back.OfferKind == OfficeRules.OfferStraw && back.DistrictDoors[2] == 44 && Math.Abs(back.DistrictPush[2] - 0.125f) < 1e-6f && back.Backs[6] == 2 && back.Backs[0] == -1,
            "office: the fund, the offer, the doors, the endorsements come back");
        Check(back.Agenda.Count == 2 && back.Agenda[0].Budget[1] == 2 && back.Agenda[1].Undeclared && back.Secrets.Count == 1 && back.Secrets[0].Decisive && back.Reserve == -123456789012L,
            "office: the agenda, the secrets and the city's reserve come back");
        Check(back.Stories.Count == 1 && back.Stories[0].Name == "A / name" && back.Held.Platform[0] == 9 && back.Holds == OfficeRace.Council && back.Seat == 4,
            "office: a story's names (with no divider in them), the office and its platform come back");
        Check(!new OfficeState().Read("2|" + line.Substring(2)) && !new OfficeState().Read("") && !new OfficeState().Read(null), "office: another version, or nothing, is not read");
        Check(!new OfficeState().Read(line.Substring(0, line.Length / 2)), "office: half a line is not read");
        var odd = new OfficeState();
        Check(odd.Read(line.Replace("|1|4|0|47.5|", "|1|99|0|47.5|")) && odd.Seat == CivicCatalogue.CouncilSeats - 1, "office: a seat out of range is brought back into it");
        bool robust = true;
        var random = new Random(3);
        for (int t = 0; t < 500; t++)
        {
            char[] chars = line.ToCharArray();
            for (int k = 0; k < 3; k++)
            {
                chars[random.Next(chars.Length)] = "0123456789|-x"[random.Next(13)];
            }
            try
            {
                var damaged = new OfficeState();
                damaged.Read(new string(chars));
                robust &= damaged.Agenda.Count <= OfficeState.AgendaMost && damaged.Secrets.Count <= OfficeState.SecretsMost && damaged.Stories.Count <= OfficeState.StoriesMost
                          && damaged.Approval >= 0f && damaged.Approval <= 100f;
            }
            catch (Exception)
            {
                robust = false;
            }
        }
        Check(robust, "office: a damaged line never throws and never reads past its bounds");
        var fresh = new OfficeState();
        Check(!fresh.Running && !fresh.InOffice && fresh.ResultShown && fresh.OfferKind == -1 && fresh.Seat == -1, "office: a new save has no run and no office");
    }

    private static void OfficeGovLine()
    {
        var g = new GovState();
        g.Reset();
        g.Seed = 5;
        g.CandidateCount = 3;
        g.Candidates[2].Party = CivicCatalogue.Independent;
        g.PlayerSlot = 2;
        g.MayorIsPlayer = true;
        g.DistrictsCounted = 2;
        g.DistrictShares[0 * CivicCatalogue.MaxCandidates + 2] = 0.5f;
        g.DistrictShares[1 * CivicCatalogue.MaxCandidates + 0] = 0.25f;
        string line = g.Write();
        var back = new GovState();
        Check(back.Read(line) && back.PlayerSlot == 2 && back.MayorIsPlayer && back.Candidates[2].Player && !back.Candidates[0].Player && back.Mayor.Player,
            "office: the government's line keeps which candidate is the player, and whether the mayor is");
        Check(back.Candidates[2].Party == CivicCatalogue.Independent, "office: an independent stays independent in the government's line");
        Check(back.DistrictsCounted == 2 && Math.Abs(back.DistrictShares[2] - 0.5f) < 1e-6f && Math.Abs(back.DistrictShares[CivicCatalogue.MaxCandidates] - 0.25f) < 1e-6f,
            "office: the count's district shares come back");
        // A line from before running for office ends at the elections voted in: it reads, with nobody the player.
        string[] fields = line.Split('|');
        int extra = 3 + 2 * CivicCatalogue.MaxCandidates;   // slot, mayor, districts, and two districts' shares
        string old = string.Join("|", fields, 0, fields.Length - 1 - extra) + "|";
        var older = new GovState();
        Check(older.Read(old) && older.PlayerSlot == -1 && !older.MayorIsPlayer && older.DistrictsCounted == 0 && !older.Candidates[2].Player,
            "office: an older line reads, with nobody the player and no district shares");
        Check(CivicCatalogue.PartyKey(CivicCatalogue.Independent) == "reallife_civic_party_independent" && CivicCatalogue.PartyKey(0) == CivicCatalogue.Parties[0], "office: an independent has a name of their own");
    }

    private static void OfficeWords()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("office", langs, codes))
        {
            return;
        }
        Dictionary<string, string> en = langs[0];
        string[] keys =
        {
            "reallife_civic_party_independent", "reallife_civic_office_option", "reallife_opt_tip_civic_office",
            "reallife_civic_office_attack_record", "reallife_civic_office_attack_business", "reallife_civic_office_attack_money", "reallife_civic_office_attack_inexperience",
            "reallife_civic_office_attack_absent", "reallife_civic_office_attack_promises",
            "reallife_civic_office_body_0", "reallife_civic_office_body_6", "reallife_civic_office_dept_0", "reallife_civic_office_dept_3",
            "reallife_justice_charge_campaign_finance", "reallife_justice_charge_misconduct", "reallife_fame_cause_30", "reallife_flare_draft_campaign"
        };
        var missing = new List<string>();
        foreach (string k in keys)
        {
            string lacking = LanguagesLacking(langs, codes, k);
            if (lacking.Length > 0)
            {
                missing.Add(k + " (" + lacking + ")");
            }
        }
        Check(missing.Count == 0, "office: the run's texts are in every language with the same {names}" + (missing.Count > 0 ? " (missing " + string.Join(", ", missing) + ")" : string.Empty));
        // What the texts say must be what the rules do: the numbers in them.
        bool numbers = en.TryGetValue("reallife_civic_office_no_window", out string window) && window.Contains(OfficeRules.NominationsOpen + " days") && window.Contains("second day")
                       && en.TryGetValue("reallife_civic_office_rally_hours", out string hours) && hours.Contains(OfficeRules.RallyFrom + ":00") && hours.Contains(OfficeRules.RallyTo + ":00");
        Check(numbers, "office: the nominations' window and the rally's hours in the texts are the rules'");
        bool dept = en.TryGetValue("reallife_civic_office_dept_about_0", out string about) && about.Contains((int)Math.Round(OfficeRules.DepartmentStep * 100) + "%")
                    && about.Contains((int)Math.Round((1f - OfficeRules.HeatScale(1)) * 100) + "%");
        Check(dept, "office: what a step of the budget does, in the text, is the rules'");
        bool guide = en.TryGetValue("reallife_guide_civic", out string g) && g.Contains("{office_open}") && g.Contains("{office_limit_mayor}") && g.Contains(OfficeRules.MeetingHour + ":00")
                     && g.Contains((int)OfficeRules.RecallBelow + "%");
        Check(guide, "office: the Guide's numbers are the rules' (meetings at " + OfficeRules.MeetingHour + ":00, recall under " + OfficeRules.RecallBelow + "%)");
    }
}
