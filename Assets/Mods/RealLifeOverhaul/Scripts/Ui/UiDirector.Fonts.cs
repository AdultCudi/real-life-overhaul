using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The director's part for the fonts that draw what the interface's faces lack (<see cref="UiFallbackRules"/>): the
    /// game's language told to the fonts, which orders their fallbacks, the language's texts read on a worker thread for
    /// the characters the faces lack (the mod's own file for the language, or its English where it has none, and the
    /// game's), the fonts' work moved on, and the check's one line in Player.log. Behind the loading screen the work is
    /// done whole; from the director's second it goes a copy or a few characters at a time, so a language changed in
    /// play never costs a frame. Cost: nothing a frame; each second a comparison of the language's code while idle.
    /// </summary>
    public sealed partial class UiDirector
    {
        // What a language's texts hold, read from its files on a worker thread.
        private sealed class LanguageText
        {
            public string Language;
            public int Marked, MarkedGame;          // the distinct characters of the mod's texts and of the game's own words
            public char[] Outside, OutsideGame;     // those the faces lack: the mod's, and the game's own beyond them
        }

        private const string English = "en";   // the language the game reads a text in when the player's own has none (Localizor's fallback)

        private static string _fontsLanguage;             // the language last told to the fonts
        private static Task<LanguageText> _fontsRead;     // its texts being read, or null

        // Behind the loading screen, a frame at a time until true: everything at once, where a long frame is not seen.
        private static bool FontsBehindScreen() => FontsLook(int.MaxValue);

        // The fonts' fallbacks moved on: true when nothing is left to do. In play a copy or a few characters a call.
        private static bool FontsLook(int most)
        {
            if (!UiFonts.Ready)
            {
                return false;
            }
            string language = UiGame.Language();
            if (language != null && !string.Equals(language, _fontsLanguage, StringComparison.Ordinal))
            {
                _fontsLanguage = language;
                UiFonts.Language(language);
                _fontsRead = ReadTexts(language);
            }
            if (_fontsRead != null && _fontsRead.IsCompleted)
            {
                LanguageText read = _fontsRead.Status == TaskStatus.RanToCompletion ? _fontsRead.Result : null;
                _fontsRead = null;
                if (read != null)
                {
                    UiFonts.Characters(read.Language, read.Marked, read.Outside, read.MarkedGame, read.OutsideGame);
                }
                else
                {
                    Logger?.Warn("UI: the texts of the game's language (" + language + ") could not be read for the fonts' check; a character the interface's faces lack is given its "
                                 + "font when a text first shows it.");
                }
            }
            bool done = UiFonts.FallbacksStep(most) && _fontsRead == null;
            if (done && UiFonts.FontsNews)
            {
                FontsSay();
            }
            return done;
        }

        // The language's texts read on a worker thread: the mod's file for the language, or its English where the mod has
        // none (the game then reads the mod's texts in English), and the game's own file. The paths are made here: the
        // engine answers for its folders on the main thread only.
        private static Task<LanguageText> ReadTexts(string language)
        {
            string folder = Path.Combine(ModRoot ?? string.Empty, "Locales");
            string own = Path.Combine(folder, language + ".json");
            string mod = File.Exists(own) ? own : Path.Combine(folder, English + ".json");
            string game = Path.Combine(Application.streamingAssetsPath, "locale", language + ".json");
            return Task.Run(() => Texts(language, mod, game));
        }

        private static LanguageText Texts(string language, string modFile, string gameFile)
        {
            try
            {
                var seen = new bool[65536];
                UiFallbackRules.Mark(File.ReadAllText(modFile), seen);
                var text = new LanguageText { Language = language };
                text.Outside = UiFallbackRules.Outside(seen, out text.Marked);
                var game = new bool[65536];
                if (File.Exists(gameFile))
                {
                    UiFallbackRules.Mark(File.ReadAllText(gameFile), game);
                }
                for (int c = 0; c < game.Length; c++)
                {
                    text.MarkedGame += game[c] ? 1 : 0;
                    game[c] &= !seen[c];   // the game's own words are asked only for what the mod's texts do not hold already
                }
                text.OutsideGame = UiFallbackRules.Outside(game, out int _);
                return text;
            }
            catch (Exception)
            {
                return null;   // said on the main thread, where the logger is
            }
        }

        // The check's line, once a language: whatever Developer diagnostics says, since a report of missing text is answered
        // by this one line of a player's log. A character left without a font, or a line the kit's own text did not draw
        // whole, is a warning.
        private static void FontsSay()
        {
            string sample = UiFonts.FontsSample(UiFontProbe.Most);
            int given = sample.Length, drawn = 0;
            if (given > 0)
            {
                drawn = UiFontProbe.Drawn(sample);
                if (drawn < 0)
                {
                    given = drawn = 0;   // no face to write with yet: the line says what the fonts hold, and nothing of a drawing
                }
            }
            string line = UiFonts.FontsLine(given, drawn, GameBuild.Running);
            if (UiFonts.FontsPass && drawn >= given)
            {
                Meta.Dev.Always(Logger, line);
            }
            else
            {
                Logger?.Warn(line);
            }
        }
    }
}
