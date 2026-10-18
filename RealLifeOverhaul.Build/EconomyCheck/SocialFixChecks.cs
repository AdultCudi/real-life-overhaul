using System;
using RealLifeOverhaul;

// The social life (Scripts/Social): the faults found when the folder was cleaned up, each checked against what the old
// code did wrong. A friend who cannot pay a loan back on the day has the few more days they ask for; a report made
// indoors moves to the building's door only when it was made where the player stands; a favour asked by a call nobody
// answered lapses at no cost when no text could follow; a new cast has one personal trainer at the most.
internal static partial class Program
{
    private static void SocialFixes()
    {
        SocialLoanGrace();
        SocialReportPlace();
        SocialMissedCall();
        SocialOneTrainer();
    }

    // Lent on day 10 to be paid back on day 15; the excuse on day 15 asks for a few more days, and nothing is asked in them.
    private static void SocialLoanGrace()
    {
        const int lent = 10, due = 15, loan = 500;
        bool waits = true;
        for (int day = lent; day < due; day++)
        {
            waits &= SocialRules.Loan(loan, due, day) == LoanStep.None;
        }
        Check(waits && SocialRules.Loan(loan, due, due) == LoanStep.Due, "social, loans: nothing is asked before the day they said, and on it they pay or make their excuse");
        int grace = SocialRules.GraceUntil(due);
        bool quiet = grace < 0;
        for (int day = due; day < due + SocialRules.LoanGraceDays; day++)
        {
            quiet &= SocialRules.Loan(loan, grace, day) == LoanStep.None;
        }
        Check(quiet && SocialRules.Loan(loan, grace, due + 1) == LoanStep.None,
            "social, loans: after the excuse nothing is asked for " + SocialRules.LoanGraceDays + " days, the next morning neither (" + SocialRules.Loan(loan, grace, due + 1) + ")");
        Check(SocialRules.Loan(loan, grace, due + SocialRules.LoanGraceDays) == LoanStep.GraceOver && SocialRules.Loan(loan, grace, due + 40) == LoanStep.GraceOver,
            "social, loans: at the end of the days more they asked for, they pay or it is written off, and it is told");
        Check(SocialRules.Loan(0, due, due) == LoanStep.None && SocialRules.Loan(0, grace, due + 40) == LoanStep.None && SocialRules.Loan(loan, 0, 0) == LoanStep.Due,
            "social, loans: with nothing lent nothing is asked; a loan with no day set is asked for at once, as it was");

        string contact = ModSource(@"Social\SocialDirector.Contact.cs");
        string daily = MemberText(contact, "private void Daily(int day)");
        int due1 = daily.IndexOf("loanStep == LoanStep.Due", StringComparison.Ordinal), over = daily.IndexOf("loanStep == LoanStep.GraceOver", StringComparison.Ordinal);
        Check(daily.Length > 0 && daily.Contains("SocialRules.Loan(p.Loan, p.LoanDue, day)") && daily.Contains("p.LoanDue = SocialRules.GraceUntil(day);")
              && !daily.Contains("day >= p.LoanDue") && due1 >= 0 && over > due1 && daily.IndexOf("reallife_social_loan_written_off", over, StringComparison.Ordinal) > over,
            "social, loans: the day's pass asks the rule which step a loan is at, and only the grace's end writes it off with its notice");
    }

    // Indoors, the player's own place is the interior's; anything else reported (a car parked down the street) is the city's.
    private static void SocialReportPlace()
    {
        float r = SocialRules.ReportAtPlayer;
        Check(SocialRules.AtPlayer(10f, 20f, 10f, 20f) && SocialRules.AtPlayer(10f + r * 0.7f, 20f + r * 0.7f, 10f, 20f) && !SocialRules.AtPlayer(10f + r + 0.01f, 20f, 10f, 20f)
              && !SocialRules.AtPlayer(-350f, 900f, 10f, 20f), "social, the paper: a report indoors is the player's own place within " + r + " m of them, and no other");
        string news = ModSource(@"Social\News.cs");
        string report = MemberText(news, "public static void Report(NewsKind kind, Vector3 where");
        string inCity = MemberText(news, "private static Vector3 InCity(Vector3 where)");
        int moved = report.IndexOf("where = InCity(where);", StringComparison.Ordinal);
        Check(report.Length > 0 && moved >= 0 && moved < report.IndexOf("forward(", StringComparison.Ordinal) && report.Contains("placed(kind, where)")
              && Occurs(news, "SocialPlaces.OnStreet(") == 1 && inCity.Contains("SocialRules.AtPlayer(") && inCity.Contains("SocialPlaces.OnStreet(where)"),
            "social, the paper: every report is placed once, the shared ledger, the districts and the paper alike, and only one made at the player moves to the door");
    }

    // They rang, nobody answered, and the day's texts were used up: nothing was said of what they wanted, so it does not stand.
    private static void SocialMissedCall()
    {
        string plan = ModSource(@"Social\SocialDirector.Plan.cs");
        string ring = MemberText(plan, "private void Ring(float real)");
        int text = ring.IndexOf("!SocialLife.SendText(_ringPerson, SocialLines.Pick(SocialCatalogue.TextMissedCall)", StringComparison.Ordinal);
        int clear = ring.IndexOf("offer.Clear();", StringComparison.Ordinal);
        Check(ring.Length > 0 && text >= 0 && clear > text && ring.Contains("offer.Person == _ringPerson.Index"),
            "social, calls: a missed call's favour lapses at no cost when its text cannot be sent, as an offer whose text is refused does");
    }

    // Dealt without replacement: two people of a cast never share a job, the gym's trainer too, and nothing else of the cast moves.
    private static void SocialOneTrainer()
    {
        var people = new SocialPerson[SocialRules.CastSize];
        var again = new SocialPerson[SocialRules.CastSize];
        int trainer = Array.IndexOf(SocialCatalogue.Jobs, "reallife_social_job_trainer");
        int gym = (int)SocialOrigin.Gym, gymTrainers = 0, seeds = 0;
        bool apart = true, same = true;
        for (int seed = 1; seed <= 3000; seed++)
        {
            SocialCatalogue.MakeCast(people, seed, 0, 112, 9);
            SocialCatalogue.MakeCast(again, seed, 0, 112, 9);
            seeds++;
            gymTrainers += people[gym].Job == trainer ? 1 : 0;
            for (int i = 0; i < people.Length; i++)
            {
                same &= again[i].Job == people[i].Job && again[i].Name == people[i].Name && again[i].Seed == people[i].Seed && again[i].Traits == people[i].Traits;
                for (int k = i + 1; k < people.Length; k++)
                {
                    apart &= people[i].Job != people[k].Job;
                }
            }
        }
        Check(apart, "social, the cast: no two people of a new cast share a job, the gym's personal trainer included (3000 seeds)");
        Check(same && gymTrainers > seeds / 4 && gymTrainers < seeds * 5 / 12,
            "social, the cast: the same seed deals the same cast, and the gym's person trains people about one time in three (" + gymTrainers + " of " + seeds + ")");
    }
}
