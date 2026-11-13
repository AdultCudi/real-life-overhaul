using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// The notice of the game's build, and the room of the game's question box (Scripts/Core/BoxFitRules, BuildNoticeRules,
// GameBuild.Notice, GameConfirm, Guard). A player on Build 3670 opened the game to a grey panel running from above the top
// of the screen to below its foot, one unbroken list of every part of the mod that was off, with no heading and no button
// in sight and the main menu dead behind it. The box is as tall as its text, with no limit. So: first the box's own sums,
// then the notice as it was against them (it must spill, or these sums prove nothing), then the notice as it is, in every
// language, at every UI zoom and screen, with every part off and with one, then the sources and the texts.
internal static partial class Program
{
    private const string OldBuildSentence = "on a game build older than the one the mod is made for, the mod's notice about it listed every part that was switched off";
    private const int OldBuildMostChars = 900;    // characters: the longest the notice may be in any language, with everything off
    private const int OldBuildMostLines = 30;     // lines of the narrow box: the same bound in lines
    private const int OldBuildRunning = 3670;     // the player's build
    private const string OldBuildVersion = "10.10.10";

    private static readonly float[] OldBuildZooms = { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f };
    private static readonly int[][] OldBuildScreens = { new[] { 1280, 720 }, new[] { 1920, 1080 }, new[] { 3840, 2160 }, new[] { 1280, 960 }, new[] { 1680, 1050 } };

    private static void OldBuild()
    {
        OldBuildBox();
        OldBuildLists();
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Check(en != null, "old build: en.json is read");
        if (en == null)
        {
            return;
        }
        OldBuildAsItWas(en);
        foreach (string code in ModLanguages.All)
        {
            Dictionary<string, string> own = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            Check(own != null, "old build: " + code + ".json is read");
            if (own != null)
            {
                OldBuildLanguage(code, en, own);
            }
        }
        OldBuildSources();
        OldBuildTexts(en);
    }

