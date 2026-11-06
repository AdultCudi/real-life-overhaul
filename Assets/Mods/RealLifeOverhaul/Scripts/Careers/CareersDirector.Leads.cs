using System.Collections.Generic;
using RealLifeOverhaul.Tech;
using UI.Notification;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// A job lead from somebody the player knows (Network, through Wiring/NetworkLinks): a vacancy of the city's in a
    /// profession the player is qualified for now, at the best rung within reach, with their name put forward (a referral:
    /// <see cref="ProRules.HireScore"/> counts it at the screening and the interview). Made on the call that asks, never on
    /// a frame.
    /// </summary>
    public sealed partial class CareersDirector
    {
        private const int LeadDraws = 600;   // draw offset of a lead's vacancy, clear of the day's (under a hundred), the reach's (500) and the search firm's (900)
        private const int LeadTries = 12;    // employers drawn for the profession before the lead is given up

        private readonly List<int> _leadProfs = new List<int>(ProCatalogue.Count);
        private readonly List<int> _leadRanks = new List<int>(ProCatalogue.Count);

        /// <summary>
        /// A vacancy passed on by <paramref name="from"/>, open for the longest a vacancy is, listed in the Career app with a
        /// notification that opens it there. Null, and why, when none can be made: the working life off, no profession the
        /// player is qualified for, or no employer of it in the city.
        /// </summary>
        public Opening Lead(string from, int salt, out string why)
        {
            why = "reallife_social_net_why_no_lead";
            WorkFile w = Work;
            if (w == null || Save == null || w.Retired > 0 || w.Openings.Count >= WorkFile.OpeningsMost)
            {
                return null;
            }
            int day = Save.Day;
            _leadProfs.Clear();
            _leadRanks.Clear();
            for (int i = 0; i < ProCatalogue.All.Length; i++)
            {
                Profession p = ProCatalogue.All[i];
                int r = ProRules.BestRank(_study, p, ProRules.Experience(w, p.Id, day, Year));
                if (r >= 0 && (w.Job == null || w.Job.Post.Prof != p.Id || r > w.Job.Post.Rank))
                {
                    _leadProfs.Add(p.Id);
                    _leadRanks.Add(r);
                }
            }
            if (_leadProfs.Count == 0)
            {
                return null;
            }
            ReadEmployers(day);
            int pick = TechRandom.Range(StudySeed, 8320, day, salt, 0, _leadProfs.Count - 1);
            Opening o = null;
            for (int n = 0; n < LeadTries && o == null; n++)
            {
                int at = (pick + n) % _leadProfs.Count;   // the next profession when one has no employer here
                o = NewVacancy(day, LeadDraws + salt * LeadTries + n, false, false, _leadProfs[at], _leadRanks[at]);
            }
            if (o == null)
            {
                return null;
            }
            o.Via = (int)Via.Referral;
            o.To = day + ProRules.OpeningDaysMost;
            o.Lead = from ?? string.Empty;
            w.Openings.Add(o);
            why = null;
            Changed();
            Logger?.Info("Careers: a job lead from " + from + ": " + CareersText.Title(o.Prof, o.Rank) + " at " + o.Employer + ", open to day " + o.To + ".");
            Tell2(NotificationType.Success, "reallife_career_note_lead",
                Data("name", from, "title", CareersText.Title(o.Prof, o.Rank), "employer", CareersText.Employer(o.Employer)), TabJobs, CareerApp.OpeningRow + o.Id);
            return o;
        }
    }
}
