using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RealLifeOverhaul;

// The cars made for the mod (25 Nov 2026, the owner: "build the custom cars for the game"). A car is a vehicle type of its own while the game runs
// and the game car it is built on in the save, because the game reads VehicleInstance.VehicleType without asking whether it is there
// (PlayerHelper.GetTotalAssetsWorth, TaxHelper.ForcePayCurrentTaxes, BuildingHelper, MovingServiceHelper): a save holding a type the game does not
// know would fault there on a machine without the mod. Held here: every model's figures inside what the game's own cars span and no car made
// pointless by its price, the model's files agreeing with the code that places its wheels, everything the code asks for by name being there,
// what a car costs the download, the save's line and its round trip, and the sources that keep a missing file quiet. Offline: run with --mod-cars.
internal static partial class Program
{
    // The game's own cars the player buys, from its vehicle types (build 3682): name, price, top speed, engine power, cargo.
    private static readonly (string Name, int Price, int Speed, float Power, int Cargo)[] CarsOfTheGame =
    {
        ("Honza Mimic", 2500, 60, 45f, 8), ("UMC Desert", 8000, 50, 55f, 14), ("Vord Tiara Vic", 11000, 70, 80f, 12), ("Vord V150", 44000, 80, 130f, 20),
        ("BIMA 320", 48000, 70, 100f, 10), ("Mersaidi Dash", 48500, 65, 70f, 24), ("UMC Nunavut", 65000, 80, 150f, 18), ("Vord Courier D500", 72500, 55, 46f, 40),
        ("Mersaidi S500", 95000, 80, 175f, 8), ("Freight Truck T1", 98000, 50, 35f, 60), ("Ferdinand 112", 130000, 90, 150f, 3), ("Lytte L6", 156000, 80, 175f, 8),
        ("Vord Pony", 187000, 80, 100f, 5), ("Mersaidi MGA GT", 220000, 100, 200f, 5), ("Petrolls Executive Limousine", 250000, 80, 175f, 8),
        ("Anselmo AF90", 520000, 100, 200f, 3), ("Petrolls Fanton", 720000, 80, 175f, 8)
    };

    private const int CarBodyTrianglesMost = 6600, CarWheelTrianglesMost = 570;   // the most any car of the game's has (the Honza Mimic's body, the UMC Desert's wheel)
    private const long CarBytesMost = 4L * 1024 * 1024;                     // what one car may add to the download

