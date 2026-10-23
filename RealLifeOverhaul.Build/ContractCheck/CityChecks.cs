using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Meta;

// DLC and update readiness (docs/standards/dlc-and-updates.md). Without the game: a feature's guards away in another city
// and back, the city table naming every feature of the mod, and the DLC rule in the sources (nothing but Core/GameDlc
// reads requiredDLC or DLC ownership, CityManager's list is read only through GameDlc.Open, every pick of the census
// below names the helper). Against the game on disk: its districts (the game's en.json), its DLCs (SteamAPI.DLC in
// HGPlugins), its scenes (Scenes.GameScenes) and the DLC fields of Building and BoatType, each as the mod knows them; a
// game that has more fails here with what to do (docs/contributing/after-a-game-update.md, "New content and DLCs").
internal static partial class Program
{
    /// <summary>The DLCs of Build 3682 (SteamAPI.DLC): the supporter packs; a new one is a DLC to read before it ships to players.</summary>
    private static readonly Dictionary<string, long> KnownDlcs = new Dictionary<string, long>(StringComparer.Ordinal)
    {
        { "None", 0 }, { "Silver", 2254190 }, { "Gold", 2254191 },
    };

    /// <summary>
    /// Every pick of the mod's from the game's buildings, found in the census of 23 Oct 2026: each names Core/GameDlc, so
    /// nothing a DLC keeps is picked. A new pick joins the list; a file reading CityManager's list or the game's building
    /// lists is held to it below without being listed.
    /// </summary>
    private static readonly string[] DlcPicks =
    {
        "Careers/CampusBuilding.cs", "Careers/CareersGame.cs", "Careers/Work/WorkGame.cs", "Civic/CivicCity.cs", "Civic/CivicWorks.cs", "Core/CityCheck.cs",
        "Core/CivicHost.cs", "Core/OfficeCensus.cs", "Core/SpecialNames.cs", "Districts/DistrictGame.cs", "Economy/Brands/BrandsGame.Franchises.cs",
        "Economy/Brands/BrandsGame.cs", "Economy/Estate/EstateGame.cs", "Economy/Online/OnlineGame.cs", "Economy/Services/ServicesGame.cs", "Events/FireScene.cs",
        "Family/FamilyPlaces.cs", "Family/PartnerCar.cs", "Faults/FaultGame.cs", "Garage/BikePlaces.cs", "Garage/GarageShop.cs", "Jobs/JobBoards.cs",
        "Jobs/JobSites.cs", "Jobs/JobSpots.cs", "Justice/Courthouse.cs", "Justice/LegalCounsel.cs", "Law/Precincts.Keep.cs", "Law/Precincts.cs",
        "Ledger/LedgerKiosks.cs", "Leisure/Night/NightLife.cs", "Licences/Driving/DrivingSchool.cs", "Navigation/NavBaseGame.cs", "Navigation/NavGraph.cs",
        "Offices/ConversionGame.cs", "Offices/Conversions.cs", "Permadeath/PermadeathGame.cs", "Social/Flare/FlareLiveGame.cs", "Social/SocialPlaces.cs",
        "Street/StreetGame.cs", "Tech/Sites/Headquarters.cs", "Transport/CarOwnership.Garage.cs", "Transport/TransitMap.cs", "Travel/Hotels.cs",
        "Utilities/UtilitiesOffice.cs", "Wiring/FlareLiveLinks.cs", "World/MapLinks.cs",
        "Floors/FloorsGame.cs",   // the building the floors take for places (FloorsGame.PickClaim)
    };

