using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A break-in at one of the player's businesses, for whoever outside street crime wants to know (business
    /// insurance, through Wiring/CompanyLinks). The loss itself is the break-in's own and is not changed by anybody
    /// who listens: this only says, once, where it was, what the stock taken was worth and what the repair cost.
    /// Nothing here runs on a timer. A listener that throws is not allowed to stop the others or the break-in.
    /// </summary>
    public static class BreakIns
    {
        /// <summary>A break-in has happened: the business, the value of the stock taken and the repair bill.</summary>
        public static event Action<BuildingRegistration, int, int> Happened;

        internal static void Raise(BuildingRegistration business, int stock, int repair)
        {
            Action<BuildingRegistration, int, int> handlers = Happened;
            if (handlers == null || business == null)
            {
                return;
            }
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<BuildingRegistration, int, int>)handler)(business, stock, repair);
                }
                catch (Exception e)
                {
                    Crime.Logger?.Error(e);
                }
            }
        }
    }
}
