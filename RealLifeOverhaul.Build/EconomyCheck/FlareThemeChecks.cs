using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Flare;

// Checks for Flare's light and dark switch (FlareThemeRules, Social/Flare/FlareApp.Theme.cs): the look each setting shows
// through the day; a press always gives the other look from the one showing and never follows the phone, from every setting,
// so two presses come back; the switch's words are the options page's choices, in every language, and the tip, the Guide and
// What's new say where it is; the press writes the option through the Settings app's own path and puts it on disk as that app
// does, so the options page and the Settings app read the same value after a restart; a new look paints every open part of
// the app at once (the frame, the cards in view and those kept for later, an open sheet); and nothing goes over the network.
internal static partial class Program
{
    private static void FlareThemeSwitch()
    {
        FlareThemeLooks();
        FlareThemePress();
        FlareThemeWords();
        FlareThemeWrites();
        FlareThemePaints();
    }

    private static readonly int[] ThemeSettings = { FlareThemeRules.FollowsPhone, FlareThemeRules.Light, FlareThemeRules.Dark, -1, 3, 99, int.MinValue, int.MaxValue };

    private static void FlareThemeLooks()
    {
        Check(FlareThemeRules.FollowsPhone == 0 && FlareThemeRules.Light == 1 && FlareThemeRules.Dark == 2,
            "Flare look: the settings keep the drop-down's numbers, which the options page saves (0 follows the phone, 1 light, 2 dark)");
        Check(FlareThemeRules.DarkFrom == 19 && FlareThemeRules.DarkUntil == 7, "Flare look: following the phone, dark from 19:00 until 07:00 of the game's clock");
        int darkHours = 0;
        bool light = true, dark = true, phone = true;
        for (int h = 0; h < 24; h++)
        {
            light &= !FlareThemeRules.IsDark(FlareThemeRules.Light, h);
            dark &= FlareThemeRules.IsDark(FlareThemeRules.Dark, h);
            bool night = h >= 19 || h < 7;
            phone &= FlareThemeRules.IsDark(FlareThemeRules.FollowsPhone, h) == night;
            darkHours += FlareThemeRules.IsDark(FlareThemeRules.FollowsPhone, h) ? 1 : 0;
        }
        Check(light, "Flare look: Light is light at every hour of the day");
        Check(dark, "Flare look: Dark is dark at every hour of the day");
        Check(phone && darkHours == 12, "Flare look: following the phone is dark from 19:00 to 06:59, 12 hours of the 24 (" + darkHours + ")");
        Check(FlareThemeRules.IsDark(0, 6) && !FlareThemeRules.IsDark(0, 7) && !FlareThemeRules.IsDark(0, 18) && FlareThemeRules.IsDark(0, 19)
              && FlareThemeRules.IsDark(0, 0) && FlareThemeRules.IsDark(0, 23) && !FlareThemeRules.IsDark(0, 12),
            "Flare look: following the phone turns light at 07:00 and dark at 19:00, and is dark at midnight and light at noon");
        bool unknown = true;
        foreach (int s in new[] { -1, 3, 99, int.MinValue, int.MaxValue })
        {
            for (int h = 0; h < 24; h++)
            {
                unknown &= FlareThemeRules.IsDark(s, h) == FlareThemeRules.IsDark(FlareThemeRules.FollowsPhone, h);
            }
        }
        Check(unknown, "Flare look: a saved setting with no choice of its own follows the phone, as the game's drop-down shows it");
        bool same = true;
        foreach (int s in ThemeSettings)
        {
            for (int h = 0; h < 24; h++)
            {
                same &= FlareThemeRules.IsDark(s, h) == FlareThemeRules.IsDark(s, h);
            }
        }
        Check(same, "Flare look: the same setting and hour give the same look every time");
    }

