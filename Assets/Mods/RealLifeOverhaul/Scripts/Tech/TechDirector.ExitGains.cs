using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The capital gain of a startup stake sold, noted for the quarter's return (Tech/Venture/ExitGains). Noted on the day the money reaches the wallet, from what the wallet
    /// really gained, so the firm account's money is never counted twice. Nothing per frame: it runs on a sale.
    /// </summary>
    public sealed partial class TechDirector
    {
        private GainFile _gains;

        private void StartGains() => _gains = ExitGainStore.Load(Save.modData) ?? new GainFile();

        private void StopGains(bool keep)
        {
            if (keep && Save != null)
            {
                WriteGains(Save.modData);
            }
            _gains = null;
        }

        private void WriteGains(Dictionary<string, string> data)
        {
            if (_gains == null || data == null)
            {
                return;
            }
            string line = ExitGainStore.Write(_gains);
            if (line != null)
            {
                data[ExitGainStore.Key] = line;
            }
            else
            {
                data.Remove(ExitGainStore.Key);
            }
        }

        /// <summary>The gains of startup stakes sold between two days, both counted, for the return. Read from the save's own line while Startups are off, so turning them off
        /// does not lift a gain already noted out of the quarter.</summary>
        internal int GainsBetween(int fromDay, int toDay)
        {
            GainFile f = _gains ?? (Save != null ? ExitGainStore.Load(Save.modData) : null);
            return ExitGains.Between(f, fromDay, toDay);
        }

        private void GainNotice(string firm, long gain)
        {
            if (gain > 0)
            {
                Notifications.Show(NotificationType.Info, "reallife_exit_note_gain", Data("firm", firm, "amount", TechText.Cents(gain)), 8f, null, null, true, false);
            }
        }

        private static string GainLog(long gain) => gain > 0 ? "; a capital gain of " + TechText.Cents(gain) + " is noted for the return" : "; no capital gain";
    }
}
