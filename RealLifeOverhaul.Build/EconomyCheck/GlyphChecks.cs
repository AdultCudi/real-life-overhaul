using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul;
using RealLifeOverhaul.Ui;

// A player's bug thread, "Mobile interface issues" (13 Nov 2026), its second report, checked here.
//
// The BizMan table showed every business name with only its Latin letters and brackets and its district, type and
// state cells blank, until the list was scrolled. A fallback font takes a character into its atlas when it is first asked for;
// a text built before its characters were in the atlas stayed blank. Now every text the kit sets goes through UiFonts.Need
// first, which puts the characters the face lacks into the fallbacks' atlases before TextMeshPro builds the text, and the
// texts that hold such characters are built again, once, a frame after a font took characters in behind the kit's back or the
// chain of fonts changed (Ui/UiFonts.Glyphs.cs, UiGlyphBook.cs). Widths never waited on this: a character the face lacks is
// measured by a table, not by the atlas.
internal static partial class Program
{
    private static void GlyphAtlases()
    {
        GlyphRuleChecks();
        GlyphLocaleChecks();
        GlyphSourceChecks();
    }

    // ------------------------------------------------------------------------------------------------- the glyphs

    private static void GlyphRuleChecks()
    {
        UiFace face = UiFontMetrics.Face(UiFamily.Overpass, 400);
        var book = new UiGlyphBook();
        var fresh = new List<uint>();
        Check(book.Beyond("Apple Store(中城店)", face, fresh) && fresh.Count == 3 && fresh[0] == '中' && fresh[1] == '城' && fresh[2] == '店',
            "glyphs: a name with three Chinese characters is beyond the face, and exactly those three are asked of the fonts");
        fresh.Clear();
        Check(!book.Beyond("Apple Store (Midtown) 100%", face, fresh) && fresh.Count == 0, "glyphs: a Latin text is no concern of the fonts' atlases");
        fresh.Clear();
        Check(!book.Beyond("Привет, Żółć İstanbul ğ ş ç ü ö ä ñ é", face, fresh) && fresh.Count == 0,
            "glyphs: Russian, Polish, Turkish and the other Latin languages' letters are the face's own");
        book.Beyond("中城店", face, fresh);
        book.Asked(fresh);
        fresh.Clear();
        Check(book.Beyond("中城店 中", face, fresh) && fresh.Count == 0 && book.Count == 3, "glyphs: a character asked of the fonts is asked once, and its text is still beyond the face");
        book.Forget();
        Check(book.Beyond("中", face, fresh) && fresh.Count == 1, "glyphs: a new chain of fonts asks every character again");
        fresh.Clear();
        var high = new UiGlyphBook();
        Check(high.Beyond("a\U0001F600b", face, fresh) && fresh.Count == 1 && fresh[0] == 0x1F600, "glyphs: a character beyond the basic plane (an emoji, a rare Chinese character) is asked by its whole code");
        high.Asked(fresh);
        fresh.Clear();
        Check(high.Beyond("\U0001F600", face, fresh) && fresh.Count == 0, "glyphs: ...and once");
        Check(!high.Beyond("a​b️­", face, fresh) && fresh.Count == 0, "glyphs: a zero width mark, a joiner or a variation selector is no glyph to ask for");
        bool lone = true;
        try
        {
            high.Beyond("\uD83D", face, fresh);
            high.Beyond("x\uDE00", face, fresh);
        }
        catch (Exception)
        {
            lone = false;
        }
        Check(lone, "glyphs: half a surrogate pair in a text is passed over");
        // Widths never waited on the atlas: the table measures a character the face lacks, whatever the fonts hold.
        float with = UiTextRules.Width("Apple Store(中城店)", UiStyle.Body), without = UiTextRules.Width("Apple Store()", UiStyle.Body);
        UiType body = UiTokens.Type(UiStyle.Body);
        Check(Math.Abs(with - without - 3f * (1f + body.Tracking) * body.Size) < 0.01f, "glyphs: the width of three Chinese characters is three ems from the table alone, so no measure is out of date when glyphs arrive");
    }

