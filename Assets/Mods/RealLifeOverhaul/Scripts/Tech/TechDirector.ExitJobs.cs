using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The job an acqui-hire opens. The player whose failing firm is bought for its team gets a lead from the buyer through Careers' own openings (ExitBridges.Job),
    /// once for each sale, saved in a line of its own (ExitJobStore). A city startup bought for its team lets its founder go to the buyer in the population. COST: on
    /// an accepted sale and on a startup's end, never otherwise.
    /// </summary>
    public sealed partial class TechDirector
    {
        private ExitJobFile _exitJobs;

        private void LoadExitJobs()
        {
            _exitJobs = ExitJobStore.Load(Save.modData, out _) ?? new ExitJobFile();
        }

        private void WriteExitJobs(Dictionary<string, string> data)
        {
            string line = _exitJobs != null ? ExitJobStore.Write(_exitJobs) : null;
            if (line != null)
            {
                data[ExitJobStore.Key] = line;
            }
            else
            {
                data.Remove(ExitJobStore.Key);
            }
        }

        /// <summary>The sale of a firm for its team went through: the player is offered a post, once. Nothing the player has is touched.</summary>
        private void ExitJobFor(ExitOffer o, int day)
        {
            try
            {
                JobLead l = ExitJobRules.Begin(_exitJobs, o.Id, day, o.Name);
                if (l == null)
                {
                    return;
                }
                if (ExitJobRules.StillOpen(_exitJobs, l, day))
                {
                    ExitJobNone(o.Name);
                    return;
                }
                if (!GiveLead(o.Name, ExitJobRules.AtOffice(o.Buyer, true), o.Id, l))
                {
                    ExitJobNone(o.Name);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The lead through Careers; Careers tells the player of it. False with no vacancy to be had.
        private bool GiveLead(string buyer, bool office, int salt, JobLead l)
        {
            if (ExitBridges.Job == null)
            {
                return false;
            }
            if (!ExitBridges.Job(buyer, office, salt & 7, out bool atBuyer, out string title, out string employer, out int opening, out string why))
            {
                Logger?.Info("Tech: no job lead from " + buyer + " (" + why + ").");
                return false;
            }
            if (l != null)
            {
                l.Kind = atBuyer ? JobKind.AtBuyer : JobKind.PutForward;
                l.Opening = opening;
            }
            Logger?.Info("Tech: " + buyer + " put the player forward for " + title + " at " + employer + (atBuyer ? " (at the buyer)" : " (a vacancy of the city's)") + ".");
            return true;
        }

        private void ExitJobNone(string buyer)
        {
            Notifications.Show(NotificationType.Info, "reallife_exit_job_none", Data("buyer", buyer), 10f, null, null, true, false);
        }

        // ---------------------------------------------------------------- the city's startups

        /// <summary>The founder of a city startup bought for its team goes to the buyer: a startup of the city with a floor (its tower's firms), or a listed company (a
        /// vacancy of their field), seeded. The population's record only. True once their employer record moved.</summary>
        private bool FounderTaken(Startup s, int day)
        {
            if (_exits == null || ExitBridges.FounderHired == null)
            {
                return false;
            }
            var buyers = new List<Startup>(4);
            for (int i = 0; i < _venture.Startups.Count; i++)
            {
                Startup b = _venture.Startups[i];
                if (b.Id != s.Id && b.Alive && b.Cash > 0)
                {
                    buyers.Add(b);
                }
            }
            string street = null;
            int number = 0;
            string name = null;
            if (buyers.Count > 0 && TechRandom.Mille(_exits.Seed, s.Id, day, 78) < 700)
            {
                Startup b = buyers[TechRandom.Range(_exits.Seed, s.Id, day, 79, 0, buyers.Count - 1)];
                name = b.Name;
                TechSites sites = TechSites.Instance;
                if (sites != null)
                {
                    sites.HeadquartersOf(b.Name, out street, out number);
                }
            }
            bool moved = false;
            try
            {
                moved = ExitBridges.FounderHired(s.Id, street, number);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Logger?.Info("Tech: the founder of " + s.Name + " " + (moved ? "was taken on by " + (name ?? "a company of the city") : "stayed as they were (no vacancy)") + " on day " + day + ".");
            return moved;
        }
    }
}
