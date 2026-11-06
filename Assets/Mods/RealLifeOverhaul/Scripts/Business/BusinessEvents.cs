using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What business pressure tells other features (Wiring/CompanyLinks): an issue closed, an inspection fined. A listener
    /// that throws stops neither the others nor the issue.
    /// </summary>
    public static class BusinessEvents
    {
        /// <summary>An issue has closed: its kind, the business's place and the locale key of how it ended.</summary>
        public static event Action<string, string, string> Closed;

        /// <summary>An inspection has fined a business: the kind, the place and the amount paid.</summary>
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
