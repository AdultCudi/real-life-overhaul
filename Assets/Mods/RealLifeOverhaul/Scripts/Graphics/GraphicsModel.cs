using System;

namespace RealLifeOverhaul.Gfx
{
    /// <summary>A grey card lying on the ground as the screen shows it, 0 to 1: where the sun or the moon reaches it, and in shade.</summary>
    public struct CardPair
    {
        public float Lit, Shade;

        public float Middle => (Lit + Shade) * 0.5f;
    }

    /// <summary>
    /// The game's own day as its files have it (build 3682: the fair-weather and rain settings of its clock, sampled on the
    /// hour), and what a grey card becomes in the game's picture and in a look's, game-free (checked by EconomyCheck).
    /// The game lights the city with a sun of up to 13,500 lux, a moon of 1,000 lux and a gradient sky, at a fixed exposure
    /// of EV 10, through the pipeline's fitted ACES curve: the card is followed through exactly that, so a look can be held
    /// to the game's own brightness hour by hour. It is a model of flat ground: no shadows of buildings, no lamps, no
    /// ambient occlusion. Nothing here runs in a frame; the "say what changed" test prints one card from it.
    /// </summary>
    public static class GraphicsModel
    {
        public const float Exposure = 1f / (1.2f * 1024f);   // the game's fixed exposure, EV 10, with the pipeline's lens factor of 1.2
        public const float SkyGain = 32f;                    // the game's gradient sky is set five stops up
        public const float MoonSine = 0.9f;                  // the game's moon stands 55 to 70 degrees up all night
        public const float SunFrom = 6f, SunTo = 21f;        // the hours between which the game has its sun switched on, and its moon off
        public const float IndoorCard = 0.2f;                // a grey card indoors, as bright in the frame as one in the city's shade
        public const float SnowSky = 0.8f;                   // how far the seasons take a snow day's sky to the game's rain sky (Seasons)

        // Each table: the hours 0 to 24, the first row to 11.
        private static readonly float[] SunLux =
        {
            0, 0, 0, 0, 0, 0, 722, 5297, 10818, 13500, 13500, 13500,
            13500, 13500, 13500, 13500, 13500, 12688, 10679, 8110, 5171, 0, 0, 0, 0
        };
        private static readonly float[] SunHeat =
        {
            9000, 9000, 9000, 9000, 8951, 8506, 7796, 7071, 6580, 6500, 6500, 6500,
            6500, 6500, 6500, 6500, 6500, 6500, 6500, 5957, 3559, 3610, 6877, 9000, 9000
        };
        private static readonly float[] MoonLux =
        {
            1000, 997, 996, 998, 999, 1000, 1, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1000, 1000, 1000
        };
        private static readonly float[] SunTurn =
        {
            225, 222, 214, 202.4f, 188.7f, 174.4f, 161, 150, 142.9f, 135.7f, 128.6f, 121.4f,
            114.3f, 107.1f, 100, 92.9f, 85.7f, 78.6f, 71.4f, 64.3f, 57.1f, 50, 40.4f, 30.8f, 21.2f
        };
        private static readonly float[] SkyTop =
        {
            6.0f, 6.0f, 6.0f, 6.0f, 6.0f, 12.1f, 42.2f, 41.9f, 41.6f, 41.3f, 41.0f, 40.7f,
            40.4f, 40.1f, 39.8f, 39.5f, 39.2f, 41.1f, 43.2f, 45.3f, 47.4f, 35.1f, 13.1f, 6.0f, 6.0f
        };
        private static readonly float[] SkyMiddle =
        {
            11.6f, 13.7f, 15.9f, 18.1f, 20.3f, 27.2f, 53.1f, 46.0f, 44.6f, 44.6f, 44.6f, 44.6f,
            44.6f, 44.6f, 44.6f, 44.6f, 44.6f, 43.9f, 41.8f, 39.8f, 35.8f, 26.7f, 14.8f, 11.0f, 11.0f
        };
        private static readonly float[] RainTop =
        {
            48.3f, 46.9f, 45.5f, 44.1f, 42.7f, 47.1f, 74.8f, 102.5f, 113.3f, 119.9f, 126.5f, 133.0f,
            139.6f, 146.2f, 152.8f, 159.4f, 166.0f, 172.6f, 179.2f, 124.5f, 69.8f, 48.0f, 48.1f, 48.2f, 48.3f
        };
        private static readonly float[] RainMiddle =
        {
            9.0f, 11.5f, 13.9f, 16.4f, 18.8f, 21.1f, 22.4f, 23.7f, 24.7f, 24.7f, 24.7f, 24.7f,
            24.7f, 24.7f, 24.7f, 24.7f, 24.7f, 24.7f, 24.7f, 22.6f, 20.6f, 18.0f, 15.0f, 12.0f, 9.0f
        };

        /// <summary>The colour temperature the game gives its sun at an hour of a fair day, in kelvin.</summary>
        public static float SunKelvin(float hour) => At(SunHeat, hour);

        /// <summary>The card in the game's own picture. <paramref name="grey"/> is how far the sky has gone to the game's rain look, 0 to 1.</summary>
        public static CardPair Game(float hour, float grey, float contrast)
        {
            Light(hour, grey, out float sun, out float moon, out float sky);
            var card = new CardPair
            {
                Lit = Card(sun + moon + sky),
                Shade = Card(sky),
            };
            card.Lit = GraphicsPicture.Screen(GraphicsPicture.GameCurve(GraphicsPicture.Contrast(card.Lit, contrast, false)));
            card.Shade = GraphicsPicture.Screen(GraphicsPicture.GameCurve(GraphicsPicture.Contrast(card.Shade, contrast, false)));
            return card;
        }