    // The box's sums: the canvas, the room at each zoom, the lines a text takes, a cut.
    private static void OldBuildBox()
    {
        Check(BoxFitRules.Canvas(1f) == 2160f && BoxFitRules.Canvas(2f) == 1080f && BoxFitRules.Canvas(0.5f) == 4320f && BoxFitRules.Canvas(9f) == 1080f
              && BoxFitRules.Canvas(0.1f) == 4320f && BoxFitRules.Canvas(0f) == 1080f && BoxFitRules.Canvas(float.NaN) == 1080f,
            "old build box: the canvas is 2160 units high over the UI zoom, held to the game's slider, and a zoom that is no number counts as the largest");
        Check(BoxFitRules.High(ConfirmBox.Hud, 0) == 480f && BoxFitRules.High(ConfirmBox.Hud, 4) == 480f && Math.Abs(BoxFitRules.High(ConfirmBox.Hud, 10) - 708.4f) < 0.01f
              && Math.Abs(BoxFitRules.High(ConfirmBox.FullMenu, 0) - 472.68f) < 0.01f && Math.Abs(BoxFitRules.High(ConfirmBox.FullMenu, 10) - 901.08f) < 0.01f,
            "old build box: the grey box is its padding and its text, at least 200 of text; the white one its heading, text and buttons with their gaps");
        int hud2 = BoxFitRules.Room(ConfirmBox.Hud, 2f), hud1 = BoxFitRules.Room(ConfirmBox.Hud, 1f), menu2 = BoxFitRules.Room(ConfirmBox.FullMenu, 2f), menu1 = BoxFitRules.Room(ConfirmBox.FullMenu, 1f);
        Check(hud2 == 17 && hud1 == 42 && menu2 == 9 && menu1 == 35 && BoxFitRules.Room(ConfirmBox.Hud, 0.5f) > hud1,
            "old build box: room for 17 lines in the grey box and 9 in the white one at a zoom of 200%, 42 and 35 at 100% (" + hud2 + ", " + menu2 + ", " + hud1 + ", " + menu1 + ")");
        // A box with exactly its room of lines is inside every screen, with its buttons; one line more is still inside (the margin), the picture's fifty lines are not.
        bool inside = true, spills = true;
        foreach (int[] screen in OldBuildScreens)
        {
            foreach (float zoom in OldBuildZooms)
            {
                foreach (ConfirmBox box in new[] { ConfirmBox.Hud, ConfirmBox.FullMenu })
                {
                    inside &= OldBuildInside(box, BoxFitRules.Room(box, zoom), zoom, screen) && OldBuildInside(box, 0, zoom, screen);
                    spills &= !OldBuildInside(box, BoxFitRules.Room(box, zoom) + 3, zoom, screen);
                }
            }
        }
        Check(inside && spills, "old build box: with its room of lines a box and its buttons are inside every screen at every zoom, and with three lines more it is not");

        Check(BoxFitRules.Lines(null, 684f) == 0 && BoxFitRules.Lines(string.Empty, 684f) == 0 && BoxFitRules.Lines("Jobs", 684f) == 1 && BoxFitRules.Lines("a\nb", 684f) == 2
              && BoxFitRules.Lines("a\n\nb", 684f) == 3 && BoxFitRules.Lines("a\n", 684f) == 2,
            "old build box: no text takes no line, a word one, and each line break starts a line");
        // 684 units hold 19 font sizes, 33 letters of 0.56: a word of 34 breaks, and five words of five letters with their spaces leave room for three letters more.
        string letters33 = new string('a', 33), letters34 = new string('a', 34);
        Check(BoxFitRules.Lines(letters33, 684f) == 1 && BoxFitRules.Lines(letters34, 684f) == 2 && BoxFitRules.Lines(new string('a', 100), 684f) == 3
              && BoxFitRules.Lines(letters33 + " " + letters33, 684f) == 2 && BoxFitRules.Lines("aaaaa aaaaa aaaaa aaaaa aaaaa aaaaa", 684f) == 2
              && BoxFitRules.Lines("aaaaa aaaaa aaaaa aaaaa aaaaa aaa", 684f) == 1,
            "old build box: a line of the narrow box holds 33 letters, words are kept whole, and a word wider than the line is broken");
        string signs = new string((char)0x4E2D, 19);
        Check(BoxFitRules.Lines(signs, 684f) == 1 && BoxFitRules.Lines(signs + (char)0x6587, 684f) == 2 && BoxFitRules.Lines(new string((char)0xD55C, 40), 684f) == 3
              && BoxFitRules.Wide("ab") == 2f * BoxFitRules.LetterWide && BoxFitRules.Wide(signs) == 19f,
            "old build box: a Chinese or Korean sign is as wide as the font is high, 19 to a line, and a line breaks after any");
        Check(BoxFitRules.Lines("word", 1003.31f) == 1 && BoxFitRules.Lines(new string('a', 49), 1003.31f) == 1 && BoxFitRules.Lines(new string('a', 50), 1003.31f) == 2,
            "old build box: the white box's line holds 49 letters");

        // A cut: a text that fits is left alone; one that does not is cut at a word, ends with the tail, fits, and is never longer.
        string tail = "(The rest is in Player.log.)";
        string shortText = "Leave the business?";
        var longText = new System.Text.StringBuilder();
        for (int i = 0; i < 3000; i++)
        {
            longText.Append(i % 40 == 39 ? "\n" : " ").Append("word").Append(i);
        }
        string whole = longText.ToString().Trim();
        bool cutsFit = true, cutsShorter = true, cutsWords = true;
        foreach (float zoom in OldBuildZooms)
        {
            foreach (bool menuToo in new[] { false, true })
            {
                string cut = BoxFitRules.Cut(whole, zoom, menuToo, tail);
                cutsFit &= BoxFitRules.Fits(cut, zoom, menuToo) && !BoxFitRules.Fits(whole, zoom, menuToo);
                cutsShorter &= cut.Length < whole.Length && cut.EndsWith(" " + tail, StringComparison.Ordinal);
                string kept = cut.Substring(0, cut.Length - tail.Length - 1);
                cutsWords &= whole.StartsWith(kept, StringComparison.Ordinal) && (whole[kept.Length] == ' ' || whole[kept.Length] == '\n') && kept.Length > 100;
                cutsFit &= BoxFitRules.Cut(whole, zoom, menuToo, tail) == cut;
            }
        }
        Check(cutsFit && cutsShorter && cutsWords, "old build box: a text of 3,000 words is cut at a word to what the box has room for at every zoom, with the tail after it, the same each time");
        Check(ReferenceEquals(BoxFitRules.Cut(shortText, 2f, true, tail), shortText) && BoxFitRules.Cut(null, 2f, true, tail) == null && BoxFitRules.Cut(string.Empty, 1f, true, tail).Length == 0,
            "old build box: a text the box has room for is left as it is");
        string cjk = new string((char)0x4E2D, 5000);
        string cjkCut = BoxFitRules.Cut(cjk, 2f, true, tail);
        Check(BoxFitRules.Fits(cjkCut, 2f, true) && cjkCut.Length < 400 && cjkCut.Length > 100, "old build box: a text of signs with no space is cut too (" + cjkCut.Length + " characters)");
        string oneWord = BoxFitRules.Cut(new string('a', 20000), 2f, true, tail);
        Check(BoxFitRules.Fits(oneWord, 2f, true) && oneWord.Length > 200 && oneWord.Length < 600 && oneWord.StartsWith("aaaa", StringComparison.Ordinal) && oneWord.EndsWith(tail, StringComparison.Ordinal),
            "old build box: so is one word of 20,000 letters, in the middle of the word (" + oneWord.Length + " characters)");
    }

