using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul.GameUi;

// A player late in a game (8 Nov 2026): so many of the mod's notifications pop up that they clutter the screen and each one
// is a hitch, and asked to switch kinds of them off, never the ones that matter. Every one of them went through the game's
// one toast maker (NotificationsUI.ShowNotification: a copy of its template, the text laid out, a sound, two fades and a
// coroutine), and nothing let a kind be kept from it. Now each kind but the police's and health's has a switch (Options,
// "Notifications that pop up"); one switched off goes straight into the notification list as the game would write it,
// whatever its sender asked, and is never made (Core/GameUi/NoticeMuteRules, Core/GameUi/NoticeMute). Warnings, errors, what
// is owed, the end of a life, what the mod says of itself in answer to the player (the options page, its apps, tools and
// services, Going Public, the camera), the driving test's words, the player's own reminders and the tests' answers always pop
// up, and so do the police's, the court's texts among them, which come as a notification when the Legal contact is not on the
// phone. First the rules on every mask of kinds, with what is owed, a death, the morning line and the court's texts each named
// by its key, never read from the rules' own table; then every notification the sources raise, by its key as written at the
// call or through a constant, a variable or a carrier's caller, every text sent through the Legal or a debt contact among them; the
// cost; the sources (the gate, the list, placing and taking off the gate, the morning line, the options page); the texts.
// Run with --notice-mute.
internal static partial class Program
{
    private static void NoticeMuting()
    {
        NoticeMuteRuleChecks();
        NoticeMuteKeyChecks();
        NoticeMutePace();
        NoticeMuteSources();
        NoticeMuteWords();
    }

    // One key of each kind with a switch, in NoticeMuteRules.Switchable's order, each one the kind's switch keeps back.
    private static readonly string[] NoticeMuteSamples =
    {
        "reallife_tow_coming", "reallife_econ_note_lent", "reallife_estate_note_pipe", "reallife_career_signed", "reallife_co_note_offer",
        "reallife_tech_note_breach", "reallife_ledger_note_delivered", "reallife_social_met", "reallife_home_morning"
    };

    // Kinds that have no switch: the police's (a scheme's inquiry among them) and health's; and keys that are not the mod's.
    private static readonly string[] NoticeMuteNever =
    {
        "reallife_law_wanted", "reallife_justice_toast_warrant", "reallife_crime_stop_fleeing", "reallife_scheme_note_caution", "reallife_scheme_note_letter",
        "reallife_biz_racket_start", "reallife_biz_visit_police", "reallife_health_caught", "reallife_pd_ill_1", "reallife_event_ambulance",
        "reallife_family_checkup_due", "reallife_travel_burnout_doctor", "notificationui_notification_insufficient_money",
        "buildingmanager_notification_business_closed", "reallife_", "Reallife_tow_coming", null, string.Empty
    };

    // What the mod says of itself in answer to the player, its welcome, the driving test's words and the player's reminders: each
    // of a kind with a switch, and none kept back by it. The review's cases first: the options page's replies (a test's answer
    // among them), the Guide's and Settings' when they cannot open at once, the bug report's, the performance tools', Going
    // Public's and the camera's.
    private static readonly string[] NoticeMuteAnswers =
    {
        "reallife_opt_text", "reallife_guide_not_here", "reallife_guide_waits", "reallife_set_not_here", "reallife_set_waits", "reallife_bug_waits",
        "reallife_perf_bench_started4", "reallife_perf_armed", "reallife_gp_toast_host", "reallife_gp_toast_absent", "reallife_gp_city_tell",
        "reallife_camera_other_toast", "reallife_staff_not_here", "reallife_staff_waits", "reallife_texts_status_off", "reallife_textsai_key_kept",
        "reallife_gp_city_shared", "reallife_gp_hotel_no_skip", "reallife_phone_layout_reset_done", "reallife_phone_moved", "reallife_windows_reset_done", "reallife_ui_apps_reset_done",
        "reallife_meta_welcome_note_update", "reallife_beta_note", "reallife_lic_toast", "reallife_lic_theory_passed", "reallife_lic_car_step_out",
        "reallife_lic_fee_returned", "reallife_planner_reminder", "reallife_planner_reminder_leave", "reallife_planner_welcome",
        "reallife_ui_note_interface_off"
    };

    // What is owed, the end of a life and the morning line's news, each key named here, never read from the rules' own table:
    // each is of a kind with a switch, so a line taken out of NoticeMuteRules' table keeps its key back and fails here.
    private static readonly string[] NoticeMuteOwed =
    {
        "reallife_util_toast",                                                    // every power, water and internet toast: the bills due, a supply cut off
        "reallife_econ_note_statement",                                           // the card's statement: what is due, the minimum and the day it is taken
        "reallife_econ_note_advice",                                              // debts long overdue, and the free advice
        "reallife_co_note_return_due",                                            // a company's tax return for the quarter, to be filed by a day
        "reallife_estate_note_trading_tax",                                       // the year's tax on the hotels and the car hire
        "reallife_civic_charge_fine_up", "reallife_civic_charge_fine_down",       // a fine the city's policy changed
        "reallife_civic_charge_ticket_up", "reallife_civic_charge_ticket_down",   // a parking ticket the city's policy changed
        "reallife_dynasty_hand_death", "reallife_dynasty_hand_retire",            // a death or a retirement in the family, and the estate passing on
        "reallife_dynasty_hand_done",
        "reallife_dynasty_elder_died",                                            // the old founder's death, and what their fund leaves
        "reallife_home_morning_news", "reallife_home_morning_more"                // the morning line with the news held while the player slept
    };

    // The court's and counsel's texts, each key named here: LegalPhone.Send puts them in the Legal contact's thread, or, when the
    // contact is not on the phone (the phone's contacts or the Legal contact off), shows them as the game's notification, an
    // information, under their own keys. A scheme's letter goes the same way. Each is the police's, which has no switch.
    private static readonly string[] NoticeMuteCourt =
    {
        "reallife_text_justice_date", "reallife_text_justice_tomorrow", "reallife_text_justice_today", "reallife_text_justice_warrant",
        "reallife_text_justice_judgment", "reallife_text_justice_dropped", "reallife_text_justice_dismissed", "reallife_text_justice_sealed",
        "reallife_text_justice_community_missed", "reallife_text_justice_written_off_hours", "reallife_text_justice_written_off",
        "reallife_text_justice_suit_property", "reallife_text_justice_suit_injury", "reallife_text_justice_suit_won", "reallife_text_justice_suit_lost",
        "reallife_text_justice_suit_default", "reallife_text_justice_suit_debt", "reallife_text_justice_suit_withdrawn", "reallife_text_justice_suit_unpaid",
        "reallife_text_justice_suit_build", "reallife_text_justice_suit_investor", "reallife_text_justice_suit_restraint", "reallife_text_justice_suit_rival",
        "reallife_text_justice_suit_rival_withdrawn", "reallife_text_justice_suit_franchise", "reallife_text_justice_suit_franchise_withdrawn",
        "reallife_text_justice_claim_filed", "reallife_text_justice_claim_won", "reallife_text_justice_claim_lost", "reallife_scheme_text_letter"
    };

    private const int NoticeMuteMasks = 1 << NoticeKinds.Count;   // every set of kinds, those without a switch among them

    private static int NoticeMuteAll()
    {
        int mask = 0;
        foreach (NoticeKind kind in NoticeMuteRules.Switchable)
        {
            mask |= NoticeMuteRules.Bit(kind);
        }
        return mask;
    }

    // ---------------------------------------------------------------- the rules

