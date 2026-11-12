using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The fonts that draw what the faces lack: Hangul, Chinese, kana, Greek, a sign (<see cref="UiFallbackRules"/>). The
    /// game's own fallback fonts are found where TextMeshPro itself asks them (its settings' general fallbacks, its
    /// default font and that font's own, then the game's interface font), and each that has a font file is copied once a
    /// session into a font asset of the mod's own whose line is cut to the faces': TextMeshPro lays a line out by the
    /// font of each character, and the game's fonts stand taller than the faces' line, which the kit's boxes are made
    /// for. The copies stand on every face as its fallbacks, ordered for the language (the Western font, then the
    /// language's own script's, then the rest), and the game's fonts are never changed. A font without a file (a static
    /// atlas, one of the system's by name) is written with as it is. The language's characters are put into the copies'
    /// atlases behind the loading screen, so no text meets one for the first time in play, and one line of Player.log
    /// says what was found. Cost: nothing a frame; a step of the work a call while any is left, then two comparisons.
    /// </summary>
    public static partial class UiFonts
    {
        private const string CopyPrefix = "RLO ";   // before the game's name of a font, on the mod's own copy of it

        // One of the game's fonts a character can fall back to, and the mod's own copy of it.
        private sealed class Link
        {
            public TMP_FontAsset Game;   // the game's font asset: never changed
            public TMP_FontAsset Copy;   // the mod's copy with the faces' line, or null: the game's is then written with as it is
            public string Family;        // its family's name: it orders the font and sizes its atlas
            public int Listed;           // its place among the game's fallbacks, as TextMeshPro asks them
            public bool Tried;           // a copy was asked for: never twice
            public bool Named;           // the game still names it: one it no longer names is let go
            public int Drew;             // characters of the mod's texts in this language that it draws, for the check's line
        }

        private static readonly List<Link> Links = new List<Link>(8);
        private static readonly List<TMP_FontAsset> Chain = new List<TMP_FontAsset>(8);   // what the faces fall back to, in order
        private static string _language;      // the language the chain is ordered for (the game's code), or null before it is known
        private static bool _gathered;        // the game's fallbacks are known as they stand now
        private static bool _ordered;         // the chain stands on the faces in the language's order
        private static char[] _wanted;        // the characters of the language's texts the faces lack: the mod's, then the game's own
        private static int _wantedMod;        // how many of them are the mod's texts'
        private static int _marked;           // the distinct characters of the mod's texts in the language
        private static int _markedGame;       // those of the game's own words
        private static int _at;               // the next of the wanted to give a font
        private static int _left, _leftGame;  // characters no font draws: the mod's texts', the game's own words'
        private static readonly char[] LeftFirst = new char[UiFallbackRules.MostNamed];
        private static bool _checked;         // every wanted character was given its font, or found to have none
        private static bool _standIn;         // the game's font writes the whole language: characters of the mod's texts have no font
        private static int _fontsOutcome, _fontsSaid;   // what the check found, as one number, and what its line last said

        /// <summary>True when the game's font writes every style in this language (<see cref="UiFallbackRules.StandsIn"/>).</summary>
        public static bool StandsIn => _standIn;

        /// <summary>
        /// The language the fallbacks are ordered for, by the game's code for it. Another language orders them again at the
        /// next step and has its characters asked for (<see cref="Characters"/>).
        /// </summary>
        public static void Language(string code)
        {
            if (string.Equals(code, _language, StringComparison.Ordinal))
            {
                return;
            }
            _language = code;
            _ordered = false;
            _wanted = null;
            _checked = false;
            _standIn = false;
        }

        /// <summary>
        /// The characters of a language's texts the faces lack (<see cref="UiFallbackRules.Outside"/>): the mod's, with how
        /// many distinct characters its texts hold in all, and the game's own words' beyond them. The steps give each its
        /// font. Texts of another language than the one the fallbacks are ordered for are let go.
        /// </summary>
        public static void Characters(string language, int marked, char[] outside, int markedGame, char[] outsideGame)
        {
            if (!string.Equals(language, _language, StringComparison.Ordinal))
            {
                return;
            }
            int mod = outside != null ? outside.Length : 0, game = outsideGame != null ? outsideGame.Length : 0;
            var wanted = new char[mod + game];
            if (mod > 0)
            {
                Array.Copy(outside, wanted, mod);
            }
            if (game > 0)
            {
                Array.Copy(outsideGame, 0, wanted, mod, game);
            }
            _wanted = wanted;
            _wantedMod = mod;
            _marked = marked;
            _markedGame = markedGame;
            _at = 0;
            _left = _leftGame = 0;
            _checked = false;
            for (int i = 0; i < Links.Count; i++)
            {
                Links[i].Drew = 0;
            }
        }

        /// <summary>
        /// Moves the fallbacks on by one piece: the game's fonts found, a copy made, the chain put on the faces, or up to
        /// <paramref name="most"/> characters of the language's texts given their fonts. True when nothing is left to do.
        /// Behind the loading screen it is called until true with no limit; in play once a second with a few characters.
        /// </summary>
        public static bool FallbacksStep(int most)
        {
            if (!_done)
            {
                return false;
            }
            bool whole = most == int.MaxValue;
            if (!_gathered)
            {
                Gather();
            }
            for (int i = 0; i < Links.Count; i++)
            {
                if (!Links[i].Tried)
                {
                    Copy(Links[i]);
                    _ordered = false;
                    if (!whole)
                    {
                        Order();        // the game's own font stood in the chain until now: its copy takes its place
                        return false;   // a copy a call: each opens a font file
                    }
                }
            }
            if (!_ordered)
            {
                Order();
            }
            if (_wanted == null || _checked)
            {
                return true;
            }
            if (_at < _wanted.Length)
            {
                Give(Math.Min(most < 1 ? 1 : most, _wanted.Length - _at));
            }
            if (_at < _wanted.Length)
            {
                return false;
            }
            _checked = true;
            _standIn = UiFallbackRules.StandsIn(_left);
            _fontsOutcome++;
            return true;
        }

        /// <summary>True when the fonts' check has a line it has not said: once a language's characters all have their fonts.</summary>
        public static bool FontsNews => _checked && _fontsOutcome != _fontsSaid;

        /// <summary>True when every character of the mod's texts in the language has a font.</summary>
        public static bool FontsPass => _left == 0;

        /// <summary>
        /// The first letters and signs of the language's texts that the faces lack, at most <paramref name="most"/>, for the
        /// line drawn through the kit's own text. A space or a mark that joins a letter is left out: it is no glyph of its
        /// own to count. Empty where the faces draw the whole language.
        /// </summary>
        public static string FontsSample(int most)
        {
            if (_wanted == null || _wanted.Length == 0 || most < 1)
            {
                return string.Empty;
            }
            var line = new StringBuilder(most);
            for (int i = 0; i < _wanted.Length && line.Length < most; i++)
            {
                char c = _wanted[i];
                if (char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsSymbol(c))
                {
                    line.Append(c);
                }
            }
            return line.ToString();
        }

        /// <summary>
        /// The fonts' check as its one line for Player.log (<see cref="UiFallbackRules.Line"/>), with what the line drawn
        /// through the kit's own text came to (<paramref name="given"/> characters, <paramref name="drawn"/> of them
        /// drawn; none given: nothing to draw). Saying it ends <see cref="FontsNews"/>.
        /// </summary>
        public static string FontsLine(int given, int drawn, int gameBuild)
        {
            _fontsSaid = _fontsOutcome;
            var names = new StringBuilder(96);
            var order = new StringBuilder(160);
            int covered = 0, copies = 0, plain = 0;
            for (int i = 0; i < Chain.Count; i++)
            {
                Link link = LinkOf(Chain[i]);
                if (link == null)
                {
                    continue;
                }
                copies += link.Copy != null ? 1 : 0;
                plain += link.Copy == null ? 1 : 0;
                order.Append(order.Length > 0 ? ", " : string.Empty).Append(link.Family);
                order.Append(link.Copy != null ? string.Empty : " (the game's own, its line as it is)");
                if (link.Drew > 0)
                {
                    covered += link.Drew;
                    names.Append(names.Length > 0 ? ", " : string.Empty).Append(link.Family).Append(' ').Append(link.Drew);
                }
            }
            int wantedGame = _wanted != null ? _wanted.Length - _wantedMod : 0;
            string more = "The game's own words: " + _markedGame + " characters, " + wantedGame + " more missing from Overpass, " + _leftGame
                          + " left. Fallbacks in order: " + (order.Length > 0 ? order.ToString() : "none found") + " (" + copies
                          + " of the mod's own copies with the faces' line, " + plain + " of the game's as they are)"
                          + (_standIn ? ". The game's font stands in for the whole language" : string.Empty) + ". Game: Build " + gameBuild + ".";
            return UiFallbackRules.Line(_language, _marked, _wantedMod, covered, names.ToString(), _left, UiFallbackRules.Named(LeftFirst, _left),
                given, drawn, more);
        }

        // The faces lose what they fell back to: the game's fonts are looked for again at the next step.
        private static void Unchain()
        {
            _gathered = false;
            _ordered = false;
        }

        // The game's fonts a character can fall back to, as TextMeshPro asks them: its settings' general fallbacks and their
        // own, its default font and its own, then the game's interface font. A font the game no longer names is let go.
        private static void Gather()
        {
            _gathered = true;
            for (int i = 0; i < Links.Count; i++)
            {
                Links[i].Named = false;
            }
            int listed = 0;
            List<TMP_FontAsset> general = GeneralFallbacks();
            for (int i = 0; general != null && i < general.Count; i++)
            {
                NoteWithOwn(general[i], ref listed);
            }
            NoteWithOwn(DefaultFont(), ref listed);
            Note(_fallback, listed);
            for (int i = Links.Count - 1; i >= 0; i--)
            {
                if (!Links[i].Named)
                {
                    if (Links[i].Copy != null)
                    {
                        UnityEngine.Object.Destroy(Links[i].Copy);   // its atlas and its material go with it (TMP_FontAsset.OnDestroy)
                    }
                    Links.RemoveAt(i);
                    _ordered = false;
                }
            }
        }

        private static void NoteWithOwn(TMP_FontAsset font, ref int listed)
        {
            Note(font, listed++);
            List<TMP_FontAsset> own = font != null ? font.fallbackFontAssetTable : null;
            for (int i = 0; own != null && i < own.Count; i++)
            {
                Note(own[i], listed++);
            }
        }

        // One of the game's fonts taken into the links, once: never a face or a copy of the mod's own, and never a second
        // asset over the same font file, which one copy stands for.
        private static void Note(TMP_FontAsset font, int listed)
        {
            if (font == null || Array.IndexOf(Read, font) >= 0)
            {
                return;
            }
            Font file = FileOf(font);
            for (int i = 0; i < Links.Count; i++)
            {
                Link had = Links[i];
                if (ReferenceEquals(had.Game, font) || ReferenceEquals(had.Copy, font) || (file != null && ReferenceEquals(FileOf(had.Game), file)))
                {
                    had.Named |= !ReferenceEquals(had.Copy, font);
                    return;
                }
            }
            string family = font.faceInfo.familyName;
            Links.Add(new Link { Game = font, Family = string.IsNullOrEmpty(family) ? font.name : family, Listed = listed, Named = true });
            _ordered = false;
            if (_checked && _wanted != null && (_left > 0 || _leftGame > 0))
            {
                Characters(_language, _marked, Part(0, _wantedMod), _markedGame, Part(_wantedMod, _wanted.Length - _wantedMod));   // it may draw what was left
            }
        }

        private static char[] Part(int from, int count)
        {
            var part = new char[count];
            Array.Copy(_wanted, from, part, 0, count);
            return part;
        }

        // The font file a dynamic font asset draws from, or null: a static atlas, or a font of the system's found by its name.
        private static Font FileOf(TMP_FontAsset font)
        {
            return font != null && font.atlasPopulationMode == AtlasPopulationMode.Dynamic ? font.sourceFontFile : null;
        }

        // The mod's own copy of one of the game's fonts: a dynamic font asset over the same font file, with the line of the
        // faces, so a line that holds one of its characters is as tall as any other and sits on the same baseline. The
        // game's own asset is left as it is: the game's screens are laid out by its line.
        private static void Copy(Link link)
        {
            link.Tried = true;
            Font file = FileOf(link.Game);
            TMP_FontAsset face = FirstFace();
            if (file == null || face == null)
            {
                return;
            }
            try
            {
                TMP_FontAsset copy = TMP_FontAsset.CreateFontAsset(file, UiFallbackRules.CopyPoint, UiFallbackRules.CopyPadding, GlyphRenderMode.SDFAA,
                    UiFallbackRules.PageWidth(link.Family), UiFallbackRules.PageHeight(link.Family), AtlasPopulationMode.Dynamic, true);
                if (copy == null)
                {
                    Said?.Invoke("UI: the game's font '" + link.Game.name + "' gave no copy for the interface (its file did not open); it is written with as it is");
                    return;
                }
                copy.name = CopyPrefix + link.Game.name;
                copy.getFontFeatures = false;   // its letters stand by their own advances, as the kit measured them: no pair moves one
                if (copy.material != null)
                {
                    copy.material.name = copy.name + " Material";
                }
                FaceInfo own = face.faceInfo, line = copy.faceInfo;
                line.ascentLine = UiFallbackRules.Cut(own.ascentLine, own.pointSize, line.pointSize);
                line.descentLine = UiFallbackRules.Cut(own.descentLine, own.pointSize, line.pointSize);
                line.lineHeight = UiFallbackRules.Cut(own.lineHeight, own.pointSize, line.pointSize);
                line.capLine = UiFallbackRules.Cut(own.capLine, own.pointSize, line.pointSize);
                copy.faceInfo = line;
                link.Copy = copy;
            }
            catch (Exception e)
            {
                Said?.Invoke("UI: the game's font '" + link.Game.name + "' gave no copy for the interface (" + e.GetType().Name + ": " + e.Message
                             + "); it is written with as it is");
            }
        }

        // The first face read, whose line every copy is cut to: the three weights share one line.
        private static TMP_FontAsset FirstFace()
        {
            for (int i = 0; i < Read.Length; i++)
            {
                if (Read[i] != null)
                {
                    return Read[i];
                }
            }
            return null;
        }

        // The chain in the language's order, put on every face. A face keeps the characters it once found in a fallback, so
        // one whose chain changed reads its own table again and forgets them.
        private static void Order()
        {
            _ordered = true;
            Chain.Clear();
            for (int i = 0; i < Links.Count; i++)
            {
                Link link = Links[i];
                int rank = UiFallbackRules.Rank(link.Family, _language, link.Listed), at = Chain.Count;
                while (at > 0 && UiFallbackRules.Rank(LinkOf(Chain[at - 1]).Family, _language, LinkOf(Chain[at - 1]).Listed) > rank)
                {
                    at--;
                }
                Chain.Insert(at, link.Copy != null ? link.Copy : link.Game);
            }
            for (int i = 0; i < Read.Length; i++)
            {
                TMP_FontAsset face = Read[i];
                if (face == null || Same(face.fallbackFontAssetTable, Chain))
                {
                    continue;
                }
                if (face.fallbackFontAssetTable == null)
                {
                    face.fallbackFontAssetTable = new List<TMP_FontAsset>(Chain.Count);
                }
                face.fallbackFontAssetTable.Clear();
                face.fallbackFontAssetTable.AddRange(Chain);
                face.ReadFontAssetDefinition();
            }
        }

        private static bool Same(List<TMP_FontAsset> a, List<TMP_FontAsset> b)
        {
            if (a == null || a.Count != b.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Count; i++)
            {
                if (!ReferenceEquals(a[i], b[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static Link LinkOf(TMP_FontAsset font)
        {
            for (int i = 0; i < Links.Count; i++)
            {
                if (ReferenceEquals(Links[i].Copy, font) || ReferenceEquals(Links[i].Game, font))
                {
                    return Links[i];
                }
            }
            return null;
        }

        // The next characters of the language's texts given their fonts, in the chain's order: each copy is asked to draw
        // what the ones before it could not and puts it into its atlas now, so that no text meets a character for the first
        // time in play. A font of the game's written with as it is is only asked whether it draws a character: its atlas is
        // the game's to fill. What no font of the chain draws is left to TextMeshPro's own last resorts, and counted.
        private static void Give(int count)
        {
            var rest = new uint[count];
            for (int i = 0; i < count; i++)
            {
                rest[i] = _wanted[_at + i];
            }
            int modLeft = Math.Max(0, Math.Min(count, _wantedMod - _at));   // the first of this batch are the mod's texts'
            for (int k = 0; k < Chain.Count && rest.Length > 0; k++)
            {
                Link link = LinkOf(Chain[k]);
                if (link == null)
                {
                    continue;
                }
                bool[] drawn = link.Copy != null ? Added(link.Copy, rest) : Drawn(link.Game, rest);
                rest = Without(rest, drawn, link, ref modLeft);
            }
            for (int i = 0; i < rest.Length; i++)
            {
                bool mod = i < modLeft;
                if (LastResort((char)rest[i]))
                {
                    continue;
                }
                if (mod)
                {
                    if (_left < LeftFirst.Length)
                    {
                        LeftFirst[_left] = (char)rest[i];
                    }
                    _left++;
                }
                else
                {
                    _leftGame++;
                }
            }
            _at += count;
        }

        // A copy asked for characters: those it draws are in its atlas when this returns.
        private static bool[] Added(TMP_FontAsset copy, uint[] characters)
        {
            var drawn = new bool[characters.Length];
            try
            {
                copy.TryAddCharacters(characters, out uint[] _);
                for (int i = 0; i < characters.Length; i++)
                {
                    drawn[i] = copy.HasCharacter((int)characters[i]);
                }
            }
            catch (Exception e)
            {
                Said?.Invoke("UI: the interface's copy '" + copy.name + "' could not take characters into its atlas (" + e.GetType().Name + ": " + e.Message + ")");
            }
            return drawn;
        }

        // One of the game's own fonts asked whether it draws characters, nothing put into its atlas: a static one by its
        // table, a dynamic one by its font file, one of the system's by its name.
        private static bool[] Drawn(TMP_FontAsset font, uint[] characters)
        {
            var drawn = new bool[characters.Length];
            try
            {
                bool file = false;
                if (font.atlasPopulationMode == AtlasPopulationMode.Dynamic && font.sourceFontFile != null)
                {
                    file = FontEngine.LoadFontFace(font.sourceFontFile, font.faceInfo.pointSize) == FontEngineError.Success;
                }
                else if (font.atlasPopulationMode == AtlasPopulationMode.DynamicOS)
                {
                    file = FontEngine.LoadFontFace(font.faceInfo.familyName, font.faceInfo.styleName, font.faceInfo.pointSize) == FontEngineError.Success;
                }
                for (int i = 0; i < characters.Length; i++)
                {
                    drawn[i] = font.HasCharacter((int)characters[i])
                               || (file && FontEngine.TryGetGlyphIndex(characters[i], out uint glyph) && glyph != 0);
                }
            }
            catch (Exception e)
            {
                Said?.Invoke("UI: the game's font '" + font.name + "' could not be asked for characters (" + e.GetType().Name + ": " + e.Message + ")");
            }
            return drawn;
        }

        // The characters a font did not draw, in their order, with the font's count of the mod's texts' characters it did.
        private static uint[] Without(uint[] characters, bool[] drawn, Link link, ref int modLeft)
        {
            int kept = 0, modKept = 0;
            for (int i = 0; i < characters.Length; i++)
            {
                if (drawn[i])
                {
                    link.Drew += i < modLeft ? 1 : 0;
                    continue;
                }
                kept++;
                modKept += i < modLeft ? 1 : 0;
            }
            if (kept == characters.Length)
            {
                return characters;
            }
            var rest = new uint[kept];
            int at = 0;
            for (int i = 0; i < characters.Length; i++)
            {
                if (!drawn[i])
                {
                    rest[at++] = characters[i];
                }
            }
            modLeft = modKept;
            return rest;
        }

        // True when TextMeshPro still finds a character no font of the chain draws, in its own general fallbacks or its
        // default font: one of the system's fonts that this machine has.
        private static bool LastResort(char c)
        {
            TMP_FontAsset face = FirstFace();
            try
            {
                return face != null && face.HasCharacter(c, true, false);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // TextMeshPro's general fallbacks in the running game, or null where its settings are not there.
        private static List<TMP_FontAsset> GeneralFallbacks()
        {
            try
            {
                return TMP_Settings.fallbackFontAssets;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
