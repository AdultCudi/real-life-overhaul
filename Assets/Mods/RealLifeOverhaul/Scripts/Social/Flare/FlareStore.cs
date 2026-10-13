using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare in a save: two entries of the save's data, short lines each.
    ///
    ///   reallife:flare_account
    ///     FL1|seed|start|settled|followers|reputation|last post day|last post minute|posts day|posts today|last viral|
    ///         posts|likes|next id|unseen|following|civic seen|career|history,history,...|notice day
    ///     D|id|source|day|hour|person|place|name|arg          a draft
    ///     S|place|first day|best|days                         a business as Flare has seen it
    ///   reallife:flare_posts
    ///     P|id|day|hour|author|who|source|tone|tag|place|name|arg|seed|quality|reach|likes|replies|likers|repliers|gained|flags
    ///
    /// Every number is written in the invariant culture. What is not understood is left out a line at a time: a post or
    /// a draft of a kind this version does not know (from a later one) is dropped, never read as something else, so an
    /// entry from a later version or a damaged one never stops a load. An account line that is not this version's is
    /// not read at all (a new account). Game-free.
    /// </summary>
    public static class FlareStore
    {
        public const string AccountKey = "reallife:flare_account";
        public const string PostsKey = "reallife:flare_posts";
        public const string Keys = "reallife:flare_*";
        private const string Version = "FL1";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // The kinds this version knows: anything past them is from a later one.
        private const int Sources = (int)FlareSource.Life + 1;
        private const int Tones = (int)FlareTone.Cheeky + 1;
        private const int Authors = (int)FlareAuthor.Friend + 1;

        public static void Save(Dictionary<string, string> data, FlareFile f)
        {
            if (data == null || f == null)
            {
                return;
            }
            data[AccountKey] = WriteAccount(f);
            data[PostsKey] = WritePosts(f);
        }

        /// <summary>The save's file, or null if it has none this version can read (a new account is opened then).</summary>
        public static FlareFile Load(Dictionary<string, string> data)
        {
            string account, posts;
            if (data == null || !data.TryGetValue(AccountKey, out account))
            {
                return null;
            }
            data.TryGetValue(PostsKey, out posts);
            return Read(account, posts);
        }

        public static string WriteAccount(FlareFile f)
        {
            var b = new StringBuilder(512 + f.Drafts.Count * 60 + f.Shops.Count * 40);
            b.Append(Version);
            Join(b, f.Seed, f.StartDay, f.SettledDay, f.Followers, f.Reputation, f.LastPostDay, f.LastPostMinute, f.PostsDay, f.PostsToday, f.LastViralDay,
                f.TotalPosts, f.TotalLikes, f.NextId, f.Unseen, f.Following, f.CivicSeen, f.CareerKey);
            b.Append('|');
            for (int i = 0; i < f.History.Length; i++)
            {
                b.Append(i > 0 ? "," : string.Empty).Append(f.History[i].ToString(Inv));
            }
            Join(b, f.NoticeDay);
            for (int i = 0; i < f.Drafts.Count; i++)
            {
                FlareDraft d = f.Drafts[i];
                b.Append("\nD");
                Join(b, d.Id, (int)d.Source, d.Day, d.Hour, d.Person);
                b.Append('|').Append(Clean(d.Place)).Append('|').Append(Clean(d.Name)).Append('|').Append(d.Arg.ToString(Inv));
            }
            for (int i = 0; i < f.Shops.Count; i++)
            {
                FlareShopSeen s = f.Shops[i];
                b.Append("\nS|").Append(Clean(s.Place));
                Join(b, s.FirstDay, s.Best, s.Days);
            }
            return b.ToString();
        }

        public static string WritePosts(FlareFile f)
        {
            var b = new StringBuilder(f.Posts.Count * 90);
            for (int i = 0; i < f.Posts.Count; i++)
            {
                FlarePost p = f.Posts[i];
                b.Append(i > 0 ? "\nP" : "P");
                Join(b, p.Id, p.Day, p.Hour, (int)p.Author, p.Who, (int)p.Source, (int)p.Tone, p.Tag);
                b.Append('|').Append(Clean(p.Place)).Append('|').Append(Clean(p.Name));
                Join(b, p.Arg, p.Seed, p.Quality, p.Reach, p.Likes, p.Replies, p.Likers, p.Repliers, p.Gained, (p.Viral ? 1 : 0) | (p.Bad ? 2 : 0));
            }
            return b.ToString();
        }

        public static FlareFile Read(string account, string posts)
        {
            if (string.IsNullOrEmpty(account))
            {
                return null;
            }
            try
            {
                string[] lines = account.Split('\n');
                string[] h = lines[0].TrimEnd('\r').Split('|');
                if (h.Length < 19 || h[0] != Version)
                {
                    return null;
                }
                var f = new FlareFile
                {
                    Seed = I(h, 1), StartDay = I(h, 2), SettledDay = I(h, 3), Followers = Math.Max(0, I(h, 4)), Reputation = FlareRules.Clamp(I(h, 5, FlareRules.ReputationStart), 0, 100),
                    LastPostDay = I(h, 6, -1), LastPostMinute = I(h, 7, -1), PostsDay = I(h, 8, -1), PostsToday = Math.Max(0, I(h, 9)), LastViralDay = I(h, 10, -100),
                    TotalPosts = Math.Max(0, I(h, 11)), TotalLikes = Math.Max(0, I(h, 12)), NextId = Math.Max(1, I(h, 13, 1)), Unseen = Math.Max(0, I(h, 14)),
                    Following = I(h, 15), CivicSeen = I(h, 16), CareerKey = I(h, 17, -1), NoticeDay = I(h, 19, -1)
                };
                string[] history = h[18].Split(',');
                for (int i = 0; i < f.History.Length; i++)
                {
                    f.History[i] = i < history.Length ? Math.Max(0, I(history, i, f.Followers)) : f.Followers;
                }
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] c = lines[i].TrimEnd('\r').Split('|');
                    if (c[0] == "D" && c.Length >= 9 && f.Drafts.Count < FlareRules.DraftsMost)
                    {
                        int source = I(c, 2, -1);
                        if (source > 0 && source < Sources)
                        {
                            f.Drafts.Add(new FlareDraft
                            {
                                Id = I(c, 1), Source = (FlareSource)source, Day = I(c, 3), Hour = I(c, 4), Person = I(c, 5, -1), Place = Text(c[6]), Name = Text(c[7]), Arg = I(c, 8)
                            });
                        }
                    }
                    else if (c[0] == "S" && c.Length >= 5 && c[1].Length > 0 && f.Shops.Count < 64)
                    {
                        f.Shops.Add(new FlareShopSeen { Place = c[1], FirstDay = I(c, 2), Best = Math.Max(0, I(c, 3)), Days = Math.Max(0, I(c, 4)) });
                    }
                }
                ReadPosts(f, posts);
                return f;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void ReadPosts(FlareFile f, string posts)
        {
            if (string.IsNullOrEmpty(posts))
            {
                return;
            }
            string[] lines = posts.Split('\n');
            for (int i = 0; i < lines.Length && f.Posts.Count < FlareRules.PostsMost; i++)
            {
                string[] c = lines[i].TrimEnd('\r').Split('|');
                if (c[0] != "P" || c.Length < 21)
                {
                    continue;
                }
                int author = I(c, 4, -1), source = I(c, 6, -1), tone = I(c, 7, -1);
                if (author < 0 || author >= Authors || source <= 0 || source >= Sources || tone < 0 || tone >= Tones)
                {
                    continue;   // a kind of a later version's
                }
                int flags = I(c, 20);
                f.Posts.Add(new FlarePost
                {
                    Id = I(c, 1), Day = I(c, 2), Hour = I(c, 3), Author = (FlareAuthor)author, Who = I(c, 5, -1), Source = (FlareSource)source, Tone = (FlareTone)tone, Tag = I(c, 8, -1),
                    Place = Text(c[9]), Name = Text(c[10]), Arg = I(c, 11), Seed = I(c, 12), Quality = I(c, 13), Reach = Math.Max(0, I(c, 14)), Likes = Math.Max(0, I(c, 15)),
                    Replies = Math.Max(0, I(c, 16)), Likers = I(c, 17), Repliers = I(c, 18), Gained = I(c, 19), Viral = (flags & 1) != 0, Bad = (flags & 2) != 0
                });
            }
        }

        private static void Join(StringBuilder b, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                b.Append('|').Append(values[i].ToString(Inv));
            }
        }

        private static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', '/').Replace('\n', ' ').Replace('\r', ' ');
        }

        private static string Text(string field)
        {
            return string.IsNullOrEmpty(field) ? null : field;
        }

        private static int I(string[] c, int at, int otherwise = 0)
        {
            int value;
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : otherwise;
        }
    }
}