    private static bool OldBuildInside(ConfirmBox box, int lines, float zoom, int[] screen)
    {
        BoxFitRules.Place(box, lines, zoom, screen[0], screen[1], out float left, out float top, out float right, out float bottom, out float buttonsTop, out float buttonsBottom);
        return left >= 0f && top >= 0f && right <= screen[0] && bottom <= screen[1] && buttonsTop >= top && buttonsBottom <= bottom && buttonsBottom <= screen[1] && buttonsTop < buttonsBottom;
    }

    // A list for a sentence: few and short are all named; else three short ones and the count of the rest.
    private static void OldBuildLists()
    {
        const string more = "{names}|{more}|{count}";
        string none = BuildNoticeRules.Listed(new List<string>(), more, out int namedNone, out bool cutNone);
        string three = BuildNoticeRules.Listed(new List<string> { "Jobs", "Camera", "Health" }, more, out int namedThree, out bool cutThree);
        string four = BuildNoticeRules.Listed(new List<string> { "Jobs", "Camera", "Health", "Insurance" }, more, out int namedFour, out bool cutFour);
        Check(none.Length == 0 && namedNone == 0 && !cutNone && BuildNoticeRules.Listed(null, more, out _, out _).Length == 0 && three == "Jobs, Camera, Health" && namedThree == 3 && !cutThree
              && four == "Jobs, Camera, Health|1|4" && namedFour == 3 && cutFour && BuildNoticeRules.MostNamed == 3,
            "old build lists: up to three short names are all named, a fourth is counted (" + four + ")");
        string wide = new string('x', 60), wider = new string('x', 90);
        string skipped = BuildNoticeRules.Listed(new List<string> { wide, "Jobs", wider, "Camera" }, more, out int namedSkipped, out bool cutSkipped);
        string onlyWide = BuildNoticeRules.Listed(new List<string> { wider, wide, wider + "x" }, more, out int namedWide, out bool cutWide);
        string oneWide = BuildNoticeRules.Listed(new List<string> { wide }, more, out int namedOne, out bool cutOne);
        Check(skipped == "Jobs, Camera|2|4" && namedSkipped == 2 && cutSkipped && onlyWide == wide + "|2|3" && namedWide == 1 && cutWide && oneWide == wide && namedOne == 1 && !cutOne,
            "old build lists: a name wider than 32 letters is counted and not named; where every name is that wide the narrowest alone is named");
        var many = new List<string>();
        for (int i = 0; i < 500; i++)
        {
            many.Add("Part " + i);
        }
        string listed = BuildNoticeRules.Listed(many, more, out int namedMany, out bool cutMany);
        Check(listed == "Part 0, Part 1, Part 2|497|500" && namedMany == 3 && cutMany && BuildNoticeRules.Listed(many, more, out _, out _) == listed,
            "old build lists: five hundred parts are three names and a count, the same each time");
        Check(!BuildNoticeRules.AsGoodAsOff(0, 104) && !BuildNoticeRules.AsGoodAsOff(77, 104) && BuildNoticeRules.AsGoodAsOff(78, 104) && BuildNoticeRules.AsGoodAsOff(104, 104)
              && !BuildNoticeRules.AsGoodAsOff(0, 0) && !BuildNoticeRules.AsGoodAsOff(5, 0),
            "old build lists: from three quarters of the features off the mod is said to be switched off; never where the features cannot be counted");
    }

    private static BuildNoticeWords OldBuildWords(Dictionary<string, string> en, Dictionary<string, string> own)
    {
        string Word(string key) => own.TryGetValue(key, out string theirs) ? theirs : en.TryGetValue(key, out string ours) ? ours : key;   // as the game reads a key a language lacks
        return new BuildNoticeWords
        {
            OldBody = Word("reallife_build_old_body"), OldOff = Word("reallife_build_old_off"), OldShort = Word("reallife_build_old_short"), OldParts = Word("reallife_build_old_parts"),
            Update = Word("reallife_guard_notice_update"), Parts = Word("reallife_guard_notice_parts"), Short = Word("reallife_guard_notice_short"),
            More = Word("reallife_guard_list_more"), Log = Word("reallife_guard_list_log")
        };
    }

    // Every part's name and every feature's title in a language (the English where it lacks one), in en.json's order.
    private static void OldBuildNames(Dictionary<string, string> en, Dictionary<string, string> own, out List<string> features, out List<string> parts)
    {
        features = new List<string>();
        parts = new List<string>();
        foreach (KeyValuePair<string, string> pair in en)
        {
            bool part = pair.Key.StartsWith("reallife_contract_part_", StringComparison.Ordinal);
            if (!part && !pair.Key.StartsWith("reallife_meta_title_", StringComparison.Ordinal))
            {
                continue;
            }
            string name = own.TryGetValue(pair.Key, out string theirs) && theirs.Length > 0 ? theirs : pair.Value;
            List<string> into = part ? parts : features;
            if (!into.Contains(name))
            {
                into.Add(name);
            }
        }
    }

