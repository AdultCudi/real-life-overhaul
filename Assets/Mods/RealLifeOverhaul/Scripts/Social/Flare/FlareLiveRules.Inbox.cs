using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>One conversation of the inbox: the account, its newest message and how many are unread. Worked out when the inbox is shown, never kept.</summary>
    public struct FlareThread
    {
        public string With;          // the account's key, as a message names it
        public string Name;          // its name, as the newest message gave it
        public FlareDm Last;         // the newest message shown
        public long At;
        public int Unread;
        public int Count;
    }

    /// <summary>
    /// Flare's living network, the kit's notifications and inbox: everything the feed's notifications hold (Notices), and what the
    /// account's own records say happened (the day's new followers from its history, a business's offer of paid posts, a hired
    /// influencer's post and what it brought, a post's boost run to its end, a resident's message about a job), newest first and
    /// capped; and the messages grouped into conversations. Game-free: the records are the save's, so nothing is sent and every
    /// machine of a session works out its own. Cost: one walk over the posts, messages, offers and boosts, when the page opens.
    /// </summary>
    public static partial class FlareLiveRules
    {
        public const int NoticeHour = 21;      // the evening a day's follows and a campaign's post are told at
        public const int OfferHour = 9;        // the morning an offer comes in

        private static readonly List<string> TagScratch = new List<string>(4);
        private static readonly List<int> CountScratch = new List<int>(4);
        private static readonly Comparison<FlareNotice> Newest = (a, b) => b.At.CompareTo(a.At);
        private static readonly Comparison<FlareThread> NewestThread = (a, b) => b.At.CompareTo(a.At);

        /// <summary>The kit's notifications, newest first, at most <paramref name="most"/>: the feed's and the account's own records.</summary>
        public static void AllNotices(FlareFile f, long now, string me, List<string> myPlaces, List<FlareOffer> offers, List<FlareReaction> scratch, List<FlareNotice> into, int most,
            IReadOnlyList<FlareAd> ads = null)
        {
            Notices(f, now, me, myPlaces, scratch, into, int.MaxValue);
            for (int i = 0; i < into.Count; i++)
            {
                FlareNotice n = into[i];
                if (n.Kind == FlareNoticeKind.Dm && JobDm(f.Net, n.Dm))
                {
                    n.Kind = FlareNoticeKind.Job;
                    into[i] = n;
                }
            }
            int today = (int)(now / 1440);
            int[] h = f.History;
            for (int k = 1; h != null && k < h.Length; k++)
            {
                int gain = h[k] - h[k - 1], day = today - (h.Length - k);
                long at = Now(day, NoticeHour, 0);
                if (gain > 0 && day >= 0 && at <= now)
                {
                    into.Add(new FlareNotice { Kind = FlareNoticeKind.Follow, At = at, Post = -1, Dm = -1, Figure = gain });
                }
            }
            for (int i = 0; offers != null && i < offers.Count; i++)
            {
                FlareOffer o = offers[i];
                long at = o != null ? Now(o.Day, OfferHour, 0) : long.MaxValue;
                if (at <= now)
                {
                    into.Add(new FlareNotice { Kind = FlareNoticeKind.Offer, At = at, Post = -1, Dm = -1, Who = OfferKey(o.Id), Name = o.Name, Figure = o.Posts, Cents = o.Fee });
                }
            }
            for (int i = 0; i < f.PastHires.Count; i++)
            {
                FlarePastHire p = f.PastHires[i];
                long at = Now(p.Day, NoticeHour, 0);
                if (at <= now)
                {
                    into.Add(new FlareNotice
                    {
                        Kind = FlareNoticeKind.Campaign, At = at, Post = -1, Dm = -1, Who = "i:" + p.Who, Name = p.Name, Figure = p.Visitors, Cents = p.Cents, Bad = p.Flopped
                    });
                }
            }
            for (int i = 0; i < f.Boosts.Count; i++)
            {
                FlareBoost b = f.Boosts[i];
                long end = (long)b.FromMinute + (long)b.Hours * 60;
                if (b.Hours > 0 && end <= now)
                {
                    FlarePost p = f.Post(b.Post);
                    into.Add(new FlareNotice
                    {
                        Kind = FlareNoticeKind.Promo, At = end, Post = b.Post, Dm = -1, Who = "me", Name = p != null ? p.Name : null, Figure = b.GivenVisitors, Cents = b.GivenCents,
                        Bad = b.Flopped
                    });
                }
            }
            FlareNoticeRules.AddDerived(f, now, ads, into, TagScratch, CountScratch);
            into.Sort(Newest);
            if (into.Count > most)
            {
                into.RemoveRange(most, into.Count - most);
            }
        }

        /// <summary>The key a business's offer of paid posts writes to the player from.</summary>
        public static string OfferKey(int id) => "d:" + id;

        /// <summary>The offer a conversation is about, or -1 for another account.</summary>
        public static int OfferOf(string with)
        {
            int id;
            return with != null && with.Length > 2 && with[0] == 'd' && with[1] == ':' && int.TryParse(with.Substring(2), out id) ? id : -1;
        }

        private static bool JobDm(FlareNet net, int id)
        {
            for (int i = 0; id >= 0 && i < net.Dms.Count; i++)
            {
                if (net.Dms[i].Id == id)
                {
                    return net.Dms[i].Key == "reallife_flare_dm_job";
                }
            }
            return false;
        }

        /// <summary>The inbox: a conversation for each account, newest first, with what is unread; messages not yet due are left out.</summary>
        public static void Threads(FlareNet net, long now, List<FlareThread> into)
        {
            into.Clear();
            for (int i = 0; i < net.Dms.Count; i++)
            {
                FlareDm m = net.Dms[i];
                if (m.At > now || m.With == null)
                {
                    continue;
                }
                int at = -1;
                for (int k = 0; k < into.Count && at < 0; k++)
                {
                    at = into[k].With == m.With ? k : -1;
                }
                FlareThread t = at >= 0 ? into[at] : new FlareThread { With = m.With };
                if (t.Last == null || m.At >= t.At)
                {
                    t.Last = m;
                    t.At = m.At;
                    t.Name = string.IsNullOrEmpty(m.Name) ? t.Name : m.Name;
                }
                t.Count++;
                t.Unread += !m.Mine && !m.Read ? 1 : 0;
                if (at >= 0)
                {
                    into[at] = t;
                }
                else
                {
                    into.Add(t);
                }
            }
            into.Sort(NewestThread);
        }

        /// <summary>One conversation's messages, oldest first, due by now.</summary>
        public static void Thread(FlareNet net, string with, long now, List<FlareDm> into)
        {
            into.Clear();
            for (int i = 0; with != null && i < net.Dms.Count; i++)
            {
                FlareDm m = net.Dms[i];
                if (m.With == with && m.At <= now)
                {
                    into.Add(m);
                }
            }
        }

        /// <summary>Messages from others not read yet: the badge on the inbox.</summary>
        public static int UnreadDms(FlareNet net, long now)
        {
            int n = 0;
            for (int i = 0; i < net.Dms.Count; i++)
            {
                FlareDm m = net.Dms[i];
                n += !m.Mine && !m.Read && m.At <= now ? 1 : 0;
            }
            return n;
        }

        /// <summary>Notifications newer than the ones seen: the badge on the notifications.</summary>
        public static int UnseenNotices(List<FlareNotice> notices, long seen)
        {
            int n = 0;
            for (int i = 0; i < notices.Count; i++)
            {
                n += notices[i].At > seen && notices[i].Kind != FlareNoticeKind.Dm ? 1 : 0;   // a message is the inbox's badge
            }
            return n;
        }
    }
}
