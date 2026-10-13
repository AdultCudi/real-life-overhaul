using System.Collections.Generic;

namespace RealLifeOverhaul.Permadeath
{
    /// <summary>Where a line of the obituary goes on the City Ledger's page.</summary>
    public enum ObitPart
    {
        Kicker,      // the small capitals over the headline
        Head,        // the headline
        Stand,       // the standfirst: how they died
        Body,        // the lead's text, under the drop cap
        Label,       // a section's label in the columns
        Text,        // a paragraph of that section
        BoxLabel,    // a label in the box down the side
        Figure,      // a line of the box: a figure's name and its value
        Mourned,     // the names of those who mourn them, under their label in the box
        NoticeHead,  // the framed notice at the foot of the page
        Notice
    }

    /// <summary>The figures of the box down the side, each with its own way of being written (a count, a sum of money, a span of time).</summary>
    public enum ObitFact { None, Age, Span, Worth, Founded, Owned, Employees, Properties, Friends, Followers, Elections, Arrests, Convictions }

    /// <summary>One line of the obituary: where it goes, its locale key, and for a figure which fact is its value.</summary>
    public struct ObitLine
    {
        public ObitPart Part;
        public string Key;
        public ObitFact Fact;

        public ObitLine(ObitPart part, string key, ObitFact fact = ObitFact.None)
        {
            Part = part;
            Key = key;
            Fact = fact;
        }
    }

    /// <summary>
    /// What the obituary says, as locale keys in the order of the page, chosen from the life's story: a sentence is in
    /// it only when the record has something for it to say (no word of a business for somebody who never ran one, of a
    /// record for somebody who never had one), and a count of one has its own sentence. The words themselves, and every
    /// {name} in them, are filled in by Permadeath/PermadeathText from the same story. Game-free: the checks run it.
    /// </summary>
    public static class PermadeathStory
    {
        private const string P = "reallife" + "_pd_obit_";   // (apart, so that the locale check does not take a prefix for a key)

