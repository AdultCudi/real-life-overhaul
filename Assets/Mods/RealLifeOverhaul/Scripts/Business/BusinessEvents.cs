using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What business pressure tells whoever outside it wants to know (running a business, through
    /// Wiring/CompanyLinks): that one of its issues has closed at a business, and that one of its inspections has
    /// fined a business. Each issue and each fine is business pressure's own and is not changed by anybody who
    /// listens. Nothing here runs on a timer. A listener that throws is not allowed to stop the others or the issue.
    /// </summary>
    public static class BusinessEvents
    {
        /// <summary>An issue has closed: its kind (BusinessCatalogue), the business's place (BusinessState.PlaceKey) and the locale key of how it ended.</summary>
        public static event Action<string, string, string> Closed;

        /// <summary>An inspection has fined a business: the issue's kind, the business's place and the amount that was paid.</summary>
        public static event Action<string, string, int> Fined;

        internal static void RaiseClosed(string kind, string place, string outcome)
        {
            Action<string, string, string> handlers = Closed;
            if (handlers == null)
            {
                return;
            }
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<string, string, string>)handler)(kind, place, outcome);
                }
                catch (Exception e)
                {
                    BusinessGame.Logger?.Error(e);
                }
            }
        }

        internal static void RaiseFined(string kind, string place, int amount)
        {
            Action<string, string, int> handlers = Fined;
            if (handlers == null || amount <= 0)
            {
                return;
            }
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<string, string, int>)handler)(kind, place, amount);
                }
                catch (Exception e)
                {
                    BusinessGame.Logger?.Error(e);
                }
            }
        }
    }
}
