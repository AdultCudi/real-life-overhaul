using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Texts;

// A call back (GitHub #15: Social/SocialFollowRules, SocialFollowCatalogue, PersonDialog.CallBack, SocialPhone.Thread):
// calling somebody back after they texted opens on what they wrote about. Checked: a text of every feature that texts the
// people of the social life is picked up (the social life's own, RLO Texts' news and gossip it sends, the family's, life's
// little moments', travel's), and every text each of them can send has a subject or is left to the ordinary call on
// purpose; the day it came and the next, never twice, never one the player wrote, the newest with a subject; RLO Texts'
// words keep the key they stand for; the save's new fields last in the record, an older save's record read as before; the
// call's flow (offers and plans first, spent once shown, worth a chat's topic and nothing after the day's chat, no money);
// the table's every line in en, pt and zh-cn; What's new and the Guide in every language.
internal static partial class Program
{
    private const int CbMinutes = 60 * 24;   // game minutes a day

    // A thread line as SocialPhone.Thread makes it from the game's message (the key RLO Texts' words stand for, if any).
    private static SocialThreadLine CbLine(string messageKey, int day, int hour, bool player = false, Dictionary<string, string> data = null)
    {
        return new SocialThreadLine(SocialFollowRules.KeyOf(messageKey, data), data, day * CbMinutes + hour * 60, player);
    }

    private static void CallBack()
    {
        CallBackSources();
        CallBackEveryText();
        CallBackWindow();
        CallBackWords();
        CallBackSave();
        CallBackFlow();
        CallBackTable();
        CallBackWorth();
        CallBackTexts();
    }

    // A text of each feature, in a thread with the welcome first: the call opens on its subject, and says where it came from.
    private static void CallBackSources()
    {
        var cases = new[]
        {
            ("reallife_social_text_news_3", "the social life", "news_good"), ("reallife_social_text_news_2", "the social life", "news_bad"),
            ("reallife_social_text_badday_1", "the social life", "news_bad"), ("reallife_social_text_crash_1", "the social life", "crash"),
            ("reallife_social_text_stoodup_4", "the social life", "stoodup"), ("reallife_social_text_nextday_bad_2", "the social life", "nextday_bad"),
            ("reallife_texts_life_mom_news_opened_2", "the social life", "opened"), ("reallife_texts_gossip_baby_1", "the social life", "baby"),
            ("reallife_family_mom_visit_2", "the family", "mom_visit"), ("reallife_family_neglect_1", "the family", "neglect"),
            ("reallife_family_mom_born", "the family", "mom_grandchild"), ("reallife_family_friend_invited", "the family", "friend_wedding_invite"),
            ("reallife_family_glad_retrain_2", "the family", "partner_glad"),
            ("reallife_ev_fm02", "life's little moments", "fm02"), ("reallife_ev_fr05_y2", "life's little moments", "fr05_y2"),
            ("reallife_ev_fr23_n1", "life's little moments", "fr23_n1"),
            ("reallife_travel_burnout_friend_3", "travel", "burnout"), ("reallife_travel_text_mom_souvenir", "travel", "souvenir_mom")
        };
        var wrong = new List<string>();
        foreach (var c in cases)
        {
            var thread = new List<SocialThreadLine> { CbLine("reallife_text_mom_welcome", 30, 9), CbLine(c.Item1, 40, 15) };
            bool found = SocialFollowRules.Pick(thread, -1, 40, out SocialFollowPick pick, out string why);
            if (!found || pick.Subject == null || pick.Subject.Id != c.Item3 || pick.Source != c.Item2 || pick.Day != 40 || pick.Key != c.Item1 || pick.Minute != 40 * CbMinutes + 900)
            {
                wrong.Add(c.Item1 + " (" + (found ? pick.Subject?.Id + ", " + pick.Source : why) + ")");
            }
        }
        Check(wrong.Count == 0, "call back: a text of each feature that texts the people of the social life is what the call opens on, with where it came from (" + cases.Length
                                + " texts; wrong: " + string.Join("; ", wrong) + ")");

        // RLO Texts' words: the thread shows its line for the words, and keeps the written line's key they stand for.
        var words = new Dictionary<string, string> { { "text", "Heard about the crash. You all right?" }, { SocialFollowRules.KeyField, "reallife_social_text_crash_2" } };
        var worded = new List<SocialThreadLine> { CbLine(SocialFollowRules.WordsKey, 40, 12, false, words) };
        bool wordsFound = SocialFollowRules.Pick(worded, -1, 40, out SocialFollowPick wordsPick, out _);
        var bare = new List<SocialThreadLine> { CbLine(SocialFollowRules.WordsKey, 40, 12, false, new Dictionary<string, string> { { "text", "Hi" } }) };
        Check(wordsFound && wordsPick.Subject.Id == "crash" && wordsPick.Key == "reallife_social_text_crash_2" && wordsPick.Source == "the social life"
              && !SocialFollowRules.Pick(bare, -1, 40, out _, out _),
            "call back: a text worded by RLO Texts is taken up by the key it stands for; words without one (an older save's) have no subject");
    }

