using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

// Checks, over the mod's sources and texts as they ship, the mod's places on the game's city map (Core/MapPlaces,
// World/MapLinks and the features that register a kind of their own): every kind has a row in the map's filters with a
// name in en, pt and zh-cn and an icon that is drawn; every building the mod claims as its own (Core/SpecialBuildings)
// has a row of its own whose place is that building, so that the row lights it on the open map as the game lights its
// own buildings; and nothing is put on the map before the map can take it.
//
// The owner's report of 15 Oct 2026: "the Hudson City University is not lighting up in the map and also not showing in
// map filters". The university was one of three or four "Places to study" (with the game's school and the hospitals'
// wards), under a star the sights share: the filters had no row by its name, the row's arrow flew to the nearest of
// those places (Bellevue Hospital, in the owner's Player.log), and with that row unticked (as the owner had left it in
// the session before) the university had no marker and no light at all. Now it has a row of its own, "Hudson City
// University", with its own icon, there while the university's part is on.
//
// What is not here: the game. Whether the building lights is the game's own work (CityBuildingController.SetHighlight);
// the "Map links: university on the city map with 1 place(s)" and "Map places: ... row(s) added" lines of Player.log
// say the row was made.
internal static partial class Program
{
    // The buildings the mod claims, by the first argument of SpecialBuildings.Set / SetNamed, and the row each has.
    private static readonly Dictionary<string, string> ClaimRows = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "\"campus\"", "university" },
        { "\"courthouse\"", "courthouse" },
        { "\"police_\"", "police" },
        { "TowerId", "tech_hq" },
        { "\"hotel_\"", "hotels" },
        { "\"utilities_office\"", "utilities_office" },
    };

    private sealed class MapRow
    {
        public string Id, TitleKey, Icon, Where;
        public bool InStreet;
    }

    private static void MapRows()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        if (!Directory.Exists(scripts))
        {
            Check(false, "map rows: the mod's sources are at " + scripts);
            return;
        }
        string locales = Path.Combine(scripts, "..", "Locales");
        Dictionary<string, string> en = MapTexts(locales, "en"), pt = MapTexts(locales, "pt"), zh = MapTexts(locales, "zh-cn");
        string places = Source(scripts, @"Core\MapPlaces.cs");
        string links = Source(scripts, @"World\MapLinks.cs");
        List<MapRow> rows = MapRowsDeclared(scripts, links);
        MapRowKinds(rows, places, en, pt, zh);
        MapRowClaims(scripts, rows);
        MapRowUniversity(rows, links, en, pt, zh);
        MapRowTiming(scripts, places, links);
        MapRowNews(en, pt, zh);
    }

    // Every row the mod's sources declare: MapLinks' kinds, and every other feature's MapPlaces.Register.
    private static List<MapRow> MapRowsDeclared(string scripts, string links)
    {
        var rows = new List<MapRow>();
        string declare = Body(links, "private void Declare()");
        string[] blocks = declare.Split(new[] { "new Link" }, StringSplitOptions.None);
        for (int b = 1; b < blocks.Length; b++)
        {
            string block = blocks[b];
            Match id = Regex.Match(block, "Id = \"(\\w+)\"");
            Match title = Regex.Match(block, "TitleKey = \"(\\w+)\"");
            Match icon = Regex.Match(block, "Icon = MapIcon\\.(\\w+)");
            rows.Add(new MapRow
            {
                Id = id.Success ? id.Groups[1].Value : null,
                TitleKey = title.Success ? title.Groups[1].Value : null,
                Icon = icon.Success ? icon.Groups[1].Value : null,
                InStreet = block.Contains("InStreet = true"),
                Where = "World/MapLinks.cs (link " + b + ")"
            });
        }
        var register = new Regex("MapPlaces\\.Register\\(\\s*([^,]+?)\\s*,\\s*\"(\\w+)\"\\s*,[^;]*?MapIcon\\.(\\w+)([^;]*);", RegexOptions.Singleline);
        foreach (string file in Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string name = Path.GetRelativePath(scripts, file).Replace('\\', '/');
            if (name == "Core/MapPlaces.cs" || name == "World/MapLinks.cs")
            {
                continue;
            }
            string code = Code(File.ReadAllText(file));
            foreach (Match m in register.Matches(code))
            {
                rows.Add(new MapRow
                {
                    Id = m.Groups[1].Value.Trim('"'),
                    TitleKey = m.Groups[2].Value,
                    Icon = m.Groups[3].Value,
                    InStreet = m.Groups[4].Value.Contains("inStreet: true"),
                    Where = name
                });
            }
        }
        return rows;
    }

    // Every row has its own id, a name in the three languages, and an icon that is drawn: every glyph but the cross has a
    // drawing of its own (the cross is what the drawing falls back to); a kind with no icon is refused by MapPlaces itself.
    private static void MapRowKinds(List<MapRow> rows, string places, Dictionary<string, string> en, Dictionary<string, string> pt, Dictionary<string, string> zh)
    {
        Check(rows.Count >= 17, "map rows: the sources declare " + rows.Count + " kinds of place, fewer than the mod has: the reading is broken");
        var drawn = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(Body(places, "private static Sprite Draw("), "case MapIcon\\.(\\w+):"))
        {
            drawn.Add(m.Groups[1].Value);
        }
        if (Body(places, "private static Sprite Draw(").Contains("default:"))
        {
            drawn.Add("Cross");   // (the drawing every other glyph would fall back to is the cross: it is MapIcon.Cross's own)
        }
        Match glyphs = Regex.Match(places, "public enum MapIcon\\s*\\{([^}]*)\\}");
        int listed = 0;
        foreach (string raw in glyphs.Success ? glyphs.Groups[1].Value.Split(',') : new string[0])
        {
            string glyph = raw.Trim();
            if (glyph.Length == 0 || glyph == "Custom")
            {
                continue;
            }
            listed++;
            Check(drawn.Contains(glyph), "map rows: the glyph MapIcon." + glyph + " has a drawing of its own (MapPlaces.Draw)");
        }
        Check(listed >= 20 && glyphs.Groups[1].Value.TrimEnd().EndsWith("Custom", StringComparison.Ordinal),
            "map rows: the glyphs are listed with Custom last (the table of drawn glyphs is as long as the list before it)");
        Check(places.Contains("owner.Icon == null") && places.Contains("\"its kind has no icon\"") && places.Contains(": Glyph(icon)"),
            "map rows: a kind is given its drawn glyph, and a place of a kind with no icon is not put on the map");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (MapRow row in rows)
        {
            string what = "map rows: '" + (row.Id ?? "?") + "' (" + row.Where + ")";
            Check(!string.IsNullOrEmpty(row.Id) && ids.Add(row.Id), what + " has an id no other row has");
            Check(!string.IsNullOrEmpty(row.TitleKey) && MapText(en, row.TitleKey) && MapText(pt, row.TitleKey) && MapText(zh, row.TitleKey),
                what + " has its name (" + (row.TitleKey ?? "no key") + ") in en, pt and zh-cn");
            Check(row.Icon != null && row.Icon != "Custom" && drawn.Contains(row.Icon), what + " has a drawn icon (MapIcon." + (row.Icon ?? "none") + ")");
        }
    }

    // Every building the mod claims as its own has a row of its own, shown in the street too (as the game's markers for
    // its special buildings are), and the claims the sources make are all known here.
    private static void MapRowClaims(string scripts, List<MapRow> rows)
    {
        var claim = new Regex("SpecialBuildings\\.Set(?:Named)?\\(\\s*([^,]+),");
        int claims = 0;
        foreach (string file in Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string name = Path.GetRelativePath(scripts, file).Replace('\\', '/');
            foreach (Match m in claim.Matches(Code(File.ReadAllText(file))))
            {
                string first = m.Groups[1].Value.Trim();
                int plus = first.IndexOf('+');
                string token = (plus >= 0 ? first.Substring(0, plus) : first).Trim();
                claims++;
                bool known = ClaimRows.TryGetValue(token, out string rowId);
                Check(known, "map rows: the building claimed as " + token + " (" + name + ") has a row of its own on the city map (add it to ClaimRows with its row)");
                if (!known)
                {
                    continue;
                }
                MapRow row = rows.Find(r => r.Id == rowId);
                Check(row != null && row.InStreet, "map rows: the building claimed as " + token + " (" + name + ") has its row '" + rowId + "', shown in the street too");
            }
        }
        Check(claims >= 7, "map rows: the sources claim " + claims + " buildings, fewer than the mod does: the reading is broken");
    }

    // Hudson City University: its own row, under its own name and icon, there while its part is on, and its place is its
    // building, which no other row has.
    private static void MapRowUniversity(List<MapRow> rows, string links, Dictionary<string, string> en, Dictionary<string, string> pt, Dictionary<string, string> zh)
    {
        MapRow uni = rows.Find(r => r.Id == "university");
        Check(uni != null && uni.TitleKey == "reallife_map_row_university" && uni.InStreet, "map rows: the university has its own row, reallife_map_row_university, in the street too");
        int same = 0;
        foreach (MapRow row in rows)
        {
            same += uni != null && row.Icon == uni.Icon ? 1 : 0;
        }
        Check(uni != null && uni.Icon == "Mortarboard" && same == 1, "map rows: the university's icon (MapIcon." + (uni != null ? uni.Icon : "none") + ") is its own, no other row's");
        foreach (var (code, texts) in new[] { ("en", en), ("pt", pt), ("zh-cn", zh) })
        {
            texts.TryGetValue("reallife_map_row_university", out string title);
            Check(title == "Hudson City University", "map rows: the university's row is named Hudson City University in " + code + " (names stay in English), not '" + title + "'");
        }
        string declare = Body(links, "private void Declare()");
        int at = declare.IndexOf("Id = \"university\"", StringComparison.Ordinal);
        int next = at >= 0 ? declare.IndexOf("new Link", at, StringComparison.Ordinal) : -1;
        string link = at >= 0 ? declare.Substring(at, (next > at ? next : declare.Length) - at) : string.Empty;
        Check(link.Contains("Enabled = () => Careers.CareersDirector.UniversityOpen,"), "map rows: the university's row is there only while the university is open (CareersDirector.UniversityOpen)");
        string stamp = Body(links, "private static long UniversityStamp()");
        Check(stamp.Contains("careers.University") && stamp.Contains("!uni.Usable") && stamp.Contains("return 0;"),
            "map rows: the university's row waits for its building and goes when its part is switched off (University is null then)");
        string fill = Body(links, "private static void UniversityPlace(");
        Check(fill.Contains("new MapPlace(uni.Front,") && fill.Contains(", uni.Building)"), "map rows: the university's place is its building, which its row lights on the open map");
        Check(!Body(links, "private static void StudyPlaces(").Contains("University") && !Body(links, "private static long StudyStamp()").Contains("University"),
            "map rows: the university is in no other row (a building lit by two rows would take either's colour)");
    }

    // Nothing put on the map before the map can take it: a marker only while the map is switched on, a building lit only
    // while the map is open, a row only once the game's list has its own rows, a kind only once it has a place.
    private static void MapRowTiming(string scripts, string places, string links)
    {
        string mark = Body(places, "private static void Mark(MapPlace place)");
        int asked = mark.IndexOf("if (!CanMark(city))", StringComparison.Ordinal);
        int made = mark.IndexOf(".AddPoi(", StringComparison.Ordinal);
        Check(asked >= 0 && made > asked && mark.Substring(asked, made - asked).Contains("return;"),
            "map rows: a place's marker is made only when CanMark says the map can take it, and waits for the next look otherwise");
        string can = Body(places, "internal static bool CanMark(CityManager city)");
        Check(can.Contains("city.cityMap.gameObject.activeInHierarchy") && can.Contains("poiTemplate != null"),
            "map rows: CanMark asks that the map is there and switched on (a marker made while it is off is never woken: 0.17.1)");
        string late = Body(places, "private void OnLateUpdate()");
        int open = late.IndexOf("if (_mapOpen)", StringComparison.Ordinal);
        int light = late.IndexOf("Light();", StringComparison.Ordinal);
        Check(open >= 0 && light > open && Regex.Matches(places, "CreatePOI\\(\\)").Count == 1 && Body(places, "private static void Light()").Contains("building.CreatePOI();"),
            "map rows: a building is lit (its marker made) only while the map is open");
        string rows = Body(places, "private static void AddRows(CityMapFilters filters)");
        int empty = rows.IndexOf("if (toggles.Count == 0)", StringComparison.Ordinal);
        int create = rows.IndexOf("create.Invoke(", StringComparison.Ordinal);
        Check(empty >= 0 && create > empty, "map rows: the rows are added only once the game's filter list has its own");
        string look = Body(links, "private void Look(Link link)");
        int none = look.IndexOf("if (_scratch.Count == 0)", StringComparison.Ordinal);
        int register = look.IndexOf("MapPlaces.Register(", StringComparison.Ordinal);
        Check(none >= 0 && register > none, "map rows: a kind's row is made only once it has a place");
        Check(!links.Contains(".AddPoi(") && !links.Contains("CreatePOI"), "map rows: MapLinks hands its places to MapPlaces and makes no marker itself");
        int makers = 0;
        foreach (string file in Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string name = Path.GetRelativePath(scripts, file).Replace('\\', '/');
            string code = Code(File.ReadAllText(file));
            if (name == "Core/MapPlaces.cs" || !code.Contains(".AddPoi("))
            {
                continue;
            }
            makers++;
            Check(code.Contains("MapPlaces.Drop("), "map rows: " + name + " gives the map a marker and lets it go through MapPlaces.Drop (out of the game's list first)");
        }
        Check(makers >= 2, "map rows: the sources other than MapPlaces that make a marker are found (" + makers + ")");
    }

    // What's new says so in the three languages, in whichever row it is by now: the release turns the next row into the
    // version's own, so every reallife_changes_ row is read, never only the next.
    private static void MapRowNews(Dictionary<string, string> en, Dictionary<string, string> pt, Dictionary<string, string> zh)
    {
        Check(MapRowSaid(en, "Fixed: ", "Hudson City University had no row of its own"), "map rows: What's new has the fix in en");
        Check(MapRowSaid(pt, "Corrigido: ", "a Hudson City University não tinha uma linha própria"), "map rows: What's new has the fix in pt");
        Check(MapRowSaid(zh, "已修复：", "Hudson City University 在城市地图的筛选列表中没有自己的一行"), "map rows: What's new has the fix in zh-cn");
    }

    private static readonly Regex ChangesRow = new Regex("^reallife_changes_(next|\\d{3}_\\d+)$", RegexOptions.Compiled);

    private static bool MapRowSaid(Dictionary<string, string> texts, string prefix, string words)
    {
        foreach (KeyValuePair<string, string> pair in texts)
        {
            if (!ChangesRow.IsMatch(pair.Key))
            {
                continue;
            }
            if (pair.Value.IndexOf(prefix + words, StringComparison.Ordinal) >= 0)   // (a row joins its entries with spaces)
            {
                return true;
            }
        }
        return false;
    }

    private static bool MapText(Dictionary<string, string> texts, string key)
    {
        return texts.TryGetValue(key, out string text) && !string.IsNullOrWhiteSpace(text);
    }

    private static Dictionary<string, string> MapTexts(string locales, string code)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        string path = Path.Combine(locales, code + ".json");
        if (!File.Exists(path))
        {
            Check(false, "map rows: the " + code + " texts are at " + path);
            return texts;
        }
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (JsonProperty p in doc.RootElement.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.String)
            {
                texts[p.Name] = p.Value.GetString();
            }
        }
        return texts;
    }
}
