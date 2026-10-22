using System;

namespace RealLifeOverhaul
{
    /// <summary>What the recorded car sounds ask of the Garage (Wiring/CarSoundLinks); unwired, every car is stock with no turntable.</summary>
    public static class CarSoundBridges
    {
        /// <summary>
        /// What a car's build fits for its sound: the exhaust's loudness (0 to 2) in the low four bits, the turbo's size
        /// (0 to 2) in the next four; on the turntable, what is in the cart.
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
