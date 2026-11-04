using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;

// Round 7 of the Flare audit fix: one world behind the badges and the pages they open, and no fact told twice on one screen. The samples are one world (UiSamples.FlareNewNotices
// and FlareUnreadMessages), the side column leaves out what the page itself shows (FlareScreen.Side reads the page first), and the Live fill is held to the same by its source.
internal static partial class Program
{
    private static void FlareTwiceChecks()
    {
        string badges = "none", twice = "none", rail = "none";
        int pages = 0, texts = 0;
        UiWords en = UiWords("en");
        var scene = new UiScene();
        foreach (string state in UiSamples.FlareStates)
        {
            FlareFaceModel m = UiSamples.FlareSample(en, state);
            UiSamples.Flare(scene, UiFrame.FullMenu(16f / 9f, "Flare"), true, UiDefaultKeys.Keyboard, en, state);
            if (m.Sheet != FlareFace.NoSheet)
            {
                continue;   // a sheet stands over a page that is checked on its own
            }
            pages++;
            string say = FlareBadges(m, scene, state);
            badges = say != null && badges == "none" ? say : badges;
            say = FlareTwice(scene, state, ref texts);
            twice = say != null && twice == "none" ? say : twice;
            say = FlareRail(m, state);
            rail = say != null && rail == "none" ? say : rail;
        }
        Check(badges == "none" && pages > 20, "flare world: each badge on the nav is the count its page opens with, and every page shows the same badge (" + pages + " pages; " + badges + ")");
        Check(twice == "none" && texts > 500, "flare world: no figure, name or sentence is told twice on one screen (" + texts + " texts over " + pages + " pages; " + twice + ")");
        Check(rail == "none", "flare world: the side column lists no tag and no account that the page itself lists (" + rail + ")");
        // The Live fill does what the samples show: the side is filled after the page and leaves out its tags and accounts, the notifications leave out what waits as a card, and the
        // profile's own week leaves out the followers.
        string fill = PlayFile("Ui", "Flare", "FlareScreen.Fill.cs"), desk = PlayFile("Ui", "Flare", "FlareScreen.Desk.cs");
        int side = fill.IndexOf("Side(file, now);   // after the page", StringComparison.Ordinal), page = fill.IndexOf("case FlareFace.Thread: Thread(file, now); break;", StringComparison.Ordinal);
        Check(side > page && page > 0 && fill.Contains("_bodyTags.Contains(_trendTags[i])") && fill.Contains("_bodyAccounts.Contains(_whoKeys[i])")
            && fill.Contains("_bodyAccounts.Add(key);") && fill.Contains("_bodyTags.Add(tag);") && fill.Contains("_bodyTags.Add(_tag);") && !fill.Contains("reallife_flare_kit_section_accounts"),
            "flare world: the Live fill reads the page before the side, which leaves out the page's tags and accounts, and Explore lists no accounts of its own");
        Check(fill.Contains("List<FlareNotice> notices = Notified(file, now);") && fill.Contains("n.Kind == FlareNoticeKind.Offer") && fill.Contains("AwaitsAnswer(file, file.Post(n.Post), mine, now)")
            && fill.Contains("if (!AwaitsAnswer(file, p, mine, now))") && !fill.Contains("_flare.KitNotices();\n            _unseen") && fill.Contains("_badges[FlareFace.Notices] = _page == FlareFace.Notices ? 0 : _unseen;")
            && fill.Contains("int unread = _flare.UnreadMessages;") && fill.Contains("_badges[FlareFace.Messages] = _flare.UnreadMessages;"),
            "flare world: Live badges and heads read one source each (the notifications as Notified lists them, the director's unread messages), and an open offer or an unanswered review is a card, not also a line");
        Check(fill.Contains("own = _page == FlareFace.Profile") && fill.Contains("labels = new[] { labels[1], labels[2] };") && fill.Contains("_m.Line = own ? null"),
            "flare world: on the player's own profile the week beside it leaves out the followers and their line");
        // A page of its own opens with focus reset: on its first target, or on the nav entry that was pressed, from which right goes in (the focus rules are FlareOrderPage's).
        string screen = PlayFile("Ui", "Flare", "FlareScreen.cs"), face = PlayFile("Ui", "Flare", "FlareFace.cs");
        Check(screen.Contains("_m.NewPage = true;") && screen.Contains("_m.NewPage = false;") && screen.Contains("NavPressed(arg)") && screen.Contains("_m.FromNav = true;") && face.Contains("s.FocusReset = true;")
            && face.Contains("m.FromNav && chosenEntry >= 0 ? chosenEntry : s.FocusFirst"),
            "flare world: a new page resets focus, a press on the nav keeps it on its entry, and the typing in the side's search does not take it away");
        // What each post and ad did in the city is told again from the director's record of it, on the post and in the studio.
        Check(fill.Contains("_flare.FunnelOfPost(p.Id)") && fill.Contains("private void FunnelRow(FlareFunnel f, string business)") && desk.Contains("_flare.AdFunnels(_adFunnels, AdsMost);")
            && desk.Contains("FunnelRow(_adFunnels[i], _flare.BusinessNameAt(_adFunnels[i].Place)"),
            "flare world: a post's own page and the studio's list say what it did in the city (seen, came, spent, put off) from the director's funnel record");
    }