    // The notice as main wrote it: the old body, then every name in one sentence. It must not fit, or the sums would pass the fault.
    private static void OldBuildAsItWas(Dictionary<string, string> en)
    {
        OldBuildNames(en, en, out List<string> features, out List<string> parts);
        Check(features.Count >= 100 && parts.Count >= 130, "old build: en.json names " + features.Count + " features and " + parts.Count + " parts of the contract");
        var names = new List<string>(parts);
        names.AddRange(features);
        string was = en["reallife_build_old_body"].Replace("{version}", OldBuildVersion).Replace("{made}", GameBuild.MadeFor.ToString()).Replace("{build}", OldBuildRunning.ToString())
                     + "\n\n" + en["reallife_build_old_parts"].Replace("{parts}", string.Join("; ", names));
        int lines = BoxFitRules.Lines(was, BoxFitRules.HudTextWide);
        bool anyFits = false;
        foreach (float zoom in OldBuildZooms)
        {
            anyFits |= BoxFitRules.Fits(was, zoom, false);
        }
        BoxFitRules.Place(ConfirmBox.Hud, lines, 1f, 1920, 1080, out _, out float top, out _, out float bottom, out _, out float buttonsBottom);
        Check(!anyFits && lines > 150 && top < 0f && bottom > 1080f && buttonsBottom > 1080f && was.Length > 5000,
            "old build: the notice as it was, with every part named, is " + was.Length + " characters and " + lines + " lines: its box runs from " + (int)top + " to " + (int)bottom
            + " on a screen 1080 high, the buttons below its foot, and it fits at no zoom");
        // The picture: some fifty lines were in sight on a screen 1074 high, the panel from above the top to below the foot.
        Check(BoxFitRules.Room(ConfirmBox.Hud, 1f) < 50 && lines > 50, "old build: the fifty lines in the player's picture were more than the box's room, and this list is longer still");
    }

