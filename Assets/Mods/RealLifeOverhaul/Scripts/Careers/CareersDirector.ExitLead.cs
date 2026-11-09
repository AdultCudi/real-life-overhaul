using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The lead an acqui-hire gives (Tech/Venture/ExitJobRules): a post at the buyer's own office when the buyer is a startup of the city with a floor of a tower and the
    /// player is qualified for a tech post, else a real vacancy of the city in a profession the player is qualified for with the buyer's people putting their name
    /// forward. The opening and application path is the ordinary one; nothing here starts a job. Made on the call, never on a frame.
    /// </summary>
    public sealed partial class CareersDirector
    {
        private const int ExitDraws = 40;   // the salt of an acqui-hire's lead, clear of the networks' (small numbers)

        private static bool AtFirm(Profession p) => p != null && !p.Public && p.Family >= 0 && p.Types.Length > 0 && p.Types[0] == ProCatalogue.TechTypes;

        /// <summary>The lead for an acqui-hire by <paramref name="buyer"/>; null, and why, when no vacancy can be had. <paramref name="atBuyer"/> is true for a post at the buyer.</summary>
        public Opening ExitLead(string buyer, bool office, int salt, out bool atBuyer, out string why)
        {
            atBuyer = false;
            why = null;
            Opening o = office ? Lead(buyer, ExitDraws + salt, buyer, "reallife_exit_job_note_at", out why) : null;
            if (o != null)
            {
                atBuyer = true;
                return o;
            }
            return Lead(buyer, ExitDraws + salt, null, "reallife_exit_job_note_forward", out why);
        }
    }
}