    // The nav's badge for each page is the count that page's own head says, and the same everywhere else.
    private static string FlareBadges(FlareFaceModel m, UiScene scene, string state)
    {
        int notices = UiSamples.FlareNewNotices, messages = UiSamples.FlareUnreadMessages;
        int open = 0;   // the unread count of the conversation open: it is read, so the badge and the head leave it out
        for (int i = 0; m.Page == FlareFace.Messages && i < m.List.Count; i++)
        {
            open += (m.List.Rows[i].Flags & FlareRowFlags.Chosen) != 0 ? Math.Max(0, FirstNumber(m.List.Rows[i].Value)) : 0;
        }
        if (m.Badges[FlareFace.Messages] != messages - open)
        {
            return state + ": the Messages badge is " + m.Badges[FlareFace.Messages] + ", the world has " + messages + " unread less " + open + " in the open conversation";
        }
        if (m.Page != FlareFace.Notices && m.Badges[FlareFace.Notices] != notices)
        {
            return state + ": the Notifications badge is " + m.Badges[FlareFace.Notices] + ", the world has " + notices + " new";
        }
        if (m.Page == FlareFace.Notices)
        {
            if (m.Badges[FlareFace.Notices] != 0)
            {
                return state + ": the open Notifications page still has a badge";
            }
            int said = FirstNumber(m.HeadLine);
            int dots = 0;
            for (int i = 0; i < m.List.Count; i++)
            {
                dots += (m.List.Rows[i].Flags & FlareRowFlags.Unread) != 0 && m.List.Rows[i].Kind == FlareRowKind.Notice ? 1 : 0;
            }
            if (m.Tabs.Length > 0 && said != notices)
            {
                return state + ": the page says " + said + " new, the world has " + notices;
            }
            if (m.Tabs.Length > 0 && m.Tab == 0 && dots != said)
            {
                return state + ": the page says " + said + " new and marks " + dots + " lines";
            }
        }
        if (m.Page == FlareFace.Messages)
        {
            int said = Math.Max(0, FirstNumber(m.HeadLine)), sum = 0;
            for (int i = 0; i < m.List.Count; i++)
            {
                sum += m.List.Rows[i].Kind == FlareRowKind.Thread && (m.List.Rows[i].Flags & FlareRowFlags.Chosen) == 0 ? Math.Max(0, FirstNumber(m.List.Rows[i].Value)) : 0;
            }
            if (said != m.Badges[FlareFace.Messages] || sum != said)
            {
                return state + ": the page says " + said + " unread and its conversations mark " + sum + ", the badge says " + m.Badges[FlareFace.Messages];
            }
        }
        // and the nav draws it: a badge text of that count in the nav's column
        float navRight = NavRight(scene);
        for (int page = 0; page < 2; page++)
        {
            int want = page == 0 ? notices : m.Badges[FlareFace.Messages], entry = page == 0 ? FlareFace.Notices : FlareFace.Messages;
            if ((entry == FlareFace.Notices && m.Page == FlareFace.Notices) || want <= 0)
            {
                continue;
            }
            bool drawn = false;
            for (int i = 0; i < scene.ItemCount && !drawn; i++)
            {
                drawn = scene.Items[i].Kind == UiKind.Text && scene.Items[i].Text == want.ToString(System.Globalization.CultureInfo.InvariantCulture) && scene.Items[i].X < navRight
                    && scene.Items[i].Style == UiStyle.Badge;
            }
            if (!drawn)
            {
                return state + ": the nav does not draw the " + want + " of " + (page == 0 ? "Notifications" : "Messages");
            }
        }
        return null;
    }