    private static void OldBuildLanguage(string code, Dictionary<string, string> en, Dictionary<string, string> own)
    {
        BuildNoticeWords words = OldBuildWords(en, own);
        OldBuildNames(en, own, out List<string> features, out List<string> parts);
        var everything = new List<string>(features);
        everything.AddRange(parts);

        // (the kind of game, the names counted, the features off whole)
        var cases = new List<(string What, BuildKind Kind, List<string> Names, int FeaturesOff)>
        {
            ("every feature and every part off on an older game", BuildKind.Older, everything, features.Count),
            ("every part of the contract off on an older game, no feature off whole", BuildKind.Older, parts, 0),
            ("half the features off on an older game", BuildKind.Older, features.GetRange(0, features.Count / 2), features.Count / 2),
            ("nothing off on an older game", BuildKind.Older, new List<string>(), 0),
            ("every part of the contract off on a newer game", BuildKind.Newer, parts, 0),
            ("every feature and every part off on a newer game", BuildKind.Newer, everything, features.Count),
            ("every part off on the build the mod is made for", BuildKind.Same, everything, features.Count),
            ("every part off on a build that could not be read", BuildKind.Unknown, everything, features.Count),
        };
        foreach (string one in everything)
        {
            cases.Add(("one off on an older game", BuildKind.Older, new List<string> { one }, 0));
            cases.Add(("one off on a newer game", BuildKind.Newer, new List<string> { one }, 0));
        }

        string fault = null;
        int longest = 0, mostLines = 0, mostNamed = 0, fullest = int.MaxValue, composed = 0;
        for (int c = 0; c < cases.Count && fault == null; c++)
        {
            (string what, BuildKind kind, List<string> names, int featuresOff) = cases[c];
            bool single = names.Count == 1;
            foreach (float zoom in OldBuildZooms)
            {
                if (single && zoom != 0.5f && zoom != 1f && zoom != 2f)
                {
                    continue;   // each single part at the ends and the middle of the slider
                }
                foreach (bool menuToo in new[] { false, true })
                {
                    int running = kind == BuildKind.Older ? OldBuildRunning : kind == BuildKind.Newer ? GameBuild.MadeFor + 20 : kind == BuildKind.Same ? GameBuild.MadeFor : 0;
                    BuildNotice notice = BuildNoticeRules.Compose(kind, running, OldBuildVersion, names, featuresOff, features.Count, words, zoom, menuToo);
                    composed++;
                    string where = what + (single ? " ('" + names[0] + "')" : string.Empty) + " at a zoom of " + zoom + (menuToo ? ", either box" : ", the grey box");
                    int hudLines = BoxFitRules.Lines(notice.Body, BoxFitRules.HudTextWide), menuLines = BoxFitRules.Lines(notice.Body, BoxFitRules.MenuTextWide);
                    longest = Math.Max(longest, notice.Body.Length);
                    mostLines = Math.Max(mostLines, hudLines);
                    mostNamed = Math.Max(mostNamed, notice.Named);
                    if (!single && kind == BuildKind.Older)
                    {
                        fullest = Math.Min(fullest, notice.Form);
                    }
                    if (string.IsNullOrWhiteSpace(notice.Body) || notice.Body.Contains('{') || notice.Body.Contains('}'))
                    {
                        fault = where + ": the notice is empty or has a placeholder left in it: " + notice.Body;
                    }
                    else if (!BoxFitRules.Fits(notice.Body, zoom, menuToo) || hudLines > BoxFitRules.Room(ConfirmBox.Hud, zoom) || (menuToo && menuLines > BoxFitRules.Room(ConfirmBox.FullMenu, zoom)))
                    {
                        fault = where + ": " + hudLines + " lines in the grey box (room " + BoxFitRules.Room(ConfirmBox.Hud, zoom) + "), " + menuLines + " in the white one (room "
                                + BoxFitRules.Room(ConfirmBox.FullMenu, zoom) + "): " + notice.Body;
                    }
                    else if (notice.Named > BuildNoticeRules.MostNamed || notice.Body.Length > OldBuildMostChars || hudLines > OldBuildMostLines)
                    {
                        fault = where + ": " + notice.Named + " parts named, " + notice.Body.Length + " characters, " + hudLines + " lines: " + notice.Body;
                    }
                    else if (OldBuildMarks(BuildNoticeRules.Compose(kind, running, OldBuildVersion, OldBuildMarked(names), featuresOff, features.Count, words, zoom, menuToo).Body) > BuildNoticeRules.MostNamed)
                    {
                        fault = where + ": more than " + BuildNoticeRules.MostNamed + " of the parts' names are in the notice: " + notice.Body;
                    }
                    else if (kind == BuildKind.Older && (!notice.Body.Contains(OldBuildRunning.ToString()) || !notice.Body.Contains(GameBuild.MadeFor.ToString()) || !notice.Body.Contains(OldBuildVersion)))
                    {
                        fault = where + ": an older game's notice names both builds and the mod's version: " + notice.Body;
                    }
                    else
                    {
                        foreach (int[] screen in OldBuildScreens)
                        {
                            if (!OldBuildInside(ConfirmBox.Hud, hudLines, zoom, screen) || (menuToo && !OldBuildInside(ConfirmBox.FullMenu, menuLines, zoom, screen)))
                            {
                                fault = where + ": the box or its buttons are off a screen of " + screen[0] + " by " + screen[1] + ": " + notice.Body;
                            }
                        }
                    }
                    if (fault != null)
                    {
                        break;
                    }
                }
                if (fault != null)
                {
                    break;
                }
            }
        }
        Check(fault == null, "old build (" + code + "): " + (fault ?? composed + " notices, each inside every screen with its buttons, at most " + BuildNoticeRules.MostNamed + " parts named"));
        Check(fault != null || (longest <= OldBuildMostChars && mostLines <= OldBuildMostLines && mostNamed <= BuildNoticeRules.MostNamed && fullest == 0),
            "old build (" + code + "): the longest notice is " + longest + " characters and " + mostLines + " lines of the narrow box (at most " + OldBuildMostChars + " and " + OldBuildMostLines
            + "), and where there is room the fullest form is used (" + fullest + ")");

        // With everything off on an older game the notice is the one text that says the mod is switched off, with no list; at 100% in the main menu, whole.
        BuildNotice off = BuildNoticeRules.Compose(BuildKind.Older, OldBuildRunning, OldBuildVersion, everything, features.Count, features.Count, words, 1f, false);
        string wantOff = words.OldOff.Replace("{version}", OldBuildVersion).Replace("{made}", GameBuild.MadeFor.ToString()).Replace("{build}", OldBuildRunning.ToString());
        Check(off.Form == 0 && off.Named == 0 && off.Body == wantOff
              && OldBuildMarks(BuildNoticeRules.Compose(BuildKind.Older, OldBuildRunning, OldBuildVersion, OldBuildMarked(everything), features.Count, features.Count, words, 1f, false).Body) == 0,
            "old build (" + code + "): with every feature off an older game's notice says the mod is switched off and how to update, and names no part");
        BuildNotice offSmall = BuildNoticeRules.Compose(BuildKind.Older, OldBuildRunning, OldBuildVersion, everything, features.Count, features.Count, words, 2f, true);
        Check(offSmall.Body == wantOff || offSmall.Body == words.OldShort.Replace("{version}", OldBuildVersion).Replace("{made}", GameBuild.MadeFor.ToString()).Replace("{build}", OldBuildRunning.ToString()),
            "old build (" + code + "): at a zoom of 200% in a city it is that text or its one line");
        // With a few off: the old body, the sentence with all of them, and nothing counted.
        List<string> few = features.OrderBy(BoxFitRules.Wide).Take(2).ToList();
        BuildNotice some = BuildNoticeRules.Compose(BuildKind.Older, OldBuildRunning, OldBuildVersion, few, 2, features.Count, words, 1f, false);
        Check(some.Form == 0 && some.Named == 2 && some.Body.Contains(few[0] + ", " + few[1]) && !some.Body.Contains(words.Log) && some.Body.Contains("\n\n"),
            "old build (" + code + "): with two features off both are named after how to update, and nothing is counted");
        BuildNotice counted = BuildNoticeRules.Compose(BuildKind.Newer, GameBuild.MadeFor + 20, OldBuildVersion, parts, 0, features.Count, words, 1f, true);
        Check(counted.Form == 0 && counted.Named >= 1 && counted.Named <= BuildNoticeRules.MostNamed && counted.Body.Contains(parts.Count.ToString()) && counted.Body.EndsWith(words.Log, StringComparison.Ordinal),
            "old build (" + code + "): with every part of the contract off on a newer game a few are named, all " + parts.Count + " counted, and the notice says where the whole list is");
    }

