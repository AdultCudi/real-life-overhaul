using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ui;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The pause menu's city card (Ui/Pause) with the economy's books: the net worth as the Overview counts it and the
        /// businesses the player runs that take money (Economy/Finance/Books). Read as the menu opens; nothing per frame.
        /// </summary>
        public static class PauseLinks
        {
            private static readonly List<BuildingRegistration> Mine = new List<BuildingRegistration>(16);

            internal static void Connect()
            {
                PauseKits.NetWorth = NetWorth;
                PauseKits.Businesses = Businesses;
            }

            internal static void Disconnect()
            {
                PauseKits.NetWorth = null;
                PauseKits.Businesses = null;
            }

            private static long NetWorth()
            {
                try
                {
                    return Books.NetWorth();
                }
                catch (Exception e)
                {
                    Fault("PauseLinks.NetWorth", e);
                    return 0L;
                }
            }

            private static int Businesses()
            {
                try
                {
                    Books.Businesses(Mine);
                    return Mine.Count;
                }
                catch (Exception e)
                {
                    Fault("PauseLinks.Businesses", e);
                    return 0;
                }
                finally
                {
                    Mine.Clear();
                }
            }
        }
    }
}
