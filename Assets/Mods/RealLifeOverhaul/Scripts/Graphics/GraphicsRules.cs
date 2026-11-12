using System;

namespace RealLifeOverhaul.Gfx
{
    /// <summary>The looks of the options page; Off is the game's own picture.</summary>
    public static class GraphicsLook
    {
        public const int Off = 0, Natural = 1, Filmic = 2, Vivid = 3, Count = 4;
    }

    /// <summary>What the picture may cost: Free adds no work for the graphics card, the other two add shadow passes.</summary>
    public static class GraphicsCost
    {
        public const int Free = 0, Balanced = 1, High = 2, Count = 3;
    }

    /// <summary>Where the player is: the city and the rooms are lit differently, so each has its own settings.</summary>
    public static class GraphicsPlace
    {
        public const int City = 0, Indoors = 1;
    }

    /// <summary>
    /// One setting the looks may write: a slider of one of the render pipeline's volume settings, or a part of the sun's
    /// or the moon's light. <see cref="GraphicsRules.Knobs"/> says which slider each is and what it costs.
    /// </summary>
    public enum GraphicsKnob
    {
        ToneCurve, ToeStrength, ToeLength, ShoulderStrength, ShoulderLength, ShoulderAngle,
        Compensation,
        Temperature,
        MixRedRed, MixRedGreen, MixRedBlue, MixGreenRed, MixGreenGreen, MixGreenBlue, MixBlueRed, MixBlueGreen, MixBlueBlue,
        ShadowsRed, ShadowsGreen, ShadowsBlue, HighlightsRed, HighlightsGreen, HighlightsBlue,
        BloomThreshold, BloomScatter,
        FogReach, FogHeight, FogBase, FogTintRed, FogTintGreen, FogTintBlue,
        Ambient, Reflections,
        SunRed, SunGreen, SunBlue, MoonRed, MoonGreen, MoonBlue,
        ContactShadows, ContactLength, ContactOpacity, ContactScale, ContactReach, ContactFade, ContactQuality,
        SunContact, MoonContact,
        MicroShadows, MicroOpacity,
        OcclusionRadius, OcclusionStrength,
        Count
    }

    /// <summary>What a knob is: the pipeline's setting and slider it writes, the cost setting it comes with, and its bounds.</summary>
    public struct KnobInfo
    {
        public GraphicsKnob Knob;
        public string Setting;   // the pipeline's volume setting by its type's name, or SunLight / MoonLight
        public string Slider;    // the slider's own name there
        public int Cost;         // GraphicsCost: the least cost setting that writes it
        public float Least, Most;   // what the looks may ever write
        public float Step;       // the most it may move between two looks a game minute apart, or for a twentieth of the weather

        public KnobInfo(GraphicsKnob knob, string setting, string slider, int cost, float least, float most, float step)
        {
            Knob = knob;
            Setting = setting;
            Slider = slider;
            Cost = cost;
            Least = least;
            Most = most;
            Step = step;
        }
    }

    /// <summary>The moment a picture is worked out for, in the steps it changes by: equal moments have the same picture.</summary>
    public struct GraphicsMoment
    {
        public int Look, Cost, Place;
        public int HourStep;   // game minutes since midnight
        public int Rain, Snow, Cover;   // twentieths: the game's sky gone to its rain look, snow falling, snow lying
        public int Season;     // CalendarRules.Winter to Autumn, or -1 while the seasons are off

        public float Hour => HourStep / (float)GraphicsRules.StepsPerHour;

        public bool Same(in GraphicsMoment other)
        {
            return Look == other.Look && Cost == other.Cost && Place == other.Place && HourStep == other.HourStep && Rain == other.Rain && Snow == other.Snow
                   && Cover == other.Cover && Season == other.Season;
        }
    }

    /// <summary>One look's numbers. Where four stand together they are for the day, the golden hours, the blue hours and the night.</summary>
    internal sealed class LookSheet
    {
        public float ToeStrength, ToeLength, ShoulderStrength, ShoulderLength, ShoulderAngle;
        public float[] Stops;        // exposure compensation, in stops
        public float StopsIndoors;
        public float[] Colour;       // how strong colour is, 1 the game's
        public float ColourIndoors;
        public float[] Warmth;       // white balance: above 0 warmer
        public float WarmthIndoors;
        public float[] ShadowRed, ShadowBlue;        // the tint of the dark parts round the neutral 0.5; green follows, so brightness stays
        public float ShadowRedIndoors, ShadowBlueIndoors;
        public float[] HighlightRed, HighlightBlue;  // the tint of the bright parts
        public float HighlightRedIndoors, HighlightBlueIndoors;
        public float SunDawn, SunNoon, SunDusk;      // kelvin: the sun as it comes up, overhead and as it goes down
        public float MoonRed, MoonGreen, MoonBlue;   // the moonlight's filter
        public float AmbientDay, AmbientNight;       // the sky's light in the shade, as a share of the game's
        public float ReflectDay, ReflectNight;       // the sky's reflection in glass and paint
        public float BloomDay, BloomNight, BloomIndoors;      // how bright a thing must be to glow
        public float ScatterDay, ScatterNight, ScatterIndoors; // how wide the glow spreads
        public float[] Haze;         // metres a ray goes before the haze takes a third of it
        public float HazeHeight;     // metres the haze reaches up
    }