    // The names each with a mark before it that no text of the mod holds, so the names a notice writes can be counted in it.
    private static List<string> OldBuildMarked(List<string> names)
    {
        return names.Select(n => OldBuildMark + n).ToList();
    }

    private static int OldBuildMarks(string text)
    {
        return text.Count(c => c == OldBuildMark);
    }

    private const char OldBuildMark = (char)0x00A7;

    private static void OldBuildSources()
    {
        string notice = ModSource(@"Core\GameBuild.Notice.cs"), confirm = ModSource(@"Core\GameConfirm.cs"), guard = ModSource(@"Core\Guard.cs"), contract = ModSource(@"Core\GameContract.cs");
        string show = MemberText(notice, "private static bool Show(string where, bool inCity)"), menu = MemberText(notice, "private static IEnumerator InMenu()");

        // Short whatever is off, through the rule.
        Check(show.Contains("BuildNoticeRules.Compose(kind, Running, Meta.Changelog.Version, names, Guard.FeaturesOff, Guard.FeaturesAll, Words(), zoom,")
              && show.Contains("List<string> names = Guard.OffNames(older);") && show.Contains("float zoom = GameConfirm.Zoom;") && show.Contains("inCity || GameConfirm.MenuBoxToo")
              && !notice.Contains("string.Join(") && !notice.Contains("PartsNamed") && MemberText(notice, "public static void TestNotice()").Contains("BuildNoticeRules.Compose("),
            "old build sources: the notice, and its test, are put together by the rule from the names, the features off and the box's room; nothing is joined in the notice's own file");
        Check(!guard.Contains("PartsNamed") && guard.Contains("public static List<string> OffNames(bool doneWithout)") && guard.Contains("public static List<string> PartNames(GameMember[] members)")
              && !Regex.IsMatch(MemberText(guard, "public static string Status()"), @"string\.Join|Append\(")
              && CountOfText(MemberText(guard, "public static string Status()"), "BuildNoticeRules.Listed(") == 2,
            "old build sources: the guard hands the notice a list, and the options page's status names a few parts and features and counts the rest");

        // Once a start.
        Check(notice.Contains("private static bool _started, _shown;") && !notice.Contains("_menuShown") && !notice.Contains("_cityShown")
              && notice.Contains("public static bool CityDue => !_shown && (Of(Running) == BuildKind.Older || Guard.PartsOff);")
              && StatementsInOrder(MemberText(notice, "public static void ShowInCity()"), "if (_shown)", "return;", "_shown = true;", "Show(\"a save was loaded\", true);")
              && StatementsInOrder(menu, "while (!_shown)", "_shown = true;", "_up = Show(\"the main menu\", false);"),
            "old build sources: one mark for the main menu and the city, so the notice shows once a start");

        // Closed by Esc and Enter in the main menu, its button selected; the game's own keys in a city.
        Check(StatementsInOrder(menu, "_up = Show(\"the main menu\", false);", "yield return null;", "GameConfirm.SelectConfirm();", "while (_up && !GameConfirm.KnownClosed && !InCity())",
                  "if (!TypingNow.Now && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))", "bool closed = GameConfirm.Confirm();", "_up = false;")
              && show.Contains("Action closed = inCity ? null : Closed;") && show.Contains("closed, closed, OkKey, null, false, false))")
              && notice.Contains("private static readonly Action Closed = () => _up = false;"),
            "old build sources: in the main menu Esc and Enter press the notice's own confirm and its button is the selected one, until either of its buttons closed it");
        Check(MemberText(confirm, "private static bool Press()").Contains("Action press = HudConfirm.onConfirm;") && MemberText(confirm, "public static bool Confirm()").Contains("catch (Exception e) when (Lacks(e))")
              && MemberText(confirm, "private static void Select()").Contains("events.SetSelectedGameObject(button.gameObject);")
              && MemberText(confirm, "private static void Select()").Contains("sheet.gameObject.activeInHierarchy")
              && Regex.Matches(confirm, @"\[MethodImpl\(MethodImplOptions\.NoInlining\)\]\s+private static (bool Press|void Select|float ReadZoom|bool MenuOpen)\(\)").Count == 4,
            "old build sources: the box's confirm, its button, the UI zoom and the full menu's flag are each named in a small method of their own, never inlined");
        Check(contract.Contains("ConfirmPress = Optional(StaticField(QuestionBox, Game, \"HudConfirm\", \"onConfirm\", \"Action\"));")
              && contract.Contains("UiZoom = Optional(Prop(QuestionBox, Game, \"PlayerPrefSettings\", \"uiZooming\", \"Single\", true));")
              && contract.Contains("FullMenuOpen = Optional(Prop(QuestionBox, Game, \"UI.Smartphone.FullMenu\", \"IsOpen\", \"Boolean\", true));")
              && StatementsInOrder(MemberText(confirm, "public static void Known()"), "_noPress = GameContract.ConfirmPress.Missing;", "_noZoom = GameContract.UiZoom.Missing;", "_noMenu = GameContract.FullMenuOpen.Missing;"),
            "old build sources: the three are lines of the game contract, each done without where a game lacks it");
        string zoomText = MemberText(confirm, "public static float Zoom"), menuText = MemberText(confirm, "public static bool MenuBoxToo");
        Check(CountOfText(zoomText, "return BoxFitRules.ZoomMost;") == 2 && zoomText.Contains("return BoxFitRules.Zoom(ReadZoom());") && CountOfText(menuText, "return true;") == 2 && menuText.Contains("return MenuOpen();"),
            "old build sources: where the zoom or the full menu cannot be read, the least room is taken");

        // Every body of every question goes through the measure.
        string byData = MemberText(confirm, "public static bool TryShow(LanguageChangeEventDataHolder headerData"), byKey = MemberText(confirm, "public static bool TryShow(string headerKey");
        string cutOf = MemberText(confirm, "private static string CutOf(string body)");
        Check(byData.Contains("string cut = bodyData.Key != null ? CutOf(Text(bodyData)) : null;") && byData.Contains("bodyData = new LanguageChangeEventDataHolder { Key = TextKey, Arguments = new { text = cut } };")
              && byKey.Contains("string cut = string.IsNullOrEmpty(bodyKey) ? null : CutOf(Text(bodyKey));") && byKey.Contains("new LanguageChangeEventDataHolder { Key = TextKey, Arguments = new { text = cut } },")
              && StatementsInOrder(cutOf, "float zoom = Zoom;", "bool menuToo = MenuBoxToo;", "if (BoxFitRules.Fits(body, zoom, menuToo))", "return null;",
                  "string cut = BoxFitRules.Cut(body, zoom, menuToo, CutKey.GetLocalization());", "return cut;")
              && cutOf.Contains("The whole text: ") && CountOfText(confirm, "HudConfirm.Show(") == 2
              && MemberText(confirm, "public static void Show(string headerKey").Contains("TryShow(") && MemberText(confirm, "public static void Show(LanguageChangeEventDataHolder headerData").Contains("TryShow("),
            "old build sources: every question's body is measured before the game's box is asked for, and one too long is shown cut with the whole of it in Player.log");
        int direct = 0;
        foreach (string file in Directory.GetFiles(ModScripts(), "*.cs", SearchOption.AllDirectories))
        {
            if (!file.EndsWith("GameConfirm.cs", StringComparison.Ordinal))
            {
                direct += Regex.Matches(File.ReadAllText(file), @"^(?!\s*//).*\bHudConfirm\.Show\(", RegexOptions.Multiline).Count;   // a comment's mention is not a call
            }
        }
        Check(direct == 0, "old build sources: no other file of the mod asks the game's box itself (" + direct + ")");

        // The whole list in Player.log, once, at start.
        string tell = MemberText(guard, "private static void TellOff()");
        Check(StatementsInOrder(MemberText(guard, "public static void Start(IModLogger logger)"), "GameBuild.Start(logger);", "CheckCode();", "TellOff();")
              && tell.Contains("Game contract: the whole list for this game. Off until ") && tell.Contains("string.Join(\"; \", all)") && tell.Contains("string.Join(\"; \", without)")
              && tell.Contains("var all = new List<string>(Lacking);") && tell.Contains("foreach (GamePart part in ContractBook.OffParts())") && tell.Contains("all.Add(part.English);")
              && show.Contains("The whole list is in the line 'Game contract: the whole list for this game' above."),
            "old build sources: every feature and part that is off, and every part done without, is written to Player.log in one line at start, and the notice's own line points to it");

        // The options page's answers in the question box (no HUD): the options that need a reload are named up to four.
        string reload = MemberText(ModSource(@"Meta\Presets.cs"), "private static void AppendReload(StringBuilder text, List<string> reload)");
        Check(reload.Contains("for (int i = 0; i < reload.Count && i < ReloadNamed; i++)") && reload.Contains("\"reallife_meta_preset_reload_more\".GetLocalization()")
              && reload.Contains("string.Join(\"; \", reload)") && !reload.Contains("foreach (string label in reload)") && ModSource(@"Meta\Presets.cs").Contains("private const int ReloadNamed = 4;"),
            "old build sources: a preset's, a reset's or an import's answer names four of the options that need a reload and counts the rest, with all of them in Player.log");
    }