    // Every text each feature can send to the people of the social life: a subject, or the ordinary call on purpose.
    private static void CallBackEveryText()
    {
        // The social life's own pools, every key: all have a subject but a few that are answered or said otherwise.
        var noSubject = new HashSet<string>(StringComparer.Ordinal)
        {
            "TextBirthdayOwn",   // their birthday: the call wishes it (Something else, Birthday)
            "TextBreakUp",       // they ended it
            "TextLiftAsk", "TextLoanAsk", "TextMovingAsk"   // a favour asked: the call answers it (PersonDialog.Asked)
        };
        int social = 0;
        var socialWrong = new List<string>();
        foreach (FieldInfo field in typeof(SocialCatalogue).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType != typeof(string[]) || !field.Name.StartsWith("Text", StringComparison.Ordinal))
            {
                continue;
            }
            foreach (string key in (string[])field.GetValue(null))
            {
                social++;
                bool has = SocialFollowCatalogue.Of(key) != null;
                if (has == noSubject.Contains(field.Name))
                {
                    socialWrong.Add(field.Name + ": " + key + (has ? " has a subject" : " has none"));
                }
            }
        }
        foreach (OutingDef def in SocialCatalogue.Outings)
        {
            if (def.Invite != null && SocialFollowCatalogue.Of(def.Invite) != null)
            {
                socialWrong.Add(def.Invite + ": an invitation has a subject (the call answers it)");
            }
        }
        foreach (string key in new[] { "reallife_social_text_where", "reallife_social_text_outside", "reallife_social_invite_declined" })
        {
            if (SocialFollowCatalogue.Of(key) != null)
            {
                socialWrong.Add(key + ": a plan's line has a subject");
            }
        }
        foreach (string key in SocialCatalogue.OriginWelcome)
        {
            if (SocialFollowCatalogue.Of(key) != null)
            {
                socialWrong.Add(key + ": a welcome has a subject");
            }
        }
        Check(social >= 80 && socialWrong.Count == 0,
            "call back: every text of the social life's own (" + social + ") has a subject, but their own birthday, a break-up, a favour asked, an invitation, "
                                                     + "a plan's and a welcome (wrong: " + string.Join("; ", socialWrong) + ")");

        // RLO Texts' news of the player's life and gossip, sent through the social life: both lines of every pool.
        var cityWrong = new List<string>();
        foreach (string row in TextsMapRules.CityKeys)
        {
            string pool = row.Substring(0, row.IndexOf('|'));
            foreach (string key in new[] { pool + "_1", pool + "_2" })
            {
                if (SocialFollowCatalogue.Of(key) == null)
                {
                    cityWrong.Add(key);
                }
            }
        }
        Check(cityWrong.Count == 0, "call back: every text of RLO Texts' news of the player's life and its gossip has a subject (" + TextsMapRules.CityKeys.Length * 2 + " lines; none: "
                                    + string.Join(", ", cityWrong) + ")");

        // Life's little moments: every outcome that comes as a text from the old friend or Mom (LifeEventRunner.Apply sends
        // the outcome's text through their contact unless it hurts or is a notice).
        int life = 0;
        var lifeWrong = new List<string>();
        foreach (LifeEvent e in LifeEventCatalogue.All)
        {
            foreach (Outcome o in e.AllOutcomes)
            {
                if (o.Via == Via.Toast || o.HurtHours > 0)
                {
                    continue;
                }
                life++;
                if (SocialFollowCatalogue.Of(e.TextKey(o)) == null)
                {
                    lifeWrong.Add(e.TextKey(o));
                }
            }
        }
        string runner = ModSource(@"Life\LifeEventRunner.cs");
        var midday = new LifeContext
        {
            Flags = Need.Friend | Need.Mom | Need.Weekday | Need.Dry | Need.Healthy | Need.Rested | Need.Fed | Need.OnFoot | Need.CanPrompt | Need.NotDriving,
            Hour = 13, Money = 500f
        };
        int fit = 0;
        foreach (LifeEvent e in LifeEventCatalogue.All)
        {
            fit += (e.Category == "fr" || e.Category == "fm") && e.Fits(in midday) ? 1 : 0;
        }
        Check(life >= 50 && lifeWrong.Count == 0 && fit > 10 && runner.Contains("Contact contact = outcome.Via == Via.Mom ? PhoneContacts.Mom : PhoneContacts.Friend;")
              && runner.Contains("contact?.SendMessage(new TextMessage(key, data, false, true)"),
            "call back: every little moment of life's that comes as a text from the old friend or Mom has a subject (" + life + " texts, " + fit + " of theirs can come on a weekday's midday; none: "
            + string.Join(", ", lifeWrong) + ")");

