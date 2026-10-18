using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>Flare's living network, the City Office: its cases and crews, the paper's stories, and what a post moves once a day.</summary>
    public static partial class FlareLiveRules
    {
        // The City Office.
        public const int ReplyLeast = 45;              // minutes before an answer, at the least...
        public const int ReplyPerOpen = 25;            // ...and so many more for each case still open
        public const int ReplyMost = 300;              // ...within five hours at the most
        public const int WeekDays = 7;   // days
        public static readonly int[] CrewsPerWeek = { 0, 3, 2, 2, 3, 0, 0, 2 };   // by FlareOrder: works, patrols, cleanups, inspections, site checks
        public const int PatrolDays = 3;               // the police in a district after a crime report
        public const int CleanupDays = 5;              // a cleaning crew's round in a district
        public const int InspectionDays = 3;           // the talk against an inspected business
        public const int TallyToPaper = 3;             // complaints of one kind in one district in a week, none acted on: the paper has it
        public const int MoodActed = 3;                // the feed's mood about the city's running: a report acted on
        public const int MoodQueued = -4;              // ...one left
        public const int MoodEscalated = -20;          // ...the tally in the paper
        public const int ReportsWeigh = 400;           // thousandths of a point of an issue's weight in the council a report adds (Wiring/FlareLinks: Issue)

        /// <summary>
        /// A report reaches the City Office. The same kind in the same district (for a business, the same business) while an
        /// earlier case is open or acted on is that case again: its repeats grow and <paramref name="again"/> is set. Otherwise
        /// a new case, its answer due within hours, later the more cases are open.
        /// </summary>
        public static FlareCase File(FlareNet net, FlareReport kind, string district, string place, string name, float x, float z, long now, string reporter, int post, int seed,
            out bool again, bool counted = true)
        {
            again = false;
            FlareCase same = OpenCase(net, kind, district, place);
            if (same != null)
            {
                same.Repeats++;
                same.Counted |= counted;
                again = true;
                return same;
            }
            int open = 0;
            for (int i = 0; i < net.Cases.Count; i++)
            {
                open += net.Cases[i].State == FlareCaseState.Open ? 1 : 0;
            }
            var made = new FlareCase
            {
                Id = net.NextCase++, Kind = kind, District = district, Place = place, Name = name, X = x, Z = z, Filed = now, Reporter = reporter, Post = post, Counted = counted,
                ReplyAt = now + Math.Min(ReplyMost, ReplyLeast + ReplyPerOpen * open + FlareRules.Roll(seed, net.NextCase, 830) % 60)
            };
            net.Cases.Add(made);
            while (net.Cases.Count > FlareNet.CasesMost)
            {
                net.Cases.RemoveAt(Evicted(net, reporter));
            }
            return made;
        }

        /// <summary>
        /// The case that gives way when there are too many: the oldest closed one, else the oldest open one that is not another
        /// player's. Another player's open case never goes for somebody else's; the one just filed always qualifies.
        /// </summary>
        public static int Evicted(FlareNet net, string reporter)
        {
            int closed = OldestClosed(net);
            if (closed >= 0)
            {
                return closed;
            }
            for (int i = 0; i < net.Cases.Count; i++)
            {
                string r = net.Cases[i].Reporter;
                if (r == null || !r.StartsWith("p:", StringComparison.Ordinal) || string.Equals(r, reporter, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return net.Cases.Count - 1;
        }

        /// <summary>
        /// Whether a player's report today is one of their first three (counted when it is): only those move the mood, the
        /// week's tally and the paper. <paramref name="who"/> as MoveKey has it; null (the city's people) always counts.
        /// </summary>
        public static bool Counted(FlareNet net, int day, string who)
        {
            if (who == null)
            {
                return true;
            }
            for (int n = 0; n < ReportsWeighed; n++)
            {
                string key = who + "|W|" + n;
                if (!MovedToday(net, day, key))
                {
                    return FirstToday(net, day, key);
                }
            }
            return false;
        }

        /// <summary>A key for what a post does to a friend (tagged) or to the partner (seeing one tagged): once a day each (FirstToday).</summary>
        public static string FriendKey(char what, int person)
        {
            return "me|" + what + "|" + person;
        }

        /// <summary>A key for the talk a post of the player's brings a business: once a day (FirstToday).</summary>
        public static string TalkKey(string place)
        {
            return place != null ? "me|B|" + place : null;
        }

        /// <summary>The case still open or being acted on for a thing (a kind in a district; a business by its place), or null.</summary>
        public static FlareCase OpenCase(FlareNet net, FlareReport kind, string district, string place)
        {
            for (int i = 0; i < net.Cases.Count; i++)
            {
                FlareCase c = net.Cases[i];
                if (c.Kind == kind && (c.State == FlareCaseState.Open || c.State == FlareCaseState.Acting) && string.Equals(c.District, district, StringComparison.Ordinal)
                    && (kind != FlareReport.Business || string.Equals(c.Place, place, StringComparison.Ordinal)))
                {
                    return c;
                }
            }
            return null;
        }

        private static int OldestClosed(FlareNet net)
        {
            for (int i = 0; i < net.Cases.Count; i++)
            {
                if (net.Cases[i].State == FlareCaseState.Done || net.Cases[i].State == FlareCaseState.Queued)
                {
                    return i;
                }
            }
            return -1;
        }

        public const int SoonerMinutes = 40;       // the city's people report the same real fault again: its answer comes so much sooner...
        public const int SoonestMinutes = 20;      // ...but never sooner than this after now

        /// <summary>An open case reported again by the city's people while the faults are real: its answer comes sooner.</summary>
        public static long Sooner(long replyAt, long now)
        {
            return Math.Max(now + SoonestMinutes, replyAt - SoonerMinutes);
        }

        /// <summary>True for a verdict of the city's faults that found nothing (nothing, a warning, no crews for now).</summary>
        public static bool FoundNothing(int verdict)
        {
            return verdict >= (int)FaultAnswer.Nothing && verdict <= (int)FaultAnswer.Barred;
        }

        public const int FalseReportCost = 2;      // points of the player's reputation on Flare a report of nothing costs

        /// <summary>The player's reputation after a report of nothing: <see cref="FalseReportCost"/> less, never below 0.</summary>
        public static int FalseReport(int reputation)
        {
            return FlareRules.Clamp(reputation - FalseReportCost, 0, 100);
        }

        /// <summary>The cases whose answer is due by now, into the list given (oldest first).</summary>
        public static void Due(FlareNet net, long now, List<FlareCase> into)
        {
            into.Clear();
            for (int i = 0; i < net.Cases.Count; i++)
            {
                if (net.Cases[i].State == FlareCaseState.Open && net.Cases[i].ReplyAt <= now)
                {
                    into.Add(net.Cases[i]);
                }
            }
        }

        /// <summary>The orders of a kind given in the week up to now: the crews it took.</summary>
        public static int CrewsUsed(FlareNet net, FlareOrder order, long now)
        {
            int n = 0;
            long from = now - WeekDays * 1440L;
            for (int i = 0; i < net.Cases.Count; i++)
            {
                FlareCase c = net.Cases[i];
                long sent = c.ActedDay >= 0 ? c.ActedDay * 1440L : c.Filed;   // a case of an older save: when it was filed
                n += c.Order == order && sent >= from && c.State != FlareCaseState.Open && c.State != FlareCaseState.Queued ? 1 : 0;
            }
            return n;
        }

        /// <summary>
        /// What the City Office does about a due case: its kind's order if a crew is free this week, an answer alone for the
        /// buses and the power, "not now" when the week's crews are taken, and an answer alone with the option to act off.
        /// </summary>
        public static FlareOrder Decide(FlareNet net, FlareCase c, long now, bool acts, bool siteNear, out FlareAnswer answer)
        {
            return Decide(net, c, now, acts, siteNear, false, out answer);
        }

        /// <summary>
        /// The same, for a report the city's faults found true (<paramref name="realFault"/>): an unsafe place that is real
        /// gets a works crew to make it safe, as a road or a lamp does.
        /// </summary>
        public static FlareOrder Decide(FlareNet net, FlareCase c, long now, bool acts, bool siteNear, bool realFault, out FlareAnswer answer)
        {
            answer = FlareAnswer.Received;
            if (!acts)
            {
                return FlareOrder.Answered;
            }
            FlareOrder order;
            switch (c.Kind)
            {
                case FlareReport.Transit: answer = FlareAnswer.Transit; return FlareOrder.Answered;
                case FlareReport.Utilities: answer = FlareAnswer.Utilities; return FlareOrder.Answered;
                case FlareReport.Road:
                case FlareReport.Lamp: order = FlareOrder.Works; answer = FlareAnswer.Works; break;
                case FlareReport.Crime: order = FlareOrder.Patrol; answer = FlareAnswer.Patrol; break;
                case FlareReport.Litter: order = FlareOrder.Cleanup; answer = FlareAnswer.Cleanup; break;
                case FlareReport.Business: order = FlareOrder.Inspection; answer = FlareAnswer.Inspect; break;
                case FlareReport.Unsafe when realFault: order = FlareOrder.Works; answer = FlareAnswer.Works; break;
                default:
                    order = siteNear ? FlareOrder.SiteCheck : FlareOrder.Patrol;
                    answer = siteNear ? FlareAnswer.Site : FlareAnswer.Patrol;
                    break;
            }
            if (CrewsUsed(net, order, now) >= CrewsPerWeek[(int)order])
            {
                answer = FlareAnswer.Busy;
                return FlareOrder.Queued;
            }
            return order;
        }

        /// <summary>A case acted on (or not): its state, its order and until when, and the watch it puts on its district.</summary>
        public static void Act(FlareNet net, FlareCase c, FlareOrder order, int day, int siteRef)
        {
            c.Order = order;
            c.Ref = siteRef;
            if (order != FlareOrder.Queued && order != FlareOrder.Answered && order != FlareOrder.None)
            {
                c.ActedDay = day;   // a crew sent today: one of the week's
            }
            switch (order)
            {
                case FlareOrder.Patrol:
                    c.Until = day + PatrolDays;
                    Watch(net, c.District, FlareOrder.Patrol, c.Until);
                    c.State = FlareCaseState.Acting;
                    break;
                case FlareOrder.Cleanup:
                    c.Until = day + CleanupDays;
                    Watch(net, c.District, FlareOrder.Cleanup, c.Until);
                    c.State = FlareCaseState.Acting;
                    break;
                case FlareOrder.Inspection:
                    c.Until = day + InspectionDays;
                    c.State = FlareCaseState.Acting;
                    break;
                case FlareOrder.Works:
                    c.State = FlareCaseState.Acting;
                    break;
                case FlareOrder.Queued:
                    c.State = FlareCaseState.Queued;
                    break;
                default:
                    c.State = FlareCaseState.Done;
                    break;
            }
        }

        /// <summary>A watch on a district until a day: the one there already is made longer, never shorter.</summary>
        public static void Watch(FlareNet net, string district, FlareOrder kind, int until)
        {
            if (district == null)
            {
                return;
            }
            for (int i = 0; i < net.Watches.Count; i++)
            {
                FlareWatch w = net.Watches[i];
                if (w.Kind == kind && string.Equals(w.District, district, StringComparison.Ordinal))
                {
                    w.Until = Math.Max(w.Until, until);
                    return;
                }
            }
            net.Watches.Add(new FlareWatch { District = district, Kind = kind, Until = until });
        }

        /// <summary>The day's turn: watches that ran out go, and cases whose days are over are done. True if anything changed.</summary>
        public static bool WatchDay(FlareNet net, int day)
        {
            bool changed = false;
            for (int i = net.Watches.Count - 1; i >= 0; i--)
            {
                if (day >= net.Watches[i].Until)
                {
                    net.Watches.RemoveAt(i);
                    changed = true;
                }
            }
            for (int i = 0; i < net.Cases.Count; i++)
            {
                FlareCase c = net.Cases[i];
                if (c.State == FlareCaseState.Acting && c.Order != FlareOrder.Works && c.Until >= 0 && day >= c.Until)
                {
                    c.State = FlareCaseState.Done;
                    changed = true;
                }
            }
            for (int i = net.Complaints.Count - 1; i >= 0; i--)
            {
                if (day - net.Complaints[i].Day >= WeekDays)
                {
                    net.Complaints.RemoveAt(i);
                }
            }
            for (int i = net.Escalated.Count - 1; i >= 0; i--)
            {
                string[] f = net.Escalated[i].Split('|');
                int when;
                if (f.Length < 3 || !int.TryParse(f[2], out when) || day - when >= WeekDays)
                {
                    net.Escalated.RemoveAt(i);
                }
            }
            return changed;
        }

        /// <summary>A complaint noted for the week's tally: whose does not matter, the player's, the city's people's or another player's.</summary>
        public static void Complained(FlareNet net, FlareReport kind, string district, int day, bool acted)
        {
            if (district == null)
            {
                return;
            }
            net.Complaints.Add(new FlareComplaint { Kind = kind, District = district, Day = day, Acted = acted });
            while (net.Complaints.Count > FlareNet.ComplaintsMost)
            {
                net.Complaints.RemoveAt(0);
            }
        }

        /// <summary>A complaint acted on after all: the week's complaints of its kind there are acted on too.</summary>
        public static void Answered(FlareNet net, FlareReport kind, string district)
        {
            for (int i = 0; i < net.Complaints.Count; i++)
            {
                FlareComplaint c = net.Complaints[i];
                if (c.Kind == kind && string.Equals(c.District, district, StringComparison.Ordinal))
                {
                    c.Acted = true;
                }
            }
        }

        /// <summary>Complaints of a kind in a district this week that nothing was done about.</summary>
        public static int Tally(FlareNet net, FlareReport kind, string district, int day)
        {
            int n = 0;
            for (int i = 0; i < net.Complaints.Count; i++)
            {
                FlareComplaint c = net.Complaints[i];
                n += c.Kind == kind && !c.Acted && day - c.Day < WeekDays && string.Equals(c.District, district, StringComparison.Ordinal) ? 1 : 0;
            }
            return n;
        }

        /// <summary>
        /// Whether the week's ignored complaints of a kind in a district are a story for the paper now: three or more, and
        /// not in the paper already this week. True takes it (it is noted).
        /// </summary>
        public static bool Escalate(FlareNet net, FlareReport kind, string district, int day, bool acts)
        {
            if (!Escalates(kind, acts) || district == null || Tally(net, kind, district, day) < TallyToPaper)
            {
                return false;
            }
            string prefix = (int)kind + "|" + district + "|";
            for (int i = 0; i < net.Escalated.Count; i++)
            {
                if (net.Escalated[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            net.Escalated.Add(prefix + day);
            return true;
        }

        /// <summary>
        /// Whether ignored complaints of a kind can reach the paper: only while the City Office acts on reports, and never for
        /// the buses or the power, which it only passes on.
        /// </summary>
        public static bool Escalates(FlareReport kind, bool acts)
        {
            return acts && kind != FlareReport.Transit && kind != FlareReport.Utilities;
        }

        /// <summary>
        /// What the City Office does once ignored complaints are in the paper: the crew for the case's kind, if the case is not
        /// acted on already and a crew is free this week; None otherwise.
        /// </summary>
        public static FlareOrder EscalationOrder(FlareNet net, FlareCase c, long now, bool acts)
        {
            if (c == null || !Escalates(c.Kind, acts) || c.State == FlareCaseState.Acting || FoundNothing(c.Verdict))
            {
                return FlareOrder.None;   // a case the city's faults found nothing in is never acted on
            }
            if (c.Kind == FlareReport.Business && c.Verdict == (int)FaultAnswer.Clean)
            {
                return FlareOrder.None;   // a business found clean is not inspected
            }
            FlareOrder order = c.Kind == FlareReport.Crime ? FlareOrder.Patrol : c.Kind == FlareReport.Litter ? FlareOrder.Cleanup : c.Kind == FlareReport.Business ? FlareOrder.Inspection
                : c.Kind == FlareReport.Unsafe ? (c.Verdict == (int)FaultAnswer.True ? FlareOrder.Works : FlareOrder.Patrol) : FlareOrder.Works;   // a real unsafe place: works, as when answered
            return CrewsUsed(net, order, now) >= CrewsPerWeek[(int)order] ? FlareOrder.None : order;
        }

        /// <summary>Whether the city's posts of a day are still to be made when Flare starts: not on a guest that follows.</summary>
        public static bool StartDay(FlareNet net, int day, bool following)
        {
            return !following && net.CityDay != day;
        }

        /// <summary>Whether a post of the player's goes to the session: all while they share their posts, a report always.</summary>
        public static bool ToSession(bool sharePosts, FlareSource source)
        {
            return sharePosts || source == FlareSource.Report;
        }

        /// <summary>
        /// Whether the host answers a post of this player's (the city's replies, an account's answer): true on a guest that
        /// follows and sends it; it then does not answer it itself, or the replies would come twice.
        /// </summary>
        public static bool HostAnswers(bool following, bool sharePosts, FlareSource source)
        {
            return following && ToSession(sharePosts, source);
        }

        /// <summary>A story for a morning's paper, kept until it is printed (a few at most, the oldest go).</summary>
        public static void Story(FlareNet net, int day, string key, params string[] data)
        {
            net.Stories.Add(new FlareStory { Day = day, Key = key, Data = data ?? new string[0] });
            while (net.Stories.Count > FlareNet.StoriesMost)
            {
                net.Stories.RemoveAt(0);
            }
        }

        /// <summary>
        /// A story for a morning's paper, once for its day and key: false (nothing kept) when that morning has it already.
        /// A pile-on is in the paper once, however many posts went badly the day before.
        /// </summary>
        public static bool StoryOnce(FlareNet net, int day, string key, params string[] data)
        {
            for (int i = 0; i < net.Stories.Count; i++)
            {
                if (net.Stories[i].Day == day && string.Equals(net.Stories[i].Key, key, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            Story(net, day, key, data);
            return true;
        }

        /// <summary>
        /// The key of what a post moves, for <see cref="FirstToday"/>: who posted it ("me", or "p:" and a session's player),
        /// what it is (a review, an announcement, a report of a kind in a district) and the business. Null for anything else.
        /// </summary>
        public static string MoveKey(string who, FlareSource source, int arg, string district, string place)
        {
            if (string.IsNullOrEmpty(who))
            {
                return null;
            }
            switch (source)
            {
                case FlareSource.Review: return who + "|R|" + place;
                case FlareSource.Announce: return who + "|A|" + arg + "|" + place;
                case FlareSource.Report: return who + "|K|" + arg + "|" + district + "|" + place;
                default: return null;
            }
        }

        /// <summary>Who posted, as <see cref="MoveKey"/> names them; null for the city's own accounts.</summary>
        public static string Mover(FlarePost p)
        {
            if (p == null)
            {
                return null;
            }
            return p.Author == FlareAuthor.Player ? "me" : p.Author == FlareAuthor.Network && !string.IsNullOrEmpty(p.AuthorId) ? "p:" + p.AuthorId : null;
        }

        /// <summary>Whether a key has moved something today; read only (the compose sheet's note).</summary>
        public static bool MovedToday(FlareNet net, int day, string key)
        {
            return key != null && net.MovedDay == day && net.Moved.Contains(key);
        }

        /// <summary>
        /// The first time today a player's review, announcement or report moves its thing: true, and noted. The same again the
        /// same day is made and answered but moves nothing more. A null key (the city's own) is always first.
        /// </summary>
        public static bool FirstToday(FlareNet net, int day, string key)
        {
            if (key == null)
            {
                return true;
            }
            if (net.MovedDay != day)
            {
                net.MovedDay = day;
                net.Moved.Clear();
            }
            if (net.Moved.Contains(key))
            {
                return false;
            }
            net.Moved.Add(key);
            while (net.Moved.Count > MovedMost)
            {
                net.Moved.RemoveAt(0);
            }
            return true;
        }

        /// <summary>Whether a report of the player's adds to an issue's weight in the council: the first three of a day do. Counted when it does.</summary>
        public static bool Weighs(FlareNet net, int day)
        {
            if (net.ReportsDay != day)
            {
                net.ReportsDay = day;
                net.ReportsToday = 0;
            }
            if (net.ReportsToday >= ReportsWeighed)
            {
                return false;
            }
            net.ReportsToday++;
            return true;
        }

        /// <summary>
        /// Whether this machine applies what a post does to a business: never for another player's post in a session (their own
        /// game applies it); an announcement only to the poster's own business; a review or a complaint's inspection to the
        /// player's own or one of the city's, which every machine has its own copy of.
        /// </summary>
        public static bool EffectHere(FlareSource source, bool foreign, bool mine, bool ownPost)
        {
            if (foreign)
            {
                return false;
            }
            if (source == FlareSource.Announce)
            {
                return ownPost && mine;
            }
            return source == FlareSource.Review || source == FlareSource.Report;
        }

        /// <summary>What a review does to a business's customers, per cent for two days: more for a review many saw.</summary>
        public static int ReviewBuzz(int stars, int reach)
        {
            int big = reach >= 2000 ? 1 : 0;
            return stars >= 4 ? 1 + big : stars <= 2 ? -1 - big : 0;
        }

        /// <summary>What an announcement for a business does: a little more custom for two days, more for an account many follow.</summary>
        public static int AnnounceBuzz(int reach)
        {
            return reach >= 5000 ? 2 : 1;
        }
    }
}
