using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BigAmbitions.Mods;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;
using UnityEngine;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>The step table's kit part: its steps, each a nested class (see SelfTestSteps.cs).</summary>
    internal static partial class SelfTestSteps
    {
        /// <summary>The mod's options and the buttons of its options page (Options, Tests).</summary>
        internal sealed class SetOptionStep : SelfTestStep
        {
            public override string Name => "set_option";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (SelfTestOptions.Set(c.Arg("id"), c.Arg("value"), out string words))
                {
                    c.Pass(words);
                }
                else
                {
                    c.Fail(words);
                }
                yield return new WaitForSecondsRealtime(1.5f);   // the interface's own once a second look sees the switch
            }
        }

        /// <summary>The "assert_option" step (SelfTestKinds has its words).</summary>
        internal sealed class AssertOptionStep : SelfTestStep
        {
            public override string Name => "assert_option";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestOptions.Value(c.Arg("id"), out int now))
                {
                    c.Fail("no option has the id '" + c.Arg("id") + "'");
                }
                else if (!SelfTestOptions.Read(c.Arg("value"), out int want))
                {
                    c.Fail("'" + c.Arg("value") + "' is not a value");
                }
                else if (now != want)
                {
                    c.Fail(c.Arg("id") + " is " + now + ", not " + want);
                }
                else
                {
                    c.Pass(c.Arg("id") + " is " + now);
                }
                yield break;
            }
        }

        /// <summary>The "list_buttons" step (SelfTestKinds has its words).</summary>
        internal sealed class ListButtonsStep : SelfTestStep
        {
            public override string Name => "list_buttons";

            public override IEnumerator Run(SelfTestContext c)
            {
                var labels = new List<string>(128);
                SelfTestUi.Buttons(labels);
                c.Pass(labels.Count + " buttons: " + string.Join(", ", labels));
                yield break;
            }
        }

        /// <summary>The phone and the apps: opened through the game's own phone, closed as a player closes them.</summary>
        internal sealed class OpenPhoneStep : SelfTestStep
        {
            public override string Name => "open_phone";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestGame.CityUp())
                {
                    c.Fail("no city stands: the phone is a city's");
                    yield break;
                }
                SelfTestGame.OpenPhone();
                yield return new WaitForSecondsRealtime(1f);
                if (SelfTestGame.PhoneOpen())
                {
                    c.Pass("the phone's menu is open; " + SelfTestGame.ShowingWords());
                }
                else
                {
                    c.Fail("the phone's menu did not open");
                }
            }
        }

        /// <summary>The "close_screen" step (SelfTestKinds has its words).</summary>
        internal sealed class CloseScreenStep : SelfTestStep
        {
            public override string Name => "close_screen";

            public override IEnumerator Run(SelfTestContext c)
            {
                c.Waiting = "the screens to close";
                yield return SelfTestGame.Close();
                if (SelfTestGame.PhoneOpen() || SelfTestGame.Standing(null) != null)
                {
                    c.Fail("something still shows after the closing: " + SelfTestGame.ShowingWords() + (SelfTestGame.PhoneOpen() ? "; the phone's menu is open" : string.Empty));
                }
                else
                {
                    c.Pass("nothing of the phone or the kit shows");
                }
            }
        }

        /// <summary>The "open_app" step (SelfTestKinds has its words).</summary>
        internal sealed class OpenAppStep : SelfTestStep
        {
            public override string Name => "open_app";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestGame.CityUp())
                {
                    c.Fail("no city stands: the phone is a city's");
                    yield break;
                }
                string app = c.Arg("app");
                if (!SelfTestUi.OpenApp(app))
                {
                    c.Fail("'" + app + "' is no app of the game's phone nor an app of the mod's that is on it");
                    yield break;
                }
                c.Waiting = "the app to open";
                yield return new WaitForSecondsRealtime(c.Number("settle", 1.5f));
                if (c.Flag("shot", true))
                {
                    c.Capture(app);
                    yield return c.PictureWritten();
                }
                if (!SelfTestGame.PhoneOpen() && SelfTestGame.Standing(null) == null)
                {
                    c.Fail("the app '" + app + "' was asked for but neither the phone's menu nor any kit screen shows: " + SelfTestDiag.Dump());
                    yield break;
                }
                c.Pass(app + " opened (phone menu open " + SelfTestGame.PhoneOpen() + "); " + SelfTestGame.ShowingWords());
            }
        }

        /// <summary>
        /// Every app of the phone in turn, the game's and the mod's: opened, settled, a picture, the fault check, closed. With the
        /// RLO interface and the app's screen switch on, an app that has a kit screen must show it, or the step fails.
        /// </summary>
        internal sealed class OpenAllAppsStep : SelfTestStep
        {
            public override string Name => "open_all_apps";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestGame.CityUp())
                {
                    c.Fail("no city stands: the phone is a city's");
                    yield break;
                }
                var apps = new List<KeyValuePair<string, string>>(48);   // app, then the kit screen it is drawn by (or null)
                foreach (string name in SelfTestGame.GameAppNames())
                {
                    apps.Add(new KeyValuePair<string, string>(name, SelfTestUi.KitOfGameApp(name)));
                }
                var mod = new List<KeyValuePair<string, string>>(32);
                PhoneApp.SelfTestList(mod);
                apps.AddRange(mod);
                var lines = new StringBuilder(512);
                int shown = 0, plain = 0, bad = 0;
                for (int i = 0; i < apps.Count; i++)
                {
                    string app = apps[i].Key;
                    if (!SelfTestUi.Wanted(app, c.Arg("only"), c.Arg("skip")))
                    {
                        continue;
                    }
                    string before = c.Runner.Watch.FaultWords();
                    c.Waiting = "the app '" + app + "' to open";
                    if (!SelfTestUi.OpenApp(app))
                    {
                        bad++;
                        lines.Append(app).Append(": could not be opened; ");
                        continue;
                    }
                    yield return new WaitForSecondsRealtime(c.Number("settle", 1.5f));
                    if (c.Flag("shots", true))
                    {
                        c.Capture(app);
                        yield return c.PictureWritten();
                    }
                    string kit = apps[i].Value;
                    bool kitShows = kit != null && SelfTestGame.IsShowing(kit);
                    bool expected = kit != null && UiDirector.On && UiScreenSwitches.On(kit);
                    bool anything = SelfTestGame.PhoneOpen() || SelfTestGame.Standing(null) != null;
                    string faults = c.Runner.Watch.FaultWords();
                    if (!anything)
                    {
                        bad++;
                        lines.Append(app).Append(": asked for but neither the phone menu nor a kit screen shows; ");
                    }
                    else if (faults != before)
                    {
                        bad++;
                        lines.Append(app).Append(": FAULT ").Append(faults).Append("; ");
                    }
                    else if (expected && !kitShows)
                    {
                        bad++;
                        lines.Append(app).Append(": the kit screen '").Append(kit).Append("' does not show (").Append(SelfTestGame.ShowingWords()).Append("); ");
                    }
                    else if (kitShows)
                    {
                        shown++;
                    }
                    else
                    {
                        plain++;
                    }
                    yield return SelfTestGame.Close();
                }
                string summary = shown + " apps showed their kit screen, " + plain + " were the game's or the old interface's own" + (bad > 0 ? ", " + bad + " BAD: " + lines : string.Empty);
                if (bad > 0)
                {
                    c.Fail(summary);
                }
                else
                {
                    c.Pass(summary);
                }
            }
        }

        /// <summary>One screen of the kit's registry opened by whatever opens it (an app, or the mod's own test button) and asserted showing.</summary>
        internal sealed class OpenScreenStep : SelfTestStep
        {
            public override string Name => "open_screen";

            public override IEnumerator Run(SelfTestContext c)
            {
                string id = c.Arg("id");
                if (UiScreens.Find(id) == null)
                {
                    c.Fail("no screen in the registry has the id '" + id + "'");
                    yield break;
                }
                if (!UiDirector.On)
                {
                    c.Fail("the RLO interface is off (or stopped): no kit screen can show; set the option ui_interface to 1 first");
                    yield break;
                }
                var how = new SelfTestUi.Opening();
                c.Waiting = "the screen '" + id + "' to open";
                yield return SelfTestUi.Open(id, c.Number("settle", 1.5f), how);
                if (c.Flag("shot", true) && how.Shown)
                {
                    c.Capture(id);
                    yield return c.PictureWritten();
                }
                if (how.Opener)
                {
                    SelfTestUi.Undo(id);
                }
                if (how.Shown)
                {
                    c.Pass("the kit shows '" + id + "' (" + how.How + ")");
                }
                else if (how.Opener)
                {
                    c.Fail("opened by " + how.How + " but '" + id + "' does not show; " + SelfTestGame.ShowingWords());
                }
                else
                {
                    c.Skip("'" + id + "' cannot be opened by a step: " + how.Why);
                }
            }
        }

        /// <summary>The whole registry: each screen opened where something opens it, a picture and the fault check for each; the rest listed as skipped with the reason.</summary>
        internal sealed class OpenEveryScreenStep : SelfTestStep
        {
            public override string Name => "open_every_screen";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestGame.CityUp())
                {
                    c.Fail("no city stands: the kit's screens are a city's");
                    yield break;
                }
                if (!UiDirector.On)
                {
                    c.Fail("the RLO interface is off (or stopped): no kit screen can show; set the option ui_interface to 1 first");
                    yield break;
                }
                var ok = new StringBuilder(512);
                var skipped = new StringBuilder(1024);
                var failed = new StringBuilder(512);
                int nOk = 0, nSkip = 0, nBad = 0;
                List<string> order = SelfTestUi.Ordered(UiScreens.All);   // plain apps first, then the screens a test button opens, the situations last
                var notAlone = new StringBuilder(256);
                var residues = new StringBuilder(256);
                for (int i = 0; i < order.Count; i++)
                {
                    string id = order[i];
                    if (!SelfTestUi.Wanted(id, c.Arg("only"), c.Arg("skip")))
                    {
                        continue;
                    }
                    yield return SelfTestGame.Close();   // nothing of the last screen is left over this one
                    string before = c.Runner.Watch.FaultWords();
                    var how = new SelfTestUi.Opening();
                    c.Waiting = "screen " + (i + 1) + " of " + order.Count + ": '" + id + "' to open (" + nOk + " shown, " + nSkip + " skipped so far)";
                    yield return SelfTestUi.Open(id, c.Number("settle", 1.5f), how);
                    string faults = c.Runner.Watch.FaultWords();
                    string with = SelfTestGame.Others(id);
                    string listed = SelfTestGame.ShowingWords();
                    if (how.Shown && c.Flag("shots", true))
                    {
                        yield return new WaitForEndOfFrame();
                        c.Capture(id);
                        yield return c.PictureWritten();
                    }
                    if (how.Shown && with.Length > 0)
                    {
                        notAlone.Append(id).Append(" with ").Append(with).Append("; ");   // it could not be shown alone
                    }
                    if (how.Opener)
                    {
                        SelfTestUi.Undo(id);
                    }
                    yield return SelfTestGame.Close();
                    if (SelfTestGame.Residue.Length > 0)
                    {
                        residues.Append("after ").Append(id).Append(": ").Append(SelfTestGame.Residue).Append("; ");
                    }
                    if (faults != before)
                    {
                        nBad++;
                        failed.Append(id).Append(": FAULT ").Append(faults).Append("; ");
                    }
                    else if (how.Shown)
                    {
                        nOk++;
                        ok.Append(id).Append(" [").Append(listed.Replace("kit screens showing: ", string.Empty)).Append("] ");
                    }
                    else
                    {
                        nSkip++;
                        skipped.Append(id).Append(" (").Append(how.Opener ? "opened by " + how.How + ", nothing showed" : how.Why).Append("); ");
                    }
                }
                yield return SelfTestGame.Close();
                string words = nOk + " screens shown, " + nSkip + " skipped, " + nBad + " with a fault. Shown (with the ids showing for each): " + ok + "Skipped: " + skipped
                    + (notAlone.Length > 0 ? "NOT SHOWN ALONE: " + notAlone : string.Empty)
                    + (residues.Length > 0 ? "LEFT OPEN: " + residues : string.Empty) + (nBad > 0 ? "BAD: " + failed : string.Empty);
                if (nBad > 0 || nOk == 0)
                {
                    c.Fail(words);
                }
                else
                {
                    c.Pass(words);
                }
            }
        }

        /// <summary>The "kit_type" step (SelfTestKinds has its words).</summary>
        internal sealed class KitTypeStep : SelfTestStep
        {
            public override string Name => "kit_type";

            public override IEnumerator Run(SelfTestContext c)
            {
                UiHost host = SelfTestGame.Target(c.Arg("screen"));
                if (host == null)
                {
                    c.Fail("no kit screen shows: " + SelfTestGame.ShowingWords());
                    yield break;
                }
                if (!host.SelfTestType(c.Arg("text"), c.Flag("submit", false), out string why))
                {
                    c.Fail("on '" + host.Screen.Id + "': " + why);
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
                c.Pass("typed '" + c.Arg("text") + "' on '" + host.Screen.Id + "'; it has " + host.SelfTestShape());
            }
        }

        /// <summary>The "kit_scroll" step (SelfTestKinds has its words).</summary>
        internal sealed class KitScrollStep : SelfTestStep
        {
            public override string Name => "kit_scroll";

            public override IEnumerator Run(SelfTestContext c)
            {
                UiHost host = SelfTestGame.Target(c.Arg("screen"));
                if (host == null)
                {
                    c.Fail("no kit screen shows: " + SelfTestGame.ShowingWords());
                    yield break;
                }
                if (!host.SelfTestWheel((int)c.Number("clip", 0f), c.Number("notches", 0f), out string why))
                {
                    c.Fail("on '" + host.Screen.Id + "': " + why);
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.4f);
                c.Pass("scrolled region " + c.Arg("clip") + " by " + c.Arg("notches") + " notches on '" + host.Screen.Id + "'");
            }
        }
    }
}
