using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Schools
{
    /// <summary>One piece of the City Ledger about the city's schools: its locale key, what fills it, and whether it leads the paper.</summary>
    public struct SchoolStory
    {
        public string Key;
        public Dictionary<string, string> Data;
        public bool Lead;   // the morning's front page: its first headline, which the City Ledger's account on Flare posts and the city's people answer
    }

    /// <summary>
    /// The director's part in words: the City Ledger's pieces of a morning from the market's news of that day, worked out from
    /// the state alone, so the same morning gives the same paper however often it is asked: the campus's opening, a sale, the
    /// league table on results day, an inspection, the pay war and an open day.
    /// </summary>
    public sealed partial class SchoolsDirector
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The morning's pieces about the schools (into the list, cleared): none while they rest.</summary>
        public static void Stories(int day, List<SchoolStory> into)
        {
            into.Clear();
            if (!Running)
            {
                return;
            }
            for (int i = 0; i < _state.News.Count; i++)
            {
                SchoolNews n = _state.News[i];
                if (n.Day == day)
                {
                    Story(n, into);
                }
            }
        }

        private static void Story(SchoolNews n, List<SchoolStory> into)
        {
            string campus = SchoolsGame.Name(SchoolRules.CampusId);
            switch (n.Kind)
            {
                case SchoolRules.NewsOpened:
                    into.Add(Piece(SchoolsText.PaperOpened, true, "school", campus, "places", n.Value.ToString(Inv), "day", n.Other.ToString(Inv)));
                    break;
                case SchoolRules.NewsSold:
                    into.Add(n.Other == SchoolOwner.Player
                        ? Piece(SchoolsText.PaperSoldPlayer, true, "school", SchoolsGame.Name(n.School), "price", SchoolsText.Money(SchoolsGame.Today(n.Value)))
                        : Piece(SchoolsText.PaperSold, n.School == SchoolRules.CampusId, "buyer", SchoolsText.Owner(n.Other), "school", SchoolsGame.Name(n.School),
                            "price", SchoolsText.Money(SchoolsGame.Today(n.Value))));
                    break;
                case SchoolRules.NewsResults:
                    into.Add(Piece(SchoolsText.PaperResults, true, "first", SchoolsGame.Name(n.School), "score", n.Value.ToString(Inv), "school", campus,
                        "rank", n.Other.ToString(Inv), "count", SchoolRules.Count.ToString(Inv), "own", _state.Campus.Results.ToString(Inv)));
                    break;
                case SchoolRules.NewsInspected:
                    into.Add(Piece(SchoolsText.PaperInspected, false, "school", SchoolsGame.Name(n.School), "grade", SchoolsText.Grade((int)n.Value)));
                    break;
                case SchoolRules.NewsPoached:
                    into.Add(Piece(SchoolsText.PaperPoached, false, "school", SchoolsGame.Name(n.School), "other", SchoolsGame.Name(n.Other), "count", n.Value.ToString(Inv)));
                    break;
                case SchoolRules.NewsOpenDay:
                    into.Add(Piece(SchoolsText.PaperOpenDay, false, "school", campus, "from", SchoolRules.OpenDayFrom.ToString("00", Inv) + ":00",
                        "to", SchoolRules.OpenDayTo.ToString("00", Inv) + ":00"));
                    break;
            }
        }

        private static SchoolStory Piece(string key, bool lead, params string[] pairs)
        {
            var data = new Dictionary<string, string>(pairs.Length / 2);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                data[pairs[i]] = pairs[i + 1] ?? string.Empty;
            }
            return new SchoolStory { Key = key, Data = data, Lead = lead };
        }
    }
}
