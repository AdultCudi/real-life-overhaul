using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>What a performance part replaces. One part per slot on a car.</summary>
    public enum PartSlot
    {
        Engine,
        Exhaust,
        Turbo,
        Gearbox,
        Brakes,
        Tyres,
        Suspension,
        Weight
    }

    /// <summary>How the paint catches the light. Set through the vehicle shader's own gloss and edge-colour properties.</summary>
    public enum PaintFinish
    {
        Gloss,      // as the game paints a car
        Satin,
        Matte,
        Metallic,
        Pearl
    }

    /// <summary>The colour of the car's lamps (the shader's emission colour and the headlamp's own light).</summary>
    public enum LampTint
    {
        Stock,
        Warm,
        Ice,
        Yellow
    }

    /// <summary>
    /// One performance part. Gains are factors on what the car does without it: <see cref="Accel"/> divides the
    /// 0-100 km/h time, <see cref="Top"/> multiplies the top speed, <see cref="Brake"/> the brake torque,
    /// <see cref="Grip"/> the tyres' grip in the dry and <see cref="WetGrip"/> in the wet, <see cref="Stiff"/> the
    /// springs and dampers, <see cref="Mass"/> the weight. <see cref="Ride"/> is metres of ride height.
    /// The costs: <see cref="Wear"/> is how much faster the car wears (for a servicing feature to read),
    /// <see cref="Insurance"/> what it adds to a premium, <see cref="Loud"/> 2 is not street legal.
    /// How it sounds with the recorded car sounds (Driving/Sound): <see cref="Loud"/> is the exhaust's sound, <see cref="Boost"/>
    /// the turbo's, and <see cref="Sound"/> the line the shop says it with.
    /// </summary>
    public sealed class PartDef
    {
        public string Id;
        public PartSlot Slot;
        public string Name;          // locale key
        public float Price;          // times the price base of the car's kind
        public float Accel = 1f;
        public float Top = 1f;
        public float Brake = 1f;
        public float Grip = 1f;
        public float WetGrip = 1f;
        public float Stiff = 1f;
        public float Mass = 1f;
        public float Ride;
        public float Wear;
        public float Insurance;
        public int Loud;
        public float Pitch;          // added to the engine note's pitch
        public bool UtilityOnly;     // a lift kit is for SUVs, pickups and vans
        public int Boost;            // a turbo's size for its sound: 1 a turbocharger, 2 a big turbo (Driving/Sound/CarSoundRules.Turbo)
        public string Sound;         // locale key: how the part sounds, on the shop's parts page
    }

    /// <summary>Everything fitted to one car, added up. Neutral when nothing is.</summary>
    public struct GarageModifiers
    {
        public float Acceleration;   // divides the 0-100 km/h time
        public float TopSpeed;       // multiplies the top speed
        public float Brakes;
        public float Grip;
        public float WetGrip;
        public float Stiffness;
        public float Mass;
        public float Ride;           // metres, + up
        public float Wear;           // 0 = as stock; 0.25 = wears a quarter faster
        public float Insurance;      // 0 = as stock; 0.10 = a tenth dearer
        public int Loudness;         // 0 stock, 1 sporty, 2 not street legal
        public float Pitch;

        public static GarageModifiers None => new GarageModifiers
        {
            Acceleration = 1f, TopSpeed = 1f, Brakes = 1f, Grip = 1f, WetGrip = 1f, Stiffness = 1f, Mass = 1f
        };

        public bool ChangesPerformance => Acceleration != 1f || TopSpeed != 1f;
    }

    /// <summary>
    /// What one car carries from the custom shop: paint, finish, lamps, underglow, fitted parts, one
    /// performance part per slot, and what was spent on it. Kept in the save's modData as one line per
    /// vehicle id (<see cref="GarageState"/>); the vehicle itself is never changed in the save.
    /// </summary>
    public sealed class GarageBuild
    {
        public int Paint = -1;                 // 0xRRGGBB, -1 = the colour the car was bought in
        public PaintFinish Finish;
        public LampTint Lamps;
        public int Glow = -1;                  // 0xRRGGBB, -1 = none
        public bool Spoiler;
        public bool Rack;
        public bool Bar;
        public string Wheels = string.Empty;   // a wheel prop id, empty = the car's own
        public readonly string[] Parts = new string[GarageCatalogue.Slots];   // part id per slot, null = stock
        public int Spent;                      // everything paid at the shop for this car

        public bool IsStock => Paint < 0 && Finish == PaintFinish.Gloss && Lamps == LampTint.Stock && Glow < 0 && !Spoiler && !Rack && !Bar
                               && Wheels.Length == 0 && !HasParts;

        public bool HasParts
        {
            get
            {
                for (int i = 0; i < Parts.Length; i++)
                {
                    if (Parts[i] != null)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public GarageBuild Copy()
        {
            var copy = new GarageBuild();
            copy.Take(this);
            return copy;
        }

        public void Take(GarageBuild other)
        {
            Paint = other.Paint;
            Finish = other.Finish;
            Lamps = other.Lamps;
            Glow = other.Glow;
            Spoiler = other.Spoiler;
            Rack = other.Rack;
            Bar = other.Bar;
            Wheels = other.Wheels;
            Spent = other.Spent;
            for (int i = 0; i < Parts.Length; i++)
            {
                Parts[i] = other.Parts[i];
            }
        }

        public GarageModifiers Modifiers()
        {
            GarageModifiers m = GarageModifiers.None;
            for (int i = 0; i < Parts.Length; i++)
            {
                PartDef part = GarageCatalogue.Part(Parts[i]);
                if (part == null)
                {
                    continue;
                }
                m.Acceleration *= part.Accel;
                m.TopSpeed *= part.Top;
                m.Brakes *= part.Brake;
                m.Grip *= part.Grip;
                m.WetGrip *= part.WetGrip;
                m.Stiffness *= part.Stiff;
                m.Mass *= part.Mass;
                m.Ride += part.Ride;
                m.Wear += part.Wear;
                m.Insurance += part.Insurance;
                m.Loudness = part.Loud > m.Loudness ? part.Loud : m.Loudness;
                m.Pitch += part.Pitch;
            }
            // A lighter car is quicker by as much as it is lighter. It is asked of the model as a quicker time, like
            // every other part: the model finds the power for a target, so weight alone would be cancelled out by it.
            m.Acceleration /= m.Mass;
            // However the parts stack, a car stays a car: these are the most the model is asked for.
            m.Acceleration = m.Acceleration > GarageCatalogue.MaxAcceleration ? GarageCatalogue.MaxAcceleration : m.Acceleration;
            m.TopSpeed = m.TopSpeed > GarageCatalogue.MaxTopSpeed ? GarageCatalogue.MaxTopSpeed : m.TopSpeed;
            return m;
        }

        // p=RRGGBB;f=2;l=1;g=RRGGBB;k=srb;w=wheel_5spoke;0=engine_2;...;v=1234
        public string Write()
        {
            var text = new StringBuilder(96);
            if (Paint >= 0)
            {
                text.Append("p=").Append(Paint.ToString("X6", CultureInfo.InvariantCulture)).Append(';');
            }
            if (Finish != PaintFinish.Gloss)
            {
                text.Append("f=").Append((int)Finish).Append(';');
            }
            if (Lamps != LampTint.Stock)
            {
                text.Append("l=").Append((int)Lamps).Append(';');
            }
            if (Glow >= 0)
            {
                text.Append("g=").Append(Glow.ToString("X6", CultureInfo.InvariantCulture)).Append(';');
            }
            if (Spoiler || Rack || Bar)
            {
                text.Append("k=").Append(Spoiler ? "s" : string.Empty).Append(Rack ? "r" : string.Empty).Append(Bar ? "b" : string.Empty).Append(';');
            }
            if (Wheels.Length > 0)
            {
                text.Append("w=").Append(Wheels).Append(';');
            }
            for (int i = 0; i < Parts.Length; i++)
            {
                if (Parts[i] != null)
                {
                    text.Append(i).Append('=').Append(Parts[i]).Append(';');
                }
            }
            text.Append("v=").Append(Spent.ToString(CultureInfo.InvariantCulture));
            return text.ToString();
        }

        /// <summary>Reads a line written by <see cref="Write"/>. Anything it does not know is skipped, so an older or newer line still loads.</summary>
        public static GarageBuild Read(string line)
        {
            var build = new GarageBuild();
            if (string.IsNullOrEmpty(line))
            {
                return build;
            }
            foreach (string field in line.Split(';'))
            {
                int at = field.IndexOf('=');
                if (at <= 0 || at == field.Length - 1)
                {
                    continue;
                }
                string key = field.Substring(0, at);
                string value = field.Substring(at + 1);
                switch (key)
                {
                    case "p": build.Paint = Hex(value); break;
                    case "g": build.Glow = Hex(value); break;
                    case "f":
                        if (int.TryParse(value, out int finish) && finish >= 0 && finish <= (int)PaintFinish.Pearl)
                        {
                            build.Finish = (PaintFinish)finish;
                        }
                        break;
                    case "l":
                        if (int.TryParse(value, out int lamps) && lamps >= 0 && lamps <= (int)LampTint.Yellow)
                        {
                            build.Lamps = (LampTint)lamps;
                        }
                        break;
                    case "k":
                        build.Spoiler = value.IndexOf('s') >= 0;
                        build.Rack = value.IndexOf('r') >= 0;
                        build.Bar = value.IndexOf('b') >= 0;
                        break;
                    case "w": build.Wheels = value; break;
                    case "v":
                        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out build.Spent);
                        break;
                    default:
                        if (int.TryParse(key, out int slot) && slot >= 0 && slot < build.Parts.Length)
                        {
                            PartDef part = GarageCatalogue.Part(value);
                            if (part != null && (int)part.Slot == slot)
                            {
                                build.Parts[slot] = part.Id;
                            }
                        }
                        break;
                }
            }
            return build;
        }

        private static int Hex(string value)
        {
            return int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int colour) && colour >= 0 && colour <= 0xFFFFFF ? colour : -1;
        }
    }

    /// <summary>
    /// What the custom shop sells: the paint palette, the finishes, the performance parts with their effects
    /// and trade-offs, and what each costs for each kind of car. Game-free: nothing here touches the engine.
    /// </summary>
    public static class GarageCatalogue
    {
        public const int Slots = 8;                    // PartSlot values
        public const float MaxAcceleration = 1.6f;     // the quickest a fully built car is asked to be, against stock
        public const float MaxTopSpeed = 1.25f;
        public const float Resale = 0.5f;              // the share of what was spent that a build adds to the car's worth
        public const float MaxLower = 0.05f;           // metres a car may be lowered, and raised
        public const float MaxRaise = 0.05f;

        /// <summary>The paint chart: six rows of seven, light to dark across each family.</summary>
        public static readonly int[] Palette =
        {
            0xF4F4F2, 0xD9DADB, 0xB3B6BA, 0x8A8E93, 0x5B5F64, 0x2E3033, 0x0C0D0E,   // whites, silvers, blacks
            0xE8D9B5, 0xC9B07A, 0xA98C55, 0x7A5C34, 0x5A3E22, 0x3A2616, 0x8C7B6B,   // creams, golds, browns
            0xF2C200, 0xF28C00, 0xE85D04, 0xD62828, 0xA4161A, 0x6A040F, 0xE56B6F,   // yellows, oranges, reds
            0xB8E0A0, 0x6BBF59, 0x2D8A3E, 0x1B5E3A, 0x0F3D2E, 0x556B2F, 0x9ACD32,   // greens
            0xA8DADC, 0x48CAE4, 0x0096C7, 0x1565C0, 0x1D3557, 0x0B1F3A, 0x5E60CE,   // blues
            0xE0AAFF, 0x9D4EDD, 0x6A0DAD, 0x3C096C, 0xFF70A6, 0xD81B60, 0x7B2D43    // purples and pinks
        };

        /// <summary>Colours an underglow kit comes in.</summary>
        public static readonly int[] GlowColours = { 0x00B4FF, 0x7A3CFF, 0xFF2D95, 0xFF3B30, 0xFF9500, 0x34C759, 0xFFFFFF };

        public static readonly PartDef[] Parts =
        {
            new PartDef { Id = "engine_1", Slot = PartSlot.Engine, Name = "reallife_garage_part_engine_1", Price = 1.0f, Accel = 1.06f, Top = 1.02f, Wear = 0.05f },
            new PartDef { Id = "engine_2", Slot = PartSlot.Engine, Name = "reallife_garage_part_engine_2", Price = 2.2f, Accel = 1.12f, Top = 1.04f, Wear = 0.12f },
            new PartDef { Id = "engine_3", Slot = PartSlot.Engine, Name = "reallife_garage_part_engine_3", Price = 4.0f, Accel = 1.20f, Top = 1.07f, Wear = 0.25f, Insurance = 0.10f },

            new PartDef { Id = "exhaust_sport", Slot = PartSlot.Exhaust, Name = "reallife_garage_part_exhaust_sport", Price = 0.5f, Accel = 1.03f, Top = 1.01f, Loud = 1, Pitch = 0.05f,
                Sound = "reallife_garage_sound_exhaust_sport" },
            new PartDef { Id = "exhaust_straight", Slot = PartSlot.Exhaust, Name = "reallife_garage_part_exhaust_straight", Price = 0.9f, Accel = 1.05f, Top = 1.02f, Loud = 2, Pitch = 0.12f,
                Sound = "reallife_garage_sound_exhaust_straight" },

            new PartDef { Id = "turbo_1", Slot = PartSlot.Turbo, Name = "reallife_garage_part_turbo_1", Price = 2.5f, Accel = 1.10f, Top = 1.05f, Wear = 0.10f,
                Boost = 1, Sound = "reallife_garage_sound_turbo_1" },
            new PartDef { Id = "turbo_2", Slot = PartSlot.Turbo, Name = "reallife_garage_part_turbo_2", Price = 4.5f, Accel = 1.18f, Top = 1.08f, Wear = 0.22f, Insurance = 0.10f,
                Boost = 2, Sound = "reallife_garage_sound_turbo_2" },

            new PartDef { Id = "gear_short", Slot = PartSlot.Gearbox, Name = "reallife_garage_part_gear_short", Price = 1.2f, Accel = 1.07f, Top = 0.92f },
            new PartDef { Id = "gear_long", Slot = PartSlot.Gearbox, Name = "reallife_garage_part_gear_long", Price = 1.2f, Accel = 0.94f, Top = 1.06f },

            new PartDef { Id = "brakes_sport", Slot = PartSlot.Brakes, Name = "reallife_garage_part_brakes_sport", Price = 0.8f, Brake = 1.25f },
            new PartDef { Id = "brakes_race", Slot = PartSlot.Brakes, Name = "reallife_garage_part_brakes_race", Price = 1.8f, Brake = 1.50f, Wear = 0.05f },

            new PartDef { Id = "tyres_allweather", Slot = PartSlot.Tyres, Name = "reallife_garage_part_tyres_allweather", Price = 0.7f, WetGrip = 1.15f },
            new PartDef { Id = "tyres_sport", Slot = PartSlot.Tyres, Name = "reallife_garage_part_tyres_sport", Price = 0.6f, Grip = 1.10f, WetGrip = 0.90f },
            new PartDef { Id = "tyres_semi", Slot = PartSlot.Tyres, Name = "reallife_garage_part_tyres_semi", Price = 1.2f, Grip = 1.20f, WetGrip = 0.75f, Wear = 0.08f },

            new PartDef { Id = "susp_sport", Slot = PartSlot.Suspension, Name = "reallife_garage_part_susp_sport", Price = 0.9f, Stiff = 1.25f, Ride = -0.02f },
            new PartDef { Id = "susp_coilover", Slot = PartSlot.Suspension, Name = "reallife_garage_part_susp_coilover", Price = 1.8f, Stiff = 1.50f, Ride = -0.04f },
            new PartDef { Id = "susp_lift", Slot = PartSlot.Suspension, Name = "reallife_garage_part_susp_lift", Price = 1.0f, Stiff = 1.10f, Ride = 0.04f, UtilityOnly = true },

            new PartDef { Id = "weight_1", Slot = PartSlot.Weight, Name = "reallife_garage_part_weight_1", Price = 1.0f, Mass = 0.95f },
            new PartDef { Id = "weight_2", Slot = PartSlot.Weight, Name = "reallife_garage_part_weight_2", Price = 2.5f, Mass = 0.90f, Insurance = 0.05f }
        };

        public static PartDef Part(string id)
        {
            if (id == null)
            {
                return null;
            }
            for (int i = 0; i < Parts.Length; i++)
            {
                if (Parts[i].Id == id)
                {
                    return Parts[i];
                }
            }
            return null;
        }

        public static string SlotName(PartSlot slot)
        {
            switch (slot)
            {
                case PartSlot.Engine: return "reallife_garage_slot_engine";
                case PartSlot.Exhaust: return "reallife_garage_slot_exhaust";
                case PartSlot.Turbo: return "reallife_garage_slot_turbo";
                case PartSlot.Gearbox: return "reallife_garage_slot_gearbox";
                case PartSlot.Brakes: return "reallife_garage_slot_brakes";
                case PartSlot.Tyres: return "reallife_garage_slot_tyres";
                case PartSlot.Suspension: return "reallife_garage_slot_suspension";
                default: return "reallife_garage_slot_weight";
            }
        }

        public static string FinishName(PaintFinish finish)
        {
            switch (finish)
            {
                case PaintFinish.Satin: return "reallife_garage_finish_satin";
                case PaintFinish.Matte: return "reallife_garage_finish_matte";
                case PaintFinish.Metallic: return "reallife_garage_finish_metallic";
                case PaintFinish.Pearl: return "reallife_garage_finish_pearl";
                default: return "reallife_garage_finish_gloss";
            }
        }

        public static string LampName(LampTint tint)
        {
            switch (tint)
            {
                case LampTint.Warm: return "reallife_garage_lamps_warm";
                case LampTint.Ice: return "reallife_garage_lamps_ice";
                case LampTint.Yellow: return "reallife_garage_lamps_yellow";
                default: return "reallife_garage_lamps_stock";
            }
        }

        // ---------------------------------------------------------------- prices

        /// <summary>What a stage 1 engine tune costs on this kind of car; every performance part is a multiple of it.</summary>
        public static int PartBase(CarKind kind)
        {
            switch (kind)
            {
                case CarKind.Economy: return 1000;
                case CarKind.Saloon: return 1800;
                case CarKind.Luxury: return 4500;
                case CarKind.Sports: return 6000;
                case CarKind.Utility: return 2500;
                case CarKind.Van: return 2000;
                case CarKind.Truck: return 3500;
                default: return 1500;
            }
        }

        /// <summary>Bodywork costs more on a bigger or dearer car.</summary>
        public static float BodyFactor(CarKind kind)
        {
            switch (kind)
            {
                case CarKind.Saloon: return 1.2f;
                case CarKind.Luxury:
                case CarKind.Sports:
                case CarKind.Truck: return 2.0f;
                case CarKind.Utility: return 1.4f;
                case CarKind.Van: return 1.5f;
                default: return 1.0f;
            }
        }

        public static int PartPrice(PartDef part, CarKind kind)
        {
            return part == null ? 0 : Round(Prices.At(PartBase(kind) * part.Price));   // at today's prices
        }

        public static int PaintPrice(CarKind kind, bool custom)
        {
            return Round((custom ? 900f : 600f) * BodyFactor(kind));
        }

        public static int FinishPrice(PaintFinish finish, CarKind kind)
        {
            float price;
            switch (finish)
            {
                case PaintFinish.Satin: price = 300f; break;
                case PaintFinish.Matte: price = 500f; break;
                case PaintFinish.Metallic: price = 700f; break;
                case PaintFinish.Pearl: price = 1200f; break;
                default: price = 150f; break;   // back to plain gloss: a respray's clear coat
            }
            return Round(price * BodyFactor(kind));
        }

        public static int LampPrice => Prices.At(150);
        public static int GlowPrice => Prices.At(400);
        public static int SpoilerPrice => Prices.At(450);
        public static int RackPrice => Prices.At(350);
        public static int BarPrice => Prices.At(600);
        public static int WheelsPrice => Prices.At(1200);
        public static int RemovalPrice => Prices.At(50);   // taking a fitted part off again

        /// <summary>
        /// What it costs to turn one build into another: every choice that differs is paid for, taking a
        /// thing off costs a little labour, and nothing already on the car is paid for twice.
        /// </summary>
        public static int Price(GarageBuild from, GarageBuild to, CarKind kind, bool paletteColour)
        {
            int total = 0;
            if (to.Paint != from.Paint)
            {
                total += to.Paint < 0 ? PaintPrice(kind, false) : PaintPrice(kind, !paletteColour);
            }
            if (to.Finish != from.Finish)
            {
                total += FinishPrice(to.Finish, kind);
            }
            if (to.Lamps != from.Lamps)
            {
                total += to.Lamps == LampTint.Stock ? RemovalPrice : LampPrice;
            }
            if (to.Glow != from.Glow)
            {
                total += to.Glow < 0 ? RemovalPrice : from.Glow < 0 ? GlowPrice : GlowPrice / 4;   // another colour is a new set of tubes only
            }
            total += Fitted(from.Spoiler, to.Spoiler, SpoilerPrice);
            total += Fitted(from.Rack, to.Rack, RackPrice);
            total += Fitted(from.Bar, to.Bar, BarPrice);
            if (to.Wheels != from.Wheels)
            {
                total += to.Wheels.Length == 0 ? RemovalPrice : WheelsPrice;
            }
            for (int i = 0; i < Slots; i++)
            {
                if (to.Parts[i] != from.Parts[i])
                {
                    total += to.Parts[i] == null ? RemovalPrice : PartPrice(Part(to.Parts[i]), kind);
                }
            }
            return total;
        }

        public static bool InPalette(int colour)
        {
            for (int i = 0; i < Palette.Length; i++)
            {
                if (Palette[i] == colour)
                {
                    return true;
                }
            }
            return false;
        }

        private static int Fitted(bool was, bool now, int price)
        {
            return was == now ? 0 : now ? price : RemovalPrice;
        }

        private static int Round(float price)
        {
            return (int)(price / 10f + 0.5f) * 10;
        }
    }
}
