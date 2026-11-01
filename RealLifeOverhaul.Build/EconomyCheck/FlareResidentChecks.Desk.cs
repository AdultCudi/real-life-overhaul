using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Population;

// Flare in the city, the desk session (the brief's eleventh step), game-free: a week's plan of a post a day, three strong posts or none, each on its
// day and nothing past the week; one plan a week for the whole account, so another office's session waits; a storm answered only by a plan that said
// so; a manager makes about their strength of the posts and answers, the player all; the save keeps the plan; and the session's kind, its words and
// its wiring read as text (in the sessions' registry while Flare in the city runs, delegable, at the player's offices).
internal static partial class Program
{
    private static void FlareResidentDesk()
    {
        var dueDays = new List<int>[3];
        for (int choice = 0; choice < 3; choice++)
        {
            FlarePlan p = FlareCityRules.MakePlan(10, choice, choice != FlareCityRules.PlanFew, 1000, false);
            dueDays[choice] = new List<int>();
            for (int day = 8; day <= 24; day++)
            {
                if (FlareCityRules.PlanDue(p, day))
                {
                    dueDays[choice].Add(day);
                    FlareCityRules.PlanPosted(p, day);
                }
            }
        }
        Check(string.Join(",", dueDays[FlareCityRules.PlanDaily]) == "10,11,12,13,14,15,16" && string.Join(",", dueDays[FlareCityRules.PlanFew]) == "10,12,14" && dueDays[FlareCityRules.PlanNone].Count == 0,
            "flare city desk: a post a day plans seven posts on the week's seven days, three strong posts one every other day, none nothing, and nothing past the week");
        FlarePlan daily = FlareCityRules.MakePlan(10, FlareCityRules.PlanDaily, true, 1000, false), few = FlareCityRules.MakePlan(10, FlareCityRules.PlanFew, false, 1000, false);
        FlarePlan late = FlareCityRules.MakePlan(10, FlareCityRules.PlanDaily, true, 1000, false);
        bool lateDue = FlareCityRules.PlanDue(late, 13);   // a load or a skip past its days: the next post due still, within the week
        Check(FlareCityRules.Planned(daily, 10) && FlareCityRules.Planned(daily, 16) && !FlareCityRules.Planned(daily, 17) && !FlareCityRules.Planned(daily, 9) && !FlareCityRules.Planned(new FlarePlan(), 10)
              && FlareCityRules.PlanAnswers(daily, 12) && !FlareCityRules.PlanAnswers(few, 12) && !FlareCityRules.PlanAnswers(daily, 17) && lateDue && !FlareCityRules.PlanDue(late, 17),
            "flare city desk: a plan stands for its week, so a session at another office waits for the next, and answers a storm in it only if it said so");
        int made = 0, answered = 0, player = 0;
        const int Trials = 2000;
        for (int i = 0; i < Trials; i++)
        {
            var manager = new FlarePlan { Manager = true, Strength = 600, Done = i % 7 };
            made += FlareCityRules.PlanMakes(manager, 42, i / 7) ? 1 : 0;
            answered += FlareCityRules.AnswerMade(true, 600, 42, i / 7, i % 7) ? 1 : 0;
            player += FlareCityRules.PlanMakes(new FlarePlan { Strength = 600 }, 42, i) && FlareCityRules.AnswerMade(false, 600, 42, i, 3) ? 1 : 0;
        }
        Check(Math.Abs(made * 1000 / Trials - 600) <= 40 && Math.Abs(answered * 1000 / Trials - 600) <= 40 && player == Trials,
            "flare city desk: a manager makes about their strength of the plan's posts and answers (" + made * 1000 / Trials + " and " + answered * 1000 / Trials + " per mille at 600), the player all of them");

        // the save keeps the week's plan
        var c = new FlareCityState { Seed = 5 };
        c.Plan = new FlarePlan { Day = 30, Posts = 3, Gap = 2, Done = 1, Made = 1, Next = 32, Strength = 750, Answer = true, Manager = true, Turn = 4 };
        FlareCityState back = FlareCityStore.Decode(FlareCityStore.Encode(c), out _);
        FlarePlan b = back != null ? back.Plan : null;
        Check(b != null && b.Day == 30 && b.Posts == 3 && b.Gap == 2 && b.Done == 1 && b.Made == 1 && b.Next == 32 && b.Strength == 750 && b.Answer && b.Manager && b.Turn == 4
              && new FlareCityState().Plan != null && !FlareCityRules.Planned(new FlareCityState().Plan, 0),
            "flare city desk: the save keeps the week's plan, and a new account has none");

        // the session read as text: its kind, its words, where it is held and when it is listed
        string scripts = ModScripts();
        string links = File.ReadAllText(System.IO.Path.Combine(scripts, "Wiring", "Wiring.FlareSessionLinks.cs"));
        string wiring = File.ReadAllText(System.IO.Path.Combine(scripts, "Wiring", "Wiring.cs"));
        string residents = File.ReadAllText(System.IO.Path.Combine(scripts, "Social", "Flare", "FlareDirector.Residents.cs"));
        string director = File.ReadAllText(System.IO.Path.Combine(scripts, "Social", "Flare", "FlareDirector.cs"));
        string desk = File.ReadAllText(System.IO.Path.Combine(scripts, "Social", "Flare", "FlareDirector.Desk.cs"));
        Check(links.Contains("Id = FlareCityRules.SessionId,") && links.Contains("Types = Offices,") && links.Contains("Delegable = true,") && links.Contains("OwnDesk = true,")
              && links.Contains("SessionRegistry.Add(_kind);") && links.Contains("SessionRegistry.Remove(FlareCityRules.SessionId);") && links.Contains("FlareBridges.Desk = Hold;")
              && links.Contains("SessionRules.Strength(ask.ByManager, ask.Skill)") && links.Contains("flare.DeskPlanned(ask.Day)") && wiring.Contains("FlareSessionLinks.Connect")
              && wiring.Contains("FlareSessionLinks.Disconnect") && residents.Contains("DeskHold(true);") && residents.Contains("DeskHold(false);") && director.Contains("PlanHour(day, hour);")
              && desk.Contains("Kind = FlareSource.Announce,") && desk.Contains("Tone = FlareTone.Honest,") && Regex.Matches(links, @"C\(""").Count == 7,
            "flare city sources: the desk session is the sessions' own kind, at the player's offices, delegable, listed while Flare in the city runs, its posts the player's announcements on the hour and a storm's answer honest");
        var keys = new List<string>();
        foreach (Match m in Regex.Matches(links, "\"(reallife_flare_session_[a-z_]+)\""))
        {
            keys.Add(m.Groups[1].Value);
        }
        var buttons = new List<string>();
        foreach (Match m in Regex.Matches(links, @"C\(""(reallife_flare_session_[a-z_]+)"""))
        {
            buttons.Add(m.Groups[1].Value);
            keys.Add(m.Groups[1].Value + "_trade");
        }
        keys.AddRange(new[] { "reallife_flare_session_done", "reallife_flare_session_storm_note", "reallife_flare_city_test_plan", "reallife_flare_city_test_plan_done" });
        Dictionary<string, string> en = LocaleFile("en");
        bool plain = buttons.Count == 7;
        foreach (string k in buttons)
        {
            plain &= en.TryGetValue(k, out string v) && !v.Contains("{");
        }
        string worst = en["reallife_session_note_manager"].Replace("{manager}", new string('m', 24)).Replace("{result}", en["reallife_flare_session_done"])
                       .Replace("{session}", en["reallife_flare_session_title"]).Replace("{posts}", "7").Replace("{answered}", "12").Replace("{storm}", en["reallife_flare_session_storm_ready"]);
        Check(AllIn(keys.ToArray(), out string missing) && keys.Count >= 25 && plain && worst.Length <= 240 && Fits(en, "reallife_flare_city_test_plan_done") && Fits(en, "reallife_flare_session_storm_note"),
            "flare city texts: the desk session's every word in en, pt and zh-cn, no button with a placeholder, a manager's notice within 240 characters (" + missing + ", " + worst.Length + ")");
    }
}