    private static void GlyphLocaleChecks()
    {
        UiFace face = UiFontMetrics.Face(UiFamily.Overpass, 400);
        string[] whole = { "en", "pt", "de", "fr", "es", "ru", "nl", "tr", "pl" };
        foreach (string code in ModLanguages.All)
        {
            string path = System.IO.Path.Combine(DesignLocales, code + ".json");
            if (!File.Exists(path))
            {
                continue;
            }
            var book = new UiGlyphBook();
            var fresh = new List<uint>();
            int texts = 0, beyond = 0;
            var lacking = new bool[65536];
            foreach (KeyValuePair<string, string> text in LoadWords(path))
            {
                texts++;
                fresh.Clear();
                if (book.Beyond(text.Value, face, fresh))
                {
                    beyond++;
                }
                foreach (uint c in fresh)
                {
                    lacking[c < 65536 ? c : 0] = true;
                }
            }
            int distinct = 0;
            for (int c = 0; c < 65536; c++)
            {
                distinct += lacking[c] ? 1 : 0;
            }
            Console.WriteLine("  glyphs " + code + ": " + beyond + " of " + texts + " texts hold a character beyond the face, " + distinct + " distinct characters");
            bool isWhole = Array.IndexOf(whole, code) >= 0;
            Check(isWhole ? beyond == 0 : beyond > texts / 20 && distinct > 100,
                "glyphs: " + code + ": " + beyond + " of " + texts + " texts hold a character beyond the face (" + distinct + " distinct), as the language is" + (isWhole ? " drawn by the face alone" : " drawn by fallback fonts"));
        }
    }

    private static void GlyphSourceChecks()
    {
        string view = ModSource(@"Ui\UiView.Slots.cs"), glyphs = ModSource(@"Ui\UiFonts.Glyphs.cs"), fallbacks = ModSource(@"Ui\UiFonts.Fallbacks.cs");
        string set = MemberText(view, "internal static void SetText(TMP_Text t, string text)");
        Check(set.Contains("UiFonts.Need(t, text);") && set.IndexOf("UiFonts.Need(t, text);", StringComparison.Ordinal) < set.IndexOf("t.text = text;", StringComparison.Ordinal),
            "glyphs: every text the kit sets has its characters put into the fallbacks' atlases before TextMeshPro builds it");
        Check(glyphs.Contains("TMPro_EventManager.TEXT_CHANGED_EVENT.Add(_onBuilt);") && glyphs.Contains("Guard.Wrap<UnityEngine.Object>(TextBuilt)") && glyphs.Contains("CountOf(Links[i].Copy ?? Links[i].Game)"),
            "glyphs: TextMeshPro says nothing when an atlas gains glyphs, so a text's own build is the moment the counts are compared, through the guard");
        string redraw = MemberText(glyphs, "private static IEnumerator Redraw()");
        Check(redraw.Contains("yield return null;") && redraw.Contains("HoldsNews(t.text)") && redraw.Contains("t.ForceMeshUpdate(false, true);") && redraw.Contains("t.isActiveAndEnabled") && glyphs.Contains("director.StartCoroutine(Guard.Routine(Redraw()));"),
            "glyphs: the texts that hold such characters are built again a frame later, once for all of them, outside the canvas's rebuild");
        Check(!glyphs.Contains("void Update(") && !glyphs.Contains("LateUpdate") && !glyphs.Contains("willRenderCanvases") && !glyphs.Contains("WaitForSeconds"),
            "glyphs: nothing runs a frame or on a timer: a text set, a text built, a change of the chain");
        Check(fallbacks.Contains("Hook();") && fallbacks.Contains("Chained();") && fallbacks.Contains("own.Seen = CountOf(copy);") && fallbacks.Contains("Book.Forget();") && fallbacks.Contains("link.Seen = CountOf(copy);"),
            "glyphs: the fonts' own additions keep the counts, a new chain or language asks every character again, and the hook stands with the first fallbacks");
        Check(MemberText(glyphs, "internal static void Need(TMP_Text t, string text)").Contains("!_standIn && Supply(Fresh)") && glyphs.Contains("Writing.RemoveWhere(w => w == null)"),
            "glyphs: where the game's font writes the whole language nothing is added to the copies, and the texts of an earlier city are let go");
        string doc = ModSource(@"..\..\..\..\docs\features\rlo-interface.md");
        Check(doc.Contains("UiFonts.Glyphs"), "glyphs: the interface's page of the docs says how a text's glyphs arrive");
    }
}
