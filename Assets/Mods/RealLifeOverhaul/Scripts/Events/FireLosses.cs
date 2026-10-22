using System;

namespace RealLifeOverhaul.Events
{
    /// <summary>
    /// Tells other features (business insurance, through Wiring/CompanyLinks) once that a fire's repair bill was charged to
    /// one of the player's businesses. Listeners cannot change the loss, and one that throws stops neither the others nor
    /// the fire.
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