    private static void ModCars()
    {
        string root = RepoRoot();
        string bundles = System.IO.Path.Combine(root, "Assets", "Mods", "RealLifeOverhaul", "Bundles");
        bool built = File.Exists(System.IO.Path.Combine(bundles, "reallife_cars"));
        string contentsPath = System.IO.Path.Combine(root, "GarageAssets", "contents", "reallife_cars.json");
        var listed = new HashSet<string>(StringComparer.Ordinal);
        long bundleBytes = 0;
        if (built)
        {
            Check(File.Exists(contentsPath), "mod cars: the bundle reallife_cars is committed with its list, GarageAssets/contents/reallife_cars.json");
            if (File.Exists(contentsPath))
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(contentsPath));
                bundleBytes = doc.RootElement.GetProperty("bytes").GetInt64();
                foreach (string group in new[] { "meshes", "textures" })
                {
                    foreach (JsonElement row in doc.RootElement.GetProperty(group).EnumerateArray())
                    {
                        listed.Add(row.GetProperty("name").GetString());
                    }
                }
                Check(bundleBytes == new FileInfo(System.IO.Path.Combine(bundles, "reallife_cars")).Length, "mod cars: the list is of the bundle that is committed (its size)");
                Check(!Directory.Exists(System.IO.Path.Combine(bundles, "cars_newer")), "mod cars: with the bundle built, the plain files beside it are gone (GarageAssets/build.ps1 removes them)");
            }
        }

        Check(ModCarCatalogue.All.Length >= 1, "mod cars: there is a car");
        var ids = new HashSet<string>();
        foreach (ModCarDef d in ModCarCatalogue.All)
        {
            string wrong = ModCarCatalogue.Wrong(d);
            Check(wrong == null, "mod cars: " + d.Id + " is sound for the game" + (wrong != null ? " (" + wrong + ")" : string.Empty));
            Check(ids.Add(d.Id), "mod cars: " + d.Id + " is listed once");
            // Each car has its own line in the driving model and its own measured row for fitted parts: without them it would be
            // placed by a guess and offered no spoiler or roof bars.
            Check(ModSource(@"Driving\CarCatalogue.cs").Contains("case \"" + d.TypeName + "\": return new CarSpec("), "mod cars: " + d.Id + " has its line in the driving model (Driving/CarCatalogue)");
            Check(ModSource(@"Driving\CarCatalogue.cs").Contains("case \"" + d.TypeName + "\": return ") && CarCatalogue.SeatsKnown(d.TypeName), "mod cars: " + d.Id + "'s seats are known");
            Check(GarageFit.TryGet(d.TypeName, out CarFit fit) && System.Math.Abs(fit.Front - d.Front) < 0.03f && System.Math.Abs(fit.Rear - d.Rear) < 0.03f && System.Math.Abs(fit.Top - d.Height) < 0.03f,
                "mod cars: " + d.Id + " has its measured row for fitted parts (Garage/GarageFit), of the same body the code describes");
            Check(Block(ModSource(@"Garage\ModCars.cs"), "private static void Register(ModCarDef d)").Contains("type.fitsFlatbed = d.FitsFlatbed;")
                  && Block(ModSource(@"Garage\ModCars.cs"), "private static void Register(ModCarDef d)").Contains("type.destinationsThatCanDeliver = d.Destinations;")
                  && (d.Destinations == 2) == d.FitsFlatbed, "mod cars: " + d.Id + "'s working switches reach the game's type, and two delivery stops go with a flatbed as on the game's vans and pickups");
            string unfair = ModCarCatalogue.Unfair(d, CarsOfTheGame);
            Check(unfair == null, "mod cars: " + d.Id + " at " + d.Price + " neither is nor makes a must-buy" + (unfair != null ? " (" + unfair + ")" : string.Empty));
            Check(ModCarCatalogue.PrefabKey(d.TypeName) == "Prefabs/Vehicles/PlayerVehicles/" + d.Id + ".prefab",
                "mod cars: " + d.Id + " is looked up under the name the game asks for (Prefabs/Vehicles/PlayerVehicles/<the type's name after its underscore>.prefab)");
            bool donorKnown = false;
            foreach (var g in CarsOfTheGame)
            {
                donorKnown |= d.Donor == ModCarCatalogue.GamePrefix + g.Name.Replace(" ", string.Empty).ToLowerInvariant();
            }
            Check(donorKnown, "mod cars: " + d.Id + " is built on, and saved as, a car the game sells (" + d.Donor + ")");

            // The model's files, as exported from Blender: what the code says of the car is what the model is.
            string folder = System.IO.Path.Combine(root, "GarageAssets", "cars", d.Id);
            long plainBytes = 0;
            foreach (string part in ModCarCatalogue.MeshParts)
            {
                string name = ModCarCatalogue.MeshName(d, part);
                string file = System.IO.Path.Combine(folder, "meshes", name + ".json");
                Check(File.Exists(file), "mod cars: " + name + " is exported (GarageAssets/cars/" + d.Id + "/meshes)");
                if (!File.Exists(file))
                {
                    continue;
                }
                plainBytes += new FileInfo(file).Length;
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file));
                float[] v = CarFloats(doc.RootElement.GetProperty("vertices"));
                int triangles = doc.RootElement.GetProperty("triangles").GetArrayLength() / 3, count = v.Length / 3;
                Check(count >= 3 && doc.RootElement.GetProperty("normals").GetArrayLength() == count * 3 && doc.RootElement.GetProperty("tangents").GetArrayLength() == count * 4
                      && doc.RootElement.GetProperty("uv").GetArrayLength() == count * 2, "mod cars: " + name + " has a normal, a tangent and a UV for every vertex");
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                for (int i = 0; i < count; i++)
                {
                    minX = Math.Min(minX, v[i * 3]); maxX = Math.Max(maxX, v[i * 3]);
                    minY = Math.Min(minY, v[i * 3 + 1]); maxY = Math.Max(maxY, v[i * 3 + 1]);
                    minZ = Math.Min(minZ, v[i * 3 + 2]); maxZ = Math.Max(maxZ, v[i * 3 + 2]);
                }
                if (part == "body")
                {
                    Check(triangles >= 1500 && triangles <= CarBodyTrianglesMost, "mod cars: " + d.Id + "'s body has " + triangles + " triangles, as a car of the game's has (2,618 to 6,578)");
                    Check(Math.Abs(maxZ - d.Front) < 0.03f && Math.Abs(minZ - d.Rear) < 0.03f, "mod cars: " + d.Id + "'s bumpers are where the code says (" + minZ + " to " + maxZ + ")");
                    Check(minY > 0.12f && minY < 0.35f && maxY > d.Height && maxY < d.Height + 0.12f, "mod cars: " + d.Id + " stands on the ground at the height the code says (" + minY + " to " + maxY + ")");
                    Check(maxX > d.HalfWidth && maxX < d.HalfWidth + 0.22f && Math.Abs(maxX + minX) < 0.01f, "mod cars: " + d.Id + " is as wide as the code says, mirrors beyond, and the same either side");
                }
                else if (part == "collider")
                {
                    Check(triangles <= 255 && maxX < d.HalfWidth + 0.03f, "mod cars: " + d.Id + "'s collision shape is small enough for a convex collider (" + triangles + " triangles) and leaves the mirrors out");
                }
                else
                {
                    Check(triangles <= CarWheelTrianglesMost, "mod cars: a wheel of " + d.Id + " has " + triangles + " triangles, as a wheel of the game's has (214 to 568)");
                    Check(Math.Abs(maxY - d.WheelRadius) < 0.005f && Math.Abs(minY + d.WheelRadius) < 0.005f && Math.Abs(maxZ - d.WheelRadius) < 0.005f,
                        "mod cars: a wheel of " + d.Id + " is round, about its own middle, of the radius the code gives the game's wheel");
                    Check(Math.Abs(maxX - minX - d.WheelWidth) < 0.01f, "mod cars: a wheel of " + d.Id + " is as wide as the code says");
                }
                Check(built ? listed.Contains(name) : File.Exists(System.IO.Path.Combine(bundles, "cars_newer", d.Id, "meshes", name + ".json")),
                    "mod cars: " + name + " is in what ships (" + (built ? "the bundle" : "the plain files, while the bundle is not built") + ")");
            }
            for (int i = 0; i < ModCarCatalogue.TextureParts.Length; i++)
            {
                string name = ModCarCatalogue.TextureName(d, ModCarCatalogue.TextureParts[i]);
                string file = System.IO.Path.Combine(folder, "textures", name + ".png");
                Check(File.Exists(file), "mod cars: " + name + " is exported");
                plainBytes += File.Exists(file) ? new FileInfo(file).Length : 0;
                Check(built ? listed.Contains(name) : File.Exists(System.IO.Path.Combine(bundles, "cars_newer", d.Id, "textures", name + ".png")), "mod cars: " + name + " is in what ships");
            }
            long adds = built ? bundleBytes / Math.Max(ModCarCatalogue.All.Length, 1) : plainBytes;
            Check(adds > 0 && adds <= CarBytesMost, "mod cars: " + d.Id + " adds " + (adds / 1024) + " KB to the download, under " + (CarBytesMost / 1024) + " KB");
        }
        // The colour texture's alpha: full on the paint, nothing on glass and lamps, and at an edge a ramp through the shader's window, never a step.
        foreach (ModCarDef d in ModCarCatalogue.All)
        {
            string file = System.IO.Path.Combine(root, "GarageAssets", "cars", d.Id, "textures", ModCarCatalogue.TextureName(d, "d") + ".png");
            if (!File.Exists(file))
            {
                continue;
            }
            byte[] alpha = PngAlpha(file, out int width, out int height);
            int full = 0, none = 0, window = 0, steps = 0;
            int low = (int)(ModCarCatalogue.PaintTo * 255f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int a = alpha[y * width + x];
                    full += a >= 250 ? 1 : 0;
                    none += a == 0 ? 1 : 0;
                    window += a > 0 && a <= low ? 1 : 0;
                    steps += x > 0 && a >= 250 && alpha[y * width + x - 1] == 0 ? 1 : 0;
                }
            }
            Check(width == 1024 && height == 1024 && full > width * height / 5 && none > width * height / 5, "mod cars: " + d.Id + "'s colour texture is 1024 square with paint (" + full + " texels) and unpainted parts");
            Check(window > 2000 && steps < window / 50, "mod cars: " + d.Id + "'s paint edges are ramps through the shader's window (" + window + " texels inside it, " + steps + " bare steps), so the game's shader smooths them");
        }
        // The Car Showroom deals in them: its model list lets the mod's own through, and its promise takes a car standing as its game car.
        string dealer = ModSource(@"Economy\Dealer\DealerGame.cs");
        Check(dealer.Contains("(VehicleTypeHelper.IsModVehicleType(names[i]) && !ModCars.Stocked(names[i]))")
              && dealer.Contains("(!ModCarCatalogue.IsOurs(type) || ModCars.Stocked(type))")
              && Block(ModSource(@"Garage\ModCars.cs"), "public static bool Stocked(string typeName)").Contains("Settings.GarageModCars && !GoingPublic.InSession && !_stoodDown"),
              "mod cars: the Car Showroom lists the mod's own cars while they are sold, and none in a Going Public session or with the sale off");
        for (int i = 0; i < ModCarCatalogue.All.Length; i++)
        {
            ModCarDef d = ModCarCatalogue.All[i];
            Check(RealLifeOverhaul.Economy.Dealership.CarStandRules.Verdict(d.TypeName, d.TypeName) == RealLifeOverhaul.Economy.Dealership.CarMaking.Made
                  && RealLifeOverhaul.Economy.Dealership.CarStandRules.Verdict(d.TypeName, d.Donor) == RealLifeOverhaul.Economy.Dealership.CarMaking.Made
                  && RealLifeOverhaul.Economy.Dealership.CarStandRules.Verdict(d.TypeName, "ba:vehicletype_nosuchcar") == RealLifeOverhaul.Economy.Dealership.CarMaking.WrongModel
                  && RealLifeOverhaul.Economy.Dealership.CarStandRules.Verdict(d.Donor, d.TypeName) == RealLifeOverhaul.Economy.Dealership.CarMaking.WrongModel,
                  "mod cars: a " + d.Id + " on a showroom's lot is the car paid for as itself and as the game car it is saved as, and no other way round");
        }
        Check(ModSource(@"Garage\ModCars.cs").Contains("draw.sharedMaterials = new[] { paint };") && !ModSource(@"Garage\ModCars.cs").Contains("draw.sharedMaterial = paint;"),
            "mod cars source: a made car's renderers carry one material each, so a game car's second material (the UMC Desert's clear glass) is not drawn over the whole body");
        Check(ModCarCatalogue.TextureSlots.Length == ModCarCatalogue.TextureParts.Length && ModCarCatalogue.TextureLinear.Length == ModCarCatalogue.TextureParts.Length,
            "mod cars: every slot of the game's vehicle shader has its texture and its colour space");
        Check(ModCarCatalogue.AssetNames().Count == ModCarCatalogue.All.Length * (ModCarCatalogue.MeshParts.Length + ModCarCatalogue.TextureParts.Length), "mod cars: the list of what the code asks for is whole");

        // A model with a figure out of the game's range, or a name the game would cut short, is refused: the rules can fail.
        ModCarDef pip = ModCarCatalogue.HaldenPip;
        Check(ModCarCatalogue.Wrong(CarAltered(pip, d => d.Price = 900)) != null && ModCarCatalogue.Wrong(CarAltered(pip, d => d.MaxSpeed = 140)) != null
              && ModCarCatalogue.Wrong(CarAltered(pip, d => d.TrackHalf = 0.95f)) != null && ModCarCatalogue.Wrong(CarAltered(pip, d => d.Donor = "reallife:vehicletype_x")) != null,
            "mod cars: a price, a speed, a track or a donor outside the game's is refused");
        Check(ModCarCatalogue.Wrong(CarAltered(pip, d => { d.Id = "halden_pip"; d.TypeName = ModCarCatalogue.Prefix + "halden_pip"; })) != null,
            "mod cars: an id with an underscore is refused (the game would look for the prefab 'halden')");
        Check(ModCarCatalogue.Unfair(CarAltered(pip, d => { d.Price = 2400; d.Cargo = 8; }), CarsOfTheGame) != null, "mod cars: a Pip with a Honza Mimic's room for less than a Mimic would leave the Mimic unbought, and is refused");
        Check(ModCarCatalogue.Unfair(CarAltered(pip, d => { d.Cargo = 8; d.MaxSpeed = 60; d.EnginePower = 45f; }), CarsOfTheGame) != null, "mod cars: a Mimic's figures for more than a Mimic's price is refused");

        // The save: written as the game car, turned back by the line, and safe whoever opens it.
        var live = new List<(string Id, string Type)> { ("a1", pip.TypeName), ("b2", "ba:vehicletype_vordpony"), ("c3", pip.TypeName), ("d4", "ba:vehicletype_honzamimic") };
        var line = new List<CarStandIn>();
        var disk = new List<(string Id, string Type)>();
        foreach (var v in live)
        {
            string on = ModCarCatalogue.OnDisk(v.Type);
            if (on != v.Type)
            {
                line.Add(new CarStandIn(v.Id, v.Type, on));
            }
            disk.Add((v.Id, on));
        }
        bool allGame = true;
        foreach (var v in disk)
        {
            allGame &= v.Type.StartsWith(ModCarCatalogue.GamePrefix, StringComparison.Ordinal);
        }
        Check(allGame && line.Count == 2, "mod cars save: every vehicle written names a type of the game's own, and the line holds the two cars of the mod's");
        string text = ModCarCatalogue.Write(line);
        List<CarStandIn> read = ModCarCatalogue.Read(text);
        Check(read.Count == 2 && ModCarCatalogue.Write(read) == text && read[0].Id == "a1" && read[1].TypeName == pip.TypeName && read[1].Donor == pip.Donor, "mod cars save: the line reads back as written");
        bool same = true;
        for (int i = 0; i < live.Count; i++)
        {
            same &= ModCarCatalogue.Back(disk[i].Id, disk[i].Type, read, t => true) == live[i].Type;
        }
        Check(same, "mod cars save: at the next load every car is what it was, and the player's own Honza Mimic stays a Honza Mimic");
        bool held = true;
        for (int i = 0; i < live.Count; i++)
        {
            held &= ModCarCatalogue.Back(disk[i].Id, disk[i].Type, read, t => false) == disk[i].Type && ModCarCatalogue.Back(disk[i].Id, disk[i].Type, read, null) == disk[i].Type;
        }
        Check(held, "mod cars save: where the model cannot be made (no files, a session of Going Public, a game update) every car stays the game car it is saved as");
        Check(ModCarCatalogue.Back("a1", "ba:vehicletype_vordpony", read, t => true) == "ba:vehicletype_vordpony",
            "mod cars save: a vehicle that is no longer the game car the line wrote (changed without the mod) is left as it is");
        Check(ModCarCatalogue.Back("zz", pip.Donor, read, t => true) == pip.Donor, "mod cars save: a vehicle the line does not name is left as it is");
        Check(ModCarCatalogue.Read(null).Count == 0 && ModCarCatalogue.Read("").Count == 0 && ModCarCatalogue.Read("a|b\nx|ba:vehicletype_q|ba:vehicletype_r\n|" + pip.TypeName + "|" + pip.Donor).Count == 0,
            "mod cars save: a line that is empty, short, or names no type of the mod's is read as nothing");
        Check(ModCarCatalogue.OnDisk("reallife:vehicletype_nosuchcar") == "reallife:vehicletype_nosuchcar" && ModCarCatalogue.OnDisk(null) == null && ModCarCatalogue.OnDisk(pip.Donor) == pip.Donor,
            "mod cars save: only a model the catalogue knows is written as its game car");

        // Texts: all of them in English; in another language a text that is there keeps its places (a language may lack a key until it is translated).
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LocaleFile(code);
            if (code == "en")
            {
                bool all = words.ContainsKey("reallife_cars_facts") && words.ContainsKey("reallife_cars_no_bay") && words.ContainsKey("reallife_cars_bought") && words.ContainsKey("reallife_cars_unready")
                           && words.ContainsKey("reallife_garage_mod_cars") && words.ContainsKey("reallife_opt_tip_garage_mod_cars");
                foreach (ModCarDef d in ModCarCatalogue.All)
                {
                    all &= words.ContainsKey(d.TypeName) && words.ContainsKey(d.KindKey);
                }
                Check(all, "mod cars texts: every car's name, its kind and the sale's texts are in en");
            }
            Check(!words.TryGetValue("reallife_cars_facts", out string facts) || (facts.Contains("{kind}") && facts.Contains("{cargo}")), "mod cars texts: the list's line keeps its two places in " + code);
            Check(!words.TryGetValue("reallife_cars_bought", out string bought) || bought.Contains("{name}"), "mod cars texts: the notice of a bought car names it in " + code);
        }
        Check(WhatsNewHas(LocaleFile("en"), "the Halden Pip"), "mod cars texts: What's new says the first car is for sale");

        // Sources: a missing file is said once and nothing more, the types are there with the option off, the save step runs whatever is switched off.
        string cars = ModSource(@"Garage\ModCars.cs");
        string open = Block(cars, "private static bool Open()");
        Check(open.Contains("if (_bundleTried)") && InOrder(open, "_bundleTried = true;", "File.Exists(file)", "Directory.Exists(folder)", "StandDown("),
            "mod cars source: the files are looked for once a run of the game; the bundle first, then the plain files");
        string down = Block(cars, "private static void StandDown(string why)");
        Check(InOrder(down, "if (!_stoodDown)", "_stoodDown = true;", "Logger?.Warn("), "mod cars source: standing down is said once, as a warning, never an error a frame");
        Check(Block(cars, "public static bool Offered(ModCarDef d, BikePlace place)").Contains("Settings.GarageModCars") && !Block(cars, "public static void Start(ModContext context)").Contains("Settings."),
            "mod cars source: the option stops the sale only; the types are registered whatever it says, so a car owned stays a car");
        Check(Block(cars, "public static void Start(ModContext context)").Contains("SaveHook.KeepEssential(SaveStep);"), "mod cars source: the save's step runs even when the garage is switched off after its faults");
        Check(InOrder(Block(cars, "private static void OnSave()"), "ModCarCatalogue.OnDisk(v.vehicleTypeName)", "v.vehicleTypeName = disk;", "save.modData[ModCarCatalogue.Key]", "_armed = true;"),
            "mod cars source: at a save each car takes its game car's name, the line is written, and the names are waited for");
        Check(Block(cars, "public static void Ready(GameInstance save, bool early)").Contains("!GoingPublic.InSession") && Block(cars, "public static bool Offered(ModCarDef d, BikePlace place)").Contains("!GoingPublic.InSession"),
            "mod cars source: in a Going Public session no car is turned back and none is sold, so nothing another player's game does not know goes over the network");
        Check(ModSource(@"DistrictSave\DistrictSaveEntry.cs").Contains("ModCars.Ready(SaveGameManager.Current, true)") && ModSource(@"Garage\GarageDirector.cs").Contains("ModCars.Ready(save, false);"),
            "mod cars source: the cars are turned back before the city is built, and again in the city for a load that did not pass the main menu");
        Check(ModSource(@"Core\Settings.cs").Contains("public static bool GarageModCars = true;"), "mod cars source: on by default");
        Check(built == ModSource(@"Core\BundleShaderRules.cs").Contains("\"reallife\" + \"_cars\""), "mod cars source: the bundle is in the list of the mod's bundles (Core/BundleShaderRules.Bundles) exactly when it is built and shipped");
        Check(Block(cars, "private static bool Open()").Contains("BundleShaders.Check(Logger, ModCarCatalogue.BundleName, _bundle"), "mod cars source: the bundle's shaders are checked as it is read, like every bundle's");
        Check(ModSource(@"Core\GameContract.cs").Contains("StaticField(ModCarPrefabs, Game, \"Helpers.PrefabHelper\", \"PrefabCache\", \"Dictionary<String,Object>\")"),
            "mod cars source: the game's lookup of prefabs is a line of the game contract, read from there");
    }

    private static ModCarDef CarAltered(ModCarDef from, Action<ModCarDef> change)
    {
        var d = new ModCarDef
        {
            Id = from.Id, TypeName = from.TypeName, Donor = from.Donor, KindKey = from.KindKey, Price = from.Price, MaxFuel = from.MaxFuel, Cargo = from.Cargo, MaxSpeed = from.MaxSpeed,
            EnginePower = from.EnginePower, BrakeForce = from.BrakeForce, TurnRadius = from.TurnRadius, DamageIntensity = from.DamageIntensity, FitsHandTruck = from.FitsHandTruck,
            Luxury = from.Luxury, AxleFront = from.AxleFront, AxleRear = from.AxleRear, TrackHalf = from.TrackHalf, WheelRadius = from.WheelRadius, WheelWidth = from.WheelWidth,
            HalfWidth = from.HalfWidth, Front = from.Front, Rear = from.Rear, Height = from.Height, BonnetHeight = from.BonnetHeight
        };
        change(d);
        return d;
    }

    // The alpha of an 8-bit RGBA PNG without interlace (what the painter writes), read with nothing but the framework.
    private static byte[] PngAlpha(string file, out int width, out int height)
    {
        byte[] png = File.ReadAllBytes(file);
        width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        if (png[24] != 8 || png[25] != 6 || png[28] != 0)
        {
            return new byte[width * height];
        }
        using var packed = new MemoryStream();
        for (int at = 8; at + 8 <= png.Length;)
        {
            int length = (png[at] << 24) | (png[at + 1] << 16) | (png[at + 2] << 8) | png[at + 3];
            if (png[at + 4] == 'I' && png[at + 5] == 'D' && png[at + 6] == 'A' && png[at + 7] == 'T')
            {
                packed.Write(png, at + 8, length);
            }
            at += 12 + length;
        }
        packed.Position = 0;
        using var plain = new MemoryStream();
        using (var inflate = new System.IO.Compression.ZLibStream(packed, System.IO.Compression.CompressionMode.Decompress))
        {
            inflate.CopyTo(plain);
        }
        byte[] raw = plain.ToArray();
        int stride = width * 4;
        var row = new byte[stride];
        var before = new byte[stride];
        var alpha = new byte[width * height];
        for (int y = 0; y < height; y++)
        {
            int filter = raw[y * (stride + 1)];
            for (int i = 0; i < stride; i++)
            {
                int x = raw[y * (stride + 1) + 1 + i], a = i >= 4 ? row[i - 4] : 0, b = before[i], c = i >= 4 ? before[i - 4] : 0;
                int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                int paeth = pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
                row[i] = (byte)(filter == 0 ? x : filter == 1 ? x + a : filter == 2 ? x + b : filter == 3 ? x + (a + b) / 2 : x + paeth);
            }
            for (int px = 0; px < width; px++)
            {
                alpha[y * width + px] = row[px * 4 + 3];
            }
            byte[] swap = before;
            before = row;
            row = swap;
        }
        return alpha;
    }

    private static float[] CarFloats(JsonElement array)
    {
        var values = new float[array.GetArrayLength()];
        int i = 0;
        foreach (JsonElement e in array.EnumerateArray())
        {
            values[i++] = e.GetSingle();
        }
        return values;
    }
}
