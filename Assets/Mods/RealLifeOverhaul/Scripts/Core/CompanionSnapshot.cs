using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The companion wire format, independent of the game and of the internal save stores. Only explicit captures
    /// allocate. Capturing invokes read-only providers once, in key order; a failure is isolated to one provider.
    /// </summary>
    public static class CompanionSnapshot
    {
        public const string Key = "reallife:companion";
        public const int SchemaVersion = 1;            // the envelope's version; a reader that does not know it reads nothing
        public const int MaxBytes = 4 * 1024 * 1024;   // bytes of UTF-8, of the whole report: a save never grows past it
        public const int MaxRows = 10000;              // entries in one array, more than any city has businesses or staff

        /// <summary>A JSON object from name and value pairs, in that order: name, value, name, value.</summary>
        public static Dictionary<string, object> Row(params object[] pairs)
        {
            var row = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < pairs.Length; i += 2)
            {
                row.Add((string)pairs[i], pairs[i + 1]);
            }
            return row;
        }

        /// <summary>The whole report as JSON. A reader that throws or gives what JSON cannot hold is "failed" alone.</summary>
        public static string Capture(string version, Dictionary<string, object> captured,
            IDictionary<string, Func<CompanionModule>> readers, Action<string, Exception> failed = null)
        {
            return Render(Collect(version, captured, readers, failed));
        }

        /// <summary>
        /// A report whose readers have run (on the caller's thread: they read the game) and whose text is not written yet.
        /// What it holds is plain values nothing changes any more, so <see cref="Render"/> may run on another thread.
        /// </summary>
        public sealed class Collected
        {
            internal string Version;
            internal Dictionary<string, object> Captured;
            internal readonly List<string> Keys = new List<string>(8);
            internal readonly List<CompanionModule> Modules = new List<CompanionModule>(8);   // null: the reader failed
            internal readonly List<KeyValuePair<string, Exception>> Failures = new List<KeyValuePair<string, Exception>>(2);
            internal readonly List<long> Ticks = new List<long>(8);   // what each reader took

            /// <summary>The readers that took the most, longest first, for the save's diagnostic line: "name 12.3 ms, name 4.5 ms".</summary>
            public string Slowest(int count)
            {
                var order = new List<int>(Keys.Count);
                for (int i = 0; i < Keys.Count; i++)
                {
                    order.Add(i);
                }
                order.Sort((a, b) => Ticks[b].CompareTo(Ticks[a]));
                var text = new StringBuilder(96);
                for (int k = 0; k < order.Count && k < count; k++)
                {
                    text.Append(k > 0 ? ", " : string.Empty).Append(Keys[order[k]]).Append(' ')
                        .Append((Ticks[order[k]] * 1000.0 / Stopwatch.Frequency).ToString("F1", CultureInfo.InvariantCulture)).Append(" ms");
                }
                return text.ToString();
            }

            /// <summary>The modules that failed, with what they threw; the caller says them (a worker never writes the log).</summary>
            public IReadOnlyList<KeyValuePair<string, Exception>> Failed => Failures;
        }

        /// <summary>Runs the readers once, in key order, and keeps what they gave. A reader that throws or gives an invalid module is "failed" alone.</summary>
        public static Collected Collect(string version, Dictionary<string, object> captured,
            IDictionary<string, Func<CompanionModule>> readers, Action<string, Exception> failed = null)
        {
            var got = new Collected { Version = version, Captured = captured };
            var keys = new List<string>(readers.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                CompanionModule module = null;
                long began = Stopwatch.GetTimestamp();
                try
                {
                    module = readers[key]() ?? new CompanionModule("unavailable");
                    if (module.Version < 1 || (module.Status != "ready" && module.Status != "disabled"
                        && module.Status != "unavailable" && module.Status != "failed")
                        || (module.Status != "ready" && module.Data != null))
                    {
                        throw new ArgumentException("Invalid companion capability");
                    }
                }
                catch (Exception e)
                {
                    module = null;
                    got.Failures.Add(new KeyValuePair<string, Exception>(key, e));
                    Say(failed, key, e);
                }
                got.Keys.Add(key);
                got.Modules.Add(module);
                got.Ticks.Add(Stopwatch.GetTimestamp() - began);
            }
            return got;
        }

        /// <summary>
        /// The report's text. Each module is written once (its text kept and set into the report as it is, not written again), at
        /// the depth it has in the report so that a module too deep fails alone. Touches nothing of the game: it may run on a worker.
        /// A module that cannot be written is added to <see cref="Collected.Failed"/>.
        /// </summary>
        public static string Render(Collected got, Action<string, Exception> failed = null)
        {
            var modules = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < got.Keys.Count; i++)
            {
                string key = got.Keys[i];
                CompanionModule module = got.Modules[i];
                string text = null;
                if (module != null)
                {
                    try
                    {
                        // The wrapper sits two levels below the root; written there to isolate depth failures too.
                        text = Json(Row("version", module.Version, "status", module.Status, "data", module.Data), 2);
                    }
                    catch (Exception e)
                    {
                        got.Failures.Add(new KeyValuePair<string, Exception>(key, e));
                        Say(failed, key, e);
                    }
                }
                modules.Add(key, new Raw(text ?? Json(Row("version", 1, "status", "failed", "data", null), 2)));
            }
            return Json(Row("schemaVersion", SchemaVersion, "provider", "rlo", "modVersion", got.Version,
                "captured", got.Captured, "modules", modules));
        }

        private static void Say(Action<string, Exception> failed, string key, Exception e)
        {
            try
            {
                failed?.Invoke(key, e);
            }
            catch (Exception)
            {
                // Diagnostics must not discard a usable report.
            }
        }

        // Text already written as JSON, set into a report as it is.
        private sealed class Raw
        {
            public readonly string Text;

            public Raw(string text)
            {
                Text = text;
            }
        }

        // The keys of the object being written, one list for each depth (an object inside an object writes at the next): nothing made for each row.
        [ThreadStatic] private static List<string>[] _keyLists;

        /// <summary>JSON for plain reporting values, with stable object order, finite numbers and bounded output.</summary>
        public static string Json(object value) => Json(value, 0);

        private static string Json(object value, int depth)
        {
            var text = new StringBuilder(1024);
            Write(text, value, depth);
            string result = text.ToString();
            if (Encoding.UTF8.GetByteCount(result) > MaxBytes)
            {
                throw new ArgumentException("Companion capture is too large");
            }
            return result;
        }

        private static void Write(StringBuilder text, object value, int depth)
        {
            if (depth > 16 || text.Length > MaxBytes)
            {
                throw new ArgumentException("Companion capture is too large or deep");
            }
            if (value == null)
            {
                text.Append("null");
            }
            else if (value is string word)
            {
                if (word.Length == 0 || word.Length > 1024)
                {
                    throw new ArgumentException("Companion text is empty or too long");
                }
                text.Append('"');
                foreach (char c in word)
                {
                    if (c == '"' || c == '\\')
                    {
                        text.Append('\\').Append(c);
                    }
                    else if (c < 32 || char.IsSurrogate(c))
                    {
                        text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        text.Append(c);
                    }
                }
                text.Append('"');
            }
            else if (value is bool flag)
            {
                text.Append(flag ? "true" : "false");
            }
            else if (value is Raw raw)
            {
                text.Append(raw.Text);
            }
            else if (value is IDictionary<string, object> row)
            {
                List<string>[] lists = _keyLists ?? (_keyLists = new List<string>[18]);
                List<string> keys = lists[depth] ?? (lists[depth] = new List<string>(16));
                keys.Clear();
                keys.AddRange(row.Keys);
                keys.Sort(StringComparer.Ordinal);
                text.Append('{');
                for (int i = 0; i < keys.Count; i++)
                {
                    if (i > 0)
                    {
                        text.Append(',');
                    }
                    Write(text, keys[i], depth + 1);
                    text.Append(':');
                    Write(text, row[keys[i]], depth + 1);
                }
                text.Append('}');
            }
            else if (value is IEnumerable rows)
            {
                text.Append('[');
                int count = 0;
                foreach (object entry in rows)
                {
                    if (++count > MaxRows)
                    {
                        throw new ArgumentException("Too many companion rows");
                    }
                    if (count > 1)
                    {
                        text.Append(',');
                    }
                    Write(text, entry, depth + 1);
                }
                text.Append(']');
            }
            else if (value is int || value is long)
            {
                long number = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                if (number < -9007199254740991L || number > 9007199254740991L)
                {
                    throw new ArgumentException("Companion integer exceeds JavaScript precision");
                }
                text.Append(number.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                throw new ArgumentException("Unsupported companion value");
            }
        }
    }
}
