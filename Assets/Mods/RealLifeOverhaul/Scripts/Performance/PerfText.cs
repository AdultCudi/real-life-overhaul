using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Small helpers for the performance reports, used only while one is written.</summary>
    internal static class PerfText
    {
        public static string F1(float value) => value.ToString("F1", CultureInfo.InvariantCulture);
        public static string F2(float value) => value.ToString("F2", CultureInfo.InvariantCulture);
        public static string F2(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
        public static string Ms(float value) => value < 0f ? "n/a" : F2(value);

        /// <summary>"Lamp_03 (12)(Clone)" and "Lamp_07" count as the same thing.</summary>
        public static string Family(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "(unnamed)";
            }
            int end = name.Length;
            bool changed = true;
            while (changed && end > 0)
            {
                changed = false;
                if (end >= 7 && string.CompareOrdinal(name, end - 7, "(Clone)", 0, 7) == 0)
                {
                    end -= 7;
                    changed = true;
                }
                while (end > 0 && (char.IsDigit(name[end - 1]) || name[end - 1] == ' ' || name[end - 1] == '_' || name[end - 1] == '-' || name[end - 1] == '.'))
                {
                    end--;
                    changed = true;
                }
                if (end > 0 && name[end - 1] == ')')
                {
                    int open = name.LastIndexOf('(', end - 1);
                    bool digits = open >= 0 && open < end - 2;
                    for (int i = open + 1; digits && i < end - 1; i++)
                    {
                        digits = char.IsDigit(name[i]);
                    }
                    if (digits)
                    {
                        end = open;
                        changed = true;
                    }
                }
            }
            return end > 0 ? name.Substring(0, end) : name;
        }

        /// <summary>The slash-separated names of up to <paramref name="levels"/> parents above this one.</summary>
        public static string Above(Transform transform, int levels)
        {
            string path = string.Empty;
            Transform t = transform.parent;
            for (int i = 0; i < levels && t != null; i++, t = t.parent)
            {
                path = path.Length == 0 ? Family(t.name) : Family(t.name) + "/" + path;
            }
            return path.Length == 0 ? "(root)" : path;
        }

        /// <summary>Counts by a name and writes the largest groups first.</summary>
        public sealed class Tally
        {
            private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
            private readonly Dictionary<string, float> _weights = new Dictionary<string, float>();

            public int Total { get; private set; }

            public void Add(string key, float weight = 0f)
            {
                _counts.TryGetValue(key, out int count);
                _counts[key] = count + 1;
                if (weight != 0f)
                {
                    _weights.TryGetValue(key, out float sum);
                    _weights[key] = sum + weight;
                }
                Total++;
            }

            public void Write(StringBuilder text, int most, string indent, string weightLabel = null)
            {
                var rows = new List<KeyValuePair<string, int>>(_counts);
                rows.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));
                for (int i = 0; i < rows.Count && i < most; i++)
                {
                    text.Append(indent).Append(rows[i].Value.ToString().PadLeft(6)).Append("  ").Append(rows[i].Key);
                    if (weightLabel != null && _weights.TryGetValue(rows[i].Key, out float sum))
                    {
                        text.Append("  (").Append(weightLabel).Append(' ').Append(F1(sum / rows[i].Value)).Append(" on average)");
                    }
                    text.AppendLine();
                }
                if (rows.Count > most)
                {
                    text.Append(indent).Append("   ...  and ").Append(rows.Count - most).AppendLine(" smaller groups");
                }
            }
        }

        /// <summary>Counts values into fixed ranges: "0-25: 12  25-50: 40 ...".</summary>
        public sealed class Ranges
        {
            private readonly float[] _edges;
            private readonly int[] _counts;

            public Ranges(params float[] edges)
            {
                _edges = edges;
                _counts = new int[edges.Length + 1];
            }

            public void Add(float value)
            {
                int i = 0;
                while (i < _edges.Length && value >= _edges[i])
                {
                    i++;
                }
                _counts[i]++;
            }

            public override string ToString()
            {
                var text = new StringBuilder();
                for (int i = 0; i < _counts.Length; i++)
                {
                    string from = i == 0 ? "0" : _edges[i - 1].ToString(CultureInfo.InvariantCulture);
                    string to = i < _edges.Length ? "-" + _edges[i].ToString(CultureInfo.InvariantCulture) : "+";
                    text.Append(from).Append(to).Append(": ").Append(_counts[i]).Append("   ");
                }
                return text.ToString();
            }
        }

        /// <summary>Writes a settings object's public fields as "name=value", nested ones indented, exactly as the game has them.</summary>
        public static void Dump(StringBuilder text, object settings, string indent, int depth)
        {
            if (settings == null)
            {
                text.Append(indent).AppendLine("(none)");
                return;
            }
            var line = new StringBuilder();
            var nested = new List<FieldInfo>();
            foreach (FieldInfo field in settings.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object value;
                try
                {
                    value = field.GetValue(settings);
                }
                catch (Exception)
                {
                    continue;
                }
                string simple = Simple(value);
                if (simple != null)
                {
                    line.Append(field.Name).Append('=').Append(simple).Append("  ");
                    if (line.Length > 150)
                    {
                        text.Append(indent).AppendLine(line.ToString());
                        line.Length = 0;
                    }
                }
                else if (depth > 0 && value != null)
                {
                    nested.Add(field);
                }
            }
            if (line.Length > 0)
            {
                text.Append(indent).AppendLine(line.ToString());
            }
            foreach (FieldInfo field in nested)
            {
                text.Append(indent).Append(field.Name).AppendLine(":");
                Dump(text, field.GetValue(settings), indent + "  ", depth - 1);
            }
        }

        // A value that fits on a line, or null for something with fields of its own.
        private static string Simple(object value)
        {
            if (value == null)
            {
                return "null";
            }
            if (value is string s)
            {
                return "'" + s + "'";
            }
            if (value is float f)
            {
                return f.ToString("0.###", CultureInfo.InvariantCulture);
            }
            if (value is UnityEngine.Object unityObject)
            {
                return unityObject != null ? "'" + unityObject.name + "'" : "null";
            }
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            if (value is Vector2 || value is Vector3 || value is Vector4 || value is Color || value is LayerMask)
            {
                return value.ToString();
            }
            if (value is IList list)
            {
                if (list.Count > 16)
                {
                    return "[" + list.Count + " entries]";
                }
                var joined = new StringBuilder("[");
                for (int i = 0; i < list.Count; i++)
                {
                    string item = Simple(list[i]);
                    if (item == null)
                    {
                        return "[" + list.Count + " x " + list[i].GetType().Name + "]";
                    }
                    joined.Append(i > 0 ? "," : string.Empty).Append(item);
                }
                return joined.Append(']').ToString();
            }
            return null;
        }
    }
}
