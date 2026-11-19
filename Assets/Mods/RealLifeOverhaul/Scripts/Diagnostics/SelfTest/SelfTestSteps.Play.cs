using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Helpers;
using RealLifeOverhaul.Ui;
using UnityEngine;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>
    /// The step table's play part: pressing the mod's test buttons and the kit's targets as a player does, with evidence of what the press
    /// said; answering the game's question; going in and out of buildings, setting the weekday, finding a place by what the city holds,
    /// and calling and reading the mod's own members by name.
    /// </summary>
    internal static partial class SelfTestSteps
    {
        /// <summary>The "press_button" step (SelfTestKinds has its words): a press passes only on what the plan expects of what it said.</summary>
        internal sealed class PressButtonStep : SelfTestStep
        {
            public override string Name => "press_button";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestUi.PressButton(c.Arg("button"), out string why))
                {
                    c.Fail(why);
                    yield break;
                }
                c.Waiting = "the button's work to settle";
                yield return new WaitForSecondsRealtime(c.Number("settle", 2f));
                List<string> said = c.Runner.Watch.NoticeLines(c.Spec.Label);
                int notices = said.Count;
                said.AddRange(c.Runner.Watch.LogLinesSince(c.Spec.Label, 8));
                string words = said.Count == 0 ? "nothing (no notice, no log line)" : string.Join(" | ", said);
                string refuse = c.Arg("refuse"), expectText = c.Arg("expect_text"), expectLog = c.Arg("expect_log");
                if (refuse != null && Holds(said, refuse))
                {
                    c.Fail("refused ('" + refuse + "'): " + words);
                }
                else if (expectText != null && !Holds(said, expectText))
                {
                    c.Fail("pressed " + c.Arg("button") + " but nothing said '" + expectText + "'; it said: " + words);
                }
                else if (expectLog != null && !Holds(said.GetRange(notices, said.Count - notices), expectLog))
                {
                    c.Fail("pressed " + c.Arg("button") + " but no log line held '" + expectLog + "'; it said: " + words);
                }
                else if (expectText == null && expectLog == null)
                {
                    c.Skip("pressed " + c.Arg("button") + ", said: " + words + " (no expect_text or expect_log: nothing was asserted)");
                }
                else
                {
                    c.Pass("pressed " + c.Arg("button") + ", said: " + words);
                }
            }

            private static bool Holds(List<string> lines, string text)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>The "kit_press" step (SelfTestKinds has its words): by the name of a command constant, by a number, or by the words on the target.</summary>
        internal sealed class KitPressStep : SelfTestStep
        {
            public override string Name => "kit_press";

            public override IEnumerator Run(SelfTestContext c)
            {
                UiHost host = SelfTestGame.Target(c.Arg("screen"));
                if (host == null)
                {
                    c.Fail("no kit screen" + (c.Arg("screen") != null ? " '" + c.Arg("screen") + "'" : string.Empty) + " shows: " + SelfTestGame.ShowingWords());
                    yield break;
                }
                string id = host.Screen.Id, why;
                string text = c.Arg("text");
                if (c.Arg("key") != null)
                {
                    text = UiText.T(c.Arg("key"));
                }
                string pressed;
                if (text != null)
                {
                    if (!host.SelfTestPressText(text, (int)c.Number("nth", 1f), out string label, out why))
                    {
                        c.Fail("on '" + id + "': " + why);
                        yield break;
                    }
                    pressed = "the target '" + label + "'";
                }
                else if (c.Arg("command") != null)
                {
                    if (!SelfTestReflect.Command(c.Arg("command"), id, out int command, out why))
                    {
                        c.Fail(why);
                        yield break;
                    }
                    int arg = (int)c.Number("arg", -1f);
                    if (!host.SelfTestPress(command, arg, out why))
                    {
                        c.Fail("on '" + id + "': " + why);
                        yield break;
                    }
                    pressed = "command " + c.Arg("command") + "=" + command + (arg >= 0 ? " argument " + arg : string.Empty);
                }
                else
                {
                    c.Fail("name a command (a constant's name or a number), the words on the target (text) or a text key (key)");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(c.Number("settle", 0.6f));
                c.Pass("pressed " + pressed + " on '" + id + "'; it has " + host.SelfTestShape());
            }
        }

        /// <summary>The "kit_commands" step (SelfTestKinds has its words): a dump to write and debug a plan from.</summary>
        internal sealed class KitCommandsStep : SelfTestStep
        {
            public override string Name => "kit_commands";

            public override IEnumerator Run(SelfTestContext c)
            {
                var sb = new StringBuilder(2048);
                int screens = 0;
                foreach (UiHost host in SelfTestGame.KitHosts())
                {
                    string id = host.Screen.Id;
                    if (c.Arg("screen") != null && id != c.Arg("screen"))
                    {
                        continue;
                    }
                    screens++;
                    sb.Append(id).Append(" (").Append(host.SelfTestShape()).Append("):\n");
                    host.SelfTestTargets(sb, command => SelfTestReflect.NameOf(command, id), (int)c.Number("most", 120f));
                }
                c.Pass(screens == 0 ? "no kit screen shows" : screens + " screen(s) with their targets:\n" + sb);
                yield break;
            }
        }

        /// <summary>The "confirm" step (SelfTestKinds has its words).</summary>
        internal sealed class ConfirmStep : SelfTestStep
        {
            public override string Name => "confirm";

            public override IEnumerator Run(SelfTestContext c)
            {
                float until = Time.realtimeSinceStartup + c.Number("wait", 5f);
                while (!HudConfirm.isOpen)
                {
                    c.Waiting = "the game's question box to open";
                    if (Time.realtimeSinceStartup > until)
                    {
                        c.Fail("no question box opened within " + c.Number("wait", 5f) + " s: " + SelfTestGame.ShowingWords());
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.25f);
                }
                bool yes = c.Arg("answer", "yes") != "no";
                if (yes)
                {
                    HudConfirm.onConfirm?.Invoke();
                }
                else
                {
                    HudConfirm.onClose?.Invoke();
                }
                yield return new WaitForSecondsRealtime(c.Number("settle", 1f));
                if (HudConfirm.isOpen)
                {
                    c.Fail("the question box is still open after answering " + (yes ? "yes" : "no"));
                }
                else
                {
                    c.Pass("the question box was open and was answered " + (yes ? "yes" : "no"));
                }
            }
        }

        /// <summary>The "enter_building" step (SelfTestKinds has its words).</summary>
        internal sealed class EnterBuildingStep : SelfTestStep
        {
            public override string Name => "enter_building";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestGame.CityUp())
                {
                    c.Fail("no city stands");
                    yield break;
                }
                if (!SelfTestWorld.ParseAddress(c.Arg("address"), out int number, out string street) || !SelfTestWorld.Enter(number, street))
                {
                    c.Fail("no building to enter at '" + c.Arg("address") + "'");
                    yield break;
                }
                float until = Time.realtimeSinceStartup + 20f;
                while (!BuildingManager.IsInsideBuilding)
                {
                    c.Waiting = "the player to be inside " + c.Arg("address");
                    if (Time.realtimeSinceStartup > until)
                    {
                        c.Fail("the player is not inside after 20 s");
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                yield return new WaitForSecondsRealtime(2f);
                c.Pass("inside " + c.Arg("address"));
            }
        }

        /// <summary>The "leave_building" step (SelfTestKinds has its words).</summary>
        internal sealed class LeaveBuildingStep : SelfTestStep
        {
            public override string Name => "leave_building";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!BuildingManager.IsInsideBuilding)
                {
                    c.Pass("already outdoors");
                    yield break;
                }
                yield return SelfTestGame.Close();
                SelfTestWorld.Leave();
                float until = Time.realtimeSinceStartup + 20f;
                while (BuildingManager.IsInsideBuilding)
                {
                    c.Waiting = "the player to be outdoors";
                    if (Time.realtimeSinceStartup > until)
                    {
                        c.Fail("the player is still inside after 20 s");
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                yield return new WaitForSecondsRealtime(2f);
                c.Pass("outdoors");
            }
        }

        /// <summary>The "set_day" step (SelfTestKinds has its words).</summary>
        internal sealed class SetDayStep : SelfTestStep
        {
            public override string Name => "set_day";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestGame.CityUp())
                {
                    c.Fail("no city stands");
                    yield break;
                }
                int hour = (int)c.Number("hour", 9f);
                int day = SelfTestWorld.NextWeekday(c.Arg("weekday"), hour);
                if (day < 0)
                {
                    c.Fail("'" + c.Arg("weekday") + "' is not a weekday (Monday to Sunday)");
                    yield break;
                }
                string before = SelfTestGame.CityWords();
                if (!SelfTestTime.SkipTo(day, hour, 0f))
                {
                    c.Fail("the game's time machine would not start");
                    yield break;
                }
                while (SelfTestTime.Running())
                {
                    c.Waiting = "the time machine to reach " + c.Arg("weekday") + " " + hour + ":00";
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                yield return new WaitForSecondsRealtime(1.5f);
                c.Pass("now " + c.Arg("weekday") + " day " + day + " " + hour + ":00 (was " + before + ")");
            }
        }

        /// <summary>The "find_place" step (SelfTestKinds has its words): an address the city holds, put in a variable later steps read as {name}.</summary>
        internal sealed class FindPlaceStep : SelfTestStep
        {
            public override string Name => "find_place";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestGame.CityUp())
                {
                    c.Fail("no city stands");
                    yield break;
                }
                if (!SelfTestWorld.FindPlace(c.Arg("kind"), out string address, out string why))
                {
                    c.Fail(why);
                    yield break;
                }
                c.Runner.SetVar(c.Arg("as"), address);
                c.Pass("a " + c.Arg("kind") + " at " + address + " (in {" + c.Arg("as") + "})");
                yield break;
            }
        }

        /// <summary>The "assert_home" step (SelfTestKinds has its words).</summary>
        internal sealed class AssertHomeStep : SelfTestStep
        {
            public override string Name => "assert_home";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (SaveGameManager.Current == null)
                {
                    c.Fail("no game is loaded");
                    yield break;
                }
                bool homeless = false;
                foreach (HappinessModifierData m in SaveGameManager.Current.happinessModifiers)
                {
                    homeless |= m != null && m.type == "ba:happinessmodifier_no_home";
                }
                bool absent = c.Flag("absent", false);
                if (homeless == absent)
                {
                    c.Pass(homeless ? "the player has no home (the game's no-home mood is on)" : "the player has a home (the game's no-home mood is off)");
                }
                else
                {
                    c.Fail(homeless ? "the player has no home: the game's no-home mood is still on" : "the player has a home, but none was wanted");
                }
                yield break;
            }
        }

        /// <summary>The "call" step (SelfTestKinds has its words).</summary>
        internal sealed class CallStep : SelfTestStep
        {
            public override string Name => "call";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestReflect.Call(c.Arg("type"), c.Arg("method"), c.Arg("args"), out string result, out string why))
                {
                    c.Fail(why);
                    yield break;
                }
                if (c.Arg("as") != null)
                {
                    c.Runner.SetVar(c.Arg("as"), result);
                }
                yield return new WaitForSecondsRealtime(c.Number("settle", 1f));
                if (c.Arg("expect") != null && (result ?? string.Empty).IndexOf(c.Arg("expect"), StringComparison.OrdinalIgnoreCase) < 0)
                {
                    c.Fail(c.Arg("type") + "." + c.Arg("method") + " returned '" + result + "', not '" + c.Arg("expect") + "'");
                }
                else
                {
                    c.Pass(c.Arg("type") + "." + c.Arg("method") + " called" + (string.IsNullOrEmpty(result) ? string.Empty : ", returned '" + result + "'"));
                }
            }
        }

        /// <summary>The "read" step (SelfTestKinds has its words).</summary>
        internal sealed class ReadStep : SelfTestStep
        {
            public override string Name => "read";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestReflect.Read(c.Arg("type"), c.Arg("member"), out string value, out string why))
                {
                    c.Fail(why);
                    yield break;
                }
                if (c.Arg("as") != null)
                {
                    c.Runner.SetVar(c.Arg("as"), value);
                }
                bool ok = true;
                if (c.Arg("expect") != null)
                {
                    ok &= value.IndexOf(c.Arg("expect"), StringComparison.OrdinalIgnoreCase) >= 0;
                }
                if (c.Spec.Args.ContainsKey("atleast"))
                {
                    string digits = value.StartsWith("count ", StringComparison.Ordinal) ? value.Substring(6) : value;
                    ok &= double.TryParse(digits, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double n) && n >= c.Number("atleast", 0f);
                }
                string words = c.Arg("type") + "." + c.Arg("member") + " is '" + value + "'";
                if (ok)
                {
                    c.Pass(words);
                }
                else
                {
                    c.Fail(words + (c.Arg("expect") != null ? ", not '" + c.Arg("expect") + "'" : string.Empty) + (c.Spec.Args.ContainsKey("atleast") ? ", under " + c.Arg("atleast") : string.Empty));
                }
            }
        }
    }
}