    /// <summary>
    /// How the picture looks, game-free (checked by EconomyCheck, --graphics): for a look, a cost setting, a place, an
    /// hour, the weather and the season, the value of every setting the picture is given. The game draws one picture for
    /// every hour: a white sun and a white moon, the same exposure day and night, a curve that never reaches white and a
    /// mist that hugs the ground. A look keeps the game's own brightness (the checker follows a grey card through both)
    /// and changes what gives a picture depth: a curve with a softer toe and whiter whites, a warm sun low in the sky over
    /// cool shade, a blue night with deeper blacks, a haze that fades far streets into the sky, and glow kept to lamps.
    /// Everything comes of the hour and the weather, so every machine of a session works out the same picture, and
    /// nothing is saved. Nothing here runs in a frame: a picture is worked out when the clock's minute or the weather's
    /// twentieth moves (under a microsecond in the checker, nothing allocated).
    /// </summary>
    public static class GraphicsRules
    {
        public const int StepsPerHour = 60;      // the picture moves with the clock's minute, a second of play: no step of it can be seen
        public const int WeatherSteps = 20;      // rain and snow move it in twentieths, as the seasons' own looks do
        public const float FreeBudgetMs = 0.1f;      // ms of the graphics card's frame the Free setting may add: the noise of the measurement
        public const float BalancedBudgetMs = 0.5f;  // ms the Balanced setting may add
        public const float HighBudgetMs = 1.2f;      // ms the High setting may add
        public const int DefaultLook = GraphicsLook.Off;   // the game's own until the cost is measured on a real computer (the measuring test)
        public const int DefaultCost = GraphicsCost.Free;
        public const float GreyTolerance = 10f;      // percent: a grey card in a look is this close to the game's own on the screen, so no look is a darker picture
        public const float RainTolerance = 15f;      // percent under a rainy sky, which the looks make a little darker on purpose
        public const float ShadeFloor = 0.92f;       // a grey card in the night's shade never falls under this share of the game's own: the night stays readable

        public const int CostPairs = 3;              // a cost setting is measured off, on, off, on, off, on, off: three pairs
        public const float CostWindowSeconds = 2f;   // seconds a window is measured for
        public const float CostSettleSeconds = 0.6f; // seconds after a switch before a window opens: the pipeline's buffers settle
        public const float HourShownSeconds = 4f;    // seconds each hour stays on the screen in the hours' test
        public const int Pass = 0, Fail = 1, Unsure = 2, NotJudged = 3;   // a cost setting's verdict

        private const float RainStops = -0.18f;      // stops: a rainy day is a little darker than the game's
        private const float SnowStops = 0.10f;       // stops: lying snow is a little brighter
        private const float RainColour = 0.12f;      // rain takes this share of the colour's strength
        private const float GreyColour = 0.06f;      // a snow sky takes this share
        private const float RainWarmth = -5f;        // white balance under rain
        private const float SnowWarmth = -4f;        // and over snow
        private const float RainHaze = 0.58f;        // rain takes this share of the haze's reach
        private const float SnowHaze = 0.5f;         // falling snow takes this share
        private const float GreyHaze = 0.2f;         // a grey sky alone, this share
        private const float RainGloss = 0.2f;        // the sky's reflection in wet streets and paint, added under rain
        private const float RainGlow = 0.06f;        // lamps glow from this much less brightness on a rainy night
        private const float RainSpread = 0.05f;      // and their glow spreads this much wider
        private const float SnowGlow = 0.15f;        // lying snow must be this much brighter to glow, or the white ground would
        private const float RainShadeBlue = 0.012f, RainShadeRed = -0.006f;   // rain cools the dark parts
        private const float SnowShadeBlue = 0.02f, SnowShadeRed = -0.01f;     // snow turns its shade blue

        // The haze's colour as a share of the sky's, for the day, the golden hours, the blue hours and the night.
        private static readonly float[] HazeRed = { 1f, 1.10f, 0.82f, 0.74f };
        private static readonly float[] HazeGreen = { 1f, 0.97f, 0.92f, 0.86f };
        private static readonly float[] HazeBlue = { 1f, 0.82f, 1.10f, 1.06f };

        // The seasons' own touch, winter to autumn: a cooler, paler winter, a fresh spring, a warm full summer, a warm autumn.
        private static readonly float[] SeasonWarmth = { -2f, 0f, 2f, 3f };
        private static readonly float[] SeasonColour = { 0.97f, 1.02f, 1.03f, 1f };

