using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A car's fuel use as the game reckons it, as rules with no game types, checked offline (EconomyCheck FixW2Checks). The game
    /// burns an engine's most an hour (a tenth of its power in litres, less the engine's efficiency) at its idle share, and a skipped
    /// bridge five and a half times that at the car's top speed (Vehicles/SkipBridgeHelper.GetFuelConsumption); the item panel's
    /// range and the vehicle purchase's litres per 100 km both come from this one sum, so they agree. Cost: a few multiplications.
    /// </summary>
    public static class FuelUseRules
    {
        public const float SkipFactor = 5.5f;        // the game's: times the idle use, at the car's top speed
        public const float Efficiency = 0.45f;       // the fuel module's own default: a car on sale shows no module of its own
        public const float Idle = 0.1f;              // ...and its idle share
        public const float LitresPer100Gallon = 235.215f;   // litres per 100 km to US miles a gallon: 235.215 over the figure

        /// <summary>Litres a kilometre at the car's top speed: <paramref name="maxPower"/> in the engine's own unit, the game's speed tier in km/h.</summary>
        public static float PerKm(float maxPower, float efficiency, float idle, float multiplier, float topSpeed)
        {
            float perHour = maxPower / 10f * Math.Min(1f, Math.Max(0f, 1f - efficiency));
            return topSpeed > 0f ? perHour * idle * SkipFactor * multiplier / topSpeed : 0f;
        }

        /// <summary>Litres per 100 km of a car on sale, on the fuel module's defaults; 0 when it burns none or its figures are not there.</summary>
        public static float Per100(float maxPower, float topSpeed) => PerKm(maxPower, Efficiency, Idle, 1f, topSpeed) * 100f;

        /// <summary>US miles a gallon for a figure in litres per 100 km; 0 for none.</summary>
        public static float MilesAGallon(float per100) => per100 > 0f ? LitresPer100Gallon / per100 : 0f;
    }
}
