using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One car body as measured: where its roof, boot deck and nose are, in the vehicle's own space
    /// (metres, origin on the ground under the middle, +Z forwards). Measured from the game's own meshes
    /// by GarageAssets/scripts/extract_fit.py (every triangle of the body, not its bounding box).
    /// </summary>
    public readonly struct CarFit
    {
        public readonly float Rear, Front, HalfWidth, Top;
        public readonly float RoofFrom, RoofTo, RoofHeight, RoofHalfWidth, RoofFootHeight;
        public readonly float DeckHeight, DeckSlope, DeckHalfWidth, DeckPostHeight;
        public readonly float NoseLow, NoseHigh, NoseHalfWidth, BonnetHeight;

        public CarFit(float rear, float front, float halfWidth, float top,
            float roofFrom, float roofTo, float roofHeight, float roofHalfWidth, float roofFootHeight,
            float deckHeight, float deckSlope, float deckHalfWidth, float deckPostHeight,
            float noseLow, float noseHigh, float noseHalfWidth, float bonnetHeight)
        {
            Rear = rear;
            Front = front;
            HalfWidth = halfWidth;
            Top = top;
            RoofFrom = roofFrom;
            RoofTo = roofTo;
            RoofHeight = roofHeight;
            RoofHalfWidth = roofHalfWidth;
            RoofFootHeight = roofFootHeight;
            DeckHeight = deckHeight;
            DeckSlope = deckSlope;
            DeckHalfWidth = deckHalfWidth;
            DeckPostHeight = deckPostHeight;
            NoseLow = noseLow;
            NoseHigh = noseHigh;
            NoseHalfWidth = noseHalfWidth;
            BonnetHeight = bonnetHeight;
        }
    }

    /// <summary>
    /// Which fitted parts go on which car, and where. A part is offered only on a body it sits on
    /// cleanly: a wing needs a boot deck that is nearly flat and well below the roof, bars need a roof
    /// long enough to carry them, a bull bar is for SUVs, pickups and vans. A vehicle type that is not
    /// in the table (another mod's car, the Mersaidi Dash whose prefab is built differently) gets none.
    /// </summary>
    public static class GarageFit
    {
        private const string Prefix = "ba:vehicletype_";

        // rear z, front z, half width, top | roof from z, to z, height at the bars, half width, height at the feet |
        // deck height, slope (deg), half width, height at the posts | nose low, nose high, nose half width, bonnet height
        private static readonly Dictionary<string, CarFit> Table = new Dictionary<string, CarFit>
        {
            { "anselmoaf90", new CarFit(-2.13f, 2.53f, 1.11f, 1.20f, -0.39f, 0.29f, 1.17f, 0.54f, 1.12f, 0.91f, 1.6f, 0.80f, 0.90f, 0.17f, 0.46f, 0.38f, 0.59f) },
            { "bima320", new CarFit(-2.38f, 2.37f, 1.07f, 1.48f, -1.68f, 0.16f, 1.47f, 0.60f, 1.42f, 1.15f, 32.5f, 0.56f, 1.14f, 0.26f, 0.72f, 0.54f, 0.88f) },
            { "deliverytruck", new CarFit(-3.71f, 3.72f, 1.36f, 3.01f, -3.25f, 2.55f, 3.01f, 1.14f, 3.01f, 0.37f, 82.2f, 1.14f, 0.37f, 0.46f, 1.51f, 1.12f, 1.76f) },
            { "ferdinand112", new CarFit(-2.22f, 2.14f, 1.00f, 1.34f, -0.68f, 0.24f, 1.34f, 0.50f, 1.28f, 1.03f, 13.0f, 0.64f, 1.03f, 0.16f, 0.53f, 0.68f, 0.79f) },
            { "freighttruckt1", new CarFit(-5.49f, 5.49f, 1.44f, 4.00f, -5.15f, 2.49f, 3.99f, 1.42f, 3.99f, 1.31f, 82.4f, 1.28f, 1.31f, 0.51f, 1.46f, 0.78f, 1.61f) },
            { "honzamimic", new CarFit(-2.28f, 2.31f, 0.99f, 1.41f, -1.14f, 0.22f, 1.41f, 0.56f, 1.37f, 1.05f, 18.3f, 0.68f, 1.05f, 0.23f, 0.54f, 0.56f, 0.77f) },
            { "limo", new CarFit(-3.27f, 2.77f, 1.10f, 1.54f, -1.69f, 0.31f, 1.53f, 0.66f, 1.50f, 1.12f, 24.5f, 0.64f, 1.11f, 0.22f, 0.63f, 0.58f, 0.88f) },
            { "mersaidimgagt", new CarFit(-2.26f, 2.26f, 1.03f, 1.28f, -1.04f, -0.12f, 1.26f, 0.56f, 1.21f, 0.90f, 19.1f, 0.58f, 0.89f, 0.10f, 0.64f, 0.52f, 0.77f) },
            { "mersaidis500", new CarFit(-2.59f, 2.59f, 1.07f, 1.50f, -1.17f, 0.27f, 1.49f, 0.62f, 1.45f, 1.07f, 20.1f, 0.62f, 1.05f, 0.24f, 0.74f, 0.62f, 0.89f) },
            { "missamvillian", new CarFit(-2.31f, 2.47f, 1.01f, 1.45f, -1.37f, 0.19f, 1.42f, 0.54f, 1.40f, 1.08f, 33.2f, 0.54f, 1.07f, 0.21f, 0.62f, 0.52f, 0.87f) },
            { "petrollsfanton", new CarFit(-2.88f, 2.88f, 1.08f, 1.66f, -1.26f, 0.50f, 1.66f, 0.48f, 1.60f, 1.07f, 16.2f, 0.56f, 1.05f, 0.25f, 1.00f, 0.56f, 1.11f) },
            { "umcdesert", new CarFit(-2.77f, 2.76f, 1.18f, 2.03f, -2.47f, 1.09f, 2.03f, 0.74f, 1.97f, 1.93f, 62.3f, 0.74f, 1.92f, 0.38f, 0.64f, 0.60f, 1.24f) },
            { "umcdesertdeliveryjob", new CarFit(-2.77f, 2.76f, 1.18f, 2.03f, -2.47f, 1.09f, 2.03f, 0.74f, 1.97f, 1.93f, 62.3f, 0.74f, 1.92f, 0.38f, 0.64f, 0.60f, 1.24f) },
            { "umcnunavut", new CarFit(-2.61f, 2.61f, 1.14f, 1.88f, -2.31f, 0.45f, 1.88f, 0.70f, 1.88f, 1.82f, 72.8f, 0.60f, 1.81f, 0.35f, 0.79f, 0.60f, 1.30f) },
            { "vordpony", new CarFit(-2.37f, 2.41f, 0.92f, 1.38f, -0.87f, 0.21f, 1.38f, 0.56f, 1.32f, 0.98f, 12.4f, 0.62f, 0.98f, 0.52f, 0.82f, 0.56f, 0.90f) },
            { "vordtiaravic", new CarFit(-2.52f, 2.38f, 0.97f, 1.34f, -1.14f, 0.22f, 1.34f, 0.54f, 1.30f, 0.92f, 41.3f, 0.64f, 0.91f, 0.26f, 0.49f, 0.64f, 0.79f) },
            { "vordv150", new CarFit(-2.72f, 2.41f, 1.13f, 1.77f, -0.98f, 0.58f, 1.77f, 0.66f, 1.74f, 0.73f, 56.5f, 0.88f, 0.72f, 0.49f, 1.03f, 0.54f, 1.23f) }
        };

        public const float MaxDeckSlope = 25f;     // degrees: steeper than this is a rear window or a rounded tail, not a deck
        public const float MinDeckBelowRoof = 0.25f;
        public const float MinRoofLength = 0.90f;

        public static bool TryGet(string vehicleTypeName, out CarFit fit)
        {
            string name = vehicleTypeName ?? string.Empty;
            if (name.StartsWith(Prefix, System.StringComparison.Ordinal))
            {
                name = name.Substring(Prefix.Length);
            }
            return Table.TryGetValue(name, out fit);
        }

        /// <summary>A wing on the boot: cars with a real, nearly flat boot deck.</summary>
        public static bool Spoiler(string vehicleTypeName, CarKind kind)
        {
            return (kind == CarKind.Economy || kind == CarKind.Saloon || kind == CarKind.Luxury || kind == CarKind.Sports)
                   && TryGet(vehicleTypeName, out CarFit fit)
                   && fit.DeckSlope <= MaxDeckSlope && fit.Top - fit.DeckHeight >= MinDeckBelowRoof && fit.DeckHalfWidth >= 0.45f;
        }

        /// <summary>Roof bars: anything with a roof long enough, except sports cars and trucks.</summary>
        public static bool Rack(string vehicleTypeName, CarKind kind)
        {
            return kind != CarKind.Sports && kind != CarKind.Truck && kind != CarKind.Untouched
                   && TryGet(vehicleTypeName, out CarFit fit)
                   && fit.RoofTo - fit.RoofFrom >= MinRoofLength && fit.RoofHalfWidth >= 0.40f;
        }

        /// <summary>A bull bar: SUVs, pickups and vans with an upright nose to carry it.</summary>
        public static bool Bar(string vehicleTypeName, CarKind kind)
        {
            return (kind == CarKind.Utility || kind == CarKind.Van)
                   && TryGet(vehicleTypeName, out CarFit fit)
                   && BarTop(fit) - (fit.NoseLow + 0.05f) >= 0.35f && fit.NoseHalfWidth >= 0.45f;
        }

        /// <summary>
        /// How high a bull bar stands on this nose: to just under the bonnet's edge, but no more than 0.22 m above
        /// the upright part of the nose (on a van the front leans back above that, and a taller bar would stand
        /// out in front of nothing), and never more than 0.75 m tall.
        /// </summary>
        public static float BarTop(in CarFit fit)
        {
            float top = fit.BonnetHeight - 0.05f;
            float nose = fit.NoseHigh + 0.22f;
            float tall = fit.NoseLow + 0.05f + 0.75f;
            top = top < nose ? top : nose;
            return top < tall ? top : tall;
        }
    }
}