        private static readonly LookSheet Natural = new LookSheet
        {
            ToeStrength = 0.32f, ToeLength = 0.61f, ShoulderStrength = 0.945f, ShoulderLength = 4.75f, ShoulderAngle = 0.10f,
            Stops = new[] { 0.05f, 0.17f, 0.06f, 0.10f }, StopsIndoors = 0f,
            Colour = new[] { 1.06f, 1.10f, 0.98f, 0.93f }, ColourIndoors = 1.04f,
            Warmth = new[] { 0f, 8f, -6f, -4f }, WarmthIndoors = 1f,
            ShadowRed = new[] { 0.488f, 0.490f, 0.470f, 0.455f }, ShadowBlue = new[] { 0.524f, 0.520f, 0.550f, 0.560f },
            ShadowRedIndoors = 0.494f, ShadowBlueIndoors = 0.512f,
            HighlightRed = new[] { 0.506f, 0.530f, 0.505f, 0.520f }, HighlightBlue = new[] { 0.490f, 0.462f, 0.492f, 0.470f },
            HighlightRedIndoors = 0.508f, HighlightBlueIndoors = 0.488f,
            SunDawn = 3200f, SunNoon = 5850f, SunDusk = 3000f,
            MoonRed = 0.74f, MoonGreen = 0.86f, MoonBlue = 1f,
            AmbientDay = 0.94f, AmbientNight = 0.92f, ReflectDay = 1.08f, ReflectNight = 1f,
            BloomDay = 1.65f, BloomNight = 1f, BloomIndoors = 1.25f, ScatterDay = 0.60f, ScatterNight = 0.70f, ScatterIndoors = 0.65f,
            Haze = new[] { 450f, 340f, 380f, 620f }, HazeHeight = 170f,
        };

        private static readonly LookSheet Filmic = new LookSheet
        {
            ToeStrength = 0.33f, ToeLength = 0.59f, ShoulderStrength = 0.94f, ShoulderLength = 3.55f, ShoulderAngle = 0.20f,
            Stops = new[] { 0.14f, 0.32f, 0.18f, 0.22f }, StopsIndoors = 0.06f,
            Colour = new[] { 0.97f, 1.04f, 0.92f, 0.86f }, ColourIndoors = 0.96f,
            Warmth = new[] { -1f, 10f, -8f, -6f }, WarmthIndoors = 0f,
            ShadowRed = new[] { 0.474f, 0.478f, 0.455f, 0.440f }, ShadowBlue = new[] { 0.540f, 0.536f, 0.565f, 0.575f },
            ShadowRedIndoors = 0.486f, ShadowBlueIndoors = 0.524f,
            HighlightRed = new[] { 0.514f, 0.545f, 0.510f, 0.532f }, HighlightBlue = new[] { 0.480f, 0.445f, 0.485f, 0.455f },
            HighlightRedIndoors = 0.514f, HighlightBlueIndoors = 0.480f,
            SunDawn = 3100f, SunNoon = 5600f, SunDusk = 2800f,
            MoonRed = 0.64f, MoonGreen = 0.84f, MoonBlue = 1f,
            AmbientDay = 0.90f, AmbientNight = 0.88f, ReflectDay = 1.04f, ReflectNight = 1f,
            BloomDay = 1.70f, BloomNight = 1f, BloomIndoors = 1.25f, ScatterDay = 0.62f, ScatterNight = 0.74f, ScatterIndoors = 0.68f,
            Haze = new[] { 380f, 280f, 330f, 520f }, HazeHeight = 190f,
        };

        private static readonly LookSheet Vivid = new LookSheet
        {
            ToeStrength = 0.33f, ToeLength = 0.59f, ShoulderStrength = 0.92f, ShoulderLength = 3.75f, ShoulderAngle = 0.09f,
            Stops = new[] { 0f, 0.08f, 0.04f, 0.08f }, StopsIndoors = 0f,
            Colour = new[] { 1.16f, 1.22f, 1.06f, 1.02f }, ColourIndoors = 1.10f,
            Warmth = new[] { 2f, 11f, -5f, -3f }, WarmthIndoors = 2f,
            ShadowRed = new[] { 0.492f, 0.492f, 0.474f, 0.462f }, ShadowBlue = new[] { 0.518f, 0.516f, 0.546f, 0.552f },
            ShadowRedIndoors = 0.496f, ShadowBlueIndoors = 0.508f,
            HighlightRed = new[] { 0.506f, 0.534f, 0.506f, 0.520f }, HighlightBlue = new[] { 0.490f, 0.458f, 0.490f, 0.470f },
            HighlightRedIndoors = 0.508f, HighlightBlueIndoors = 0.488f,
            SunDawn = 3400f, SunNoon = 5900f, SunDusk = 3100f,
            MoonRed = 0.70f, MoonGreen = 0.84f, MoonBlue = 1f,
            AmbientDay = 0.97f, AmbientNight = 0.95f, ReflectDay = 1.12f, ReflectNight = 1.02f,
            BloomDay = 1.55f, BloomNight = 1f, BloomIndoors = 1.20f, ScatterDay = 0.60f, ScatterNight = 0.70f, ScatterIndoors = 0.65f,
            Haze = new[] { 560f, 420f, 460f, 720f }, HazeHeight = 150f,
        };

