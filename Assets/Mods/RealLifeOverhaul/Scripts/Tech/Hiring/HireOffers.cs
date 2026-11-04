using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Tech
{
    /// <summary>One past job on a CV: a level of the same line of work, for so many years, at a firm.</summary>
    public struct PastJob
    {
        public int Level;    // TalentCatalogue's level the job was at
        public int Years;
        public string Firm;
    }

    /// <summary>
    /// What a rival does to an applicant the player is courting, how many posts an ad fills and what a CV lists. A rival's named offer
    /// to an applicant comes by the rule rivals post their ads by (a rival with an ad in the applicant's family and level), carries a
    /// sum and a deadline, and takes the applicant when the player does not beat it in time; the player who hires first takes the
    /// applicant from the rival. A seeker's past jobs are seeded from the seeker, always lower than or equal to their level and no more
    /// years than the experience their level claims. Game-free (EconomyCheck runs it). Cost: one pass over the applicants a day.
    /// </summary>
    public static class HireOffers
    {
        public const int PostsMost = 5;
        public const int RivalDays = 3;                 // days the player has to beat a rival's offer
        public const int OpenOfferChance = 70;          // per mille a day that a rival with a matching ad makes an offer to a waiting applicant
        public const int OpenOfferBp = 700;             // basis points over the applicant's ask the rival offers
        public const int OldBidLapse = 3;               // days an older save's bid, with no deadline, is given from the day it is first seen
        public const int HistoryMost = 3;

        /// <summary>The posts an ad fills, at least one.</summary>
        public static int Posts(JobAd ad) => ad == null ? 1 : Math.Max(1, Math.Min(PostsMost, ad.Posts));

        /// <summary>The posts of an ad already filled: applicants who accepted or started.</summary>
        public static int Filled(HiringFile f, JobAd ad)
        {
            int n = 0;
            for (int i = 0; i < f.Applicants.Count; i++)
            {
                Applicant a = f.Applicants[i];
                n += a.Ad == ad.Id && (a.Stage == (int)ApplyStage.Accepted || a.Stage == (int)ApplyStage.Started) ? 1 : 0;
            }
            return n;
        }

        /// <summary>The posts an ad still has to fill; 0 once they are all taken.</summary>
        public static int ToFill(HiringFile f, JobAd ad) => Math.Max(0, Posts(ad) - Filled(f, ad));

        /// <summary>True when an ad has every post filled, so it brings no more applicants.</summary>
        public static bool Full(HiringFile f, JobAd ad) => ad != null && !ad.Search && ToFill(f, ad) == 0 && Filled(f, ad) > 0;

        /// <summary>True when the applicant has a rival's offer open at a deadline the day has not passed.</summary>
        public static bool Competing(Applicant a, int day) => a.RivalBid > 0 && a.RivalLapse >= day && a.Live && a.Stage != (int)ApplyStage.Accepted;

        /// <summary>True when the applicant's offer beats the rival's: at least their bid.</summary>
        public static bool Beats(Applicant a, int cents) => a.RivalBid <= 0 || cents >= a.RivalBid;

        /// <summary>The day's turn for rivals' offers: new ones are made, a deadline passed takes the applicant, a hire by the player ends one.</summary>
        public static void Day(HiringFile f, int day, IList<string> rivals)
        {
            for (int i = 0; i < f.Applicants.Count; i++)
            {
                Applicant a = f.Applicants[i];
                if (a.Stage == (int)ApplyStage.Accepted || a.Stage == (int)ApplyStage.Started)
                {
                    a.RivalLapse = 0;   // the player hired first: the rival's offer is gone
                    continue;
                }
                if (a.RivalBid > 0 && a.RivalLapse == 0 && a.Live)
                {
                    a.RivalLapse = day + OldBidLapse;   // a bid from an older save gets a deadline from today, so it can be beaten
                    continue;
                }
                Seeker s = f.SeekerOf(a.Seeker);
                if (s == null || s.Taken)
                {
                    continue;
                }
                if (a.RivalBid > 0 && a.RivalLapse > 0 && day > a.RivalLapse && a.Live)
                {
                    s.Taken = true;   // the rival has them; HiringRules.Day withdraws the application
                    s.Employer = string.IsNullOrEmpty(a.RivalBy) ? s.Employer : a.RivalBy;
                    continue;
                }
                if (a.RivalBid == 0 && a.Live && a.Stage < (int)ApplyStage.Offered && !s.Passive)
                {
                    string by = OfferBy(f, s, rivals, day, a.Id);
                    if (by != null)
                    {
                        a.RivalBid = (int)(s.Ask * (10000L + OpenOfferBp) / 10000);
                        a.RivalBy = by;
                        a.RivalLapse = day + RivalDays;
                    }
                }
            }
        }

        /// <summary>The rival that makes an offer to a seeker today, or null: one with an ad in the seeker's family and level, by the seeded chance.</summary>
        public static string OfferBy(HiringFile f, Seeker s, IList<string> rivals, int day, int salt)
        {
            for (int i = 0; i < f.RivalAds.Count; i++)
            {
                RivalAd ad = f.RivalAds[i];
                if (ad.To > day && ad.Family == (int)s.P.Def.Family && ad.Level == s.P.Level && TechRandom.Mille(f.Seed, day, 7901 + salt, i) < OpenOfferChance)
                {
                    return string.IsNullOrEmpty(ad.Rival) ? HiringCatalogue.RivalFirms[0] : ad.Rival;
                }
            }
            return null;
        }

        // --- a CV's work history -------------------------------------------------------------------------------------------

        /// <summary>
        /// The past jobs a seeker lists, newest first: as many as their level has behind it (none for the first level), each a level
        /// lower than the one before, a few years each, at firms of the sector. Seeded from the seeker's id, so a CV never changes.
        /// </summary>
        public static string PastJobs(int seed, Seeker s)
        {
            int level = s.P.Level;
            var sb = new StringBuilder();
            int count = Math.Min(HistoryMost, level);
            int at = level;
            for (int k = 0; k < count; k++)
            {
                at = Math.Max(0, at - (k == 0 ? 0 : 1));
                int years = TechRandom.Range(seed, 7911 + s.Id, k, 0, 1, 4);
                string firm = HiringCatalogue.RivalFirms[TechRandom.Range(seed, 7912 + s.Id, k, 0, 0, HiringCatalogue.RivalFirms.Length - 1)];
                if (sb.Length > 0)
                {
                    sb.Append('#');
                }
                sb.Append(at).Append(':').Append(years).Append(':').Append(firm);
            }
            return sb.ToString();
        }

        /// <summary>The history as records; each field read, a bad one left out.</summary>
        public static int Read(string history, List<PastJob> into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(history))
            {
                return 0;
            }
            string[] jobs = history.Split('#');
            for (int i = 0; i < jobs.Length && into.Count < HistoryMost; i++)
            {
                string[] f = jobs[i].Split(':');
                if (f.Length >= 3 && int.TryParse(f[0], out int level) && int.TryParse(f[1], out int years) && f[2].Length > 0)
                {
                    into.Add(new PastJob { Level = TalentCatalogue.Clamp(level, 0, TalentCatalogue.Levels - 1), Years = Math.Max(1, Math.Min(40,
                        years)), Firm = f[2] });
                }
            }
            return into.Count;
        }

        /// <summary>The seeker's history, made on first need and kept on the seeker.</summary>
        public static string HistoryOf(int seed, Seeker s)
        {
            if (s.History == null || s.History.Length == 0)
            {
                s.History = s.P.Level > 0 ? PastJobs(seed, s) : "-";
            }
            return s.History == "-" ? string.Empty : s.History;
        }
    }
}
