using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What competition in niches asks of the rest of the mod, filled in by Wiring/Wiring.BakeLinks: the bake off's desk session put into the sessions' registry while the venture
    /// part runs. Optional, and a fault in the sessions reads as no session (the firm's page still enters a bake off). Game-free.
    /// </summary>
    public static class RivalryBridges
    {
        public static Action<bool> Sessions;

        public static void SessionsNow(bool on)
        {
            try
            {
                Sessions?.Invoke(on);
            }
            catch (Exception)
            {
                // the sessions' fault: a bake off is entered from the firm's page
            }
        }

        public static void Reset()
        {
            Sessions = null;
        }
    }
}
