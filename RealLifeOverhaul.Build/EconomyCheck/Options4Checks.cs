using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Tech;

// Four options and rules a player asked for (Scripts/Phone/Contacts/MomRules.cs, Careers/Study/StudyRules.Learning.cs,
// Licences/LicenceTerm.cs, Core/RivalMatchRules.cs and Tech/People/BulkRaiseRules.cs): Mom's own contact and its one a day,
// skills that do not fade, a licence that lasts two of the save's years, a raise for everyone on the team page and rivals'
// offers matched by themselves. The rules are run; the sources and the texts are read.
internal static partial class Program
{
    private static void Options4()
    {
        Options4Mom();
        Options4Skills();
        Options4Licence();
        Options4Raises();
        Options4Matches();
        Options4Texts();
    }

    // ---------------------------------------------------------------- Mom

    private static void Options4Mom()
    {
        Check(!MomRules.May(false, 10, 3) && !MomRules.May(false, 10, 0), "Mom: with her option off she never starts a contact, whatever the day");
        Check(MomRules.May(true, 10, 9) && MomRules.May(true, 10, 0) && !MomRules.May(true, 10, 10) && !MomRules.May(true, 0, 0),
            "Mom: with it on she may start one on a day she has not, and not a second the same day");
        int contacts = 0, last = 0;
        for (int day = 1; day <= 20; day++)
        {
            for (int channel = 0; channel < 6; channel++)   // a text, a ring, a visit, an introduction, a life event, a dinner text: every feature asks
            {
                if (MomRules.May(true, day, last))
                {
                    last = day;
                    contacts++;
                }
            }
        }
        Check(contacts == 20 && MomRules.MostUnpromptedPerDay == 1, "Mom: six features asking every day of twenty give twenty contacts, one a day");
        int burst = 0, lastOff = 0;
        for (int day = 1; day <= 30; day++)
        {
            bool on = day > 20;   // off for twenty days, then switched on
            if (MomRules.May(on, day, lastOff))
            {
                lastOff = day;
                burst++;
            }
        }
        Check(burst == 10, "Mom: nothing was kept for her while it was off, so switching it on again brings one contact a day and no burst");

        string life = ModSource(@"Social\SocialLife.cs"), plan = ModSource(@"Social\SocialDirector.Plan.cs"), contact = ModSource(@"Social\SocialDirector.Contact.cs");
        string meetings = ModSource(@"Social\SocialDirector.Meetings.cs"), relatives = ModSource(@"Family\Relatives.cs"), runner = ModSource(@"Life\LifeEventRunner.cs");
        string send = MemberText(life, "public static bool SendText(");
        Check(send.Contains("bool momStarts = person.IsFamily && !reply;") && send.Contains("if (momStarts && !MomContact.MayOn(Day))") && Occurs(send, "MomContact.Note(Day);") == 2,
            "Mom: every text of the social life to her is held to her option and the day's one contact, and noted when it goes, unless it answers the player");
        Check(MemberText(plan, "private bool StartRing(").Contains("person.IsFamily && !MomContact.MayOn(SocialLife.Day)") && MemberText(plan, "private bool StartRing(").Contains("MomContact.Note(SocialLife.Day);"),
            "Mom: her ring is held to the same rule and counts as her contact");
        Check(MemberText(contact, "private static bool Reachable(").Contains("(!p.IsFamily || MomContact.MayOn(day))") && contact.Contains("MomContact.MayOn(day) && Random.value < 0.5f ? mom : Contactor(hour)")
              && MemberText(meetings, "private static SocialPerson Visitor()").Contains("(!p.IsFamily || MomContact.MayOn(day))"),
            "Mom: she is not picked to text, to ask, to bring somebody or to drop by while she may not");
        Check(meetings.Contains("_pendingDropBy.IsFamily && !MomContact.MayOn(SocialLife.Day)") && meetings.Contains("MomContact.Note(SocialLife.Day);"),
            "Mom: a visit chosen earlier is dropped if she has made a contact since, and a visit counts as her contact");
        Check(contact.Contains("if (p.IsFamily && !MomContact.On)") && contact.Contains("p.AfterDay = 0;   // with Mom's own contact off"),
            "Mom: the day after text she owes is let go while her option is off, so none comes late when it is on again");
        int replies = Occurs(life, ", true, true);") + Occurs(plan, ", true, true);") + Occurs(contact, ", true, true);") + Occurs(plan, ", false, true))")
                      + Occurs(ModSource(@"Social\OutingScene.Meeting.cs"), ", true, true);") + Occurs(ModSource(@"Social\OutingScene.cs"), ", true, true);")
                      + Occurs(ModSource(@"Social\SocialDirector.cs"), "data, false, true))");
        Check(replies >= 12, "Mom: what answers the player (a plan, a debt, a loan back, a favour's thanks, a call missed) is marked as a reply and never held back (" + replies + ")");
        Check(runner.Contains("flags |= If(PhoneContacts.Mom != null && MomContact.May, Need.Mom);")
              && MemberText(runner, "public static void Run(LifeEvent lifeEvent)").Contains("if ((lifeEvent.Needs & Need.Mom) != 0)")
              && MemberText(runner, "public static void Run(LifeEvent lifeEvent)").Contains("MomContact.Note(TimeHelper.CurrentDay);"),
            "Mom: her life events are dealt only while she may get in touch, and dealing one counts as her contact");
        string daily = MemberText(relatives, "public static void Daily(int day)");
        Check(daily.Contains("PhoneContacts.Mom != null && MomContact.MayOn(day)") && MemberText(relatives, "private static void Invite(int which)").Contains("!MomContact.May")
              && MemberText(relatives, "private static DinnerPlace DinnerToday(").Contains("PhoneContacts.Mom != null && MomContact.May") && Occurs(relatives, "MomContact.May") >= 4
              && MemberText(relatives, "public static void DinnerMissed(Errand errand)").Contains("MomContact.May"),
            "Mom: the family's texts of hers (a visit, an invitation, the dinner day, a missed dinner) are held to the same rule");
        Check(MemberText(relatives, "public static void MomSays(string key)").Contains("&& MomContact.On") && MemberText(relatives, "public static void MomGift(").Contains("|| !MomContact.On"),
            "Mom: her words about the player's own news and her gift wait on her option alone, since they answer it");
        string settings = ModSource(@"Core\Settings.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), list = ModSource(@"Meta\FeatureList.Life.cs"), tree = ModSource(@"Meta\OptionsTree.cs");
        Check(settings.Contains("public const string FamilyMomContactId = \"family_mom_contact\";") && settings.Contains("public static bool FamilyMomContact = true;")
              && settings.Contains("FamilyMomContact = Bool(modId, FamilyMomContactId, FamilyMomContact);")
              && mod.Contains(".AddToggle(Settings.FamilyMomContactId, \"reallife_family_mom_contact_opt\", Settings.FamilyMomContact,") && list.Contains("Settings.FamilyMomContactId)")
              && tree.Contains(".FreeOpts(\"family_mom_contact\")"),
            "Mom: the option is in the family area, on by default, kept, listed and not grey with the family switch (she is the social life's too)");
        string state = ModSource(@"Core\ModState.cs");
        Check(state.Contains("data[KeyMomContactDay] = LastMomContactDay.ToString(CultureInfo.InvariantCulture);") && state.Contains("LastMomContactDay = (int)ReadFloat(data, KeyMomContactDay);")
              && state.Contains("LastMomContactDay = 0;"),
            "Mom: the day of her last contact is saved, read back and reset with the rest of the mod's state");
        Check(ModSource(@"Family\HardTimesWatch.cs").Contains("g.MomThere = Settings.FamilyRelatives && Settings.FamilyMomContact && PhoneContacts.Mom != null;")
              && ModSource(@"Family\HardTimesWatch.cs").Contains("Settings.FamilyRelatives && Settings.FamilyMomContact && s.MomDinnerDay == day"),
            "Mom: with her contact off the partner does not say she rang, and a dinner at hers nobody told of is not counted late");
        Check(!ModSource(@"Phone\PhoneContacts.cs").Contains("MomContact"), "Mom: she stays in the phone and her call is the player's, never held back by the option");
    }

    // ---------------------------------------------------------------- skills

    private static void Options4Skills()
    {
        StudyFile Idle()
        {
            var f = new StudyFile();
            f.Skill[0] = f.Best[0] = 60000;
            f.Used[0] = 10;
            return f;
        }
        StudyFile on = Idle();
        int lost = StudyRules.Fade(on, 10 + StudyCatalogue.FadeAfterDays + 1);
        Check(lost == StudyCatalogue.FadePerDay && StudyRules.Fading(on, 0, 10 + StudyCatalogue.FadeAfterDays + 1) && !StudyRules.Fading(Idle(), 0, 10 + StudyCatalogue.FadeAfterDays),
            "skills: with the option on a skill unused for more than the days fades by the day's share, as before");

        // Off for a hundred days: each midnight holds, so nothing is lost, and the option on again starts the count from that day.
        StudyFile held = Idle();
        for (int day = 11; day <= 110; day++)
        {
            StudyRules.Hold(held, day);
        }
        Check(held.Skill[0] == 60000 && held.FadeFrom == 110, "skills: a hundred midnights with the option off lose nothing and keep the last day");
        int back = 0;
        for (int day = 111; day <= 110 + StudyCatalogue.FadeAfterDays; day++)
        {
            back += StudyRules.Fade(held, day);
        }
        Check(back == 0 && !StudyRules.Fading(held, 0, 110 + StudyCatalogue.FadeAfterDays), "skills: switched on again it loses nothing in one go: the grace runs from the day it was on again");
        Check(StudyRules.Fade(held, 110 + StudyCatalogue.FadeAfterDays + 1) == StudyCatalogue.FadePerDay && StudyRules.Fading(held, 0, 110 + StudyCatalogue.FadeAfterDays + 1),
            "skills: and after those days a little fades each day again");
        StudyRules.Hold(held, 50);
        Check(held.FadeFrom == 110, "skills: the held day never goes back");
        StudyFile used = Idle();
        StudyRules.Use(used, 0, 100);
        StudyRules.Hold(used, 90);
        Check(!StudyRules.Fading(used, 0, 100 + StudyCatalogue.FadeAfterDays) && StudyRules.Fading(used, 0, 100 + StudyCatalogue.FadeAfterDays + 1),
            "skills: a skill used after the held day counts from its own day");

        StudyFile saved = Idle();
        saved.FadeFrom = 321;
        var data = new Dictionary<string, string> { { CareersStore.Key, CareersStore.Write(saved) } };
        StudyFile read = CareersStore.Load(data, out int version);
        Check(read != null && read.FadeFrom == 321 && read.Skill[0] == 60000 && version == CareersStore.Version, "skills: the held day is saved and read back with the file");

        string study = MemberText(ModSource(@"Careers\CareersDirector.Study.cs"), "private void StudyDay(int day)"), campus = ModSource(@"Careers\CampusApp.cs");
        Check(study.Contains("if (Settings.CareersSkillFade)") && study.Contains("faded = StudyRules.Fade(_study, day);") && study.Contains("StudyRules.Hold(_study, day);"),
            "skills: the night's pass fades them only with the option on, and holds the day with it off");
        Check(Occurs(campus, "Settings.CareersSkillFade && StudyRules.Fading(") == 2 && campus.Contains("\"reallife_campus_skills_about_steady\"")
              && !campus.Contains("StudyCatalogue.FadeLeast"), "skills: the Campus app marks a skill as fading only with the option on and says nothing fades otherwise");
        string settings = ModSource(@"Core\Settings.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), tree = ModSource(@"Meta\OptionsTree.cs");
        Check(settings.Contains("public const string CareersSkillFadeId = \"careers_skill_fade\";") && settings.Contains("public static bool CareersSkillFade = true;")
              && settings.Contains("CareersSkillFade = Bool(modId, CareersSkillFadeId, CareersSkillFade);")
              && mod.Contains(".AddToggle(Settings.CareersSkillFadeId, \"reallife_study_opt_skill_fade\", Settings.CareersSkillFade,")
              && ModSource(@"Meta\FeatureList.Life.cs").Contains("Settings.CareersClassStartId, Settings.CareersSkillFadeId)") && tree.Contains(".Opts(\"careers_class_start\", \"careers_skill_fade\")"),
            "skills: the option is on by default, kept, listed with the study part and on the options page");
        string store = ModSource(@"Careers\CareersStore.cs");
        Check(store.Contains(".Add(f.Taught).Add(f.FadeFrom);") && store.Contains("f.FadeFrom = Math.Max(0, h.Int());"), "skills: the held day is the last field of the file's head, so a save without it reads 0");
    }

    // ---------------------------------------------------------------- licence

    private static void Options4Licence()
    {
        Check(LicenceTerm.Days(60) == 120 && LicenceTerm.Days(120) == 240 && LicenceTerm.Days(365) == 730 && LicenceTerm.Days(61) == 122 && LicenceTerm.Days(0) == 120,
            "licence: two of the save's own years: 120 days in the game's 60, 240 in 120, 730 in 365, and the default year when no save says");
        bool floor = true;
        for (int year = -5; year <= 500; year++)
        {
            floor &= LicenceTerm.Days(year) >= LicenceTerm.FloorDays && LicenceTerm.Days(year) >= LicenceTerm.Years * Math.Max(1, Math.Min(365, year));
        }
        Check(floor && LicenceTerm.Days(8) == 120 && LicenceTerm.Days(30) == 120, "licence: never fewer than 120 days, whatever the year (a year of 8 or 30 days still gives 120)");
        Check(LicenceTerm.End(200, 0, 120) == 200 && LicenceTerm.End(200, 0, 240) == 320 && LicenceTerm.End(200, 120, 240) == 320 && LicenceTerm.End(500, 240, 240) == 500,
            "licence: a card held before terms were kept (term 0) runs the rest of its term as the longer one, and one given the long term is as it was");
        bool never = true;
        for (int stored = 1; stored < 900; stored += 37)
        {
            foreach (int term in new[] { 0, 120, 240, 730 })
            {
                foreach (int now in new[] { 120, 122, 240, 730 })
                {
                    never &= LicenceTerm.End(stored, term, now) >= stored;   // a year shortened since never takes a day off
                }
            }
        }
        Check(never, "licence: a licence already issued never ends sooner than it would have, whatever the year does after");
        int issued = 30, given = LicenceTerm.Days(120), end = LicenceTerm.End(issued + given, given, LicenceTerm.Days(120));
        int renewed = Math.Max(end, 200) + given;
        Check(end == 30 + 240 && renewed == 270 + 240, "licence: issued on day 30 in a 120-day year it runs 240 days, and a renewal runs from the old end");

        string state = ModSource(@"Licences\LicenceState.cs");
        Check(state.Contains("public static int ValidDays => LicenceTerm.Days(GameCalendar.DaysPerYear);") && !state.Contains("public const int ValidDays"),
            "licence: the term is read from the save's year as the calendar reads it, not a constant");
        Check(MemberText(state, "public static void Grant(").Contains("Term = term") && MemberText(state, "public static void Renew(").Contains("card.Expires = Math.Max(card.End, day) + term;")
              && MemberText(state, "public static void Renew(").Contains("card.Term = term;") && MemberText(state, "public static LicenceStanding Standing(").Contains("day <= card.End")
              && MemberText(state, "public static bool Renewable(").Contains("card.End"),
            "licence: issuing and renewing store the term given, and standing and renewal read the card's end");
        Check(state.Contains("case \"terms\":") && state.Contains("Pair(text, \"terms\", Cards[0].Term, Cards[1].Term, Cards[2].Term, Cards[3].Term, Cards[4].Term);")
              && state.IndexOf("HeavyRules.Write(text, Heavy);", StringComparison.Ordinal) < state.IndexOf("Pair(text, \"terms\"", StringComparison.Ordinal),
            "licence: the terms are saved after the heavy part, so an older RLO reads everything before and passes over them, and a save without them reads 0");
        bool readers = true;
        foreach (string file in new[] { @"Licences\LicenceDirector.cs", @"Licences\Driving\DrivingSchool.cs", @"Planner\Sources\WorkAndLawSources.cs" })
        {
            readers &= !ModSource(file).Contains("card.Expires") && ModSource(file).Contains("card.End");
        }
        Check(readers, "licence: the renewal notice, the card's line and the planner read the card's end, never the stored day");
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        string desk = ModSource(@"Licences\SchoolDesk.cs");
        Check(en != null && en["reallife_school_done_renew"].Contains("{days}") && !en["reallife_school_done_renew"].Contains("120") && en["reallife_school_pitch_renew"].Contains("{days}")
              && !en["reallife_school_pitch_renew"].Contains("120") && desk.Contains(".Replace(\"{days}\", LicenceLaw.ValidDays.ToString())")
              && desk.Contains("Licences.Toast(NotificationType.Success, done, \"days\", LicenceLaw.ValidDays.ToString(), 9f);"),
            "licence: every text that gives a number of days for the term shows the real figure ({days}), on the list and on the renewal's notice");
        string[] question = en != null ? en["reallife_lic_q_d64"].Split('|') : new string[0];
        Check(question.Length == 4 && !en["reallife_lic_q_d64"].Contains("120 days|") && question[1].Contains("Two years") && question[1].Contains("120 days if that is longer"),
            "licence: the written test's question names two years, true for every year the save can have");
        bool days = true;
        foreach (string key in new[] { "reallife_lic_start_with", "reallife_lic_grandfather_car", "reallife_lic_grandfather_both", "reallife_lic_grandfather_motorcycle", "reallife_lic_grandfather_heavy", "reallife_lic_sheet_pass" })
        {
            days &= en != null && en[key].Contains("{days}");
        }
        Check(days, "licence: the notices at a start and the pass sheet already take the term from {days}");
    }

    // ---------------------------------------------------------------- raises for everyone

    private static void Options4Raises()
    {
        var plan = new BulkRaiseRules.Plan();
        plan.Add(1000, 1050, false, false);   // raised
        plan.Add(2000, 0, true, false);       // raised lately
        plan.Add(3000, 0, false, true);       // on notice
        plan.Add(4000, 4000, false, false);   // at the top of the pay scale
        plan.Add(1500, 1575, false, false);   // raised
        Check(plan.Shown == 5 && plan.Raised == 2 && plan.Rested == 1 && plan.Leaving == 1 && plan.Capped == 1 && plan.Left == 3 && plan.Any
              && plan.BeforeHourly == 2500 && plan.AfterHourly == 2625,
            "raise for all: the plan counts who is raised and why each other is left out, and sums only those raised");
        // A month in a 120-day year is 10 days: 40 hours a week over 10 days is 57 hours.
        Check(plan.ExtraMonthly(10) == 125 * 57 && plan.TotalMonthly(10) == 2625 * 57 && RivalMatchRules.HoursInMonth(10) == 57 && RivalMatchRules.HoursInMonth(5) == 28
              && RivalMatchRules.HoursInMonth(0) >= 1 && RivalMatchRules.Monthly(100, 5) == 2800,
            "raise for all: the monthly cost is the hourly figure over a 40 hour week for the days of the save's month");
        Check(BulkRaiseRules.Percents.Length == 6 && BulkRaiseRules.Percents[BulkRaiseRules.StartAt] == 5 && BulkRaiseRules.Bp(0) == 300 && BulkRaiseRules.Bp(99) == 2000 && BulkRaiseRules.Bp(-4) == 300,
            "raise for all: 3, 5, 8, 10, 15 and 20 per cent, starting at 5, an index held to its ends");
        Check(!new BulkRaiseRules.Plan().Any && new BulkRaiseRules.Plan().Left == 0, "raise for all: an empty page plans nothing");

        string director = ModSource(@"Tech\TechDirector.Raises.cs");
        string give = MemberText(director, "internal BulkRaiseRules.Plan GiveRaises(");
        Check(give.Contains("TalentRules.CanRise(p, day)") && give.Contains("Raise(person, bp, day);") && give.Contains("TalentRules.Raised(p, day, bp);") && give.Contains("done.Add(before, after, rested, leaving);")
              && Occurs(give, "Tell(") == 1 && !give.Contains("Rested(person)"),
            "raise for all: each is one raise by the rule of one (the rest, the cap), with one notice for all and none for a person");
        string planned = MemberText(director, "internal BulkRaiseRules.Plan PlanRaises(");
        Check(planned.Contains("plan.Add(PaidNow(person), leaving || rested ? 0 : PaidAfter(person, bp), rested, leaving);") && planned.Contains("!TalentRules.CanRise(p, day)")
              && !planned.Contains("Raise(person") && !planned.Contains("Changed()"),
            "raise for all: the plan is read the same way and changes nothing");
        string old = ModSource(@"Tech\People\RolesApp.Team.cs");
        Check(old.Contains("_raiseIds.Add(p.Id);") && old.Contains("byState.Chosen < 0 || row.Kind2 == byState.Chosen") && old.Contains("business == null || person.Business == business")
              && old.Contains("InfoBox.Button(box.ButtonRow(), InfoBox.Positive, \"reallife_roles_button_raise_all\", () => ConfirmRaiseAll(plan, bp));")
              && MemberText(old, "private void ConfirmRaiseAll(").Contains("\"reallife_roles_confirm_raise_body\".Localize(new { count, percent, total, extra })")
              && MemberText(old, "private void ConfirmRaiseAll(").Contains("_director.GiveRaises(ids, bp)") && old.Contains("box.Switch(\"reallife_co_opt_match_rivals\", Settings.EconomyCompanyMatchRivals,")
              && old.Contains("Settings.SetMatchRivals(on);") && ModSource(@"Tech\People\RolesApp.cs").Contains("_table.Filtered = () =>"),
            "raise for all, old interface: the Team page has the share, the plan with the monthly cost, a question with the same figures before it is given, and the switch");
        string kit = ModSource(@"Ui\Work\RolesLive.People.cs"), kitMain = ModSource(@"Ui\Work\RolesLive.cs"), screens = ModSource(@"Ui\UiScreens.Work.cs");
        Check(kit.Contains("RaiseAll(d, view, t, w);") || kit.Contains("AddRaiseAll(d, view, t, w);"), "raise for all, kit: the Team page adds its raise");
        Check(kit.Contains("case RaiseAll: return AskRaiseAll(d, view);") && kit.Contains("case MatchRivals: Settings.SetMatchRivals(!Settings.EconomyCompanyMatchRivals); return true;")
              && MemberText(kit, "private bool AskRaiseAll(").Contains("\"reallife_roles_confirm_raise_body\".Localize(new { count, percent, total, extra })")
              && MemberText(kit, "private bool AskRaiseAll(").Contains("d.GiveRaises(ids, bp);") && kitMain.Contains("RaiseAll = 150, MatchRivals = 151;")
              && kit.Contains("t.Acts.Add(Act(MatchRivals, 0,") && kit.Contains("new WorkSection { Part = WorkPart.Amount, Title = w.T(\"reallife_roles_sec_raise\", \"Raise the team\"), Stepper = 0,"),
            "raise for all, kit: the Team page has the share, the stat for the monthly cost, a question with the same figures, and the switch as a button");
        bool keys = true;
        foreach (string key in new[] { "reallife_roles_sec_raise", "reallife_roles_raise_nobody", "reallife_roles_raise_none", "reallife_work_raise_note", "reallife_work_stat_raise", "reallife_work_raise_all",
                     "reallife_work_match_on", "reallife_work_match_off" })
        {
            keys &= screens.Contains("\"" + key + "\"");
        }
        Check(keys, "raise for all, kit: every text the page asks for by key is in the screen's list");
    }

    // ---------------------------------------------------------------- rivals' offers matched

    private static void Options4Matches()
    {
        Check(RivalMatchRules.Limit(-3, 50) == 0 && RivalMatchRules.Limit(18, 50) == 18 && RivalMatchRules.Limit(90, 50) == 50, "match rivals: the limit is the automatic raises' own, held to its ends");
        Check(!RivalMatchRules.WithinOffer(1000, 1050, 0) && RivalMatchRules.WithinOffer(1000, 1050, 5) && !RivalMatchRules.WithinOffer(1000, 1051, 5) && RivalMatchRules.WithinOffer(1000, 900, 1)
              && !RivalMatchRules.WithinOffer(1000, 0, 10),
            "match rivals: a staff offer is within the limit when it is no more than that share over the pay, and with the limit at 0 nothing is");
        Check(RivalMatchRules.WithinBp(1500, 15) && !RivalMatchRules.WithinBp(1500, 14) && !RivalMatchRules.WithinBp(1500, 0) && !RivalMatchRules.WithinBp(0, 15)
              && TalentCatalogue.PoachPremiumBp == 1500, "match rivals: a professional's offer, basis points over the pay, is held to the same limit");

        var tally = new RivalMatchTally();
        Check(!tally.Take(10, 5, out _, out _), "match rivals: nothing matched, nothing to say");
        tally.Add(10, 300);
        tally.Add(10, 450);
        tally.Add(10, 0);
        Check(!tally.Take(10, 0, out _, out _), "match rivals: not told in the hour of the night's pass itself");
        bool told = tally.Take(10, 1, out int count, out long monthly);
        Check(told && count == 3 && monthly == 750 && !tally.Take(10, 2, out _, out _), "match rivals: told once, from the first hour, with the count and the sum of what they cost a month");
        tally.Add(11, 100);
        tally.Add(12, 40);   // the first was never told: let go, never told as the next day's
        Check(tally.Take(12, 0, out count, out monthly) == false && tally.Take(12, 3, out count, out monthly) && count == 1 && monthly == 40,
            "match rivals: a day's matches are not added to the next day's");
        tally.Add(20, 10);
        Check(tally.Take(21, 0, out count, out _) && count == 1, "match rivals: matches left untold are told in the first hour of any later day");

        string company = ModSource(@"Economy\Company\CompanyDirector.Staff.cs"), rivals = ModSource(@"Economy\Company\CompanyDirector.Rivals.cs");
        string staff = MemberText(company, "private bool MatchRival(StaffFile f, int day)");
        Check(staff.Contains("Settings.EconomyCompanyMatchRivals") && staff.Contains("StaffRules.GivesWay(f)") && staff.Contains("Manages(f.Place, ManagerTask.Offers, f.Id)")
              && staff.Contains("RivalMatchRules.WithinOffer(f.Paid, f.PoachOffer, limit)") && staff.Contains("RivalMatchRules.Limit(Settings.EconomyCompanyAgreeUpTo, CompanyCatalogue.AgreeUpToMost)")
              && staff.Contains("StaffRules.OfferAnswer(f, true, day);") && staff.Contains("RivalMatches.Matched(day,") && !staff.Contains("Tell("),
            "match rivals, staff: the offer is answered as Match it answers it, only inside the limit, with the option on, where no manager answers offers, and no notice of its own");
        Check(MemberText(company, "private void Approach(int day)").Contains("if (MatchRival(f, day))") && rivals.Contains("if (MatchRival(best, Save.Day))")
              && company.Contains("RivalMatches.Flush(day, hour);"),
            "match rivals, staff: the company's own rivals and the tycoons' offers both go through it, and the hour tells the night's matches");
        string tech = ModSource(@"Tech\TechDirector.Raises.cs"), people = ModSource(@"Tech\TechDirector.People.cs");
        string match = MemberText(tech, "private bool MatchOffer(Person person, int day)");
        Check(match.Contains("!Settings.EconomyCompanyMatchRivals") && match.Contains("RivalMatchRules.WithinBp(p.PoachBp, limit)") && match.Contains("Raise(person, bp, day);")
              && match.Contains("TalentRules.Answer(p, true, day, Year);") && match.Contains("RivalMatches.Matched(day,") && !match.Contains("Tell("),
            "match rivals, professionals: the offer is answered as Match it answers it, inside the same limit, with no notice of its own");
        Check(people.Contains("if (MatchOffer(person, day))") && people.Contains("else if (++offersTold <= TalentCatalogue.NoticesMost)") && people.Contains("RivalMatches.Flush(day, Save.Hour);"),
            "match rivals, professionals: a matched offer is left out of the night's notices, and the hour tells the matches");
        string settings = ModSource(@"Core\Settings.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), tree = ModSource(@"Meta\OptionsTree.cs");
        Check(settings.Contains("public const string EconomyCompanyMatchRivalsId = \"economy_company_match_rivals\";") && settings.Contains("public static bool EconomyCompanyMatchRivals = false;")
              && settings.Contains("EconomyCompanyMatchRivals = Bool(modId, EconomyCompanyMatchRivalsId, EconomyCompanyMatchRivals);") && settings.Contains("public static void SetMatchRivals(bool on)")
              && mod.Contains(".AddToggle(Settings.EconomyCompanyMatchRivalsId, \"reallife_co_opt_match_rivals\", Settings.EconomyCompanyMatchRivals,")
              && ModSource(@"Meta\FeatureList.Economy.cs").Contains("Settings.EconomyCompanyMatchRivalsId") && tree.Contains(".Opt(\"economy_company_match_rivals\", \"economy_company_staff\")"),
            "match rivals: the option is off by default, kept, written from the page too, listed with the Company and needs staff pay on");
        string notice = ModSource(@"Core\RivalMatches.cs");
        string game = MemberText(notice, "private static void MatchGameOffers(int day)");
        Check(game.Contains("RivalMatchRules.WithinOffer(was, asked, limit)") && game.Contains("RivalOfferGuard.OfferOpen(e)") && game.Contains("Accept.Invoke(e, null);")
              && game.Contains("!Settings.EconomyCompanyMatchRivals") && game.Contains("limit <= 0") && game.Contains("GoingPublic.InSession && !GoingPublic.Mine(at)")
              && game.Contains("Matched(day,") && !game.Contains("Notifications") && notice.Contains("MatchGameOffers(day);")
              && ModSource(@"Core\GameContract.cs").Contains("\"AcceptPoachPayRaise\""),
            "match rivals, the game's own offers: answered through the game's own Accept, only inside the limit, only the player's own staff, counted in the same one notice, hourly and never per frame");
        Check(notice.Contains("\"reallife_rival_matches_notice\"") && Occurs(notice, "Notifications.Show(") == 1, "match rivals: one notice for the day's matches, from one place");
    }

    // ---------------------------------------------------------------- texts

    private static void Options4Texts()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        string[] keys =
        {
            "reallife_family_mom_contact_opt", "reallife_opt_tip_family_mom_contact", "reallife_study_opt_skill_fade", "reallife_opt_tip_careers_skill_fade", "reallife_campus_skills_about_steady",
            "reallife_co_opt_match_rivals", "reallife_opt_tip_economy_company_match_rivals", "reallife_rival_matches_notice", "reallife_roles_sec_raise", "reallife_roles_raise_plan", "reallife_roles_raise_left",
            "reallife_roles_raise_none", "reallife_roles_raise_nobody", "reallife_roles_button_raise_all", "reallife_roles_confirm_raise_title", "reallife_roles_confirm_raise_body",
            "reallife_roles_raised_all", "reallife_roles_match_note", "reallife_roles_match_note_off", "reallife_work_raise_note", "reallife_work_stat_raise", "reallife_work_raise_all",
            "reallife_work_match_on", "reallife_work_match_off"
        };
        bool all = en != null;
        for (int i = 0; all && i < keys.Length; i++)
        {
            all = en.TryGetValue(keys[i], out string text) && text.Length > 0 && !text.Contains('!') && !text.Contains((char)0x2014) && !text.Contains((char)0x2013);
        }
        Check(all, "options four: every new text is in en.json, with no exclamation mark and no dash");
        bool tips = en != null;
        foreach (string key in new[] { "reallife_opt_tip_family_mom_contact", "reallife_opt_tip_careers_skill_fade", "reallife_opt_tip_economy_company_match_rivals" })
        {
            tips &= en != null && en[key].Length <= 190 && en[key].Split(' ').Length <= 32;
        }
        Check(tips, "options four: each option's tip is at most 32 words and 190 characters");
        Check(en != null && en["reallife_rival_matches_notice"].Length <= 240 && en["reallife_roles_raised_all"].Length <= 240 && en["reallife_roles_raised_all"].Contains("{total}")
              && en["reallife_roles_raised_all"].Contains("{extra}") && en["reallife_rival_matches_notice"].Contains("{count}") && en["reallife_rival_matches_notice"].Contains("{cost}"),
            "options four: the notices are at most 240 characters and keep their figures");
        Check(en != null && en["reallife_roles_raise_plan"].Contains("{total}") && en["reallife_roles_raise_plan"].Contains("{extra}") && en["reallife_roles_confirm_raise_body"].Contains("{total}")
              && en["reallife_roles_confirm_raise_body"].Contains("{extra}") && en["reallife_roles_confirm_raise_title"].Contains("{count}") && en["reallife_roles_confirm_raise_title"].Contains("{percent}"),
            "options four: the page and the question both say the new monthly cost before anything is given");
        Check(en != null && en["reallife_guide_family"].Contains("<b>Mom gets in touch by herself</b>") && en["reallife_guide_careers_study"].Contains("<b>Skills fade without practice</b>")
              && en["reallife_guide_economy_company"].Contains("<b>Match rivals' offers automatically</b>") && en["reallife_guide_tech_people"].Contains("<b>Raise everyone shown</b>")
              && en["reallife_guide_licences"].Contains("two of your save's years"),
            "options four: the Guide's family, study, company, professionals and licences pages say each");
        Check(en != null && en["reallife_family_mom_contact_opt"] == "Mom gets in touch by herself" && en["reallife_study_opt_skill_fade"] == "Skills fade without practice"
              && en["reallife_co_opt_match_rivals"] == "Match rivals' offers automatically" && en["reallife_roles_button_raise_all"] == "Raise everyone shown",
            "options four: the names the Guide puts in bold are the options' and the button's own texts");
    }
}
