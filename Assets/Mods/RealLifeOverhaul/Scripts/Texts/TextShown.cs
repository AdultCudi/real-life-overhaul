using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>What was shown for a line the game had for RLO Texts: the model's words, or the written line and why.</summary>
    public enum TextShownWhy
    {
        Model,      // the model's words
        NoPack,     // no pack installed, or none found yet
        Off,        // RLO Texts, or this kind of message, switched off
        Loading,    // the model was still starting
        Busy,       // the queue was full, or the game saved while the words were coming
        Slow,       // the words would not have come in time, or did not
        Refused,    // the words came and the checker refused them
        NoCard,     // the pack has no card for it, or a value the card needs was missing
        Language,   // the pack does not write the game's language
        Unwanted,   // asked again, or no longer wanted when the words came
        Failed      // the pack could not run, or the model gave nothing
    }

    /// <summary>
    /// Every line the game had for RLO Texts, counted by pack and intent: the model's words shown against the written lines,
    /// and why each written line was shown. Kept for the session, never saved. Read by the hourly line in Player.log and by
    /// the test that shows it. Game-free. COST: two dictionary reads and an increment a line; a readout built when asked.
    /// </summary>
    public sealed class TextShown
    {
        public const int RowsMost = 800;   // pairs of pack and intent counted at most; past it a new pair is counted under "other"

        private static readonly int Whys = Enum.GetValues(typeof(TextShownWhy)).Length;
        private static readonly int Verdicts = Enum.GetValues(typeof(TextVerdict)).Length;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public sealed class Row
        {
            public string Pack, Intent;
            public readonly int[] Why = new int[Whys];
            public readonly int[] Rule = new int[Verdicts];

            public int Written
            {
                get
                {
                    int n = 0;
                    for (int i = 1; i < Why.Length; i++)
                    {
                        n += Why[i];
                    }
                    return n;
                }
            }
        }

        private readonly Dictionary<string, Dictionary<string, Row>> _byPack = new Dictionary<string, Dictionary<string, Row>>(4, StringComparer.Ordinal);
        private readonly List<Row> _rows = new List<Row>(64);
        private int _since;

        public IReadOnlyList<Row> Rows => _rows;

        /// <summary>Lines counted since <see cref="Line"/> last took them; the hourly line is written only when there are some.</summary>
        public int Since => _since;

        /// <summary>Counts one line of an intent under a pack: what was shown, and for a refusal by which rule.</summary>
        public void Count(string pack, string intent, TextShownWhy why, TextVerdict rule = TextVerdict.Ok)
        {
            Row row = RowOf(pack ?? "no pack", intent ?? "?");
            row.Why[(int)why]++;
            if (why == TextShownWhy.Refused)
            {
                row.Rule[(int)rule]++;
            }
            _since++;
        }

        private Row RowOf(string pack, string intent)
        {
            if (!_byPack.TryGetValue(pack, out Dictionary<string, Row> intents))
            {
                intents = new Dictionary<string, Row>(64, StringComparer.Ordinal);
                _byPack[pack] = intents;
            }
            if (intents.TryGetValue(intent, out Row row))
            {
                return row;
            }
            if (_rows.Count >= RowsMost)
            {
                intent = "other";
                if (intents.TryGetValue(intent, out row))
                {
                    return row;
                }
            }
            row = new Row { Pack = pack, Intent = intent };
            intents[intent] = row;
            _rows.Add(row);
            return row;
        }

        public void Clear()
        {
            _byPack.Clear();
            _rows.Clear();
            _since = 0;
        }

        /// <summary>A pack's counts by what was shown into <paramref name="why"/>, and by rule into <paramref name="rule"/>; the lines in all.</summary>
        public int Totals(string pack, int[] why, int[] rule)
        {
            Array.Clear(why, 0, why.Length);
            Array.Clear(rule, 0, rule.Length);
            int lines = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                Row r = _rows[i];
                if (r.Pack != pack)
                {
                    continue;
                }
                for (int k = 0; k < Whys && k < why.Length; k++)
                {
                    why[k] += r.Why[k];
                    lines += r.Why[k];
                }
                for (int k = 0; k < Verdicts && k < rule.Length; k++)
                {
                    rule[k] += r.Rule[k];
                }
            }
            return lines;
        }

        /// <summary>The packs counted, in the order first counted.</summary>
        public List<string> Packs()
        {
            var packs = new List<string>(4);
            for (int i = 0; i < _rows.Count; i++)
            {
                if (!packs.Contains(_rows[i].Pack))
                {
                    packs.Add(_rows[i].Pack);
                }
            }
            return packs;
        }

        /// <summary>A reason's name as the log and the readout write it.</summary>
        public static string Name(TextShownWhy why)
        {
            switch (why)
            {
                case TextShownWhy.Model: return "the model";
                case TextShownWhy.NoPack: return "no pack";
                case TextShownWhy.Off: return "off";
                case TextShownWhy.Loading: return "loading";
                case TextShownWhy.Busy: return "busy";
                case TextShownWhy.Slow: return "slow";
                case TextShownWhy.Refused: return "refused";
                case TextShownWhy.NoCard: return "no card";
                case TextShownWhy.Language: return "language";
                case TextShownWhy.Unwanted: return "unwanted";
                default: return "failed";
            }
        }

        /// <summary>The counts of every pack as one line for Player.log; null with nothing counted since the last.</summary>
        public string Line(string when)
        {
            if (_since == 0)
            {
                return null;
            }
            _since = 0;
            var b = new StringBuilder(512);
            b.Append("RLO Texts check (what was shown").Append(when != null ? ", " + when : string.Empty).Append("): ");
            var why = new int[Whys];
            var rule = new int[Verdicts];
            List<string> packs = Packs();
            for (int p = 0; p < packs.Count; p++)
            {
                Totals(packs[p], why, rule);
                b.Append(p > 0 ? "; " : string.Empty).Append(packs[p]).Append(": the model ").Append(why[0].ToString(Inv)).Append(", written ")
                    .Append(Sum(why, 1).ToString(Inv));
                Reasons(why, rule, b, "; ", " ");
                b.Append(", by intent:");
                int shown = 0;
                for (int i = 0; i < _rows.Count; i++)
                {
                    Row r = _rows[i];
                    if (r.Pack != packs[p])
                    {
                        continue;
                    }
                    b.Append(shown++ > 0 ? "," : string.Empty).Append(' ').Append(r.Intent).Append(' ').Append(r.Why[0].ToString(Inv)).Append('/').Append(r.Written.ToString(Inv));
                }
            }
            return b.Append('.').ToString();
        }

        /// <summary>The written line's reasons, as " (loading 3; slow 5; refused 2: Long 1, Number 1)"; nothing with none.</summary>
        public static void Reasons(int[] why, int[] rule, StringBuilder b, string between, string count)
        {
            bool any = false;
            for (int k = 1; k < why.Length; k++)
            {
                if (why[k] == 0)
                {
                    continue;
                }
                b.Append(any ? between : " (").Append(Name((TextShownWhy)k)).Append(count).Append(why[k].ToString(Inv));
                any = true;
                if (k == (int)TextShownWhy.Refused)
                {
                    bool first = true;
                    for (int v = 1; v < rule.Length; v++)
                    {
                        if (rule[v] > 0)
                        {
                            b.Append(first ? ": " : ", ").Append(((TextVerdict)v).ToString()).Append(' ').Append(rule[v].ToString(Inv));
                            first = false;
                        }
                    }
                }
            }
            if (any)
            {
                b.Append(')');
            }
        }

        private static int Sum(int[] values, int from)
        {
            int n = 0;
            for (int i = from; i < values.Length; i++)
            {
                n += values[i];
            }
            return n;
        }
    }
}