    private static void FlareThemePress()
    {
        bool fixedOnly = true, other = true, back = true;
        foreach (int s in ThemeSettings)
        {
            foreach (bool showing in new[] { false, true })
            {
                int next = FlareThemeRules.Next(s, showing);
                fixedOnly &= next == FlareThemeRules.Light || next == FlareThemeRules.Dark;
                other &= next == (showing ? FlareThemeRules.Light : FlareThemeRules.Dark) && next == FlareThemeRules.Next(s, showing);
            }
            for (int h = 0; h < 24; h++)
            {
                bool before = FlareThemeRules.IsDark(s, h);
                int once = FlareThemeRules.Next(s, before);
                bool after = FlareThemeRules.IsDark(once, h);
                int twice = FlareThemeRules.Next(once, after);
                other &= after != before;
                back &= FlareThemeRules.IsDark(twice, h) == before && twice != FlareThemeRules.FollowsPhone;
            }
        }
        Check(fixedOnly, "Flare look: a press always sets Light or Dark, never Follows the phone, from every setting (the option keeps the third choice)");
        Check(other, "Flare look: a press always gives the other look from the one showing, at every hour and from every setting");
        Check(back, "Flare look: two presses bring back the look there was, at every hour and from every setting");
        Check(FlareThemeRules.Next(FlareThemeRules.FollowsPhone, true) == FlareThemeRules.Light && FlareThemeRules.Next(FlareThemeRules.FollowsPhone, false) == FlareThemeRules.Dark,
            "Flare look: following the phone, a press at night (dark showing) sets Light and by day sets Dark");
        Check(FlareThemeRules.Next(FlareThemeRules.Light, false) == FlareThemeRules.Dark && FlareThemeRules.Next(FlareThemeRules.Dark, true) == FlareThemeRules.Light,
            "Flare look: from Light a press sets Dark, from Dark it sets Light");
        Check(FlareThemeRules.Next(FlareThemeRules.Light, true) == FlareThemeRules.Light && FlareThemeRules.Next(FlareThemeRules.Dark, false) == FlareThemeRules.Dark,
            "Flare look: a look still to be painted again (the option changed while the app showed the other) is turned by a press, not kept as it is");
        Check(FlareThemeRules.Next(FlareThemeRules.FollowsPhone, FlareThemeRules.IsDark(FlareThemeRules.FollowsPhone, 18)) == FlareThemeRules.Dark
              && FlareThemeRules.Next(FlareThemeRules.FollowsPhone, false) == FlareThemeRules.Dark,
            "Flare look: following the phone, the light still showing at 19:00 before the app paints again is pressed to dark, not left light");
    }

