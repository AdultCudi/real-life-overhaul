using System;
using Helpers;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A seated outing's time skip rests the player as the game's own seat in a cinema does (its RestActivity): no
    /// walking drain, the bench's recovery at the default multiplier. Given back when the skip ends and on every way out.
    /// </summary>
    public sealed partial class OutingScene
    {
        private bool _resting;

        private void BeginRestEffects()
        {
            if (!_def.SeatedRest)
            {
                return;
            }
            _resting = true;   // claimed first, so a fault below is still given back
            EnergyHelper.RemoveEnergySpender("move");
            EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.Bench);
            EnergyHelper.energyRegenMultiplier = EnergyHelper.DefaultEnergyRegenMultiplier;
        }

        /// <summary>Puts the game's energy recovery back as it was. Safe at any time and to repeat.</summary>
        internal void EndRestEffects()
        {
            if (!_resting)
            {
                return;
            }
            _resting = false;
            try
            {
                EnergyHelper.energyRegenMultiplier = EnergyHelper.DefaultEnergyRegenMultiplier;
                EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.None);
            }
            catch (Exception)
            {
                // the player is gone with the scene: there is nothing to put back
            }
        }
    }
}
