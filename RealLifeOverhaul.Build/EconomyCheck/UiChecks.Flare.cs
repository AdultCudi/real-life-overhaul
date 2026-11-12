using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    static partial void UiCasesFlare(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id != "flare")
        {
            return;
        }
        for (int i = 0; i < UiSamples.FlareStates.Length; i++)
        {
            string state = UiSamples.FlareStates[i];
            cases.Add(new UiScreenCase
            {
                Def = def,
                Name = "flare (" + state + ")",
                Title = "Flare",
                Commands = FlareFace.Commands,
                Lay = (s, f, dark, k, t) => UiSamples.Flare(s, f, dark, k, t, state)
            });
        }
    }

    private static void FlareLayoutChecks()
    {
        UiChrome.Status = () => UiSamples.Status;
        UiScreenDef def = null;
        foreach (UiScreenDef screen in UiScreens.All)
        {
            def = screen.Id == "flare" ? screen : def;
        }
        var cases = new List<UiScreenCase>();
        UiCasesFlare(def, cases);
        var scene = new UiScene();
        var said = new HashSet<string>();
        Action<string> say = line =>
        {
            if (said.Add(line))
            {
                Console.WriteLine("        " + line);
            }
        };
        foreach (UiScreenCase c in cases)
        {
            var used = new List<string>();
            UiWords en = UiWords("en");
            c.Lay(scene, UiFrame.FullMenu(16f / 9f, c.Title), true, UiDefaultKeys.Keyboard, (key, english) =>
            {
                used.Add(key);
                return en(key, english);
            });
            string missed = "none";
            foreach (string key in used)
            {
                if (Array.IndexOf(c.Def.Keys, key) < 0)
                {
                    missed = key;
                }
            }
            Check(missed == "none", "flare face: every key is named (" + missed + ")");
            foreach (string lang in ModLanguages.All)
            {
                UiWords t = UiWords(lang);
                foreach (float aspect in UiAspects)
                {
                    foreach (IUiKeys keys in new IUiKeys[] { UiDefaultKeys.Keyboard, UiDefaultKeys.XboxPad })
                    {
                        if (keys != UiDefaultKeys.Keyboard && lang != "en")
                        {
                            continue;
                        }
                        UiFrame frame = UiFrame.FullMenu(aspect, c.Title);
                        c.Lay(scene, frame, true, keys, t);
                        string where = c.Name + " " + lang + " " + aspect.ToString("0.00") + (keys.Pad != UiPad.None ? " pad" : "");
                        Check(UiCheckRules.Overlaps(scene, say) == 0, "flare face: nothing overlaps: " + where);
                        Check(UiCheckRules.Spills(scene, say) == 0, "flare face: nothing spills: " + where);
                        Check(UiCheckRules.OffGrid(scene, say) == 0, "flare face: on the grid: " + where);
                        Check(UiCheckRules.Unfit(scene, say) == 0, "flare face: labels fit: " + where);
                        Check(UiCheckRules.SmallTargets(scene, say) == 0, "flare face: targets: " + where);
                        Check(UiCheckRules.TooSmall(scene, say) == 0, "flare face: text size: " + where);
                        Check(UiCheckRules.Crowded(scene, say) == 0, "flare face: not crowded: " + where);
                        Check(UiCheckRules.NoPrompts(scene, say) == 0, "flare face: prompts: " + where);
                        Check(UiCheckRules.Contradictions(scene, say) == 0, "flare face: indicators: " + where);
                        Check(UiCheckRules.DeadSpace(scene, say) == 0, "flare face: no dead space: " + where);
                        Check(UiCheckRules.Unfilled(scene, frame, say) == 0, "flare face: fills the window: " + where);
                        Check(UiCheckRules.KeyClash(scene, say) == 0, "flare face: keys: " + where);
                        Check(UiFocusRules.Unreached(scene, out string who) == 0, "flare face: d-pad reaches every target: " + where + " (" + who + ")");
                        Check(UiCheckRules.LowContrast(scene, say) == 0, "flare face: contrast: " + where);
                        Check(UiCheckRules.RawKeys(scene, say) == 0, "flare face: no raw keys: " + where);
                        Check(UiCheckRules.Repeats(scene, frame, say) == 0, "flare face: no repeated title: " + where);
                        Check(UiCheckRules.Stacked(scene, say) == 0, "flare face: no stacked labels: " + where);
                        Check(UiCheckRules.EmptyPanels(scene, say) == 0, "flare face: no empty panel: " + where);
                        Check(UiCheckRules.Floating(scene, say) == 0, "flare face: overlays: " + where);
                        Check(UiCheckRules.Strays(scene, say) == 0, "flare face: no stray marks: " + where);
                        Check(UiCheckRules.Spines(scene, say) == 0, "flare face: no spines: " + where);
                        Check(UiCheckRules.TypeLevels(scene, say) == 0, "flare face: three sizes: " + where);
                        Check(UiCheckRules.UnknownCommands(scene, c.Commands, say) == 0, "flare face: commands: " + where);
                        if (_failed > 12)
                        {
                            return;
                        }
                    }
                }
            }
        }
    }
}
