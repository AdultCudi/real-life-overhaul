using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What the game studio does to the game beyond the firms' own bookings (FirmsGame): its money's keys, a publisher's
    /// advance and crowdfunding as capital at the studio, and the way to the studio's desk. Everything else of the studio
    /// goes through FirmsGame and the director's notices.
    /// </summary>
    internal static class StudioGame
    {
        public const string EngineKey = "reallife_transaction_tech_game_engine";               // a licensed engine's fee for a game
        public const string CertificationKey = "reallife_transaction_tech_game_certification"; // a console's certification
        public const string ContractorsKey = "reallife_transaction_tech_game_contractors";     // contractors a milestone added
        public const string BonusKey = "reallife_transaction_tech_game_bonus";                 // a release's bonus pool
        public const string RoyaltyKey = "reallife_transaction_tech_game_royalty";             // the leads' royalty
        public const string AdvanceKey = "reallife_transaction_tech_game_advance";             // a publisher's advance against royalties
        public const string CrowdfundKey = "reallife_transaction_tech_game_crowdfund";         // crowdfunding from the studio's followers

        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>A publisher's advance, cents at today's prices: money in at the studio, kept out of its takings (it is earned back from royalties).</summary>
        public static void Advance(long cents, BuildingRegistration at, string name) => Capital(cents, AdvanceKey, at, name);

        /// <summary>Crowdfunding pledged, cents at today's prices: money in at the studio, not its sales.</summary>
        public static void Crowdfund(long cents, BuildingRegistration at, string name) => Capital(cents, CrowdfundKey, at, name);

        private static void Capital(long cents, string key, BuildingRegistration at, string name)
        {
            if (cents <= 0 || Save == null || at == null)
            {
                return;
            }
            var info = new TransactionInfo(key, new Dictionary<string, string>(1) { { "name", name ?? string.Empty } });
            RealLifeOverhaul.Economy.BizBooks.Book(cents / 100f, info, null, at.Address, true);
        }

        /// <summary>The game's destination marker on a studio, for its desk; false where it has none.</summary>
        public static bool Route(string place)
        {
            return FirmsGame.Where(place, out string street, out int number, out _) && FirmsGame.Destination(street, number);
        }

        /// <summary>A notice of the game's that says where the desk is.</summary>
        public static void TellDesk(string key, Dictionary<string, string> data)
        {
            Notifications.Show(NotificationType.Info, key, data, 10f);
        }
    }
}
