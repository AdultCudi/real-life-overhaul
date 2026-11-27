using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>One car model of the mod's own: what the game is told about it, and where its parts go on the game car it is built on.</summary>
    public sealed class ModCarDef
    {
        public string Id;              // "haldenpip": the prefab's name to the game, and the stem of every asset of the bundle
        public string TypeName;        // the vehicle type's name, which is also its text key
        public string Donor;           // the game's own type the car is built on, and the name it is saved under
        public string KindKey;         // what sort of car, for the stand's list
        public int Price;              // the game's money, as its own cars are priced
        public float MaxFuel;          // litres
        public int Cargo;              // boxes
        public int MaxSpeed;           // the game's own figure, 50 to 100 on its cars
        public float EnginePower;      // the game's own figure (kW), 35 to 200 on its cars
        public float BrakeForce;       // the game's own figure (N m), 2500 to 10000 on its cars
        public int TurnRadius;         // the game's own figure, 28 to 65 on its cars
        public float DamageIntensity;  // 0.16 to 0.64 on the game's cars
        public bool FitsHandTruck;
        public bool Luxury;
        public bool FitsFlatbed;       // takes the game's flatbed trolley, as its vans and pickups do
        public bool TaxDeductible;     // a working vehicle to the game's tax, as its vans and pickups are
        public bool AutoPark;          // parks itself, as the game's UMC Nunavut does
        public int Destinations = 1;   // how many stops a delivery in it may make: 1 on the game's cars, 2 on its vans and pickups
        public float AxleFront, AxleRear;   // metres along the car from its middle (Unity z)
        public float TrackHalf;             // a wheel's centre from the middle line
        public float WheelRadius, WheelWidth;
        public float HalfWidth;             // the body without its mirrors
        public float Front, Rear;           // the bumpers' tips (Unity z)
        public float Height;                // the roof
        public float BonnetHeight;          // where a broken engine smokes from

        public float Wheelbase => AxleFront - AxleRear;
        public float Length => Front - Rear;
    }

    /// <summary>A car of the mod's in a save: which vehicle it is, the model it really is, and the game car it is written as.</summary>
    public readonly struct CarStandIn
    {
        public readonly string Id, TypeName, Donor;

        public CarStandIn(string id, string typeName, string donor)
        {
            Id = id;
            TypeName = typeName;
            Donor = donor;
        }
    }

    /// <summary>
    /// The cars made for the mod, game-free so the offline checks read the same table (EconomyCheck --mod-cars). A car is a
    /// vehicle type of its own while the game runs, registered through the game's mod interface, and is written into the save
    /// as the game car it is built on, with a line of the mod's that says which vehicles to turn back at the next load
    /// (<see cref="Write"/>, <see cref="Back"/>): a save opened without the mod, or with an older one, holds only cars the game
    /// knows, where an unknown type would fault the game's net worth, tax and moving code (PlayerHelper.GetTotalAssetsWorth and
    /// others read VehicleInstance.VehicleType without asking whether it is there). Nothing here runs per frame.
    /// </summary>
    public static class ModCarCatalogue
    {
        public const string Bundle = "Bundles/reallife_cars";
        public const string BundleName = "reallife" + "_cars";            // (in two parts, as Core/BundleShaderRules writes a bundle's name)
        public const string Loose = "Bundles/cars_newer";                 // the same meshes and pictures as plain files, read while the bundle is not built
        public const string Key = "reallife:garage_cars";                 // the save line of stand-ins
        public const string Prefix = "reallife:vehicletype_";
        public const string GamePrefix = "ba:vehicletype_";
        public const string PrefabFolder = "Prefabs/Vehicles/PlayerVehicles/";   // where the game looks a vehicle's prefab up (PrefabHelper, VehicleHelper)

        // What the game's own cars span (read from its vehicle types, build 3682): a car of the mod's stays inside.
        public const int GamePriceLeast = 2500, GamePriceMost = 720000;
        public const int GameSpeedLeast = 50, GameSpeedMost = 100;
        public const float GamePowerLeast = 35f, GamePowerMost = 200f;
        public const float GameBrakeLeast = 2500f, GameBrakeMost = 10000f;
        public const int GameTurnLeast = 28, GameTurnMost = 65;
        public const float GameFuelLeast = 55f, GameFuelMost = 378f;
        public const int GameCargoLeast = 3, GameCargoMost = 60;
        public const float GameDamageLeast = 0.16f, GameDamageMost = 0.64f;
        public const float GameWheelbaseLeast = 2.2f, GameWheelbaseMost = 5.0f;   // metres
        public const float GameHalfWidthLeast = 0.80f, GameHalfWidthMost = 1.30f;

        public static readonly string[] MeshParts = { "body", "wheel_left", "wheel_right", "collider" };
        // The game's vehicle shader's slots, and the texture of the bundle that goes in each (the stem, then the suffix).
        public static readonly string[] TextureSlots =
        {
            "Texture2D_3d0babbacbb04731a784ae3943688e65", "Texture2D_196dcf5fb385498aa0919d56d7cd684d", "Texture2D_9b32f246716c41009146a43593b55e3f",
            "_EmissionTexture", "_EmissionMask", "_IndicatorsMask", "_DirtDiffuse", "_Dirt_Met_AO_Smooth"
        };
        public static readonly string[] TextureParts = { "d", "mask", "n", "e", "em", "im", "dirt", "dirtmask" };
        // The game's vehicle shader paints where smoothstep(_Min, _Max, the colour texture's alpha) says so. The cars' textures carry a ramp across
        // exactly this window at every paint edge (0.0485 on the edge), so the shader's own step smooths the edge.
        public const float PaintFrom = 0f, PaintTo = 0.097f;
        public static readonly bool[] TextureLinear = { false, true, true, false, true, true, false, true };   // data, not colour

        public static readonly ModCarDef HaldenPip = new ModCarDef
        {
            Id = "haldenpip", TypeName = Prefix + "haldenpip", Donor = GamePrefix + "honzamimic", KindKey = "reallife_cars_kind_city",
            // Between the Honza Mimic (2,500: slower, more room) and the UMC Desert van (8,000) and Vord Tiara Vic (11,000: faster, half as much room again).
            Price = 6500, MaxFuel = 42f, Cargo = 6, MaxSpeed = 65, EnginePower = 52f, BrakeForce = 3000f, TurnRadius = 30, DamageIntensity = 0.56f,
            FitsHandTruck = true, Luxury = false,
            AxleFront = 1.21f, AxleRear = -1.27f, TrackHalf = 0.755f, WheelRadius = 0.30f, WheelWidth = 0.20f,
            HalfWidth = 0.885f, Front = 1.95f, Rear = -1.95f, Height = 1.50f, BonnetHeight = 0.86f
        };

        public static readonly ModCarDef HaldenMarlow = new ModCarDef
        {
            Id = "haldenmarlow", TypeName = Prefix + "haldenmarlow", Donor = GamePrefix + "vordtiaravic", KindKey = "reallife_cars_kind_saloon",
            // The family saloon in the gap above the Vord Tiara Vic (11,000: slower, a little more room) and far under the BIMA 320 (48,000: more power).
            Price = 19000, MaxFuel = 62f, Cargo = 10, MaxSpeed = 75, EnginePower = 95f, BrakeForce = 4000f, TurnRadius = 35, DamageIntensity = 0.48f,
            FitsHandTruck = true, Luxury = false,
            AxleFront = 1.42f, AxleRear = -1.30f, TrackHalf = 0.78f, WheelRadius = 0.32f, WheelWidth = 0.215f,
            HalfWidth = 0.915f, Front = 2.35f, Rear = -2.35f, Height = 1.44f, BonnetHeight = 0.86f
        };

        public static readonly ModCarDef HaldenMarlowEstate = new ModCarDef
        {
            Id = "haldenmarlowestate", TypeName = Prefix + "haldenmarlowestate", Donor = GamePrefix + "vordtiaravic", KindKey = "reallife_cars_kind_estate",
            // The Marlow with a load bay: 16 boxes where the saloon takes 10, a little slower for the weight. More room than any car under the
            // Vord V150 pickup (44,000: 20 boxes, faster, stronger) without being a van.
            Price = 24000, MaxFuel = 62f, Cargo = 16, MaxSpeed = 72, EnginePower = 95f, BrakeForce = 4000f, TurnRadius = 35, DamageIntensity = 0.48f,
            FitsHandTruck = true, Luxury = false,
            AxleFront = 1.42f, AxleRear = -1.30f, TrackHalf = 0.78f, WheelRadius = 0.32f, WheelWidth = 0.215f,
            HalfWidth = 0.915f, Front = 2.35f, Rear = -2.35f, Height = 1.44f, BonnetHeight = 0.86f
        };

        public static readonly ModCarDef HaldenDrover = new ModCarDef
        {
            Id = "haldendrover", TypeName = Prefix + "haldendrover", Donor = GamePrefix + "vordv150", KindKey = "reallife_cars_kind_pickup",
            // A working pickup under the Vord V150 (44,000: faster, stronger, 20 boxes): 18 boxes in an open bed, a flatbed trolley and two stops
            // a delivery as a van has, for two thirds of the money. Its power stays under the BIMA 320's so that saloon keeps its buyers.
            Price = 29000, MaxFuel = 80f, Cargo = 18, MaxSpeed = 70, EnginePower = 98f, BrakeForce = 4500f, TurnRadius = 40, DamageIntensity = 0.32f,
            FitsHandTruck = true, Luxury = false, FitsFlatbed = true, TaxDeductible = true, Destinations = 2,
            AxleFront = 1.70f, AxleRear = -1.40f, TrackHalf = 0.79f, WheelRadius = 0.40f, WheelWidth = 0.27f,
            HalfWidth = 0.94f, Front = 2.60f, Rear = -2.60f, Height = 1.82f, BonnetHeight = 1.17f
        };

        public static readonly ModCarDef HaldenCarrier = new ModCarDef
        {
            Id = "haldencarrier", TypeName = Prefix + "haldencarrier", Donor = GamePrefix + "umcdesert", KindKey = "reallife_cars_kind_van",
            // The van between the old UMC Desert (8,000: 14 boxes, slow) and the Mersaidi Dash (48,500: 24 boxes, faster): 20 boxes for
            // under half the Dash's money, slower than any saloon.
            Price = 21000, MaxFuel = 120f, Cargo = 20, MaxSpeed = 60, EnginePower = 68f, BrakeForce = 3000f, TurnRadius = 38, DamageIntensity = 0.32f,
            FitsHandTruck = true, Luxury = false, FitsFlatbed = true, TaxDeductible = true, Destinations = 2,
            AxleFront = 1.80f, AxleRear = -1.65f, TrackHalf = 0.83f, WheelRadius = 0.40f, WheelWidth = 0.26f,
            HalfWidth = 0.985f, Front = 2.65f, Rear = -2.65f, Height = 2.05f, BonnetHeight = 1.17f
        };

        public static readonly ModCarDef HaldenTarn = new ModCarDef
        {
            Id = "haldentarn", TypeName = Prefix + "haldentarn", Donor = GamePrefix + "umcnunavut", KindKey = "reallife_cars_kind_offroad",
            // The off-roader under the UMC Nunavut (65,000: faster, stronger, 18 boxes): it parks itself and takes 16 boxes and a flatbed trolley,
            // and is slower than the saloons. The spare wheel on its tailgate is part of its length (the rear is 2.66 m from the middle).
            Price = 38000, MaxFuel = 95f, Cargo = 16, MaxSpeed = 65, EnginePower = 120f, BrakeForce = 5000f, TurnRadius = 36, DamageIntensity = 0.24f,
            FitsHandTruck = true, Luxury = false, FitsFlatbed = true, TaxDeductible = false, AutoPark = true, Destinations = 2,
            AxleFront = 1.55f, AxleRear = -1.40f, TrackHalf = 0.83f, WheelRadius = 0.42f, WheelWidth = 0.28f,
            HalfWidth = 0.98f, Front = 2.45f, Rear = -2.66f, Height = 1.90f, BonnetHeight = 1.22f
        };

        public static readonly ModCarDef[] All = { HaldenPip, HaldenMarlow, HaldenMarlowEstate, HaldenDrover, HaldenCarrier, HaldenTarn };

        /// <summary>True for a vehicle type of the mod's own.</summary>
        public static bool IsOurs(string typeName)
        {
            return typeName != null && typeName.StartsWith(Prefix, StringComparison.Ordinal);
        }

        public static ModCarDef Find(string typeName)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].TypeName == typeName)
                {
                    return All[i];
                }
            }
            return null;
        }

        /// <summary>A mesh of a car in the bundle, by its part ("haldenpip_body").</summary>
        public static string MeshName(ModCarDef def, string part)
        {
            return def.Id + "_" + part;
        }

        public static string TextureName(ModCarDef def, string part)
        {
            return def.Id + "_" + part;
        }

        /// <summary>Every asset the code asks the bundle for, by name: what the offline check holds the bundle's list against.</summary>
        public static List<string> AssetNames()
        {
            var names = new List<string>(All.Length * 12);
            for (int i = 0; i < All.Length; i++)
            {
                for (int k = 0; k < MeshParts.Length; k++)
                {
                    names.Add(MeshName(All[i], MeshParts[k]));
                }
                for (int k = 0; k < TextureParts.Length; k++)
                {
                    names.Add(TextureName(All[i], TextureParts[k]));
                }
            }
            return names;
        }

        /// <summary>The name the game looks the car's prefab up by: its type's name after the underscore (StringIdExtensions.GetIdWithoutType).</summary>
        public static string PrefabKey(string typeName)
        {
            int at = typeName != null ? typeName.IndexOf('_') : -1;
            if (at < 0)
            {
                return null;
            }
            string rest = typeName.Substring(at + 1);
            int next = rest.IndexOf('_');
            return PrefabFolder + (next < 0 ? rest : rest.Substring(0, next)) + ".prefab";
        }

        /// <summary>What is wrong with a model's numbers, or null: every figure inside what the game's own cars span, the id usable as a prefab name.</summary>
        public static string Wrong(ModCarDef d)
        {
            if (d == null || string.IsNullOrEmpty(d.Id) || d.TypeName != Prefix + d.Id)
            {
                return "its type name is not the prefix and its id";
            }
            if (d.Id.IndexOf('_') >= 0 || d.Id != d.Id.ToLowerInvariant())
            {
                return "its id has an underscore or a capital: the game cuts a type's name at the underscore to find its prefab";
            }
            if (d.Donor == null || !d.Donor.StartsWith(GamePrefix, StringComparison.Ordinal))
            {
                return "it is not built on a car of the game's";
            }
            return Outside(d.Price, GamePriceLeast, GamePriceMost, "its price")
                   ?? Outside(d.MaxSpeed, GameSpeedLeast, GameSpeedMost, "its top speed")
                   ?? Outside(d.EnginePower, GamePowerLeast, GamePowerMost, "its engine power")
                   ?? Outside(d.BrakeForce, GameBrakeLeast, GameBrakeMost, "its brake force")
                   ?? Outside(d.TurnRadius, GameTurnLeast, GameTurnMost, "its turn radius")
                   ?? Outside(d.MaxFuel, GameFuelLeast * 0.7f, GameFuelMost, "its tank")   // a smaller car than any of the game's may hold a little less
                   ?? Outside(d.Cargo, GameCargoLeast, GameCargoMost, "its cargo")
                   ?? Outside(d.DamageIntensity, GameDamageLeast, GameDamageMost, "its damage figure")
                   ?? Outside(d.Wheelbase, GameWheelbaseLeast, GameWheelbaseMost, "its wheelbase")
                   ?? Outside(d.HalfWidth, GameHalfWidthLeast, GameHalfWidthMost, "its width")
                   ?? Outside(d.WheelRadius, 0.25f, 0.45f, "its wheel's radius")
                   ?? (d.Destinations < 1 || d.Destinations > 2 ? "its delivery stops are not the game's 1 or 2" : null)
                   ?? (d.TrackHalf + d.WheelWidth * 0.5f > d.HalfWidth + 0.02f ? "its wheels stand out of its body" : null)
                   ?? (d.AxleFront + d.WheelRadius > d.Front || d.AxleRear - d.WheelRadius < d.Rear ? "a wheel is past a bumper" : null);
        }

        private static string Outside(float value, float least, float most, string what)
        {
            return value < least || value > most ? what + " is outside the game's" : null;
        }

        /// <summary>
        /// Whether a car's price leaves the game's cars their place: no game car costs less and is at least as good in speed,
        /// power and cargo alike (then this one would never be bought), and this one does not beat a dearer game car in all three
        /// (then that one never would). The game's cars as (price, speed, power, cargo).
        /// </summary>
        public static string Unfair(ModCarDef d, IReadOnlyList<(string Name, int Price, int Speed, float Power, int Cargo)> game)
        {
            for (int i = 0; i < game.Count; i++)
            {
                var g = game[i];
                if (g.Price <= d.Price && g.Speed >= d.MaxSpeed && g.Power >= d.EnginePower && g.Cargo >= d.Cargo)
                {
                    return g.Name + " costs no more and is as good or better in every figure";
                }
                if (g.Price >= d.Price && g.Speed <= d.MaxSpeed && g.Power <= d.EnginePower && g.Cargo <= d.Cargo)
                {
                    return g.Name + " costs as much or more and is no better in any figure";
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- the save

        /// <summary>The type name a vehicle is written into the save under: the game car a car of the mod's is built on, any other name as it is.</summary>
        public static string OnDisk(string typeName)
        {
            ModCarDef def = IsOurs(typeName) ? Find(typeName) : null;
            return def != null ? def.Donor : typeName;
        }

        /// <summary>
        /// The type name a loaded vehicle gets: the mod's, when the save's line names this vehicle, the vehicle is still the game
        /// car it was written as, and that model can be made now (<paramref name="ready"/>); otherwise what the save says.
        /// </summary>
        public static string Back(string id, string onDisk, IReadOnlyList<CarStandIn> line, Func<string, bool> ready)
        {
            for (int i = 0; i < line.Count; i++)
            {
                if (line[i].Id == id)
                {
                    return line[i].Donor == onDisk && ready != null && ready(line[i].TypeName) ? line[i].TypeName : onDisk;
                }
            }
            return onDisk;
        }

        public static string Write(IReadOnlyList<CarStandIn> line)
        {
            var text = new StringBuilder(line.Count * 72);
            for (int i = 0; i < line.Count; i++)
            {
                if (i > 0)
                {
                    text.Append('\n');
                }
                text.Append(line[i].Id).Append('|').Append(line[i].TypeName).Append('|').Append(line[i].Donor);
            }
            return text.ToString();
        }

        /// <summary>The save's line read back; a row that is not three parts, or names no type of the mod's form, is left out.</summary>
        public static List<CarStandIn> Read(string text)
        {
            var line = new List<CarStandIn>(4);
            if (string.IsNullOrEmpty(text))
            {
                return line;
            }
            string[] rows = text.Split('\n');
            for (int i = 0; i < rows.Length; i++)
            {
                string[] parts = rows[i].Split('|');
                if (parts.Length == 3 && parts[0].Length > 0 && IsOurs(parts[1]) && parts[2].StartsWith(GamePrefix, StringComparison.Ordinal))
                {
                    line.Add(new CarStandIn(parts[0], parts[1], parts[2]));
                }
            }
            return line;
        }
    }
}