        private static readonly LookSheet[] Sheets = { null, Natural, Filmic, Vivid };

        /// <summary>
        /// Every setting the looks may write, in the order of <see cref="GraphicsKnob"/>. The Free rows are sliders of work
        /// the game's pipeline already does each frame (its colour table, its exposure, its bloom, its fog, its lights), so
        /// writing them starts nothing; the checker holds them against a list of the switches that would.
        /// </summary>
        public static readonly KnobInfo[] Knobs =
        {
            new KnobInfo(GraphicsKnob.ToneCurve, "Tonemapping", "mode", GraphicsCost.Free, 1f, 1f, 0f),
            new KnobInfo(GraphicsKnob.ToeStrength, "Tonemapping", "toeStrength", GraphicsCost.Free, 0f, 1f, 0f),
            new KnobInfo(GraphicsKnob.ToeLength, "Tonemapping", "toeLength", GraphicsCost.Free, 0f, 1f, 0f),
            new KnobInfo(GraphicsKnob.ShoulderStrength, "Tonemapping", "shoulderStrength", GraphicsCost.Free, 0f, 1f, 0f),
            new KnobInfo(GraphicsKnob.ShoulderLength, "Tonemapping", "shoulderLength", GraphicsCost.Free, 0f, 8f, 0f),
            new KnobInfo(GraphicsKnob.ShoulderAngle, "Tonemapping", "shoulderAngle", GraphicsCost.Free, 0f, 1f, 0f),
            new KnobInfo(GraphicsKnob.Compensation, "Exposure", "compensation", GraphicsCost.Free, -0.4f, 0.5f, 0.012f),
            new KnobInfo(GraphicsKnob.Temperature, "WhiteBalance", "temperature", GraphicsCost.Free, -25f, 25f, 0.7f),
            new KnobInfo(GraphicsKnob.MixRedRed, "ChannelMixer", "redOutRedIn", GraphicsCost.Free, 60f, 140f, 1f),
            new KnobInfo(GraphicsKnob.MixRedGreen, "ChannelMixer", "redOutGreenIn", GraphicsCost.Free, -40f, 40f, 1f),
            new KnobInfo(GraphicsKnob.MixRedBlue, "ChannelMixer", "redOutBlueIn", GraphicsCost.Free, -40f, 40f, 1f),
            new KnobInfo(GraphicsKnob.MixGreenRed, "ChannelMixer", "greenOutRedIn", GraphicsCost.Free, -40f, 40f, 1f),
            new KnobInfo(GraphicsKnob.MixGreenGreen, "ChannelMixer", "greenOutGreenIn", GraphicsCost.Free, 60f, 140f, 1f),
            new KnobInfo(GraphicsKnob.MixGreenBlue, "ChannelMixer", "greenOutBlueIn", GraphicsCost.Free, -40f, 40f, 1f),
            new KnobInfo(GraphicsKnob.MixBlueRed, "ChannelMixer", "blueOutRedIn", GraphicsCost.Free, -40f, 40f, 1f),
            new KnobInfo(GraphicsKnob.MixBlueGreen, "ChannelMixer", "blueOutGreenIn", GraphicsCost.Free, -40f, 40f, 1f),
            new KnobInfo(GraphicsKnob.MixBlueBlue, "ChannelMixer", "blueOutBlueIn", GraphicsCost.Free, 60f, 140f, 1f),
            new KnobInfo(GraphicsKnob.ShadowsRed, "SplitToning", "shadows", GraphicsCost.Free, 0.38f, 0.62f, 0.002f),
            new KnobInfo(GraphicsKnob.ShadowsGreen, "SplitToning", "shadows", GraphicsCost.Free, 0.38f, 0.62f, 0.002f),
            new KnobInfo(GraphicsKnob.ShadowsBlue, "SplitToning", "shadows", GraphicsCost.Free, 0.38f, 0.62f, 0.002f),
            new KnobInfo(GraphicsKnob.HighlightsRed, "SplitToning", "highlights", GraphicsCost.Free, 0.38f, 0.62f, 0.002f),
            new KnobInfo(GraphicsKnob.HighlightsGreen, "SplitToning", "highlights", GraphicsCost.Free, 0.38f, 0.62f, 0.002f),
            new KnobInfo(GraphicsKnob.HighlightsBlue, "SplitToning", "highlights", GraphicsCost.Free, 0.38f, 0.62f, 0.002f),
            new KnobInfo(GraphicsKnob.BloomThreshold, "Bloom", "threshold", GraphicsCost.Free, 0.8f, 2.2f, 0.01f),
            new KnobInfo(GraphicsKnob.BloomScatter, "Bloom", "scatter", GraphicsCost.Free, 0.4f, 0.9f, 0.0035f),
            new KnobInfo(GraphicsKnob.FogReach, "Fog", "meanFreePath", GraphicsCost.Free, 80f, 900f, 28f),
            new KnobInfo(GraphicsKnob.FogHeight, "Fog", "maximumHeight", GraphicsCost.Free, 100f, 300f, 0f),
            new KnobInfo(GraphicsKnob.FogBase, "Fog", "baseHeight", GraphicsCost.Free, 0f, 0f, 0f),
            new KnobInfo(GraphicsKnob.FogTintRed, "Fog", "tint", GraphicsCost.Free, 0.5f, 1.3f, 0.011f),
            new KnobInfo(GraphicsKnob.FogTintGreen, "Fog", "tint", GraphicsCost.Free, 0.5f, 1.3f, 0.011f),
            new KnobInfo(GraphicsKnob.FogTintBlue, "Fog", "tint", GraphicsCost.Free, 0.5f, 1.3f, 0.011f),
            new KnobInfo(GraphicsKnob.Ambient, "IndirectLightingController", "indirectDiffuseLightingMultiplier", GraphicsCost.Free, 0.8f, 1.05f, 0.008f),
            new KnobInfo(GraphicsKnob.Reflections, "IndirectLightingController", "reflectionLightingMultiplier", GraphicsCost.Free, 0.9f, 1.5f, 0.013f),
            new KnobInfo(GraphicsKnob.SunRed, "SunLight", "color", GraphicsCost.Free, 0f, 1f, 0.025f),
            new KnobInfo(GraphicsKnob.SunGreen, "SunLight", "color", GraphicsCost.Free, 0f, 1f, 0.025f),
            new KnobInfo(GraphicsKnob.SunBlue, "SunLight", "color", GraphicsCost.Free, 0f, 1f, 0.025f),
            new KnobInfo(GraphicsKnob.MoonRed, "MoonLight", "color", GraphicsCost.Free, 0.5f, 1f, 0f),
            new KnobInfo(GraphicsKnob.MoonGreen, "MoonLight", "color", GraphicsCost.Free, 0.5f, 1f, 0f),
            new KnobInfo(GraphicsKnob.MoonBlue, "MoonLight", "color", GraphicsCost.Free, 0.5f, 1f, 0f),
            new KnobInfo(GraphicsKnob.ContactShadows, "ContactShadows", "enable", GraphicsCost.Balanced, 1f, 1f, 0f),
            new KnobInfo(GraphicsKnob.ContactLength, "ContactShadows", "length", GraphicsCost.Balanced, 0.05f, 0.5f, 0f),
            new KnobInfo(GraphicsKnob.ContactOpacity, "ContactShadows", "opacity", GraphicsCost.Balanced, 0.3f, 1f, 0f),
            new KnobInfo(GraphicsKnob.ContactScale, "ContactShadows", "distanceScaleFactor", GraphicsCost.Balanced, 0f, 1f, 0f),
            new KnobInfo(GraphicsKnob.ContactReach, "ContactShadows", "maxDistance", GraphicsCost.Balanced, 50f, 300f, 0f),
            new KnobInfo(GraphicsKnob.ContactFade, "ContactShadows", "fadeDistance", GraphicsCost.Balanced, 5f, 80f, 0f),
            new KnobInfo(GraphicsKnob.ContactQuality, "ContactShadows", "quality", GraphicsCost.Balanced, 0f, 2f, 0f),
            new KnobInfo(GraphicsKnob.SunContact, "SunLight", "useContactShadow", GraphicsCost.Balanced, 1f, 1f, 0f),
            new KnobInfo(GraphicsKnob.MoonContact, "MoonLight", "useContactShadow", GraphicsCost.High, 1f, 1f, 0f),
            new KnobInfo(GraphicsKnob.MicroShadows, "MicroShadowing", "enable", GraphicsCost.Balanced, 1f, 1f, 0f),
            new KnobInfo(GraphicsKnob.MicroOpacity, "MicroShadowing", "opacity", GraphicsCost.Balanced, 0.3f, 1f, 0f),
            new KnobInfo(GraphicsKnob.OcclusionRadius, "HBAO", "radius", GraphicsCost.Balanced, 1f, 2.5f, 0f),
            new KnobInfo(GraphicsKnob.OcclusionStrength, "HBAO", "intensity", GraphicsCost.Balanced, 1f, 1.5f, 0f),
        };

