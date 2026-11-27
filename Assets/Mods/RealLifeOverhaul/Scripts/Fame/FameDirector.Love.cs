using System;

namespace RealLifeOverhaul.Fame
{
    /// <summary>Standing's part for a scandal in the player's private life (Social/Love): how known they are, and a small dent that fades by itself.</summary>
    public sealed partial class FameDirector
    {
        /// <summary>How known the player is, 0 to 100 (their recognition); -1 while Standing is not running.</summary>
        public int Recognition => Running && _file != null ? _file.Recognition : -1;

        /// <summary>
        /// Customers think a little less of the player for a while: the part falls by <paramref name="customers"/> points, and drifts back to
        /// where the player's doing puts it (FameRules.Drift), so the dent fades by itself. Integrity falls by <paramref name="integrity"/>.
        /// </summary>
        public void PrivateScandal(int customers, int integrity)
        {
            if (!Running || _file == null || Save == null)
            {
                return;
            }
            try
            {
                int day = Save.Day;
                if (customers > 0)
                {
                    FameRules.Move(_file, FamePart.Customers, FameCause.Scandal, -customers, day);
                }
                if (integrity > 0)
                {
                    FameRules.Move(_file, FamePart.Integrity, FameCause.Scandal, -integrity, day);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
