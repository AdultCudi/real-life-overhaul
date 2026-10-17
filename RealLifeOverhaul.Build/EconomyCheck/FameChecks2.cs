using System.Collections.Generic;
using RealLifeOverhaul.Fame;

// Checks for Standing's press and what standing does (Scripts/Fame/FamePress): the reporter and the photographer, the
// questions of an interview and the table of what each answer brings, promises and their day, invitations from the offer
// to the hour, the papers' milestones and criticism, the customers a name brings, the deal of business pressure's issues,
// the esteem as an employer, walking past somebody who needed help, and the press's line in the save.
internal static partial class Program
{
    private static FameFile FameKnown(int recognition)
    {
        FameFile f = FameRules.Open(5, 1);
        f.Recognition = recognition;
        f.SettledDay = 1;
        return f;
    }

    private static void FamePressChecks()
    {
        Check(FamePress.ReporterChance(34) == 0 && FamePress.ReporterChance(35) == 10 && FamePress.ReporterChance(60) == 60, "Standing: reporters from Known on, 60 per mille an hour at 60");
        Check(FamePress.PhotoChance(44) == 0 && FamePress.PhotoChance(70) == 60, "Standing: photographers from 45");
        FameFile f = FameKnown(60);
        f.ReporterDay = 10;
        Check(!FamePress.ReporterMay(f, 14) && FamePress.ReporterMay(f, 15), "Standing: five days between two reporters");

        // ---- the questions
        var asks = new List<FameAsk>();
        FamePress.Questions(f, FameVenue.Street, 2, asks);
        Check(asks.Count == 2 && asks[0].Question == FameQuestion.Plans && asks[1].Question == FameQuestion.Promise && asks[1].Promise == FamePromiseKind.Jobs,
            "Standing: in the street with no weak part, the plans and a promise of jobs");
        f.Rep[1] = 30;
        f.Rep[0] = 40;
        FamePress.Questions(f, FameVenue.Street, 3, asks);
        Check(asks.Count == 2 && asks[0].Question == FameQuestion.Rise && asks[1].Question == FameQuestion.Hard && asks[1].Topic == FamePart.Workers, "Standing: a hard question about the weakest part");
        FamePress.Questions(f, FameVenue.Show, 3, asks);
        Check(asks.Count == 3 && asks[0].Question == FameQuestion.Rise && asks[1].Question == FameQuestion.Hard && asks[2].Question == FameQuestion.Promise && asks[2].Promise == FamePromiseKind.Wages,
            "Standing: the show asks the rise, a hard one and for a promise about the staff");

        // ---- what each answer brings
        var rise = new FameAsk { Question = FameQuestion.Rise };
        Check(FamePress.Answer(rise, 0, FameVenue.Street).Press == 20 && FamePress.Answer(rise, 0, FameVenue.Magazine).Press == 30 && FamePress.Answer(rise, 0, FameVenue.Show).Press == 50
              && FamePress.Answer(rise, 1, FameVenue.Street).Press == 12, "Standing: an answer brings more in a magazine and most on the show");
        FameAnswer deflect = FamePress.Answer(new FameAsk { Question = FameQuestion.Hard, Topic = FamePart.Workers }, 1, FameVenue.Street);
        FameAnswer honest = FamePress.Answer(new FameAsk { Question = FameQuestion.Hard, Topic = FamePart.Workers }, 0, FameVenue.Street);
        Check(deflect.Press == 14 && deflect.PartA == FamePart.Workers && deflect.MoveA == -1 && deflect.PartB == FamePart.Integrity && deflect.MoveB == -2 && !deflect.Good
              && honest.Press == 8 && honest.MoveA == 1 && honest.MoveB == 2 && honest.Good, "Standing: honest helps, deflecting makes noise and costs");
        FameAnswer integrity = FamePress.Answer(new FameAsk { Question = FameQuestion.Hard, Topic = FamePart.Integrity }, 1, FameVenue.Street);
        Check(integrity.PartA == FamePart.Integrity && integrity.MoveA == -3 && integrity.MoveB == 0, "Standing: a hard question about integrity moves only integrity");
        FameAnswer promise = FamePress.Answer(new FameAsk { Question = FameQuestion.Promise }, 0, FameVenue.Street);
        Check(promise.Promises && promise.Press == 14 && !FamePress.Answer(new FameAsk { Question = FameQuestion.Promise }, 1, FameVenue.Street).Promises, "Standing: only the first answer promises");
        FameFile g = FameKnown(40);
        int press = FamePress.Apply(g, FamePress.Answer(rise, 1, FameVenue.Show), FameCause.Show, 1);
        Check(press == 30 && g.Press == 30 && g.Rep[1] == 51 && g.Rep[3] == 51, "Standing: an answer given moves the papers and the parts");

        // ---- promises
        g.Staff = 4;
        FamePromise jobs = FamePress.Promise(g, FamePromiseKind.Jobs, FameCause.Interview, 10);
        Check(jobs != null && jobs.Goal == 7 && jobs.Due == 10 + FamePress.PromiseDays && FamePress.Promise(g, FamePromiseKind.Jobs, FameCause.Show, 11) == null, "Standing: a promise of jobs is three more, one of a kind at a time");
        Check(FamePress.Promise(g, FamePromiseKind.Wages, FameCause.Show, 11) != null && FamePress.Promise(g, FamePromiseKind.Prices, FameCause.Show, 11) != null
              && FamePress.Promise(g, FamePromiseKind.Charity, FameCause.Show, 11) == null, "Standing: three open promises at most");
        Check(FamePress.Judge(g, jobs, 20, -1) == FamePromiseState.Open, "Standing: a promise not reached and not due stays open");
        int before = g.Rep[3];
        g.Staff = 7;
        Check(FamePress.Judge(g, jobs, 21, -1) == FamePromiseState.Kept && g.Rep[3] == before + FamePress.KeptIntegrity, "Standing: kept as soon as it is reached: integrity +4");
        FamePromise wages = g.Promises[1];
        before = g.Rep[3];
        Check(FamePress.Judge(g, wages, wages.Due - 1, -1) == FamePromiseState.Open && FamePress.Judge(g, wages, wages.Due, -1) == FamePromiseState.Broken && g.Rep[3] == before + FamePress.BrokenIntegrity,
            "Standing: broken on its day: integrity -6");
        FameFile h = FameKnown(40);
        FamePromise charity = FamePress.Promise(h, FamePromiseKind.Charity, FameCause.Magazine, 50);
        Check(FamePress.Judge(h, charity, 51, 49) == FamePromiseState.Open && FamePress.Judge(h, charity, 52, 52) == FamePromiseState.Kept, "Standing: a gift to charity after the promise keeps it");

        // ---- invitations
        FameFile s = FameKnown(30);
        Check(FamePress.InviteRoll(s, 20, 0) == null, "Standing: nobody invites somebody little known");
        s.Recognition = 45;
        FameInvite mag = FamePress.InviteRoll(s, 20, 100);
        Check(mag != null && mag.Kind == FameInviteKind.Magazine && mag.Day == 23 && mag.Hour == FamePress.MagazineHour && mag.AnswerBy == 22 && FamePress.InviteRoll(s, 21, 0) == null,
            "Standing: a magazine from 40, three days on, and one at a time");
        Check(!FamePress.Due(s, mag, 23, FamePress.MagazineHour, true) && mag.State == FameInviteState.Lapsed, "Standing: an invitation not accepted lapses");
        s.Recognition = 60;
        Check(FamePress.InviteRoll(s, 25, 0) == null && FamePress.InviteRoll(s, 32, 900) == null, "Standing: twelve days between two, and the roll must say so");
        FameInvite show = FamePress.InviteRoll(s, 32, 100);
        Check(show != null && show.Kind == FameInviteKind.Show && show.Hour == FamePress.ShowHour, "Standing: the night show from Prominent on");
        show.State = FameInviteState.Accepted;
        Check(!FamePress.Due(s, show, 35, FamePress.ShowHour - 1, true) && !FamePress.Due(s, show, 35, FamePress.ShowHour, false) && FamePress.Due(s, show, 35, FamePress.ShowHour + 1, true),
            "Standing: it starts at its hour if the player is free, an hour late at most");
        int integrityBefore = s.Rep[3], pressBefore = s.Press = 50;
        Check(!FamePress.Due(s, show, 35, FamePress.ShowHour + 2, true) && show.State == FameInviteState.Missed && s.Rep[3] == integrityBefore - 1 && s.Press == pressBefore + FamePress.MissedPress,
            "Standing: missed after that, and noticed");
        for (int i = 0; i < 12; i++)
        {
            FamePress.Offer(s, FameInviteKind.Magazine, 100 + i, 15).State = FameInviteState.Declined;
        }
        Check(s.Invites.Count == FamePress.InvitesMost, "Standing: the newest eight invitations are kept");

        // ---- the papers' pieces
        FameFile m = FameKnown(50);
        m.Businesses = 3;
        m.Properties = 1;
        Check(FamePress.Milestone(m) == FameStory.None && m.Milestones == (1 | 16), "Standing: what there was when the file opened is no news");
        m.Businesses = 5;
        m.Properties = 5;
        Check(FamePress.Milestone(m) == FameStory.Shops5 && FamePress.Milestone(m) == FameStory.Property5 && FamePress.Milestone(m) == FameStory.None, "Standing: a fifth store, a fifth building, one at a time, once");
        FameFile c = FameKnown(40);
        c.Staff = 5;
        c.Rep[1] = 20;
        Check(FamePress.Criticism(c, 100, out string district) == FameStory.Conditions && c.Rep[1] == 18 && FamePress.Criticism(c, 110, out district) == FameStory.None
              && FamePress.Criticism(c, 130, out district) == FameStory.Conditions, "Standing: criticism over the staff's conditions, thirty days apart");
        c.Recognition = 20;
        Check(FamePress.Criticism(c, 200, out district) == FameStory.None, "Standing: the papers write only about somebody Known");
        Check(FamePress.StoryMay(c, 5) && !FamePress.StoryMay(c, 5) && FamePress.StoryMay(c, 6), "Standing: one piece a day");

        // ---- what standing does to business
        FameFile b = FameKnown(50);
        b.Rep[0] = 80;
        Check(FamePress.Footfall(b, new FameDistrict { Opinion = 70, Known = 60 }) == 3, "Standing: a good name brings 3% more customers where it is known (" + FamePress.Footfall(b, new FameDistrict { Opinion = 70, Known = 60 }) + ")");
        b.Rep[0] = 20;
        Check(FamePress.Footfall(b, new FameDistrict { Opinion = 30, Known = 80 }) == -4, "Standing: a poor one costs 4%");
        b.Rep[0] = 100;
        Check(FamePress.Footfall(b, new FameDistrict { Opinion = 100, Known = 100 }) == FamePress.FootfallMost, "Standing: 5% at most");
        b.Rep[0] = 20;
        b.Rep[2] = 30;
        b.Rep[3] = 20;
        b.Recognition = 80;
        Check(FamePress.IssueScale(b, FameIssue.Complaint) == 1270 && FamePress.IssueScale(b, FameIssue.Praise) == 700 && FamePress.IssueScale(b, FameIssue.Inspection) == 1540,
            "Standing: more complaints, less praise, more inspections (" + FamePress.IssueScale(b, FameIssue.Complaint) + ", " + FamePress.IssueScale(b, FameIssue.Praise) + ")");
        b.Rep[3] = 90;
        b.Recognition = 0;
        Check(FamePress.IssueScale(b, FameIssue.Audit) == FamePress.IssueLeast, "Standing: and fewer audits for somebody trusted and little known");
        b.Rep[1] = 100;
        int high = FamePress.Esteem(b);
        b.Rep[1] = 0;
        int low = FamePress.Esteem(b);
        b.Rep[1] = 52;
        Check(high == 10 && low == -10 && FamePress.Esteem(b) == 0, "Standing: the esteem as an employer, -10 to 10");

        // ---- help refused
        FameFile r = FameKnown(50);
        Check(FamePress.RefusalMay(r, 3) && !FamePress.RefusalMay(r, 3) && FamePress.RefusalMay(r, 4), "Standing: walking on is counted once a day");
        Check(FamePress.Refused(r, 4, 0) && r.Rep[3] == 50 + FamePress.RefusedIntegrity + FamePress.ReportedIntegrity, "Standing: walked on and reported: integrity -5");
        FameFile q = FameKnown(50);
        Check(!FamePress.Refused(q, 4, 999) && q.Rep[3] == 50 + FamePress.RefusedIntegrity, "Standing: walked on and not reported: integrity -3");
        FameFile famous = FameKnown(100);
        Check(FamePress.Refused(famous, 4, 640) && !FamePress.Refused(famous, 5, 760), "Standing: the better known, the likelier the paper hears of it, 75% at most");

        // ---- the press's line in the save
        FameFile w = FameKnown(60);
        w.ReporterDay = 7;
        w.Milestones = 5;
        w.MilestonesSeen = true;
        FamePress.Promise(w, FamePromiseKind.Prices, FameCause.Magazine, 3);
        FamePress.Offer(w, FameInviteKind.Show, 4, 21).State = FameInviteState.Accepted;
        var data = new Dictionary<string, string>();
        FameStore.Save(data, w);
        FameFile back = FameStore.Load(data);
        Check(back != null && back.ReporterDay == 7 && back.Milestones == 5 && back.MilestonesSeen && back.NextId == w.NextId && back.Promises.Count == 1 && back.Promises[0].Kind == FamePromiseKind.Prices
              && back.Promises[0].Source == FameCause.Magazine && back.Invites.Count == 1 && back.Invites[0].State == FameInviteState.Accepted && back.Invites[0].Day == 7 && back.Invites[0].Hour == 21,
            "Standing: the press's line keeps the dates, promises and invitations");
        data[FameStore.PressKey] = data[FameStore.PressKey] + "\nI|99|9|1|2|3|4|0\nP|98|0|1|2|3|7|0";
        FameFile later = FameStore.Load(data);
        Check(later != null && later.Invites.Count == 1 && later.Promises.Count == 1, "Standing: an invitation or a promise of a later version's is left out");
        data[FameStore.PressKey] = "SP9|1";
        Check(FameStore.Load(data) != null && FameStore.Load(data).Invites.Count == 0, "Standing: another version's press line leaves the file without it");
    }
}