    private static void NoticeMuteRuleChecks()
    {
        int all = NoticeMuteAll();
        NoticeKind[] kinds = NoticeMuteRules.Switchable;
        var distinct = new HashSet<NoticeKind>(kinds);
        Check(kinds.Length == 9 && distinct.Count == 9 && !distinct.Contains(NoticeKind.None) && !distinct.Contains(NoticeKind.Police) && !distinct.Contains(NoticeKind.Health),
            "notice mute: nine kinds have a switch, each once; the police, health and what is not the mod's have none");
        bool switches = true;
        foreach (NoticeKind kind in Enum.GetValues(typeof(NoticeKind)))
        {
            switches &= NoticeMuteRules.HasSwitch(kind) == distinct.Contains(kind);
        }
        Check(switches, "notice mute: a kind has a switch exactly when the options page lists one for it (a new kind needs its own switch, or none)");
        Check(NoticeMuteRules.With(0, NoticeKind.Road, false) == NoticeMuteRules.Bit(NoticeKind.Road) && NoticeMuteRules.With(NoticeMuteRules.Bit(NoticeKind.Road), NoticeKind.Road, true) == 0
              && NoticeMuteRules.With(NoticeMuteRules.Bit(NoticeKind.Road), NoticeKind.Road, false) == NoticeMuteRules.Bit(NoticeKind.Road)
              && NoticeMuteRules.With(0, NoticeKind.Police, false) == 0 && NoticeMuteRules.With(0, NoticeKind.Health, false) == 0 && NoticeMuteRules.With(0, NoticeKind.None, false) == 0,
            "notice mute: a switch off sets its kind's bit and on clears it; the police's and health's bits are never set");

        bool own = true, others = true, urgent = true, samples = true;
        for (int i = 0; i < kinds.Length; i++)
        {
            string key = NoticeMuteSamples[i];
            own &= NoticeKinds.Of(key) == kinds[i] && NoticeMuteRules.Muted(key, false, NoticeMuteRules.Bit(kinds[i])) && NoticeMuteRules.Muted(key, false, all);
            others &= !NoticeMuteRules.Muted(key, false, all & ~NoticeMuteRules.Bit(kinds[i])) && !NoticeMuteRules.Muted(key, false, 0);
            urgent &= !NoticeMuteRules.Muted(key, true, all);
            string sample = "reallife_notice_test_" + kinds[i].ToString().ToLowerInvariant();
            samples &= NoticeMuteRules.Muted(sample, false, NoticeMuteRules.Bit(kinds[i])) && !NoticeMuteRules.Muted(sample, false, all & ~NoticeMuteRules.Bit(kinds[i]));
        }
        Check(own, "notice mute: a notification of each kind is kept back when its own kind is off");
        Check(others, "notice mute: a kind's notification pops up while its own kind is on, whatever the others say");
        Check(urgent, "notice mute: a warning or an error always pops up, every kind off");
        Check(samples && !NoticeMuteRules.Muted("reallife_notice_test_police", false, all) && !NoticeMuteRules.Muted("reallife_notice_test_health", false, all),
            "notice mute: the options page's samples go as their kind goes, the police's and health's always pop up");

        bool never = true;
        string caught = "none";
        for (int mask = 0; mask < NoticeMuteMasks; mask++)
        {
            foreach (string key in NoticeMuteNever)
            {
                if (NoticeMuteRules.Muted(key, false, mask))
                {
                    never = false;
                    caught = (key ?? "null") + " under mask " + mask;
                }
            }
        }
        Check(never, "notice mute: the police's, health's and anything not the mod's pop up under every one of " + NoticeMuteMasks + " sets of kinds off (" + caught + ")");

        // What is owed, a death and the morning line, key by key as named above (never the rules' own table): each asked of the rule
        // the gate asks, as an information, under every set of kinds off.
        bool owed = true, needed = true;
        string owedOne = "none", spareOne = "none";
        foreach (string key in NoticeMuteOwed)
        {
            if (!NoticeMuteRules.HasSwitch(NoticeKinds.Of(key)))
            {
                needed = false;
                spareOne = key + " is " + NoticeKinds.Of(key);
            }
            for (int mask = 0; mask < NoticeMuteMasks; mask++)
            {
                if (NoticeMuteRules.Muted(key, false, mask))
                {
                    owed = false;
                    owedOne = key + " under mask " + mask;
                    break;
                }
            }
        }
        Check(owed, "notice mute: the bills, the card's statement, debts long overdue, a tax return, the trading tax, a fine or a ticket the city's policy changed, a death "
                    + "or a retirement and the estate passing on, and the morning line's news pop up under every one of " + NoticeMuteMasks + " sets of kinds off, each key named ("
                    + owedOne + ")");
        Check(needed, "notice mute: each of those is of a kind with a switch, so only the table of what always pops up keeps it from the switches and the check above can fail ("
                      + spareOne + ")");

        // The court's and counsel's texts and a scheme's letter, key by key as named above: the police's, and so never kept back,
        // also as the information they come as when the Legal contact is not on the phone.
        bool court = true;
        string courtOne = "none";
        foreach (string key in NoticeMuteCourt)
        {
            if (NoticeKinds.Of(key) != NoticeKind.Police)
            {
                court = false;
                courtOne = key + " is " + NoticeKinds.Of(key);
                continue;
            }
            for (int mask = 0; mask < NoticeMuteMasks; mask++)
            {
                if (NoticeMuteRules.Muted(key, false, mask))
                {
                    court = false;
                    courtOne = key + " under mask " + mask;
                    break;
                }
            }
        }
        Check(court, "notice mute: a court date, a warrant, a judgment, a suit and the rest of the court's and counsel's texts, and a scheme's letter, are the police's and pop up "
                     + "under every one of " + NoticeMuteMasks + " sets of kinds off, also as the notification they come as without the Legal contact, each key named (" + courtOne + ")");

        bool table = true;
        foreach (string prefix in NoticeMuteRules.AlwaysPrefixes())
        {
            string key = NoticeKinds.ModPrefix + prefix + (prefix.EndsWith("_", StringComparison.Ordinal) ? "x" : string.Empty);
            table &= NoticeMuteRules.Always(key) && NoticeMuteRules.HasSwitch(NoticeKinds.Of(key));
        }
        Check(table && NoticeMuteRules.AlwaysPrefixes().Length >= 30,
            "notice mute: each line of the table of what always pops up is of a kind with a switch, so none stands there for nothing (the keys it keeps are each named in these checks)");
        Check(!NoticeMuteRules.Always("reallife_econ_note_lent") && !NoticeMuteRules.Always("reallife_util") && !NoticeMuteRules.Always("util_toast") && !NoticeMuteRules.Always(null)
              && !NoticeMuteRules.Always("xreallife_util_toast") && NoticeMuteRules.Always("reallife_util_toast"),
            "notice mute: only the mod's own keys that begin with what always pops up are kept from the switches");
        Check(!NoticeMuteRules.Muted("reallife_notice_mute_test_done", false, all) && !NoticeMuteRules.Muted("reallife_career_test_promoted", false, all)
              && !NoticeMuteRules.Muted("reallife_lic_test_granted", false, all) && NoticeMuteRules.TestAnswer("reallife_lic_test_granted") && !NoticeMuteRules.TestAnswer("reallife_notice_test_road"),
            "notice mute: a test's answer and the driving test's words always pop up; the samples are no answers");
        Check(!NoticeMuteRules.Muted("reallife_home_morning_news", false, all) && !NoticeMuteRules.Muted("reallife_home_morning_more", false, all)
              && NoticeMuteRules.Muted("reallife_home_morning", false, NoticeMuteRules.Bit(NoticeKind.Other)),
            "notice mute: the morning line with the night's news pops up (it holds only kinds that pop up); the greeting alone goes as the rest of the mod's");

        bool answered = true;
        string swallowed = "none";
        foreach (string key in NoticeMuteAnswers)
        {
            if (!NoticeMuteRules.HasSwitch(NoticeKinds.Of(key)) || !NoticeMuteRules.Always(key) || NoticeMuteRules.Muted(key, false, all))
            {
                answered = false;
                swallowed = key;
            }
        }
        Check(answered, "notice mute: what the mod says of itself in answer to the player (the options page, its apps, tools and services, Going Public, the camera), its welcome, "
                        + "the driving test's words and the player's own reminders pop up with every kind off (" + swallowed + ")");
        Check(NoticeMuteRules.Muted("reallife_staff_batch_hired", false, all) && NoticeMuteRules.Muted("reallife_staff_offer_gave_way", false, all)
              && NoticeMuteRules.Muted("reallife_planner_note_done", false, all) && NoticeMuteRules.Muted("reallife_lic_granted", false, all)
              && NoticeMuteRules.Muted("reallife_staff_batch_hired", false, NoticeMuteRules.Bit(NoticeKind.Other)),
            "notice mute: only those answers are kept from the switches: a hire in the Staff app, a planner's note and a licence go as their kinds go");

        var words = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "amount", "$10" } };
        Dictionary<string, string> copy = NoticeMuteRules.ListData(words, false);
        Dictionary<string, string> asked = NoticeMuteRules.ListData(words, true);
        words["amount"] = "$20";
        Check(ReferenceEquals(asked, words) && copy != null && !ReferenceEquals(copy, words) && copy.Count == 1 && copy.TryGetValue("AMOUNT", out string kept) && kept == "$10"
              && NoticeMuteRules.ListData(null, false) == null && NoticeMuteRules.ListData(null, true) == null,
            "notice mute: one kept back goes into the list whatever its sender asked: with its own words when the sender asked for the list, with a copy of them "
            + "when it did not (so the sender may use its words again), with none when it had none");
    }

    // ---------------------------------------------------------------- every notification the sources raise, wherever its key comes from

    private static void NoticeMuteKeyChecks()
    {
        Dictionary<string, string> en = LocaleFile("en");
        foreach (string prefix in NoticeMuteRules.AlwaysPrefixes())
        {
            int matched = 0;
            foreach (string key in en.Keys)
            {
                matched += key.StartsWith(NoticeKinds.ModPrefix + prefix, StringComparison.Ordinal) ? 1 : 0;
            }
            Check(matched > 0, "notice mute: what always pops up names keys the mod has (" + prefix + " matches " + matched + ")");
        }

        // Each key named for what is owed, a death, the morning line and the court is a text of the mod's, and every court text the
        // mod has is named, so a new one cannot be missed.
        var courtKeys = new HashSet<string>(NoticeMuteCourt, StringComparer.Ordinal);
        string untold = "none", unnamed = "none";
        foreach (string key in NoticeMuteOwed)
        {
            untold = en.ContainsKey(key) ? untold : key;
        }
        foreach (string key in NoticeMuteCourt)
        {
            untold = en.ContainsKey(key) ? untold : key;
        }
        foreach (string key in en.Keys)
        {
            unnamed = key.StartsWith("reallife_text_justice_", StringComparison.Ordinal) && !courtKeys.Contains(key) ? key : unnamed;
        }
        Check(untold == "none" && unnamed == "none" && courtKeys.Count == NoticeMuteCourt.Length,
            "notice mute: each key named for what is owed, a death, the morning line and the court has a text, and every court text in en.json is named (no text: "
            + untold + "; not named: " + unnamed + ")");

        List<NoticeSite> sites = NoticeMuteSites();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        int carried = 0;
        foreach (NoticeSite site in sites)
        {
            keys.Add(site.Key);
            carried += site.How != NoticeHow.Written ? 1 : 0;
        }
        Check(keys.Count >= 1000 && carried >= 150,
            "notice mute: the notifications the sources raise were read by their keys, written at the call or through a constant, a variable or a carrier's caller ("
            + keys.Count + " keys at " + sites.Count + " places, " + carried + " of them not written at the call; " + _noticeMuteUnread + " keys made in code, not read)");

        int all = NoticeMuteAll();
        int noSwitch = 0, wrong = 0, calm = 0, calmKept = 0, missing = 0;
        string wrongOne = "none", noSwitchOne = "none", missingOne = "none";
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (NoticeSite site in sites)
        {
            if (!seen.Add((site.Calm ? "calm " : "urgent ") + site.Key))
            {
                continue;
            }
            bool urgent = !site.Calm;
            string key = site.Key;
            NoticeKind kind = NoticeKinds.Of(key);
            bool kept = NoticeMuteRules.Muted(key, urgent, all);
            if (!NoticeMuteRules.HasSwitch(kind) && kept)
            {
                noSwitch++;
                noSwitchOne = key;
            }
            foreach (NoticeKind other in NoticeMuteRules.Switchable)
            {
                if (other != kind && NoticeMuteRules.Muted(key, urgent, NoticeMuteRules.Bit(other)))
                {
                    wrong++;
                    wrongOne = key + " by " + other;
                }
            }
            if (!urgent && NoticeMuteRules.HasSwitch(kind))
            {
                calm++;
                calmKept += kept ? 1 : 0;
            }
            if (urgent && kept)
            {
                missing++;
                missingOne = key;
            }
        }
        Check(noSwitch == 0, "notice mute: no notification of the police, health or the game's own is kept back by any switch (" + noSwitchOne + ")");
        Check(wrong == 0, "notice mute: no notification is kept back by another kind's switch (" + wrong + ": " + wrongOne + ")");
        Check(missing == 0, "notice mute: no warning or error in the sources is kept back (" + missingOne + ")");
        Check(calm > 500 && calmKept * 2 > calm, "notice mute: most of the calm notifications of the kinds with a switch can be kept back ("
              + calmKept + " of " + calm + "): what always pops up is the exception");

        // Every key the sources send through the Legal contact, read where it is sent (LegalPhone.Send, whose key is the
        // notification's when the contact is not on the phone): a court text named here, never kept back with every kind off.
        int legal = 0;
        string stray = "none";
        foreach (NoticeSite site in sites)
        {
            if (!NoticeSentBy(site, "LegalPhone."))
            {
                continue;
            }
            legal++;
            if (!courtKeys.Contains(site.Key) || NoticeMuteRules.Muted(site.Key, false, all))
            {
                stray = site.Key + " at " + site.File + ":" + NoticeLine(site);
            }
        }
        Check(legal >= 25 && stray == "none",
            "notice mute: every text the sources send through the Legal contact, a notification when the contact is not on the phone, is a court text named here "
            + "and pops up with every kind off (" + legal + " read; not: " + stray + ")");

        // Every key the sources send through a debt contact (DebtPhone.Text: what the private lender says is owed and when), read
        // where it is sent and asked of the rule as the gate asks it, by the type its notification has without the contact.
        int debts = 0;
        string loose = "none";
        foreach (NoticeSite site in sites)
        {
            if (!NoticeSentBy(site, "DebtPhone."))
            {
                continue;
            }
            debts++;
            if (NoticeMuteRules.Muted(site.Key, !site.Calm, all))
            {
                loose = site.Key + " at " + site.File + ":" + NoticeLine(site);
            }
        }
        Check(debts >= 3 && loose == "none",
            "notice mute: every text the sources send through a debt contact pops up with every kind off as the notification it is when the contact is not on the phone ("
            + debts + " read; not: " + loose + ")");

        // The answers to the player's own presses, wherever their keys come from: nothing raised in these places is kept back
        // with every kind off (the options page and the mod's apps and tools in Meta, Going Public and the shared city, the
        // camera's handover, the performance tools, the texts service, the phone's layout, the windows' reset), nor the driving
        // test's words, nor a reminder the player set.
        string[] answering =
        {
            "Meta/", "Core/GoingPublic.cs", "Core/SharedCity.cs", "Camera/CameraYield.cs", "Performance/", "Texts/", "Core/GameUi/PhoneMove.cs",
            "Ui/UiGameWindowPrefs.cs", "Licences/TheoryTest.cs", "Licences/Driving/PracticalTest", "Licences/Driving/TestCar.cs", "Planner/PlannerDirector.cs"
        };
        int answers = 0, swallowed = 0;
        string swallowedOne = "none";
        foreach (NoticeSite site in sites)
        {
            bool answer = false;
            foreach (string place in answering)
            {
                answer |= site.File.StartsWith(place, StringComparison.Ordinal);
            }
            if (!answer)
            {
                continue;
            }
            answers++;
            if (site.Calm && NoticeMuteRules.Muted(site.Key, false, all))
            {
                swallowed++;
                swallowedOne = site.Key + " at " + site.File + ":" + NoticeLine(site);
            }
        }
        Check(answers >= 100 && swallowed == 0,
            "notice mute: nothing the options page, the mod's apps and tools, Going Public, the shared city, the camera's handover, the performance tools, the texts service, "
            + "the phone's layout, the windows' reset, the driving test or the Planner's reminders say is kept back by a switch (" + answers + " read, " + swallowed + " kept: " + swallowedOne + ")");

        // The review's cases, each read where it is raised (through a constant, a variable or a carrier's caller where it is
        // not written at the call), and none kept back.
        (string File, string Key)[] cases =
        {
            ("Meta/OptionsNotice.cs", "reallife_opt_text"), ("Meta/GuideApp.cs", "reallife_guide_not_here"), ("Meta/GuideApp.cs", "reallife_guide_waits"),
            ("Meta/SettingsApp.cs", "reallife_set_not_here"), ("Meta/SettingsApp.cs", "reallife_set_waits"), ("Meta/BugReport.cs", "reallife_bug_waits"),
            ("Core/GoingPublic.cs", "reallife_gp_toast_guest"), ("Core/GoingPublic.cs", "reallife_gp_toast_absent"), ("Core/SharedCity.cs", "reallife_gp_city_tell"),
            ("Camera/CameraYield.cs", "reallife_camera_other_toast"), ("Performance/PerfDirector.cs", "reallife_perf_armed"),
            ("Performance/PerfBench.cs", "reallife_perf_bench_started4"), ("Planner/PlannerDirector.cs", "reallife_planner_reminder_leave"),
            ("Licences/TheoryTest.cs", "reallife_lic_theory_right")
        };
        int found = 0;
        string lost = "none";
        foreach ((string file, string key) in cases)
        {
            bool read = false;
            foreach (NoticeSite site in sites)
            {
                read |= site.File == file && site.Key == key && site.Calm && !NoticeMuteRules.Muted(key, false, all);
            }
            if (read)
            {
                found++;
            }
            else
            {
                lost = key + " at " + file;
            }
        }
        Check(found == cases.Length, "notice mute: each answer the review found kept back is read where it is raised and pops up with every kind off ("
              + found + " of " + cases.Length + "; not: " + lost + ")");

        string notice = ModSource(@"Meta\OptionsNotice.cs");
        Match text = Regex.Match(notice, "const string TextKey = \"(reallife_[a-z0-9_]+)\";");
        Check(text.Success && NoticeMuteRules.Always(text.Groups[1].Value) && !NoticeMuteRules.Muted(text.Groups[1].Value, false, all)
              && CodeAt(notice, "Notifications.Show(NotificationType.Info, TextKey, data, Seconds, null, null, true, false);") > 0,
            "notice mute: the options page's replies (OptionsNotice: a preset, an import, a reset, the set-up check, the bug report, a test's answer, a test that needs a city) "
            + "always pop up (" + (text.Success ? text.Groups[1].Value : "no key") + ")");
    }

    // How a key was read: written at the call, a constant, a variable of the method, or at the caller of a carrier (a method that
    // hands one of its parameters on as a notification's key: Licences.Toast, PerfDirector.Notify, MorningNews.Show).
    private enum NoticeHow
    {
        Written,
        Constant,
        Variable,
        Caller
    }

    private sealed class NoticeSite
    {
        public string File;     // under Scripts, with forward slashes
        public string Text;     // the file's text, for the line
        public int At;          // where the call is in it
        public string Key;
        public bool Calm;       // an information or a success, or a type not written at the call: a switch may keep it back
        public NoticeHow How;
    }

    private sealed class NoticeFile
    {
        public string Name;
        public string Text;            // a generic's commas made middle dots, so they never part a call's arguments or a method's parameters
        public int[] HeadAt;           // where each method's head begins (its line), in order
        public int[] HeadNameAt;       // where its name is
        public string[] HeadName;
        public string[] HeadParameters;
        public int[] ClassAt;
        public string[] ClassName;
    }

    private sealed class NoticeScan
    {
        public readonly List<NoticeFile> Files = new List<NoticeFile>(4096);
        public readonly Dictionary<string, string> Constants = new Dictionary<string, string>(StringComparer.Ordinal);                // "file|Name": its value
        public readonly Dictionary<string, HashSet<string>> Anywhere = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal); // Name: each value the mod gives it
        public readonly Dictionary<string, HashSet<string>> Classes = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);  // a class: the files it is written in
        public readonly Dictionary<(string Method, string Class, int Index), bool> Carriers = new Dictionary<(string, string, int), bool>();  // calm when its own type is
        public readonly List<(string Method, string Class, int Index)> Waiting = new List<(string, string, int)>();
        public readonly List<NoticeSite> Sites = new List<NoticeSite>(8192);
        public readonly HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
        public int Unread;
    }

    private static List<NoticeSite> _noticeMuteSites;   // read once for these checks
    private static int _noticeMuteUnread;

    private static readonly Regex NoticeGeneric = new Regex(@"<\s*[\w.]+(?:\s*,\s*[\w.]+)+\s*>", RegexOptions.Compiled);
    private static readonly Regex NoticeConstant = new Regex("\\bconst\\s+string\\s+(\\w+)\\s*=\\s*\"([^\"\\\\]*)\"\\s*;", RegexOptions.Compiled);
    private static readonly Regex NoticeHead = new Regex(@"^[ \t]*(?:(?:public|private|internal|protected|static|override|virtual|sealed|async|unsafe|new|extern|partial)[ \t]+)+"
                                                         + @"[\w.]+(?:<[^>\n]*>)?(?:\[\])*\??[ \t]+(\w+)[ \t]*\(([^)]*)\)", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex NoticeClass = new Regex(@"^[ \t]*(?:(?:public|private|internal|protected|static|sealed|abstract|partial|readonly|unsafe|new|ref)[ \t]+)*(?:class|struct)[ \t]+(\w+)",
                                                          RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex NoticeCall = new Regex(@"\bNotifications\s*\.\s*(Show|ShowError)\s*\(|\b(\w+)\s*\((?=\s*(?:[^;(){}]{0,120}?\?\s*)?(?:UI\.Notification\.)?NotificationType\.)",
                                                         RegexOptions.Compiled);
    private static readonly Regex NoticeTypeArgument = new Regex(@"^(?:[^?]*\?\s*)?(?:UI\.Notification\.)?NotificationType\.\w+"
                                                                 + @"(?:\s*:\s*(?:[^?:]*\?\s*)?(?:UI\.Notification\.)?NotificationType\.\w+)*$", RegexOptions.Compiled);
    private static readonly Regex NoticeTypeName = new Regex(@"NotificationType\.(\w+)", RegexOptions.Compiled);
    private static readonly Regex NoticeKeyShape = new Regex(@"^reallife_[a-z0-9_]+$", RegexOptions.Compiled);
    private static readonly Regex NoticeName = new Regex(@"^(?:@?[A-Za-z_]\w*\s*\.\s*)*@?([A-Za-z_]\w*)$", RegexOptions.Compiled);
    private static readonly Regex NoticeNamed = new Regex(@"^\w+\s*:(?!:)", RegexOptions.Compiled);
    private static readonly Regex NoticeParameter = new Regex(@"(\w+)\s*(?:=[^,]*)?$", RegexOptions.Compiled);
    private static readonly Regex NoticeWritten = new Regex("NotificationType\\.(Info|Success|Warning|Error)\\s*,\\s*\"(reallife_[a-z0-9_]+)\"", RegexOptions.Compiled);

    // Every notification the mod's sources raise, by its key: each call of the game's Notifications.Show (its second argument) or
    // ShowError (its first), and of any method whose arguments are a notification's type and then its key. A key is read as
    // written there, a choice of keys (?:), a constant (the file's own, or Class.Name when the mod gives the name one value), or a
    // variable of the method given those; a parameter of the method makes the method a carrier, whose callers are read in turn,
    // three steps deep. A key made in code (a call, joined texts) is not read, and counted. Then every type and key written side
    // by side, as the first reading did, so nothing it read is lost.
    private static List<NoticeSite> NoticeMuteSites()
    {
        if (_noticeMuteSites != null)
        {
            return _noticeMuteSites;
        }
        var scan = new NoticeScan();
        _noticeMuteSites = scan.Sites;
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        if (!System.IO.Directory.Exists(scripts))
        {
            return scan.Sites;
        }
        foreach (string path in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            NoticeFile file = NoticeRead(scan, System.IO.Path.GetRelativePath(scripts, path).Replace('\\', '/'), System.IO.File.ReadAllText(path));
            scan.Files.Add(file);
        }

        var found = new List<(string Key, NoticeHow How)>(8);
        foreach (NoticeFile file in scan.Files)
        {
            if (file.Text.IndexOf("NotificationType.", StringComparison.Ordinal) < 0 && file.Text.IndexOf("Notifications", StringComparison.Ordinal) < 0)
            {
                continue;
            }
            foreach (Match m in NoticeCall.Matches(file.Text))
            {
                List<string> args = NoticeArguments(file.Text, m.Index + m.Length);
                bool game = m.Groups[1].Success;
                string type, key;
                if (game && m.Groups[1].Value == "ShowError")
                {
                    type = "NotificationType.Error";
                    key = args.Count > 0 ? args[0] : string.Empty;
                }
                else if (args.Count > 1 && NoticeTypeArgument.IsMatch(args[0]))
                {
                    type = args[0];
                    key = args[1];
                }
                else if (game && args.Count > 1)
                {
                    type = args[0];
                    key = args[1];
                }
                else
                {
                    continue;
                }
                bool calm = NoticeCalm(type);
                found.Clear();
                // The game's own call: its key argument is a key, whatever it is. Another's with a type first: only a key written
                // there, a choice of them or a constant (a variable there is read when the method is found to be a carrier).
                if (!NoticeResolve(scan, file, key, m.Index, calm, game, found, NoticeHow.Written) && game)
                {
                    scan.Unread++;
                }
                foreach ((string k, NoticeHow how) in found)
                {
                    NoticeAdd(scan, file, m.Index, k, calm, how);
                }
            }
        }

        var done = new HashSet<(string, string, int)>();
        for (int step = 0; step < 3 && scan.Waiting.Count > 0; step++)
        {
            var now = new List<(string Method, string Class, int Index)>(scan.Waiting);
            scan.Waiting.Clear();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var carrier in now)
            {
                done.Add(carrier);
                names.Add(Regex.Escape(carrier.Method));
            }
            var call = new Regex(@"\b(" + string.Join("|", names) + @")\s*\(", RegexOptions.Compiled);
            foreach (NoticeFile file in scan.Files)
            {
                foreach (Match m in call.Matches(file.Text))
                {
                    if (Array.BinarySearch(file.HeadNameAt, m.Index) >= 0)
                    {
                        continue;   // a method's own head, not a call
                    }
                    List<string> args = null;
                    foreach (var carrier in now)
                    {
                        if (carrier.Method != m.Groups[1].Value || !NoticeCalls(scan, file, m.Index, carrier.Class))
                        {
                            continue;
                        }
                        args ??= NoticeArguments(file.Text, m.Index + m.Length);
                        string arg = null;
                        int position = 0;
                        foreach (string a in args)
                        {
                            if (NoticeNamed.IsMatch(a))
                            {
                                continue;
                            }
                            if (position++ == carrier.Index)
                            {
                                arg = a;
                                break;
                            }
                        }
                        if (arg == null)
                        {
                            continue;
                        }
                        bool calm = scan.Carriers[carrier];
                        foreach (string a in args)
                        {
                            if (NoticeTypeArgument.IsMatch(a))
                            {
                                calm = NoticeCalm(a);
                                break;
                            }
                        }
                        found.Clear();
                        if (!NoticeResolve(scan, file, arg, m.Index, calm, true, found, NoticeHow.Caller))
                        {
                            scan.Unread++;
                        }
                        foreach ((string k, NoticeHow how) in found)
                        {
                            NoticeAdd(scan, file, m.Index, k, calm, how);
                        }
                    }
                }
            }
            scan.Waiting.RemoveAll(c => done.Contains(c));
        }

        var read = new HashSet<string>(StringComparer.Ordinal);
        foreach (NoticeSite site in scan.Sites)
        {
            read.Add(site.File + "|" + site.Key);
        }
        foreach (NoticeFile file in scan.Files)
        {
            foreach (Match m in NoticeWritten.Matches(file.Text))
            {
                if (read.Add(file.Name + "|" + m.Groups[2].Value))
                {
                    NoticeAdd(scan, file, m.Index, m.Groups[2].Value, m.Groups[1].Value == "Info" || m.Groups[1].Value == "Success", NoticeHow.Written);
                }
            }
        }
        _noticeMuteUnread = scan.Unread;
        return scan.Sites;
    }

    // A source file, read for its methods' heads, its classes and its constants.
    private static NoticeFile NoticeRead(NoticeScan scan, string name, string text)
    {
        var file = new NoticeFile { Name = name, Text = NoticeGeneric.Replace(text, m => m.Value.Replace(',', '\u00B7')) };
        MatchCollection heads = NoticeHead.Matches(file.Text);
        file.HeadAt = new int[heads.Count];
        file.HeadNameAt = new int[heads.Count];
        file.HeadName = new string[heads.Count];
        file.HeadParameters = new string[heads.Count];
        for (int i = 0; i < heads.Count; i++)
        {
            file.HeadAt[i] = heads[i].Index;
            file.HeadNameAt[i] = heads[i].Groups[1].Index;
            file.HeadName[i] = heads[i].Groups[1].Value;
            file.HeadParameters[i] = heads[i].Groups[2].Value;
        }
        MatchCollection classes = NoticeClass.Matches(file.Text);
        file.ClassAt = new int[classes.Count];
        file.ClassName = new string[classes.Count];
        for (int i = 0; i < classes.Count; i++)
        {
            file.ClassAt[i] = classes[i].Index;
            file.ClassName[i] = classes[i].Groups[1].Value;
            if (!scan.Classes.TryGetValue(file.ClassName[i], out HashSet<string> where))
            {
                scan.Classes[file.ClassName[i]] = where = new HashSet<string>(StringComparer.Ordinal);
            }
            where.Add(name);
        }
        foreach (Match c in NoticeConstant.Matches(file.Text))
        {
            scan.Constants[name + "|" + c.Groups[1].Value] = c.Groups[2].Value;
            if (!scan.Anywhere.TryGetValue(c.Groups[1].Value, out HashSet<string> values))
            {
                scan.Anywhere[c.Groups[1].Value] = values = new HashSet<string>(StringComparer.Ordinal);
            }
            values.Add(c.Groups[2].Value);
        }
        return file;
    }

    // The keys an argument names (see NoticeMuteSites) into found; false when it is made in code and cannot be read.
    private static bool NoticeResolve(NoticeScan scan, NoticeFile file, string expression, int at, bool calm, bool variables, List<(string Key, NoticeHow How)> found, NoticeHow how)
    {
        string expr = NoticeBare(expression);
        if (expr.Length == 0)
        {
            return false;
        }
        if (NoticeChoice(expr, out string yes, out string no))
        {
            bool first = NoticeResolve(scan, file, yes, at, calm, variables, found, how);
            bool second = NoticeResolve(scan, file, no, at, calm, variables, found, how);
            return first && second;
        }
        if (expr[0] == '"')
        {
            if (expr.Length < 2 || expr.IndexOf('"', 1) != expr.Length - 1 || expr.IndexOf('\\') >= 0)
            {
                return false;
            }
            string written = expr.Substring(1, expr.Length - 2);
            if (NoticeKeyShape.IsMatch(written))
            {
                found.Add((written, how));
            }
            return true;   // the mod's key, or one that is not the mod's (the game's own), which no switch touches
        }
        Match name = NoticeName.Match(expr);
        if (!name.Success)
        {
            return false;
        }
        string last = name.Groups[1].Value;
        NoticeHow named = how == NoticeHow.Written ? NoticeHow.Constant : how;
        if (expr.IndexOf('.') < 0 && scan.Constants.TryGetValue(file.Name + "|" + last, out string own))
        {
            if (NoticeKeyShape.IsMatch(own))
            {
                found.Add((own, named));
            }
            return true;
        }
        if (expr.IndexOf('.') >= 0)
        {
            if (!scan.Anywhere.TryGetValue(last, out HashSet<string> values) || values.Count != 1)
            {
                return false;
            }
            foreach (string value in values)
            {
                if (NoticeKeyShape.IsMatch(value))
                {
                    found.Add((value, named));
                }
            }
            return true;
        }
        if (!variables)
        {
            return false;
        }
        int head = NoticeBefore(file.HeadAt, at);
        if (head < 0)
        {
            return false;
        }
        string body = file.Text.Substring(file.HeadAt[head], at - file.HeadAt[head]);
        bool given = false, read = true;
        foreach (Match set in Regex.Matches(body, @"(?<![\w.])" + Regex.Escape(last) + @"\s*=(?![=>])\s*([^;]+);"))
        {
            given = true;
            read &= NoticeResolve(scan, file, set.Groups[1].Value, at, calm, false, found, how == NoticeHow.Written ? NoticeHow.Variable : how);
        }
        if (given)
        {
            return read;
        }
        string[] parameters = file.HeadParameters[head].Split(',');
        for (int i = 0; i < parameters.Length; i++)
        {
            Match parameter = NoticeParameter.Match(parameters[i].Trim());
            if (!parameter.Success || parameter.Groups[1].Value != last)
            {
                continue;
            }
            int owner = NoticeBefore(file.ClassAt, file.HeadAt[head]);
            var carrier = (file.HeadName[head], owner >= 0 ? file.ClassName[owner] : null, i);
            if (!scan.Carriers.TryGetValue(carrier, out bool wasCalm))
            {
                scan.Carriers[carrier] = calm;
                scan.Waiting.Add(carrier);
            }
            else if (calm && !wasCalm)
            {
                scan.Carriers[carrier] = true;
            }
            return true;   // read at the carrier's callers
        }
        return false;
    }

    // Whether a call by a carrier's name at this place is that carrier's: Class.Method anywhere (or through Class.Instance), a
    // bare Method in the class's own files, or a variable's where the file names the class; never another type's of that name.
    private static bool NoticeCalls(NoticeScan scan, NoticeFile file, int at, string owner)
    {
        string text = file.Text;
        bool own = owner != null && scan.Classes.TryGetValue(owner, out HashSet<string> where) && where.Contains(file.Name);
        int i = at - 1;
        while (i >= 0 && char.IsWhiteSpace(text[i]))
        {
            i--;
        }
        if (i < 0 || text[i] != '.')
        {
            int word = i;
            while (word >= 0 && char.IsLetter(text[word]))
            {
                word--;
            }
            return own && !(i >= 0 && i - word == 3 && string.CompareOrdinal(text, word + 1, "new", 0, 3) == 0);   // `new Name(` makes a thing
        }
        var chain = new List<string>(3);
        while (i >= 0 && text[i] == '.')
        {
            i--;
            if (i >= 0 && text[i] == '?')
            {
                i--;
            }
            while (i >= 0 && char.IsWhiteSpace(text[i]))
            {
                i--;
            }
            int end = i;
            while (i >= 0 && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
            {
                i--;
            }
            if (end == i)
            {
                chain.Add(")");   // a call's or an index's result
                break;
            }
            chain.Add(text.Substring(i + 1, end - i));
            while (i >= 0 && char.IsWhiteSpace(text[i]))
            {
                i--;
            }
        }
        if (owner != null && chain.Contains(owner))
        {
            return true;
        }
        string first = chain[chain.Count - 1];
        if (first == "this" || first == "base")
        {
            return own;
        }
        if (first == ")" || first[0] == '_' || char.IsLower(first[0]))
        {
            return own || (owner != null && Regex.IsMatch(text, @"\b" + Regex.Escape(owner) + @"\b"));
        }
        return own && !scan.Classes.ContainsKey(first);   // a property of the class's own (Instance.Say), not another type's method
    }

    // A call's arguments from just after its "(": parted at the commas outside brackets, texts and comments; none past a ';'.
    private static List<string> NoticeArguments(string text, int start)
    {
        var args = new List<string>(6);
        var part = new StringBuilder(64);
        int depth = 0, limit = Math.Min(text.Length, start + 4000);
        for (int i = start; i < limit; i++)
        {
            char c = text[i];
            if (c == '/' && i + 1 < limit && (text[i + 1] == '/' || text[i + 1] == '*'))
            {
                int end = text[i + 1] == '/' ? text.IndexOf('\n', i) : text.IndexOf("*/", i + 2, StringComparison.Ordinal) + 1;
                i = end <= 0 ? limit : end;
                continue;
            }
            if (c == '"')
            {
                bool verbatim = i > 0 && (text[i - 1] == '@' || (text[i - 1] == '$' && i > 1 && text[i - 2] == '@'));
                int j = i + 1;
                while (j < text.Length)
                {
                    if (!verbatim && text[j] == '\\')
                    {
                        j += 2;
                        continue;
                    }
                    if (text[j] == '"')
                    {
                        if (verbatim && j + 1 < text.Length && text[j + 1] == '"')
                        {
                            j += 2;
                            continue;
                        }
                        break;
                    }
                    j++;
                }
                j = Math.Min(j, text.Length - 1);
                part.Append(text, i, j - i + 1);
                i = j;
                continue;
            }
            if (c == '\'')
            {
                int j = i + 1;
                while (j < text.Length && text[j] != '\'')
                {
                    j += text[j] == '\\' ? 2 : 1;
                }
                j = Math.Min(j, text.Length - 1);
                part.Append(text, i, j - i + 1);
                i = j;
                continue;
            }
            if (c == ';' && depth == 0)
            {
                break;
            }
            if (c == '(' || c == '[' || c == '{')
            {
                depth++;
            }
            else if (c == ')' || c == ']' || c == '}')
            {
                if (depth == 0)
                {
                    args.Add(part.ToString().Trim());
                    return args;
                }
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                args.Add(part.ToString().Trim());
                part.Clear();
                continue;
            }
            part.Append(c);
        }
        return args;   // no closing bracket in reach: what was read
    }

    // An argument without the brackets around the whole of it.
    private static string NoticeBare(string expression)
    {
        string expr = expression.Trim();
        while (expr.Length > 1 && expr[0] == '(' && expr[expr.Length - 1] == ')')
        {
            int depth = 0;
            bool whole = true;
            for (int i = 0; i < expr.Length - 1 && whole; i++)
            {
                depth += expr[i] == '(' ? 1 : expr[i] == ')' ? -1 : 0;
                whole = depth > 0;
            }
            if (!whole)
            {
                break;
            }
            expr = expr.Substring(1, expr.Length - 2).Trim();
        }
        return expr;
    }

    // A choice (c ? yes : no) at the argument's own level: the two answers; false for anything else.
    private static bool NoticeChoice(string expr, out string yes, out string no)
    {
        yes = no = null;
        int depth = 0, ask = -1, nested = 0;
        for (int i = 0; i < expr.Length; i++)
        {
            char c = expr[i];
            if (c == '"' || c == '\'')
            {
                int j = i + 1;
                while (j < expr.Length && expr[j] != c)
                {
                    j += expr[j] == '\\' ? 2 : 1;
                }
                i = j;
                continue;
            }
            if (c == '(' || c == '[' || c == '{')
            {
                depth++;
            }
            else if (c == ')' || c == ']' || c == '}')
            {
                depth--;
            }
            else if (depth == 0 && c == '?')
            {
                char next = i + 1 < expr.Length ? expr[i + 1] : ' ';
                if (next == '?' || next == '.' || next == '[')
                {
                    i++;   // ?? and ?. are no choice
                    continue;
                }
                if (ask < 0)
                {
                    ask = i;
                }
                else
                {
                    nested++;
                }
            }
            else if (depth == 0 && c == ':' && ask >= 0)
            {
                if (nested > 0)
                {
                    nested--;
                    continue;
                }
                yes = expr.Substring(ask + 1, i - ask - 1);
                no = expr.Substring(i + 1);
                return true;
            }
        }
        return false;
    }

    // True for a type a switch may keep back: an information or a success, or one not written at the call.
    private static bool NoticeCalm(string type)
    {
        MatchCollection names = NoticeTypeName.Matches(type);
        if (names.Count == 0)
        {
            return true;
        }
        foreach (Match name in names)
        {
            if (name.Groups[1].Value == "Info" || name.Groups[1].Value == "Success")
            {
                return true;
            }
        }
        return false;
    }

    // The last of the places (in order) at or before at, or -1.
    private static int NoticeBefore(int[] places, int at)
    {
        int found = Array.BinarySearch(places, at);
        return found >= 0 ? found : ~found - 1;
    }

    private static void NoticeAdd(NoticeScan scan, NoticeFile file, int at, string key, bool calm, NoticeHow how)
    {
        if (scan.Seen.Add(file.Name + "|" + at + "|" + key + "|" + calm))
        {
            scan.Sites.Add(new NoticeSite { File = file.Name, Text = file.Text, At = at, Key = key, Calm = calm, How = how });
        }
    }

    // A key read at a call of a carrier of the class given ("LegalPhone.", "DebtPhone."): a contact's text, or the game's notification
    // without the contact. The scan puts a carrier's caller at the method's name, so the class stands just before it.
    private static bool NoticeSentBy(NoticeSite site, string sender)
    {
        return site.How == NoticeHow.Caller && site.At >= sender.Length && string.CompareOrdinal(site.Text, site.At - sender.Length, sender, 0, sender.Length) == 0;
    }

    private static int NoticeLine(NoticeSite site)
    {
        int line = 1;
        for (int i = 0; i < site.At && i < site.Text.Length; i++)
        {
            line += site.Text[i] == '\n' ? 1 : 0;
        }
        return line;
    }

    // ---------------------------------------------------------------- the cost

    private static void NoticeMutePace()
    {
        List<NoticeSite> sites = NoticeMuteSites();
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<string>(sites.Count);
        foreach (NoticeSite site in sites)
        {
            if (distinct.Add(site.Key))
            {
                list.Add(site.Key);
            }
        }
        string[] keys = list.ToArray();
        int all = NoticeMuteAll();
        NoticeMuteAsk(keys, all, 1, out _);   // warmed: the tables are made once
        long before = GC.GetAllocatedBytesForCurrentThread();
        int calmHits = NoticeMuteAsk(keys, 0, 1000000 / Math.Max(1, keys.Length), out double calmMs);
        long calmBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        int rounds = 200000 / Math.Max(1, keys.Length);
        int hits = NoticeMuteAsk(keys, all, rounds, out double ms);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        double each = ms * 1000.0 / Math.Max(1.0, (double)rounds * keys.Length);
        Check(keys.Length > 0 && calmHits == 0 && calmBytes == 0 && calmMs < 100.0,
            "notice mute: with every kind on a notification costs one comparison and allocates nothing (" + calmMs.ToString("F1") + " ms and " + calmBytes + " bytes for a million)");
        Check(keys.Length > 0 && hits > 0 && bytes == 0 && each < 10.0,
            "notice mute: with every kind off a notification's kind and exceptions take " + each.ToString("F2") + " microseconds and " + bytes + " bytes (under 10, and none)");
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization | System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int NoticeMuteAsk(string[] keys, int mask, int rounds, out double ms)
    {
        int hits = 0;
        long began = System.Diagnostics.Stopwatch.GetTimestamp();
        for (int r = 0; r < rounds; r++)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                hits += NoticeMuteRules.Muted(keys[i], false, mask) ? 1 : 0;
            }
        }
        ms = (System.Diagnostics.Stopwatch.GetTimestamp() - began) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        return hits;
    }

    // ---------------------------------------------------------------- the sources

    private static void NoticeMuteSources()
    {
        string mute = ModSource(@"Core\GameUi\NoticeMute.cs");
        string pass = MemberText(mute, "private static void Pass(NotificationType type, string key, Dictionary<string, string> data, float seconds, string same, "
                                       + "Action onClick, bool sound, bool track)");
        string keep = MemberText(mute, "private static void Keep(NotificationsUI maker, NotificationType type, string key, Dictionary<string, string> data, float seconds, string same, bool track)");
        string stand = MemberText(mute, "private static void Stand()");
        string aside = MemberText(mute, "private static void StepAside()");
        Check(CodeAt(stand, "GameContract.NoticeMaker.Method") > 0 && CodeAt(stand, "Delegate.CreateDelegate(") > 0 && CodeAt(stand, "Array.IndexOf(list, own)") > 0
              && CodeAt(stand, "list[at] = Gate;") > CodeAt(stand, "_game = (Action<") && CodeAt(stand, "SetEvent(list);") > CodeAt(stand, "list[at] = Gate;"),
            "notice mute, as code: the gate takes the game's maker's own place on its event, found through the game contract, and keeps the maker's entry");
        Check(CodeAt(aside, "list[at] = game;") > 0 && CodeAt(aside, "Delegate.Remove(shown, Gate)") > 0 && CodeAt(aside, "maker != null") > 0 && CodeAt(aside, "_keptKey = null;") > 0,
            "notice mute, as code: stepping aside gives the maker its own place back, or takes the gate off alone when the maker went with its city");
        Check(CodeAt(pass, "catch (Exception fault)") > 0 && CodeAt(pass, "Guard.Fail(FrameGuard, fault);") > 0 && !pass.Contains("Guard.Wrap")
              && CodeAt(pass, "game(type, key, data, seconds, same, onClick, sound, track);") > CodeAt(pass, "catch (Exception fault)")
              && CodeAt(pass, "NoticeMuteRules.Muted(key, Urgent(type), _mask)") > 0 && CodeAt(pass, "StepAside();") > 0,
            "notice mute, as code: the gate guards itself and calls the game's maker outside its guard, so a fault or the guard standing it down never loses a notification");
        Check(Regex.IsMatch(pass, @"if \(!kept && game != null\)\s*\{\s*game\(type, key, data, seconds, same, onClick, sound, track\);\s*\}")
              && Occurs(pass, "game(type, key, data, seconds, same, onClick, sound, track);") == 1 && CodeAt(pass, "bool kept = false;") > 0
              && Occurs(pass, "kept = true;") == 1 && Occurs(pass, "Keep(") == 1
              && CodeAt(pass, "Keep(maker, type, key, data, seconds, same, track);") > CodeAt(pass, "else if (NoticeMuteRules.Muted(key, Urgent(type), _mask))")
              && CodeAt(pass, "kept = true;") > CodeAt(pass, "Keep(maker, type, key, data, seconds, same, track);"),
            "notice mute, as code: the game's maker is called once for every notification but one the gate kept back, and never for a kept one");
        int cleared = CodeAt(pass, "_keptKey = null;");
        Check(cleared > 0 && cleared < CodeAt(pass, "try") && CodeAt(pass, "_keptKey = key;") > CodeAt(pass, "Keep(maker, type, key, data, seconds, same, track);")
              && CodeAt(pass, "_keptData = data;") > cleared && CodeAt(mute, "return key != null && ReferenceEquals(key, _keptKey) && ReferenceEquals(data, _keptData);") > 0,
            "notice mute, as code: the gate says of each notification whether it kept it back, so the listeners after the maker never take one made before it stood for one kept");
        int written = CodeAt(keep, "if (save != null && save.notifications != null)");
        Check(CodeAt(keep, "maker.container.Find(same)") > 0 && CodeAt(keep, "SameUp(same, now)") > 0 && written > CodeAt(keep, "maker.container.Find(same)")
              && CodeAt(keep, "save.notifications.Enqueue(new Notification { type = type, key = key, notificationData = NoticeMuteRules.ListData(data, track), date = TimeHelper.Now() });") > written
              && Occurs(keep, "Enqueue(") == 1 && CodeAt(keep, "track &&") < 0 && CodeAt(keep, "&& track") < 0 && CodeAt(keep, "if (track") < 0 && CodeAt(keep, "!track") < 0
              && !keep.Contains("Instantiate") && !keep.Contains("PlayNotificationSound"),
            "notice mute, as code: a kept notification goes into the save's list as the game writes it, whatever its sender asked (a copy of its words when it asked for none), "
            + "its duplicate dropped, with nothing made and no sound");

        string options = MemberText(mute, "private static int OptionsMask()");
        MatchCollection made = Regex.Matches(options, @"NoticeMuteRules\.With\((0|mask), NoticeKind\.(\w+), Settings\.NoticeShow(\w+)\)");
        bool paired = made.Count == NoticeMuteRules.Switchable.Length && Occurs(options, "NoticeMuteRules.With(") == made.Count
                      && CodeAt(options, "int mask = NoticeMuteRules.With(0, ") > 0 && CodeAt(options, "return NoticeMuteRules.With(mask, ") > 0
                      && Occurs(options, "mask = NoticeMuteRules.With(mask, ") == made.Count - 2;
        for (int i = 0; i < made.Count && paired; i++)
        {
            paired &= made[i].Groups[2].Value == made[i].Groups[3].Value && made[i].Groups[2].Value == NoticeMuteRules.Switchable[i].ToString()
                      && made[i].Groups[1].Value == (i == 0 ? "0" : "mask");
        }
        Check(paired, "notice mute, as code: each kind's switch sets its own kind's bit, in the rules' order, threaded through one mask (a switch read for another kind fails here)");
        Check(Occurs(mute, "NoticeMuteRules.With(") == 9 && CodeAt(mute, "Settings.Changed += Reread;") > 0 && CodeAt(mute, "Harmony") < 0 && CodeAt(mute, "void Update(") < 0
              && CodeAt(mute, "LateUpdate") < 0 && CodeAt(mute, "SharedCity") < 0 && CodeAt(mute, "GoingPublicLink") < 0 && mute.Length > 0,
            "notice mute, as code: the nine switches make the mask, read again with the options; no patch, nothing a frame, nothing sent");

        string muting = MemberText(mute, "public static bool Muting(NotificationType type, string key)");
        string urgent = MemberText(mute, "private static bool Urgent(NotificationType type)");
        Check(CodeAt(muting, "return _standing && NoticeMuteRules.Muted(key, Urgent(type), _mask);") > 0
              && CodeAt(urgent, "return type == NotificationType.Warning || type == NotificationType.Error;") > 0,
            "notice mute, as code: the morning line asks the gate's own rule (while it stands, the kinds off, never a warning or an error)");
        string place = MemberText(mute, "public static void Place()");
        Check(Regex.IsMatch(place, @"_mask = OptionsMask\(\);\s*if \(_mask != 0 && !FrameGuard\.Off\)\s*\{\s*Stand\(\);\s*\}\s*else\s*\{\s*_waiting = false;\s*StepAside\(\);\s*\}")
              && CodeAt(place, "Guard.Fail(FrameGuard, e);") > 0,
            "notice mute, as code: placing reads the switches, stands the gate with a kind off, and steps it aside with every kind on or the guard's stand-down");
        string heard = MemberText(mute, "internal static void Shown()");
        Check(Regex.IsMatch(heard, @"if \(_waiting\)\s*\{\s*_waiting = false;\s*_passing = true;\s*Place\(\);\s*_passing = false;\s*\}") && Occurs(heard, "Place();") == 1,
            "notice mute, as code: a gate waiting for the maker to join its event takes its place at the first notification after, once");
        string attached = MemberText(mute, "internal static void Attached(NotificationsUI maker)");
        int other = CodeAt(attached, "if (_standing && !ReferenceEquals(_game != null ? _game.Target : null, maker))");
        Check(other > 0 && CodeAt(attached, "StepAside();") > other && CodeAt(attached, "_maker = maker;") > CodeAt(attached, "StepAside();")
              && CodeAt(attached, "_heard = true;") > CodeAt(attached, "if (!_heard)") && CodeAt(attached, "Settings.Changed += Reread;") > CodeAt(attached, "_heard = true;")
              && CodeAt(attached, "Place();") > CodeAt(attached, "catch (Exception e)"),
            "notice mute, as code: with a city's maker found, an earlier maker's place is given back first, the switches are heard once a session, and the gate is placed");
        string leave = MemberText(mute, "internal static void Leave()");
        int caught = CodeAt(leave, "catch (Exception e)");
        Check(CodeAt(leave, "StepAside();") > 0 && caught > CodeAt(leave, "StepAside();") && CodeAt(leave, "_maker = null;") > caught && CodeAt(leave, "_test = null;") > caught
              && CodeAt(leave, "_waiting = false;") > caught && CodeAt(leave, "Array.Clear(SameIds, 0, SameSlots);") > caught,
            "notice mute, as code: with the city going the gate steps aside, then forgets the maker, the test, a wait and the duplicates it kept, whatever stepping aside did");
        Check(CodeAt(mute, "TestRefusals.Tell(Logger, \"Notice mute\", TestRefusal.NotLoaded);") > 0 && CodeAt(mute, "NoticeLook.Samples") > 0
              && CodeAt(mute, "bool pass = kept == expected && wrong == 0 && counted == samples.Length;") > 0
              && CodeAt(mute, "wrong += stack.childCount - toasts == (held ? 0 : 1) ? 0 : 1;") > CodeAt(mute, "Notifications.Show(NotificationType.Info, samples[i], null, TestSeconds);")
              && CodeAt(mute, "Notifications.Show(NotificationType.Info, DoneKey, data, DoneSeconds);") > 0,
            "notice mute, as code: the test sends each kind's sample, sets what was kept back against what the switches say, and counts the game's toasts: none for one kept back, one for each other");

        string contract = ModSource(@"Core\GameContract.Notices.cs");
        Check(contract.Contains("ContractBook.Part(\"notice_mute\", \"phone\"") && contract.Contains("\"UI.Notification.NotificationsUI\", \"ShowNotification\", \"Void\"")
              && contract.Contains("\"NotificationType\", \"String\", \"Dictionary<String,String>\", \"Single\", \"String\", \"Action\", \"Boolean\", \"Boolean\""),
            "notice mute, as code: the game's private toast maker is a line of the game contract, with its eight parameters");

        string look = ModSource(@"Core\GameUi\NoticeLook.cs");
        string shown = MemberText(look, "private void Shown(NotificationType type, string key, Dictionary<string, string> data, float seconds, string duplicate, "
                                        + "Action onClick, bool sound, bool track)");
        Check(CodeAt(look, "NoticeMute.Attached(_toasts);") > 0 && CodeAt(MemberText(look, "private void OnDestroy()"), "NoticeMute.Leave();") > 0
              && CodeAt(shown, "NoticeMute.Shown();") > 0 && CodeAt(shown, "if (NoticeMute.KeptBack(key, data))") > 0
              && CodeAt(shown, "_lastToast = made;") > CodeAt(shown, "if (NoticeMute.KeptBack(key, data))") && CodeAt(shown, "NoticeMute.Muting(") < 0,
            "notice mute, as code: the gate is placed with the city's maker and taken off with it; the look never draws on an older toast for one kept back");
        string shell = ModSource(@"Ui\Shell\ShellNotices.cs");
        string card = MemberText(shell, "private static void Shown(NotificationType type, string key, Dictionary<string, string> data, float seconds, string duplicate, "
                                        + "Action click, bool sound, bool track)");
        Check(CodeAt(card, "if (NoticeMute.KeptBack(key, data))") > 0 && CodeAt(card, "stack.GetChild(stack.childCount - 1)") > CodeAt(card, "if (NoticeMute.KeptBack(key, data))"),
            "notice mute, as code: the kit's cards make no card of an older toast for one kept back");
        string hold = ModSource(@"Core\MorningNews.cs");
        string held = MemberText(hold, "public static void Show(NotificationType type, string key, Dictionary<string, string> data, float seconds, string same = null, bool track = true)");
        Check(CodeAt(held, "if (GameUi.NoticeMute.Muting(type, key))") > 0 && CodeAt(held, "Held.Add(notice);") > CodeAt(held, "if (GameUi.NoticeMute.Muting(type, key))"),
            "notice mute, as code: a kind switched off is never held for the morning line, which pops up");

        string settings = ModSource(@"Core\Settings.cs");
        string page = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string tree = ModSource(@"Meta\OptionsTree.cs");
        string features = ModSource(@"Meta\FeatureList.cs");
        bool declared = true, placed = true, ordered = true;
        int last = -1;
        foreach (NoticeKind kind in NoticeMuteRules.Switchable)
        {
            string name = kind.ToString();
            string id = "notice_show_" + name.ToLowerInvariant();
            declared &= CodeAt(settings, "public const string NoticeShow" + name + "Id = \"" + id + "\";") > 0 && CodeAt(settings, "public static bool NoticeShow" + name + " = true;") > 0
                        && CodeAt(settings, "NoticeShow" + name + " = Bool(modId, NoticeShow" + name + "Id, NoticeShow" + name + ");") > 0
                        && CodeAt(mute, "NoticeKind." + name + ", Settings.NoticeShow" + name + ")") > 0;
            placed &= CodeAt(page, ".AddToggle(Settings.NoticeShow" + name + "Id, \"reallife_" + id + "\", Settings.NoticeShow" + name + ",") > 0
                      && CodeAt(page, "Settings.NoticeShow" + name + " = v; GameUi.NoticeMute.Place(); }") > 0 && CodeAt(features, "Settings.NoticeShow" + name + "Id") > 0;
            int at = CodeAt(tree, "\"" + id + "\"");
            ordered &= at > last;
            last = at;
        }
        Check(declared, "notice mute, as code: each kind's switch is an option, on by default, read at load and read by the gate for its own kind");
        Check(placed && CodeAt(page, ".AddButton(\"reallife_test_notice_mute\", GameUi.NoticeMute.Test)") > 0 && CodeAt(page, "GameUi.NoticeMute.Logger = context.Logger") > 0
              && CodeAt(features, "\"reallife_test_notice_mute\"") > 0,
            "notice mute, as code: each switch is on the options page and places the gate at once; the test is the phone's");
        Check(ordered && CodeAt(tree, "S(\"notices\").Guide(\"phone\")") > 0, "notice mute, as code: the options page lists the switches in their own section, in the rules' order");
    }

    // ---------------------------------------------------------------- the texts

    private static void NoticeMuteWords()
    {
        Dictionary<string, string> en = LocaleFile("en");
        string Text(string key) => en.TryGetValue(key, out string v) ? v : null;
        int Words(string text) => text == null ? 0 : text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
        bool Plain(string text) => text != null && text.IndexOf('!') < 0 && text.IndexOf('-') < 0 && text.IndexOf((char)0x2013) < 0 && text.IndexOf((char)0x2014) < 0;
        bool Searchable(string text) => text != null && !Regex.IsMatch(text.ToLowerInvariant(), "(^|[^a-z])(rent|fps)");

        string section = Text("reallife_opt_sec_notices");
        Check(section != null && Words(section) <= 6 && section.Length <= 40 && Plain(section), "notice mute, texts: the section's title is short and plain (" + section + ")");
        bool labels = true, tips = true, plain = true, search = true, list = true;
        foreach (NoticeKind kind in NoticeMuteRules.Switchable)
        {
            string id = "notice_show_" + kind.ToString().ToLowerInvariant();
            string label = Text("reallife_" + id);
            string tip = Text("reallife_opt_tip_" + id);
            labels &= label != null && Words(label) <= 9 && label.Length <= 60 && !label.EndsWith(".", StringComparison.Ordinal);
            tips &= tip != null && Words(tip) <= 32 && tip.Length <= 190 && tip.EndsWith(".", StringComparison.Ordinal);
            plain &= Plain(label) && Plain(tip);
            search &= Searchable(label) && Searchable(tip);
            list &= tip != null && tip.Contains("notification list") && tip.Contains("always pop up");
        }
        Check(labels, "notice mute, texts: each switch's label is at most nine words and 60 characters, with no full stop");
        Check(tips, "notice mute, texts: each switch's sentence is at most 32 words and 190 characters, ending in a full stop");
        Check(plain && search, "notice mute, texts: no exclamation mark or dash, and no word the search keeps for other rows (rent, fps)");
        Check(list, "notice mute, texts: each sentence says where a kind switched off goes and what always pops up");
        Check((Text("reallife_opt_tip_notice_show_money") ?? string.Empty).Contains("bills") && (Text("reallife_opt_tip_notice_show_other") ?? string.Empty).Contains("death"),
            "notice mute, texts: the money switch says bills always pop up, the rest of the mod's says a death does");
        string test = Text("reallife_test_notice_mute");
        Check(test != null && Words(test) <= 13 && test.Length <= 80 && char.IsUpper(test[0]) && !test.StartsWith("Test", StringComparison.OrdinalIgnoreCase) && Plain(test),
            "notice mute, texts: the test's button says what it does (" + test + ")");
        string done = Text("reallife_notice_mute_test_done");
        Check(Fits(en, "reallife_notice_mute_test_done") && done.Contains("{shown}") && done.Contains("{all}") && done.Contains("{kept}") && Plain(done),
            "notice mute, texts: the test's answer fits a toast and names what popped up and what went into the list");
        Check(Text("reallife_contract_part_notice_mute") != null, "notice mute, texts: the game contract's part has its name");
        Check(WhatsNewHas(en, "goes straight into the notification list without popping up"), "notice mute, texts: What's new says a kind switched off goes into the list");
    }
}