        // The family: every text it sends to Mom, the partner or the old friend, read from its own calls.
        HashSet<string> family = FamilyTextsToPeople(out List<string> unread);
        var familyNone = new HashSet<string>(StringComparer.Ordinal)
        {
            "reallife_family_birth_time_text",   // the waters breaking: the birth's own way
            "reallife_family_sms_new_home_small", "reallife_family_sms_new_home_yes",   // the answer to the player's own text, at once
            "reallife_family_work_text", "reallife_family_wish_text", "reallife_family_expect_home", "reallife_family_expect_second_home",
            "reallife_family_expect_dates", "reallife_family_expect_car", "reallife_family_expect_allowance",
            "reallife_family_ask_remind_1", "reallife_family_ask_remind_2", "reallife_family_ask_lapsed"   // the partner's asks and wishes, talked over at home
        };
        var familyWrong = new List<string>();
        foreach (string key in family)
        {
            bool has = SocialFollowCatalogue.Of(key) != null;
            if (has == familyNone.Contains(key))
            {
                familyWrong.Add(key + (has ? " has a subject" : " has none"));
            }
        }
        foreach (string key in familyNone)
        {
            if (!family.Contains(key))
            {
                familyWrong.Add(key + " is left out but the family no longer sends it");
            }
        }
        Check(family.Count >= 50 && unread.Count == 0 && familyWrong.Count == 0,
            "call back: every text the family sends to Mom, the partner or the old friend (" + family.Count + ", read from its calls) has a subject, but the waters breaking, "
            + "the answer to the player's own text and the partner's asks (wrong: " + string.Join("; ", familyWrong) + "; calls not read: " + string.Join("; ", unread) + ")");

