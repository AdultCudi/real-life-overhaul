using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// Newsstand's rules: prices, which titles the subscriptions cover, taking one out and cancelling it, the morning's
    /// editions over the home's connection, the editions still to be read, and what each title prints. Game-free.
    /// </summary>
    public static class NewsstandRules
    {
        /// <summary>A week of a subscription at a price level (tenths of a point, 1000 at the start's prices).</summary>
        public static int WeekPrice(int sub, int level)
        {
            return PriceRules.At(NewsstandCatalogue.WeekAtStart[sub], level);
        }

        /// <summary>The better of two homes' connections (the one nearer to working).</summary>
        public static NetState Best(NetState a, NetState b)
        {
            return a <= b ? a : b;
        }

        public static bool Works(NetState net)
        {
            return net == NetState.Online;
        }

        /// <summary>True while a title is read by subscription: its own, or the pass (a cancelled one runs until its last day paid for).</summary>
        public static bool Covered(NewsstandState s, int title)
        {
            return s.Subs[title].Subscribed || s.Subs[NewsstandCatalogue.Pass].Subscribed;
        }

        /// <summary>How many subscriptions run, cancelled ones included.</summary>
        public static int Running(NewsstandState s)
        {
            int count = 0;
            for (int i = 0; i < s.Subs.Length; i++)
            {
                count += s.Subs[i].Subscribed ? 1 : 0;
            }
            return count;
        }

        /// <summary>What the subscriptions that renew come to a week at a price level (the cancelled ones are not counted).</summary>
        public static int Weekly(NewsstandState s, int level)
        {
            int sum = 0;
            for (int i = 0; i < s.Subs.Length; i++)
            {
                sum += s.Subs[i].Subscribed && !s.Subs[i].Cancelled ? WeekPrice(i, level) : 0;
            }
            return sum;
        }

        /// <summary>Whether a subscription can be taken out over the homes' connection; payNow false for a cancelled one taken up again.</summary>
        public static OnlineRefusal Subscribe(NewsstandState s, int sub, NetState net, out bool payNow)
        {
            payNow = false;
            if (net == NetState.NoHome)
            {
                return OnlineRefusal.NoHome;
            }
            if (!Works(net))
            {
                return OnlineRefusal.NoNet;
            }
            OnlineSub pass = s.Subs[NewsstandCatalogue.Pass];
            if (sub != NewsstandCatalogue.Pass && pass.Subscribed && !pass.Cancelled)
            {
                return OnlineRefusal.InPass;
            }
            OnlineSub x = s.Subs[sub];
            if (x.Subscribed && x.Cancelled)
            {
                x.Cancelled = false;
                return OnlineRefusal.None;
            }
            if (x.Subscribed)
            {
                return OnlineRefusal.Already;
            }
            payNow = true;
            return OnlineRefusal.None;
        }

        /// <summary>The first week paid: the subscription runs from today. The pass cancels the titles' own; returns how many it cancelled.</summary>
        public static int Start(NewsstandState s, int sub, int day)
        {
            OnlineSub x = s.Subs[sub];
            x.Subscribed = true;
            x.Cancelled = false;
            x.Since = day;
            x.PaidUntil = day + NewsstandCatalogue.WeekDays - 1;
            x.LastDay = day - 1;
            x.Weeks++;
            int cancelled = 0;
            if (sub == NewsstandCatalogue.Pass)
            {
                for (int t = 0; t < NewsstandCatalogue.Titles; t++)
                {
                    cancelled += Cancel(s, t) ? 1 : 0;
                }
            }
            return cancelled;
        }

        /// <summary>Cancels a subscription: its editions still come until the last day paid for. False if there is none to cancel.</summary>
        public static bool Cancel(NewsstandState s, int sub)
        {
            OnlineSub x = s.Subs[sub];
            if (!x.Subscribed || x.Cancelled)
            {
                return false;
            }
            x.Cancelled = true;
            return true;
        }

        /// <summary>The next morning a week is paid for, while the subscription runs and is not cancelled; 0 otherwise.</summary>
        public static int NextPayment(OnlineSub x)
        {
            return x.Subscribed && !x.Cancelled ? x.PaidUntil + 1 : 0;
        }

        /// <summary>
        /// One subscription's morning while today's edition is out: paid-for mornings that never came are given back.
        /// With no connection nothing comes or is charged (Waiting); past the days paid for, a cancelled one ends and
        /// any other pays a week or lapses.
        /// </summary>
        public static OnlineMorning Morning(NewsstandState s, int sub, int day, bool online, Func<int, bool> pay)
        {
            OnlineSub x = s.Subs[sub];
            if (!x.Subscribed || day <= x.LastDay)
            {
                return OnlineMorning.None;
            }
            int missedPaid = Math.Min(day - 1, x.PaidUntil) - x.LastDay;
            if (missedPaid > 0)
            {
                x.PaidUntil += missedPaid;
            }
            x.LastDay = day - 1;   // the mornings before are settled: a second look this morning gives nothing more back
            if (!online)
            {
                return OnlineMorning.Waiting;
            }
            x.LastDay = day;
            if (day <= x.PaidUntil)
            {
                return OnlineMorning.Loaded;
            }
            if (x.Cancelled)
            {
                x.Subscribed = false;
                x.Cancelled = false;
                return OnlineMorning.Ended;
            }
            if (pay == null || !pay(sub))
            {
                x.Subscribed = false;
                return OnlineMorning.Lapsed;
            }
            x.PaidUntil = day + NewsstandCatalogue.WeekDays - 1;
            x.Weeks++;
            return OnlineMorning.Billed;
        }

        public static bool Came(OnlineMorning m)
        {
            return m == OnlineMorning.Loaded || m == OnlineMorning.Billed;
        }

        /// <summary>Every subscription's morning into results, then each title's edition marked as come; returns how many came now.</summary>
        public static int Round(NewsstandState s, int day, bool online, Func<int, bool> pay, OnlineMorning[] results)
        {
            for (int i = 0; i < NewsstandCatalogue.Subscriptions; i++)
            {
                results[i] = Morning(s, i, day, online, pay);
            }
            int came = 0;
            bool pass = Came(results[NewsstandCatalogue.Pass]);
            for (int t = 0; t < NewsstandCatalogue.Titles; t++)
            {
                if ((pass || Came(results[t])) && !Has(s, t, day))
                {
                    s.Loaded[t].Add(day);
                    came++;
                }
                Forget(s.Loaded[t], day);
            }
            return came;
        }

        /// <summary>True if some subscription has today still to settle: the round has something to do.</summary>
        public static bool Due(NewsstandState s, int day)
        {
            for (int i = 0; i < s.Subs.Length; i++)
            {
                if (s.Subs[i].Subscribed && s.Subs[i].LastDay < day)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool Has(NewsstandState s, int title, int day)
        {
            return s.Loaded[title].Contains(day);
        }

        /// <summary>The days a title can be read on, newest first: those its edition came on, within the days kept.</summary>
        public static void Days(NewsstandState s, int title, int today, List<int> into)
        {
            into.Clear();
            List<int> loaded = s.Loaded[title];
            for (int i = loaded.Count - 1; i >= 0; i--)
            {
                if (loaded[i] > today - NewsstandCatalogue.KeepDays && loaded[i] <= today && !into.Contains(loaded[i]))
                {
                    into.Add(loaded[i]);
                }
            }
            into.Sort((a, b) => b.CompareTo(a));
        }

        private static void Forget(List<int> days, int today)
        {
            for (int i = days.Count - 1; i >= 0; i--)
            {
                if (days[i] <= today - NewsstandCatalogue.KeepDays || days[i] > today)
                {
                    days.RemoveAt(i);
                }
            }
        }

        /// <summary>The edition just replaced as the latest, kept to be read again: one per day, its chart cut, the oldest let go.</summary>
        public static void Keep(NewsstandState s, LedgerEdition old, int today)
        {
            if (old != null && old.Day > today - NewsstandCatalogue.KeepDays && old.Day < today)
            {
                for (int i = s.Earlier.Count - 1; i >= 0; i--)
                {
                    if (s.Earlier[i] == null || s.Earlier[i].Day == old.Day)
                    {
                        s.Earlier.RemoveAt(i);
                    }
                }
                if (old.IndexCloses.Count > NewsstandCatalogue.CloseDays)
                {
                    old.IndexCloses.RemoveRange(0, old.IndexCloses.Count - NewsstandCatalogue.CloseDays);
                }
                s.Earlier.Add(old);
            }
            for (int i = s.Earlier.Count - 1; i >= 0; i--)
            {
                if (s.Earlier[i] == null || s.Earlier[i].Day <= today - NewsstandCatalogue.KeepDays || s.Earlier[i].Day >= today)
                {
                    s.Earlier.RemoveAt(i);
                }
            }
            s.Earlier.Sort((a, b) => a.Day.CompareTo(b.Day));
        }

        /// <summary>The edition of a day: the latest (the City Ledger's own) or one kept; null if there is none.</summary>
        public static LedgerEdition Find(NewsstandState s, LedgerEdition latest, int day)
        {
            if (latest != null && latest.Day == day)
            {
                return latest;
            }
            for (int i = 0; i < s.Earlier.Count; i++)
            {
                if (s.Earlier[i] != null && s.Earlier[i].Day == day)
                {
                    return s.Earlier[i];
                }
            }
            return null;
        }

        /// <summary>True if a title prints a section of the paper (every title has the weather).</summary>
        public static bool Prints(int title, LedgerSection section)
        {
            if (section == LedgerSection.Weather)
            {
                return true;
            }
            LedgerSection[] sections = NewsstandCatalogue.Sections[title];
            for (int i = 0; i < sections.Length; i++)
            {
                if (sections[i] == section)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A title's lead: the heaviest piece of the sections it prints, the weather left out; null if it has none.</summary>
        public static LedgerPiece Lead(LedgerEdition e, int title)
        {
            LedgerPiece best = null;
            for (int i = 0; i < e.Pieces.Count; i++)
            {
                LedgerPiece p = e.Pieces[i];
                if (p == null || p.Section == LedgerSection.Weather || !Prints(title, p.Section))
                {
                    continue;
                }
                if (best == null || p.Weight > best.Weight)
                {
                    best = p;
                }
            }
            return best;
        }

        /// <summary>How many pieces of the edition a title prints, the weather left out.</summary>
        public static int Count(LedgerEdition e, int title)
        {
            int count = 0;
            for (int i = 0; i < e.Pieces.Count; i++)
            {
                LedgerPiece p = e.Pieces[i];
                count += p != null && p.Section != LedgerSection.Weather && Prints(title, p.Section) ? 1 : 0;
            }
            return count;
        }
    }
}