    private static void OldBuildTexts(Dictionary<string, string> en)
    {
        string[] added = { "reallife_build_old_off", "reallife_build_old_short", "reallife_guard_notice_short", "reallife_guard_list_more", "reallife_guard_list_log", "reallife_box_cut", "reallife_meta_preset_reload_more" };
        string[][] holes =
        {
            new[] { "{version}", "{made}", "{build}" }, new[] { "{version}", "{made}", "{build}" }, new[] { "{count}" }, new[] { "{names}", "{more}", "{count}" }, new string[0], new string[0], new[] { "{count}" }
        };
        bool allThere = added.All(k => en.TryGetValue(k, out string text) && text.Length > 0 && UiOffPlain(text));
        Check(allThere, "old build texts: the seven new texts are in en.json, with no dash or exclamation mark");
        if (!allThere)
        {
            return;
        }
        Check(Enumerable.Range(0, added.Length).All(i => OldBuildHoles(en[added[i]]).SetEquals(holes[i])), "old build texts: each has its placeholders and no other");
        string off = en["reallife_build_old_off"], body = en["reallife_build_old_body"];
        Check(off.Contains("needs Build {made} of Big Ambitions or newer") && off.Contains("This game is Build {build}") && off.Contains("the mod is switched off until the game is updated")
              && off.Contains("Properties") && off.Contains("Betas") && off.Contains("None") && off.Contains("Verify integrity of game files") && CountOfText(off, "\n\n") == 1
              && body.Contains("Properties") && body.Contains("Betas") && body.Contains("Verify integrity of game files") && OldBuildHoles(body).SetEquals(holes[0]),
            "old build texts: the notice for a game too old says which build the mod needs, which this is, that the mod is off until the game is updated, and each step in Steam");
        Check(en["reallife_build_old_short"].Length <= 200 && en["reallife_guard_notice_short"].Length <= 120 && en["reallife_guard_list_log"].Contains("Player.log") && en["reallife_guard_list_log"].Contains("options page")
              && en["reallife_box_cut"].Contains("Player.log") && en["reallife_box_cut"].Length <= 40 && en["reallife_guard_notice_short"].Contains("Player.log"),
            "old build texts: the one-line forms are short, and each cut or counted list says where the whole of it is");
        foreach (string key in new[] { "reallife_build_old_parts", "reallife_guard_notice_update", "reallife_guard_notice_parts", "reallife_guard_status_off" })
        {
            Check(en.TryGetValue(key, out string text) && OldBuildHoles(text).Contains("{parts}"), "old build texts: " + key + " still takes its list");
        }
        Check(en.TryGetValue("reallife_guide_trouble", out string guide) && guide.Contains("once each time the game starts, in the main menu") && guide.Contains("names a few of them and counts the rest")
              && guide.Contains("'What a game update switched off'") && en.TryGetValue("reallife_guard_button", out string button) && button == "What a game update switched off"
              && !guide.Contains("and when a save is loaded"),
            "old build texts: the Guide's page on trouble says the notice shows once a start, and where the whole list is");
        string row = en.Keys.FirstOrDefault(k => k.StartsWith("reallife_changes_", StringComparison.Ordinal) && !k.EndsWith("_title", StringComparison.Ordinal) && en[k].Contains(OldBuildSentence));
        Check(row != null && UiOffPlain(en[row]) && en[row].Contains("Fixed: " + OldBuildSentence), "old build texts: What's new has the line in English, as a fix");

        // The other languages come with the translation step: where one has a text, its placeholders are the English's.
        foreach (string file in Directory.GetFiles(DesignLocales, "*.json"))
        {
            Dictionary<string, string> words = LoadWords(file);
            for (int i = 0; words != null && i < added.Length; i++)
            {
                if (words.TryGetValue(added[i], out string theirs))
                {
                    Check(theirs.Length > 0 && OldBuildHoles(theirs).SetEquals(holes[i]) && !theirs.Contains('!') && !theirs.Contains('！'),
                        "old build texts: " + added[i] + " in " + System.IO.Path.GetFileName(file) + " has the English text's placeholders and no exclamation mark");
                }
            }
        }
    }

    private static HashSet<string> OldBuildHoles(string text)
    {
        return new HashSet<string>(Regex.Matches(text, @"\{[a-z_]+\}").Select(m => m.Value), StringComparer.Ordinal);
    }
}