        /// <summary>The page for a story; <paramref name="test"/> is the options page's rehearsal, whose notice says nobody died.</summary>
        public static void Draft(LifeStory s, bool test, List<ObitLine> into)
        {
            into.Clear();
            DeathCause cause = test ? DeathCause.Test : s.Cause;
            into.Add(new ObitLine(ObitPart.Kicker, P + "kicker"));
            into.Add(new ObitLine(ObitPart.Head, P + "head"));
            into.Add(new ObitLine(ObitPart.Stand, P + "stand_" + Word(cause)));

            // ---- the lead: how long they lived here, what happened, the paper's sympathy
            into.Add(new ObitLine(ObitPart.Body, P + "lived"));
            if (cause != DeathCause.Test && cause != DeathCause.None)
            {
                into.Add(new ObitLine(ObitPart.Body, P + "how_" + Word(cause)));
            }
            into.Add(new ObitLine(ObitPart.Body, P + "close"));

            // ---- a life in business
            into.Add(new ObitLine(ObitPart.Label, P + "sec_work"));
            if (s.Career.Length > 0)
            {
                into.Add(new ObitLine(ObitPart.Text, P + "career"));
            }
            if (s.Degree.Length > 0)
            {
                into.Add(new ObitLine(ObitPart.Text, P + "degree"));
            }
            if (s.Founded <= 0 && s.Owned <= 0)
            {
                into.Add(new ObitLine(ObitPart.Text, P + "no_business"));
            }
            else
            {
                into.Add(new ObitLine(ObitPart.Text, P + (Founded(s) == 1 ? "founded_one" : "founded_many")));
                if (s.Owned > 0)
                {
                    into.Add(new ObitLine(ObitPart.Text, P + (s.Owned == 1 ? "owned_one" : "owned_many")));
                }
            }
            if (s.Employees > 0)
            {
                into.Add(new ObitLine(ObitPart.Text, P + (s.Employees == 1 ? "staff_one" : "staff_many")));
            }
            if (s.Properties > 0)
            {
                into.Add(new ObitLine(ObitPart.Text, P + (s.Properties == 1 ? "property_one" : "property_many")));
            }
            if (s.Worth > 0)
            {
                into.Add(new ObitLine(ObitPart.Text, P + "worth"));
            }
            else if (s.Worth < 0)
            {
                into.Add(new ObitLine(ObitPart.Text, P + "debt"));
            }

            // ---- family and friends
            into.Add(new ObitLine(ObitPart.Label, P + "sec_people"));
            if (s.Partner.Length > 0 && s.Bond > 0)
            {
                into.Add(new ObitLine(ObitPart.Text, P + (s.Bond >= 3 ? "married" : s.Bond == 2 ? "engaged" : "together")));
            }
            if (s.Children.Count > 0)
            {
                into.Add(new ObitLine(ObitPart.Text, P + (s.Children.Count == 1 ? "child_one" : "child_many")));
            }
            if (s.Family.Count > 0)
            {
                into.Add(new ObitLine(ObitPart.Text, P + "family"));
            }
            if (s.Friends <= 0 && s.Close.Count == 0)
            {
                into.Add(new ObitLine(ObitPart.Text, P + "friends_none"));
            }
            else
            {
                into.Add(new ObitLine(ObitPart.Text, P + (s.Close.Count > 0 ? "friends_close" : "friends_count")));
            }

            // ---- in the city
            into.Add(new ObitLine(ObitPart.Label, P + "sec_city"));
            if (s.Arrests > 0 && s.Convictions > 0)
            {
                into.Add(new ObitLine(ObitPart.Text, P + "record"));
            }
            else if (s.Arrests > 0)
            {
                into.Add(new ObitLine(ObitPart.Text, P + "arrested"));
            }
            else if (s.Convictions > 0)
            {
                into.Add(new ObitLine(ObitPart.Text, P + "convicted"));
            }
            else
            {
                into.Add(new ObitLine(ObitPart.Text, P + "clean"));
            }
            if (s.Followers > 0)
            {
                into.Add(new ObitLine(ObitPart.Text, P + (s.Followers == 1 ? "followers_one" : "followers_many")));
            }
            if (s.Elections > 0)
            {
                into.Add(new ObitLine(ObitPart.Text, P + (s.Elections == 1 ? "voted_one" : "voted_many")));
            }

            // ---- the box: a life in figures, and who mourns them
            into.Add(new ObitLine(ObitPart.BoxLabel, P + "box"));
            into.Add(new ObitLine(ObitPart.Figure, "reallife_pd_fig_age", ObitFact.Age));
            into.Add(new ObitLine(ObitPart.Figure, "reallife_pd_fig_years", ObitFact.Span));
            into.Add(new ObitLine(ObitPart.Figure, "reallife_pd_fig_worth", ObitFact.Worth));
            into.Add(new ObitLine(ObitPart.Figure, "reallife_pd_fig_founded", ObitFact.Founded));
            into.Add(new ObitLine(ObitPart.Figure, "reallife_pd_fig_owned", ObitFact.Owned));
            into.Add(new ObitLine(ObitPart.Figure, "reallife_pd_fig_staff", ObitFact.Employees));
            into.Add(new ObitLine(ObitPart.Figure, "reallife_pd_fig_property", ObitFact.Properties));
            into.Add(new ObitLine(ObitPart.Figure, "reallife_pd_fig_friends", ObitFact.Friends));
            into.Add(new ObitLine(ObitPart.Figure, "reallife_pd_fig_followers", ObitFact.Followers));
            into.Add(new ObitLine(ObitPart.Figure, "reallife_pd_fig_voted", ObitFact.Elections));
            into.Add(new ObitLine(ObitPart.Figure, "reallife_pd_fig_arrests", ObitFact.Arrests));
            into.Add(new ObitLine(ObitPart.Figure, "reallife_pd_fig_convictions", ObitFact.Convictions));
            into.Add(new ObitLine(ObitPart.BoxLabel, P + "mourned"));
            into.Add(new ObitLine(ObitPart.Mourned, s.Partner.Length > 0 || s.Children.Count > 0 || s.Family.Count > 0 || s.Close.Count > 0 ? P + "mourners" : P + "mourned_none"));

            // ---- the notice at the foot: what has become of the save
            into.Add(new ObitLine(ObitPart.NoticeHead, P + (test ? "notice_head_test" : "notice_head")));
            into.Add(new ObitLine(ObitPart.Notice, P + (test ? "notice_test" : "notice")));
        }

        /// <summary>The businesses founded as the page counts them: never fewer than are running at the end.</summary>
        public static int Founded(LifeStory s)
        {
            return s.Founded > s.Owned ? s.Founded : s.Owned;
        }

        /// <summary>The word a cause's keys end in. None is a death with no cause of its own (the options page's real test, a record that could not be read).</summary>
        public static string Word(DeathCause cause)
        {
            switch (cause)
            {
                case DeathCause.Crash: return "crash";
                case DeathCause.Struck: return "struck";
                case DeathCause.Illness: return "illness";
                case DeathCause.Mugging: return "mugging";
                case DeathCause.Assault: return "assault";
                case DeathCause.Test: return "test";
                default: return "none";
            }
        }
    }
}