        /// <summary>The look's name in English, for Player.log.</summary>
        public static string LookName(int look)
        {
            return look == GraphicsLook.Natural ? "Natural" : look == GraphicsLook.Filmic ? "Filmic" : look == GraphicsLook.Vivid ? "Vivid" : "Off";
        }

        /// <summary>The cost setting's name in English, for Player.log.</summary>
        public static string CostName(int cost)
        {
            return cost == GraphicsCost.Balanced ? "Balanced" : cost == GraphicsCost.High ? "High" : "Free";
        }

        /// <summary>The most a cost setting may add to the graphics card's frame, in milliseconds.</summary>
        public static float Budget(int cost)
        {
            return cost == GraphicsCost.Balanced ? BalancedBudgetMs : cost == GraphicsCost.High ? HighBudgetMs : FreeBudgetMs;
        }

        /// <summary>
        /// What a setting adds to a frame, in ms: the mean over the pairs of each "on" window less the mean of the "off"
        /// windows either side of it, which cancels a steady drift. The windows run off, on, off ... and end off.
        /// </summary>
        public static float Added(float[] windows, int pairs)
        {
            float sum = 0f;
            for (int k = 0; k < pairs; k++)
            {
                sum += windows[k * 2 + 1] - (windows[k * 2] + windows[k * 2 + 2]) * 0.5f;
            }
            return sum / Math.Max(1, pairs);
        }

