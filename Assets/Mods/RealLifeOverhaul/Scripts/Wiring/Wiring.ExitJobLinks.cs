using System;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>The job an acqui-hire opens (Tech/Venture/ExitJobRules): Careers' lead for the player, and the population's employer record for the founder of a city
        /// startup. Asked on an accepted sale or a startup's end, never on a frame.</summary>
        internal static class ExitJobLinks
        {
            internal static void Connect()
            {
                ExitBridges.Job = Job;
                ExitBridges.FounderHired = FounderHired;
            }

            private static bool Job(string buyer, bool office, int salt, out bool atBuyer, out string title, out string employer, out int opening, out string why)
            {
                atBuyer = false;
                title = employer = null;
                opening = 0;
                why = "careers off";
                try
                {
                    CareersDirector careers = CareersDirector.Instance;
                    Opening o = careers != null ? careers.ExitLead(buyer, office, salt, out atBuyer, out why) : null;
                    if (o == null)
                    {
                        return false;
                    }
                    title = CareersText.Title(o.Prof, o.Rank);
                    employer = CareersText.Employer(o.Employer);
                    opening = o.Id;
                    return true;
                }
                catch (Exception e)
                {
                    Fault("ExitJobLinks.Job", e);
                    return false;
                }
            }

            private static bool FounderHired(int startup, string street, int number)
            {
                try
                {
                    PopulationState live = CityPopulation.Live;
                    if (live == null || SaveGameManager.Current == null)
                    {
                        return false;
                    }
                    string key = street != null ? PopulationState.KeyOf(street, number) : null;
                    return Population.PopulationLinks.Employ(live, LinkKind.Founder, ExitBridges.KeyOf(startup), key, SaveGameManager.Current.Day);
                }
                catch (Exception e)
                {
                    Fault("ExitJobLinks.FounderHired", e);
                    return false;
                }
            }
        }
    }
}
