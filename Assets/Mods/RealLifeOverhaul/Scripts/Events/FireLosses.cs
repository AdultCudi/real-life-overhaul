using System;

namespace RealLifeOverhaul.Events
{
    /// <summary>Tells other features (business insurance) once that a fire's repair bill was charged; a listener that throws stops nothing.</summary>
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
