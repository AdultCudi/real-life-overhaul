using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;

// Round 8 of the Flare audit fix: every state of Flare is one world (UiSamples.FlareWorld) and its figures agree with each other. The rail's week is the world's, worded by the
// functions the Live face calls; a head says no figure that a tile or a post already shows; a post's reply count is its replies; what the city saw of a post is never more than
// the post's views; a new account has no week anywhere and its empty state is said once; the businesses' week is the studio's; and the account's reach by day is the week's reach.
// The Live fill is held to the same by its source.
internal static partial class Program
{
    private static readonly Regex FigureToken = new Regex(@"[+\-$]*\d[\d,.]*[KM%]?", RegexOptions.Compiled);

    private static void FlareWorldChecks()
    {
        UiWords en = UiWords("en");
        var scene = new UiScene();
        string rail = "none", head = "none", replies = "none", seen = "none", fresh = "none", entity = "none", same = "none";
        int states = 0, figures = 0, posts = 0;
        var counts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string state in UiSamples.FlareStates)
        {
            FlareFaceModel m = UiSamples.FlareSample(en, state);
            if (m.Sheet != FlareFace.NoSheet)
            {
                continue;
            }
            UiSamples.Flare(scene, UiFrame.FullMenu(16f / 9f, "Flare"), true, UiDefaultKeys.Keyboard, en, state);
            UiSamples.FlareWorld world = UiSamples.FlareWorldOf(state);
            states++;
            string say = FlareRailAgrees(m, world, state);
            rail = say != null && rail == "none" ? say : rail;
            say = FlareHeadAlone(m, scene, state, ref figures);
            head = say != null && head == "none" ? say : head;
            say = FlareRepliesAgree(m, state);
            replies = say != null && replies == "none" ? say : replies;
            say = FlareSeenWithinViews(m, world, state);
            seen = say != null && seen == "none" ? say : seen;
            say = FlareNewTold(m, world, scene, state);
            fresh = say != null && fresh == "none" ? say : fresh;
            say = FlareNamedOnce(m, state);
            entity = say != null && entity == "none" ? say : entity;
            say = FlarePostSaysTheSame(m, state, counts, ref posts);
            same = say != null && same == "none" ? say : same;
        }
        Check(rail == "none" && states > 20, "flare world: the side's week is the world's (followers, what the posts reached, the rating of the businesses together), the followers' line ends on them, and Who to follow keeps eight rows ("
            + states + " pages; " + rail + ")");
        Check(head == "none" && figures > 10, "flare world: a page's head says no figure and no word that a tile, a row or a post of the same page already shows (" + figures + " head parts; " + head + ")");
        Check(replies == "none", "flare world: the count on a post's card is the replies listed under it and the more its list says (" + replies + ")");
        Check(same == "none" && posts > 8, "flare world: a post says the same likes, replies, reposts and views on the feed, the profile and in its own thread, a player's post with replies its thread does not list and a neighbour's alike ("
            + posts + " cards; " + same + ")");
        Check(seen == "none", "flare world: what the city saw of a post is never more than the views the post shows, and the week's seen is never more than the week's reach (" + seen + ")");
        Check(fresh == "none", "flare world: a new account has no week in the side or on the city page, its empty state is said once, and no page shows a row of zeros (" + fresh + ")");
        Check(entity == "none", "flare world: an account named in the page (a post, a card, a story, a conversation, a head) is not offered again in the side (" + entity + ")");
        FlareWorldFigures(en);
        FlareCityAndHire(en);
        FlareFiguresKeptWhole();
        FlareWorldLive();
    }

    // The city page's donut says what it adds up to, and that is the followers placed in districts out of the rail's; Jade Rivers is one record for the hire table, the card and the
    // sheet; an empty city page does not call the city quiet over a rail of trends; a post count is no other figure of the world.
    private static void FlareCityAndHire(UiWords en)
    {
        UiSamples.FlareWorld known = UiSamples.FlareWorldOf("home");
        FlareFaceModel city = UiSamples.FlareSample(en, "city");
        long sum = (long)city.City.Rest;
        for (int i = 0; i < city.City.Values.Length; i++)
        {
            sum += ParseFigure(city.City.Values[i]);
        }
        string total = city.City.DonutTotal ?? string.Empty;
        string bad = "none";
        if (sum != known.FollowersInCity || !total.StartsWith(FlareFigures.N(sum) + " of your " + city.WeekFigures[0] + " followers", StringComparison.Ordinal))
        {
            bad = "the donut's slices add to " + sum + " and it says \"" + total + "\" beside the rail's " + city.WeekFigures[0] + " followers (the world places " + known.FollowersInCity + ")";
        }
        else if (known.FollowersInCity > known.Followers)
        {
            bad = "the city holds " + known.FollowersInCity + " followers of an account that has " + known.Followers;
        }
        Check(bad == "none", "flare world: the city page's donut adds up to the followers it places in districts and says so beside the rail's figure (" + bad + ")");

        FlareFaceModel sheet = UiSamples.FlareSample(en, "hire_sheet");
        UiSamples.FlareInfluencerWorld jade = known.Influencers[0];
        bad = "none";
        if (string.Join("|", sheet.Hire.AudienceLabels) != string.Join("|", sheet.Sheet0.BarLabels) || string.Join("|", sheet.Hire.AudienceFigures) != string.Join("|", sheet.Sheet0.BarFigures)
            || sheet.Hire.AudienceLabels.Length == 0)
        {
            bad = "the card names " + string.Join(", ", sheet.Hire.AudienceLabels) + " at " + string.Join(", ", sheet.Hire.AudienceFigures) + " and the sheet " + string.Join(", ", sheet.Sheet0.BarLabels)
                + " at " + string.Join(", ", sheet.Sheet0.BarFigures);
        }
        else if (!sheet.Hire.Note.Contains(FlareFigures.Odds(jade.Flop) + "\u00a0in\u00a0100") || !sheet.Sheet0.Note.Contains(FlareFigures.Odds(jade.Flop) + "\u00a0in\u00a0100")
            || !sheet.Hire.Note.Contains(FlareFigures.Odds(jade.Viral) + "\u00a0in\u00a0100") || !sheet.Sheet0.Note.Contains(FlareFigures.Odds(jade.Viral) + "\u00a0in\u00a0100"))
        {
            bad = "the card says \"" + sheet.Hire.Note + "\" and the sheet \"" + sheet.Sheet0.Note + "\" of one influencer's odds";
        }
        else if (sheet.Hire.AudienceFigures[0] != FlareFigures.Percent(jade.AudienceCounts[0], jade.AudienceTotal) || sheet.Sheet0.Figures[3] != jade.District)
        {
            bad = "the audience is not the influencer record's";
        }
        Check(bad == "none", "flare world: an influencer's card and hire sheet name the same districts at the same shares and give the same odds, both from the one record (" + bad + ")");

        bad = "none";
        foreach (string state in new[] { "city_empty", "city_new" })
        {
            FlareFaceModel m = UiSamples.FlareSample(en, state);
            string said = (m.List.EmptyTitle + " " + m.List.EmptyBody).ToLowerInvariant();
            if (m.Trends.Count > 0 && (said.Contains("quiet") || said.Contains("trend") || said.Contains("nothing is happening")))
            {
                bad = state + " says \"" + m.List.EmptyTitle + ". " + m.List.EmptyBody + "\" over a rail of " + m.Trends.Count + " trending tags";
            }
        }
        Check(bad == "none", "flare world: the city page's empty words claim nothing about trends while its rail lists them (" + bad + ")");

        bad = "none";
        for (int i = 0; i < known.Mine.Length && bad == "none"; i++)
        {
            FlareWorldPostCoincidence(known, known.Mine[i], ref bad);
        }
        Check(bad == "none", "flare world: the number of posts is no like, reply, repost or view count of the world (" + bad + ")");
    }

    private static void FlareWorldPostCoincidence(UiSamples.FlareWorld known, UiSamples.FlareMyPost p, ref string bad)
    {
        if (known.PostsTotal == p.Likes || known.PostsTotal == p.Replies || known.PostsTotal == p.Reposts || known.PostsTotal == known.Following)
        {
            bad = known.PostsTotal + " posts is also post " + p.Id + "'s likes, replies or reposts, or the accounts followed";
        }
    }

    // A figure is never parted from what it counts when a note wraps: the spaced languages by no-break spaces, Chinese by the width Balanced picks.
    private static void FlareFiguresKeptWhole()
    {
        string bad = "none";
        int widths = 0;
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            UiWords t = UiWords(lang);
            FlareFaceModel m = UiSamples.FlareSample(t, "hire_sheet");
            foreach (string note in new[] { m.Hire.Note, m.Sheet0.Note })
            {
                for (float width = 360f; width <= 900f && bad == "none"; width += 6f)
                {
                    float used = FlareFace.Balanced(note, UiStyle.Body, width, 2);
                    int lines = UiTextRules.Lines(note, UiStyle.Body, used, 0, out float _);
                    widths++;
                    for (int k = 1; k < lines && bad == "none"; k++)
                    {
                        int at = UiTextRules.LineStart(note, UiStyle.Body, used, k);
                        if (at <= 0 || at >= note.Length)
                        {
                            continue;
                        }
                        int before = at - 1;
                        while (before > 0 && note[before] == ' ')
                        {
                            before--;
                        }
                        bool cut = UiTextRules.Wide(note[at]) ? char.IsDigit(note[before]) || note[before] == '%' : false;
                        if (!cut && note[at - 1] == ' ')
                        {
                            int from = note.LastIndexOf(' ', Math.Max(0, at - 2)) + 1;
                            string word = note.Substring(from, at - 1 - from);
                            cut = word.Length > 0 && char.IsDigit(word[word.Length - 1]) && char.IsLetter(note[at]);   // a figure at the end of a line and its noun at the start of the next
                        }
                        if (cut)
                        {
                            bad = lang + ": at " + (int)used + " px the note \"" + note + "\" starts line " + (k + 1) + " at \"" + note.Substring(at, Math.Min(6, note.Length - at)) + "\", parted from its figure";
                        }
                    }
                }
            }
        }
        Check(bad == "none" && widths > 300, "flare world: a note that wraps never parts a figure from what it counts, in English, Portuguese and Chinese (" + widths + " widths; " + bad + ")");
        Check(FlareFigures.Bind("12 in 100 posts like this fall flat, 9 in 100 go viral.") == "12\u00a0in\u00a0100\u00a0posts like this fall flat, 9\u00a0in\u00a0100\u00a0go viral."
            && FlareFigures.Bind("12 em cada 100 posts assim") == "12\u00a0em\u00a0cada\u00a0100\u00a0posts assim" && FlareFigures.Bind("Nothing here yet") == "Nothing here yet"
            && FlareFigures.Bind(null) == null, "flare world: FlareFigures.Bind joins a figure to its noun and the words between two figures, and nothing else");
    }

    // The rail of a state is the world's week worded by FlareFigures, and no more than eight accounts.
    private static string FlareRailAgrees(FlareFaceModel m, UiSamples.FlareWorld world, string state)
    {
        if (!FlareFace.HasSide(m.Page))
        {
            return null;
        }
        FlareWeekFacts w = world.Week();
        if (world.NewPlayer)
        {
            return m.WeekLabels.Length > 0 || m.Line != null ? state + " draws a week for a new account (" + string.Join(" ", m.WeekFigures) + ")" : null;
        }
        bool own = m.Page == FlareFace.Profile && m.Profile.Edit != null;
        int at = own ? -1 : 0;   // the followers are the first tile unless the profile's head says them
        if (m.WeekLabels.Length != (own ? 2 : 3))
        {
            return state + " has " + m.WeekLabels.Length + " tiles in its week";
        }
        string reach = FlareFigures.Count(w.Reach), rating = FlareFigures.Stars(w.StarsMilli);
        if (!own && m.WeekFigures[0] != FlareFigures.Count(w.Followers))
        {
            return state + " says " + m.WeekFigures[0] + " followers, the world has " + w.Followers;
        }
        if (m.WeekFigures[1 + at] != reach)
        {
            return state + " says " + m.WeekFigures[1 + at] + " reached, the posts of the week reached " + w.Reach;
        }
        if (m.WeekFigures[2 + at] != rating || !m.WeekDeltas[2 + at].Contains(FlareFigures.N(w.Reviews)))
        {
            return state + " says " + m.WeekFigures[2 + at] + " on " + m.WeekDeltas[2 + at] + ", the businesses have " + rating + " on " + w.Reviews + " reviews";
        }
        if (!own && (m.Line == null || m.Line[m.Line.Length - 1] != w.Followers || !m.WeekDeltas[0].Contains(FlareFigures.Signed(w.Gain))))
        {
            return state + "'s followers' line or its gain does not end on the world's " + w.Followers + " (" + m.WeekDeltas[0] + ")";
        }
        if (m.Who.Count > 8)
        {
            return state + " lists " + m.Who.Count + " accounts to follow";
        }
        return null;
    }

    // The words of a head that is not the page's name: no figure of it, and no part of it, is drawn again by another text of the page.
    private static string FlareHeadAlone(FlareFaceModel m, UiScene scene, string state, ref int figures)
    {
        float navRight = NavRight(scene);
        foreach (string head in new[] { m.HeadLine, m.DetailLine })
        {
            if (string.IsNullOrEmpty(head))
            {
                continue;
            }
            var parts = new List<string>(head.Split(new[] { ", ", " \u00b7 " }, StringSplitOptions.RemoveEmptyEntries));
            foreach (Match token in FigureToken.Matches(head))
            {
                if (token.Value.Length >= 2)
                {
                    parts.Add(token.Value);
                }
            }
            for (int p = 0; p < parts.Count; p++)
            {
                figures++;
                for (int i = 0; i < scene.ItemCount; i++)
                {
                    ref UiItem it = ref scene.Items[i];
                    if (it.Kind != UiKind.Text || it.Layer != 0 || it.X < navRight || string.IsNullOrEmpty(it.Text) || (it.Flags & UiFlags.Chrome) != 0 || it.Text.Trim() == head.Trim())
                    {
                        continue;
                    }
                    bool whole = it.Text.Trim().Equals(parts[p].Trim(), StringComparison.OrdinalIgnoreCase) && parts[p].Trim().Length >= 4;
                    bool figure = false;
                    foreach (Match other in FigureToken.Matches(it.Text))
                    {
                        figure |= other.Value == parts[p] && parts[p].Length >= 3 && FigureToken.IsMatch(parts[p]);   // a figure of three marks or more: 2.4K, $1,180; a bare 12 is a coincidence
                    }
                    if (whole || figure)
                    {
                        return state + ": the head \"" + head + "\" says \"" + parts[p] + "\" and \"" + it.Text + "\" does too (at " + (int)it.X + "," + (int)it.Y + ")";
                    }
                }
            }
        }
        return null;
    }

    // A post's card counts the replies listed under it and the line of "more" replies.
    private static string FlareRepliesAgree(FlareFaceModel m, string state)
    {
        if (m.Page != FlareFace.Thread)
        {
            return null;
        }
        int post = -1, listed = 0, more = 0;
        for (int i = 0; i < m.List.Count; i++)
        {
            ref FlareRow r = ref m.List.Rows[i];
            if (r.Kind == FlareRowKind.Post && (r.Flags & FlareRowFlags.Comment) == 0 && post < 0)
            {
                post = i;
            }
            else if (r.Kind == FlareRowKind.Post && (r.Flags & FlareRowFlags.Comment) != 0)
            {
                listed++;
            }
            else if (r.Kind == FlareRowKind.Section && post >= 0 && FirstNumber(r.Name) > 0 && r.Name.Contains("more"))
            {
                more = FirstNumber(r.Name);
            }
        }
        if (post < 0)
        {
            return state + " shows no post";
        }
        if (listed + more > 0 && !string.IsNullOrEmpty(m.HeadLine))
        {
            return state + " has the head \"" + m.HeadLine + "\" over a post that counts its replies itself";
        }
        string said = m.List.Rows[post].Comments;
        int count = said == null ? 0 : (int)ParseFigure(said);
        if (count != FlareFigures.Replies(listed, more))
        {
            return state + ": the post says " + (said ?? "no") + " replies, the list shows " + listed + " and " + more + " more";
        }
        return null;
    }

    // One post, one set of counts: the card of a post (by its author and its words) says the same wherever the sample draws it, the feed, a profile, a search and its own thread, whose
    // card is the one that is worked out from the rows the thread lists and the "more" line under them. The threads are checked among the rest by this.
    private static string FlarePostSaysTheSame(FlareFaceModel m, string state, Dictionary<string, string> counts, ref int posts)
    {
        string bad = null;
        for (int i = 0; i < m.List.Count; i++)
        {
            ref FlareRow r = ref m.List.Rows[i];
            if (r.Kind != FlareRowKind.Post || (r.Flags & FlareRowFlags.Comment) != 0 || string.IsNullOrEmpty(r.Text))
            {
                continue;
            }
            posts++;
            string key = r.Name + "|" + r.Text, said = (r.Likes ?? "-") + " likes, " + (r.Comments ?? "-") + " replies, " + (r.Reposts ?? "-") + " reposts, " + (r.Views ?? "-") + " views";
            if (counts.TryGetValue(key, out string was) && was != said && bad == null)
            {
                bad = state + " says " + r.Name + "'s post has " + said + " where another page says " + was;
            }
            counts[key] = said;
        }
        return bad;
    }

    // Seen by N under a post is never above the views the post shows, and every post of the world keeps to it.
    private static string FlareSeenWithinViews(FlareFaceModel m, UiSamples.FlareWorld world, string state)
    {
        if (m.Page == FlareFace.Thread)
        {
            long views = -1, seenBy = -1;
            for (int i = 0; i < m.List.Count; i++)
            {
                ref FlareRow r = ref m.List.Rows[i];
                if (r.Kind == FlareRowKind.Post && (r.Flags & FlareRowFlags.Mine) != 0 && views < 0)
                {
                    views = r.Views != null ? ParseFigure(r.Views) : 0;
                }
                if (r.Kind == FlareRowKind.Result && r.Text != null && r.Text.StartsWith("Seen by", StringComparison.Ordinal))
                {
                    seenBy = ParseFigure(r.Text.Substring("Seen by ".Length).Split(' ')[0]);
                }
            }
            if (seenBy >= 0 && views >= 0 && seenBy > views)
            {
                return state + ": seen by " + seenBy + " is more than the post's " + views + " views";
            }
        }
        for (int i = 0; i < world.Mine.Length; i++)
        {
            if (world.Mine[i].FunnelAt >= 0 && world.Funnels[world.Mine[i].FunnelAt].Seen > world.Mine[i].Views)
            {
                return "the post " + world.Mine[i].Id + " was seen by " + world.Funnels[world.Mine[i].FunnelAt].Seen + " and viewed " + world.Mine[i].Views;
            }
        }
        world.CityWeek(out int week, out _, out _);
        return week > world.WeekReach ? state + ": the city saw " + week + " in the week and the posts reached " + world.WeekReach : null;
    }

    // A new account: nothing counted in the side, nothing on the city page but its sentence, and no row of zeros; an account with followers says nothing of nobody following.
    private static string FlareNewTold(FlareFaceModel m, UiSamples.FlareWorld world, UiScene scene, string state)
    {
        bool nobody = !string.IsNullOrEmpty(m.City.DonutEmpty) && m.City.Names.Length == 0;   // the sentence is drawn only where there are no districts to name
        if (world.NewPlayer && m.Page == FlareFace.City)
        {
            if (!nobody || m.City.WeekLabels.Length > 0 || m.City.Names.Length > 0)
            {
                return state + " is a new account and its city page has " + (nobody ? "a week of zeros" : "no empty sentence");
            }
        }
        else if (!world.NewPlayer && nobody)
        {
            return state + " says nobody follows an account that has " + world.Followers + " followers";
        }
        int told = 0;
        for (int i = 0; i < scene.ItemCount; i++)
        {
            told += scene.Items[i].Kind == UiKind.Text && scene.Items[i].Text != null && scene.Items[i].Text.StartsWith("Nobody in the city follows you", StringComparison.Ordinal) ? 1 : 0;
        }
        if (told > 1)
        {
            return state + " says nobody follows the new account " + told + " times";
        }
        // no row of zeros: three or more figures in a row that are all nothing
        string[][] rows = { m.City.WeekFigures, m.WeekFigures, m.Studio.Figures };
        for (int r = 0; r < rows.Length; r++)
        {
            int zeros = 0;
            for (int i = 0; i < rows[r].Length; i++)
            {
                zeros += rows[r][i] == "0" || rows[r][i] == "$0" ? 1 : 0;
            }
            if (zeros >= 2)
            {
                return state + " shows a row of " + zeros + " zeros (" + string.Join(" ", rows[r]) + ")";
            }
        }
        return null;
    }

    // An account the page names (a post's author, a card, a story, a conversation, a profile, the head) is not also offered in the side, nor a tag the page lists or heads.
    private static string FlareNamedOnce(FlareFaceModel m, string state)
    {
        if (!FlareFace.HasSide(m.Page))
        {
            return null;
        }
        var body = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < m.List.Count; i++)
        {
            ref FlareRow r = ref m.List.Rows[i];
            if (r.Kind != FlareRowKind.Section && r.Kind != FlareRowKind.Notice && r.Kind != FlareRowKind.Bubble && !string.IsNullOrEmpty(r.Name))
            {
                body.Add(r.Name);
            }
        }
        for (int i = 0; i < m.Stories.Length; i++)
        {
            body.Add(m.Stories[i]);
        }
        if (m.Page == FlareFace.Profile && !string.IsNullOrEmpty(m.Profile.Name))
        {
            body.Add(m.Profile.Name);
        }
        if (!string.IsNullOrEmpty(m.Head))
        {
            body.Add(m.Head);
        }
        for (int i = 0; i < m.Who.Count; i++)
        {
            if (body.Contains(m.Who.Rows[i].Name))
            {
                return state + " names " + m.Who.Rows[i].Name + " in the page and offers them in the side";
            }
        }
        for (int i = 0; i < m.Trends.Count; i++)
        {
            if (body.Contains(m.Trends.Rows[i].Name))
            {
                return state + " names " + m.Trends.Rows[i].Name + " in the page and in the side";
            }
        }
        return null;
    }

    // The world on its own: the figures the pages quote of the businesses, the studio and the city are one set.
    private static void FlareWorldFigures(UiWords en)
    {
        UiSamples.FlareWorld known = UiSamples.FlareWorldOf("home");
        string bad = "none";
        FlareFaceModel studio = UiSamples.FlareSample(en, "studio");
        if (studio.Studio.Figures[2] != FlareFigures.N(known.Visitors) || studio.Studio.Figures[3] != "$" + FlareFigures.N(known.Dollars))
        {
            bad = "the studio says " + studio.Studio.Figures[2] + " visitors and " + studio.Studio.Figures[3] + ", the businesses together had " + known.Visitors + " and " + known.Dollars;
        }
        if (!string.IsNullOrEmpty(studio.HeadLine))
        {
            bad = "the studio has a head line (" + studio.HeadLine + ") over a tile that says the same";
        }
        for (int shop = 0; shop < known.Shops.Length && bad == "none"; shop++)
        {
            FlareFaceModel business = UiSamples.FlareSample(en, shop == 0 ? "business" : "business_turning");
            UiSamples.FlareShopWorld s = known.Shops[shop];
            float[] reach = business.Business.Reach;
            double lastWeek = 0;
            for (int i = Math.Max(0, reach.Length - 7); i < reach.Length; i++)
            {
                lastWeek += reach[i];
            }
            if (business.Business.Figures[0] != FlareFigures.N(s.Visitors) || business.Business.Rating != FlareFigures.Stars(s.StarsMilli) || business.Business.Bars.Length != 5
                || business.Business.RatingLine != FlareFigures.ReviewsCount(s.Reviews, en))
            {
                bad = "the business page of " + s.Name + " does not say its world's " + s.Visitors + " visitors and " + FlareFigures.Stars(s.StarsMilli) + " stars on " + s.Reviews + " reviews";
            }
            else if (Math.Abs(lastWeek - known.WeekReach) > 0.5)
            {
                bad = "the reach line's last seven days add to " + lastWeek + " and the week's reach is " + known.WeekReach;
            }
            else if (s.TurningNow > 0 && Math.Abs((s.TurningNow * 27L + s.TurningBefore * 14L) / 41f - s.StarsMilli) > 100)
            {
                bad = "the stars lately and before do not make the " + FlareFigures.Stars(s.StarsMilli) + " the reviews average";
            }
        }
        FlareFaceModel city = UiSamples.FlareSample(en, "city");
        known.CityWeek(out int seen, out int visits, out int dollars);
        if (bad == "none" && (city.City.WeekFigures[0] != FlareFigures.Count(seen) || city.City.WeekFigures[1] != FlareFigures.N(visits) || visits != known.Visitors))
        {
            bad = "the city page's week says " + string.Join(" ", city.City.WeekFigures) + ", the world saw " + seen + " and brought " + visits + " visits against the studio's " + known.Visitors;
        }
        // a trend holds no more posts than the district it trends in, the districts the city posted in come largest first
        FlareFaceModel explore = UiSamples.FlareSample(en, "explore");
        var byDistrict = new Dictionary<string, long>();
        for (int i = 0; i < known.PostingDistricts.Length; i++)
        {
            byDistrict[known.PostingDistricts[i]] = known.PostingCounts[i];
        }
        var tagged = new Dictionary<string, long>();
        for (int i = 0; i < explore.List.Count && bad == "none"; i++)
        {
            ref FlareRow r = ref explore.List.Rows[i];
            if (r.Kind == FlareRowKind.Tag && r.Handle != null && r.Handle.StartsWith("Trending in ", StringComparison.Ordinal))
            {
                string district = r.Handle.Substring("Trending in ".Length);
                tagged.TryGetValue(district, out long sum);
                tagged[district] = sum + ParseFigure(r.Value.Split(' ')[0]);
            }
        }
        foreach (KeyValuePair<string, long> pair in tagged)
        {
            if (!byDistrict.TryGetValue(pair.Key, out long posted) || pair.Value > posted + 100)
            {
                bad = "the tags of " + pair.Key + " hold " + pair.Value + " posts and the district posted " + (byDistrict.ContainsKey(pair.Key) ? byDistrict[pair.Key].ToString() : "none listed");
            }
        }
        Check(bad == "none", "flare world: the studio's tiles are the businesses' week together, each business page keeps to its own, the city's week is the same visits, and a trend holds no more posts than its district (" + bad + ")");
        // the shared functions: a new account has no week, a count never rounds up, a view is never fewer than the residents who saw
        FlareFigures.Week(new FlareWeekFacts(), en, false, out string[] labels, out _, out _, out _);
        Check(labels.Length == 0 && FlareFigures.NewAccount(new FlareWeekFacts()) && !FlareFigures.NewAccount(new FlareWeekFacts { Followers = 1 })
            && FlareFigures.Count(11950) == "11.9K" && FlareFigures.Count(2418) == "2.4K" && FlareFigures.Views(3100, 9400) == "9.4K" && FlareFigures.Views(0, 0) == null && FlareFigures.Replies(2, 82) == 84 && FlareFigures.ReplyTotal(3, 11) == 14 && FlareFigures.ReplyMore(14, 2) == 12 && FlareFigures.ReplyMore(2, 5) == 0 && FlareFigures.ReplyMore(-1, 0) == 0
            && FlareFigures.Change(0, 0) == 0 && FlareFigures.Change(5, 0) == 100 && FlareFigures.Signed(164) == "+164" && FlareFigures.Signed(-3) == "-3",
            "flare world: FlareFigures gives a new account no week, counts as a feed does, never shows fewer views than the residents who saw, and adds a post's replies");
        Check(FlareFigures.UnreadHead(0, en) == "All read" && FlareFigures.UnreadHead(3, en) == "3 unread" && FlareFigures.NewHead(0, en) == "All caught up" && FlareFigures.NewHead(5, en) == "5 new"
            && FlareFigures.FunnelSaid(9400, 46, false, en) == "Seen by 9.4K and 46 came" && FlareFigures.FunnelSaid(700, -12, true, en) == "Seen by 700 and it put 12 off",
            "flare world: the heads that count and the words of what a post did are FlareFigures' own");
    }

    // The Live fill is the same: the side's week and the studio's tiles are FlareFigures' over one set of facts, an account with nothing has no week, the thread's card counts its list,
    // a post's views are never fewer than the residents who saw it, the offer is said once, the studio and the thread have no head line, the accounts a page names are kept out of the
    // side, the city says nobody follows only to an account with no followers, and Explore says where the city posted.
    private static void FlareWorldLive()
    {
        string fill = PlayFile("Ui", "Flare", "FlareScreen.Fill.cs"), desk = PlayFile("Ui", "Flare", "FlareScreen.Desk.cs"), rows = PlayFile("Ui", "Flare", "FlareScreen.Rows.cs");
        Check(fill.Contains("FlareFigures.Week(facts, Words, own,") && fill.Contains("own || FlareFigures.NewAccount(facts) ? null") && fill.Contains("private FlareWeekFacts WeekFacts(FlareFile file, long now)")
            && desk.Contains("FlareFigures.StudioTiles(WeekFacts(file, now)"),
            "flare world: the Live side's week and the studio's tiles are FlareFigures' words over the same WeekFacts, and a new account has no week or line");
        Check(fill.Contains("_m.List.Rows[postRow].Comments = replies > 0 ? FlareFeedRules.Count(replies) : null;") && fill.Contains("FlareFigures.ReplyMore(RepliesOf(p, now), listed)")
            && rows.Contains("int replies = RepliesOf(p, now);") && rows.Contains("FlareFigures.ReplyTotal(posted,") && !fill.Contains("more = Comments(p, now)")
            && fill.Contains("_m.HeadLine = replies > 0 ? null : FlareText.T(\"reallife_flare_no_replies\");") && rows.Contains("FlareFigures.Views(FlareLiveRules.Shown(p.Reach, p, now), did != null ? did.Seen : 0)"),
            "flare world: Live's thread counts its card from its list and has no head line over a post that has replies, and a player's post shows at least the views the city saw");
        Check(fill.Contains("_seenOfPost") && fill.Contains("FlareFeedRules.Reach(file.Posts, (int)(now / 1440), out long week, out long before, _seenOfPost)")
            && fill.Contains("WhoToFollow(file, _whoKeys, WhoMost + _bodyAccounts.Count)") && desk.Contains("FlareFigures.DonutTotal(total, file.Followers, Words)")
            && desk.Contains("FlareFigures.Audience(_bandNames, _bandCounts, total, FlareText.District, out d.AudienceLabels") && PlayFile("Ui", "Flare", "FlareScreen.Sheet.cs").Contains("FlareFigures.Audience(_bandNames, _bandCounts, total, FlareText.District, out k.BarLabels")
            && desk.Contains("reallife_flare_kit_city_idle_body") && !desk.Contains("reallife_flare_kit_city_empty"),
            "flare world: Live's week reach is never below the views its posts show, the rail keeps its eight whatever the page names, the donut says its total, the hire card and sheet read one audience function, and the city's empty words are the new ones");
        Check(fill.Contains("o == null || o.Open(_flare.CurrentDay) ? KindOf(_with)") && !fill.Contains("reallife_flare_kit_offer_line") && desk.Contains("_m.HeadLine = null;")
            && !desk.Contains("reallife_flare_kit_studio_line"),
            "flare world: Live's open offer is the bubble and the buttons, not the head, and the studio has no head line over its tiles");
        Check(rows.Contains("_bodyAccounts.Add(key);") && fill.Contains("_bodyAccounts.Add(_storyPicks[i]);") && desk.Contains("_bodyAccounts.Add(\"k:\" + p.Name);"),
            "flare world: Live keeps out of the side every account the page names (a post's author, a story, a rival's card)");
        Check(desk.Contains("else if (!fresh)") && desk.Contains("bool fresh = FlareFigures.NewAccount(WeekFacts(file, now));") && fill.Contains("BusyDistricts(file, now);") && fill.Contains("reallife_flare_kit_section_busy"),
            "flare world: Live's city says nobody follows only to an account with nothing to count, and Explore lists the districts the city posted in under its tags");
    }

    private static long ParseFigure(string words)
    {
        string s = words.Trim().TrimStart('+', '-', '$').Replace(",", string.Empty);
        double scale = 1;
        if (s.EndsWith("K", StringComparison.Ordinal))
        {
            scale = 1000;
            s = s.Substring(0, s.Length - 1);
        }
        else if (s.EndsWith("M", StringComparison.Ordinal))
        {
            scale = 1000000;
            s = s.Substring(0, s.Length - 1);
        }
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? (long)Math.Round(v * scale) : 0;
    }
}
