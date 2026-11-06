using System;

namespace RealLifeOverhaul
{
    /// <summary>A break-in at the player's business, told once to each listener (insurance, districts); one that throws stops no other.</summary>
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
                    Crime.Fault(e);
                }
            }
        }
    }
}
