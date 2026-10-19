using System;
using RealLifeOverhaul;

// The social life (Scripts/Social): the faults found when the folder was cleaned up, each checked against what the old
// code did wrong. A friend who cannot pay a loan back on the day has the few more days they ask for; a report made
// indoors moves to the building's door only when it was made where the player stands; a favour asked by a call nobody
// answered lapses at no cost when no text could follow; a new cast has one personal trainer at the most; an outing put
// forward on a call is never turned down a step later; an outing is never agreed for a place that is shut at the hour.
internal static partial class Program
{
    private static void SocialFixes()
    {
        SocialLoanGrace();
        SocialReportPlace();
        SocialMissedCall();
        SocialOneTrainer();
        SocialPlanKept();
        SocialOpenHours();
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

    // A player's call (19 Oct 2026): a friend put forward drinks, the player said yes, and the friend said the week was
    // impossible. The offer looked only at the outing and the place; the free hour was looked for after the yes (the
    // friend free 11 to 18 on weekdays, drinks from 19). Now the hour is found before anything is put forward and kept,
    // and a rough day's no is drawn once a call, before the offer.
    private static void SocialPlanKept()
    {
        Func<int, bool> weekend = d => d % 7 == 5 || d % 7 == 6;   // (days 5 and 6 of each week here: the mod asks the game's own calendar)
        OutingDef drinks = SocialCatalogue.Outing(PlanKind.Drinks), coffee = SocialCatalogue.Outing(PlanKind.Coffee);
        var day = new SocialPerson { FreeFrom = 11, FreeTo = 18 };      // the call's friend: free in the day on weekdays
        var evening = new SocialPerson { FreeFrom = 17, FreeTo = 23 };
        var owl = new SocialPerson { FreeFrom = 17, FreeTo = 23, Traits = SocialTrait.NightOwl };
        const float monday9 = 9f * 60f;                                 // day 0, 09:00
        float sat = SocialRules.FirstSlot(day, drinks, 4f * 1440f + 9f * 60f, weekend);
        Check(SocialRules.FirstSlot(day, drinks, monday9, weekend) < 0f && SocialRules.FirstSlot(day, drinks, monday9 + 1440f, weekend) < 0f
              && Math.Abs(sat - (5f * 1440f + 19f * 60f)) < 0.01f,
            "social, plans: somebody free 11 to 18 on weekdays has no hour for drinks (19 on) in the two days ahead, and has one on the weekend's evening (" + sat + ")");
        float c = SocialRules.FirstSlot(day, coffee, monday9 + SocialRules.PlanLead, weekend);
        float e = SocialRules.FirstSlot(evening, drinks, monday9, weekend), o = SocialRules.FirstSlot(owl, drinks, monday9, weekend);
        Check(Math.Abs(c - 11f * 60f) < 0.01f && Math.Abs(e - 19f * 60f) < 0.01f && Math.Abs(o - 19f * 60f) < 0.01f,
            "social, plans: coffee with the same friend is at 11 that morning, drinks with somebody free in the evening at 19 (" + c + ", " + e + ", " + o + ")");
        Check(Math.Abs(SocialRules.FirstSlot(evening, coffee, 17f * 60f, weekend) - 17f * 60f) < 0.01f && Math.Abs(SocialRules.FirstSlot(evening, coffee, 17f * 60f + 1f, weekend) - 18f * 60f) < 0.01f
              && Math.Abs(SocialRules.FirstSlot(evening, coffee, 19f * 60f, weekend) - (1440f + 17f * 60f)) < 0.01f,
            "social, plans: an hour is a whole one from the time asked, that hour itself when on it, and past the outing's hours the next day's");
        var late = new SocialPerson { FreeFrom = 22, FreeTo = 2 };
        float wrap = SocialRules.FirstSlot(late, drinks, monday9, weekend);
        Check(Math.Abs(wrap - 22f * 60f) < 0.01f && SocialRules.FirstSlot(new SocialPerson { FreeFrom = 0, FreeTo = 7 }, drinks, monday9, weekend) < 0f,
            "social, plans: free hours over midnight and an outing past midnight meet before their bed at 23, and somebody free only while asleep is never asked out (" + wrap + ")");

        // Nothing left to chance between the steps: the same person, outing and time give the same hour each time, and a
        // rough day's no is the call's one draw.
        bool same = true;
        var cast = new SocialPerson[SocialRules.CastSize];
        for (int seed = 1; seed <= 200; seed++)
        {
            SocialCatalogue.MakeCast(cast, seed, 0, 112, 9);
            for (int i = 0; i < cast.Length; i++)
            {
                for (int k = 0; k <= (int)PlanKind.Business; k++)
                {
                    float from = seed * 97f + i * 61f + k * 13f;
                    float a = SocialRules.FirstSlot(cast[i], SocialCatalogue.Outing((PlanKind)k), from, weekend);
                    same &= a == SocialRules.FirstSlot(cast[i], SocialCatalogue.Outing((PlanKind)k), from, weekend) && (a < 0f || a >= from);
                }
            }
        }
        Check(same, "social, plans: the free hour is the same each time it is asked (200 casts, every outing), never before the time asked");
        Check(SocialRules.MoodSaysNo(-2, false, 0.2f) && !SocialRules.MoodSaysNo(-2, false, 0.8f) && !SocialRules.MoodSaysNo(-1, false, 0f) && !SocialRules.MoodSaysNo(-2, true, 0f),
            "social, plans: only a rough day says no, by the call's draw, and never to their own invitation");

        string dialog = ModSource(@"Social\PersonDialog.cs");
        string plans = MemberText(dialog, "private DialogEntry Plans(int at, int tried, bool noHour = false)");
        string when = MemberText(dialog, "private DialogEntry When(PlanKind kind, Venue venue, float first, bool theirIdea)");
        string agree = MemberText(dialog, "private DialogEntry Agree(PlanKind kind, Venue venue, float time, bool theirIdea)");
        string asked = MemberText(dialog, "private DialogEntry Asked()");
        int slot = plans.IndexOf("PlanFind found = SocialLife.PlaceAndHour(_p, kind, SocialDirector.Where(), 40f, 1800f, SocialLife.Now + SocialRules.PlanLead, out Venue venue, out float first);", StringComparison.Ordinal);
        int offer = plans.IndexOf("Line(def.Offer, data)", StringComparison.Ordinal);
        int mood = plans.IndexOf("SocialRules.MoodSaysNo(_p.Mood, false, _moodRoll)", StringComparison.Ordinal);
        Check(plans.Length > 0 && slot > 0 && offer > slot && mood > 0 && mood < slot && plans.Contains("Plans(at + 1, tried + 1, true)")
              && plans.Contains("When(kind, venue, first, false)") && plans.Contains("noHour ? Close(SocialLines.Pick(SocialCatalogue.PlanBusy))")
              && plans.Contains("if (_moodRoll < 0f)") && Occurs(dialog, "_moodRoll = Random.value;") == 1,
            "social, plans: asked for plans, they say no (a rough day, by the call's one draw) or that they are busy before anything is put forward, and put forward only an outing they have the hour for");
        Check(when.Length > 0 && agree.Length > 0 && !when.Contains("Close(") && !when.Contains("PlanBusy") && !when.Contains("SocialLife.Now") && !agree.Contains("Random")
              && !agree.Contains("PlanMood") && agree.Contains("SocialLife.MakePlan(_p, kind, time, venue, theirIdea, out string reason)"),
            "social, plans: after the player's yes nothing turns it down: the hour put forward is the one offered, and the plan is made at it");
        int invited = asked.IndexOf("found = SocialLife.PlaceAndHour(_p, kind, player, 40f, 1800f, SocialLife.Now + SocialRules.PlanLead, out venue, out first);", StringComparison.Ordinal);
        int moving = asked.IndexOf("float time = SocialLife.NextSlot(_p, PlanKind.Walk, SocialLife.Now + SocialRules.PlanLead);", StringComparison.Ordinal);
        Check(invited > 0 && invited < asked.IndexOf("Line(def.Offer, invite)", StringComparison.Ordinal) && asked.Contains("When(kind, venue, first, true)")
              && moving > 0 && moving < asked.IndexOf("DialogEntry moving = Line(def.Offer, data);", StringComparison.Ordinal) && Occurs(asked, "NextSlot(") == 1 && Occurs(asked, "PlaceAndHour(") == 1,
            "social, plans: their own invitation and a move they ask help with find the hour before they put it, and keep it after the yes");
        string invite = MemberText(ModSource(@"Social\SocialDirector.Contact.cs"), "private void Invite(SocialPerson person, float real)");
        string life = ModSource(@"Social\SocialLife.cs");
        int free = invite.IndexOf("SocialLife.NextSlot(person, kind, SocialLife.Now + SocialRules.PlanLead) < 0f", StringComparison.Ordinal);
        Check(free > 0 && free < invite.IndexOf("offer.Person = person.Index;", StringComparison.Ordinal)
              && MemberText(life, "public static float NextSlot(").Contains("SocialRules.FirstSlot(person, SocialCatalogue.Outing(kind), from, WeekendOf)") && !life.Contains("new Timestamp(at)"),
            "social, plans: nobody texts an invitation they have no free hour for, and every hour is found by the one rule");
    }

    // A cinema open 10 to 22 and a club open 20 to 3, shut on the week's last day: an outing is agreed only for an hour at
    // which the place is open for all of it, and every way a plan is made asks for the place and the hour together.
    private static void SocialOpenHours()
    {
        Func<int, int, bool> cinema = (d, h) => d % 7 != 6 && h >= 10 && h < 22;
        Func<int, int, bool> club = (d, h) => d % 7 != 6 && (h >= 20 || h < 3);
        const int film = 130, night = 110;
        Check(SocialRules.OpenThrough(19f * 60f, film, cinema) && !SocialRules.OpenThrough(21f * 60f, film, cinema) && !SocialRules.OpenThrough(9f * 60f, film, cinema)
              && SocialRules.OpenThrough(19f * 60f + 50f, film, cinema) && !SocialRules.OpenThrough(19f * 60f + 51f, film, cinema),
            "social, open hours: a film at 19:00 fits a cinema shut at 22:00, one at 21:00 or before it opens does not, and the last minute counts, not the one after it");
        Check(SocialRules.OpenThrough(23f * 60f, night, club) && !SocialRules.OpenThrough(2f * 60f, night, club)
              && !SocialRules.OpenThrough(5f * 1440f + 23f * 60f, night, club) && !SocialRules.OpenThrough(6f * 1440f + 20f * 60f, night, club),
            "social, open hours: drinks over midnight fit a club open to 3, run into its closing they do not, and not into or on the day it is shut");
        Check(SocialRules.OpenThrough(12f * 60f, 0, cinema) && !SocialRules.OpenThrough(22f * 60f, 0, cinema),
            "social, open hours: an outing of no length needs only the hour it starts in to be open");

        string places = ModSource(@"Social\SocialPlaces.cs"), life = ModSource(@"Social\SocialLife.cs"), dialog = ModSource(@"Social\PersonDialog.cs");
        string find = MemberText(places, "public static bool Find(PlanKind kind");
        string openFor = MemberText(places, "public static bool OpenFor(CityBuildingController building");
        Check(find.Contains("(openAt >= 0f && !OpenFor(building, def, openAt))") && openFor.Contains("def.Match == null") && openFor.Contains("registration.temporarilyClosed")
              && openFor.Contains("SocialRules.OpenThrough(at, def.Minutes, OpenAtHour)"),
            "social, open hours: a place is looked for open for the whole outing at the hour asked, by its business's own week of hours, and an outing with no business is never held up");
        string both = MemberText(life, "public static PlanFind PlaceAndHour(");
        Check(both.Contains("SocialPlaces.Find(kind, around, minDistance, maxDistance, out venue, null, time)") && both.Contains("SocialRules.OpenTries")
              && both.Contains("return PlanFind.Shut;") && MemberText(life, "public static float NextOpenSlot(").Contains("SocialPlaces.OpenFor(venue, kind, time)"),
            "social, open hours: their free hours are tried in turn for one at which a place nearby is open, and a second hour offered is open too");
        string plans = MemberText(dialog, "private DialogEntry Plans(int at, int tried");
        string flare = ModSource(@"Wiring\FlareInviteLinks.cs"), staged = ModSource(@"Social\SocialDirector.Plan.cs");
        Check(plans.Contains("SocialLife.PlaceAndHour(") && plans.Contains("found == PlanFind.Shut") && plans.Contains("ShutWalk(")
              && Occurs(dialog, "SocialLife.PlaceAndHour(") == 3 && dialog.Contains("SocialLife.NextOpenSlot(_p, kind, venue, first + 180f)")
              && !dialog.Contains("SocialPlaces.Find(kind, SocialDirector.Where(), 40f, 1800f, out Venue venue)") && !dialog.Contains("SocialPlaces.Find(kind, player, 40f, 1800f, out Venue venue)"),
            "social, open hours: what the player asks for, what they invite to and the walk offered instead all find the place and the hour together");
        Check(flare.Contains("SocialLife.PlaceAndHour(p, a.Kind, SocialDirector.Where(), PlaceFrom, PlaceTo, a.From, out Venue venue, out float time)")
              && staged.Contains("out venue, null, plan.Time)"),
            "social, open hours: a plan made on Flare or in the street, and a place found again on the day, are open at the hour agreed");
    }
}