    private static float NavRight(UiScene scene)
    {
        UiFrame.FullMenu(16f / 9f, "Flare").Window(out float wx, out _, out _, out _);
        return wx + FlareFace.NavWidth;
    }

    private static int FirstNumber(string words)
    {
        Match m = words != null ? Regex.Match(words, @"\d+") : Match.Empty;
        return m.Success ? int.Parse(m.Value) : -1;
    }

    // No sentence, name or figure is drawn twice in the page (the nav and the window's strip are the frame's own). Labels and the words of a control (Follow, Reply, a tab) are not facts and may repeat.
    private static string FlareTwice(UiScene scene, string state, ref int texts)
    {
        float navRight = NavRight(scene);
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var where = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < scene.ItemCount; i++)
        {
            ref UiItem it = ref scene.Items[i];
            if (it.Kind != UiKind.Text || it.Layer != 0 || it.X < navRight || string.IsNullOrEmpty(it.Text) || (it.Flags & UiFlags.Chrome) != 0)
            {
                continue;
            }
            string text = it.Text.Trim();
            texts++;
            bool fact = text.Length >= 18 || Regex.IsMatch(text, @"\d");   // a figure or a sentence; a label such as Followers over a figure is a word, not a fact
            if (!fact || text.Length < 4)
            {
                continue;
            }
            seen.TryGetValue(text, out int n);
            seen[text] = n + 1;
            where.TryGetValue(text, out string at);
            where[text] = (at != null ? at + " and " : string.Empty) + (int)it.X + "," + (int)it.Y;
        }
        foreach (KeyValuePair<string, int> pair in seen)
        {
            if (pair.Value > 1)
            {
                return state + " says \"" + pair.Key + "\" " + pair.Value + " times (at " + where[pair.Key] + ")";
            }
        }
        return null;
    }

    // The side column and the page: no tag and no account in both.
    private static string FlareRail(FlareFaceModel m, string state)
    {
        for (int i = 0; i < m.Trends.Count; i++)
        {
            string tag = m.Trends.Rows[i].Name;
            for (int k = 0; k < m.List.Count; k++)
            {
                if (m.List.Rows[k].Kind == FlareRowKind.Tag && m.List.Rows[k].Name == tag)
                {
                    return state + " lists " + tag + " in the page and in the side";
                }
            }
            if (m.Head == tag && FlareFace.HasSide(m.Page))
            {
                return state + " heads the page " + tag + " and lists it in the side";
            }
        }
        for (int i = 0; i < m.Who.Count; i++)
        {
            string name = m.Who.Rows[i].Name;
            for (int k = 0; k < m.List.Count; k++)
            {
                if (m.List.Rows[k].Kind == FlareRowKind.Account && m.List.Rows[k].Name == name)
                {
                    return state + " lists " + name + " in the page and in the side";
                }
            }
            if (m.Page == FlareFace.Profile && m.Profile.Name == name)
            {
                return state + " heads the page with " + name + " and lists them in the side";
            }
        }
        return null;
    }
}