    private static void FlareThemeWords()
    {
        Check(FlareThemeRules.ChoiceKeys.Length == 3 && FlareThemeRules.ChoiceKey(FlareThemeRules.FollowsPhone) == "reallife_flare_theme_0"
              && FlareThemeRules.ChoiceKey(FlareThemeRules.Light) == "reallife_flare_theme_1" && FlareThemeRules.ChoiceKey(FlareThemeRules.Dark) == "reallife_flare_theme_2",
            "Flare look: each setting's words are the options page's choice for it");
        Check(FlareThemeRules.ChoiceKey(-1) == "reallife_flare_theme_0" && FlareThemeRules.ChoiceKey(3) == "reallife_flare_theme_0"
              && FlareThemeRules.ChoiceKey(int.MaxValue) == "reallife_flare_theme_0",
            "Flare look: a setting with no choice of its own has Follows the phone's words, as it follows the phone");
        bool named = true;
        foreach (int s in ThemeSettings)
        {
            foreach (bool showing in new[] { false, true })
            {
                named &= FlareThemeRules.ChoiceKey(FlareThemeRules.Next(s, showing)) == (showing ? "reallife_flare_theme_1" : "reallife_flare_theme_2");
            }
        }
        Check(named, "Flare look: the switch names the look a press gives: Dark while light shows, Light while dark shows");

        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("Flare look", langs, codes))
        {
            return;
        }
        var lacking = new List<string>();
        var unnamed = new List<string>();
        var unplaced = new List<string>();
        for (int l = 0; l < langs.Length; l++)
        {
            string lightWord = langs[l].TryGetValue("reallife_flare_theme_1", out string a) ? a : null;
            string darkWord = langs[l].TryGetValue("reallife_flare_theme_2", out string b) ? b : null;
            if (string.IsNullOrWhiteSpace(lightWord) || string.IsNullOrWhiteSpace(darkWord) || lightWord == darkWord)
            {
                lacking.Add(codes[l]);
                continue;
            }
            // A page taken out of a later language's file, to be translated again, reads in English until it is (the Guide's Flare page was changed).
            bool guideOut = l >= RealLifeOverhaul.ModLanguages.Required && !LoadWords(System.IO.Path.Combine(LocalesFolder(), codes[l] + ".json")).ContainsKey("reallife_guide_flare");
            foreach (string key in new[] { "reallife_opt_tip_flare_theme", "reallife_guide_flare" })
            {
                if (guideOut && key == "reallife_guide_flare")
                {
                    continue;
                }
                string text = langs[l].TryGetValue(key, out string t) ? t : string.Empty;
                if (!ThemeQuoted(text, lightWord) || !ThemeQuoted(text, darkWord))
                {
                    unnamed.Add(key + " (" + codes[l] + ")");
                }
            }
            if (!ThemeNextLines.TryGetValue(codes[l], out string line) || !FixMarks.TryGetValue(codes[l], out string mark))
            {
                unplaced.Add(codes[l] + ": no line or fix mark known for it here");
                continue;
            }
            if (!WhatsNewHas(langs[l], line) || !ThemeBeforeFixes(langs[l], line, mark))
            {
                unplaced.Add(codes[l]);
            }
        }
        Check(lacking.Count == 0, "Flare look: the switch's two words are in every language and differ (" + string.Join(", ", lacking) + ")");
        Check(unnamed.Count == 0, "Flare look: the option's tip and the Guide's Flare page name the switch's two words in quotes in every language ("
                                  + string.Join(", ", unnamed) + ")");
        Check(unplaced.Count == 0, "Flare look: What's new has the switch's line in every language, before the row's fixes, so the change note lists it as new ("
                                   + string.Join(", ", unplaced) + ")");
    }

    // What's new's line for the switch in each language, as the locale files have it.
    private static readonly Dictionary<string, string> ThemeNextLines = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "en", "Flare has a light and dark switch on its top bar" },
        { "pt", "O Flare tem um botão de claro e escuro na barra superior" },
        { "zh-cn", "Flare顶栏新增浅色与深色切换按钮" },
        { "de", "Flare hat in der oberen Leiste eine Taste für hell und dunkel" },
        { "fr", "Flare a un bouton clair et sombre dans sa barre du haut" },
        { "es", "Flare tiene un botón de claro y oscuro en su barra superior" },
        { "ru", "На верхней панели Flare появилась кнопка светлого и тёмного вида" },
        { "nl", "Flare heeft een knop voor licht en donker in de bovenbalk" },
        { "tr", "Flare'in üst çubuğunda açık ve koyu görünüm için bir düğme var" }
    };

    // The word in one of the quote marks the languages use: 'x', "x", “x”, „x“, «x» or « x ».
    private static bool ThemeQuoted(string text, string word)
    {
        return Regex.IsMatch(text ?? string.Empty, "['\"“„«]\\s?" + Regex.Escape(word) + "\\s?['\"“”»]");
    }

    // In the What's new row that has the line, the line comes before the row's first fix (the change note splits on the marks).
    private static bool ThemeBeforeFixes(Dictionary<string, string> words, string line, string mark)
    {
        foreach (KeyValuePair<string, string> row in words)
        {
            if (!row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) || row.Key.EndsWith("_title", StringComparison.Ordinal))
            {
                continue;
            }
            int at = row.Value.IndexOf(line, StringComparison.Ordinal);
            if (at >= 0)
            {
                int fix = row.Value.IndexOf(mark, StringComparison.Ordinal);
                return fix < 0 || at < fix;
            }
        }
        return false;
    }

    private static void FlareThemeWrites()
    {
        string theme = ReviewSource("Social/Flare/FlareApp.Theme.cs");
        string press = FlareMethod(theme, "private void ThemeClicked");
        Check(press.Contains("FlareThemeRules.Next(Settings.FlareTheme, wasDark)") && press.Contains("Meta.OptionsLayout.AppSet(Meta.OptionsLayout.AppFind(Settings.FlareThemeId), next)"),
            "Flare look: a press works out the next setting by the rule and writes the option flare_theme through OptionsLayout.AppSet, the Settings app's own path");
        Check(!theme.Contains("PlayerPrefs.SetInt") && !Regex.IsMatch(theme, @"Settings\.FlareTheme\s*=[^=]") && !theme.Contains("SettingsTable.Write"),
            "Flare look: the switch has no second way to write the option (no PlayerPrefs.SetInt, no Settings.FlareTheme set by hand, no preset's path)");
        string settingsSet = FlareMethod(ReviewSource("Meta/SettingsView.Rows.cs"), "private void Set(SettingsLine line, int value)");
        Check(settingsSet.Contains("OptionsLayout.AppSet(line, value)"), "Flare look: the Settings app writes an option through the same OptionsLayout.AppSet");
        string appSet = FlareMethod(ReviewSource("Meta/OptionsLayout.Book.cs"), "internal static bool AppSet(SettingsLine line, int value)");
        Check(appSet.Contains("Settings.PrefsKey(row.Id)") && appSet.Contains("UnityEngine.PlayerPrefs.SetInt(key, value)") && appSet.Contains("dropdown.OnValueChanged?.Invoke(value)"),
            "Flare look: AppSet saves under the options page's own key and then calls the drop-down's handler, as the game's control does");
        string mod = ReviewSource("Core/RealLifeOverhaulMod.cs");
        string dropdown = @"\.AddDropdown\(Settings\.FlareThemeId, ""reallife_flare_opt_theme"",\s*"
                          + @"new\[\] \{ ""reallife_flare_theme_0"", ""reallife_flare_theme_1"", ""reallife_flare_theme_2"" \}, "
                          + @"Settings\.FlareTheme,\s*v => Settings\.FlareTheme = v\)";
        Check(Regex.IsMatch(mod, dropdown),
            "Flare look: the options page's drop-down has the switch's three choices in the rule's order, and its handler sets Settings.FlareTheme");
        string settings = ReviewSource("Core/Settings.cs");
        Check(settings.Contains("FlareThemeId = \"flare_theme\"") && settings.Contains("FlareTheme = Int(modId, FlareThemeId, FlareTheme);"),
            "Flare look: the option is read back from its key when the options are read, so the choice survives a restart");

        string closed = FlareMethod(ReviewSource("Meta/SettingsView.cs"), "public void Closed()");
        string save = FlareMethod(theme, "private void SaveTheme()");
        Check(closed.Contains("_wrote") && closed.Contains("UiClone.Safe(UnityEngine.PlayerPrefs.Save)") && save.Contains("_themeWritten")
              && save.Contains("UiClone.Safe(UnityEngine.PlayerPrefs.Save)"),
            "Flare look: what the switch wrote goes on disk with PlayerPrefs.Save when it closes, as the Settings app's changes do");
        string app = ReviewSource("Social/Flare/FlareApp.cs");
        Check(press.IndexOf("_themeWritten = true", StringComparison.Ordinal) > press.IndexOf("AppSet(", StringComparison.Ordinal)
              && FlareMethod(app, "public void Closed()").Contains("SaveTheme();") && FlareMethod(app, "public void Dispose()").Contains("SaveTheme();"),
            "Flare look: a written choice is saved when the app closes and when Flare stops, never left only in memory");
        Check(ReviewSource("Social/Flare/FlareKit.cs").Contains("FlareThemeRules.IsDark(Settings.FlareTheme, hour) ? Night : Light"),
            "Flare look: the look painted reads the same setting through the same rule as the switch");
        string rules = ReviewSource("Social/Flare/FlareThemeRules.cs");
        bool local = true;
        foreach (string net in new[] { "Share", "Net.", "GoingPublic", "SharedCity", "Outbox", "Send" })
        {
            local &= !theme.Contains(net) && !rules.Contains(net);
        }
        Check(local, "Flare look: the switch is this machine's look only: nothing of it goes to the shared city or over Going Public");
        string buildFrame = FlareMethod(ReviewSource("Social/Flare/FlareApp.Frame.cs"), "private void BuildFrame()");
        Check(!Regex.IsMatch(theme, @"void\s+(Update|LateUpdate|FixedUpdate|OnGUI)\s*\(") && buildFrame.Contains("BuildTheme();")
              && FlareMethod(theme, "private void BuildTheme()").Contains("new Pill(_header, \"Theme\", ThemeClicked)"),
            "Flare look: the switch is Flare's own pill on the top bar, made once with the frame; it acts only when pressed, with nothing run each frame");
    }

    private static void FlareThemePaints()
    {
        string app = ReviewSource("Social/Flare/FlareApp.cs");
        string frame = ReviewSource("Social/Flare/FlareApp.Frame.cs");
        string theme = ReviewSource("Social/Flare/FlareApp.Theme.cs");
        string press = FlareMethod(theme, "private void ThemeClicked");
        Check(press.IndexOf("Refresh(false)", StringComparison.Ordinal) > press.IndexOf("AppSet(", StringComparison.Ordinal),
            "Flare look: after the option is written, a press writes the app again at once, keeping the list where it was");
        string refresh = FlareMethod(app, "private void Refresh(bool all)");
        int paint = refresh.IndexOf("Paint();", StringComparison.Ordinal), lay = refresh.IndexOf("Lay();", StringComparison.Ordinal);
        int sheet = refresh.IndexOf("WriteSheet();", StringComparison.Ordinal);
        Check(refresh.Contains("FlareLook look = FlareLook.Now(hour);") && refresh.Contains("bool painted = look != _look || all;") && paint > 0 && lay > paint
              && sheet > lay && refresh.LastIndexOf("if (painted)", sheet, StringComparison.Ordinal) > lay,
            "Flare look: a new look paints the app, and an open sheet is written again in it once laid out (its chips and words take their colours as written)");
        Check(refresh.Contains("WriteRail(file);") && refresh.Contains("WriteSide(file);") && refresh.Contains("WriteHeader(file);") && refresh.Contains("_list.Set(_rows, _top);"),
            "Flare look: the rail, the side, the top bar and the cards in view are written again in the look on the same refresh");
        string paintBody = FlareMethod(frame, "private void Paint()");
        Check(paintBody.Contains("_backdrop.color = l.Back;") && paintBody.Contains("PaintSide();") && paintBody.Contains("PaintSheet();") && paintBody.Contains("_list.Paint(l);"),
            "Flare look: painting covers the frame, the side, the sheet and every card");
        string list = ReviewSource("Social/Flare/FlareList.cs");
        string listPaint = FlareMethod(list, "public void Paint(FlareLook look)");
        Check(listPaint.Contains("_live[i].Paint(look);") && listPaint.Contains("_pool") && listPaint.Contains("c.Paint(look);")
              && FlareMethod(list, "private FlareCell Take(int kind)").Contains("c.Paint(_look);"),
            "Flare look: the list paints the cards in view, those kept for later and any new one in the look, not only the next screen");
        string set = FlareMethod(list, "public void Set(List<FlareRow> rows, bool top)");
        Check(set.Contains("Free(_live[i]);") && set.Contains("Fill();"), "Flare look: the cards in view are bound again on a refresh, so their own colours follow the look");
        Check(FlareMethod(frame, "private void WriteHeader(FlareFile file)").Contains("WriteTheme(colW)")
              && FlareMethod(theme, "private float WriteTheme(float width)").Contains("_theme.Show(_look,"),
            "Flare look: the switch itself is written with the top bar in the look it shows, naming the other");
        Check(FlareMethod(ReviewSource("Social/Flare/FlareApp.Sheet.cs"), "private void WriteSheet()").Contains("if (_sheetKind == SheetNone)"),
            "Flare look: with no sheet open, writing the sheet again costs nothing");
    }
}
