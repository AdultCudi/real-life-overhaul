using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>Flare by rule, what a post does outside Flare: talk about a business, scandals, the boss, the partner, the council and the market.</summary>
    public static partial class FlareRules
    {
        // What a post does elsewhere (Wiring/FlareLinks carries each to its feature).
        public const int BuzzMost = 6;             // per cent of a business's customers from Flare's talk, all of it together, either way
        public const int BuzzDays = 3;             // a post about a business is talked about this long
        public const int HypeGapDays = 14;         // a viral post raises the game's own hype for a product once a fortnight at most
        public const int ScandalWorst = 1250;      // thousandths of a scandal's loss to a brand, for an account of no reputation...
        public const int ScandalAnswered = 800;    // ...and what is left of it after an honest answer
        public const int ArrestShare = 30;         // thousandths of the followers an arrest costs, from Local on
        public const int ArrestReputation = 3, ArrestReputationKnown = 6;   // points
        public const int EndorseFull = 100000;     // followers at which the account's word in an election is worth the most
        public const int BossBase = 200;           // per mille: the boss follows the player, and more for each rank and tier
        public const int BossStep = 150;   // per mille more for each rank and tier
        public const int SponsoredFrom = 500;      // followers: a sponsored post is offered from Local on
        public const int SponsoredLeast = 250, SponsoredMost = 1500;   // thousandths of radio's customers a day, by followers

        // The rest of the city: people met online, the partner, the market, the council and the courts.
        public static readonly int[] OnlinePerMonth = { 0, 2, 3, 4, 5 };   // people the player has still to meet who answer a post and become contacts, by tier
        public const int OnlineGapDays = 5;
        public const int NeverPostedDays = 21;     // the partner not posted this long, while others are: they mind
        public const int OthersLately = 7;   // days
        public const int NeglectPosts = 3;         // posts in a day while a friend's texts wait
        public const int NeglectGapDays = 7;
        public const int MoodBadNews = 8;          // the feed's mood about the city's running: each kind of bad news today
        public const int MoodIssuePost = 10;       // ...a post of the player's about an issue
        public const int MoodKeep = 800;           // thousandths of yesterday's mood kept
        public const int IssueMost = 300;          // thousandths of a point of an issue's weight in the council a post can add, at the most
        public const int AbuseDays = 2;            // selling shares this soon after a viral post about the company is market abuse
        public const int FameFee = 150;            // thousandths more a lawyer asks of a Celebrity
        public const int Thumbs = 5;               // the newest pictures shown in their posts
        public const int ThumbWidth = 256;         // pixels

        /// <summary>The talk a post about a business brings, per cent of its customers: more as more people see it.</summary>
        public static int BuzzOf(FlarePost p)
        {
            if (p.Place == null || p.Bad || p.Author != FlareAuthor.Player)
            {
                return 0;
            }
            return p.Viral ? 4 : p.Reach >= 5000 ? 3 : p.Reach >= 1000 ? 2 : 1;
        }

        /// <summary>Talk about a business from today, added within the cap either way. The business's figure now.</summary>
        public static int AddBuzz(FlareFile f, string place, int percent, int day, int days)
        {
            if (string.IsNullOrEmpty(place) || percent == 0)
            {
                return f.BuzzAt(place, day);
            }
            int now = f.BuzzAt(place, day);
            int room = percent > 0 ? Math.Max(0, BuzzMost - now) : Math.Min(0, -BuzzMost - now);
            int add = percent > 0 ? Math.Min(percent, room) : Math.Max(percent, room);
            if (add != 0)
            {
                f.Buzz.Add(new FlareBuzz { Place = place, Percent = add, Until = day + Math.Max(1, days) });
            }
            return f.BuzzAt(place, day);
        }

        /// <summary>Talk that has run its days goes. True if any went.</summary>
        public static bool BuzzDay(FlareFile f, int day)
        {
            bool gone = false;
            for (int i = f.Buzz.Count - 1; i >= 0; i--)
            {
                if (day >= f.Buzz[i].Until)
                {
                    f.Buzz.RemoveAt(i);
                    gone = true;
                }
            }
            return gone;
        }

        public static bool MayHype(FlareFile f, int day)
        {
            return day - f.HypeDay >= HypeGapDays;
        }

        /// <summary>What a scandal at a brand's shop costs, thousandths of its loss: more for an account the city doubts.</summary>
        public static int ScandalScale(int reputation)
        {
            return reputation >= 50 ? 1000 : Clamp(1000 + (50 - reputation) * (ScandalWorst - 1000) / 50, 1000, ScandalWorst);
        }

        /// <summary>What an honest answer to a scandal gives back of what it cost: down to four fifths of the plain loss.</summary>
        public static int Answered(int lost, int scale)
        {
            return Math.Max(0, lost - (int)((long)lost * ScandalAnswered / Math.Max(1000, scale)));
        }

        /// <summary>How much of the most an account's word adds to an endorsement, thousandths: by followers, for a trusted account.</summary>
        public static int EndorseShare(int followers, int reputation)
        {
            double share = Math.Log10(1.0 + Math.Max(0, followers)) / Math.Log10(1.0 + EndorseFull);
            return Clamp((int)(Math.Min(1.0, share) * 1000.0 * Clamp(reputation, 0, 50) / 50.0), 0, 1000);
        }

        /// <summary>An arrest trends: from Local on, 3% of the followers and some reputation, once a day. Returns the followers lost.</summary>
        public static int Arrest(FlareFile f, int day)
        {
            if (f.ArrestDay == day || f.Followers < TierFrom[(int)FlareTier.Local])
            {
                return 0;
            }
            f.ArrestDay = day;
            int lost = (int)((long)f.Followers * ArrestShare / 1000);
            f.Reputation = Clamp(f.Reputation - (f.Tier >= FlareTier.Known ? ArrestReputationKnown : ArrestReputation), 0, 100);
            f.Followers -= lost;
            return lost;
        }

        /// <summary>Whether the boss follows the player: a fifth of bosses, more for each rank and tier. The same for the same job.</summary>
        public static bool BossFollows(int seed, int career, FlareTier tier)
        {
            return career >= 0 && Roll(seed, career / 100, 811) < BossBase + BossStep * (career % 100 + (int)tier);
        }

        /// <summary>What a sponsored post brings, thousandths of radio's customers a day, from Local on; 0 below.</summary>
        public static int SponsoredReach(int followers)
        {
            if (followers < SponsoredFrom)
            {
                return 0;
            }
            return Clamp((int)Math.Round(SponsoredLeast + 375.0 * Math.Log10(followers / (double)SponsoredFrom)), SponsoredLeast, SponsoredMost);
        }

        /// <summary>What a sponsored post costs, thousandths of its base price: less for a trusted account, more for a doubted one.</summary>
        public static int SponsoredCost(int reach, int reputation)
        {
            return (int)((long)reach * (1100 - 2 * Clamp(reputation, 0, 100)) / 1000);
        }

        /// <summary>Whether a company campaign is one of Flare's own deals still on, which has its Business tab row already.</summary>
        public static bool IsDeal(FlareFile f, string place, int kind, int from)
        {
            for (int i = 0; f != null && i < f.Hires.Count; i++)
            {
                FlareHire h = f.Hires[i];
                if (!h.Ended && h.Kind == kind && h.From == from && string.Equals(h.Place, place, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        public static string BusyKey(bool onFlare)
        {
            return onFlare ? "reallife_flare_busy_flare" : "reallife_flare_busy_company";
        }

        public static string BusyWhenKey(int day, int from, int to)
        {
            return day >= from ? "reallife_flare_busy_running" : from >= to ? "reallife_flare_busy_booked_day" : "reallife_flare_busy_booked_days";
        }

        /// <summary>Whether somebody the player has still to meet answers yesterday's post and becomes a contact: a few a month.</summary>
        public static bool MeetsOnline(FlareFile f, int day, bool postedYesterday)
        {
            int perMonth = OnlinePerMonth[Clamp((int)f.Tier, 0, OnlinePerMonth.Length - 1)];
            return postedYesterday && perMonth > 0 && day - f.OnlineDay >= OnlineGapDays && Roll(f.Seed, day, 901) < perMonth * 1000 / 30;
        }

        /// <summary>How a partner takes a post of the player with somebody they could fall for.</summary>
        public enum PartnerView { Proud = 0, Cool = 1, Jealous = 2 }

        public static PartnerView ViewOf(FlareTaste partner)
        {
            return (partner & FlareTaste.Cheers) != 0 ? PartnerView.Proud : (partner & FlareTaste.Cool) != 0 ? PartnerView.Jealous : PartnerView.Cool;
        }

        /// <summary>A tag noted, the partner's or somebody else's, for "you never post us".</summary>
        public static void Tagged(FlareFile f, bool partner, int day)
        {
            if (partner)
            {
                f.PartnerTagDay = day;
            }
            else
            {
                f.OthersTagDay = day;
            }
        }

        /// <summary>The partner not in a post for three weeks while others were lately minds it, once in three weeks.</summary>
        public static bool NeverPosted(FlareFile f, int day, bool partner)
        {
            if (!partner || day - Math.Max(f.PartnerTagDay, f.StartDay) < NeverPostedDays || f.OthersTagDay < 0 || day - f.OthersTagDay > OthersLately || day - f.NeverDay < NeverPostedDays)
            {
                return false;
            }
            f.NeverDay = day;
            return true;
        }

        /// <summary>A friend whose texts wait minds the player posting all day: three posts yesterday, once a week.</summary>
        public static bool Neglect(FlareFile f, int day, int postsYesterday, bool unanswered)
        {
            if (!unanswered || postsYesterday < NeglectPosts || day - f.NeglectDay < NeglectGapDays)
            {
                return false;
            }
            f.NeglectDay = day;
            return true;
        }

        /// <summary>The feed's mood about the city's running for a day: four fifths of yesterday's, less for each kind of bad news.</summary>
        public static int Mood(FlareFile f, int badNews)
        {
            f.MayorMood = Clamp(f.MayorMood * MoodKeep / 1000 - MoodBadNews * Math.Max(0, badNews), -100, 100);
            return f.MayorMood;
        }

        /// <summary>What the mood pulls on the mayor's approval a day, in tenths of a point: a point at most either way.</summary>
        public static int ApprovalPull(int mood)
        {
            return Clamp(mood / 10, -10, 10);
        }

        /// <summary>What a post about an issue adds to its weight in the council, thousandths of a point: at most 0.3.</summary>
        public static int IssueWeight(int followers)
        {
            return (int)(IssueMost * Math.Min(1.0, Math.Log10(1.0 + Math.Max(0, followers)) / Math.Log10(1.0 + EndorseFull)));
        }

        public static bool Discloses(FlareFile f)
        {
            return f.Tier >= FlareTier.Known;
        }

        /// <summary>Market abuse: the viral post, spent, about a company sold within two days of it; or null.</summary>
        public static FlareViralShares Abuse(FlareFile f, int stock, int day)
        {
            for (int i = 0; i < f.ViralShares.Count; i++)
            {
                FlareViralShares v = f.ViralShares[i];
                if (v.Stock == stock && day - v.Day <= AbuseDays && day >= v.Day)
                {
                    f.ViralShares.RemoveAt(i);
                    return v;
                }
            }
            return null;
        }

        public static void AbuseDay(FlareFile f, int day)
        {
            for (int i = f.ViralShares.Count - 1; i >= 0; i--)
            {
                if (day - f.ViralShares[i].Day > AbuseDays)
                {
                    f.ViralShares.RemoveAt(i);
                }
            }
        }

        /// <summary>What a lawyer asks more of a Celebrity, thousandths.</summary>
        public static int Fame(FlareFile f)
        {
            return f.Tier >= FlareTier.Celebrity ? FameFee : 0;
        }

        /// <summary>Puts the post numbers of the player's newest posts with a picture into <paramref name="into"/>, newest first.</summary>
        public static void Pictures(FlareFile f, List<int> into)
        {
            into.Clear();
            for (int i = f.Posts.Count - 1; i >= 0 && into.Count < Thumbs; i--)
            {
                FlarePost p = f.Posts[i];
                if (p.Author == FlareAuthor.Player && (p.Source == FlareSource.Photo || p.Source == FlareSource.Selfie) && !string.IsNullOrEmpty(p.Name))
                {
                    into.Add(p.Id);
                }
            }
        }

        public static int Count(int bits)
        {
            int n = 0;
            for (; bits != 0; bits &= bits - 1)
            {
                n++;
            }
            return n;
        }
    }
}
