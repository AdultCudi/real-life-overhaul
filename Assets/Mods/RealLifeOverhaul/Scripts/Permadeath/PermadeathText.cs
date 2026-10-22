using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Extensions;
using Localizor;

namespace RealLifeOverhaul.Permadeath
{
    /// <summary>
    /// Permadeath's words, written out when shown, in the language of that moment. The obituary's sentences come from
    /// <see cref="PermadeathStory"/>; here every {name} in them is filled from the story.
    /// </summary>
    public static class PermadeathText
    {
        private static readonly List<ObitLine> Lines = new List<ObitLine>(64);
        private static readonly List<string> Names = new List<string>(LifeStory.MournersMost);
        private static readonly StringBuilder Body = new StringBuilder(1024);

        public static string T(string key)
        {
            return string.IsNullOrEmpty(key) ? string.Empty : key.GetLocalization();
        }

        /// <summary>The page for a story, every word written out now. <paramref name="test"/> is the options page's rehearsal.</summary>
        public static ObituaryPage Page(LifeStory s, bool test)
        {
            var page = new ObituaryPage
            {
                Day = s.Day,
                Header = T(test ? "reallife_pd_obit_header_test" : "reallife_pd_obit_header"),
                EarLeftLabel = T("reallife_pd_obit_ear_left"),
                EarLeft = s.Name,
                EarRightLabel = T("reallife_pd_obit_ear_right"),
                EarRight = Number(s.Age),
                DateMiddle = T("reallife_pd_obit_dateline"),
                DateRight = T("reallife_pd_obit_dateline_right"),
                Byline = T("reallife_pd_obit_byline")
            };
            PermadeathStory.Draft(s, test, Lines);
            Body.Length = 0;
            List<string> section = null, box = null;
            for (int i = 0; i < Lines.Count; i++)
            {
                ObitLine line = Lines[i];
                switch (line.Part)
                {
                    case ObitPart.Kicker: page.Kicker = Fill(line.Key, s); break;
                    case ObitPart.Head: page.Headline = Fill(line.Key, s); break;
                    case ObitPart.Stand: page.Stand = Fill(line.Key, s); break;
                    case ObitPart.Body:
                        Body.Append(Body.Length > 0 ? " " : string.Empty).Append(Fill(line.Key, s));
                        break;
                    case ObitPart.Label:
                        section = new List<string>(6);
                        page.Labels.Add(T(line.Key));
                        page.Texts.Add(section);
                        break;
                    case ObitPart.Text:
                        section?.Add(Fill(line.Key, s));
                        break;
                    case ObitPart.BoxLabel:
                        box = new List<string>(12);
                        page.BoxLabels.Add(T(line.Key));
                        page.BoxLines.Add(box);
                        break;
                    case ObitPart.Figure:
                        box?.Add(T(line.Key) + ": " + Figure(line.Fact, s));
                        break;
                    case ObitPart.Mourned:
                        box?.Add(Fill(line.Key, s));
                        break;
                    case ObitPart.NoticeHead: page.NoticeHead = Fill(line.Key, s); break;
                    case ObitPart.Notice: page.Notice = Fill(line.Key, s); break;
                }
            }
            page.Body = Body.ToString();
            return page;
        }

        /// <summary>A sentence of the obituary with every name of the story filled in.</summary>
        public static string Fill(string key, LifeStory s)
        {
            string text = T(key);
            if (text.IndexOf('{') < 0)
            {
                return text;
            }
            s.Mourners(Names);
            return text
                .Replace("{name}", s.Name)
                .Replace("{age}", Number(s.Age))
                .Replace("{years}", Span(s))
                .Replace("{place}", s.Place.Length > 0 ? s.Place : T("reallife_pd_obit_place_unknown"))
                .Replace("{worth}", Money(s.Worth < 0 ? -s.Worth : s.Worth))
                .Replace("{founded}", Number(PermadeathStory.Founded(s)))
                .Replace("{owned}", Number(s.Owned))
                .Replace("{businesses}", Join(s.Businesses))
                .Replace("{employees}", Number(s.Employees))
                .Replace("{properties}", Number(s.Properties))
                .Replace("{friends}", Count(s.Friends, "reallife_pd_n_friend_one", "reallife_pd_n_friend_many"))
                .Replace("{close}", Join(s.Close))
                .Replace("{partner}", s.Partner)
                .Replace("{children}", Join(s.Children))
                .Replace("{count}", Number(s.Children.Count))
                .Replace("{family}", Join(s.Family))
                .Replace("{arrests}", Count(s.Arrests, "reallife_pd_n_time_one", "reallife_pd_n_time_many"))
                .Replace("{convictions}", Count(s.Convictions, "reallife_pd_n_time_one", "reallife_pd_n_time_many"))
                .Replace("{followers}", Number(s.Followers))
                .Replace("{elections}", Number(s.Elections))
                .Replace("{degree}", s.Degree)
                .Replace("{career}", s.Career)
                .Replace("{mourners}", Join(Names));
        }

        // A figure of the box down the side.
        private static string Figure(ObitFact fact, LifeStory s)
        {
            switch (fact)
            {
                case ObitFact.Age: return Number(s.Age);
                case ObitFact.Span: return Span(s);
                case ObitFact.Worth: return Money(s.Worth);
                case ObitFact.Founded: return Number(PermadeathStory.Founded(s));
                case ObitFact.Owned: return Number(s.Owned);
                case ObitFact.Employees: return Number(s.Employees);
                case ObitFact.Properties: return Number(s.Properties);
                case ObitFact.Friends: return Number(s.Friends);
                case ObitFact.Followers: return Number(s.Followers);
                case ObitFact.Elections: return Number(s.Elections);
                case ObitFact.Arrests: return Number(s.Arrests);
                case ObitFact.Convictions: return Number(s.Convictions);
                default: return string.Empty;
            }
        }

        /// <summary>The time lived in the city: "3 years and 12 days", "one year", "40 days".</summary>
        public static string Span(LifeStory s)
        {
            int years = s.Years, days = s.DaysOver;
            string y = Count(years, "reallife_pd_n_year_one", "reallife_pd_n_year_many");
            string d = Count(days, "reallife_pd_n_day_one", "reallife_pd_n_day_many");
            if (years <= 0)
            {
                return Count(s.Days, "reallife_pd_n_day_one", "reallife_pd_n_day_many");
            }
            return days <= 0 ? y : y + " " + T("reallife_pd_and") + " " + d;
        }

        private static string Count(int count, string oneKey, string manyKey)
        {
            return count == 1 ? T(oneKey) : T(manyKey).Replace("{count}", Number(count));
        }

        /// <summary>Names as a sentence lists them: "A", "A and B", "A, B and C".</summary>
        public static string Join(List<string> names)
        {
            if (names == null || names.Count == 0)
            {
                return string.Empty;
            }
            if (names.Count == 1)
            {
                return names[0];
            }
            var t = new StringBuilder(64);
            for (int i = 0; i < names.Count - 1; i++)
            {
                if (i > 0)
                {
                    t.Append(", ");
                }
                t.Append(names[i]);
            }
            return t.Append(' ').Append(T("reallife_pd_and")).Append(' ').Append(names[names.Count - 1]).ToString();
        }

        public static string Number(long value)
        {
            return value.ToString("N0", CultureInfo.CurrentCulture);
        }

        /// <summary>A sum of money in whole units, as the game writes one.</summary>
        public static string Money(long amount)
        {
            return ((double)amount).ToShortCurrencyFormat();
        }
    }
}
