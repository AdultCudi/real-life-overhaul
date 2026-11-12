using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;

// A player's report (12 Nov 2026, a few hours after 1.0.0): "all Korean text has completely disappeared and is
// invisible". No glyph was missing. The interface's face, Overpass, holds Latin and Cyrillic letters; Hangul and Chinese
// are drawn by the game's own fonts, TextMeshPro's general fallbacks, and TextMeshPro lays a line out by the font of each
// of its characters. The game's Noto fonts stand 1.448 em from ascender to descender, the face 1.266, and the kit laid
// every text in a box one line of the face tall: a text that may be cut is ended at its first character when a line is
// taller than its box, so every such line of Korean, Chinese or Japanese drew nothing (and a Greek one, and any name
// typed in those scripts). The offline preview had met it and cut its own stand-in font's line, so its pictures were
// right while the game's were not. Now the faces fall back to the mod's own copies of the game's fonts with their line
// cut to the face's, ordered by language, every text's box has room no line needs, a character is measured as the font
// that draws it draws it, and one line of Player.log says what a language's characters came to
// (Ui/UiFallbackRules.cs, Ui/UiFonts.Fallbacks.cs, Ui/UiTextRules.cs, Ui/UiView.Slots.cs, Ui/UiDirector.Fonts.cs).
// Held here against the census of the game's fonts (Census/fonts-NNNN.json, written by Census/game_fonts.py from the
// installed game): the facts, the rules, every character of the eleven locale files and of the game's own words in
// every language it offers, and the sources that carry it out.
//
// The same fault stood, smaller, outside the interface: the mod's older screens (Flare's own kit, the Camera, the
// Gallery, the City Ledger and others) write in the game's font in boxes sized by its line, under 1.2 em, and the text
// engine dropped a Korean or Chinese line there too wherever a box was under 1.448 em (29 of Flare's kit's texts by
// their numbers). Every text the mod has the engine cut outside the interface's view now carries its room
// (Core/GameUi/TextRoom.cs, TextRoomRules.cs): its margins give a box that is too short what it lacks for one line of
// the tallest font, on the side where its alignment leaves the words where they were.
internal static partial class Program
{
    private static void FontFallbacks()
    {
        FontCensus census = FontCensus.Read(out string why);
        Check(census != null, "fonts: the census of the game's fonts is read (" + (why ?? "RealLifeOverhaul.Build/Census/fonts-NNNN.json") + ")");
        if (census != null)
        {
            FontCensusChecks(census);
            FontRuleChecks(census);
            FontLocaleChecks(census);
            FontRoomChecks(census);
        }
        FontTextRuleChecks();
        FontSourceChecks();
        FontWhatsNewChecks();
    }

    // ------------------------------------------------------------------------------------------------- the census

    private sealed class GameFont
    {
        public string Name, Family, Mode, Source;
        public int Point, Upem, Listed;
        public float Ascent, Descent, Cap;
        public string[] Fallbacks;
        public bool[] Covers = new bool[65536];
        public int[] Advance = new int[65536];

        public float LineEm => (Ascent - Descent) / Point;
    }

    private sealed class FontCensus
    {
        public readonly Dictionary<string, GameFont> Fonts = new Dictionary<string, GameFont>(StringComparer.Ordinal);
        public readonly Dictionary<string, bool[]> Words = new Dictionary<string, bool[]>(StringComparer.Ordinal);
        public string Default, File;
        public string[] General = new string[0];
        public int MissingGlyph;

        // The newest census beside the script that writes it.
        public static FontCensus Read(out string why)
        {
            why = null;
            string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\Census"));
            string newest = null;
            int build = -1;
            foreach (string file in Directory.Exists(folder) ? Directory.GetFiles(folder, "fonts-*.json") : new string[0])
            {
                string number = System.IO.Path.GetFileNameWithoutExtension(file).Substring(6);
                if (int.TryParse(number, out int n) && n > build)
                {
                    build = n;
                    newest = file;
                }
            }
            if (newest == null)
            {
                why = "no fonts-NNNN.json in " + folder;
                return null;
            }
            try
            {
                var census = new FontCensus { File = System.IO.Path.GetFileName(newest) };
                using JsonDocument doc = JsonDocument.Parse(System.IO.File.ReadAllText(newest));
                JsonElement settings = doc.RootElement.GetProperty("settings");
                census.Default = settings.GetProperty("default").GetString();
                census.General = Strings(settings.GetProperty("fallbacks"));
                census.MissingGlyph = settings.GetProperty("missing_glyph").GetInt32();
                foreach (JsonProperty entry in doc.RootElement.GetProperty("fonts").EnumerateObject())
                {
                    JsonElement f = entry.Value;
                    var font = new GameFont
                    {
                        Name = entry.Name, Family = f.GetProperty("family").GetString(), Mode = f.GetProperty("mode").GetString(),
                        Source = f.GetProperty("source").ValueKind == JsonValueKind.String ? f.GetProperty("source").GetString() : null,
                        Point = f.GetProperty("point").GetInt32(), Upem = f.GetProperty("upem").GetInt32(), Ascent = f.GetProperty("ascent").GetSingle(),
                        Descent = f.GetProperty("descent").GetSingle(), Cap = f.GetProperty("cap").GetSingle(), Fallbacks = Strings(f.GetProperty("fallbacks")),
                    };
                    Ranges(f.GetProperty("covers"), (first, last, value) => Fill(font.Covers, first, last));
                    Array.Fill(font.Advance, -1);
                    Ranges(f.GetProperty("advances"), (first, last, value) =>
                    {
                        for (int c = first; c <= last; c++)
                        {
                            font.Advance[c] = font.Covers[c] ? value : -1;
                        }
                    });
                    census.Fonts[font.Name] = font;
                }
                foreach (JsonProperty entry in doc.RootElement.GetProperty("words").EnumerateObject())
                {
                    var seen = new bool[65536];
                    Ranges(entry.Value.GetProperty("covers"), (first, last, value) => Fill(seen, first, last));
                    census.Words[entry.Name] = seen;
                }
                return census;
            }
            catch (Exception e)
            {
                why = System.IO.Path.GetFileName(newest) + ": " + e.GetType().Name + ": " + e.Message;
                return null;
            }
        }

        private static void Fill(bool[] set, int first, int last)
        {
            for (int c = first; c <= last && c < set.Length; c++)
            {
                set[c] = true;
            }
        }

        private static string[] Strings(JsonElement list)
        {
            var found = new List<string>();
            foreach (JsonElement item in list.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    found.Add(item.GetString());
                }
            }
            return found.ToArray();
        }

        // The census writes ranges as lines of words: "first-last" in hex, with ":units" after it for an advance.
        private static void Ranges(JsonElement lines, Action<int, int, int> each)
        {
            foreach (JsonElement line in lines.EnumerateArray())
            {
                foreach (string word in line.GetString().Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    int colon = word.IndexOf(':'), dash = word.IndexOf('-');
                    string span = colon >= 0 ? word.Substring(0, colon) : word;
                    int first = Convert.ToInt32(span.Substring(0, dash), 16), last = Convert.ToInt32(span.Substring(dash + 1), 16);
                    each(first, last, colon >= 0 ? int.Parse(word.Substring(colon + 1), System.Globalization.CultureInfo.InvariantCulture) : 0);
                }
            }
        }

        /// <summary>The fonts a character falls back to as the game's TextMeshPro asks them: the general fallbacks and their own, then the default font and its own.</summary>
        public List<GameFont> Asked()
        {
            var asked = new List<GameFont>();
            void Note(string name)
            {
                if (name != null && Fonts.TryGetValue(name, out GameFont font) && !asked.Contains(font))
                {
                    font.Listed = asked.Count;
                    asked.Add(font);
                }
            }
            void NoteWithOwn(string name)
            {
                Note(name);
                if (name != null && Fonts.TryGetValue(name, out GameFont font))
                {
                    foreach (string own in font.Fallbacks)
                    {
                        Note(own);
                    }
                }
            }
            foreach (string name in General)
            {
                NoteWithOwn(name);
            }
            NoteWithOwn(Default);
            return asked;
        }

