using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What a role does for a game: the kind of work its points are, as the studio's day sorts them (<see cref="StudioFeeds.Of(RoleDef)"/>).</summary>
    public enum Feed
    {
        None = 0, Engineering = 1, Design = 2, Product = 3, Testing = 4, Support = 5, Count = 6
    }

    /// <summary>
    /// Which roles feed which parts of a game, said once. The studio's crew (<see cref="StudioRules.CrewOf"/>) and the people it counts
    /// (TechDirector.StudioHands) sort a role by <see cref="Of(int, bool, bool)"/>, and the splits that carry each kind of work into the five parts
    /// are the catalogue's, so the hints the production card and the Team and hiring pages give (a player could not tell which roles
    /// polish or design a game: a studio showed "design 0, art 0, polish 0") are read from the same two things and cannot drift from the rule.
    /// Game-free; nothing a frame, a few comparisons when a page is written.
    /// </summary>
    public static class StudioFeeds
    {
        /// <summary>The kind of work a role does for a game. Technicians keep racks running and build nothing; support people answer players' tickets.</summary>
        public static Feed Of(RoleDef role)
        {
            return role == null ? Feed.None : Of((int)role.Family, role.Id == TalentCatalogue.QaTester, role.Id == TalentCatalogue.Technician);
        }

        /// <summary>The same from the fields a studio's hand holds: the engineering families give engineers, or testers for the QA role.</summary>
        public static Feed Of(int family, bool tester, bool technician = false)
        {
            if (technician)
            {
                return Feed.None;
            }
            if (family <= (int)Family.Infrastructure)
            {
                return tester ? Feed.Testing : Feed.Engineering;
            }
            if (family == (int)Family.Design)
            {
                return Feed.Design;
            }
            if (family == (int)Family.Product)
            {
                return Feed.Product;
            }
            return family == (int)Family.Support ? Feed.Support : Feed.None;
        }

        /// <summary>True for the kinds of work that put points into a game's parts (support answers tickets and builds nothing).</summary>
        public static bool Builds(Feed feed) => feed == Feed.Engineering || feed == Feed.Design || feed == Feed.Product || feed == Feed.Testing;

        /// <summary>The thousandths of a kind's points that go into a part, by the catalogue's splits. Testers' go to polish in the polish phase only: 1000 is their share of it.</summary>
        public static int Share(Feed feed, int part)
        {
            int p = Math.Max(0, Math.Min((int)Part.Count - 1, part));
            switch (feed)
            {
                case Feed.Engineering: return StudioCatalogue.EngineeringSplit[p];
                case Feed.Design: return StudioCatalogue.DesignSplit[p];
                case Feed.Product: return StudioCatalogue.ProductSplit[p];
                case Feed.Testing: return p == (int)Part.Polish ? 1000 : 0;
                default: return 0;
            }
        }

        /// <summary>True if the kind's work goes into the part at all.</summary>
        public static bool Feeds(Feed feed, int part) => Share(feed, part) > 0;

        /// <summary>The kinds of work that put points into a part, in the order the card names them.</summary>
        public static int Makers(int part, Feed[] into)
        {
            int n = 0;
            for (int f = (int)Feed.Engineering; f <= (int)Feed.Testing && n < into.Length; f++)
            {
                if (Feeds((Feed)f, part))
                {
                    into[n++] = (Feed)f;
                }
            }
            return n;
        }

        /// <summary>The text of what a kind of work does for a game: the Team and hiring pages' line for a role.</summary>
        public static string RoleKey(Feed feed)
        {
            switch (feed)
            {
                case Feed.Engineering: return "reallife_studio_role_engineering";
                case Feed.Design: return "reallife_studio_role_design";
                case Feed.Product: return "reallife_studio_role_product";
                case Feed.Testing: return "reallife_studio_role_testing";
                case Feed.Support: return "reallife_studio_role_support";
                default: return "reallife_studio_role_none";
            }
        }

        /// <summary>The text that names the people of a kind of work on the production card.</summary>
        public static string GroupKey(Feed feed)
        {
            switch (feed)
            {
                case Feed.Engineering: return "reallife_studio_group_engineering";
                case Feed.Design: return "reallife_studio_group_design";
                case Feed.Product: return "reallife_studio_group_product";
                case Feed.Testing: return "reallife_studio_group_testing";
                default: return "reallife_studio_group_none";
            }
        }

        /// <summary>The text of who builds a part, written for the card (the whole of it, so the part's makers are named in one sentence).</summary>
        public static string MakersKey(int part)
        {
            switch (part)
            {
                case (int)Part.Design: return "reallife_studio_makers_design";
                case (int)Part.Tech: return "reallife_studio_makers_tech";
                case (int)Part.Art: return "reallife_studio_makers_art";
                case (int)Part.Content: return "reallife_studio_makers_content";
                default: return "reallife_studio_makers_polish";
            }
        }

        /// <summary>
        /// The texts of what a studio's team lacks for the game it is building, in the order the card says them. <paramref name="people"/> is how many of each kind of work the studio has
        /// (by <see cref="Feed"/>), <paramref name="phase"/> the game's <see cref="GamePhase"/>. Each line is what the rules would do: no engineers leaves tech at nothing, no designers
        /// nor product people leaves design and art at nothing, no designers alone leaves art, no testers leaves the found bugs to the engineers and polish to half of everyone's work
        /// in the polish phase; before that phase polish is built by nobody. Returns how many.
        /// </summary>
        public static int Gaps(int[] people, int phase, System.Collections.Generic.List<string> into)
        {
            into.Clear();
            int e = people[(int)Feed.Engineering], d = people[(int)Feed.Design], p = people[(int)Feed.Product], t = people[(int)Feed.Testing];
            if (e <= 0)
            {
                into.Add("reallife_studio_gap_engineering");
            }
            if (d <= 0 && p <= 0)
            {
                into.Add("reallife_studio_gap_design");
            }
            else if (d <= 0)
            {
                into.Add("reallife_studio_gap_art");
            }
            if (t <= 0)
            {
                into.Add("reallife_studio_gap_testing");
            }
            if (phase < (int)GamePhase.Polish)
            {
                into.Add("reallife_studio_gap_polish_wait");
            }
            return into.Count;
        }

        /// <summary>
        /// The parts of a game a kind of work builds, with each part's share, as "Tech 40%, Content 60%": the placeholder of a role's line. The names are the texts of the
        /// parts (<paramref name="name"/> gives a part's), so the figures are the catalogue's and the words the player's own.
        /// </summary>
        public static string PartsLine(Feed feed, Func<int, string> name, Func<int, string> share)
        {
            string line = string.Empty;
            for (int p = 0; p < (int)Part.Count; p++)
            {
                int s = Share(feed, p);
                if (s > 0 && feed != Feed.Testing)
                {
                    line += (line.Length > 0 ? ", " : string.Empty) + name(p) + " " + share(s);
                }
            }
            return line;
        }
    }
}
