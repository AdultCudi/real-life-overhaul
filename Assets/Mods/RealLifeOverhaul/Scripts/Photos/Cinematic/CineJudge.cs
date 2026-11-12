using System;
using System.Globalization;

namespace RealLifeOverhaul.Photos
{
    /// <summary>What a ray cast across the frame met.</summary>
    public enum CineHit : byte
    {
        Sky,       // nothing within the ray's reach
        Ground,    // the street, the pavement, grass
        Tree,
        Solid,     // a wall, a roof, a car, a lamp: anything else
        Subject    // the shot's own subject, which may fill a frame
    }

    /// <summary>A picture in three figures: its mean brightness and how far its brightness spreads (0 to 255), and its mean saturation (0 to 1).</summary>
    public struct CineStat
    {
        public float Mean, Spread, Saturation;
        public int Count;

        public string Words()
        {
            return Count == 0
                ? "not read"
                : "brightness " + Mean.ToString("0", CultureInfo.InvariantCulture) + " spread " + Spread.ToString("0", CultureInfo.InvariantCulture)
                  + " saturation " + Saturation.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>The sums a picture's three figures are made from, a pixel at a time. Plain numbers.</summary>
    public struct CineTally
    {
        private double _sum, _squares, _saturation;
        private int _count;

        public void Add(int r, int g, int b)
        {
            double light = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            int most = Math.Max(r, Math.Max(g, b)), least = Math.Min(r, Math.Min(g, b));
            _sum += light;
            _squares += light * light;
            _saturation += most > 0 ? (most - least) / (double)most : 0.0;
            _count++;
        }

        public CineStat Finish()
        {
            if (_count == 0)
            {
                return default(CineStat);
            }
            double mean = _sum / _count;
            double spread = Math.Sqrt(Math.Max(0.0, _squares / _count - mean * mean));
            return new CineStat { Mean = (float)mean, Spread = (float)spread, Saturation = (float)(_saturation / _count), Count = _count };
        }
    }

    /// <summary>The game's light as it is read back from the game after an hour was shown: what a picture's name and its list say.</summary>
    public struct CineSeen
    {
        public bool Read;          // false where the game's lights could not be read
        public bool SunOn, MoonOn, Lamps;
        public float SunLux, SunPeak, SunKelvin, SunUp;   // the sun's strength, the day's most, its colour temperature, degrees over the horizon
        public float MoonLux;
        public float Hour;         // the hour the game lights the city for
        public float SunOnHour, SunOffHour;
        public float Exposure;     // the pipeline's fixed exposure where the camera stands, NaN where it is not fixed
        public float HazeGame;     // metres a ray goes in the fog where the camera stands, as the game (and the mod's look) set it; 0 for no fog
        public float HazeUsed;     // and as the picture was drawn
        public float HazeHeight;   // metres the fog reaches up
    }

    /// <summary>
    /// The judging of a cinematic shot in plain numbers: what fills the frame (rays cast across it), which light the game
    /// really shows (read back from its sun, moon and lamps), how far the game's ground mist is lifted for a camera that
    /// stands in it, and whether the saved picture is the screen's own (its brightness, spread and saturation against a
    /// reading of the screen). Game-free, so EconomyCheck runs it (CinematicChecks.cs). Asked once a shot.
    /// </summary>
    public static class CineJudge
    {
        public const int Columns = 9, Rows = 5;     // rays across the frame and up it; row 0 is the foot of the frame
        public const int Rays = Columns * Rows;
        public const float RayReach = 600f;         // metres a ray is cast: beyond it is sky or the far city
        public const float NearWall = 6f;           // metres: a surface this near that fills the frame is a wall in the lens
        public const float NearShare = 0.4f;        // of the rays on one surface that near: the shot is given up
        public const float NearAll = 0.6f;          // of the rays on anything that near
        public const float Clutter = 12f;           // metres: a tank or a hut this near on a roof is in the skyline's way
        public const float FarLeast = 80f;          // metres: a ray that goes this far sees the city, not the foreground
        public const float StreetLeast = 88f;       // metres the middle of a street shot must run clear
        public const float SideMost = 90f;          // metres within which a side of a street shot must show something of the street's side
        public const int TreesLeast = 4;            // trees in the frame of a park shot

        public const float HazeLeast = 450f;        // metres a ray goes at the least in a picture's air: about what the game's own high camera sees through its mist
        public const float HazeDepths = 2.5f;       // times the shot's depth
        public const float HazeMost = 3000f;

        public const float GoldenShare = 0.2f;      // of the sun's most: under it the sun is too weak to gild anything
        public const float GoldenKelvin = 4800f;    // a sun this warm or warmer is a golden one
        public const float GoldenUp = 20f;          // or one this low
        public const float BlueHours = 1.25f;       // hours after the sun goes out, or before it comes on, that are the blue hour

        public const float SameMean = 12f;          // of 255: a target and a screen this near in brightness are one picture
        public const float SameSpread = 8f;
        public const float SameSaturation = 0.04f;
        public const float FlatSpread = 14f;        // a picture whose brightness spreads less than this is flat
        public const float FlatShare = 0.45f;       // or less than this share of the game's own view's spread
        public const float GreyShare = 0.45f;       // a picture with less than this share of the game's own view's saturation is grey
        public const float GreyLeast = 0.05f;       // unless the game's own view has hardly any colour itself
        public const float FogBrighter = 20f;       // a fogged picture is this much brighter than the game's own view,
        public const float FogSpread = 0.8f;        // spreads less than this share of it
        public const float FogSaturation = 0.7f;    // and has less than this share of its colour

        // ------------------------------------------------------------------ what fills the frame

        public static int Ray(int column, int row)
        {
            return row * Columns + column;
        }

        /// <summary>Where a ray crosses the frame, 0 to 1 across and up.</summary>
        public static void RayAt(int ray, out float x, out float y)
        {
            x = (ray % Columns + 0.5f) / Columns;
            y = (ray / Columns + 0.5f) / Rows;
        }

        private static bool Far(float metres, CineHit kind, float least)
        {
            return kind == CineHit.Sky || metres >= least;
        }

        /// <summary>
        /// Whether a frame is worth taking, from <see cref="Rays"/> rays cast across it: how far each went, what it met and
        /// which surface that was (a number of its own for each). A wall in the lens fails any shot; a shot that asks for
        /// depth must have it: sky over a skyline with no clutter before it, a street running clear down its middle with
        /// a side of it in the frame (the street with walls on both sides is the finder's to prefer), sky over a tall thing,
        /// trees with open ground in a park. False with why.
        /// </summary>
        public static bool Frame(CineStyle style, CineSubject subject, float[] metres, CineHit[] kinds, int[] surfaces, int treesSeen, out string why)
        {
            why = null;
            if (metres == null || kinds == null || surfaces == null || metres.Length < Rays || kinds.Length < Rays || surfaces.Length < Rays)
            {
                why = "the frame could not be measured";
                return false;
            }
            int near = 0, most = 0, ground = 0, solid = 0;
            for (int i = 0; i < Rays; i++)
            {
                ground += kinds[i] == CineHit.Ground ? 1 : 0;
                solid += kinds[i] == CineHit.Solid ? 1 : 0;
                if ((kinds[i] != CineHit.Solid && kinds[i] != CineHit.Tree) || metres[i] > NearWall)
                {
                    continue;   // the ground under a low lens and the subject itself are not a wall in it
                }
                near++;
                int same = 0;
                for (int k = 0; k < Rays; k++)
                {
                    same += (kinds[k] == CineHit.Solid || kinds[k] == CineHit.Tree) && metres[k] <= NearWall && surfaces[k] == surfaces[i] ? 1 : 0;
                }
                most = Math.Max(most, same);
            }
            if (most > NearShare * Rays)
            {
                why = "one surface within " + NearWall.ToString("0", CultureInfo.InvariantCulture) + " m fills " + Percent(most) + " of the frame";
                return false;
            }
            if (near > NearAll * Rays)
            {
                why = "things within " + NearWall.ToString("0", CultureInfo.InvariantCulture) + " m fill " + Percent(near) + " of the frame";
                return false;
            }
            switch (style)
            {
                case CineStyle.Establishing:
                {
                    int topSky = 0, clutter = 0, open = 0;
                    for (int c = 0; c < Columns; c++)
                    {
                        topSky += kinds[Ray(c, Rows - 1)] == CineHit.Sky ? 1 : 0;
                    }
                    for (int i = Columns; i < Rays; i++)   // every row but the foot, where the roof's own edge is
                    {
                        clutter += kinds[i] != CineHit.Sky && kinds[i] != CineHit.Ground && metres[i] <= Clutter ? 1 : 0;
                        open += Far(metres[i], kinds[i], FarLeast) ? 1 : 0;
                    }
                    if (topSky * 2 < Columns)
                    {
                        why = "no sky over the city (" + topSky + " of " + Columns + " rays along the top of the frame)";
                        return false;
                    }
                    if (clutter > 2)
                    {
                        why = "things on the roof within " + Clutter.ToString("0", CultureInfo.InvariantCulture) + " m stand in the view (" + clutter + " rays)";
                        return false;
                    }
                    if (open * 10 < (Rays - Columns) * 6)
                    {
                        why = "the view is closed: only " + Percent(open, Rays - Columns) + " of it goes " + FarLeast.ToString("0", CultureInfo.InvariantCulture) + " m or to the sky";
                        return false;
                    }
                    return true;
                }
                case CineStyle.Symmetry:
                case CineStyle.Compression:
                {
                    int middle = Rows / 2, centre = Columns / 2;
                    int ahead = Ray(centre, middle);
                    if (!Far(metres[ahead], kinds[ahead], StreetLeast))
                    {
                        why = "the street does not run clear down the middle of the frame (" + metres[ahead].ToString("0", CultureInfo.InvariantCulture) + " m)";
                        return false;
                    }
                    bool left = false, right = false;
                    for (int r = middle; r < Rows; r++)
                    {
                        for (int c = 0; c < 2; c++)
                        {
                            int l = Ray(c, r), q = Ray(Columns - 1 - c, r);
                            left |= (kinds[l] == CineHit.Solid || kinds[l] == CineHit.Tree) && metres[l] <= SideMost;
                            right |= (kinds[q] == CineHit.Solid || kinds[q] == CineHit.Tree) && metres[q] <= SideMost;
                        }
                    }
                    if (!left && !right)
                    {
                        why = "neither side of the street is in the frame";   // one side may be a park or the water, which no ray meets
                        return false;
                    }
                    return true;
                }
                case CineStyle.LowWide:
                {
                    int topSky = 0;
                    for (int c = 0; c < Columns; c++)
                    {
                        topSky += kinds[Ray(c, Rows - 1)] == CineHit.Sky ? 1 : 0;
                    }
                    if (topSky * 3 < Columns)
                    {
                        why = "no sky over it: it is not whole in the frame (" + topSky + " of " + Columns + " rays along the top)";
                        return false;
                    }
                    if (subject != CineSubject.Trees)
                    {
                        return true;
                    }
                    if (treesSeen < TreesLeast)
                    {
                        why = "only " + treesSeen + " tree(s) in sight of the lens";
                        return false;
                    }
                    if (ground * 100 < Rays * 15)
                    {
                        why = "no open ground before the trees (" + Percent(ground) + " of the frame)";
                        return false;
                    }
                    if (solid * 100 > Rays * 45)
                    {
                        why = "walls fill " + Percent(solid) + " of the frame";
                        return false;
                    }
                    return true;
                }
                default:
                    return true;
            }
        }

        private static string Percent(int rays, int of = Rays)
        {
            return (rays * 100 / Math.Max(1, of)).ToString(CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>
        /// The way to look at the city from where the player stands: of <paramref name="bearings"/> ways round the compass,
        /// the one with the most buildings within a cone of 30 degrees either side and between 120 m and 1300 m off, so a
        /// skyline never looks off the map's edge. False for a place with no city round it.
        /// </summary>
        public static bool DensestBearing(float[] dx, float[] dz, int count, int bearings, out int best, out int buildings)
        {
            best = -1;
            buildings = 0;
            count = Math.Min(count, Math.Min(dx != null ? dx.Length : 0, dz != null ? dz.Length : 0));
            double cone = Math.Cos(30.0 * Math.PI / 180.0);
            for (int b = 0; b < bearings; b++)
            {
                double turn = b * 2.0 * Math.PI / bearings;
                double wx = Math.Sin(turn), wz = Math.Cos(turn);
                int seen = 0;
                for (int i = 0; i < count; i++)
                {
                    double away = Math.Sqrt(dx[i] * (double)dx[i] + dz[i] * (double)dz[i]);
                    seen += away >= 120.0 && away <= 1300.0 && (dx[i] * wx + dz[i] * wz) / away >= cone ? 1 : 0;
                }
                if (seen > buildings)
                {
                    buildings = seen;
                    best = b;
                }
            }
            return best >= 0 && buildings >= 12;
        }

        /// <summary>
        /// The middle of a carriageway from where its lanes lie across it (metres to the right of a line along the street,
        /// negative to the left): half way between the outermost two, and half its width with half a lane beyond each.
        /// False with no lane.
        /// </summary>
        public static bool Carriageway(float[] across, int count, out float middle, out float half)
        {
            middle = half = 0f;
            count = Math.Min(count, across != null ? across.Length : 0);
            if (count <= 0)
            {
                return false;
            }
            float least = across[0], most = across[0];
            for (int i = 1; i < count; i++)
            {
                least = Math.Min(least, across[i]);
                most = Math.Max(most, across[i]);
            }
            middle = (least + most) * 0.5f;
            half = (most - least) * 0.5f + 1.75f;
            return true;
        }

        // ------------------------------------------------------------------ the air

        /// <summary>How deep a kind of shot looks, in metres: what its air must be clear for.</summary>
        public static float Depth(CineStyle style, float focus)
        {
            switch (style)
            {
                case CineStyle.Establishing: return 1000f;
                case CineStyle.Compression: return 500f;
                case CineStyle.Symmetry: return 300f;
                default: return Math.Max(60f, focus);
            }
        }

        /// <summary>
        /// The metres a ray goes in the fog of a picture. The game's fog is a ground mist a ray goes some 50 m in, made for a
        /// camera that looks down through it from above; a camera that stands in it looks along it and sees white. So the
        /// mist is lifted to what the shot's depth needs, and never thickened: a clearer air of the game's or the mod's look stays.
        /// </summary>
        public static float Haze(float game, float depth)
        {
            float wanted = Math.Max(HazeLeast, Math.Min(HazeMost, depth * HazeDepths));
            return game > 0f ? Math.Max(game, wanted) : 0f;   // no fog is left no fog
        }

        // ------------------------------------------------------------------ the light

        /// <summary>
        /// The light the game shows, from its own lights read back: day or golden with the sun on (golden: warm or low, and
        /// still strong enough to gild), the blue hour just after the sun goes out or before it comes on, else night.
        /// </summary>
        public static CineLight Light(in CineSeen seen)
        {
            if (seen.SunOn && seen.SunLux > 0f)
            {
                float share = seen.SunPeak > 0f ? seen.SunLux / seen.SunPeak : 1f;
                bool warm = seen.SunKelvin > 0f && seen.SunKelvin <= GoldenKelvin, low = seen.SunUp <= GoldenUp;
                return share >= GoldenShare && (warm || low) ? CineLight.Golden : CineLight.Midday;
            }
            float after = Wrap(seen.Hour - seen.SunOffHour), before = Wrap(seen.SunOnHour - seen.Hour);
            return after <= BlueHours || before <= BlueHours ? CineLight.Blue : CineLight.Night;
        }

        private static float Wrap(float hours)
        {
            hours %= 24f;
            return hours < 0f ? hours + 24f : hours;
        }

        /// <summary>The light's word in a picture's name, from what was read back: "midday" only round noon, else "day".</summary>
        public static string Word(CineLight light, float hour)
        {
            switch (light)
            {
                case CineLight.Midday: return hour >= 11f && hour <= 14f ? "midday" : "day";
                case CineLight.Golden: return "golden";
                case CineLight.Blue: return "blue";
                case CineLight.Night: return "night";
                default: return CineRules.NowWord;
            }
        }

        /// <summary>The light as shots.txt writes it: the sun, the moon, the lamps, the exposure and the air, as read back.</summary>
        public static string LightWords(in CineSeen seen)
        {
            if (!seen.Read)
            {
                return "the game's lights could not be read";
            }
            CultureInfo inv = CultureInfo.InvariantCulture;
            string sun = seen.SunOn
                ? "sun on, " + seen.SunLux.ToString("0", inv) + " lux" + (seen.SunPeak > 0f ? " of " + seen.SunPeak.ToString("0", inv) : string.Empty) + ", "
                  + seen.SunKelvin.ToString("0", inv) + " K, " + seen.SunUp.ToString("0", inv) + " degrees up"
                : "sun off";
            string moon = seen.MoonOn ? "moon on, " + seen.MoonLux.ToString("0", inv) + " lux" : "moon off";
            string air = seen.HazeGame <= 0f
                ? "no fog"
                : seen.HazeUsed > seen.HazeGame + 1f
                    ? "a ray goes " + seen.HazeUsed.ToString("0", inv) + " m in the fog (the game's mist here: " + seen.HazeGame.ToString("0", inv) + " m, reaching "
                      + seen.HazeHeight.ToString("0", inv) + " m up, lifted for a camera that stands in it)"
                    : "a ray goes " + seen.HazeGame.ToString("0", inv) + " m in the fog, as the game has it";
            return sun + "; " + moon + "; street lamps " + (seen.Lamps ? "on" : "off") + "; exposure "
                   + (float.IsNaN(seen.Exposure) ? "not a fixed one" : "EV " + seen.Exposure.ToString("0.0", inv)) + "; " + air;
        }

        // ------------------------------------------------------------------ the picture against the screen

        /// <summary>Whether the picture drawn into its own target is the picture the screen shows from the same place: false, with what differs.</summary>
        public static bool Same(in CineStat screen, in CineStat target, out string why)
        {
            why = null;
            if (screen.Count == 0 || target.Count == 0)
            {
                why = "one of the two was not read";
                return false;
            }
            if (Math.Abs(screen.Mean - target.Mean) > SameMean)
            {
                why = "brightness " + target.Mean.ToString("0", CultureInfo.InvariantCulture) + " against the screen's " + screen.Mean.ToString("0", CultureInfo.InvariantCulture);
            }
            else if (Math.Abs(screen.Spread - target.Spread) > Math.Max(SameSpread, screen.Spread * 0.2f))
            {
                why = "spread " + target.Spread.ToString("0", CultureInfo.InvariantCulture) + " against the screen's " + screen.Spread.ToString("0", CultureInfo.InvariantCulture);
            }
            else if (Math.Abs(screen.Saturation - target.Saturation) > Math.Max(SameSaturation, screen.Saturation * 0.25f))
            {
                why = "saturation " + target.Saturation.ToString("0.00", CultureInfo.InvariantCulture) + " against the screen's "
                      + screen.Saturation.ToString("0.00", CultureInfo.InvariantCulture);
            }
            return why == null;
        }

        /// <summary>
        /// What is wrong with a picture against the game's own view in the same light, or null: flat (its brightness hardly
        /// spreads), grey (it has lost the view's colour), fogged (brighter, flatter and paler at once).
        /// </summary>
        public static string Flags(in CineStat picture, in CineStat own)
        {
            if (picture.Count == 0)
            {
                return "not read";
            }
            if (picture.Spread < FlatSpread || (own.Count > 0 && picture.Spread < own.Spread * FlatShare))
            {
                return "flat";
            }
            if (own.Count == 0)
            {
                return null;
            }
            if (picture.Mean > own.Mean + FogBrighter && picture.Spread < own.Spread * FogSpread && picture.Saturation < own.Saturation * FogSaturation)
            {
                return "fogged";
            }
            return own.Saturation >= GreyLeast && picture.Saturation < own.Saturation * GreyShare ? "grey" : null;
        }

        /// <summary>
        /// Whether the screen's own frame is saved in place of the target's: when the two are not one picture, or when the
        /// target's is flagged and the screen's is not.
        /// </summary>
        public static bool UseScreen(in CineStat screen, in CineStat target, in CineStat own)
        {
            if (screen.Count == 0)
            {
                return false;
            }
            return target.Count == 0 || !Same(screen, target, out _) || (Flags(target, own) != null && Flags(screen, own) == null);
        }

        /// <summary>The whole number the screen is drawn larger by so that a picture read from it has 2160 lines or more: 2 on a 1080 line screen.</summary>
        public static int SuperSize(int screenHeight)
        {
            return screenHeight <= 0 ? 1 : Math.Max(1, Math.Min(4, (CineRules.Height + screenHeight - 1) / screenHeight));
        }
    }
}