        /// <summary>How far the pairs disagree, in ms: the largest pair's difference less the smallest's. It is the measurement's own noise.</summary>
        public static float Spread(float[] windows, int pairs)
        {
            float least = float.MaxValue, most = float.MinValue;
            for (int k = 0; k < pairs; k++)
            {
                float d = windows[k * 2 + 1] - (windows[k * 2] + windows[k * 2 + 2]) * 0.5f;
                least = Math.Min(least, d);
                most = Math.Max(most, d);
            }
            return pairs > 0 ? most - least : 0f;
        }

        /// <summary>
        /// The verdict on what a setting added against its budget: within it passes; over it by less than the pairs
        /// disagree is not shown either way; over it by more fails.
        /// </summary>
        public static int CostVerdict(float added, float spread, float budget)
        {
            return added <= budget ? Pass : added - Math.Max(0f, spread) <= budget ? Unsure : Fail;
        }

        /// <summary>A look or cost as the options keep it, held to what there is: anything else is the game's own, at no cost.</summary>
        public static int LookOf(int saved) => saved > GraphicsLook.Off && saved < GraphicsLook.Count ? saved : GraphicsLook.Off;

        public static int CostOf(int saved) => saved > GraphicsCost.Free && saved < GraphicsCost.Count ? saved : GraphicsCost.Free;

        /// <summary>The moment for a look, in its steps: an hour of 0 to 24 with its minutes, and rain, falling snow and lying snow from 0 to 1.</summary>
        public static GraphicsMoment Moment(int look, int cost, int place, float hour, float rain, float snow, float cover, int season)
        {
            int steps = 24 * StepsPerHour;
            int step = float.IsNaN(hour) || float.IsInfinity(hour) ? steps / 2 : (int)Math.Round(hour * StepsPerHour) % steps;   // no hour: noon
            return new GraphicsMoment
            {
                Look = LookOf(look), Cost = CostOf(cost), Place = place == GraphicsPlace.Indoors ? GraphicsPlace.Indoors : GraphicsPlace.City,
                HourStep = step < 0 ? step + steps : step,
                Rain = Twentieths(rain), Snow = Twentieths(snow), Cover = Twentieths(cover),
                Season = season >= CalendarRules.Winter && season <= CalendarRules.Autumn ? season : -1,
            };
        }

        // The day's phases, each 0 to 1. The game lights its sun at 6:00 and puts it out at 21:00, and its sky turns in the
        // hour before each: the looks turn round the same hours.

        /// <summary>How much of the day's own settings an hour has: 0 through the night, 1 from mid morning to early evening.</summary>
        public static float Day(float hour) => Smooth(5.4f, 8.6f, hour) * (1f - Smooth(18.2f, 21.2f, hour));

        /// <summary>How much of the night's: 1 from late evening to before dawn.</summary>
        public static float Night(float hour) => hour < 12f ? 1f - Smooth(4.9f, 6.4f, hour) : Smooth(20.6f, 22.2f, hour);

        /// <summary>The golden hours, after sunrise and before sunset.</summary>
        public static float Golden(float hour)
        {
            return Math.Max(Smooth(5.8f, 7.0f, hour) * (1f - Smooth(7.4f, 9.6f, hour)), Smooth(16.4f, 18.8f, hour) * (1f - Smooth(19.9f, 21.0f, hour)));
        }

        /// <summary>The blue hours, before sunrise and after sunset.</summary>
        public static float Blue(float hour)
        {
            return Math.Max(Smooth(4.6f, 5.6f, hour) * (1f - Smooth(5.9f, 6.8f, hour)), Smooth(20.0f, 21.0f, hour) * (1f - Smooth(21.3f, 22.4f, hour)));
        }

        /// <summary>How high the sun stands for its colour: 0 as it comes up and goes down, 1 through the middle of the day.</summary>
        public static float SunHeight(float hour) => Smooth(6.0f, 9.5f, hour) * (1f - Smooth(16.5f, 20.75f, hour));

