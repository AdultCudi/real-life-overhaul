using System;

namespace RealLifeOverhaul.Flare
{
    /// <summary>Flare's part for a scandal in the player's private life (Social/Love): some followers lose interest and a scandal draws others.</summary>
    public sealed partial class FlareDirector
    {
        /// <summary>
        /// Followers lost and gained, in per mille of the followers there are (at least one gained where any are lost, since a scandal draws
        /// watchers). Returns the numbers of people (lost, gained); null while Flare is not running. COST: a sum, on the day it happens.
        /// </summary>
        public int[] Gossip(int lostPerMille, int gainedPerMille)
        {
            if (!Running || _file == null)
            {
                return null;
            }
            try
            {
                long now = _file.Followers;
                int lost = (int)Math.Min(now, now * Math.Max(0, lostPerMille) / 1000);
                int gained = (int)(now * Math.Max(0, gainedPerMille) / 1000);
                if (gainedPerMille > 0 && gained == 0)
                {
                    gained = 1;
                }
                _file.Followers = Math.Max(0, (int)(now - lost + gained));
                Changed();
                Logger?.Info("Flare: talk about the player's private life: " + lost + " followers lost, " + gained + " gained.");
                return new[] { lost, gained };
            }
            catch (Exception e)
            {
                Fault("a scandal", e);
                return null;
            }
        }
    }
}