        // Travel: the old friend's nudge to take a break, and a souvenir's thanks.
        var travelWrong = new List<string>();
        foreach (string key in TravelCatalogue.BurnoutNudges)
        {
            if (SocialFollowCatalogue.Of(key)?.Id != "burnout")
            {
                travelWrong.Add(key);
            }
        }
        int souvenirs = 0;
        foreach (string file in new[] { @"Travel\Holidays.cs", @"Travel\Holidays.Trip.cs" })
        {
            foreach (Match m in Regex.Matches(ModSource(file), @"PhoneContacts\.(?:Friend|Mom)\.SendMessage\(new TextMessage\(""(reallife_[a-z0-9_]+)"""))
            {
                souvenirs++;
                if (SocialFollowCatalogue.Of(m.Groups[1].Value) == null)
                {
                    travelWrong.Add(m.Groups[1].Value);
                }
            }
        }
        Check(TravelCatalogue.BurnoutNudges.Length >= 3 && souvenirs >= 2 && travelWrong.Count == 0,
            "call back: travel's texts to the old friend and Mom (a nudge to take a break, a souvenir's thanks) have their subjects (none: " + string.Join(", ", travelWrong) + ")");
    }

    // The keys of every text the family sends to a person of the social life, read from each call of Family/ that texts Mom,
    // the partner or the old friend (FamilyText.Message to their contact, MomSays, MomGift, the partner's voice: Say, Warm,
    // Ask and its own Text, and PartnerExpect's Speak and Wish): the literal keys and the arrays named in the call's key
    // argument alone (never the values filled into the line). A key argument the reading cannot follow is listed in unread,
    // but for those read where they come from: the partner's expecting lines, the chores' asks, the glad lines, and the
    // helpers (MomSays, MomGift, the partner's voice), whose own callers are read.
    private static HashSet<string> FamilyTextsToPeople(out List<string> unread)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        unread = new List<string>();
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Family"));
        var literal = new Regex("\"(reallife_family_[a-z0-9_]+)\"");
        var array = new Regex(@"string\[\]\s+(\w+)\s*=\s*\{([^}]*)\}");
        // Each call that texts somebody: its name, which of its arguments are the line's key, and the one file it is read in (null: any).
        var calls = new List<(Regex at, int[] key, string only)>
        {
            (new Regex(@"(?<![\w.])FamilyText\.Message\("), new[] { 1 }, null),
            (new Regex(@"(?<![\w.])(?:Relatives\.)?MomSays\("), new[] { 0 }, null),
            (new Regex(@"(?<![\w.])(?:Relatives\.)?MomGift\("), new[] { 1 }, null),
            (new Regex(@"PartnerVoice\.(?:Say|Warm)\("), new[] { 0 }, null),
            (new Regex(@"PartnerVoice\.Ask\("), new[] { 1 }, null),
            (new Regex(@"(?<![\w.])(?:Say|Warm)\("), new[] { 0 }, "PartnerVoice.cs"),
            (new Regex(@"(?<![\w.])Ask\("), new[] { 1 }, "PartnerVoice.cs"),
            (new Regex(@"(?<![\w.])Text\("), new[] { 0 }, "PartnerVoice.cs"),
            (new Regex(@"(?<![\w.])Speak\("), new[] { 0 }, "PartnerExpect.cs"),
            (new Regex(@"(?<![\w.])Wish\("), new[] { 5, 6 }, "PartnerExpect.cs")
        };
        foreach (string path in Directory.GetFiles(folder, "*.cs"))
        {
            string file = System.IO.Path.GetFileName(path);
            string code = Regex.Replace(File.ReadAllText(path), @"//[^\n]*", string.Empty);   // (comments are not calls)
            var arrays = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (Match a in array.Matches(code))
            {
                var list = new List<string>();
                foreach (Match k in literal.Matches(a.Groups[2].Value))
                {
                    list.Add(k.Groups[1].Value);
                }
                arrays[a.Groups[1].Value] = list;
            }
            foreach (var call in calls)
            {
                if (call.only != null && call.only != file)
                {
                    continue;
                }
                foreach (Match m in call.at.Matches(code))
                {
                    List<string> args = CallArguments(code, m.Index + m.Length);
                    string before = code.Substring(Math.Max(0, m.Index - 40), Math.Min(40, m.Index));
                    if (args == null || Regex.IsMatch(before, @"\b(?:void|bool|string|DialogEntry)\s+$"))
                    {
                        continue;   // (where the helper is defined, not called: its callers are read)
                    }
                    if (m.Value.Contains("FamilyText.Message") && (args[0].Contains("FamilyContact") || args[0].Contains("InLawContact")))
                    {
                        continue;   // (the family's own contact, or the in-laws: nobody of the social life)
                    }
                    int found = 0;
                    var expressions = new List<string>();
                    foreach (int k in call.key)
                    {
                        if (k < args.Count)
                        {
                            expressions.Add(args[k]);
                        }
                    }
                    foreach (string e in expressions)
                    {
                        foreach (Match k in literal.Matches(e))
                        {
                            keys.Add(k.Groups[1].Value);
                            found++;
                        }
                        foreach (KeyValuePair<string, List<string>> a in arrays)
                        {
                            if (Regex.IsMatch(e, @"\b" + Regex.Escape(a.Key) + @"\b"))
                            {
                                keys.UnionWith(a.Value);
                                found += a.Value.Count;
                            }
                        }
                    }
                    string shown = string.Join(", ", expressions).Trim();
                    if (found > 0)
                    {
                        continue;
                    }
                    if (file == "Children.cs" && shown == "line" && arrays.ContainsKey("Expecting"))
                    {
                        keys.UnionWith(arrays["Expecting"]);   // (line = FamilyText.Pick(Expecting, ...): the partner's texts while expecting)
                    }
                    else if (file == "Chores.cs" && shown == "ChoreRules.AskKey(kind)")
                    {
                        foreach (Match k in literal.Matches(MemberText(ModSource(@"Family\ChoreRules.cs"), "public static string AskKey(")))
                        {
                            keys.Add(k.Groups[1].Value);
                        }
                    }
                    else if (file == "PartnerGlad.cs" && shown.StartsWith("lines[", StringComparison.Ordinal))
                    {
                        foreach (List<string> set in arrays.Values)
                        {
                            keys.UnionWith(set);   // (lines = one of the sets of glad lines: SetOf)
                        }
                    }
                    else if (!(shown == "key" && (file == "Relatives.cs" || file == "PartnerVoice.cs" || file == "PartnerExpect.cs"))
                             && !(file == "PartnerExpect.cs" && (shown == "ask" || shown == "thanks")))
                    {
                        unread.Add(file + ": " + m.Value + shown + ")");
                    }
                }
            }
        }
        return keys;
    }

    // The arguments of a call whose "(" ends at <paramref name="at"/>, split at its own commas; null if it does not close.
    private static List<string> CallArguments(string code, int at)
    {
        var args = new List<string>();
        int depth = 0, from = at;
        bool quoted = false;
        for (int i = at; i < code.Length; i++)
        {
            char c = code[i];
            if (quoted)
            {
                quoted = c != '"' || code[i - 1] == '\\';
                continue;
            }
            switch (c)
            {
                case '"': quoted = true; break;
                case '(': case '[': case '{': depth++; break;
                case ')': case ']': case '}':
                    if (depth == 0)
                    {
                        args.Add(code.Substring(from, i - from).Trim());
                        return args;
                    }
                    depth--;
                    break;
                case ',':
                    if (depth == 0)
                    {
                        args.Add(code.Substring(from, i - from).Trim());
                        from = i + 1;
                    }
                    break;
            }
        }
        return null;
    }

    // The day it came and the next; never twice; never the player's own; the newest of theirs with a subject.
    private static void CallBackWindow()
    {
        bool Pick(List<SocialThreadLine> t, int followed, int today, out SocialFollowPick p) => SocialFollowRules.Pick(t, followed, today, out p, out _);
        var one = new List<SocialThreadLine> { CbLine("reallife_ev_fm02", 40, 10) };
        Check(Pick(one, -1, 40, out _) && Pick(one, -1, 41, out _) && !Pick(one, -1, 42, out _) && !Pick(one, -1, 39, out _)
              && SocialFollowRules.WindowDays == 1,
            "call back: a text is taken up on the day it came and the next game day, not later, and never one dated after today");
        int minute = 40 * CbMinutes + 600;
        Check(!Pick(one, minute, 40, out _) && !Pick(one, minute + 5, 41, out _) && Pick(one, minute - 1, 40, out _),
            "call back: once a call opened on a text, neither it nor an older one is taken up again (the person keeps its minute)");
        var two = new List<SocialThreadLine> { CbLine("reallife_ev_fm02", 40, 10), CbLine("reallife_ev_fm08", 40, 16) };
        Check(Pick(two, -1, 40, out SocialFollowPick newest) && newest.Subject.Id == "fm08" && Pick(two, minute, 40, out SocialFollowPick after) && after.Subject.Id == "fm08"
              && !Pick(two, 40 * CbMinutes + 960, 40, out _),
            "call back: the newest text with a subject is the one taken up; one newer than the last taken up still is; the newest taken up leaves the older too");
        var mine = new List<SocialThreadLine> { CbLine("reallife_family_mom_visit_1", 40, 10), CbLine("reallife_family_sms_new_home_sent", 40, 11, true) };
        var invite = new List<SocialThreadLine> { CbLine("reallife_social_text_crash_1", 40, 9), CbLine("reallife_social_invite_coffee", 40, 14) };
        Check(Pick(mine, -1, 40, out SocialFollowPick theirs) && theirs.Subject.Id == "mom_visit" && Pick(invite, -1, 40, out SocialFollowPick under) && under.Subject.Id == "crash",
            "call back: a message the player wrote, and a text of theirs with no subject of its own (an invitation), do not hide the newest one that has one");
        var nothing = new List<SocialThreadLine> { CbLine("reallife_social_welcome_gym", 40, 8), CbLine("reallife_social_invite_meal", 40, 9) };
        bool none = !SocialFollowRules.Pick(nothing, -1, 40, out _, out string why) && why.Contains("subject")
                    && !SocialFollowRules.Pick(new List<SocialThreadLine>(), -1, 40, out _, out string empty) && empty.Length > 0 && !SocialFollowRules.Pick(null, -1, 40, out _, out _);
        Check(none, "call back: a thread with no text that has a subject, an empty one and none at all open the call as always, and say why (" + why + ")");
        Check(SocialFollowRules.DayOf(41 * CbMinutes) == 41 && SocialFollowRules.DayOf(41 * CbMinutes - 1) == 40 && SocialFollowRules.DayOf(-1) == -1,
            "call back: a text's day is its game minute's, as the game's Timestamp counts it");
    }

    // RLO Texts' words keep the written line's key they stand for, set where the words are sent.
    private static void CallBackWords()
    {
        var kept = new Dictionary<string, string> { { "text", "words" }, { SocialFollowRules.KeyField, "reallife_family_neglect_3" } };
        Check(SocialFollowRules.KeyOf(SocialFollowRules.WordsKey, kept) == "reallife_family_neglect_3" && SocialFollowRules.KeyOf(SocialFollowRules.WordsKey, null) == SocialFollowRules.WordsKey
              && SocialFollowRules.KeyOf("reallife_ev_fm02", kept) == "reallife_ev_fm02" && SocialFollowRules.KeyOf(null, kept) == null,
            "call back: a message of RLO Texts' words is read as the key it stands for; any other as its own");
        string phone = ModSource(@"Wiring\TextsPhone.cs");
        string life = ModSource(@"Social\SocialLife.cs");
        Check(phone.Contains("{ { \"text\", words }, { SocialFollowRules.KeyField, key } }") && life.Contains("public const string WordsKey = SocialFollowRules.WordsKey;"),
            "call back: the phone's held texts (the social life's and the family's) keep their written line's key beside the words, and the words' line is the one the rules read");
        Dictionary<string, string> filled = SocialFollowRules.Filled(new Dictionary<string, string> { { "text", "x" }, { SocialFollowRules.KeyField, "k" }, { "street", "5th Ave" } },
            new Dictionary<string, string> { { "first", "Dana" }, { "street", "nowhere" } });
        Check(filled["street"] == "5th Ave" && filled["first"] == "Dana" && !filled.ContainsKey("text") && !filled.ContainsKey(SocialFollowRules.KeyField),
            "call back: the call's line is filled with the text's own values first, then the speaker's, never the words or their key");
    }

    // The new fields at the end of each person's record; an older record read as before; nothing of a later version past the table.
    private static void CallBackSave()
    {
        var data = new Dictionary<string, string>();
        SocialState.Load(data, 30, 112, 9, 4747);
        SocialPerson p = SocialState.Get(2);
        p.Met = true;
        p.FollowedAt = 40 * CbMinutes + 615;
        p.AfterDay = 40;
        p.AfterKind = PlanKind.Cinema;
        p.AfterWell = false;
        p.AfterPlace = "The Grand|Cinema\nTwo";
        SocialState.Save(data);
        string record = null;
        foreach (string r in data["reallife:social_people"].Split('\n'))
        {
            record = r.StartsWith("2|", StringComparison.Ordinal) ? r : record;
        }
        string[] f = record != null ? record.Split('|') : new string[0];
        Check(f.Length == 48 && f[42] == "30" && f[43] == (40 * CbMinutes + 615).ToString() && f[44] == "40" && f[45] == ((int)PlanKind.Cinema).ToString() && f[46] == "0"
              && f[47] == "The Grand/Cinema Two",
            "call back: the minute of the text a call opened on and the outing to text about tomorrow are saved after the age's day, the last fields of the record, the place made one field");
        SocialState.Load(data, 31, 112, 9, 1);
        SocialPerson back = SocialState.Get(2);
        Check(back != null && back.FollowedAt == 40 * CbMinutes + 615 && back.AfterDay == 40 && back.AfterKind == PlanKind.Cinema && !back.AfterWell && back.AfterPlace == "The Grand/Cinema Two"
              && back.AgeDay == 30,
            "call back: read back as saved");

        // An older save: records of 43 fields (to the age's day), and of 44 (a call back's mark alone).
        string[] records = data["reallife:social_people"].Split('\n');
        var older = new string[records.Length];
        var markOnly = new string[records.Length];
        for (int i = 0; i < records.Length; i++)
        {
            string[] g = records[i].Split('|');
            older[i] = string.Join("|", g, 0, 43);
            markOnly[i] = string.Join("|", g, 0, 44);
        }
        data["reallife:social_people"] = string.Join("\n", older);
        SocialState.Load(data, 31, 112, 9, 1);
        SocialPerson old = SocialState.Get(2);
        bool olderRead = old != null && old.FollowedAt == -1 && old.AfterDay == 0 && old.AfterPlace == null && old.AgeDay == 30 && SocialState.Get(0) != null && SocialState.Get(0).Name == "Mom";
        data["reallife:social_people"] = string.Join("\n", markOnly);
        SocialState.Load(data, 31, 112, 9, 1);
        SocialPerson mark = SocialState.Get(2);
        Check(olderRead && mark != null && mark.FollowedAt == 40 * CbMinutes + 615 && mark.AfterDay == 0,
            "call back: an older save loads, its people with no text taken up yet and no outing waiting; a record that has the mark alone keeps it");

        // A later version's outing kind: let go, never a number past the table.
        string[] later = data["reallife:social_people"].Split('\n');
        for (int i = 0; i < later.Length; i++)
        {
            later[i] = records[i].Replace("|" + (int)PlanKind.Cinema + "|0|The Grand/Cinema Two", "|99|0|Somewhere");
        }
        data["reallife:social_people"] = string.Join("\n", later);
        SocialState.Load(data, 31, 112, 9, 1);
        SocialPerson strange = SocialState.Get(2);
        Check(strange != null && strange.AfterDay == 0 && strange.AfterKind == PlanKind.Coffee && strange.FollowedAt == 40 * CbMinutes + 615,
            "call back: an outing of a kind this version does not have is let go, and the rest of the record read as it is");
    }

    // The call: offers and plans first; the text spent once shown; worth a topic, nothing after the day's chat; no money.
    private static void CallBackFlow()
    {
        string dialog = ModSource(@"Social\PersonDialog.cs");
        string back = ModSource(@"Social\PersonDialog.CallBack.cs");
        string root = MemberText(dialog, "private DialogEntry Root()");
        int asked = dialog.IndexOf("else if (offer.Active && offer.Person == _p.Index)", StringComparison.Ordinal);
        int planned = dialog.IndexOf("else if (plan.Active && plan.Person == _p.Index)", StringComparison.Ordinal);
        int rooted = dialog.IndexOf("Show(Root());", StringComparison.Ordinal);
        Check(root.IndexOf("DialogEntry callBack = CallBack();", StringComparison.Ordinal) >= 0
              && root.IndexOf("DialogEntry callBack = CallBack();", StringComparison.Ordinal) < root.IndexOf("string[] pool =", StringComparison.Ordinal)
              && asked > 0 && planned > asked && rooted > planned,
            "call back: a call the player makes opens on the text before the greeting; an invitation or favour to answer and a plan that is on come first, as before");
        string pick = MemberText(back, "private DialogEntry CallBack()");
        int test = pick.IndexOf("if (_testTopic >= 0)", StringComparison.Ordinal);
        int spent = pick.IndexOf("_p.FollowedAt = pick.Minute;", StringComparison.Ordinal);
        int shown = pick.IndexOf("DialogEntry entry = Line(topic.Open[0], data);", StringComparison.Ordinal);
        int had = pick.IndexOf("if (SocialRules.ChatHad(_topics, _p.LastCallDay, SocialLife.Day))", StringComparison.Ordinal);
        int typed = pick.IndexOf("else if (Composing(topic, data, entry))", StringComparison.Ordinal);
        Check(test >= 0 && test < spent && spent < shown && shown < had && had < typed && pick.Contains("entry.OnConfirm = () => Heard(topic, true, data);")
              && pick.Contains("entry.OnConfirm = () => Reply(topic, true, data);") && pick.Contains("entry.OnSecondOption = () => Reply(topic, false, data);"),
            "call back: a test's own topic keeps its call; the text is spent before it is shown; after the day's chat its answers are words only; else typed with a pack or its two answers");
        string heard = MemberText(back, "private DialogEntry Heard(");
        Check(heard.Length > 0 && !heard.Contains("Answer") && heard.Contains("SocialLife.Contacted(_p);") && heard.Contains("return Onward("),
            "call back: an answer after the day's chat changes no figure (no answer is counted), and the call goes on as after any topic");
        Check(!back.Contains("Pay(") && !back.Contains("Give(") && !back.Contains(".Debt") && !back.Contains(".Loan") && !back.Contains("Money") && !back.Contains("SocialState.Offer")
              && !back.Contains("SocialState.Plan"),
            "call back: it moves no money and settles no favour or plan: a text that already did keeps what it did");
        string phone = ModSource(@"Social\SocialPhone.cs");
        string thread = MemberText(phone, "public static void Thread(");
        Check(thread.Contains("Contact contact = Existing(person);") && !thread.Contains("GetContact(") && !thread.Contains("Add(person") && thread.Contains("foreach (TextMessage m in messages)")
              && thread.Contains("SocialFollowRules.KeyOf(m.messageKey, m.messageData)"),
            "call back: a person's thread is read as the game keeps it, never making their contact again, each message by the key it stands for");
    }

    // The table: every line in en, pt and zh-cn; every key it names a text of the game's; ids of their own.
    private static void CallBackTable()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("call back", words, codes))
        {
            return;
        }
        var required = new[] { words[0], words[1], words[2] };
        var codesRequired = new[] { codes[0], codes[1], codes[2] };
        var missing = new List<string>();
        foreach (string key in SocialFollowCatalogue.Lines())
        {
            string lacking = LanguagesLacking(required, codesRequired, key);
            if (lacking.Length > 0)
            {
                missing.Add(key + " (" + lacking + ")");
            }
        }
        Check(SocialFollowCatalogue.All.Length >= 120 && missing.Count == 0,
            "call back: every subject's line, answers and reactions are in en, pt and zh-cn (" + SocialFollowCatalogue.All.Length + " subjects; missing: " + string.Join(", ", missing) + ")");
        var unknown = new List<string>();
        foreach (string key in SocialFollowCatalogue.Keys)
        {
            bool there = words[0].ContainsKey(key);
            if (!there)
            {
                foreach (string k in words[0].Keys)
                {
                    if (k.Length > key.Length + 1 && k.StartsWith(key + "_", StringComparison.Ordinal) && Regex.IsMatch(k.Substring(key.Length + 1), "^[0-9]+$"))
                    {
                        there = true;
                        break;
                    }
                }
            }
            if (!there)
            {
                unknown.Add(key);
            }
        }
        Check(unknown.Count == 0, "call back: every key and pool the table names is a text of the game's (none: " + string.Join(", ", unknown) + ")");
        bool ids = true, topics = true;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < SocialFollowCatalogue.All.Length; i++)
        {
            SocialFollowSubject s = SocialFollowCatalogue.All[i];
            ids &= seen.Add(s.Id) && s.Talk.Id == SocialFollowCatalogue.TopicBase + i && s.Talk.Id >= SocialCatalogue.Topics.Length && SocialFollowCatalogue.ById(s.Id) == s
                   && s.Talk.Open.Length == 1 && s.Talk.ReplyA != null && s.Talk.ReactB != null && s.Talk.TypedGood != null && s.Talk.TypedBad != null;
            topics &= !string.IsNullOrEmpty(s.Topic) && Regex.IsMatch(s.Topic, "^[A-Za-z' -]+$");
        }
        Check(ids && topics, "call back: every subject has an id of its own, a topic id past the twenty topics', one line, its answers and typed lines, and its card words in plain English");
        bool news = true;
        for (int n = 1; n <= 12; n++)
        {
            bool bad = n == 2 || n == 5 || n == 8 || n == 9;
            news &= SocialFollowCatalogue.Of("reallife_social_text_news_" + n)?.Id == (bad ? "news_bad" : "news_good")
                    && (TextsMapRules.Phone("reallife_social_text_news_" + n, TextsMapRules.From.Friend, out _) == "friend.news.bad") == bad;
        }
        Check(news && SocialFollowCatalogue.Of("reallife_social_text_news_13")?.Id == "news_good" && SocialFollowCatalogue.Of(null) == null && SocialFollowCatalogue.Of("") == null
              && SocialFollowCatalogue.Of("reallife_social_text_badday_3")?.Id == "news_bad" && SocialFollowCatalogue.Of("reallife_nothing_at_all") == null,
            "call back: a friend's bad day among their news is the bad day, as RLO Texts' map reads it; a later line of a pool is of its pool; nothing for no key");
    }

    // A call back's answer is worth what any topic's is, with the day's limits.
    private static void CallBackWorth()
    {
        string dialog = ModSource(@"Social\PersonDialog.cs");
        string reply = MemberText(dialog, "private DialogEntry Reply(");
        string answered = MemberText(dialog, "private string Answered(");
        Check(reply.Contains("Answered(topic, warm)") && answered.Contains("SocialTalk.Answer(_p, topic, warm, false, _topics == 0)") && answered.Contains("_topics++;"),
            "call back: an answer is counted as the call's topic it is, through the topics' own reckoning (the day's chat once, two topics a call)");

        // The worth of each subject's answers, by the topics' own rule: the warm answer the most a topic gives, the cool one a
        // slight only where it is one (BothFine: a joke is no slight); the day's first chat worth more than a later answer.
        var curt = new HashSet<string>(StringComparer.Ordinal) { "reallife_social_follow_sorry_b", "reallife_social_follow_repaid_b", "reallife_social_topic_arrest_b",
            "reallife_social_topic_badday_b", "reallife_social_topic_goodday_b", "reallife_social_topic_memory_b", "reallife_social_topic_birthday_b", "reallife_social_topic_rich_b",
            "reallife_social_topic_theirjob_b", "reallife_social_topic_sick_b" };
        var wrong = new List<string>();
        foreach (SocialFollowSubject s in SocialFollowCatalogue.All)
        {
            SocialWorth warm = SocialRules.Chat(true, false, true, true, s.Talk.BothFine), cool = SocialRules.Chat(true, false, true, false, s.Talk.BothFine);
            SocialWorth topic = SocialRules.Chat(true, false, true, true, false);
            bool slight = cool.Affection < SocialRules.Chat(true, false, true, false, true).Affection;
            if (warm.Affection != topic.Affection || warm.Familiarity != topic.Familiarity || warm.Trust != topic.Trust || cool.Affection > warm.Affection
                || slight != (!s.Talk.BothFine) || (!s.Talk.BothFine && !curt.Contains(s.Talk.ReplyB)))
            {
                wrong.Add(s.Id);
            }
        }
        SocialWorth first = SocialRules.Chat(true, false, true, true, false), again = SocialRules.Chat(false, false, true, true, false);
        Check(wrong.Count == 0 && first.Familiarity > again.Familiarity && first.Affection > again.Affection,
            "call back: each subject's warm answer is worth a topic's warm answer, its cool one a slight only where the answer is curt; the day's first chat counts once (wrong: "
            + string.Join(", ", wrong) + ")");
    }

    // What's new and the Guide say so, in every language; the tests' labels in en, pt and zh-cn.
    private static void CallBackTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("call back texts", words, codes))
        {
            return;
        }
        var line = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Calling somebody back after a text opens on what they wrote about" }, { "pt", "Ligar de volta para alguém depois de uma mensagem" },
            { "zh-cn", "回电话给发来短信的人" }, { "de", "Ein Rückruf nach einer Nachricht" }, { "fr", "Rappeler quelqu'un après un message" },
            { "es", "Devolver la llamada tras un mensaje" }, { "ru", "Перезвонив после сообщения" }, { "nl", "Iemand terugbellen na een bericht" },
            { "tr", "Bir mesajın ardından birini geri aramak" }
        };
        var guide = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "- Call somebody back the day they text you" }, { "pt", "- Ligue de volta no dia em que a pessoa mandar uma mensagem" },
            { "zh-cn", "- 在对方发来短信的当天或第二天回电话" }, { "de", "- Ruf jemanden am Tag zurück, an dem er dir schreibt" },
            { "fr", "- Rappelez quelqu'un le jour où il vous écrit" }, { "es", "- Devuelve la llamada el día en que alguien te escribe" },
            { "ru", "- Перезвоните человеку в тот день, когда он вам написал" }, { "nl", "- Bel iemand terug op de dag dat die je een bericht stuurt" },
            { "tr", "- Size mesaj attığı gün ya da ertesi gün birini geri arayın" }
        };
        var wrong = new List<string>();
        for (int l = 0; l < codes.Length; l++)
        {
            if (!line.TryGetValue(codes[l], out string part) || !AnyChangesRow(words[l], part))
            {
                wrong.Add(codes[l] + " What's new");
            }
            if (!guide.TryGetValue(codes[l], out string bullet) || !words[l].TryGetValue("reallife_guide_social", out string page) || !page.Contains(bullet))
            {
                wrong.Add(codes[l] + " Guide");
            }
        }
        string lacking = string.Empty;
        foreach (string key in new[] { "reallife_social_test_followup", "reallife_social_test_nextday", "reallife_social_test_followup_other" })
        {
            string l = LanguagesLacking(new[] { words[0], words[1], words[2] }, new[] { codes[0], codes[1], codes[2] }, key);
            lacking += l.Length > 0 ? key + " (" + l + ") " : string.Empty;
        }
        Check(codes.Length == 9 && wrong.Count == 0 && lacking.Length == 0,
            "call back: What's new and the Guide's People page say so in all nine languages, and the tests' buttons are in en, pt and zh-cn (wrong: " + string.Join(", ", wrong) + " " + lacking + ")");
    }
}
