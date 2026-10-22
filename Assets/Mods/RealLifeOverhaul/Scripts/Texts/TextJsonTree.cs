using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// A whole JSON text read into plain objects (Dictionary&lt;string, object&gt;, List&lt;object&gt;, string, double,
    /// bool, null) for an AI service's answers, which nest the words too deep for <see cref="TextJson.Field"/>. Never
    /// throws: a text that is not JSON gives null. Game-free.
    /// COST: one pass over an answer of a few kilobytes, once a reply; nothing per frame.
    /// </summary>
    public static class TextJsonTree
    {
        public const int Deepest = 32;   // levels of nesting read at most: deeper is no answer of a service

        /// <summary>The value of the whole text, or null if it is not JSON.</summary>
        public static object Read(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }
            int i = 0;
            try
            {
                object value = Value(json, ref i, 0);
                Space(json, ref i);
                return i == json.Length ? value : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>A value by its path from the root, such as "choices[0].message.content"; null if any step is missing.</summary>
        public static object At(object root, string path)
        {
            object at = root;
            int i = 0;
            while (at != null && i < path.Length)
            {
                if (path[i] == '.')
                {
                    i++;
                    continue;
                }
                if (path[i] == '[')
                {
                    int close = path.IndexOf(']', i);
                    if (close < 0 || !int.TryParse(path.Substring(i + 1, close - i - 1), NumberStyles.None, CultureInfo.InvariantCulture, out int index))
                    {
                        return null;
                    }
                    at = at is List<object> list && index < list.Count ? list[index] : null;
                    i = close + 1;
                    continue;
                }
                int end = i;
                while (end < path.Length && path[end] != '.' && path[end] != '[')
                {
                    end++;
                }
                string name = path.Substring(i, end - i);
                at = at is Dictionary<string, object> map && map.TryGetValue(name, out object v) ? v : null;
                i = end;
            }
            return at;
        }

        /// <summary>The string at a path; null if it is missing or not a string.</summary>
        public static string Text(object root, string path) => At(root, path) as string;

        /// <summary>The number at a path; NaN if it is missing or not a number.</summary>
        public static double Number(object root, string path) => At(root, path) is double d ? d : double.NaN;

        private static object Value(string s, ref int i, int depth)
        {
            if (depth > Deepest)
            {
                throw new FormatException("too deep");
            }
            Space(s, ref i);
            if (i >= s.Length)
            {
                throw new FormatException("ended");
            }
            char c = s[i];
            switch (c)
            {
                case '{': return Object(s, ref i, depth);
                case '[': return Array(s, ref i, depth);
                case '"': return Str(s, ref i);
                case 't': Word(s, ref i, "true"); return true;
                case 'f': Word(s, ref i, "false"); return false;
                case 'n': Word(s, ref i, "null"); return null;
                default: return Num(s, ref i);
            }
        }

        private static Dictionary<string, object> Object(string s, ref int i, int depth)
        {
            var map = new Dictionary<string, object>(8, StringComparer.Ordinal);
            i++;
            Space(s, ref i);
            if (i < s.Length && s[i] == '}')
            {
                i++;
                return map;
            }
            while (true)
            {
                Space(s, ref i);
                if (i >= s.Length || s[i] != '"')
                {
                    throw new FormatException("a name");
                }
                string name = Str(s, ref i);
                Space(s, ref i);
                if (i >= s.Length || s[i] != ':')
                {
                    throw new FormatException("a colon");
                }
                i++;
                map[name] = Value(s, ref i, depth + 1);
                Space(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    continue;
                }
                if (i < s.Length && s[i] == '}')
                {
                    i++;
                    return map;
                }
                throw new FormatException("an object's end");
            }
        }

        private static List<object> Array(string s, ref int i, int depth)
        {
            var list = new List<object>(4);
            i++;
            Space(s, ref i);
            if (i < s.Length && s[i] == ']')
            {
                i++;
                return list;
            }
            while (true)
            {
                list.Add(Value(s, ref i, depth + 1));
                Space(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    continue;
                }
                if (i < s.Length && s[i] == ']')
                {
                    i++;
                    return list;
                }
                throw new FormatException("an array's end");
            }
        }

        private static string Str(string s, ref int i)
        {
            var t = new StringBuilder(32);
            for (i++; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"')
                {
                    i++;
                    return t.ToString();
                }
                if (c != '\\')
                {
                    t.Append(c);
                    continue;
                }
                if (++i >= s.Length)
                {
                    break;
                }
                switch (s[i])
                {
                    case 'n': t.Append('\n'); break;
                    case 'r': t.Append('\r'); break;
                    case 't': t.Append('\t'); break;
                    case 'b': t.Append('\b'); break;
                    case 'f': t.Append('\f'); break;
                    case 'u':
                        if (i + 4 >= s.Length || !int.TryParse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                        {
                            throw new FormatException("an escape");
                        }
                        t.Append((char)code);   // a surrogate pair comes as two escapes, each appended as it is
                        i += 4;
                        break;
                    default: t.Append(s[i]); break;   // \" \\ \/
                }
            }
            throw new FormatException("a string's end");
        }

        private static double Num(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E'))
            {
                i++;
            }
            if (i == start || !double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                throw new FormatException("a number");
            }
            return v;
        }

        private static void Word(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
            {
                throw new FormatException(word);
            }
            i += word.Length;
        }

        private static void Space(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\n' || s[i] == '\r' || s[i] == '\t'))
            {
                i++;
            }
        }
    }
}
