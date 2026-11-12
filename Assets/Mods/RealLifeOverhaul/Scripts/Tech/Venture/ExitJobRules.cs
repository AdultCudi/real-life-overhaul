using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>How an acqui-hire's job lead came out. Saved as a number.</summary>
    public static class JobKind
    {
        public const int None = 0, AtBuyer = 1, PutForward = 2;
    }

    /// <summary>The lead one acqui-hire gave the player: the sale (its offer and day), the buyer, and how it came out. Game-free.</summary>
    public sealed class JobLead
    {
        public int Offer, Day, Kind, Opening;
        public string Buyer = string.Empty;
    }

    /// <summary>What the acqui-hires' job leads keep in a save (<see cref="ExitJobStore"/>). A save from before has none, so a past acqui-hire gives nothing.</summary>
    public sealed class ExitJobFile
    {
        public const int Most = 40;
        public readonly List<JobLead> Leads = new List<JobLead>(4);

        public JobLead Of(int offer, int day)
        {
            for (int i = 0; i < Leads.Count; i++)
            {
                if (Leads[i].Offer == offer && Leads[i].Day == day)
                {
                    return Leads[i];
                }
            }
            return null;
        }
    }

    /// <summary>The rules of the job an acqui-hire opens. Game-free; the vacancy itself is Careers' (CareersDirector.ExitLead).</summary>
    public static class ExitJobRules
    {
        /// <summary>How long a lead stands: the life of any lead (ProRules.OpeningDaysMost).</summary>
        public static int Life => Careers.ProRules.OpeningDaysMost;

        /// <summary>
        /// The sale entered before the lead is asked for, so asking again can never happen. Null when this sale already has its entry, or the buyer is nobody:
        /// one lead for one acqui-hire.
        /// </summary>
        public static JobLead Begin(ExitJobFile f, int offer, int day, string buyer)
        {
            if (f == null || string.IsNullOrEmpty(buyer) || f.Of(offer, day) != null)
            {
                return null;
            }
            var l = new JobLead { Offer = offer, Day = day, Kind = JobKind.None, Buyer = buyer };
            f.Leads.Add(l);
            if (f.Leads.Count > ExitJobFile.Most)
            {
                f.Leads.RemoveAt(0);
            }
            return l;
        }

        /// <summary>True while an earlier acqui-hire's lead is still open: firms chained one after another farm no more than the one post at a time.</summary>
        public static bool StillOpen(ExitJobFile f, JobLead except, int day)
        {
            for (int i = 0; f != null && i < f.Leads.Count; i++)
            {
                JobLead l = f.Leads[i];
                if (!ReferenceEquals(l, except) && l.Kind != JobKind.None && day <= l.Day + Life)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether the buyer of a sale could have the post at its own office: a startup of the city with a floor of a tower.</summary>
        public static bool AtOffice(int buyerKind, bool hasFloor) => buyerKind == 1 && hasFloor;
    }
}
