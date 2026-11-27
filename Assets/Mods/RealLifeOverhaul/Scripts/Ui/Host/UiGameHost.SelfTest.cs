using System.Text;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The self-test's view of the hosts that draw the kit over the game's screens (Diagnostics/SelfTest): which of them are
    /// the game's own after a fault of the kit's. Read only by a running plan. COST: nothing unless a plan asks.
    /// </summary>
    public sealed partial class UiGameHost
    {
        /// <summary>Names of the hosts whose screen is the game's own for the rest of the session after a fault of the kit's; empty when none.</summary>
        internal static string FellBack()
        {
            StringBuilder sb = null;
            for (int i = 0; i < Hosts.Count; i++)
            {
                if (Hosts[i]._gaveUp)
                {
                    sb = (sb ?? new StringBuilder(32)).Append(sb != null ? ", " : string.Empty).Append(Hosts[i]._screen.Id);
                }
            }
            return sb != null ? sb.ToString() : string.Empty;
        }
    }
}