        /// <summary>
        /// The faces' chain for a language as the mod builds it (UiFonts.Order): one entry a font file, in the rules' order.
        /// <paramref name="copies"/> false gives the fonts as the game asks them, the kit's order left out: the chain before the fix.
        /// </summary>
        public List<GameFont> Chain(string language, bool copies = true)
        {
            var chain = new List<GameFont>();
            foreach (GameFont font in Asked())
            {
                if (font.Source != null && chain.Exists(had => had.Source == font.Source))
                {
                    continue;   // a second asset over the same font file: one copy stands for both
                }
                int at = chain.Count;
                while (copies && at > 0 && UiFallbackRules.Rank(chain[at - 1].Family, language, chain[at - 1].Listed) > UiFallbackRules.Rank(font.Family, language, font.Listed))
                {
                    at--;
                }
                chain.Insert(at, font);
            }
            return chain;
        }
    }

    private static readonly UiFace FontFace = UiFontMetrics.Face(UiFamily.Overpass, 400);
    private static readonly string[] FontRegions = { "Noto Sans JP", "Noto Sans KR", "Noto Sans SC" };

    private static GameFont FontDrawing(List<GameFont> chain, int c)
    {
        foreach (GameFont font in chain)
        {
            if (font.Covers[c])
            {
                return font;
            }
        }
        return null;
    }

    private static void FontCensusChecks(FontCensus census)
    {
        List<GameFont> asked = census.Asked();
        Check(census.Default != null && census.Fonts.ContainsKey(census.Default) && census.General.Length >= 4 && asked.Count >= 5,
            "fonts census: " + census.File + " names TextMeshPro's default font and general fallbacks in the game, each a font of the census (" + asked.Count + " asked)");
        Check(census.MissingGlyph == '?' && FontFace.Advance('?') >= 0,
            "fonts census: a character no font draws is replaced by the game's missing glyph character, a question mark, which the face draws: never by nothing");
        float faceLine = FontFace.LineEm;
        var tall = new List<string>();
        foreach (string family in FontRegions)
        {
            GameFont font = asked.Find(f => f.Family == family);
            Check(font != null && font.Source != null && font.Mode == "dynamic" && UiFallbackRules.Region(family) == family.Substring(family.Length - 2),
                "fonts census: " + family + " is one of the game's fallbacks, with a font file of its own, and its name says its region");
            if (font != null)
            {
                Check(Math.Abs(font.LineEm - 1.448f) < 0.001f && font.LineEm > faceLine + 0.15f,
                    "fonts census: " + family + "'s line is 1.448 em from ascender to descender, taller than the face's " + faceLine.ToString("0.000") + " (" + font.LineEm.ToString("0.000") + ")");
                tall.Add(family);
            }
        }
        Check(tall.Count == 3, "fonts census: the game's Chinese, Japanese and Korean fonts are all three among its fallbacks");
        GameFont western = asked.Find(f => f.Source != null && UiFallbackRules.Region(f.Family) == null);
        Check(western != null && western.Family == UiFallbackRules.WesternFamily && western.Upem == UiFallbackRules.WesternUnits,
            "fonts census: the first of the game's fallbacks with a font file and no region is the Western family the kit measures by (" + (western?.Family ?? "none") + ")");
        if (western != null)
        {
            int same = 0, drawn = 0;
            for (int c = 0; c < 65536; c++)
            {
                drawn += western.Covers[c] ? 1 : 0;
                same += western.Covers[c] && UiFallbackRules.WesternAdvance((char)c) == western.Advance[c] ? 1 : 0;
                same -= !western.Covers[c] && UiFallbackRules.WesternAdvance((char)c) >= 0 ? 1 : 0;
            }
            Check(drawn == UiFallbackRules.WesternCount && same == drawn && drawn > 2000,
                "fonts census: the kit's table of the Western fallback is the census's, character for character and advance for advance (" + same + " of " + drawn
                + "; run Census/game_fonts.py --data)");
        }
        // What the rule says of a character drawn by a region's font: a Hangul letter one advance, anything else a square at the most.
        int hangul = 0, hangulOff = 0, wider = 0, square = 0;
        string firstWider = null;
        foreach (GameFont font in asked)
        {
            if (UiFallbackRules.Region(font.Family) == null || font.Source == null)
            {
                continue;
            }
            for (int c = 32; c < 65536; c++)
            {
                if (!font.Covers[c] || FontFace.Advance((char)c) >= 0 || UiFallbackRules.WesternAdvance((char)c) >= 0)
                {
                    continue;   // drawn by the face or by the Western fallback before any region's font
                }
                float em = font.Advance[c] / (float)font.Upem;
                if (UiFallbackRules.Hangul((char)c))
                {
                    hangul++;
                    hangulOff += Math.Abs(em - UiFallbackRules.HangulEm) > 0.0005f ? 1 : 0;
                }
                else
                {
                    square++;
                }
                if (em > UiFallbackRules.SquareEm((char)c) + 0.0005f)
                {
                    wider++;
                    firstWider ??= "U+" + c.ToString("X4") + " in " + font.Family + " " + em.ToString("0.000");
                }
            }
        }
        Check(hangul > 11000 && hangulOff == 0, "fonts census: every Hangul letter of the game's fonts advances " + UiFallbackRules.HangulEm + " em, as the kit measures it ("
                                                 + hangul + " looked at, " + hangulOff + " other)");
        Check(square > 40000 && wider == 0, "fonts census: no character of the game's Chinese, Japanese and Korean fonts is drawn wider than the kit measures it (" + square
                                            + " looked at, " + wider + " wider" + (firstWider != null ? ", the first " + firstWider : string.Empty) + ")");
        Check(UiFallbackRules.Region("Noto Sans KR") == "KR" && UiFallbackRules.Region("Noto Sans SC") == "SC" && UiFallbackRules.Region("Noto Sans JP") == "JP"
              && UiFallbackRules.Region("Noto Sans TC") == "TC" && UiFallbackRules.Region("Noto Sans HK") == "HK" && UiFallbackRules.Region("noto sans kr") == "KR"
              && UiFallbackRules.Region("KR") == "KR" && UiFallbackRules.Region("Noto Sans") == null && UiFallbackRules.Region("Liberation Sans") == null
              && UiFallbackRules.Region("Rubik") == null && UiFallbackRules.Region("PARK") == null && UiFallbackRules.Region("K") == null
              && UiFallbackRules.Region(string.Empty) == null && UiFallbackRules.Region(null) == null,
            "fonts rules: a family's region is the last word of its name, and a family without one has none");
    }

    // ------------------------------------------------------------------------------------------------- the rules

    private static void FontRuleChecks(FontCensus census)
    {
        // The order of a language's chain.
        string Order(string language)
        {
            var names = new List<string>();
            foreach (GameFont font in census.Chain(language))
            {
                names.Add(font.Family);
            }
            return string.Join(", ", names);
        }
        Check(Order("ko").StartsWith("Liberation Sans, Noto Sans KR, Noto Sans", StringComparison.Ordinal), "fonts rules: Korean is written by the Western font, then the Korean one, then the others (" + Order("ko") + ")");
        Check(Order("zh-cn").StartsWith("Liberation Sans, Noto Sans SC, Noto Sans JP, Noto Sans KR", StringComparison.Ordinal),
            "fonts rules: Chinese is written by the simplified font before the Japanese one, whose shapes for many characters are Japan's (" + Order("zh-cn") + ")");
        Check(Order("zh-tw").StartsWith("Liberation Sans, Noto Sans SC", StringComparison.Ordinal) && Order("ja").StartsWith("Liberation Sans, Noto Sans JP", StringComparison.Ordinal),
            "fonts rules: traditional Chinese takes the simplified font, the nearest the game has, and Japanese its own (" + Order("zh-tw") + " | " + Order("ja") + ")");
        Check(Order("en").StartsWith("Liberation Sans, Noto Sans JP, Noto Sans KR, Noto Sans SC", StringComparison.Ordinal) && Order("en") == Order("el") && Order("en") == Order(null),
            "fonts rules: a language of Latin, Greek or Cyrillic letters keeps the game's own order after the Western font (" + Order("en") + ")");
        Check(Order("en") != string.Join(", ", census.Chain("en", false).ConvertAll(f => f.Family)) && census.Chain("ko", false)[0].Family != "Liberation Sans",
            "fonts rules: the game's own order asks its Chinese, Japanese and Korean fonts before its Western one, which the kit's order changes");
        bool total = true;
        foreach (string language in new[] { null, "en", "ko", "zh-cn", "zh-tw", "ja", "el", "ru", "xx" })
        {
            var ranks = new HashSet<int>();
            foreach (GameFont font in census.Chain(language))
            {
                total &= ranks.Add(UiFallbackRules.Rank(font.Family, language, font.Listed));
            }
        }
        Check(total, "fonts rules: no two fonts of a chain share a place, in any language");
        Check(UiFallbackRules.Wants("ko", "KR") == 0 && UiFallbackRules.Wants("ko", "SC") < 0 && UiFallbackRules.Wants("zh-cn", "SC") == 0 && UiFallbackRules.Wants("zh-cn", "JP") < 0
              && UiFallbackRules.Wants("zh-tw", "TC") == 0 && UiFallbackRules.Wants("zh-tw", "HK") == 1 && UiFallbackRules.Wants("zh-tw", "SC") == 2 && UiFallbackRules.Wants("ja", "JP") == 0
              && UiFallbackRules.Wants("en", "KR") < 0 && UiFallbackRules.Wants(null, "KR") < 0 && UiFallbackRules.Wants("ko", null) < 0,
            "fonts rules: each language asks for its own script's font first, traditional Chinese for a traditional one before the simplified, and no other language for any");
        Check(UiFallbackRules.Rank("Liberation Sans", "ko", 4) == 0 && UiFallbackRules.Rank("Noto Sans KR", "ko", 2) < UiFallbackRules.Rank("Noto Sans JP", "ko", 1)
              && UiFallbackRules.Rank("Noto Sans JP", "ko", 1) < UiFallbackRules.Rank("Noto Sans SC", "ko", 3) && UiFallbackRules.Rank("Noto Sans SC", "ko", 3) < UiFallbackRules.Rank("Rubik", "ko", 0)
              && UiFallbackRules.Rank("Rubik", "ko", 0) < UiFallbackRules.Rank("Noto Sans", "ko", 5),
            "fonts rules: the Western font first, the language's own, the other regions' in the game's order, then every other font in the game's order");

        // The line cut: a copy stands as tall as the face at any point size.
        const float FaceAscent = 42.384f, FaceDescent = -18.384f;   // the bundle's own, at its 48 px
        const int FacePoint = 48;
        bool cut = true;
        foreach (int point in new[] { 40, 48, 90, 175 })
        {
            float ascent = UiFallbackRules.Cut(FaceAscent, FacePoint, point), descent = UiFallbackRules.Cut(FaceDescent, FacePoint, point);
            cut &= Math.Abs((ascent - descent) / point - (FaceAscent - FaceDescent) / FacePoint) < 0.00001f && Math.Abs(ascent / point - FaceAscent / FacePoint) < 0.00001f;
        }
        Check(cut && UiFallbackRules.Cut(5f, 0, 40) == 5f && Math.Abs((FaceAscent - FaceDescent) / FacePoint - FontFace.LineEm) < 0.0005f,
            "fonts rules: a copy's line cut to the face's is the face's own, ascender and descender, at every point size, and the bundle's face stands as the kit measures it");

        // TextMeshPro's own test, and what it did to the box before the fix: a box one line of the face tall.
        Check(!UiFallbackRules.Ends(10f, 10f) && !UiFallbackRules.Ends(10.00005f, 10f) && UiFallbackRules.Ends(10.001f, 10f) && !UiFallbackRules.Ends(9f, 10f),
            "fonts rules: a text that may be cut is ended by a line over its box, not by one that fills it");
        int ended = 0, kept = 0, endedWithRoom = 0, endedCopy = 0, gains = 0, wrapEnded = 0;
        string keptStyles = string.Empty;
        for (int s = 0; s < (int)UiStyle.Count; s++)
        {
            var style = (UiStyle)s;
            UiType type = UiTokens.Type(style);
            float box = UiTextRules.LineHeight(style), tallest = 1.448f * type.Size, face = FontFace.LineEm * type.Size;
            if (UiFallbackRules.Ends(tallest, box))
            {
                ended++;
            }
            else
            {
                kept++;
                keptStyles += (keptStyles.Length > 0 ? ", " : string.Empty) + style;
            }
            UiTextRules.Room(style, UiFlags.Truncate, out float above, out float below);
            UiTextRules.Room(style, UiFlags.Truncate | UiFlags.Middle, out float aboveMiddle, out float belowMiddle);
            endedWithRoom += UiFallbackRules.Ends(2.2f * type.Size, box + above + below) || UiFallbackRules.Ends(2.2f * type.Size, box + aboveMiddle + belowMiddle) ? 1 : 0;
            endedCopy += UiFallbackRules.Ends(face, box) ? 1 : 0;
            UiTextRules.Room(style, UiFlags.Truncate | UiFlags.Wrap, out float aboveWrap, out float belowWrap);
            wrapEnded += aboveWrap != 0f || UiFallbackRules.Ends(tallest, box + belowWrap) ? 1 : 0;
            for (int lines = 1; lines <= 8; lines++)
            {
                // One line more than a block was cut to, as TextMeshPro counts it: each line the face's, the style's leading between two.
                float more = (lines + 1) * face + lines * (type.Leading - 1f) * face;
                gains += UiFallbackRules.Ends(more, lines * box + belowWrap) ? 0 : 1;
            }
        }
        Check(ended == 15 && kept == 2 && keptStyles == "Body, BodyStrong",
            "fonts rules: in a box one line of the face tall, as every label had, the game's Korean font ended a text that may be cut in 15 of the 17 styles (" + ended
            + "), all but the two whose leading left it room (" + keptStyles + "): the report's fault");
        Check(endedWithRoom == 0, "fonts rules: with its room, a one-line text's box holds a line of 2.2 em, half as tall again as the game's tallest font, in every style (" + endedWithRoom + " ended)");
        Check(endedCopy == 0, "fonts rules: a copy's line, cut to the face's, fits the box with no room at all in every style (" + endedCopy + " ended)");
        Check(wrapEnded == 0 && gains == 0,
            "fonts rules: a wrapped block's room holds the first line of the game's tallest font and is under a line, so a block cut to one to eight lines never shows one more ("
            + wrapEnded + " ended, " + gains + " gained)");
        bool room = true;
        for (int s = 0; s < (int)UiStyle.Count; s++)
        {
            float size = UiTokens.Type((UiStyle)s).Size;
            UiTextRules.Room((UiStyle)s, UiFlags.None, out float a, out float b);
            room &= a == 0f && b == UiTextRules.LineRoom * size;
            UiTextRules.Room((UiStyle)s, UiFlags.Middle | UiFlags.Centre, out a, out b);
            room &= a == b && a == UiTextRules.LineRoom * size;
            UiTextRules.Room((UiStyle)s, UiFlags.Wrap | UiFlags.Middle, out a, out b);
            room &= a == 0f && b == UiTextRules.WrapRoom * size && b < UiTextRules.LineHeight((UiStyle)s);
        }
        Check(room && UiTextRules.LineRoom >= 1f && UiTextRules.WrapRoom > 0.19f && UiTextRules.WrapRoom < 1f,
            "fonts rules: room stands under a text laid from its top, as much over as under one centred on its box, so neither moves, and under a wrapped block alone");

        // The pages of a copy's atlas.
        Check(UiFallbackRules.PageWidth("Noto Sans KR") == UiFallbackRules.WidePage && UiFallbackRules.PageHeight("Noto Sans SC") == UiFallbackRules.WidePageHeight
              && UiFallbackRules.PageWidth("Liberation Sans") == UiFallbackRules.SmallPage && UiFallbackRules.PageHeight("Rubik") == UiFallbackRules.SmallPage
              && UiFallbackRules.PageHolds("Noto Sans KR") == (4096 / 48) * (2048 / 48) && UiFallbackRules.CopyPadding * 10 == UiFallbackRules.CopyPoint,
            "fonts rules: a region's copy has a wide page, any other a small one, and a glyph's cell is its em with a tenth of it of distance field on each side");

        // The check's line.
        string pass = UiFallbackRules.Line("ko", 1370, 1273, 1273, "Noto Sans KR 1273", 0, null, 12, 12, "Game: Build 3682.");
        Check(pass == "Fonts check (ko): 1370 of the language's distinct characters in the mod's texts, 1273 missing from Overpass, 1273 covered by fallbacks (Noto Sans KR 1273), 0 left: PASS. Game: Build 3682.",
            "fonts line: a language whose characters all have a font, and whose line the kit drew whole, passes, with its counts: " + pass);
        string none = UiFallbackRules.Line("en", 91, 0, 0, string.Empty, 0, null, 0, 0, null);
        Check(none == "Fonts check (en): 91 of the language's distinct characters in the mod's texts, 0 missing from Overpass, 0 covered by fallbacks (none), 0 left: PASS.",
            "fonts line: a language the face draws whole passes with no fallback named: " + none);
        string left = UiFallbackRules.Line("ko", 1370, 1273, 1270, "Noto Sans KR 1270", 3, UiFallbackRules.Named(new[] { '가', '나', '다' }, 3), 12, 12, null);
        Check(left.Contains("1270 covered by fallbacks (Noto Sans KR 1270), 3 left: FAIL (the first without a font: 가 U+AC00, 나 U+B098, 다 U+B2E4: the game's font stands in for this language)")
              && !left.Contains("PASS"), "fonts line: characters left without a font fail, named with their code points, and say the game's font stands in: " + left);
        string blank = UiFallbackRules.Line("ko", 1370, 1273, 1273, "Noto Sans KR 1273", 0, null, 12, 0, null);
        Check(blank.Contains("0 left: FAIL (a line of 12 of them that may be cut drew 0 through the kit's own text)") && !blank.Contains("PASS"),
            "fonts line: a line the kit's own text did not draw whole fails though every character has a font: " + blank);
        var many = new char[30];
        for (int i = 0; i < many.Length; i++)
        {
            many[i] = (char)('가' + i);
        }
        string named = UiFallbackRules.Named(many, 30);
        Check(named.Split(new[] { ", " }, StringSplitOptions.None).Length == UiFallbackRules.MostNamed && UiFallbackRules.Named(many, 0) == null && UiFallbackRules.Named(null, 3) == null
              && pass.IndexOf('\n') < 0 && left.IndexOf('\n') < 0,
            "fonts line: at most twenty characters are named, and the check is one line of the log");
        Check(!UiFallbackRules.StandsIn(0) && UiFallbackRules.StandsIn(1), "fonts rules: the game's font writes a whole language only when a character of its texts has no font");
    }

    // The measure of a character the face lacks, and the scan of a locale file.
    private static void FontTextRuleChecks()
    {
        float alpha = UiFallbackRules.AdvanceEm('α');
        Check(UiFallbackRules.WesternAdvance('α') > 0 && Math.Abs(alpha - UiFallbackRules.WesternAdvance('α') / (float)UiFallbackRules.WesternUnits) < 0.00001f && alpha > 0.5f && alpha < 0.65f
              && FontFace.Advance('α') < 0,
            "fonts measure: a Greek letter, which the face lacks, is measured by the Western fallback's own advance (" + alpha.ToString("0.000") + " em)");
        Check(UiFallbackRules.AdvanceEm('가') == UiFallbackRules.HangulEm && UiFallbackRules.AdvanceEm('힣') == UiFallbackRules.HangulEm && UiFallbackRules.AdvanceEm('ㄱ') == UiFallbackRules.HangulEm
              && UiFallbackRules.AdvanceEm('中') == 1f && UiFallbackRules.AdvanceEm('あ') == 1f && UiFallbackRules.AdvanceEm('，') == 1f && UiFallbackRules.AdvanceEm('①') == 1f
              && UiFallbackRules.AdvanceEm('⸺') == 2f && UiFallbackRules.AdvanceEm('⸻') == 3f && UiFallbackRules.AdvanceEm((char)0xE000) == 1f,
            "fonts measure: a Hangul letter is the Korean font's advance, a Chinese character, a kana or a sign a square, the two long dashes their own, and a character no table knows a square");
        UiType small = UiTokens.Type(UiStyle.Small);
        float korean = UiTextRules.Width("새 게임", UiStyle.Small), spaceEm = FontFace.Advance(' ') / (float)FontFace.UnitsPerEm;
        Check(Math.Abs(korean - (3 * UiFallbackRules.HangulEm + spaceEm) * small.Size) < 0.001f,
            "fonts measure: three Hangul letters and a space are measured as the Korean font and the face draw them (" + korean.ToString("0.00") + " px)");
        float mixed = UiTextRules.Width("RLO 界面", UiStyle.Small);
        Check(Math.Abs(mixed - (UiTextRules.Width("RLO ", UiStyle.Small) + 2f * small.Size)) < 0.001f, "fonts measure: Latin letters by the face and Chinese characters as squares add up on one line");

        var seen = new bool[65536];
        UiFallbackRules.Mark("{\r\n  \"key_é\": \"a\\\"b\\\\c\\n\\u00e9 \\ud83d\\ude00 가\",\n  \"k2\" : \"界: {x}, \\/\"\n}", seen);
        Check(seen['a'] && seen['"'] && seen['b'] && seen['\\'] && seen['c'] && seen['é'] && seen[' '] && seen['가'] && seen['界'] && seen[':'] && seen['{'] && seen['x'] && seen['}'] && seen[',']
              && seen['/'] && !seen['k'] && !seen['e'] && !seen['y'] && !seen['_'] && !seen['2'] && !seen['\n'] && !seen['n'] && !seen['u'] && !seen[0xD83D] && !seen[0xDE00],
            "fonts scan: a locale file's texts are marked with their escapes read, and never a key, a line break or half of a character beyond the basic plane");
        char[] outside = UiFallbackRules.Outside(seen, out int marked);
        Check(marked == 15 && outside.Length == 2 && outside[0] == '界' && outside[1] == '가', "fonts scan: the characters the face lacks are told from the rest, in order (" + marked + " marked, "
                                                                                           + outside.Length + " outside)");
        UiFallbackRules.Mark(null, seen);
        UiFallbackRules.Mark("{\"a\": \"z\"}", new bool[10]);
        UiFallbackRules.Mark("{\"a\": \"q\\u00", seen);
        Check(!seen['z'] && seen['q'] && UiFallbackRules.Outside(null, out int nothing).Length == 0 && nothing == 0, "fonts scan: no text, a table too small and a file cut short are read without a fault");
    }

    // ------------------------------------------------------------------------------------------------- every character of every language

    private static void FontLocaleChecks(FontCensus census)
    {
        string[] whole = { "en", "pt", "de", "fr", "es", "ru", "nl", "tr", "pl" };   // the languages the face draws without help
        var outsideOf = new Dictionary<string, bool[]>(StringComparer.Ordinal);
        long scanTicks = 0;
        foreach (string code in ModLanguages.All)
        {
            string path = System.IO.Path.Combine(DesignLocales, code + ".json");
            if (!File.Exists(path))
            {
                Check(Array.IndexOf(ModLanguages.All, code) >= ModLanguages.Required, "fonts locales: " + code + ".json is there");
                continue;
            }
            string json = File.ReadAllText(path);
            var seen = new bool[65536];
            long began = Stopwatch.GetTimestamp();
            UiFallbackRules.Mark(json, seen);
            scanTicks = Math.Max(scanTicks, Stopwatch.GetTimestamp() - began);
            // The scan against the file read as JSON.
            var wanted = new bool[65536];
            foreach (KeyValuePair<string, string> text in LoadWords(path))
            {
                foreach (char c in text.Value)
                {
                    wanted[c] |= c >= ' ' && !char.IsSurrogate(c);
                }
            }
            int differ = 0;
            for (int c = 0; c < 65536; c++)
            {
                differ += seen[c] != wanted[c] ? 1 : 0;
            }
            Check(differ == 0, "fonts locales: " + code + ".json's characters by the kit's scan are those of the file read as JSON (" + differ + " differ)");
            char[] outside = UiFallbackRules.Outside(seen, out int marked);
            var set = new bool[65536];
            foreach (char c in outside)
            {
                set[c] = true;
            }
            outsideOf[code] = set;
            FontLanguage(census, "the mod's " + code, code, outside, marked);
            if (Array.IndexOf(whole, code) >= 0)
            {
                Check(outside.Length == 0, "fonts locales: the face draws every character of " + code + ".json (" + outside.Length + " outside it"
                                           + (outside.Length > 0 ? ": " + UiFallbackRules.Named(outside, outside.Length) : string.Empty) + ")");
            }
        }
        Check(scanTicks * 1000.0 / Stopwatch.Frequency < 400.0, "fonts locales: the largest locale file is scanned in under 400 ms (" + (scanTicks * 1000.0 / Stopwatch.Frequency).ToString("0.0") + " ms), on a worker thread in the game");
        Check(outsideOf.TryGetValue("ko", out bool[] ko) && Count(ko) > 1000 && outsideOf.TryGetValue("zh-cn", out bool[] zh) && Count(zh) > 2000,
            "fonts locales: Korean and Chinese are the two of the mod's languages written almost wholly by fallbacks");

        // The game's own words, in every language it offers: the kit draws them on its screens whatever the mod's language file holds.
        Check(census.Words.Count >= 20 && census.Words.ContainsKey("ja") && census.Words.ContainsKey("zh-tw") && census.Words.ContainsKey("el"),
            "fonts census: the game's own words are in the census for every language it offers (" + census.Words.Count + ")");
        foreach (KeyValuePair<string, bool[]> words in census.Words)
        {
            char[] outside = UiFallbackRules.Outside(words.Value, out int marked);
            FontLanguage(census, "the game's " + words.Key, words.Key, outside, marked);
            // One page of the language's own font holds its characters, the mod's and the game's together.
            var both = (bool[])words.Value.Clone();
            if (outsideOf.TryGetValue(words.Key, out bool[] mod))
            {
                for (int c = 0; c < 65536; c++)
                {
                    both[c] |= mod[c];
                }
            }
            List<GameFont> chain = census.Chain(words.Key);
            var drawn = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (char c in UiFallbackRules.Outside(both, out int _))
            {
                GameFont font = FontDrawing(chain, c);
                if (font != null)
                {
                    drawn[font.Family] = drawn.TryGetValue(font.Family, out int n) ? n + 1 : 1;
                }
            }
            foreach (KeyValuePair<string, int> by in drawn)
            {
                Check(by.Value <= UiFallbackRules.PageHolds(by.Key) * 0.85f, "fonts pages: " + words.Key + "'s characters drawn by " + by.Key + " fit one page of its copy's atlas with room to spare ("
                                                                             + by.Value + " of " + UiFallbackRules.PageHolds(by.Key) + ")");
            }
        }
    }

    private static int Count(bool[] set)
    {
        int n = 0;
        foreach (bool on in set)
        {
            n += on ? 1 : 0;
        }
        return n;
    }

    // One language's characters the face lacks, against the language's chain: each drawn by a font of the game's that has a
    // file of its own (so by a copy whose line is the face's), by one font where the script is one, and none measured narrower
    // than it is drawn.
    private static void FontLanguage(FontCensus census, string whose, string language, char[] outside, int marked)
    {
        List<GameFont> chain = census.Chain(language);
        int left = 0, plain = 0, narrow = 0;
        string firstLeft = null, firstNarrow = null;
        var by = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (char c in outside)
        {
            GameFont font = FontDrawing(chain, c);
            if (font == null)
            {
                left++;
                firstLeft ??= c + " U+" + ((int)c).ToString("X4");
                continue;
            }
            plain += font.Source == null ? 1 : 0;
            by[font.Family] = by.TryGetValue(font.Family, out int n) ? n + 1 : 1;
            float drawn = font.Advance[c] / (float)font.Upem;
            if (drawn > UiFallbackRules.AdvanceEm(c) + 0.0005f)
            {
                narrow++;
                firstNarrow ??= c + " U+" + ((int)c).ToString("X4") + " by " + font.Family + ": " + drawn.ToString("0.000") + " em drawn, " + UiFallbackRules.AdvanceEm(c).ToString("0.000") + " measured";
            }
        }
        var names = new List<string>();
        foreach (KeyValuePair<string, int> font in by)
        {
            names.Add(font.Key + " " + font.Value);
        }
        string said = whose + " words: " + marked + " characters, " + outside.Length + " outside the face, drawn by " + (names.Count > 0 ? string.Join(", ", names) : "none");
        Check(left == 0, "fonts locales: every character of " + said + " (" + left + " left" + (firstLeft != null ? ", the first " + firstLeft : string.Empty) + ")");
        Check(plain == 0, "fonts locales: each of them is drawn by a font with a file of its own, so by the mod's copy with the face's line, in " + whose + " words (" + plain + " by a font without one)");
        Check(narrow == 0, "fonts locales: none is drawn wider than the kit measures it in " + whose + " words (" + narrow + " wider" + (firstNarrow != null ? ", the first " + firstNarrow : string.Empty) + ")");
        if (language == "ko" || language == "zh-cn" || language == "zh-tw" || language == "ja")
        {
            string own = language == "ko" ? "Noto Sans KR" : language == "ja" ? "Noto Sans JP" : "Noto Sans SC";
            // A sign the Western font has too (a box rule among the game's traditional Chinese words) is drawn by it, as it is measured.
            bool onlyOwn = by.ContainsKey(own);
            foreach (KeyValuePair<string, int> font in by)
            {
                onlyOwn &= font.Key == own || font.Key == UiFallbackRules.WesternFamily;
            }
            Check(onlyOwn, "fonts locales: the language's own font draws its script, and no other region's font a character, in " + said);
            // The same characters as the game asks its fonts: the first that draws each has the tall line, the fault's cause.
            int tall = 0;
            foreach (char c in outside)
            {
                GameFont font = FontDrawing(census.Chain(language, false), c);
                tall += font != null && font.LineEm > FontFace.LineEm + 0.0001f ? 1 : 0;
            }
            Check(tall == outside.Length && tall > 500, "fonts locales: in the game's own order every one of them comes from a font whose line is taller than the face's (" + tall + " of "
                                                         + outside.Length + "), which is what dropped the lines of " + whose + " words");
        }
    }

    // ------------------------------------------------------------------------------------------------- the sources

    private static void FontSourceChecks()
    {
        string fonts = ModSource(@"Ui\UiFonts.cs"), chain = ModSource(@"Ui\UiFonts.Fallbacks.cs"), slots = ModSource(@"Ui\UiView.Slots.cs"), director = ModSource(@"Ui\UiDirector.cs"),
            look = ModSource(@"Ui\UiDirector.Fonts.cs"), game = ModSource(@"Ui\UiGame.cs"), probe = ModSource(@"Ui\UiFontProbe.cs"), rules = ModSource(@"Ui\UiTextRules.cs");
        Check(fonts.Length > 0 && chain.Length > 0 && slots.Length > 0 && director.Length > 0 && look.Length > 0 && game.Length > 0 && probe.Length > 0 && rules.Length > 0,
            "fonts sources: the fonts', the view's, the director's, the probe's and the text rules' sources are read");

        // The chain: the game's fonts where TextMeshPro asks them, a copy of each with the face's line, on every face.
        string gather = MemberText(chain, "private static void Gather()");
        Check(gather.Contains("GeneralFallbacks()") && gather.Contains("NoteWithOwn(DefaultFont(), ref listed);") && gather.Contains("Note(_fallback, listed);")
              && MemberText(chain, "private static List<TMP_FontAsset> GeneralFallbacks()").Contains("return TMP_Settings.fallbackFontAssets;"),
            "fonts sources: the fallbacks are the game's own, found where TextMeshPro asks them: its general fallbacks, its default font and the game's interface font");
        string copy = MemberText(chain, "private static void Copy(Link link)");
        Check(copy.Contains("TMP_FontAsset.CreateFontAsset(file, UiFallbackRules.CopyPoint, UiFallbackRules.CopyPadding, GlyphRenderMode.SDFAA,")
              && copy.Contains("UiFallbackRules.PageWidth(link.Family), UiFallbackRules.PageHeight(link.Family), AtlasPopulationMode.Dynamic, true);")
              && copy.Contains("line.ascentLine = UiFallbackRules.Cut(own.ascentLine, own.pointSize, line.pointSize);")
              && copy.Contains("line.descentLine = UiFallbackRules.Cut(own.descentLine, own.pointSize, line.pointSize);")
              && copy.Contains("line.lineHeight = UiFallbackRules.Cut(own.lineHeight, own.pointSize, line.pointSize);")
              && copy.Contains("line.capLine = UiFallbackRules.Cut(own.capLine, own.pointSize, line.pointSize);") && copy.Contains("copy.faceInfo = line;")
              && copy.Contains("copy.getFontFeatures = false;") && copy.Contains("link.Copy = copy;"),
            "fonts sources: a copy is a font asset of the mod's own over the game's font file, its ascender, descender, line and capitals cut to the face's");
        Check(!Regex.IsMatch(chain, @"\.Game\.(faceInfo|fallbackFontAssetTable|material|atlasPopulationMode)\s*=(?!=)") && !Regex.IsMatch(chain, @"\bGame\.(TryAddCharacters|ReadFontAssetDefinition|ClearFontAssetData)\s*\(")
              && !Regex.IsMatch(chain + fonts, @"TMP_Settings\.\w+\s*(=(?!=)|\.Add\(|\.Insert\(|\.Remove)"),
            "fonts sources: nothing of the game's own fonts or of TextMeshPro's settings is written: the game's screens are laid out by them");
        string order = MemberText(chain, "private static void Order()");
        Check(order.Contains("UiFallbackRules.Rank(link.Family, _language, link.Listed)") && order.Contains("Chain.Insert(at, link.Copy != null ? link.Copy : link.Game);")
              && order.Contains("TMP_FontAsset face = Read[i];") && order.Contains("face.fallbackFontAssetTable.AddRange(Chain);") && order.Contains("face.ReadFontAssetDefinition();"),
            "fonts sources: the chain stands on every face read, in the rules' order, and a face whose chain changed forgets the characters it found in the old one");
        string give = MemberText(chain, "private static void Give(int count)");
        Check(give.Contains("link.Copy != null ? Added(link.Copy, rest) : Drawn(link.Game, rest)") && MemberText(chain, "private static bool[] Added(TMP_FontAsset copy, uint[] characters)").Contains("copy.TryAddCharacters(characters, out uint[] _);")
              && !MemberText(chain, "private static bool[] Drawn(TMP_FontAsset font, uint[] characters)").Contains("TryAddCharacters"),
            "fonts sources: a language's characters are put into the copies' atlases ahead of play, and one of the game's own fonts is only asked whether it draws them");
        string step = MemberText(chain, "public static bool FallbacksStep(int most)");
        Check(step.Contains("if (!_done)") && step.Contains("Gather();") && step.Contains("Copy(Links[i]);") && step.Contains("Order();") && step.Contains("Give(Math.Min(most < 1 ? 1 : most, _wanted.Length - _at));")
              && step.Contains("_standIn = UiFallbackRules.StandsIn(_left);") && !Regex.IsMatch(chain, @"\bvoid\s+(Update|LateUpdate|FixedUpdate)\s*\("),
            "fonts sources: the work goes a step a call (a copy, the chain, a batch of characters), never in a frame method, and ends with whether the game's font must stand in");
        Check(MemberText(fonts, "private static void Take(UnityEngine.Object[] found)").Contains("Unchain();") && MemberText(fonts, "public static void SetFallback(TMP_FontAsset gameFont)").Contains("Unchain();")
              && !fonts.Contains("AddFallback") && fonts.Contains("public static partial class UiFonts"),
            "fonts sources: faces just read, and the game's interface font once found, have the chain built again; no face is given the game's font alone");
        string forStyle = MemberText(fonts, "public static TMP_FontAsset For(UiStyle style)");
        Check(forStyle.Contains("if (_standIn && _fallback != null)") && forStyle.IndexOf("return _fallback;", StringComparison.Ordinal) < forStyle.IndexOf("UiTokens.Type(style)", StringComparison.Ordinal),
            "fonts sources: with a character of the language's texts left without a font, every style is written with the game's font");

        // Every text the kit makes: a face of the fonts', and a box with room.
        string bind = MemberText(slots, "private void Bind(Slot slot, ref UiItem it, int index)");
        string place = MemberText(slots, "private void PlaceText(RectTransform r, ref UiItem it)");
        Check(bind.Contains("PlaceText(slot.Rect, ref it);\r\n                    BindText(((TextSlot)slot).Text, ref it);") || bind.Contains("PlaceText(slot.Rect, ref it);\n                    BindText(((TextSlot)slot).Text, ref it);"),
            "fonts sources: a text's rect is placed with its room before it is bound");
        Check(place.Contains("UiTextRules.Room(it.Style, it.Flags, out float above, out float below);") && place.Contains("Place(r, it.X, it.Y - above, it.W, it.H + above + below);"),
            "fonts sources: the room is the rules', over and under the box the layout gave");
        string before = slots.Replace("PlaceText(slot.Rect, ref it);", "Place(slot.Rect, it.X, it.Y, it.W, it.H);");
        Check(!MemberText(before, "private void Bind(Slot slot, ref UiItem it, int index)").Contains("PlaceText(slot.Rect, ref it);") && before != slots,
            "fonts sources: a text placed in its bare box, as it was, is found");
        Check(Occurs(slots, "overflowMode =") == 1 && MemberText(slots, "private void BindText(TextMeshProUGUI t, ref UiItem it)").Contains("t.overflowMode = (it.Flags & UiFlags.Truncate) != 0 ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;"),
            "fonts sources: only a text bound by the view may be cut by the text engine, and it is the one placed with room");
        var made = new List<string>();
        var fontless = new List<string>();
        string ui = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui"));
        foreach (string file in Directory.Exists(ui) ? Directory.GetFiles(ui, "*.cs", SearchOption.AllDirectories) : new string[0])
        {
            string code = File.ReadAllText(file), name = System.IO.Path.GetFileName(file);
            int makes = Regex.Matches(code, @"AddComponent<TextMeshPro(UGUI)?>\s*\(").Count;
            if (makes > 0)
            {
                made.Add(name + " " + makes);
            }
            foreach (Match set in Regex.Matches(code, @"\.font\s*=(?!=)\s*([^;]+);"))
            {
                string from = set.Groups[1].Value.Trim();
                if (!from.StartsWith("UiFonts.For(", StringComparison.Ordinal) && !(name == "UiView.Slots.cs" && from == "font"))
                {
                    fontless.Add(name + ": " + set.Value);
                }
            }
            if (name != "UiView.Slots.cs" && Regex.IsMatch(code, @"overflowMode\s*=(?!=)|TextOverflowModes\."))
            {
                fontless.Add(name + ": a text's overflow set outside the view");
            }
        }
        made.Sort(StringComparer.Ordinal);
        Check(string.Join(", ", made) == "UiHost.cs 1, UiView.Slots.cs 3",
            "fonts sources: the kit makes a text in two files only, the view's three kinds and the host's hidden typing field (" + string.Join(", ", made) + "): a new one is given a face and room here first");
        Check(fontless.Count == 0, "fonts sources: every text of the kit takes its font from the fonts, so every one has the chain (" + string.Join("; ", fontless) + ")");
        Check(MemberText(slots, "private void BindText(TextMeshProUGUI t, ref UiItem it)").Contains("TMP_FontAsset font = UiFonts.For(it.Style);")
              && slots.Contains("a.Letters.font = UiFonts.For(UiStyle.Title);") && slots.Contains("k.Label.font = UiFonts.For(UiStyle.KeyCap);")
              && slots.Contains("public TMP_Text TextOf(int item) => SlotOf(item) is TextSlot text ? text.Text : null;"),
            "fonts sources: the view's label, initials and key cap each take a face by its style, and a check can ask for an item's text");

        // The measure.
        string advance = MemberText(rules, "private static float AdvanceEm(char c, UiFace primary)");
        Check(advance.Contains("return a >= 0 ? a / (float)primary.UnitsPerEm : UiFallbackRules.AdvanceEm(c);") && !rules.Contains("AverageEm") && !rules.Contains("UiFace backup"),
            "fonts sources: a character the face lacks is measured by the fallbacks' rule, never as an average letter");

        // The director: behind the loading screen whole, a little a second in play, one line a language.
        string awake = MemberText(director, "private void Awake()"), slow = MemberText(director, "private void Slow()");
        Check(awake.Contains("LoadHold.Add(\"ui: the interface's fallback fonts\", FontsBehindScreen, done => { });")
              && awake.IndexOf("UiFonts.Step", StringComparison.Ordinal) < awake.IndexOf("FontsBehindScreen", StringComparison.Ordinal)
              && look.Contains("private static bool FontsBehindScreen() => FontsLook(int.MaxValue);"),
            "fonts sources: the fallbacks are made and the language's characters given their fonts behind the loading screen, after the faces are read, with no limit there");
        Check(slow.Contains("FontsLook(UiFallbackRules.TickBatch);") && slow.IndexOf("FontsLook(", StringComparison.Ordinal) < slow.IndexOf("bool city =", StringComparison.Ordinal)
              && UiFallbackRules.TickBatch <= 32 && UiFallbackRules.TickBatch >= 1,
            "fonts sources: what the loading screen did not hide goes on each second, a copy or at most " + UiFallbackRules.TickBatch + " characters at a time, in the main menu too");
        string fontsLook = MemberText(look, "private static bool FontsLook(int most)");
        Check(fontsLook.Contains("string language = UiGame.Language();") && fontsLook.Contains("UiFonts.Language(language);") && fontsLook.Contains("_fontsRead = ReadTexts(language);")
              && fontsLook.Contains("UiFonts.Characters(read.Language, read.Marked, read.Outside, read.MarkedGame, read.OutsideGame);") && fontsLook.Contains("UiFonts.FallbacksStep(most)")
              && fontsLook.Contains("if (done && UiFonts.FontsNews)"),
            "fonts sources: a language the game changes to orders the chain again, has its texts read and its check said once");
        string read = MemberText(look, "private static Task<LanguageText> ReadTexts(string language)"), texts = MemberText(look, "private static LanguageText Texts(string language, string modFile, string gameFile)");
        Check(read.Contains("File.Exists(own) ? own : Path.Combine(folder, English + \".json\")") && read.Contains("Application.streamingAssetsPath") && read.Contains("Task.Run(() => Texts(language, mod, game));")
              && texts.Contains("UiFallbackRules.Mark(File.ReadAllText(modFile), seen);") && texts.Contains("UiFallbackRules.Mark(File.ReadAllText(gameFile), game);") && !fontsLook.Contains("File.ReadAllText"),
            "fonts sources: the check's characters come from the locale file the game reads for the language (the mod's English where it has none) and the game's own, read on a worker thread");
        string say = MemberText(look, "private static void FontsSay()");
        Check(say.Contains("UiFonts.FontsSample(UiFontProbe.Most)") && say.Contains("UiFontProbe.Drawn(sample)") && say.Contains("UiFonts.FontsLine(given, drawn, GameBuild.Running)")
              && say.Contains("Meta.Dev.Always(Logger, line);") && say.Contains("Logger?.Warn(line);"),
            "fonts sources: the line is written whatever Developer diagnostics says, with what the kit's own text drew of a line of the language, and as a warning when it fails");
        Check(MemberText(game, "public static string Language()").Contains("return LanguageNow();") && game.Contains("private static string LanguageNow() => LocalizorManager.LoadedLocale;"),
            "fonts sources: the language is the game's own code for it, read in a method of its own");
        string drawnBy = MemberText(probe, "public static int Drawn(string line)");
        Check(drawnBy.Contains("UiDraw.Text(scene, UiAt.Screen, 0f, 0f, Width, Style, UiRole.Text, line, UiFlags.Truncate | UiFlags.Label);") && drawnBy.Contains("view.Write(scene, false);")
              && drawnBy.Contains("text.ForceMeshUpdate(true, true);") && drawnBy.Contains("Object.Destroy(root);") && UiTokens.Type(UiStyle.Small).Leading == 1f,
            "fonts sources: the probe's line is laid and bound as every label that may be cut is, in a style whose box is one line of the face, and its objects go before a frame shows them");

        // The offline preview draws with the mod's own chain: it no longer cuts a stand-in font's line itself.
        string runner = BuildSource(@"UiPreview\UiPreviewRunner.cs"), probeRunner = BuildSource(@"UiPreview\UiPreviewRunner.Fonts.cs");
        Check(runner.Length > 0 && !runner.Contains("face.ascentLine = own.faceInfo.ascentLine") && !runner.Contains("UiFonts.SetFallback(cjk)") && runner.Contains("UiFonts.FallbacksStep(int.MaxValue)"),
            "fonts sources: the preview hands its stand-in fonts to the kit as the game's fallbacks and lets the kit cut their line, so its pictures are what the game draws");
        Check(probeRunner.Contains("public static void FontsCheck()") && probeRunner.Contains("UiFontProbe.Drawn("), "fonts sources: the preview project has the probe that draws Korean and Chinese pages with the game's own fonts");
    }

    // ------------------------------------------------------------------------------------------------- the texts outside the interface

    // Every file under Scripts, outside the interface's view, that has the text engine cut a text: how many texts it cuts,
    // how many times it gives one its room, and why a text has none.
    private static readonly (string File, int Cuts, int Rooms, string Why)[] FontCutSites =
    {
        ("Calendar/CalendarGrid.cs", 1, 1, null),
        ("Character/GridLooks.cs", 1, 0, "sizes itself"),
        ("Core/GameUi/AppShell.cs", 2, 2, null),
        ("Core/GameUi/HelpFrame.cs", 2, 2, null),
        ("Core/GameUi/TravelList.cs", 1, 1, null),
        ("Core/GameUi/UiClone.cs", 1, 1, null),
        ("Corners/CornerSigns.cs", 1, 0, "sizes itself"),
        ("Districts/DistrictPanel.cs", 2, 2, null),
        ("Economy/Dealer/DealerReportPage.cs", 1, 1, null),
        ("Home/HomeNeedsBar.cs", 2, 2, null),
        ("Ledger/LedgerPage.Flow.cs", 2, 0, "made by LedgerPage.Text"),
        ("Ledger/LedgerPage.cs", 3, 1, "made by LedgerPage.Text"),
        ("Markets/MarketPage.cs", 2, 2, null),
        ("Meta/GuideView.cs", 1, 1, null),
        ("Meta/MetaUi.cs", 1, 1, null),
        ("Meta/OptionsLayout.Search.cs", 2, 2, null),
        ("Navigation/NavHud.Destination.cs", 1, 0, "sizes itself"),
        ("Navigation/NavHud.Make.cs", 2, 0, "sizes itself"),
        ("Navigation/NavHud.Strip.cs", 1, 0, "sizes itself"),
        ("Outlook/OutlookPage.cs", 1, 1, null),
        ("Planner/PlannerWeek.cs", 1, 1, null),
        ("Seasons/SeasonBar.cs", 1, 1, null),
        ("Social/Flare/FlareKit.cs", 1, 1, null),
        ("Transport/BusVehicle.cs", 1, 0, "sizes itself"),
    };

    private static void FontRoomChecks(FontCensus census)
    {
        // The tallest line a fallback can bring, and the interface font the mod's older boxes are sized by.
        float tallest = 0f, interfaceLine = 0f;
        foreach (GameFont font in census.Asked())
        {
            tallest = font.Source != null ? Math.Max(tallest, font.LineEm) : tallest;
        }
        foreach (KeyValuePair<string, GameFont> font in census.Fonts)
        {
            interfaceLine = font.Value.Family == "Rubik" ? Math.Max(interfaceLine, font.Value.LineEm) : interfaceLine;
        }
        Check(TextRoomRules.TallestLineEm >= tallest && TextRoomRules.TallestLineEm < tallest + 0.01f && tallest > 1.4f,
            "fonts room: the room's tallest line is a hair over the tallest of the game's fallback fonts (" + TextRoomRules.TallestLineEm + " over " + tallest.ToString("0.000") + " em)");
        Check(interfaceLine > 1.1f && interfaceLine < 1.2f && interfaceLine * 2f > TextRoomRules.TallestLineEm,
            "fonts room: two lines of the game's interface font (" + interfaceLine.ToString("0.000") + " em each) never fit the room one tall line needs, so a wrapped text gains no line by it");

        // The room, and the text engine's own sums with it: the box as the engine takes it holds the tall line, and the
        // words' anchor is where it was with no room, for every way a text is anchored by height but by its capitals.
        const float Size = 30f, Ascender = 28.05f, Descender = -7.5f;   // a line of the game's interface font at that size
        bool holds = true, still = true, untouched = true, none = true;
        for (float box = 20f; box <= 80f; box += 0.5f)
        {
            foreach (TextRoomSide side in new[] { TextRoomSide.Under, TextRoomSide.Both, TextRoomSide.Over })
            {
                TextRoomRules.Room(box, Size, side, out float over, out float under);
                float marginTop = -over, marginBottom = -under, engineBox = box - marginTop - marginBottom;
                holds &= engineBox >= TextRoomRules.TallestLineEm * Size - 0.001f && !UiFallbackRules.Ends(tallest * Size, engineBox);
                untouched &= box < TextRoomRules.TallestLineEm * Size || (over == 0f && under == 0f);
                // TextMeshPro's anchors (TextMeshProUGUI.GenerateTextMesh): top, middle and bottom of the box, each with its margins.
                float top = 0f - Ascender - marginTop, topBare = 0f - Ascender;
                float middle = -box / 2f - (Ascender + marginTop + Descender - marginBottom) / 2f, middleBare = -box / 2f - (Ascender + Descender) / 2f;
                float bottom = -box - Descender + marginBottom, bottomBare = -box - Descender;
                still &= side != TextRoomSide.Under || Math.Abs(top - topBare) < 0.0001f;
                still &= side != TextRoomSide.Both || Math.Abs(middle - middleBare) < 0.0001f;
                still &= side != TextRoomSide.Over || Math.Abs(bottom - bottomBare) < 0.0001f;
            }
            TextRoomRules.Room(box, Size, TextRoomSide.None, out float o, out float u);
            none &= o == 0f && u == 0f;
        }
        Check(holds, "fonts room: with its room every box, however short, holds a line of the game's tallest font as the text engine counts it");
        Check(still, "fonts room: the room moves no word: a text laid from the top has it under, one laid from the bottom over, a centred one half on each side");
        Check(untouched && none, "fonts room: a box that already holds the tall line gets no room, and a text centred by its capitals, which any room would move, gets none");
        TextRoomRules.Room(34f, 24f, TextRoomSide.Both, out float half, out float otherHalf);
        Check(Math.Abs(half - 0.4f) < 0.001f && half == otherHalf && UiFallbackRules.Ends(1.448f * 24f, 34f) && !UiFallbackRules.Ends(1.448f * 24f, 34f + half + otherHalf),
            "fonts room: Flare's 24 px line in its 34 px box, which dropped a Korean handle, has 0.4 px on each side and holds it");

        // The sources: every text the mod has the engine cut outside the interface's view is listed, and has its room.
        string scripts = ModScripts();
        var found = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string file in Directory.Exists(scripts) ? Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories) : new string[0])
        {
            string rel = System.IO.Path.GetRelativePath(scripts, file).Replace('\\', '/');
            if (rel == "Ui/UiView.Slots.cs")
            {
                continue;   // the interface's own view: its texts' boxes have the kit's room (UiTextRules.Room)
            }
            int cuts = Regex.Matches(File.ReadAllText(file), @"TextOverflowModes\.(Ellipsis|Truncate)").Count;
            if (cuts > 0)
            {
                found[rel] = cuts;
            }
        }
        var faults = new List<string>();
        foreach ((string File, int Cuts, int Rooms, string Why) site in FontCutSites)
        {
            string code = ModSource(site.File.Replace('/', '\\'));
            int rooms = Occurs(code, "TextRoom.On(");
            if (!found.TryGetValue(site.File, out int cuts) || cuts != site.Cuts || rooms != site.Rooms)
            {
                faults.Add(site.File + ": " + cuts + " cut and " + rooms + " given room, listed as " + site.Cuts + " and " + site.Rooms);
            }
            if (site.Why == "sizes itself" && !code.Contains("enableAutoSizing = true"))
            {
                faults.Add(site.File + ": listed as sizing itself, and does not");
            }
            if (site.Rooms < site.Cuts && site.Why == null)
            {
                faults.Add(site.File + ": a text with no room and no reason");
            }
            found.Remove(site.File);
        }
        foreach (KeyValuePair<string, int> more in found)
        {
            faults.Add(more.Key + ": " + more.Value + " text(s) cut, not listed: give each its room (GameUi.TextRoom.On) and list the file");
        }
        Check(faults.Count == 0 && FontCutSites.Length == 24, "fonts room: every text the mod has the text engine cut, outside the interface's view, is given its room or sizes itself ("
                                                             + (faults.Count == 0 ? FontCutSites.Length + " files" : string.Join("; ", faults)) + ")");
        string room = ModSource(@"Core\GameUi\TextRoom.cs"), ledger = ModSource(@"Ledger\LedgerPage.cs"), kit = ModSource(@"Social\Flare\FlareKit.cs");
        string fit = MemberText(room, "private void Fit()");
        Check(fit.Contains("TextRoomSide side = _text.enableAutoSizing ? TextRoomSide.None : Side(_text.verticalAlignment);")
              && fit.Contains("float box = _text.rectTransform.rect.height - _own.y - _own.w;") && fit.Contains("TextRoomRules.Room(box, _text.fontSize, side, out float over, out float under);")
              && fit.Contains("_given = new Vector4(_own.x, _own.y - over, _own.z, _own.w - under);") && fit.Contains("Guard.Fail(FrameGuard, e, this);"),
            "fonts room: the room is the rules', taken off the text's own margins on the side its alignment allows, behind the guard");
        int taken = fit.IndexOf("_own = now;", StringComparison.Ordinal), noted = fit.IndexOf("_given = new Vector4(", StringComparison.Ordinal), written = fit.IndexOf("_text.margin = _given;", StringComparison.Ordinal);
        Check(taken >= 0 && taken < noted && noted < written && Occurs(fit, "if (now != _given)") == 2 && fit.IndexOf("Vector4 now = _text.margin;", StringComparison.Ordinal) < taken,
            "fonts room: margins a text's maker set since are taken as its own, and the new margins are noted as given before they are written, which the text engine answers by a second call");
        Check(fit.Contains("_text.RegisterDirtyVerticesCallback(_fit);") && room.Contains("_text.UnregisterDirtyVerticesCallback(_fit);") && room.Contains("protected override void OnRectTransformDimensionsChange()")
              && room.Contains("protected override void OnEnable()") && !Regex.IsMatch(room, @"\bvoid\s+(Update|LateUpdate|FixedUpdate)\s*\("),
            "fonts room: it is fitted when its box changes, when it is switched on and when the text engine is told the text changed (a size or an alignment set after it was made), never a frame");
        Check(room.Contains("[SerializeField] private Vector4 _own;") && room.Contains("[SerializeField] private Vector4 _given;") && room.Contains("_text = GetComponent<TMP_Text>();")
              && MemberText(room, "private static TextRoomSide Side(VerticalAlignmentOptions vertical)").Contains("case VerticalAlignmentOptions.Capline: return TextRoomSide.None;"),
            "fonts room: a copy of a text keeps what tells its own margins from its room and finds its text itself, and a text centred by its capitals is left as it is");
        Check(MemberText(ledger, "private TMP_Text Text(RectTransform parent, string name, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)").Contains("TextRoom.On(t);")
              && MemberText(kit, "public static TextMeshProUGUI Text(Transform parent, string name, float size, Color color, FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)").Contains("TextRoom.On(t);"),
            "fonts room: the City Ledger's and Flare's own makers of a text give every text they make its room");
    }

    private static string BuildSource(string relative)
    {
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..", relative));
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static void FontWhatsNewChecks()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Check(en != null && WhatsNewHas(en, "Fixed: with the RLO interface on, most Korean and Chinese text was not drawn at all."), "fonts texts: What's new tells of the fix in some row");
    }
}
