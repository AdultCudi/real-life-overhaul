using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the recorded car sounds ask of the Garage, filled in by Wiring/CarSoundLinks. With nothing wired every car is
    /// stock and there is no turntable.
    /// </summary>
    public static class CarSoundBridges
    {
        /// <summary>
        /// What a car's build fits that changes its sound: the exhaust's loudness (0 stock, 1 sports, 2 straight-through) in
        /// the low four bits, the turbo's size (0 none, 1 a turbocharger, 2 a big turbo) in the next four; on the turntable,
        /// what is in the cart. Default: stock.
        /// </summary>
        public static Func<VehicleController, int> Parts;

        /// <summary>The car on the custom shop's turntable while the shop is open, else null. Default: none.</summary>
        public static Func<VehicleController> Turntable;

        /// <summary>The exhaust's loudness in <paramref name="parts"/>.</summary>
        public static int ExhaustOf(int parts) => parts & 0xF;

        /// <summary>The turbo's size in <paramref name="parts"/>.</summary>
        public static int TurboOf(int parts) => (parts >> 4) & 0xF;

        /// <summary>The two packed as <see cref="Parts"/> gives them.</summary>
        public static int Pack(int exhaust, int turbo) => (exhaust & 0xF) | ((turbo & 0xF) << 4);
    }
}
