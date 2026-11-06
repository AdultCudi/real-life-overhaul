using UI.Notification;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>
    /// An investor met through somebody the player knows (Network's investor meeting, through Wiring/NetworkLinks): one of
    /// the save's investors who will see the player today, whatever their diary or a recent pitch said. The pitch itself is
    /// the Investors tab's own, and nothing else about the investor changes. Made on the call that asks, never on a frame.
    /// </summary>
    public sealed partial class VenturesDirector
    {
        /// <summary>
        /// The investor picked by <paramref name="salt"/> (the next one along when that one is a tycoon and the player has
        /// no business open, as a tycoon backs none), open to a pitch from today, with a notification that opens the
        /// Investors tab. Null, and why, when Ventures is not running or has no investor to name.
        /// </summary>
        public Investor Introduced(int salt, string by, out string why)
        {
            why = "reallife_social_net_why_no_investor";
            if (!Running || _file == null || _file.Investors.Count == 0 || Save == null)
            {
                return null;
            }
            int count = _file.Investors.Count;
            int at = (salt & int.MaxValue) % count;
            bool open = BestBusiness(out _) != null;
            Investor investor = null;
            for (int n = 0; n < count && investor == null; n++)
            {
                Investor candidate = _file.Investors[(at + n) % count];
                if (candidate != null && (open || candidate.Kind != InvestorKind.Tycoon))
                {
                    investor = candidate;
                }
            }
            if (investor == null)
            {
                return null;
            }
            int day = Save.Day;
            investor.ClosedUntil = System.Math.Min(investor.ClosedUntil, day);
            investor.PitchedDay = 0;   // (an introduction is a fresh start: the cooldown of an old pitch goes)
            why = null;
            Detail("an introduction from " + by + " to " + investor.Name + ", open to a pitch from day " + day);
            Tell(NotificationType.Success, "reallife_ven_note_introduced", _openInvestors, "name", investor.Name, "by", by ?? string.Empty);
            return investor;
        }
    }
}