        /// <summary>
        /// Every setting of the picture for a moment: <paramref name="values"/> by <see cref="GraphicsKnob"/>, and in
        /// <paramref name="set"/> whether the look writes it at all (a knob not set is the game's own). Off sets nothing.
        /// Both arrays are the caller's, <see cref="GraphicsKnob.Count"/> long; nothing is allocated.
        /// </summary>
        public static void Evaluate(in GraphicsMoment at, float[] values, bool[] set)
        {
            for (int i = 0; i < (int)GraphicsKnob.Count; i++)
            {
                set[i] = false;
                values[i] = 0f;
            }
            if (at.Look <= GraphicsLook.Off || at.Look >= GraphicsLook.Count)
            {
                return;
            }
            LookSheet look = Sheets[at.Look];
            bool city = at.Place == GraphicsPlace.City;
            float hour = at.Hour;
            float night = Night(hour), golden = Golden(hour), blue = Blue(hour), day = Day(hour);
            float rain = city ? at.Rain / (float)WeatherSteps : 0f;
            float snow = city ? at.Snow / (float)WeatherSteps : 0f;
            float cover = city ? at.Cover / (float)WeatherSteps : 0f;
            float grey = Math.Max(snow, cover * 0.4f);   // a snow sky, as the seasons work it out
            float wet = rain * (1f - snow);              // rain that falls as rain: while it snows the seasons show the game's rain as the snow

            Put(values, set, GraphicsKnob.ToneCurve, 1f);
            Put(values, set, GraphicsKnob.ToeStrength, look.ToeStrength);
            Put(values, set, GraphicsKnob.ToeLength, look.ToeLength);
            Put(values, set, GraphicsKnob.ShoulderStrength, look.ShoulderStrength);
            Put(values, set, GraphicsKnob.ShoulderLength, look.ShoulderLength);
            Put(values, set, GraphicsKnob.ShoulderAngle, look.ShoulderAngle);

            float stops = city ? Phased(look.Stops, night, golden, blue) + RainStops * wet * day + SnowStops * cover * day : look.StopsIndoors;
            Put(values, set, GraphicsKnob.Compensation, stops);

            float warmth = city ? Phased(look.Warmth, night, golden, blue) + RainWarmth * wet + SnowWarmth * Math.Max(snow, cover) : look.WarmthIndoors;
            float colour = city ? Phased(look.Colour, night, golden, blue) * (1f - RainColour * wet) * (1f - GreyColour * grey) : look.ColourIndoors;
            if (city && at.Season >= 0)
            {
                warmth += SeasonWarmth[at.Season];
                colour *= SeasonColour[at.Season];
            }
            Put(values, set, GraphicsKnob.Temperature, warmth);
            for (int row = 0; row < 3; row++)
            {
                GraphicsPicture.MixerRow(row, colour, out float fromRed, out float fromGreen, out float fromBlue);
                int first = (int)GraphicsKnob.MixRedRed + row * 3;
                Put(values, set, (GraphicsKnob)first, fromRed);
                Put(values, set, (GraphicsKnob)(first + 1), fromGreen);
                Put(values, set, (GraphicsKnob)(first + 2), fromBlue);
            }

            float shadowRed = city ? Phased(look.ShadowRed, night, golden, blue) + RainShadeRed * wet + SnowShadeRed * cover : look.ShadowRedIndoors;
            float shadowBlue = city ? Phased(look.ShadowBlue, night, golden, blue) + RainShadeBlue * wet + SnowShadeBlue * cover : look.ShadowBlueIndoors;
            Tint(values, set, GraphicsKnob.ShadowsRed, shadowRed, shadowBlue);
            float highRed = city ? Phased(look.HighlightRed, night, golden, blue) : look.HighlightRedIndoors;
            float highBlue = city ? Phased(look.HighlightBlue, night, golden, blue) : look.HighlightBlueIndoors;
            Tint(values, set, GraphicsKnob.HighlightsRed, highRed, highBlue);

            if (city)
            {
                Put(values, set, GraphicsKnob.BloomThreshold, look.BloomNight + (look.BloomDay - look.BloomNight) * day - RainGlow * wet * night + SnowGlow * cover * day);
                Put(values, set, GraphicsKnob.BloomScatter, look.ScatterNight + (look.ScatterDay - look.ScatterNight) * day + RainSpread * wet);
                float thick = Math.Max(RainHaze * wet, Math.Max(SnowHaze * snow, GreyHaze * grey));   // the thickest of the three, never one on top of another
                Put(values, set, GraphicsKnob.FogReach, Phased(look.Haze, night, golden, blue) * (1f - thick));
                Put(values, set, GraphicsKnob.FogHeight, look.HazeHeight);
                Put(values, set, GraphicsKnob.FogBase, 0f);
                Put(values, set, GraphicsKnob.FogTintRed, Phased(HazeRed, night, golden, blue));
                Put(values, set, GraphicsKnob.FogTintGreen, Phased(HazeGreen, night, golden, blue));
                Put(values, set, GraphicsKnob.FogTintBlue, Phased(HazeBlue, night, golden, blue));
                // Under a grey sky there is no sun for the shade to stand against: the sky's light is the game's own again.
                float ambient = look.AmbientDay + (look.AmbientNight - look.AmbientDay) * night;
                Put(values, set, GraphicsKnob.Ambient, ambient + (1f - ambient) * Math.Max(rain, grey));
                Put(values, set, GraphicsKnob.Reflections, look.ReflectNight + (look.ReflectDay - look.ReflectNight) * day + RainGloss * wet);
            }
            else
            {
                Put(values, set, GraphicsKnob.BloomThreshold, look.BloomIndoors);
                Put(values, set, GraphicsKnob.BloomScatter, look.ScatterIndoors);
            }

            // The sun's and the moon's own light: the same lights shine through the windows, so indoors too. While the
            // game has its sun out the filter stands still, as it went down or as it will come up: nothing is written for a
            // light that does not shine.
            float sunHour = hour >= GraphicsModel.SunTo ? GraphicsModel.SunTo : hour < GraphicsModel.SunFrom ? GraphicsModel.SunFrom : hour;
            float low = sunHour < 12f ? look.SunDawn : look.SunDusk;
            float kelvin = low + (look.SunNoon - low) * SunHeight(sunHour);
            GraphicsPicture.Filter(kelvin, GraphicsModel.SunKelvin(sunHour), out float sunRed, out float sunGreen, out float sunBlue);
            Put(values, set, GraphicsKnob.SunRed, sunRed);
            Put(values, set, GraphicsKnob.SunGreen, sunGreen);
            Put(values, set, GraphicsKnob.SunBlue, sunBlue);
            Put(values, set, GraphicsKnob.MoonRed, look.MoonRed);
            Put(values, set, GraphicsKnob.MoonGreen, look.MoonGreen);
            Put(values, set, GraphicsKnob.MoonBlue, look.MoonBlue);

            if (at.Cost >= GraphicsCost.Balanced)
            {
                bool high = at.Cost >= GraphicsCost.High;
                Put(values, set, GraphicsKnob.ContactShadows, 1f);
                Put(values, set, GraphicsKnob.ContactLength, high ? 0.3f : 0.2f);      // metres a ray is followed
                Put(values, set, GraphicsKnob.ContactOpacity, high ? 0.85f : 0.75f);
                Put(values, set, GraphicsKnob.ContactScale, 0.4f);                     // longer rays farther off: the camera stands well back
                Put(values, set, GraphicsKnob.ContactReach, high ? 200f : 150f);       // metres: the pipeline's own 50 would end them half way up the screen
                Put(values, set, GraphicsKnob.ContactFade, high ? 40f : 30f);
                Put(values, set, GraphicsKnob.ContactQuality, high ? 2f : 1f);         // the pipeline's medium (10 samples) and high (16)
                Put(values, set, GraphicsKnob.SunContact, 1f);
                if (high)
                {
                    Put(values, set, GraphicsKnob.MoonContact, 1f);
                }
                Put(values, set, GraphicsKnob.MicroShadows, 1f);
                Put(values, set, GraphicsKnob.MicroOpacity, high ? 0.9f : 0.7f);
                if (city)
                {
                    // Shares of the game's own corner shading; a room keeps the game's, which is up to twice the street's already.
                    Put(values, set, GraphicsKnob.OcclusionRadius, high ? 1.8f : 1.5f);
                    Put(values, set, GraphicsKnob.OcclusionStrength, high ? 1.3f : 1.15f);
                }
            }
        }