    /// <summary>Walks of the game's building lists that pick nothing, with why: held to no helper.</summary>
    private static readonly Dictionary<string, string> NotPicks = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "Core/HeldUnitsGame.cs", "takes the mod's own held units, picked through GameDlc, out of the game's district lists for the night, and counts them" },
    };

    private static readonly Regex FeatureAdd = new Regex("Features\\.Add\\(\"([a-z_]+)\"", RegexOptions.Compiled);
    private static readonly Regex GuardFeature = new Regex("(?:Guard|FeatureGuards)\\.For\\(\"([a-z_]+)\"", RegexOptions.Compiled);
    private static readonly Regex OwnerFeature = new Regex("\\{ \"[A-Za-z.]+\", \"([a-z_]+)\" \\}", RegexOptions.Compiled);
    private static readonly Regex CityList = new Regex("cityBuildingControllers", RegexOptions.Compiled);
    private static readonly Regex CityListTest = new Regex("cityBuildingControllers(\\s*(==|!=)\\s*null|\\.Length)", RegexOptions.Compiled);
    private static readonly Regex DlcRead = new Regex("requiredDLC|DlcIsOwned|IsDlcInstalled|SteamAPI\\.|SteamApps\\.", RegexOptions.Compiled);
    private static readonly Regex BuildingLists = new Regex("BuildingHelper\\.(allBuildings|AllNeighbourhoodBuildings)|\\bBoatTypes?\\b", RegexOptions.Compiled);

    // Game-free: the parts of the checks that need no game.
    private static void CityReadyRules(string scripts)
    {
        Section("DLC and update readiness: a feature away in another city, the city table, the DLC rule in the sources");
        CityAway();
        CityTable(scripts);
        DlcSources(scripts);
    }

    private static void CityAway()
    {
        FeatureGuard law = FeatureGuards.For("law", "LawDirector.Update");
        FeatureGuard lawPart = FeatureGuards.For("law", "PrecinctFrontage.Update");
        FeatureGuard phone = FeatureGuards.For("phone", "PhoneApps.Update");
        FeatureGuard machinery = FeatureGuards.For(null, "LoadHold");
        var went = new List<string>();
        var back = new List<string>();
        FeatureGuards.SetAway(new[] { "law", "travel" }, went, back);
        Check(law.Off && law.Away && lawPart.Off && !phone.Off && !machinery.Off, "a New York feature's every guard is away in another city, nothing else's");
        Check(went.Count == 2 && went.Contains("law") && back.Count == 0 && FeatureGuards.FeatureAway("law") && !FeatureGuards.FeatureAway("phone"),
            "the features that went away are told once");
        FeatureGuard later = FeatureGuards.For("travel", "TravelDirector.Update");
        Check(later.Off && later.Away, "a guard first made in the other city is away too");
        Check(!law.Count(1f) && law.Faults == 1, "a fault while away is counted and switches nothing");
        went.Clear();
        FeatureGuards.SetAway(new[] { "law", "travel" }, went, back);
        Check(went.Count == 0 && back.Count == 0, "the same city again: nothing went, nothing came back");
        FeatureGuards.SetAway(null, went, back);
        Check(!law.Off && !law.Away && !lawPart.Off && !later.Off && back.Count == 2 && !FeatureGuards.FeatureAway("law"), "back in New York every guard is on again");
        law.SwitchOff();
        FeatureGuards.SetAway(new[] { "law" }, null, null);
        FeatureGuards.SetAway(null, null, null);
        Check(law.Off && !law.Away, "a guard switched off for its faults stays off when its feature comes back");
        FeatureGuards.ForgetForTests();
    }

    private static void CityTable(string scripts)
    {
        var features = new SortedSet<string>(StringComparer.Ordinal);
        var guarded = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            foreach (Match m in FeatureAdd.Matches(text))
            {
                features.Add(m.Groups[1].Value);
            }
            foreach (Match m in GuardFeature.Matches(text))
            {
                guarded.Add(m.Groups[1].Value);
            }
            if (Path.GetFileName(file) == "FeatureOwners.cs")
            {
                foreach (Match m in OwnerFeature.Matches(text))
                {
                    guarded.Add(m.Groups[1].Value);
                }
            }
        }
        Check(features.Count > 70, "the features of Meta are read from the sources (" + features.Count + ")");
        foreach (string feature in features)
        {
            Check(FeatureCities.Has(feature), "the feature '" + feature + "' has no line in the city table (Meta/FeatureCities.cs): say whether it stands on New York's data");
        }
        foreach (string feature in guarded)
        {
            Check(FeatureCities.Has(feature), "a guard names the feature '" + feature + "', which the city table does not name");
        }
        foreach (string feature in FeatureCities.Named)
        {
            Check(features.Contains(feature), "the city table names '" + feature + "', which no Features.Add registers");
        }
        Check(FeatureCities.Twice().Count == 0, "the city table names a feature twice: " + string.Join(", ", FeatureCities.Twice()));
        Console.WriteLine("   " + features.Count + " features in the city table, " + FeatureCities.NewYork(new List<string>()).Count + " of them New York's.");
    }

    private static void DlcSources(string scripts)
    {
        int picks = 0, lists = 0;
        foreach (string file in Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(scripts, file).Replace('\\', '/');
            string[] lines = File.ReadAllLines(file);
            bool helper = rel == "Core/GameDlc.cs";
            bool names = false;
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n];
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }
                names |= line.Contains("GameDlc.", StringComparison.Ordinal);
                if (!helper && DlcRead.IsMatch(line))
                {
                    Check(false, rel + ":" + (n + 1) + " reads a DLC (" + DlcRead.Match(line).Value + "): only Core/GameDlc does, and never ownership (GameDlc.Gated)");
                }
                if (!helper && CityList.Matches(line).Count > CityListTest.Matches(line).Count)
                {
                    Check(false, rel + ":" + (n + 1) + " reads CityManager's list of buildings: through GameDlc.Open, so nothing a DLC keeps is picked");
                }
                if (!helper && !NotPicks.ContainsKey(rel) && BuildingLists.IsMatch(line))
                {
                    lists++;
                    if (!File.ReadAllText(file).Contains("GameDlc.", StringComparison.Ordinal))
                    {
                        Check(false, rel + ":" + (n + 1) + " walks the game's buildings or boats (" + BuildingLists.Match(line).Value + ") and never asks GameDlc.Gated");
                    }
                }
            }
            if (Array.IndexOf(DlcPicks, rel) >= 0)
            {
                picks++;
                Check(names, rel + " is a pick of the mod's from the game's buildings and no longer names GameDlc");
            }
        }
        foreach (string rel in NotPicks.Keys)
        {
            Check(File.Exists(Path.Combine(scripts, rel)), "a walk declared as no pick is gone: " + rel);
        }
        Check(picks == DlcPicks.Length, "every pick of the census is still there (" + picks + " of " + DlcPicks.Length + "): take a pick that is gone out of the list");
        Console.WriteLine("   " + picks + " picks of the mod's from the game's buildings, each through Core/GameDlc; " + lists + " walks of the game's building lists.");
    }

    // Against the game on disk.
    private static void CityGame(GameFiles files, string locale)
    {
        Section("the game's districts, DLCs and scenes, against what the mod knows (Core/CityDistricts, Core/GameDlc, Core/CityRules)");
        if (File.Exists(locale))
        {
            var districts = new List<string>();
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(locale)))
            {
                foreach (JsonProperty p in doc.RootElement.EnumerateObject())
                {
                    if (p.Name.StartsWith("ba:neighborhood_", StringComparison.Ordinal) && p.Name.IndexOf('_', "ba:neighborhood_".Length) < 0)
                    {
                        districts.Add(p.Name);
                    }
                }
            }
            foreach (string d in districts)
            {
                Check(CityDistricts.Known(d) || d == CityDistricts.Global, "the game has a district the mod does not know: " + d
                      + " (a free update's or a DLC's city): read after-a-game-update.md, New content and DLCs, then add it to Core/CityDistricts if it is New York's");
            }
            foreach (string d in CityDistricts.NewYork)
            {
                Check(districts.Contains(d), "the game no longer has New York's district " + d + ": the city check would not find it (Core/CityDistricts)");
            }
            Console.WriteLine("   the game's districts: " + string.Join(", ", districts) + ".");
        }
        else
        {
            Check(false, "the game's en.json is not at " + locale + ": its districts cannot be read");
        }

        Dictionary<string, long> dlcs = EnumValues(files, "HGPlugins", "SteamAPI+DLC");
        Check(dlcs != null, "the game has no SteamAPI.DLC in HGPlugins: Core/GameDlc reads requiredDLC as that enum");
        if (dlcs != null)
        {
            foreach (KeyValuePair<string, long> dlc in dlcs)
            {
                Check(KnownDlcs.TryGetValue(dlc.Key, out long id) && id == dlc.Value, "the game knows a DLC the mod has not read: " + dlc.Key + " (Steam app " + dlc.Value
                      + "): run the census (RealLifeOverhaul.Build/Census) for what it gates, and read after-a-game-update.md, New content and DLCs");
            }
            Console.WriteLine("   the game's DLCs: " + string.Join(", ", dlcs.Select(d => d.Key + " " + d.Value)) + ".");
        }
        Check(FieldTyped(files, "BigAmbitions", "Buildings.Building", "requiredDLC"), "Building.requiredDLC is gone or changed: Core/GameDlc.Gated reads it");
        Check(FieldTyped(files, "BigAmbitions", "Entities.BoatType", "requiredDLC"), "BoatType.requiredDLC is gone or changed: Core/GameDlc.Gated reads it");

        Dictionary<string, long> scenes = EnumValues(files, "BigAmbitions", "Scenes.GameScenes");
        Check(scenes != null, "the game has no Scenes.GameScenes: the city check reads the scenes by those names");
        if (scenes != null)
        {
            foreach (string scene in scenes.Keys)
            {
                Check(Array.IndexOf(CityRules.NewYorkScenes, scene) >= 0 || Array.IndexOf(CityRules.SharedScenes, scene) >= 0, "the game has a scene the city check does not know: "
                      + scene + ": add it to Core/CityRules (New York's own scenes, or the shared ones) after reading what loads it");
            }
            foreach (string scene in CityRules.NewYorkScenes)
            {
                Check(scenes.ContainsKey(scene), "the game no longer has New York's scene " + scene + " (Core/CityRules)");
            }
            Console.WriteLine("   the game's scenes: " + string.Join(", ", scenes.Keys) + ".");
        }
    }

    // The named constants of an enum of the game's, or null when the type is not there.
    private static Dictionary<string, long> EnumValues(GameFiles files, string assembly, string type)
    {
        FoundType t = files.Find(assembly, type);
        if (!t.Exists)
        {
            return null;
        }
        MetadataReader md = t.Assembly.Md;
        var values = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (FieldDefinitionHandle h in t.Def.GetFields())
        {
            FieldDefinition f = md.GetFieldDefinition(h);
            if ((f.Attributes & FieldAttributes.Literal) == 0)
            {
                continue;
            }
            Constant c = md.GetConstant(f.GetDefaultValue());
            BlobReader blob = md.GetBlobReader(c.Value);
            long value = c.TypeCode == ConstantTypeCode.Int64 ? blob.ReadInt64() : c.TypeCode == ConstantTypeCode.UInt32 ? blob.ReadUInt32() : blob.ReadInt32();
            values[md.GetString(f.Name)] = value;
        }
        return values;
    }

    // True when the type has the field, of the type SteamAPI.DLC (a value type of HGPlugins).
    private static bool FieldTyped(GameFiles files, string assembly, string type, string field)
    {
        FoundType t = files.Find(assembly, type);
        if (!t.Exists)
        {
            return false;
        }
        MetadataReader md = t.Assembly.Md;
        foreach (FieldDefinitionHandle h in t.Def.GetFields())
        {
            FieldDefinition f = md.GetFieldDefinition(h);
            if (md.GetString(f.Name) != field)
            {
                continue;
            }
            BlobReader sig = md.GetBlobReader(f.Signature);
            sig.ReadSignatureHeader();
            SignatureTypeCode code = sig.ReadSignatureTypeCode();
            if (code != SignatureTypeCode.TypeHandle)
            {
                return false;
            }
            EntityHandle handle = sig.ReadTypeHandle();
            if (handle.Kind != HandleKind.TypeReference)
            {
                return false;
            }
            TypeReference r = md.GetTypeReference((TypeReferenceHandle)handle);
            return md.GetString(r.Name) == "DLC";
        }
        return false;
    }
}
