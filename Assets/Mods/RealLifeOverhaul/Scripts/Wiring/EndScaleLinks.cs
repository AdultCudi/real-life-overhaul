using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Tech;
using RealLifeOverhaul.Utilities;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The end game at scale (Core/EndScale) with the ventures it scales. Each venture reads the option and keeps the
        /// day its scale began itself; only the two tests on the options page reach them all, through these bridges
        /// (EndScaleTests): the data centres and the cloud (Tech), the broadband company (Utilities) and the bank
        /// (Economy/OwnBank). Called on a button press only.
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
