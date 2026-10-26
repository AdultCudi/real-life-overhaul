using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Landmarks
{
    /// <summary>One piece of the City Ledger about a landmark: its locale key, what fills its placeholders, and whether it leads the paper.</summary>
    public struct LandmarkStory
    {
        public string Key;
        public Dictionary<string, string> Data;
        public bool Lead;   // the morning's front page: its first headline, which the City Ledger's account on Flare posts and the city's people answer
    }

    /// <summary>
    /// The director's part in words: each landmark's name, where it stands for the construction game's Land tab, the
    /// stadium's fixtures and results, and the City Ledger's pieces of a morning, worked out from the state and the day
    /// alone, so the same morning gives the same paper however often it is asked.
    /// </summary>
    public sealed partial class LandmarksDirector
    {
        private static readonly CultureInfo Plain = CultureInfo.InvariantCulture;

        /// <summary>A landmark's name in the player's language.</summary>
        public static string NameOf(LandmarkKind kind) => LandmarksText.T(LandmarksText.NameKey(kind));

        /// <summary>A landmark's name by its site's id; null for any other id.</summary>
        public static string NameOf(string siteId)
        {
            int k = LandmarkRules.KindOf(siteId);
            return k < 0 ? null : NameOf((LandmarkKind)k);
        }

        // Who builds it, in words: the player's company, a rival by name, the city's own works.
        private static string BuilderWords(LandmarkFile f)
        {
            if (f.Builder == LandmarkBuilder.Player)
            {
                return LandmarksText.T(LandmarksText.BuilderPlayer);
            }
            if (f.Builder >= 0)
            {
                string name = LandmarkBridges.RivalName != null ? LandmarkBridges.RivalName(f.Builder) : null;
                return string.IsNullOrEmpty(name) ? LandmarksText.T(LandmarksText.BuilderRival) : name;
            }
            return LandmarksText.T(LandmarksText.BuilderCity);
        }

        /// <summary>Where a landmark stands, in words for the construction game's Land tab, by its site's id; null for any other id.</summary>
        public static string Words(string siteId)
        {
            if (!SiteOf(siteId, out _, out LandmarkFile f))
            {
                return null;
            }
            switch (f.Stage)
            {
                case LandmarkStage.Planned:
                    return LandmarksText.T(LandmarksText.WordsPlanned);
                case LandmarkStage.Announced:
                    return LandmarksText.T(LandmarksText.WordsAnnounced);
                case LandmarkStage.Tendered:
                    return LandmarksText.T(LandmarksText.WordsTendered);
                case LandmarkStage.Building:
                    return f.Due > 0
                        ? LandmarksText.Fill(LandmarksText.WordsBuildingDue, "builder", BuilderWords(f), "day", f.Due.ToString(Plain))
                        : LandmarksText.Fill(LandmarksText.WordsBuilding, "builder", BuilderWords(f));
                case LandmarkStage.Finished:
                    return LandmarksText.Fill(LandmarksText.WordsOpening, "day", f.Opening.ToString(Plain));
                default:
                    return LandmarksText.T(LandmarksText.WordsOpen);
            }
        }

        // The stadium's next home match, in words.
        private static string Fixture(LandmarkFile stadium, int day)
        {
            int next = LandmarkRules.NextMatch(day, stadium.Opening);
            string club = LandmarksText.T(LandmarksText.ClubKey(LandmarkRules.Opponent(next)));
            return next == day
                ? LandmarksText.Fill(LandmarksText.PanelMatchToday, "team", LandmarksText.T(LandmarksText.Club), "club", club, "hour", LandmarkRules.KickOff.ToString(Plain))
                : LandmarksText.Fill(LandmarksText.PanelMatchNext, "club", club, "day", next.ToString(Plain), "hour", LandmarkRules.KickOff.ToString(Plain));
        }

        // The stadium's last home result, in words; null before the first.
        private static string Result(LandmarkFile stadium, int day)
        {
            for (int d = day - 1; d > stadium.Opening && d >= day - 7; d--)
            {
                if (LandmarkRules.MatchDay(d, stadium.Opening))
                {
                    LandmarkRules.Score(d, out int home, out int away);
                    return LandmarksText.Fill(LandmarksText.PanelLast, "team", LandmarksText.T(LandmarksText.Club), "club", LandmarksText.T(LandmarksText.ClubKey(LandmarkRules.Opponent(d))),
                        "home", home.ToString(Plain), "away", away.ToString(Plain));
                }
            }
            return null;
        }

        /// <summary>
        /// The City Ledger's pieces about the landmarks for a day's paper: announced, given out or taken on by the city, a new
        /// phase of the works, finished and opening today, opened, and the stadium's match day and its result. The same for
        /// the same day however often asked. Into the caller's list (not cleared).
        /// </summary>
        public static void Stories(int day, List<LandmarkStory> into)
        {
            if (!_loaded || _unread || !Settings.Landmarks)
            {
                return;
            }
            for (int k = 0; k < _state.Files.Length; k++)
            {
                LandmarkFile f = _state.Files[k];
                string name = NameOf(f.Kind);
                if (f.Announced > 0 && f.Announced == day)
                {
                    Add(into, LandmarksText.PaperAnnounced(f.Kind), true, "name", name, "jobs", LandmarkRules.JobsOf(f.Kind).ToString(Plain));
                }
                if (f.Builder != LandmarkBuilder.Nobody && f.Started > 0 && f.Started == day)
                {
                    Add(into, f.Builder == LandmarkBuilder.City ? LandmarksText.PaperCity : LandmarksText.PaperAwarded, false, "name", name, "builder", BuilderWords(f));
                }
                if (f.Phase > 0 && f.PhaseDay == day && f.Stage == LandmarkStage.Building)
                {
                    Add(into, LandmarksText.PaperPhase(f.Phase), false, "name", name);
                }
                if (f.Built && f.Opening == day)
                {
                    Add(into, LandmarksText.PaperOpening, true, "name", name, "builder", BuilderWords(f));
                }
                if (f.Stage == LandmarkStage.Opened && f.Opening + 1 == day)
                {
                    Add(into, LandmarksText.PaperOpened, false, "name", name, "jobs", LandmarkRules.JobsOf(f.Kind).ToString(Plain));
                }
                if (f.Kind == LandmarkKind.Stadium && f.Stage == LandmarkStage.Opened)
                {
                    if (LandmarkRules.MatchDay(day, f.Opening))
                    {
                        Add(into, LandmarksText.PaperMatchDay, false, "team", LandmarksText.T(LandmarksText.Club), "club", LandmarksText.T(LandmarksText.ClubKey(LandmarkRules.Opponent(day))), "hour",
                            LandmarkRules.KickOff.ToString(Plain), "jobs", LandmarkRules.MatchJobs.ToString(Plain));
                    }
                    if (LandmarkRules.MatchDay(day - 1, f.Opening))
                    {
                        LandmarkRules.Score(day - 1, out int home, out int away);
                        string key = home > away ? LandmarksText.PaperWon : home < away ? LandmarksText.PaperLost : LandmarksText.PaperDrew;
                        Add(into, key, true, "team", LandmarksText.T(LandmarksText.Club), "club", LandmarksText.T(LandmarksText.ClubKey(LandmarkRules.Opponent(day - 1))), "home", home.ToString(Plain),
                            "away", away.ToString(Plain), "crowd", LandmarkRules.Attendance(day - 1, f.Opening).ToString(Plain));
                    }
                }
            }
        }

        private static void Add(List<LandmarkStory> into, string key, bool lead, params string[] pairs)
        {
            var data = new Dictionary<string, string>(pairs.Length / 2);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                data[pairs[i]] = pairs[i + 1] ?? string.Empty;
            }
            into.Add(new LandmarkStory { Key = key, Data = data, Lead = lead });
        }
    }
}
