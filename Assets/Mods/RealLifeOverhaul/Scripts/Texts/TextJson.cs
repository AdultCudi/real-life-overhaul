using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// The little JSON the runner speaks with llama.cpp's server: the body of a completion request (the prompt as it is,
    /// the sampling the models were tested with) and the "content" read back from the answer, with every escape. No
    /// library. Game-free (EconomyCheck, TextsChecks.cs). COST: one pass over a few hundred characters.
    /// </summary>
    public static class TextJson
    {
        public const int Tokens = 70;               // at most this many tokens a reply (the tests' setting; a reply of 200 characters is about 50)
        public const double Temperature = 0.7;      // the sampling the models were tested with
        public const double TopP = 0.9;

        /// <summary>The body of a POST /completion for this prompt, stopping at the end of the assistant's turn.</summary>
        public static string Request(string prompt, int seed) => Request(prompt, seed, Tokens);

        /// <summary>The same, with a number of tokens of its own (0: read the prompt only, as the cached head is made).</summary>
        public static string Request(string prompt, int seed, int tokens) => Request(prompt, seed, tokens, null, null);

        /// <summary>
        /// A request of data v6: the same body with the reply's grammar (TextDecoding.Grammar) and the tokens it may never
        /// write (TextDecoding.Bias), each left out when there is none, after the fields of every request.
        /// </summary>
        public static string Request(string prompt, int seed, int tokens, string grammar, string bias)
        {
            var t = new StringBuilder(prompt.Length + 200 + (grammar != null ? grammar.Length + 32 : 0));
            t.Append("{\"prompt\":");
            Quote(prompt, t);
            t.Append(",\"n_predict\":").Append(tokens.ToString(CultureInfo.InvariantCulture));
            t.Append(",\"temperature\":").Append(Temperature.ToString("0.0#", CultureInfo.InvariantCulture));
            t.Append(",\"top_p\":").Append(TopP.ToString("0.0#", CultureInfo.InvariantCulture));
            t.Append(",\"seed\":").Append(seed.ToString(CultureInfo.InvariantCulture));
            t.Append(",\"stop\":[\"").Append(TextCard.Stop).Append("\"],\"cache_prompt\":true,\"stream\":false");
            if (!string.IsNullOrEmpty(grammar))
            {
                t.Append(",\"grammar\":");
                Quote(grammar, t);
            }
            if (!string.IsNullOrEmpty(bias))
            {
                t.Append(",\"logit_bias\":").Append(bias);
            }
            t.Append('}');
            return t.ToString();
        }

        /// <summary>The body of a POST /tokenize for a piece of text: no special tokens added, none read in the text.</summary>
        public static string Tokenize(string text)
        {
            var t = new StringBuilder(text.Length + 64);
            t.Append("{\"content\":");
            Quote(text, t);
            t.Append(",\"add_special\":false,\"parse_special\":false}");
            return t.ToString();
        }

        /// <summary>The first token of a /tokenize answer ({"tokens":[12,34]}); -1 when there is none.</summary>
        public static int FirstToken(string json)
        {
            if (json == null)
            {
                return -1;
            }
            int at = json.IndexOf("\"tokens\"", System.StringComparison.Ordinal);
            if (at < 0)
            {
                return -1;
            }
            int open = json.IndexOf('[', at);
            if (open < 0)
            {
                return -1;
            }
            int i = open + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i]))
            {
                i++;
            }
            int start = i;
            while (i < json.Length && char.IsDigit(json[i]))
            {
                i++;
            }
            return i > start && int.TryParse(json.Substring(start, i - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out int token) ? token : -1;
        }

        /// <summary>True when the server's answer says the reply stopped at its most tokens ("stop_type":"limit"): it was cut off.</summary>
        public static bool Cut(string json) => Field(json, "stop_type") == "limit";

        /// <summary>A JSON string of the text, quotes included.</summary>
        public static void Quote(string text, StringBuilder t)
        {
            t.Append('"');
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case '"': t.Append("\\\""); break;
                    case '\\': t.Append("\\\\"); break;
                    case '\n': t.Append("\\n"); break;
                    case '\r': t.Append("\\r"); break;
                    case '\t': t.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            t.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            t.Append(c);
                        }
                        break;
                }
            }
            t.Append('"');
        }

        /// <summary>The string value of a top-level field ("content"), unescaped; null if it is not there or not a string.</summary>
        public static string Field(string json, string name)
        {
            if (json == null)
            {
                return null;
            }
            string key = "\"" + name + "\"";
            int at = json.IndexOf(key, System.StringComparison.Ordinal);
            while (at >= 0)
            {
                int i = at + key.Length;
                while (i < json.Length && char.IsWhiteSpace(json[i]))
                {
                    i++;
                }
                if (i < json.Length && json[i] == ':')
                {
                    i++;
                    while (i < json.Length && char.IsWhiteSpace(json[i]))
                    {
                        i++;
                    }
                    return i < json.Length && json[i] == '"' ? Unquote(json, i) : null;
                }
                at = json.IndexOf(key, at + 1, System.StringComparison.Ordinal);
            }
            return null;
        }

        /// <summary>The number of a field anywhere in the answer ("prompt_per_token_ms" inside "timings"); NaN if it is not there.</summary>
        public static double Number(string json, string name)
        {
            if (json == null)
            {
                return double.NaN;
            }
            string key = "\"" + name + "\"";
            int at = json.IndexOf(key, System.StringComparison.Ordinal);
            if (at < 0)
            {
                return double.NaN;
            }
            int i = at + key.Length;
            while (i < json.Length && (char.IsWhiteSpace(json[i]) || json[i] == ':'))
            {
                i++;
            }
            int start = i;
            while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '.' || json[i] == '-' || json[i] == 'e' || json[i] == 'E' || json[i] == '+'))
            {
                i++;
            }
            return i > start && double.TryParse(json.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : double.NaN;
        }

        // The string starting at the quote at i.
        private static string Unquote(string json, int i)
        {
            var t = new StringBuilder(128);
            for (i++; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    return t.ToString();
                }
                if (c != '\\')
                {
                    t.Append(c);
                    continue;
                }
                if (++i >= json.Length)
                {
                    return null;
                }
                switch (json[i])
                {
                    case 'n': t.Append('\n'); break;
                    case 'r': t.Append('\r'); break;
                    case 't': t.Append('\t'); break;
                    case 'b': t.Append('\b'); break;
                    case 'f': t.Append('\f'); break;
                    case 'u':
                        if (i + 4 >= json.Length || !int.TryParse(json.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                        {
                            return null;
                        }
                        t.Append((char)code);   // (a pair of surrogates comes as two escapes, each appended as it is)
                        i += 4;
                        break;
                    default: t.Append(json[i]); break;   // \" \\ \/
                }
            }
            return null;
        }
    }
}
