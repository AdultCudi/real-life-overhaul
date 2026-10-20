using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>One request to a service, as the web client sends it.</summary>
    public sealed class TextWebRequest
    {
        public string Method = "POST";
        public string Url;
        public string Body;                     // null for a GET
        public string ContentType = "application/json";
        public readonly List<KeyValuePair<string, string>> Headers = new List<KeyValuePair<string, string>>(4);
        public Func<string, bool> Lines;        // a streamed answer's lines as they come, on the worker's thread: true once it is whole (not waited for further); null: the body at once

        public TextWebRequest Header(string name, string value)
        {
            Headers.Add(new KeyValuePair<string, string>(name, value));
            return this;
        }
    }

    /// <summary>A service's answer: its status and body, or why there is none (no connection, the time ran out).</summary>
    public sealed class TextWebAnswer
    {
        public int Status;               // 0 when nothing came back
        public string Body;
        public string Failure;           // why nothing came back, for the log (no key or token in it)
        public bool TimedOut;
        public double RetryAfter;        // seconds the service asked to wait (its Retry-After), 0 when it named none
    }

    /// <summary>What went wrong with a request, and so what the runner does next.</summary>
    public enum TextFault
    {
        None,          // words came back
        NoWords,       // an answer with no words in it (a refusal, an empty reply, a stream that never completed)
        TimedOut,      // nothing in the time
        Network,       // no connection: rest, then try again
        RateLimited,   // too many requests (429): rest as long as the service asks
        Busy,          // the service's own fault or overload (5xx): rest a little
        BadRequest,    // the service refused the request's form (400, 422): leave out what it named
        Key,           // the key or the sign-in was refused (401, 403): stop until the player mends it
        Model,         // no such model (404): stop until the player names another
        UsageLimit,    // the ChatGPT plan's allowance for this app is used up: rest long, tell the player once
        NotEligible    // the ChatGPT plan cannot be used in apps (the plan, the workspace or the region): stop
    }

    /// <summary>A request's outcome: the words, or the fault with a short reason fit for the log.</summary>
    public struct TextServiceResult
    {
        public string Words;
        public TextFault Fault;
        public string Why;
        public string Param;             // the request field the service named in a refusal ("temperature"), or null
    }

    /// <summary>
    /// Your own AI's rules, game-free (EconomyCheck, TextsServiceChecks.cs): the request for a card to each kind of
    /// service (the chat API that most speak, Anthropic's Messages API, the ChatGPT plan's streamed Responses API), with
    /// exactly RLO Texts' system turn and card (TextCard.System and TextCard.Card, as the local pack is given them);
    /// reading the words back out of each kind of answer; telling the faults apart and how long to rest after each; the
    /// models a service lists; and taking anything like a key or a token out of a line before it is logged.
    /// COST: a few strings a request, on the runner's worker threads; nothing per frame.
    /// </summary>
    public static class TextServiceRules
    {
        public const int ReplyTokens = 300;          // tokens a reply may take, its thinking included (a reply of 200 characters is about 50)
        public const int ThinkingTokens = 800;       // ...for Anthropic's models, which may think before they answer
        public const double Temperature = 0.7;       // the sampling RLO Texts' packs were tested with
        public const double TimeOut = 6.0;           // seconds a reply may take before it is given up (the line falls back to the pack or the written line)
        public const double ListSeconds = 6.0;       // seconds a service's list of models may take
        public const double RestNetwork = 30.0;      // seconds of rest after no connection, doubled each time up to RestMost
        public const double RestBusy = 10.0;         // ...after a fault of the service's own (5xx), doubled each time
        public const double RestRate = 20.0;         // ...after too many requests, unless the service names its own wait
        public const double RestMost = 300.0;        // the longest rest of the above
        public const double RestUsage = 900.0;       // seconds of rest when the ChatGPT plan's allowance for apps is used up
        public const double RestRefusals = 60.0;     // ...after RefusalsInRow requests refused in a row for their form
        public const int RefusalsInRow = 5;          // refusals in a row before that rest
        public const int TimeOutsInRow = 3;          // time-outs in a row that count as no connection
        public const int WhyMost = 120;              // characters of a service's own reason kept for the log
        public const string AnthropicVersion = "2023-06-01";   // the Messages API's version header
        public const string AppName = "Real Life Overhaul";    // how the mod names itself to a service that asks

        // ---------------------------------------------------------------- the request

        /// <summary>
        /// The request for one card: <paramref name="system"/> and <paramref name="card"/> are RLO Texts' own (TextCard.System,
        /// TextCard.Card); <paramref name="secret"/> the key or the access token (null for a program on this PC).
        /// <paramref name="noReasoning"/> and <paramref name="noTemperature"/> leave out what the service refused before.
        /// </summary>
        public static TextWebRequest Build(TextProviderInfo p, string baseUrl, string model, string system, string card, string secret, bool noReasoning, bool noTemperature)
        {
            var t = new StringBuilder(system.Length + card.Length + 256);
            var r = new TextWebRequest();
            bool reasoning = !noReasoning && p.Reasoning != null && (p.ReasoningFor == null || (model != null && model.IndexOf(p.ReasoningFor, StringComparison.OrdinalIgnoreCase) >= 0));
            switch (p.Api)
            {
                case TextApi.Messages:
                    r.Url = baseUrl + "/messages";
                    t.Append("{\"model\":");
                    TextJson.Quote(model, t);
                    t.Append(",\"max_tokens\":").Append(ThinkingTokens.ToString(CultureInfo.InvariantCulture)).Append(",\"system\":");
                    TextJson.Quote(system, t);
                    t.Append(",\"messages\":[{\"role\":\"user\",\"content\":");
                    TextJson.Quote(card, t);
                    t.Append("}]");
                    if (reasoning)
                    {
                        t.Append(",\"output_config\":{\"effort\":\"").Append(p.Reasoning).Append("\"}");
                    }
                    t.Append('}');
                    if (secret != null)
                    {
                        r.Header("x-api-key", secret);
                    }
                    r.Header("anthropic-version", AnthropicVersion);
                    break;
                case TextApi.Responses:
                    r.Url = baseUrl + "/responses";
                    t.Append("{\"model\":");
                    TextJson.Quote(model, t);
                    t.Append(",\"instructions\":");
                    TextJson.Quote(system, t);
                    t.Append(",\"input\":[{\"role\":\"user\",\"content\":");
                    TextJson.Quote(card, t);
                    t.Append("}],\"store\":false,\"stream\":true");   // (the ChatGPT plan's route: never stored, always streamed)
                    if (reasoning)
                    {
                        t.Append(",\"reasoning\":{\"effort\":\"").Append(p.Reasoning).Append("\"}");
                    }
                    t.Append('}');
                    r.Header("Authorization", "Bearer " + secret).Header("Accept", "text/event-stream");
                    break;
                default:
                    r.Url = baseUrl + "/chat/completions";
                    t.Append("{\"model\":");
                    TextJson.Quote(model, t);
                    t.Append(",\"messages\":[{\"role\":\"system\",\"content\":");
                    TextJson.Quote(system, t);
                    t.Append("},{\"role\":\"user\",\"content\":");
                    TextJson.Quote(card, t);
                    t.Append("}],\"").Append(p.TokensField).Append("\":").Append(ReplyTokens.ToString(CultureInfo.InvariantCulture));
                    if (p.Temperature && !noTemperature)
                    {
                        t.Append(",\"temperature\":").Append(Temperature.ToString("0.0#", CultureInfo.InvariantCulture));
                    }
                    if (reasoning)
                    {
                        t.Append(",\"reasoning_effort\":\"").Append(p.Reasoning).Append('"');
                    }
                    t.Append(",\"stream\":false}");
                    if (secret != null)
                    {
                        r.Header("Authorization", "Bearer " + secret);
                    }
                    if (p.Titled)
                    {
                        r.Header("X-Title", AppName);
                    }
                    break;
            }
            r.Body = t.ToString();
            return r;
        }

        /// <summary>The request for a service's list of models (GET {base}/models).</summary>
        public static TextWebRequest Models(TextProviderInfo p, string baseUrl, string secret)
        {
            var r = new TextWebRequest { Method = "GET", Url = baseUrl + "/models", Body = null };
            if (secret != null)
            {
                if (p.Api == TextApi.Messages)
                {
                    r.Header("x-api-key", secret).Header("anthropic-version", AnthropicVersion);
                }
                else
                {
                    r.Header("Authorization", "Bearer " + secret);
                }
            }
            return r;
        }

        // ---------------------------------------------------------------- the answer

        /// <summary>The words of an answer, or the fault: the kind of answer is the service's API.</summary>
        public static TextServiceResult Read(TextApi api, TextWebAnswer a)
        {
            if (a == null || a.Status == 0)
            {
                return Fail(a != null && a.TimedOut ? TextFault.TimedOut : TextFault.Network, a != null && a.Failure != null ? a.Failure : "no answer");
            }
            if (a.Status < 200 || a.Status > 299)
            {
                return Refused(a);
            }
            string words;
            string why;
            switch (api)
            {
                case TextApi.Messages:
                    words = MessagesWords(TextJsonTree.Read(a.Body), out why);
                    break;
                case TextApi.Responses:
                    TextServiceResult streamed = Stream(a.Body);
                    if (streamed.Fault != TextFault.None)
                    {
                        return streamed;
                    }
                    words = streamed.Words;
                    why = null;
                    break;
                default:
                    words = ChatWords(TextJsonTree.Read(a.Body), out why);
                    break;
            }
            words = Unthought(words);
            if (string.IsNullOrEmpty(words))
            {
                return Fail(TextFault.NoWords, why ?? "no words in the answer");
            }
            return new TextServiceResult { Words = words, Fault = TextFault.None };
        }

        // The chat API: choices[0].message.content, a string (or, from some servers, a list of parts with text).
        private static string ChatWords(object root, out string why)
        {
            why = null;
            if (root == null)
            {
                why = "the answer was not JSON";
                return null;
            }
            object content = TextJsonTree.At(root, "choices[0].message.content");
            if (content is string s)
            {
                if (s.Length == 0 && TextJsonTree.Text(root, "choices[0].finish_reason") == "length")
                {
                    why = "the reply ran out of tokens before it said anything";
                }
                return s;
            }
            if (content is List<object> parts)
            {
                return Joined(parts);
            }
            why = TextJsonTree.At(root, "choices[0].message.refusal") is string refusal ? "the model refused: " + Short(refusal) : "no words in the answer";
            return null;
        }

        // The Messages API: the text of every text block of content, in order; a refusal gives none.
        private static string MessagesWords(object root, out string why)
        {
            why = null;
            if (root == null)
            {
                why = "the answer was not JSON";
                return null;
            }
            if (TextJsonTree.Text(root, "stop_reason") == "refusal")
            {
                why = "the model declined";
                return null;
            }
            return TextJsonTree.At(root, "content") is List<object> blocks ? Joined(blocks) : null;
        }

        private static string Joined(List<object> blocks)
        {
            var t = new StringBuilder(128);
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i] is Dictionary<string, object> b && b.TryGetValue("text", out object text) && text is string s
                    && (!b.TryGetValue("type", out object type) || "text".Equals(type) || "output_text".Equals(type)))
                {
                    t.Append(s);
                }
            }
            return t.ToString();
        }

        /// <summary>
        /// The ChatGPT plan's streamed answer (server-sent events): the text of every response.output_text.delta, and a
        /// success only once the text is whole: its response.output_text.done (the last word: the web client stops
        /// reading there, TextStreamRead) or response.completed (OpenAI: a stream may start and still end in
        /// response.failed, which counts when it comes before either).
        /// </summary>
        public static TextServiceResult Stream(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return Fail(TextFault.NoWords, "an empty stream");
            }
            var words = new StringBuilder(128);
            string done = null;
            bool completed = false, whole = false;
            int at = 0;
            while (at < body.Length)
            {
                int end = body.IndexOf('\n', at);
                if (end < 0)
                {
                    end = body.Length;
                }
                string line = body.Substring(at, end - at).TrimEnd('\r');
                at = end + 1;
                if (!line.StartsWith("data:", StringComparison.Ordinal))
                {
                    continue;
                }
                object e = TextJsonTree.Read(line.Substring(5).Trim());
                string type = TextJsonTree.Text(e, "type");
                switch (type)
                {
                    case "response.output_text.delta":
                        words.Append(TextJsonTree.Text(e, "delta"));
                        break;
                    case "response.output_text.done":
                        done = TextJsonTree.Text(e, "text") ?? done;
                        whole = true;
                        break;
                    case "response.completed":
                        completed = true;
                        break;
                    case "response.failed":
                    case "error":
                        string code = TextJsonTree.Text(e, "response.error.code") ?? TextJsonTree.Text(e, "error.code") ?? TextJsonTree.Text(e, "code");
                        string message = TextJsonTree.Text(e, "response.error.message") ?? TextJsonTree.Text(e, "error.message") ?? TextJsonTree.Text(e, "message");
                        return ByCode(code, 0, message, null);
                    case "response.incomplete":
                        return Fail(TextFault.NoWords, "the reply was cut short (" + (TextJsonTree.Text(e, "response.incomplete_details.reason") ?? "no reason given") + ")");
                }
            }
            if (!completed && !whole)
            {
                return Fail(TextFault.NoWords, "the stream ended before the reply was complete");
            }
            string text = !string.IsNullOrEmpty(done) ? done : words.Length > 0 ? words.ToString() : null;   // (the whole text as the service gave it, else its deltas joined)
            return string.IsNullOrEmpty(text) ? Fail(TextFault.NoWords, "no words in the stream") : new TextServiceResult { Words = text, Fault = TextFault.None };
        }

        // An answer that is not a success: the status and the service's own code tell the fault.
        private static TextServiceResult Refused(TextWebAnswer a)
        {
            object root = TextJsonTree.Read(a.Body);
            if (root is List<object> list && list.Count > 0)
            {
                root = list[0];   // (Google's chat API wraps its error in a list)
            }
            string code = TextJsonTree.Text(root, "error.code") ?? TextJsonTree.Text(root, "error.type") ?? TextJsonTree.Text(root, "error.status");
            string message = TextJsonTree.Text(root, "error.message") ?? TextJsonTree.Text(root, "detail") ?? TextJsonTree.Text(root, "message");
            string param = TextJsonTree.Text(root, "error.param");
            TextServiceResult r = ByCode(code, a.Status, message, param);
            if (r.Fault == TextFault.RateLimited && a.RetryAfter > 0)
            {
                r.Why += " (it asked for " + a.RetryAfter.ToString("0", CultureInfo.InvariantCulture) + " s)";
            }
            return r;
        }

        private static TextServiceResult ByCode(string code, int status, string message, string param)
        {
            string why = "the service said " + (status > 0 ? status.ToString(CultureInfo.InvariantCulture) + " " : string.Empty) + (code ?? string.Empty)
                         + (message != null ? ": " + Short(message) : string.Empty);
            TextFault fault;
            switch (code)
            {
                case "subscription_sharing_usage_limit_exceeded": fault = TextFault.UsageLimit; break;
                case "subscription_sharing_user_not_eligible": fault = TextFault.NotEligible; break;
                case "subscription_sharing_invalid_user": fault = TextFault.Key; break;
                case "subscription_sharing_usage_unavailable":
                case "subscription_sharing_user_unavailable": fault = TextFault.Busy; break;
                case "subscription_sharing_unsupported_capability": fault = TextFault.BadRequest; break;
                case "model_not_found": fault = TextFault.Model; break;
                default:
                    fault = status == 401 || status == 403 ? TextFault.Key
                        : status == 404 ? TextFault.Model
                        : status == 429 ? TextFault.RateLimited
                        : status == 400 || status == 413 || status == 422 ? TextFault.BadRequest
                        : status >= 500 ? TextFault.Busy
                        : status == 0 ? TextFault.NoWords
                        : TextFault.BadRequest;
                    break;
            }
            if (fault == TextFault.BadRequest && message != null && message.IndexOf("API key", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                fault = TextFault.Key;   // (Google answers a key it does not know with 400 INVALID_ARGUMENT, "Please pass a valid API key")
            }
            return new TextServiceResult { Fault = fault, Why = why, Param = param ?? Named(message) };
        }

        // The field a refusal's message names, when the service gives no param of its own: the two the mod may leave out.
        private static string Named(string message)
        {
            if (message == null)
            {
                return null;
            }
            if (message.IndexOf("temperature", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "temperature";
            }
            if (message.IndexOf("reasoning", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("effort", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "reasoning";
            }
            return null;
        }

        private static TextServiceResult Fail(TextFault fault, string why) => new TextServiceResult { Fault = fault, Why = why };

        /// <summary>
        /// A reply with a thinking model's working taken out: everything up to a closing &lt;/think&gt; (some local models write
        /// their thinking into the reply), trimmed.
        /// </summary>
        public static string Unthought(string words)
        {
            if (words == null)
            {
                return null;
            }
            int close = words.LastIndexOf("</think>", StringComparison.OrdinalIgnoreCase);
            if (close >= 0)
            {
                words = words.Substring(close + 8);
            }
            else if (words.TrimStart().StartsWith("<think>", StringComparison.OrdinalIgnoreCase))
            {
                return null;   // (thinking that never ended: no reply in it)
            }
            return words.Trim();
        }

        // ---------------------------------------------------------------- the models a service lists

        /// <summary>
        /// The models of a list: the ChatGPT plan's models[].slug of those shown in a list (visibility "list"), else the chat
        /// API's data[].id, in the service's order. Empty when the answer is not a list.
        /// </summary>
        public static List<string> ModelList(string body)
        {
            var models = new List<string>(16);
            object root = TextJsonTree.Read(body);
            if (TextJsonTree.At(root, "models") is List<object> plan)
            {
                for (int i = 0; i < plan.Count; i++)
                {
                    string slug = TextJsonTree.Text(plan[i], "slug") ?? TextJsonTree.Text(plan[i], "id");
                    string seen = TextJsonTree.Text(plan[i], "visibility");
                    if (slug != null && (seen == null || seen == "list"))
                    {
                        models.Add(slug);
                    }
                }
            }
            if (models.Count == 0 && TextJsonTree.At(root, "data") is List<object> data)   // (llama.cpp's server answers with both lists; Ollama's of another shape)
            {
                for (int i = 0; i < data.Count; i++)
                {
                    string id = TextJsonTree.Text(data[i], "id");
                    if (id != null && id.IndexOf("embed", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        models.Add(id);   // (an embedding model a local program lists cannot answer)
                    }
                }
            }
            return models;
        }

        // ---------------------------------------------------------------- rest after a fault

        /// <summary>
        /// Seconds to rest after a fault, the <paramref name="times"/>-th in a row of its kind (1 the first): doubled each
        /// time up to <see cref="RestMost"/>; a rate limit as long as the service asked when it asked longer.
        /// 0: no rest, the next request goes as usual.
        /// </summary>
        public static double Rest(TextFault fault, int times, double asked)
        {
            int n = Math.Max(1, Math.Min(times, 8));
            switch (fault)
            {
                case TextFault.Network: return Math.Min(RestMost, RestNetwork * (1 << (n - 1)));
                case TextFault.Busy: return Math.Min(RestMost, RestBusy * (1 << (n - 1)));
                case TextFault.RateLimited: return Math.Min(RestMost * 2, Math.Max(asked, Math.Min(RestMost, RestRate * (1 << (n - 1)))));
                case TextFault.UsageLimit: return RestUsage;
                case TextFault.BadRequest: return times >= RefusalsInRow ? RestRefusals : 0.0;
                case TextFault.TimedOut: return times >= TimeOutsInRow ? RestNetwork : 0.0;
                default: return 0.0;
            }
        }

        /// <summary>True for the faults that stop the service until the player mends it (the key, the sign-in, the model).</summary>
        public static bool Stops(TextFault fault) => fault == TextFault.Key || fault == TextFault.Model || fault == TextFault.NotEligible;

        /// <summary>Seconds a request may take: the time left to its deadline, at most <see cref="TimeOut"/>.</summary>
        public static double Limit(double deadline, double now) => Math.Min(TimeOut, deadline - now);

        // ---------------------------------------------------------------- nothing secret in a line

        /// <summary>
        /// A line fit for the log: each of <paramref name="secrets"/> (the key, the tokens) taken out wherever it is, and any
        /// word that looks like a key or a token (a run of 32 or more letters, digits and _-. with both letters and
        /// digits, or anything after "Bearer ") made "[hidden]".
        /// </summary>
        public static string Clean(string line, params string[] secrets)
        {
            if (string.IsNullOrEmpty(line))
            {
                return line;
            }
            for (int i = 0; secrets != null && i < secrets.Length; i++)
            {
                if (!string.IsNullOrEmpty(secrets[i]) && secrets[i].Length >= 6)
                {
                    line = line.Replace(secrets[i], "[hidden]");
                }
            }
            var t = new StringBuilder(line.Length);
            int at = 0;
            while (at < line.Length)
            {
                int start = at;
                while (at < line.Length && Keyish(line[at]))
                {
                    at++;
                }
                if (at > start)
                {
                    string word = line.Substring(start, at - start);
                    bool bearer = start >= 7 && string.Compare(line, start - 7, "Bearer ", 0, 7, StringComparison.OrdinalIgnoreCase) == 0;
                    t.Append(bearer || Looks(word) ? "[hidden]" : word);
                }
                if (at < line.Length)
                {
                    t.Append(line[at]);
                    at++;
                }
            }
            return t.ToString();
        }

        private static bool Keyish(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.';

        private static bool Looks(string word)
        {
            if (word.Length < 32)   // (shorter is a model's name; every service's keys are 32 characters or more)
            {
                return false;
            }
            bool letter = false, digit = false;
            for (int i = 0; i < word.Length; i++)
            {
                letter |= char.IsLetter(word[i]);
                digit |= char.IsDigit(word[i]);
            }
            return letter && digit;
        }

        private static string Short(string message)
        {
            string one = message.Replace('\n', ' ').Replace('\r', ' ').Trim();
            return one.Length <= WhyMost ? one : one.Substring(0, WhyMost - 1) + "\u2026";
        }
    }

    /// <summary>
    /// A streamed answer read as it comes (the ChatGPT plan's server-sent events), on the worker's thread: it says when
    /// the first words came (<see cref="Begun"/>: the person may be shown about to speak) and when the text is whole (its
    /// response.output_text.done, the last word, or the stream's end: completed, failed, incomplete or an error), so the
    /// web client stops reading there and the line is checked and shown at once, not when the service closes the answer.
    /// The words themselves are read only from the whole body (TextServiceRules.Stream): nothing partial is ever shown.
    /// Game-free (EconomyCheck, TextsServiceChecks.cs). COST: one small parse an event, on the worker's thread.
    /// </summary>
    public sealed class TextStreamRead
    {
        private volatile bool _begun, _whole;

        /// <summary>Called once, on the worker's thread, when the first words came.</summary>
        public Action OnBegun;

        public bool Begun => _begun;
        public bool Whole => _whole;

        /// <summary>One line of the stream; true once the answer is whole (the web client reads no more).</summary>
        public bool Feed(string line)
        {
            if (_whole || line == null || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                return _whole;
            }
            object e = TextJsonTree.Read(line.Substring(5).Trim());
            switch (TextJsonTree.Text(e, "type"))
            {
                case "response.output_text.delta":
                    if (!_begun && !string.IsNullOrEmpty(TextJsonTree.Text(e, "delta")))
                    {
                        _begun = true;
                        OnBegun?.Invoke();
                    }
                    break;
                case "response.output_text.done":
                case "response.completed":
                case "response.failed":
                case "response.incomplete":
                case "error":
                    _whole = true;
                    break;
            }
            return _whole;
        }
    }
}