        // A setting through the day: the day's value turning to the night's, the blue hours' in between, the golden hours' on top.
        private static float Phased(float[] four, float night, float golden, float blue)
        {
            float steady = four[0] + (four[3] - four[0]) * night;
            return steady * (1f - blue) + four[2] * blue + (four[1] - four[0]) * golden * (1f - night) * (1f - blue);
        }

        private static void Tint(float[] values, bool[] set, GraphicsKnob red, float r, float b)
        {
            Put(values, set, red, r);
            Put(values, set, (GraphicsKnob)((int)red + 1), GraphicsPicture.ToningGreen(r, b));
            Put(values, set, (GraphicsKnob)((int)red + 2), b);
        }

        private static void Put(float[] values, bool[] set, GraphicsKnob knob, float value)
        {
            KnobInfo info = Knobs[(int)knob];
            values[(int)knob] = Math.Max(info.Least, Math.Min(info.Most, value));
            set[(int)knob] = true;
        }

        private static int Twentieths(float share)
        {
            return share > 0f ? (int)Math.Round(Math.Min(1f, share) * WeatherSteps) : 0;   // also for a share that is no number
        }

        private static float Smooth(float from, float to, float x)
        {
            float t = Math.Max(0f, Math.Min(1f, (x - from) / (to - from)));
            return t * t * (3f - 2f * t);
        }
    }
}
