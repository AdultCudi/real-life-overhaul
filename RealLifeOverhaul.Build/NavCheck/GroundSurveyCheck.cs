using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Uptown;

// "What is this ground?" (Scripts/Uptown/GroundSurvey.cs): the district's bare blocks on the real plan (its three squares and
// the landmarks' three blocks, the campus's square among them), every lot's middle read back as that lot, the big places'
// ground, the open runs, the streets, avenues and crossings, the game's own city and the scenery past the walls, the nearest
// streets of a block, the same answer every time; the sources (game-free rules, the test's line, the guide's entry) and the
// texts in en, pt and zh-cn.
internal static partial class Program
{
    private static void GroundSurveys()
    {
        GroundSurveyBlockChecks();
        GroundSurveyLotChecks();
        GroundSurveyStreetChecks();
        GroundSurveySourceChecks();
    }

    private static void GroundSurveyBlockChecks()
    {
        var bare = new List<int>();
        GroundSurvey.BareBlocks(bare);
        bare.Sort();
        Check(string.Join(",", bare) == "110,111,113,117,202,309",
            "ground survey: the band's bare blocks are the landmarks' three (110, 111, 113) and the plan's three squares (117, 202, 309): " + string.Join(",", bare));
        Check(LayoutRules.CampusBlock == 202 && LayoutRules.IsSquare(2, 2) && LayoutRules.Squares.Contains(202), "ground survey: the campus stands on the plan's west square, which keeps no lots");
        Check(LayoutRules.Lots.All(l => l.Block != LayoutRules.CampusBlock), "ground survey: no lot of the plan is on the campus's square");
        // The middle of each bare block, read as what it is.
        var expect = new Dictionary<int, GroundKind>
        {
            { 202, GroundKind.Campus }, { 309, GroundKind.ParkSquare }, { 117, GroundKind.ParkSquare },
            { 110, GroundKind.LandmarkSite }, { 111, GroundKind.LandmarkSite }, { 113, GroundKind.LandmarkSite }
        };
        foreach (KeyValuePair<int, GroundKind> e in expect)
        {
            LayoutRules.Block(e.Key / 100, e.Key % 100, out float x0, out float z0, out float x1, out float z1);
            GroundAnswer a = GroundSurvey.At((x0 + x1) * 0.5f, (z0 + z1) * 0.5f);
            Check(a.Kind == e.Value && a.Block == e.Key, "ground survey: block " + e.Key + "'s middle is " + e.Value + " (" + a.Kind + ", block " + a.Block + ")");
        }
        GroundSurvey.StreetsOf(202, out string west, out string east, out string south, out string north);
        Check(west != null && west.EndsWith("_birch") && east != null && east.EndsWith("_elm") && south != null && south.EndsWith("_uptownavenue") && north != null && north.EndsWith("_summitavenue"),
            "ground survey: block 202 lies between Birch Street and Elm Street, Uptown Avenue and Summit Avenue (" + west + ", " + east + ", " + south + ", " + north + ")");
        GroundSurvey.StreetsOf(110, out west, out east, out south, out north);
        Check(west != null && west.EndsWith("_fifth") && east != null && east.EndsWith("_fourth") && south == GroundSurvey.FirstAvenue && north != null && north.EndsWith("_uptownavenue"),
            "ground survey: block 110 lies between 5th and 4th Street, behind 1st Avenue's north side to Uptown Avenue (" + west + ", " + east + ", " + south + ", " + north + ")");
        LayoutRules.Block(2, 2, out float bx0, out float bz0, out float bx1, out float bz1);
        GroundAnswer inside = GroundSurvey.At((bx0 + bx1) * 0.5f, (bz0 + bz1) * 0.5f);
        Check(inside.West != null && inside.West.EndsWith("_birch") && inside.North != null && inside.North.EndsWith("_summitavenue"),
            "ground survey: a spot on the campus names Birch Street to its west and Summit Avenue to its north (" + inside.West + ", " + inside.North + ")");
    }

    // Every lot's middle is that lot (an open run's space its own), every big place's middle its ground; the same every time.
    private static void GroundSurveyLotChecks()
    {
        int wrong = 0, open = 0, landmark = 0;
        string first = null;
        foreach (Lot l in LayoutRules.Lots)
        {
            GroundAnswer a = GroundSurvey.At(l.X, l.Z);
            bool right = a.Lot == l.Id && a.Block == l.Block && a.LotNumber == l.Number && a.LotStreet == l.Street
                         && (l.Open ? a.Kind == GroundKind.OpenRun : a.Kind == GroundKind.Lot || a.Kind == GroundKind.LandmarkSite);
            open += a.Kind == GroundKind.OpenRun ? 1 : 0;
            landmark += a.Kind == GroundKind.LandmarkSite ? 1 : 0;
            if (!right)
            {
                wrong++;
                first = first ?? l.Id + " read as " + a.Kind + " " + a.Lot;
            }
        }
        Check(wrong == 0, "ground survey: every one of the plan's " + LayoutRules.Lots.Count + " lots is read back at its middle as itself (" + wrong + " not: " + first + ")");
        Check(open == LayoutRules.Lots.Count(l => l.Open) && open > 0, "ground survey: the open runs' spaces read as land for sale: " + open);
        Check(landmark == LayoutRules.Lots.Count(l => GroundSurvey.IsLandmarkBlock(l.Block)) && landmark > 0, "ground survey: the landmarks' blocks' lots read as their sites: " + landmark);
        foreach (Lot p in LayoutRules.Places)
        {
            GroundAnswer a = GroundSurvey.At(p.X, p.Z);
            Check(a.Kind == GroundKind.BigPlace && a.Lot == p.Id, "ground survey: the big place " + p.Id + " reads as its ground (" + a.Kind + " " + a.Lot + ")");
        }
        Lot any = LayoutRules.Lots[LayoutRules.Lots.Count / 2];
        GroundAnswer once = GroundSurvey.At(any.X, any.Z), again = GroundSurvey.At(any.X, any.Z);
        Check(once.Kind == again.Kind && once.Lot == again.Lot && once.West == again.West && once.North == again.North, "ground survey: the same spot gives the same answer");
    }

