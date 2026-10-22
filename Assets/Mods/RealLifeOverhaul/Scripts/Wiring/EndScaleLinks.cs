using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Tech;
using RealLifeOverhaul.Utilities;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The end game at scale's two tests (EndScaleTests) with the ventures they scale: the data centres, the broadband
        /// company and the player's bank. Called on a button press only.
        /// </summary>
        public static class EndScaleLinks
        {
            internal static void Connect()
            {
                EndScaleTests.Infra = full => TechDirector.Instance != null && TechDirector.Instance.TestScale(full);
                EndScaleTests.Broadband = full => UtilitiesDirector.Instance != null && UtilitiesDirector.Instance.Company != null && UtilitiesDirector.Instance.Company.TestScale(full);
                EndScaleTests.Bank = full => OwnBankDirector.Instance != null && OwnBankDirector.Instance.TestScale(full);
            }

            internal static void Disconnect()
            {
                EndScaleTests.Infra = null;
                EndScaleTests.Broadband = null;
                EndScaleTests.Bank = null;
            }
        }
    }
}
