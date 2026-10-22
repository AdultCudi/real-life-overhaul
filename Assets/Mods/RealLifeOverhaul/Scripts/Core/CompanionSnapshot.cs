using System;
using System.Collections;
using System.Collections.Generic;
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
        public const int SchemaVersion = 1;
        public const int MaxBytes = 4 * 1024 * 1024;
        public const int MaxRows = 10000;

        public static Dictionary<string, object> Row(params object[] pairs)
        {
            var row = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < pairs.Length; i += 2)
            {
                row.Add((string)pairs[i], pairs[i + 1]);
            }
            return row;
        }

        public static string Capture(string version, Dictionary<string, object> captured,
            IDictionary<string, Func<CompanionModule>> readers, Action<string, Exception> failed = null)
        {
            var modules = new Dictionary<string, object>(StringComparer.Ordinal);
            var keys = new List<string>(readers.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                Dictionary<string, object> row;
                try
                {
                    CompanionModule module = readers[key]() ?? new CompanionModule("unavailable");
                    if (module.Version < 1 || (module.Status != "ready" && module.Status != "disabled"
                        && module.Status != "unavailable" && module.Status != "failed")
                        || (module.Status != "ready" && module.Data != null))
                    {
                        throw new ArgumentException("Invalid companion capability");
                    }
                    row = Row("version", module.Version, "status", module.Status, "data", module.Data);
                    // The wrapper sits two levels below the root; validate there to isolate depth failures too.
                    Json(row, 2);
                }
                catch (Exception e)
                {
                    row = Row("version", 1, "status", "failed", "data", null);
                    try
                    {
                        failed?.Invoke(key, e);
                    }
                    catch (Exception)
                    {
                        // Diagnostics must not discard a usable report.
                    }
                }
                modules.Add(key, row);
            }
            return Json(Row("schemaVersion", SchemaVersion, "provider", "rlo", "modVersion", version,
                "captured", captured, "modules", modules));
        }

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
            else if (value is IDictionary<string, object> row)
            {
                var keys = new List<string>(row.Keys);
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