    private static void GroundSurveyStreetChecks()
    {
        float birch = LayoutRules.StreetX(-4), uptownAvenue = LayoutRules.AvenueZ(1);
        GroundAnswer street = GroundSurvey.At(birch + 1f, uptownAvenue + 60f);
        Check(street.Kind == GroundKind.Street && street.On != null && street.On.EndsWith("_birch"), "ground survey: Birch Street's carriageway is a street (" + street.Kind + " " + street.On + ")");
        GroundAnswer avenue = GroundSurvey.At(birch + 40f, uptownAvenue + 2f);
        Check(avenue.Kind == GroundKind.Avenue && avenue.On != null && avenue.On.EndsWith("_uptownavenue"), "ground survey: Uptown Avenue is an avenue (" + avenue.Kind + " " + avenue.On + ")");
        GroundAnswer crossing = GroundSurvey.At(birch, uptownAvenue);
        Check(crossing.Kind == GroundKind.Crossing, "ground survey: where Birch Street meets Uptown Avenue is a crossing (" + crossing.Kind + ")");
        GroundAnswer game = GroundSurvey.At(0f, 200f);
        Check(game.Kind == GroundKind.GameGround && game.West == null && game.Block == 0, "ground survey: Midtown south of 1st Avenue is the game's own city, its streets the game side's (" + game.Kind + ")");
        GroundAnswer past = GroundSurvey.At(0f, LayoutRules.AvenueZ(LayoutRules.Rows) + LayoutRules.AvenueHalf + LayoutRules.RingDepth(true) + 20f);
        Check(past.Kind == GroundKind.PastWalls, "ground survey: north of the ring behind Crest Avenue is the scenery past the walls (" + past.Kind + ")");
        // Between the lots of a built block: a block of row 2 west of the campus, halfway between its strips.
        LayoutRules.Block(2, 3, out float x0, out float z0, out float x1, out float z1);
        GroundAnswer between = GroundSurvey.At((x0 + x1) * 0.5f, (z0 + z1) * 0.5f);
        Check(between.Kind == GroundKind.Between || between.Kind == GroundKind.Lot, "ground survey: inside a built block is a lot or the ground between its buildings (" + between.Kind + ")");
    }

    private static void GroundSurveySourceChecks()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string rules = File.ReadAllText(Path.Combine(scripts, @"Uptown\GroundSurvey.cs"));
        string test = File.ReadAllText(Path.Combine(scripts, @"Uptown\GroundDirector.Survey.cs"));
        string guide = File.ReadAllText(Path.Combine(scripts, @"Uptown\GroundGuide.cs"));
        Check(!Regex.IsMatch(rules, @"\bUnityEngine\b|\bRandom\b|DateTime|\bTime\."), "ground survey sources: the rules are game-free and leave nothing to chance");
        Check(test.Contains("Ground check (what is this ground)") && test.Contains("Notifications.Show") && test.Contains("TestRefusals.Tell"),
            "ground survey sources: the test writes its line and a notice, or says why not");
        Check(guide.Contains("\"reallife_uptown_ground_test_what\", GroundDirector.TestWhatGround"), "ground survey sources: Options > Tests has What is this ground?");
        Check(!Regex.IsMatch(test, @"FindObjectsOfType|FindObjectsByType|GameObject\.Find\b|\bHarmony\b"), "ground survey sources: no whole-scene search, no patch");
        string locales = Path.GetFullPath(Path.Combine(scripts, @"..\Locales"));
        var keys = Regex.Matches(File.ReadAllText(Path.Combine(scripts, @"Uptown\GroundText.cs")), "\"(reallife_uptown_ground_(?:what|test_what)[a-z_]*)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        Check(keys.Count == 15, "ground survey texts: its keys are found: " + keys.Count);
        foreach (string language in new[] { "en", "pt", "zh-cn" })
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(locales, language + ".json")));
            foreach (string key in keys)
            {
                bool has = doc.RootElement.TryGetProperty(key, out JsonElement value) && value.GetString().Length > 0;
                Check(has, "ground survey texts: " + key + " is in " + language + ".json");
                if (has)
                {
                    string text = value.GetString();
                    Check(!Regex.IsMatch(text, "[–—!]| - "), "ground survey texts: " + key + " has no dash and no exclamation mark in " + language);
                    using JsonDocument en = JsonDocument.Parse(File.ReadAllText(Path.Combine(locales, "en.json")));
                    string english = en.RootElement.GetProperty(key).GetString();
                    var mine = Regex.Matches(text, @"\{[a-z_]+\}").Select(m => m.Value).OrderBy(v => v);
                    var theirs = Regex.Matches(english, @"\{[a-z_]+\}").Select(m => m.Value).OrderBy(v => v);
                    Check(mine.SequenceEqual(theirs), "ground survey texts: " + key + " has the English text's placeholders in " + language);
                    if (language == "en")
                    {
                        Check(text.Length <= 240, "ground survey texts: " + key + " is 240 characters at most in en");
                    }
                }
            }
        }
    }
}
