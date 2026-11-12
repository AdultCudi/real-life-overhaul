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

        private void WriteGains(Dictionary<string, string> data) => KeepGains(_gains, data);

        private static void KeepGains(GainFile file, Dictionary<string, string> data)
        {
            if (file == null || data == null)
            {
                return;
            }
            string line = ExitGainStore.Write(file);
            if (line != null)
            {
                data[ExitGainStore.Key] = line;
            }
            else
            {
                data.Remove(ExitGainStore.Key);
            }
        }

        /// <summary>
        /// The player dealt in the whole company's shares on the market, or paid for a larger share of it (MarketBridges.CompanyDealt): a sale notes its gain for the return, a
        /// purchase adds what it cost to the stake's basis. The company is not a firm and has no cap table, so the file is read from the save when the venture part is off, and
        /// written back at once. Runs on the founder's machine at the deal; nothing per frame.
        /// </summary>
        internal void CompanyDealt(string name, long shares, long money, long fee, long held)
        {
            StakeDealt(ExitGains.CompanyPlace, 0L, name, shares, money, fee, held);
        }

        /// <summary>
        /// The same for any stake of the player's that trades on the market and has no cap table: the whole company (<see cref="ExitGains.CompanyPlace"/>) or their bank's own
        /// stock (Markets/Extras), each with a basis line of its own. <paramref name="invested"/> is what the stake cost to found, cents at the start's prices; 0 for the company.
        /// </summary>
        internal void StakeDealt(string place, long invested, string name, long shares, long money, long fee, long held)
        {
            if (Save == null)
            {
                return;
            }
            GainFile file = _gains ?? ExitGainStore.Load(Save.modData) ?? new GainFile();
            long gain = ExitGains.ForMarketDeal(file, invested > 0 ? invested : 0L, place, name, money, fee, held, shares, Save.Day, Prices.Level);
            if (_gains == null)
            {
                KeepGains(file, Save.modData);
            }
            GainNotice(name, gain);
            string what = shares < 0 ? "sold " + (-shares) + " share(s) of" : shares > 0 ? "bought " + shares + " share(s) of" : "paid for a larger share of";
            Logger?.Info("Tech: the player " + what + " their company " + name + " on the market for " + TechText.Cents(ExitGains.CentsOfMarket(money)) + " and "
                         + TechText.Cents(ExitGains.CentsOfMarket(fee)) + " in fees" + (shares < 0 ? GainLog(gain) : "; the stake's basis grows by it") + ".");
        }

        /// <summary>
        /// A bid for the whole company won (MarketBridges.CompanyTaken): the founder's shares were paid <paramref name="money"/>, and the businesses handed over take their stock and
        /// furniture (<paramref name="fixtures"/>, read while they are still the player's) out of the gain in the founder's part of the company, as a firm sold with its business does.
        /// </summary>
        internal void CompanyTaken(string name, long founder, long total, long money, long fixtures)
        {
            if (Save == null)
            {
                return;
            }
            GainFile file = _gains ?? ExitGainStore.Load(Save.modData) ?? new GainFile();
            long gain = ExitGains.ForTakeover(file, name, money, fixtures, founder, total, Save.Day, Prices.Level);
            if (_gains == null)
            {
                KeepGains(file, Save.modData);
            }
            GainNotice(name, gain);
            Logger?.Info("Tech: the player's company " + name + " was taken over, the founder's " + founder + " share(s) paid " + TechText.Cents(ExitGains.CentsOfMarket(money)) + GainLog(gain) + ".");
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