        /// <summary>The card in a look's picture, from the settings the rules worked out for the same moment.</summary>
        public static CardPair Look(float hour, float grey, float contrast, float[] values, bool[] set)
        {
            if (!set[(int)GraphicsKnob.ToneCurve])
            {
                return Game(hour, grey, contrast);   // no look on: the game's own
            }
            Light(hour, grey, out float sun, out float moon, out float sky);
            float sunShare = Filter(values, set, GraphicsKnob.SunRed);
            float moonShare = Filter(values, set, GraphicsKnob.MoonRed);
            float ambient = set[(int)GraphicsKnob.Ambient] ? values[(int)GraphicsKnob.Ambient] : 1f;
            float gain = Gain(values, set);
            ToneCurve curve = Curve(values);
            var card = new CardPair
            {
                Lit = Card(sun * sunShare + moon * moonShare + sky * ambient) * gain,
                Shade = Card(sky * ambient) * gain,
            };
            card.Lit = GraphicsPicture.Screen(curve.At(GraphicsPicture.Contrast(card.Lit, contrast, true)));
            card.Shade = GraphicsPicture.Screen(curve.At(GraphicsPicture.Contrast(card.Shade, contrast, true)));
            return card;
        }

        /// <summary>The indoor card, which the game lights with lamps of its own: the same card under the game's curve and under a look's.</summary>
        public static float Indoors(float contrast, float[] values, bool[] set)
        {
            if (values == null || !set[(int)GraphicsKnob.ToneCurve])
            {
                return GraphicsPicture.Screen(GraphicsPicture.GameCurve(GraphicsPicture.Contrast(IndoorCard, contrast, false)));
            }
            return GraphicsPicture.Screen(Curve(values).At(GraphicsPicture.Contrast(IndoorCard * Gain(values, set), contrast, true)));
        }

        /// <summary>A brightness of the frame through a look's curve, as the screen shows it: for the checks of white and black.</summary>
        public static float Through(float[] values, float brightness)
        {
            return GraphicsPicture.Screen(Curve(values).At(brightness));
        }

        /// <summary>The same brightness in the game's own picture.</summary>
        public static float ThroughGame(float brightness)
        {
            return GraphicsPicture.Screen(GraphicsPicture.GameCurve(brightness));
        }

        /// <summary>How far the game's sky has gone to its rain look: by its own rain, and by the seasons' snow sky under it.</summary>
        public static float Grey(float rain, float snow, float cover)
        {
            float snowSky = Math.Max(Clamp01(snow), Clamp01(cover) * 0.4f) * SnowSky;
            return 1f - (1f - snowSky) * (1f - Clamp01(rain));
        }

        // The light on flat ground at an hour, in lux: the sun and the moon as far as they reach it, and the sky.
        private static void Light(float hour, float grey, out float sun, out float moon, out float sky)
        {
            hour = Wrap(hour);
            grey = Clamp01(grey);
            bool sunUp = hour >= SunFrom && hour < SunTo;
            float turn = At(SunTurn, hour);
            float height = turn <= 90f ? turn : 180f - turn;
            sun = sunUp ? Math.Max(0f, At(SunLux, hour)) * (1f - grey) * MathF.Sin(Math.Max(0f, height) * MathF.PI / 180f) : 0f;
            moon = sunUp ? 0f : At(MoonLux, hour) * (1f - grey) * MoonSine;
            float top = At(SkyTop, hour) * (1f - grey) + At(RainTop, hour) * grey;
            float middle = At(SkyMiddle, hour) * (1f - grey) + At(RainMiddle, hour) * grey;
            sky = MathF.PI * (2f * top + middle) / 3f * SkyGain;   // a sky that runs from its horizon colour to its top, on ground facing up
        }

        private static float Card(float lux) => GraphicsPicture.MidGrey * lux / MathF.PI * Exposure;

        private static float Filter(float[] values, bool[] set, GraphicsKnob red)
        {
            int at = (int)red;
            return set[at] ? GraphicsPicture.Luminance(values[at], values[at + 1], values[at + 2]) : 1f;
        }

        private static float Gain(float[] values, bool[] set)
        {
            float stops = set[(int)GraphicsKnob.Compensation] ? values[(int)GraphicsKnob.Compensation] : 0f;
            float balance = set[(int)GraphicsKnob.Temperature] ? GraphicsPicture.BalanceGain(values[(int)GraphicsKnob.Temperature]) : 1f;
            return MathF.Pow(2f, stops) * balance;
        }

        private static ToneCurve Curve(float[] values)
        {
            return ToneCurve.Make(values[(int)GraphicsKnob.ToeStrength], values[(int)GraphicsKnob.ToeLength], values[(int)GraphicsKnob.ShoulderStrength],
                values[(int)GraphicsKnob.ShoulderLength], values[(int)GraphicsKnob.ShoulderAngle], 1f);
        }

        private static float At(float[] table, float hour)
        {
            hour = Wrap(hour);
            int i = Math.Min(23, (int)hour);
            return table[i] + (table[i + 1] - table[i]) * (hour - i);
        }

        private static float Wrap(float hour)
        {
            hour %= 24f;
            return hour < 0f ? hour + 24f : hour;
        }

        private static float Clamp01(float v) => Math.Max(0f, Math.Min(1f, v));
    }
}
