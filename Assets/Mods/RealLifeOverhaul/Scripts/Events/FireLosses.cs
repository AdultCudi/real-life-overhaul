using System;

namespace RealLifeOverhaul.Events
{
    /// <summary>
    /// What a fire has cost one of the player's businesses, for whoever outside the fires wants to know (business
    /// insurance, through Wiring/CompanyLinks). The loss itself is FireDamage's and is not changed by anybody who
    /// listens: this only says, once, that a repair bill has just been charged, at which business and for how much.
    /// Nothing here runs on a timer. A listener that throws is not allowed to stop the others or the fire.
    /// </summary>
    public static class FireLosses
    {
        /// <summary>A repair bill was charged after a fire: the business and the amount.</summary>
        public static event Action<BuildingRegistration, int> Billed;

        internal static void Raise(BuildingRegistration business, int bill)
        {
            Action<BuildingRegistration, int> handlers = Billed;
            if (handlers == null || business == null || bill <= 0)
            {
                return;
            }
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<BuildingRegistration, int>)handler)(business, bill);
                }
                catch (Exception e)
                {
                    EventDirector.Logger?.Error(e);
                }
            }
        }
    }
}
