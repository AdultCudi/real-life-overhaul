using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the player has that some life events are about, asked of the feature that keeps it (set by the wiring, null
    /// when absent), so a card's late fee never reaches somebody with no card. Game-free; no on a fault.
    /// </summary>
    public static class LifeAccounts
    {
        public static Func<bool> Card;          // a credit card of the mod's banks (Economy/Products)
        public static Func<bool> Accountant;    // an accountant hired for the businesses (Economy/Company)
        public static Func<bool> Power;         // a power account (the bills)
        public static Func<bool> Home;          // a residential building rented or owned; a hotel or hostel booking is none

        public static bool HasCard() => Has(Card);
        public static bool HasAccountant() => Has(Accountant);
        public static bool HasPower() => Has(Power);
        public static bool HasHome() => Has(Home);

        private static bool Has(Func<bool> test)
        {
            try
            {
                return test != null && test();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
