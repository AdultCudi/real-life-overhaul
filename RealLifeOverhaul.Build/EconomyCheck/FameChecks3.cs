using System.Collections.Generic;
using RealLifeOverhaul.Fame;

// Checks for Standing's connections and power (Scripts/Fame/FameCity): what a request costs and does, when one comes and
// lapses, the customers a sponsorship or a partnership brings, evenings, influence and a push on the council, a bribe
// asked, paid and come out, the scandal and what it does, and the city's line in the save.
internal static partial class Program
{
    private static void FameCityChecks()
    {
        // ---- what a request costs
        Check(FameCity.Cost(FameRequestKind.Sponsor, 0) == 500 && FameCity.Cost(FameRequestKind.Sponsor, 100000) == 10000 && FameCity.Cost(FameRequestKind.Sponsor, 999999999) == 20000,
            "Standing: a sponsorship is 10% of a week's income, $500 to $20,000");
        Check(FameCity.Cost(FameRequestKind.Rescue, 50000) == 20000 && FameCity.Cost(FameRequestKind.Complaint, 10000) == 300 && FameCity.Cost(FameRequestKind.Partnership, 12345) == 600
              && FameCity.Cost(FameRequestKind.Charity, 33333) % 50 == 0, "Standing: the other requests' costs, in whole fifties");

        // ---- when one comes
        FameFile f = FameKnown(40);
        f.Wealth = 40;
        f.Weekly = 20000;
        Check(FameCity.RequestRoll(FameKnown(20), 50, 0, 0, "north", "a~1") == null, "Standing: nobody asks somebody little known");
        Check(FameCity.RequestRoll(f, 50, 300, 0, "north", "a~1") == null, "Standing: and only on a day the roll says so");
        FameRequest first = FameCity.RequestRoll(f, 50, 0, 0, "north", "a~1");
        Check(first != null && first.Kind == FameRequestKind.Sponsor && first.Amount == 2000 && first.Until == 50 + FameCity.RequestDays && first.District == "north" && first.Place == null,
            "Standing: a sponsorship, its cost and its five days");
        Check(FameCity.RequestRoll(f, 51, 0, 0, "north", "a~1") == null, "Standing: one open at a time");
        first.State = FameRequestState.Refused;
        Check(FameCity.RequestRoll(f, 56, 0, 0, "north", "a~1") == null, "Standing: a week between two");
        FameRequest partner = FameCity.RequestRoll(f, 57, 0, 999, "north", "a~1");
        Check(partner != null && partner.Kind == FameRequestKind.Partnership && partner.Place == "a~1", "Standing: another owner's partnership for a business of the player's");
        partner.State = FameRequestState.Refused;
        FameRequest noShop = FameCity.RequestRoll(f, 64, 0, 999, "north", null);
        Check(noShop != null && noShop.Kind == FameRequestKind.Complaint, "Standing: no partnership without a business");
        noShop.State = FameRequestState.Refused;
        f.Wealth = 60;
        var kinds = new HashSet<FameRequestKind>();
        for (int pick = 0; pick < 1000; pick += 50)
        {
            f.RequestDay = -100;
            FameRequest r = FameCity.RequestRoll(f, 100, 0, pick, "north", "a~1");
            kinds.Add(r.Kind);
            r.State = FameRequestState.Refused;
        }
        Check(kinds.Count == 5, "Standing: somebody rich is asked every kind of request (" + kinds.Count + ")");

        // ---- what an answer does
        FameFile g = FameKnown(40);
        g.Weekly = 20000;
        FameRules.Day(g, FameSample(), 2);
        int north = g.District("north").Opinion, customers = g.Rep[0];
        FameRequest sponsor = FameCity.Offer(g, FameRequestKind.Sponsor, 2, 0, "north", null);
        Check(FameCity.Answer(g, sponsor, true, 2) == FameStory.Sponsor && sponsor.State == FameRequestState.Accepted && g.District("north").Opinion == north + 4 && g.Rep[0] == customers + 1
              && g.Press == 15, "Standing: a sponsorship: the district +4, the customers +1, the papers");
        Check(FameCity.BoostAt(g, "x", "north", 3) == FameCity.SponsorBoost && FameCity.BoostAt(g, "x", "south", 3) == 0 && FameCity.BoostAt(g, "x", "north", 2 + FameCity.SponsorDays) == 0,
            "Standing: 3% more customers in the district for five days");
        Check(FameCity.Answer(g, FameCity.Offer(g, FameRequestKind.Sponsor, 3, 1, "north", null), true, 3) == FameStory.Renovation, "Standing: or a corner done up");
        int integrity = g.Rep[3];
        FameRequest charity = FameCity.Offer(g, FameRequestKind.Charity, 4, 0, "north", null);
        Check(FameCity.Answer(g, charity, true, 4) == FameStory.Charity && g.Rep[3] == integrity + 4 && g.CharityDay == 4, "Standing: a gift to charity: integrity +4");
        customers = g.Rep[0];
        FameRequest complaint = FameCity.Offer(g, FameRequestKind.Complaint, 5, 0, "north", null);
        Check(FameCity.Answer(g, complaint, false, 5) == FameStory.None && g.Rep[0] == customers - 2, "Standing: a public complaint refused costs the customers");
        customers = g.Rep[0];
        FameCity.Offer(g, FameRequestKind.Complaint, 6, 0, "north", null);
        Check(FameCity.RequestsDay(g, 6 + FameCity.RequestDays) == 0 && FameCity.RequestsDay(g, 7 + FameCity.RequestDays) == 1 && g.Rep[0] == customers - 2, "Standing: and so does one left to lapse");
        FameRequest deal = FameCity.Offer(g, FameRequestKind.Partnership, 20, 0, "north", "a~1");
        Check(FameCity.Answer(g, deal, true, 20) == FameStory.Partnership && FameCity.BoostAt(g, "a~1", "north", 21) == FameCity.PartnerBoost, "Standing: a partnership: 4% more customers at the business");
        g.ScandalUntil = 30;
        Check(FameCity.BoostAt(g, "a~1", "north", 22) == FameCity.PartnerBoost + FameCity.ScandalFootfall && FameCity.BoostAt(g, "b~2", "south", 22) == FameCity.ScandalFootfall,
            "Standing: a scandal costs 2% everywhere");
        FameRequest called = FameCity.Offer(g, FameRequestKind.Partnership, 23, 0, "north", "a~1");
        FameCity.RequestsDay(g, 24);
        Check(called.State == FameRequestState.Withdrawn, "Standing: and calls a partnership off");

        // ---- evenings
        FameFile e = FameKnown(30);
        e.Wealth = 50;
        Check(FameCity.EventRoll(e, 50, 0, 0) == null, "Standing: no evening for somebody little known");
        e.Recognition = 40;
        FameInvite evening = FameCity.EventRoll(e, 50, 100, 0);
        Check(evening != null && evening.Kind == FameInviteKind.Investors && evening.Hour == FameCity.InvestorsHour && FamePress.InviteRoll(e, 51, 0) == null,
            "Standing: an evening with investors from a wealth of 45, and one invitation at a time");
        evening.State = FameInviteState.Declined;
        e.Recognition = 50;
        e.Wealth = 10;
        FameInvite reception = FameCity.EventRoll(e, 62, 100, 900);
        Check(reception != null && reception.Kind == FameInviteKind.CityEvent && reception.Hour == FameCity.ReceptionHour, "Standing: a reception at City Hall from 45");

        // ---- the council
        FameFile c = FameKnown(60);
        Check(FameCity.Influence(c) == 56 && FameCity.MayLobby(c, 10), "Standing: influence is 60% recognition and 40% reputation");
        c.LobbyCredit = FameCity.CreditFromReception;
        Check(FameCity.Influence(c) == 66 && FameCity.LobbyBase(c) == 31, "Standing: a word at a reception counts (" + FameCity.LobbyBase(c) + ")");
        FameLobby push = FameCity.Lobby(c, 2, -1, 10, 0);
        Check(push.Rival == FameCity.RivalLeast && push.Chance == 21 && push.DecideDay == 17 && c.LobbyRest == 38 && c.LobbyCredit == 0 && !FameCity.MayLobby(c, 20),
            "Standing: a push: the other side's backers off it, decided in a week, the next in four weeks (" + push.Chance + "%)");
        Check(FameCity.Decide(c, push, 16, 0) == FameStory.None && FameCity.Decide(c, push, 17, 20) == FameStory.LobbyWon && push.State == FameLobbyState.Won, "Standing: decided on its day");
        FameFile weak = FameKnown(40);
        weak.Rep[3] = 0;
        Check(FameCity.Lobby(weak, 1, 1, 1, 999).Chance == FameCity.LobbyLeast && FameCity.Decide(weak, weak.Lobbies[0], 8, FameCity.LobbyLeast) == FameStory.LobbyLost,
            "Standing: never under 10%, and lost on a bad roll");
        FameFile strong = FameKnown(100);
        strong.Rep[0] = strong.Rep[1] = strong.Rep[2] = strong.Rep[3] = 100;
        Check(FameCity.Lobby(strong, 1, 1, 1, 0).Chance == FameCity.LobbyMost, "Standing: never over 70%");
        Check(FameCity.LobbyFee(0) == 500 && FameCity.LobbyFee(100000) == 5000 && FameCity.LobbyFee(99999999) == 10000, "Standing: a lobbyist's fee");
        Check(!FameCity.MayLobby(FameKnown(30), 1), "Standing: no lobbying without influence");

        // ---- bribes
        int asked = 0;
        for (int seed = 0; seed < 4000; seed++)
        {
            asked += FameCity.BribeAsked(seed) ? 1 : 0;
        }
        Check(asked > 1200 && asked < 1600 && FameCity.BribeAsked(77) == FameCity.BribeAsked(77), "Standing: an official asks about one time in three, the same for the same issue (" + asked + ")");
        FameFile b = FameKnown(40);
        FameBribe paid = FameCity.Bribed(b, "a~1", 400, 10);
        Check(b.Rep[3] == 45 && b.Bribes.Count == 1, "Standing: a bribe costs integrity 5 at once");
        Check(FameCity.SurfaceChance(b, paid, 19) == 0 && FameCity.SurfaceChance(b, paid, 20) == FameCity.SurfaceBase + 2, "Standing: it may come out from ten days on");
        FameCity.Bribed(b, "b~2", 400, 11);
        Check(FameCity.SurfaceChance(b, paid, 20) == FameCity.SurfaceBase + FameCity.SurfaceEach + 2, "Standing: likelier with every other bribe");
        b.Recognition = 100;
        for (int i = 0; i < 6; i++)
        {
            FameCity.Bribed(b, "c~3", 100, 12 + i);
        }
        Check(FameCity.SurfaceChance(b, b.Bribes[0], 40) == 39 && FameCity.SurfaceChance(b, b.Bribes[0], 40) <= FameCity.SurfaceMost, "Standing: eight bribes and every eye on the player: about 4% a day");
        FameFile s = FameKnown(40);
        FameRules.Day(s, FameSample(), 2);
        FameBribe out1 = FameCity.Bribed(s, "a~1", 300, 2);
        Check(FameCity.Surface(s, 11, 0) == null, "Standing: nothing comes out too soon");
        int sIntegrity = s.Rep[3], sCustomers = s.Rep[0], sWorkers = s.Rep[1];
        Check(FameCity.Surface(s, 12, 999) == null && FameCity.Surface(s, 12, 0) == out1 && out1.Surfaced == 12 && s.ScandalUntil == 12 + FameCity.ScandalDays,
            "Standing: a bribe come out is a scandal of two weeks");
        Check(s.Rep[3] == sIntegrity + FameCity.ScandalIntegrity && s.Rep[0] == sCustomers + FameCity.ScandalCustomers && s.Rep[1] == sWorkers + FameCity.ScandalWorkers && FameCity.InScandal(s, 25)
              && !FameCity.InScandal(s, 26), "Standing: integrity -8, the customers -4, the workers -2");
        Check(FameCity.Surface(s, 13, 0) == null, "Standing: once only");
        s.SettledDay = 13;
        s.Rep[3] = 50;
        s.Recognition = 0;
        Check(FamePress.IssueScale(s, FameIssue.Inspection) == 1500 && FamePress.Esteem(s) == FamePress.Esteem(FameKnown(0)) - FameCity.ScandalEsteem, "Standing: more inspections and a worse name as an employer in a scandal");
        s.Rep[3] = 40;
        FameRules.Day(s, FameSample(), 15);
        Check(s.Rep[3] == 40, "Standing: integrity forgets nothing while a scandal runs");

        // ---- the city's line in the save
        FameFile w = FameKnown(60);
        w.CharityDay = 9;
        w.LobbyCredit = 10;
        FameCity.Offer(w, FameRequestKind.Rescue, 3, 5, "south", null);
        FameCity.Bribed(w, "a~1", 250, 4).Case = 17;
        FameCity.Lobby(w, 5, 1, 6, 500);
        FameCity.Answer(w, FameCity.Offer(w, FameRequestKind.Partnership, 7, 0, "north", "a~1"), true, 7);
        var data = new Dictionary<string, string>();
        FameStore.Save(data, w);
        FameFile back = FameStore.Load(data);
        Check(back != null && back.CharityDay == 9 && back.LobbyCredit == 0 && back.LobbyRest == w.LobbyRest && back.Requests.Count == 2 && back.Requests[0].Kind == FameRequestKind.Rescue
              && back.Requests[0].District == "south" && back.Requests[1].Place == "a~1" && back.Bribes.Count == 1 && back.Bribes[0].Case == 17 && back.Bribes[0].Surfaced == -1
              && back.Lobbies.Count == 1 && back.Lobbies[0].Lever == 5 && back.Lobbies[0].Chance == w.Lobbies[0].Chance && back.Boosts.Count == 1 && back.Boosts[0].Place == "a~1",
            "Standing: the city's line keeps the requests, the bribes, the council and the boosts");
        data[FameStore.CityKey] = data[FameStore.CityKey] + "\nR|99|9|1|2|3|x||0|0\nL|98|1|1|1|1|1|1|9";
        FameFile later = FameStore.Load(data);
        Check(later != null && later.Requests.Count == 2 && later.Lobbies.Count == 1, "Standing: a request or a push of a later version's is left out");
    }
}
