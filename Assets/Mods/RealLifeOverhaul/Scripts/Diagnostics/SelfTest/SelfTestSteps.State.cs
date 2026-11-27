using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>The step table's state part: asserts on what only the game's state holds (money, an owned building, the bank's lines, the notices), and two staging steps.</summary>
    internal static partial class SelfTestSteps
    {
        /// <summary>The "assert_money" step (SelfTestKinds has its words).</summary>
        internal sealed class AssertMoneyStep : SelfTestStep
        {
            public override string Name => "assert_money";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (SaveGameManager.Current == null)
                {
                    c.Fail("no game is loaded");
                    yield break;
                }
                string from = c.Arg("from");
                if (from != null && !c.Runner.Watch.Knows(from))
                {
                    c.Fail("no step labelled '" + from + "' has run");
                    yield break;
                }
                if (!c.Spec.Args.ContainsKey("atleast") && !c.Spec.Args.ContainsKey("atmost") && !c.Spec.Args.ContainsKey("changed_by"))
                {
                    c.Fail("name atleast, atmost or changed_by");
                    yield break;
                }
                float now = SelfTestGame.MoneyNow();
                float then = c.Runner.Watch.MoneyAt(from);
                var said = new List<string>(3);
                if (c.Spec.Args.ContainsKey("atleast") && now < c.Number("atleast", 0f))
                {
                    said.Add("is " + (long)now + ", under " + (long)c.Number("atleast", 0f));
                }
                if (c.Spec.Args.ContainsKey("atmost") && now > c.Number("atmost", 0f))
                {
                    said.Add("is " + (long)now + ", over " + (long)c.Number("atmost", 0f));
                }
                if (c.Spec.Args.ContainsKey("changed_by") && Math.Abs((now - then) - c.Number("changed_by", 0f)) > c.Number("within", 1f))
                {
                    said.Add("moved by " + (long)(now - then) + ", not " + (long)c.Number("changed_by", 0f) + " (within " + c.Number("within", 1f) + ")");
                }
                string words = "money " + (long)now + " (was " + (long)then + " at " + (from ?? "the last action") + ")";
                if (said.Count > 0)
                {
                    c.Fail(words + ": " + string.Join("; ", said));
                }
                else
                {
                    c.Pass(words);
                }
            }
        }

        /// <summary>The "assert_owns" step (SelfTestKinds has its words).</summary>
        internal sealed class AssertOwnsStep : SelfTestStep
        {
            public override string Name => "assert_owns";

            public override IEnumerator Run(SelfTestContext c)
            {
                string address = c.Arg("address");
                if (!SelfTestWorld.ParseAddress(address, out int number, out string street))
                {
                    c.Fail("'" + address + "' is not an address (a number and a street: 12 5th Avenue)");
                    yield break;
                }
                bool owned = SelfTestWorld.Owns(number, street, c.Arg("business"), out string words);
                bool absent = c.Flag("absent", false);
                if (owned != absent)
                {
                    c.Pass(words);
                }
                else
                {
                    c.Fail(words + (absent ? " (it should not be the player's)" : " (it should be the player's)"));
                }
            }
        }

        /// <summary>The "assert_bank_line" step (SelfTestKinds has its words).</summary>
        internal sealed class AssertBankLineStep : SelfTestStep
        {
            public override string Name => "assert_bank_line";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (SaveGameManager.Current == null)
                {
                    c.Fail("no game is loaded");
                    yield break;
                }
                string key = c.Arg("key"), from = c.Arg("from");
                if (from != null && !c.Runner.Watch.Knows(from))
                {
                    c.Fail("no step labelled '" + from + "' has run");
                    yield break;
                }
                float since = c.Runner.Watch.MinutesAt(from) - 0.5f;
                int found = 0;
                float sum = 0f;
                foreach (Transaction t in SaveGameManager.Current.Transactions)
                {
                    if (t != null && t.transactionType != null && t.transactionType.IndexOf(key, StringComparison.Ordinal) >= 0 && t.timestamp != null && t.timestamp.GetTotalMinutes() >= since)
                    {
                        found++;
                        sum += t.amount;
                    }
                }
                int exactly = c.Spec.Args.ContainsKey("count") ? (int)c.Number("count", 1f) : -1;
                bool absent = c.Flag("absent", false);
                bool ok = absent ? found == 0 : exactly >= 0 ? found == exactly : found >= 1;
                string words = found + " line(s) of '" + key + "' (" + (long)sum + " in all) since " + (from ?? "the last action");
                if (ok)
                {
                    c.Pass(words);
                }
                else
                {
                    c.Fail(words + (absent ? ", none wanted" : exactly >= 0 ? ", " + exactly + " wanted" : ", at least one wanted"));
                }
                yield break;
            }
        }

        /// <summary>The "assert_notice" step (SelfTestKinds has its words).</summary>
        internal sealed class AssertNoticeStep : SelfTestStep
        {
            public override string Name => "assert_notice";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (SaveGameManager.Current == null)
                {
                    c.Fail("no game is loaded");
                    yield break;
                }
                string key = c.Arg("key"), from = c.Arg("from");
                if (from != null && !c.Runner.Watch.Knows(from))
                {
                    c.Fail("no step labelled '" + from + "' has run");
                    yield break;
                }
                float since = c.Runner.Watch.MinutesAt(from) - 0.5f;
                bool absent = c.Flag("absent", false);
                float until = Time.realtimeSinceStartup + (absent ? 0f : c.Number("wait", 3f));
                while (true)
                {
                    int found = 0;
                    foreach (string shown in c.Runner.Watch.NoticeLines(from))
                    {
                        found += shown.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0;
                    }
                    foreach (Notification n in SaveGameManager.Current.notifications)
                    {
                        if (n != null && n.key != null && n.key.IndexOf(key, StringComparison.Ordinal) >= 0 && n.date != null && n.date.GetTotalMinutes() >= since)
                        {
                            found++;
                        }
                    }
                    if (found > 0 && !absent)
                    {
                        c.Pass(found + " notice(s) with the key '" + key + "' since " + (from ?? "the last action"));
                        yield break;
                    }
                    if (found > 0)
                    {
                        c.Fail(found + " notice(s) with the key '" + key + "' were shown, none wanted");
                        yield break;
                    }
                    c.Waiting = "a notice with the key '" + key + "'";
                    if (Time.realtimeSinceStartup > until)
                    {
                        if (absent)
                        {
                            c.Pass("no notice with the key '" + key + "' since " + (from ?? "the last action"));
                        }
                        else
                        {
                            c.Fail("no notice with the key '" + key + "' since " + (from ?? "the last action") + " (only notices the game keeps in the save are seen)");
                        }
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.25f);
                }
            }
        }

        /// <summary>The "write_wav" step (SelfTestKinds has its words).</summary>
        internal sealed class WriteWavStep : SelfTestStep
        {
            public override string Name => "write_wav";

            public override IEnumerator Run(SelfTestContext c)
            {
                string rel = c.Arg("path").Replace('\\', '/');
                string root = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "RealLifeOverhaul", "GigMusic"));
                string full = Path.GetFullPath(Path.Combine(root, rel));
                if (rel.Contains("..") || !full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !full.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    c.Fail("the path must be a .wav inside RealLifeOverhaul/GigMusic of the game's data folder (got '" + rel + "')");
                    yield break;
                }
                if (c.Flag("delete", false))
                {
                    bool had = File.Exists(full);
                    if (had)
                    {
                        File.Delete(full);
                    }
                    c.Pass(had ? "deleted " + rel : "there was no " + rel + " to delete");
                }
                else if (c.Flag("bad", false))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(full));
                    File.WriteAllText(full, "this is not a wav file");
                    c.Pass("wrote a file that is not a wav to " + rel);
                }
                else
                {
                    SelfTestWav.Write(full, c.Number("seconds", 1f), c.Number("hz", 440f));
                    c.Pass("wrote a " + c.Number("seconds", 1f) + " s tone of " + c.Number("hz", 440f) + " Hz to " + rel);
                }
                yield break;
            }
        }

        /// <summary>The "set_year" step (SelfTestKinds has its words).</summary>
        internal sealed class SetYearStep : SelfTestStep
        {
            public override string Name => "set_year";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (SaveGameManager.Current == null)
                {
                    c.Fail("no game is loaded");
                    yield break;
                }
                int was = SaveGameManager.Current.gameVariables.daysPerYear;
                SaveGameManager.Current.gameVariables.daysPerYear = (int)c.Number("days", was);
                c.Pass("the year is " + SaveGameManager.Current.gameVariables.daysPerYear + " days (it was " + was + ")");
                yield break;
            }
        }
    }
}
