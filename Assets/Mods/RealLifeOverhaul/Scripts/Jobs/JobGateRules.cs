namespace RealLifeOverhaul
{
    /// <summary>What a shift does about a business's ticket gates (the cinema's and the theatre's), game-free so EconomyCheck runs the rule the director runs.</summary>
    public static class JobGateRules
    {
        /// <summary>What to do with a gate this tick.</summary>
        public enum Act
        {
            Leave,      // nothing: it is the game's to decide
            Open,       // switch the gate's collider off: the worker walks through
            GiveBack    // the shift is over or the worker is out: the game decides again
        }

        /// <summary>
        /// A gate stays shut for whoever holds no ticket, but a worker on a client's floor is there for the floor, and the mop in their
        /// hands cannot be put away to buy one. While the shift works inside the building the gate is open, and the game's own state
        /// is asked for again when it stops.
        /// </summary>
        /// <param name="worksHere">The shift is on and works inside the building the player is in.</param>
        /// <param name="gateShut">The gate's collider is on now.</param>
        /// <param name="held">The shift has switched gates off and not yet given them back.</param>
        public static Act Decide(bool worksHere, bool gateShut, bool held)
        {
            if (worksHere)
            {
                return gateShut ? Act.Open : Act.Leave;
            }
            return held ? Act.GiveBack : Act.Leave;
        }
    }
}
