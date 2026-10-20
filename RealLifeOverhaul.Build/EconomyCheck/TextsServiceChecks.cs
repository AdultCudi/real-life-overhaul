using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using RealLifeOverhaul.Texts;

// Checks for Your own AI (Scripts/Texts: TextProviders, TextServiceRules, TextJsonTree, TextWeb, TextKeys, TextChatGpt,
// TextSignIn, TextService and the hub's route): the table of services; each kind of request with RLO Texts' own system
// turn and card; reading every kind of answer and telling the faults apart; the rests; the runner (two in flight at most,
// each within its time, stopping, resting, leaving out what a service refused, the first listed model); the hub's
// fallback to the pack and the written line, and shared text never sent; Sign in with ChatGPT end to end against a
// pretend sign-in server (registration, PKCE, the ID token's signature, the plan's scope, the refresh, signing out); the
// web client against a pretend local service (answers, a service that never answers, nothing listening); the keys'
// file sealed for the Windows user; no key or token in a log line, a reason, the save's line or the options; the texts
// in every language; and the director's look costing two comparisons.
internal static partial class Program
{
    private const string ServiceKey = "sk-test-0123456789abcdefSECRETkeyABCDEFGH4567";   // a pretend key: never in a log line

    private static void TextsService()
    {
        ServiceTable();
        ServiceRequests();
        ServiceAnswers();
        ServiceRests();
        ServiceJsonTree();
        ServiceClean();
        ServiceHub();
        ServiceRunner();
        ServiceWeb();
        ServiceKeysFile();
        ServiceChatGptRules();
        ServiceSignIn();
        ServiceNoSecrets();
        ServiceTexts();
        ServiceSources();
        ServiceTiming();
    }

    // ---------------------------------------------------------------- the table

    private static void ServiceTable()
    {
        bool order = TextProviders.Count == 12;
        for (int i = 0; i < TextProviders.Count; i++)
        {
            order &= (int)TextProviders.Of(i).Id == i;
        }
        Check(order && TextProviders.Of(99).Id == TextProvider.Off && TextProviders.Of(-1).Id == TextProvider.Off && TextProviders.ChoiceKeys().Length == 12,
            "your own AI, services: twelve entries in the list's order (Off first), a number past them reads as Off");
        bool bases = true, models = true;
        for (int i = 1; i < TextProviders.Count; i++)
        {
            TextProviderInfo p = TextProviders.Of(i);
            bases &= p.Access == TextAccess.Local ? p.Base.StartsWith("http://127.0.0.1:", StringComparison.Ordinal) : p.Base.StartsWith("https://", StringComparison.Ordinal);
            models &= p.Access == TextAccess.Key ? !string.IsNullOrEmpty(p.Model) : p.Model == null;
        }
        Check(bases && models, "your own AI, services: every service is reached over https but the two programs on this PC (127.0.0.1); a keyed one has a model of its own, the sign-in and the local ones take the first they list");
        TextProviderInfo ollama = TextProviders.Of(TextProvider.Ollama), groq = TextProviders.Of(TextProvider.Groq);
        Check(TextProviders.Base(ollama, null) == ollama.Base && TextProviders.Base(ollama, "http://localhost:11435/v1/") == "http://localhost:11435/v1"
              && TextProviders.Base(ollama, "http://192.168.1.5:11434/v1") == null && TextProviders.Base(ollama, "https://example.com/v1") == null
              && TextProviders.Base(ollama, "file:///c:/x") == null && TextProviders.Base(groq, "http://127.0.0.1:1/v1") == groq.Base
              && TextProviders.Base(TextProviders.Of(0), null) == null,
            "your own AI, services: a program on this PC may be given another port, never an address off this PC; a service on the internet keeps its own address");
        Check(TextProviders.Of(TextProvider.Anthropic).Api == TextApi.Messages && TextProviders.Of(TextProvider.ChatGpt).Api == TextApi.Responses
              && TextProviders.Of(TextProvider.ChatGpt).Access == TextAccess.SignIn && TextProviders.Of(TextProvider.Gemini).Base.EndsWith("/openai", StringComparison.Ordinal),
            "your own AI, services: Anthropic by its Messages API, ChatGPT by the Responses API its sign-in allows, Gemini through its OpenAI-compatible address, the rest by the chat API");
    }

    // ---------------------------------------------------------------- the requests

    private static JsonElement Body(TextWebRequest r) => JsonDocument.Parse(r.Body).RootElement;

    private static string Header(TextWebRequest r, string name)
    {
        foreach (KeyValuePair<string, string> h in r.Headers)
        {
            if (string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return h.Value;
            }
        }
        return null;
    }

    private static bool Has(JsonElement e, string name) => e.TryGetProperty(name, out _);

    private static void ServiceRequests()
    {
        string card = "who: Dana \"D\" Reyes, 30\nnews: a \u00e9clair at 5th Ave\\Broadway\nlang: en";
        TextProviderInfo router = TextProviders.Of(TextProvider.OpenRouter);
        TextWebRequest r = TextServiceRules.Build(router, router.Base, router.Model, TextCard.System, card, ServiceKey, false, false);
        JsonElement b = Body(r);
        JsonElement messages = b.GetProperty("messages");
        Check(r.Url == "https://openrouter.ai/api/v1/chat/completions" && r.Method == "POST" && Header(r, "Authorization") == "Bearer " + ServiceKey && Header(r, "X-Title") == "Real Life Overhaul"
              && messages.GetArrayLength() == 2 && messages[0].GetProperty("role").GetString() == "system" && messages[0].GetProperty("content").GetString() == TextCard.System
              && messages[1].GetProperty("role").GetString() == "user" && messages[1].GetProperty("content").GetString() == card
              && b.GetProperty("model").GetString() == router.Model && b.GetProperty("max_tokens").GetInt32() == TextServiceRules.ReplyTokens
              && Math.Abs(b.GetProperty("temperature").GetDouble() - TextJson.Temperature) < 1e-9 && !b.GetProperty("stream").GetBoolean() && !Has(b, "reasoning_effort"),
            "your own AI, the chat API: RLO Texts' system turn and the card as they are (quotes, a line break, a backslash, an accent), the model, the reply's length, the packs' temperature, the key as a bearer, the app's name to OpenRouter");
        TextWebRequest oss = TextServiceRules.Build(router, router.Base, "openai/gpt-oss-120b:free", TextCard.System, card, ServiceKey, false, false);
        TextWebRequest ossLeft = TextServiceRules.Build(router, router.Base, "openai/gpt-oss-120b:free", TextCard.System, card, ServiceKey, true, true);
        Check(Body(oss).GetProperty("reasoning_effort").GetString() == "low" && !Has(Body(ossLeft), "reasoning_effort") && !Has(Body(ossLeft), "temperature"),
            "your own AI, the chat API: a low reasoning effort only to the models that think first; a field the service refused is left out");
        TextProviderInfo openai = TextProviders.Of(TextProvider.OpenAi);
        JsonElement o = Body(TextServiceRules.Build(openai, openai.Base, openai.Model, TextCard.System, card, ServiceKey, false, false));
        Check(o.GetProperty("max_completion_tokens").GetInt32() == TextServiceRules.ReplyTokens && !Has(o, "max_tokens") && !Has(o, "temperature") && o.GetProperty("reasoning_effort").GetString() == "low",
            "your own AI, OpenAI: the reply's length as max_completion_tokens, no temperature (its reasoning models refuse one), a low reasoning effort");
        TextProviderInfo gemini = TextProviders.Of(TextProvider.Gemini), mistral = TextProviders.Of(TextProvider.Mistral);
        TextWebRequest g = TextServiceRules.Build(gemini, gemini.Base, gemini.Model, TextCard.System, card, ServiceKey, false, false);
        TextWebRequest m = TextServiceRules.Build(mistral, mistral.Base, mistral.Model, TextCard.System, card, ServiceKey, false, false);
        Check(g.Url == "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions" && Body(g).GetProperty("reasoning_effort").GetString() == "minimal"
              && m.Url == "https://api.mistral.ai/v1/chat/completions" && !Has(Body(m), "reasoning_effort") && Header(m, "X-Title") == null,
            "your own AI, Gemini and Mistral: Gemini's chat address with the least thinking, Mistral with none asked");
        TextProviderInfo anthropic = TextProviders.Of(TextProvider.Anthropic);
        TextWebRequest a = TextServiceRules.Build(anthropic, anthropic.Base, anthropic.Model, TextCard.System, card, ServiceKey, false, false);
        JsonElement ab = Body(a);
        Check(a.Url == "https://api.anthropic.com/v1/messages" && Header(a, "x-api-key") == ServiceKey && Header(a, "anthropic-version") == "2023-06-01" && Header(a, "Authorization") == null
              && ab.GetProperty("system").GetString() == TextCard.System && ab.GetProperty("messages")[0].GetProperty("content").GetString() == card
              && ab.GetProperty("messages")[0].GetProperty("role").GetString() == "user" && ab.GetProperty("max_tokens").GetInt32() == TextServiceRules.ThinkingTokens
              && ab.GetProperty("output_config").GetProperty("effort").GetString() == "low" && !Has(ab, "temperature")
              && !Has(Body(TextServiceRules.Build(anthropic, anthropic.Base, "claude-sonnet-4-6", TextCard.System, card, ServiceKey, false, false)), "output_config"),
            "your own AI, Anthropic: the Messages API with the key in x-api-key and its version, the system turn as system, the card as the user's, a low effort only to the model that takes it");
        TextProviderInfo chatgpt = TextProviders.Of(TextProvider.ChatGpt);
        TextWebRequest c = TextServiceRules.Build(chatgpt, chatgpt.Base, "gpt-6.1-sol", TextCard.System, card, "access-token", false, false);
        JsonElement cb = Body(c);
        Check(c.Url == "https://api.openai.com/v1/responses" && Header(c, "Authorization") == "Bearer access-token" && Header(c, "Accept") == "text/event-stream"
              && !cb.GetProperty("store").GetBoolean() && cb.GetProperty("stream").GetBoolean() && cb.GetProperty("instructions").GetString() == TextCard.System
              && cb.GetProperty("input")[0].GetProperty("content").GetString() == card && cb.GetProperty("input")[0].GetProperty("role").GetString() == "user"
              && !Has(cb, "temperature") && !Has(cb, "max_output_tokens") && !Has(cb, "metadata") && !Has(cb, "previous_response_id"),
            "your own AI, ChatGPT: POST /v1/responses with the plan's access token, never stored and streamed, the system turn as instructions (no system message), none of the fields OpenAI's preview refuses");
        TextProviderInfo ollama = TextProviders.Of(TextProvider.Ollama);
        TextWebRequest l = TextServiceRules.Build(ollama, "http://127.0.0.1:11434/v1", "llama3.2", TextCard.System, card, null, false, false);
        TextWebRequest list = TextServiceRules.Models(ollama, "http://127.0.0.1:11434/v1", null);
        Check(l.Url == "http://127.0.0.1:11434/v1/chat/completions" && l.Headers.Count == 0 && list.Method == "GET" && list.Url == "http://127.0.0.1:11434/v1/models" && list.Body == null
              && Header(TextServiceRules.Models(anthropic, anthropic.Base, ServiceKey), "x-api-key") == ServiceKey,
            "your own AI, a program on this PC: no key header at all; its models asked at GET /models");
    }

    // ---------------------------------------------------------------- the answers

    private static TextWebAnswer Ans(int status, string body, double retry = 0) => new TextWebAnswer { Status = status, Body = body, RetryAfter = retry };

    private static void ServiceAnswers()
    {
        TextServiceResult ok = TextServiceRules.Read(TextApi.Chat, Ans(200, "{\"id\":\"x\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"reasoning_content\":\"hmm\",\"content\":\"Heard about 5th Ave. You alright?\"},\"finish_reason\":\"stop\"}]}"));
        TextServiceResult parts = TextServiceRules.Read(TextApi.Chat, Ans(200, "{\"choices\":[{\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"Hi \"},{\"type\":\"text\",\"text\":\"there.\"}]}}]}"));
        TextServiceResult think = TextServiceRules.Read(TextApi.Chat, Ans(200, "{\"choices\":[{\"message\":{\"content\":\"<think>The card says coffee.</think>\\n Coffee at six?\"}}]}"));
        TextServiceResult thinking = TextServiceRules.Read(TextApi.Chat, Ans(200, "{\"choices\":[{\"message\":{\"content\":\"<think>still going\"}}]}"));
        TextServiceResult empty = TextServiceRules.Read(TextApi.Chat, Ans(200, "{\"choices\":[{\"message\":{\"content\":\"\"},\"finish_reason\":\"length\"}]}"));
        TextServiceResult notJson = TextServiceRules.Read(TextApi.Chat, Ans(200, "<html>proxy</html>"));
        Check(ok.Fault == TextFault.None && ok.Words == "Heard about 5th Ave. You alright?" && parts.Words == "Hi there." && think.Words == "Coffee at six?"
              && thinking.Fault == TextFault.NoWords && empty.Fault == TextFault.NoWords && empty.Why.Contains("ran out of tokens") && notJson.Fault == TextFault.NoWords,
            "your own AI, reading the chat API: the reply's content (its thinking left out), a list of parts joined, a local model's <think> taken off, an empty or unreadable answer is no words");
        TextServiceResult msg = TextServiceRules.Read(TextApi.Messages, Ans(200, "{\"type\":\"message\",\"content\":[{\"type\":\"thinking\",\"thinking\":\"\",\"signature\":\"s\"},{\"type\":\"text\",\"text\":\"See you at the gym.\"}],\"stop_reason\":\"end_turn\"}"));
        TextServiceResult refusal = TextServiceRules.Read(TextApi.Messages, Ans(200, "{\"content\":[],\"stop_reason\":\"refusal\"}"));
        Check(msg.Words == "See you at the gym." && refusal.Fault == TextFault.NoWords && refusal.Why.Contains("declined"),
            "your own AI, reading the Messages API: only the text blocks (a thinking block left out); a refusal is no words");
        string stream = "event: response.created\ndata: {\"type\":\"response.created\"}\n\nevent: response.output_text.delta\ndata: {\"type\":\"response.output_text.delta\",\"delta\":\"Saw the \"}\n\n"
                        + "data: {\"type\":\"response.output_text.delta\",\"delta\":\"news. Proud of you.\"}\n\ndata: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\"}}\n\n";
        TextServiceResult streamed = TextServiceRules.Read(TextApi.Responses, Ans(200, stream));
        TextServiceResult cut = TextServiceRules.Read(TextApi.Responses, Ans(200, "data: {\"type\":\"response.output_text.delta\",\"delta\":\"Saw the\"}\n\n"));
        TextServiceResult limit = TextServiceRules.Read(TextApi.Responses, Ans(200, "data: {\"type\":\"response.output_text.delta\",\"delta\":\"Saw\"}\n\ndata: {\"type\":\"response.failed\",\"response\":{\"error\":{\"code\":\"subscription_sharing_usage_limit_exceeded\",\"message\":\"limit\"}}}\n\n"));
        TextServiceResult incomplete = TextServiceRules.Read(TextApi.Responses, Ans(200, "data: {\"type\":\"response.incomplete\",\"response\":{\"incomplete_details\":{\"reason\":\"max_output_tokens\"}}}\n\n"));
        TextServiceResult crlf = TextServiceRules.Read(TextApi.Responses, Ans(200, stream.Replace("\n", "\r\n")));
        Check(streamed.Words == "Saw the news. Proud of you." && crlf.Words == streamed.Words && cut.Fault == TextFault.NoWords && limit.Fault == TextFault.UsageLimit
              && incomplete.Fault == TextFault.NoWords && incomplete.Why.Contains("max_output_tokens"),
            "your own AI, reading ChatGPT's stream: the deltas joined, a success only once response.completed came; a stream cut short, a response.failed (the plan's limit) or response.incomplete is no reply");
        TextServiceResult key = TextServiceRules.Read(TextApi.Chat, Ans(401, "{\"error\":{\"message\":\"Incorrect API key provided\",\"type\":\"invalid_request_error\",\"code\":\"invalid_api_key\"}}"));
        TextServiceResult forbidden = TextServiceRules.Read(TextApi.Messages, Ans(403, "{\"type\":\"error\",\"error\":{\"type\":\"permission_error\",\"message\":\"no\"}}"));
        TextServiceResult model = TextServiceRules.Read(TextApi.Chat, Ans(404, "{\"error\":{\"message\":\"The model does not exist\",\"code\":\"model_not_found\"}}"));
        TextServiceResult rate = TextServiceRules.Read(TextApi.Chat, Ans(429, "{\"error\":{\"message\":\"Rate limit reached\"}}", 12));
        TextServiceResult busy = TextServiceRules.Read(TextApi.Messages, Ans(529, "{\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\",\"message\":\"Overloaded\"}}"));
        TextServiceResult temp = TextServiceRules.Read(TextApi.Chat, Ans(400, "{\"error\":{\"message\":\"Unsupported value: 'temperature' does not support 0.7\",\"param\":\"temperature\"}}"));
        TextServiceResult effort = TextServiceRules.Read(TextApi.Chat, Ans(400, "{\"error\":{\"message\":\"reasoning_effort is not supported for this model\"}}"));
        TextServiceResult geminiList = TextServiceRules.Read(TextApi.Chat, Ans(400, "[{\"error\":{\"code\":400,\"message\":\"API key not valid. Please pass a valid API key.\",\"status\":\"INVALID_ARGUMENT\"}}]"));
        TextServiceResult detail = TextServiceRules.Read(TextApi.Responses, Ans(401, "{\"detail\":\"Could not validate the signed identity\"}"));
        TextServiceResult notEligible = TextServiceRules.Read(TextApi.Responses, Ans(403, "{\"error\":{\"code\":\"subscription_sharing_user_not_eligible\",\"message\":\"not eligible\"}}"));
        TextServiceResult usage = TextServiceRules.Read(TextApi.Responses, Ans(429, "{\"error\":{\"code\":\"subscription_sharing_usage_limit_exceeded\"}}"));
        TextServiceResult late = TextServiceRules.Read(TextApi.Chat, new TextWebAnswer { TimedOut = true, Failure = "no answer within 6.0 s" });
        TextServiceResult nobody = TextServiceRules.Read(TextApi.Chat, new TextWebAnswer { Failure = "no connection (HttpRequestException)" });
        Check(key.Fault == TextFault.Key && forbidden.Fault == TextFault.Key && model.Fault == TextFault.Model && rate.Fault == TextFault.RateLimited && rate.Why.Contains("12 s")
              && busy.Fault == TextFault.Busy && temp.Fault == TextFault.BadRequest && temp.Param == "temperature" && effort.Param == "reasoning"
              && geminiList.Fault == TextFault.Key && geminiList.Why.Contains("INVALID_ARGUMENT") && detail.Fault == TextFault.Key && detail.Why.Contains("signed identity")
              && notEligible.Fault == TextFault.NotEligible && usage.Fault == TextFault.UsageLimit && late.Fault == TextFault.TimedOut && nobody.Fault == TextFault.Network,
            "your own AI, faults: a refused key or sign-in, no such model, too many requests (with the wait asked), the service's own fault, a refused field named, Google's list of errors (its 400 for a key it does not know is a refused key), ChatGPT's detail and plan codes, a time-out and no connection, each told apart");
        List<string> plan = TextServiceRules.ModelList("{\"models\":[{\"slug\":\"gpt-6.1-sol\",\"display_name\":\"GPT-6.1 Sol\",\"visibility\":\"list\"},{\"slug\":\"hidden\",\"visibility\":\"hide\"},{\"slug\":\"gpt-6-luna\",\"visibility\":\"list\"}]}");
        List<string> local = TextServiceRules.ModelList("{\"object\":\"list\",\"data\":[{\"id\":\"nomic-embed-text\"},{\"id\":\"llama3.2:3b\"},{\"id\":\"qwen3:4b\"}]}");
        List<string> llama = TextServiceRules.ModelList("{\"models\":[{\"name\":\"m.gguf\",\"model\":\"m.gguf\"}],\"object\":\"list\",\"data\":[{\"id\":\"m.gguf\"}]}");
        Check(plan.Count == 2 && plan[0] == "gpt-6.1-sol" && plan[1] == "gpt-6-luna" && local.Count == 2 && local[0] == "llama3.2:3b" && llama.Count == 1 && llama[0] == "m.gguf" && TextServiceRules.ModelList("nope").Count == 0,
            "your own AI, the models a service lists: the plan's shown ones in its order, a local program's chat models (not an embedding model), llama.cpp's server's two lists, nothing from a non-list");
    }

    private static void ServiceRests()
    {
        Check(TextServiceRules.Rest(TextFault.Network, 1, 0) == TextServiceRules.RestNetwork && TextServiceRules.Rest(TextFault.Network, 2, 0) == TextServiceRules.RestNetwork * 2
              && TextServiceRules.Rest(TextFault.Network, 30, 0) == TextServiceRules.RestMost && TextServiceRules.Rest(TextFault.Busy, 1, 0) == TextServiceRules.RestBusy
              && TextServiceRules.Rest(TextFault.RateLimited, 1, 0) == TextServiceRules.RestRate && TextServiceRules.Rest(TextFault.RateLimited, 1, 90) == 90
              && TextServiceRules.Rest(TextFault.RateLimited, 1, 100000) == TextServiceRules.RestMost * 2 && TextServiceRules.Rest(TextFault.UsageLimit, 1, 0) == TextServiceRules.RestUsage
              && TextServiceRules.Rest(TextFault.BadRequest, TextServiceRules.RefusalsInRow - 1, 0) == 0 && TextServiceRules.Rest(TextFault.BadRequest, TextServiceRules.RefusalsInRow, 0) == TextServiceRules.RestRefusals
              && TextServiceRules.Rest(TextFault.TimedOut, 1, 0) == 0 && TextServiceRules.Rest(TextFault.TimedOut, TextServiceRules.TimeOutsInRow, 0) > 0
              && TextServiceRules.Rest(TextFault.NoWords, 9, 0) == 0,
            "your own AI, rests: doubled after each fault in a row up to " + TextServiceRules.RestMost + " s, a rate limit as long as the service asked (bounded), the plan's limit " + TextServiceRules.RestUsage + " s, refusals and time-outs only in a row, no words none");
        Check(TextServiceRules.Stops(TextFault.Key) && TextServiceRules.Stops(TextFault.Model) && TextServiceRules.Stops(TextFault.NotEligible) && !TextServiceRules.Stops(TextFault.Network)
              && !TextServiceRules.Stops(TextFault.RateLimited) && TextServiceRules.Limit(100, 99) == 1 && TextServiceRules.Limit(100, 0) == TextServiceRules.TimeOut,
            "your own AI, rests: a refused key or sign-in and a missing model stop the service until the player mends it; a request's time is its deadline's, at most " + TextServiceRules.TimeOut + " s");
    }

    private static void ServiceJsonTree()
    {
        object root = TextJsonTree.Read("{\"a\":[1,{\"b\":\"x\\u00e9\\n\\\"q\\\"\"}],\"t\":true,\"f\":false,\"n\":null,\"num\":-1.5e2}");
        Check(TextJsonTree.Text(root, "a[1].b") == "x\u00e9\n\"q\"" && TextJsonTree.Number(root, "a[0]") == 1 && TextJsonTree.At(root, "t") is bool t && t
              && TextJsonTree.At(root, "f") is bool f && !f && TextJsonTree.At(root, "n") == null && TextJsonTree.Number(root, "num") == -150 && TextJsonTree.At(root, "a[9]") == null
              && TextJsonTree.At(root, "x.y") == null && TextJsonTree.Read("{\"a\":}") == null && TextJsonTree.Read("[1,2") == null && TextJsonTree.Read("") == null
              && TextJsonTree.Read("{} extra") == null && TextJsonTree.Read(new string('[', 40) + new string(']', 40)) == null && TextJsonTree.Read(new string('[', 20) + new string(']', 20)) != null,
            "your own AI, reading JSON: nested objects and arrays by a path, every escape, numbers and the three words; anything not JSON, cut short, followed by more, or nested past " + TextJsonTree.Deepest + " levels is nothing, never an error");
    }

    private static void ServiceClean()
    {
        string line = "the service said 401 invalid_api_key: Incorrect API key provided: " + ServiceKey + "; Authorization: Bearer abc.def.ghi; model deepseek-v4.1-flash-free";
        string clean = TextServiceRules.Clean(line, ServiceKey);
        string hidden = TextServiceRules.Clean("token eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.c2lnbmF0dXJlMTIzNDU2Nzg5MA left", null);
        Check(!clean.Contains(ServiceKey) && !clean.Contains("abc.def.ghi") && clean.Contains("[hidden]") && clean.Contains("deepseek-v4.1-flash-free") && clean.Contains("invalid_api_key")
              && !hidden.Contains("eyJhbGci") && TextServiceRules.Clean(null) == null,
            "your own AI, nothing secret in a line: the key wherever it is, whatever follows Bearer, and any run that looks like a key or a token are hidden; a model's name and an error's code stay");
    }

    // ---------------------------------------------------------------- the hub's route

    /// <summary>A service runner whose state the checks set.</summary>
    private sealed class PretendService : ITextServiceRunner
    {
        public TextServerState State { get; set; } = TextServerState.Ready;
        public bool Resting { get; set; }
        public bool Quiet { set { } }
        public int Nudged;
        public void Nudge() => Nudged++;
    }

    private static void ServiceHub()
    {
        var packQ = new TextQueue();
        var serviceQ = new TextQueue();
        var service = new PretendService();
        var hub = new TextsHub(packQ, serviceQ) { Runner = new PretendRunner(), Service = service, Data = TextIntents.DataV4 };
        string got = "(none)";
        TextRequest r = TextsAskOf("s1", "friend.crash", null, w => got = w);
        Check(hub.Ask(r, 0) && serviceQ.Count == 1 && packQ.Count == 0 && service.Nudged == 1 && hub.Data == TextsHub.Newest && hub.PackData == TextIntents.DataV4,
            "your own AI, the route: with the service up a request goes to it first, through its own queue; the cards asked are the newest (data v" + TextsHub.Newest + "), the pack's own stay v4");
        TextJob job = serviceQ.Take(0.1, true);
        var sheet = new TextSheet();
        TextIntents.Build(TextIntents.Find("friend.crash", null, TextsHub.Newest), r.Who, r.Slots, r.History, sheet);
        Check(job.Card == TextCard.Card(sheet) && job.Prompt == null && TextCard.ChatMl(sheet).Contains(job.Card) && TextCard.ChatMl(sheet).Contains(TextCard.System),
            "your own AI, the route: the service is given the very card the pack's prompt holds, with the same system turn");
        serviceQ.Done(job, "Heard about 5th Ave. You alright?", 0.8, null);
        hub.Look(1.0);
        Check(got == "Heard about 5th Ave. You alright?" && r.ByService && hub.ServiceShown == 1 && hub.Shown == 1, "your own AI, the route: the service's words come back checked by TextCheck, and are counted as the service's");
        got = "(none)";
        r = TextsAskOf("s2", "friend.crash", null, w => got = w);
        hub.Ask(r, 2);
        job = serviceQ.Take(2.1, true);
        serviceQ.Done(job, "Heard about 5th Ave! Wow!", 0.5, null);
        hub.Look(2.7);
        TextJob packJob = packQ.Take(2.8, true);
        Check(got == "(none)" && packJob != null && packJob.Prompt != null && packJob.Prompt.StartsWith("<|im_start|>system", StringComparison.Ordinal) && hub.Refused == 1,
            "your own AI, fallback: words the checker refuses send the request to the pack, with its own card, in the time left");
        packQ.Done(packJob, "Heard about 5th Ave. All right?", 0.6, null);
        hub.Look(3.5);
        Check(got == "Heard about 5th Ave. All right?" && !r.ByService && hub.ServiceFell == 1, "your own AI, fallback: the pack's words are shown, and the service's miss is counted");
        got = "(none)";
        r = TextsAskOf("s3", "friend.crash", null, w => got = w);
        hub.Ask(r, 10);
        serviceQ.MissAll("the service rests");
        hub.Look(10.1);
        Check(got == "(none)" && packQ.Count == 1, "your own AI, fallback: a service that rests (every request missed at once) sends them to the pack at once, without waiting for their time");
        hub.Clear();
        var alone = new TextsHub(new TextQueue(), serviceQ = new TextQueue()) { Service = service };
        got = "(none)";
        r = TextsAskOf("s4", "friend.crash", null, w => got = w);
        bool asked = alone.Ask(r, 20);
        job = serviceQ.Take(20.1, true);
        serviceQ.Done(job, null, 6.0, "no answer within 6.0 s");
        alone.Look(26.2);
        Check(asked && alone.Wants(TextUse.People, false) && got == null && alone.Fallbacks == 1 && alone.ServiceFell == 1,
            "your own AI, fallback: with no pack, a service that fails gives the written line (the player never waits)");
        var shared = TextsAskOf("fp:9", "friend.crash", null, w => { });
        shared.Shared = true;
        Check(!alone.Wants(TextUse.People, true) && !alone.Ask(shared, 30) && serviceQ.Count == 0 && shared.Why.Contains("shared text never goes"),
            "your own AI, Going Public: shared text (the shared feed and paper) never goes to the service; without a pack it keeps the written line");
        Check(hub.Ask(shared, 31) && serviceQ.Count == 0, "your own AI, Going Public: shared text goes to the pack as before, never the service");
        hub.Clear();
        service.Resting = true;
        r = TextsAskOf("s5", "friend.crash", null, w => { });
        Check(hub.Ask(r, 40) && packQ.Count == 1 && hub.Data == TextIntents.DataV4 && hub.WhyNot(TextUse.People, false, "friend.crash", null, TextPriority.Soon, false, 30) == null,
            "your own AI, the route: a resting service is passed over for the pack, and the pack's own cards are asked meanwhile");
        hub.Clear();
        service.Resting = false;
        service.State = TextServerState.Failed;
        Check(hub.Ask(TextsAskOf("s6", "friend.crash", null, w => { }), 50) && packQ.Count == 1, "your own AI, the route: a stopped service is passed over for the pack");
        hub.Clear();
        service.State = TextServerState.Ready;
        var only = TextsAskOf("test:service", "friend.crash", null, w => got = w);
        only.ServiceOnly = true;
        got = "(none)";
        hub.Ask(only, 60);
        job = hub.ServiceQueue.Take(60.1, true);
        hub.ServiceQueue.Done(job, null, 1.0, "the service said 500");
        hub.Look(61);
        Check(got == null && packQ.Count == 0 && only.Why.Contains("500"), "your own AI, the test: a request for the service only never goes to the pack, and says why it failed");
        packQ.Expected(0.3);
        hub.ServiceQueue.Expected(3.0);
        var spoken = TextsAskOf("st:1", "friend.crash", null, w => { }, TextPriority.Now, 1.0);
        Check(hub.Ask(spoken, 70) && hub.ServiceQueue.Count == 0 && packQ.Count == 1,
            "your own AI, the route: a line due sooner than the service is expected to answer (" + hub.ServiceQueue.Expect.ToString("0.0") + " s) goes to the pack");
        hub.Clear();
        hub.Service = null;
        Check(hub.Data == TextIntents.DataV4 && hub.Expect == packQ.Expect && hub.ServiceWhy(TextUse.People, false, "friend.crash", null) != null,
            "your own AI, the route: with the service off the hub is as it was (the pack's cards, its expectation)");
    }

    // ---------------------------------------------------------------- the runner

    /// <summary>A web of answers the checks give, counting how many requests are in flight at once.</summary>
    private sealed class PretendWeb : ITextWeb
    {
        public Func<TextWebRequest, int, TextWebAnswer> Answer;
        public readonly List<TextWebRequest> Sent = new List<TextWebRequest>();
        public int Most, Limit;
        private int _now;

        public TextWebAnswer Send(TextWebRequest request, int timeoutMs)
        {
            int now = Interlocked.Increment(ref _now);
            lock (Sent)
            {
                Sent.Add(request);
                Most = Math.Max(Most, now);
                Limit = Math.Max(Limit, timeoutMs);
            }
            try
            {
                return Answer(request, timeoutMs);
            }
            finally
            {
                Interlocked.Decrement(ref _now);
            }
        }
    }

    private static bool Until(Func<bool> done, int ms)
    {
        var watch = Stopwatch.StartNew();
        while (!done() && watch.ElapsedMilliseconds < ms)
        {
            Thread.Sleep(10);
        }
        return done();
    }

    private static TextJob ServiceJob(TextQueue q, string key, double seconds)
    {
        var job = new TextJob { Key = key, Card = "who: Dana\nlang: en", Priority = TextPriority.Soon, Deadline = TextClock.Now + seconds };
        q.Add(job, TextClock.Now);
        return job;
    }

    private static void ServiceRunner()
    {
        var logs = new List<string>();
        Action<string> log = s => { lock (logs) { logs.Add(s); } };
        TextProviderInfo groq = TextProviders.Of(TextProvider.Groq);
        var web = new PretendWeb { Answer = (r, ms) => { Thread.Sleep(80); return Ans(200, "{\"choices\":[{\"message\":{\"content\":\"On my way.\"}}]}"); } };
        var q = new TextQueue();
        var run = new TextService(q, new TextServiceStart { Provider = groq, Base = groq.Base, Key = ServiceKey }, web, log);
        run.Start();
        var jobs = new List<TextJob>();
        for (int i = 0; i < 6; i++)
        {
            jobs.Add(ServiceJob(q, "j" + i, 20));
        }
        run.Nudge();
        bool all = Until(() => jobs.TrueForAll(j => j.State == TextJobState.Written), 5000);
        Check(run.State == TextServerState.Ready && all && jobs[0].Raw == "On my way." && web.Most <= TextService.Workers && web.Most >= 1
              && web.Limit <= TextServiceRules.TimeOut * 1000 && run.Written == 6,
            "your own AI, the runner: every request written by the service, at most " + TextService.Workers + " in flight (" + web.Most + " seen), each within " + TextServiceRules.TimeOut + " s (" + web.Limit + " ms asked)");
        run.Dispose();
        var none = new TextService(new TextQueue(), new TextServiceStart { Provider = groq, Base = groq.Base, Key = null }, web, log);
        none.Start();
        Check(none.State == TextServerState.Failed && none.Fault == TextFault.Key && none.Why.Contains("no API key"), "your own AI, the runner: without a key it stops at once and says so");
        var refusing = new PretendWeb { Answer = (r, ms) => Ans(401, "{\"error\":{\"message\":\"Incorrect API key provided: " + ServiceKey + "\",\"code\":\"invalid_api_key\"}}") };
        q = new TextQueue();
        run = new TextService(q, new TextServiceStart { Provider = groq, Base = groq.Base, Key = ServiceKey }, refusing, log);
        run.Start();
        TextJob refused = ServiceJob(q, "k", 20);
        run.Nudge();
        Until(() => run.State == TextServerState.Failed, 3000);
        Check(run.State == TextServerState.Failed && run.Fault == TextFault.Key && refused.State == TextJobState.Missed && !run.Why.Contains(ServiceKey) && !refused.Why.Contains(ServiceKey),
            "your own AI, the runner: a refused key stops the service (the pack or the written lines until it is mended), and the key the service echoed is in no reason");
        run.Dispose();
        int calls = 0;
        var limited = new PretendWeb { Answer = (r, ms) => Interlocked.Increment(ref calls) == 1 ? Ans(429, "{\"error\":{\"message\":\"slow down\"}}", 30) : Ans(200, "{\"choices\":[{\"message\":{\"content\":\"Hi.\"}}]}") };
        q = new TextQueue();
        run = new TextService(q, new TextServiceStart { Provider = groq, Base = groq.Base, Key = ServiceKey, Workers = 1 }, limited, log);
        run.Start();
        TextJob first = ServiceJob(q, "r1", 20);
        run.Nudge();
        Until(() => run.Resting, 3000);
        TextJob second = ServiceJob(q, "r2", 20);
        Until(() => second.State != TextJobState.Waiting, 2000);
        Check(run.Resting && first.State == TextJobState.Missed && second.State == TextJobState.Missed && calls == 1 && run.State == TextServerState.Ready,
            "your own AI, the runner: too many requests rests the service as long as it asked, and what waits meanwhile is missed at once (the hub falls back) without another request");
        run.Dispose();
        var bodies = new List<string>();
        var temperature = new PretendWeb
        {
            Answer = (r, ms) =>
            {
                lock (bodies) { bodies.Add(r.Body); }
                return r.Body.Contains("\"temperature\"") ? Ans(400, "{\"error\":{\"message\":\"temperature is not supported\",\"param\":\"temperature\"}}") : Ans(200, "{\"choices\":[{\"message\":{\"content\":\"Hi.\"}}]}");
            }
        };
        q = new TextQueue();
        run = new TextService(q, new TextServiceStart { Provider = TextProviders.Of(TextProvider.Mistral), Base = "https://api.mistral.ai/v1", Key = ServiceKey, Workers = 1 }, temperature, log);
        run.Start();
        TextJob t1 = ServiceJob(q, "t1", 20);
        run.Nudge();
        Until(() => t1.State != TextJobState.Waiting && t1.State != TextJobState.Running, 2000);
        TextJob t2 = ServiceJob(q, "t2", 20);
        run.Nudge();
        Until(() => t2.State == TextJobState.Written, 2000);
        Check(t1.State == TextJobState.Missed && t2.State == TextJobState.Written && bodies.Count == 2 && !bodies[1].Contains("temperature") && !run.Resting,
            "your own AI, the runner: a field the service refused (the temperature) is left out from the next request on, without a rest");
        run.Dispose();
        int lists = 0;
        var local = new PretendWeb
        {
            Answer = (r, ms) =>
            {
                if (r.Method == "GET")
                {
                    return Interlocked.Increment(ref lists) == 1 ? new TextWebAnswer { Failure = "no connection" } : Ans(200, "{\"data\":[{\"id\":\"qwen3:4b\"}]}");
                }
                return Ans(200, "{\"choices\":[{\"message\":{\"content\":\"<think>x</think>Hello.\"}}]}");
            }
        };
        TextProviderInfo ollama = TextProviders.Of(TextProvider.Ollama);
        q = new TextQueue();
        run = new TextService(q, new TextServiceStart { Provider = ollama, Base = ollama.Base }, local, log);
        run.Start();
        Until(() => run.Resting, 2000);
        bool restedFirst = run.State == TextServerState.Loading && run.Resting;
        Check(restedFirst && run.Model == null, "your own AI, a program on this PC: while nothing answers it rests and stays not ready (nothing is sent its way)");
        run.Dispose();
        lists = 1;   // (the next list answers)
        q = new TextQueue();
        run = new TextService(q, new TextServiceStart { Provider = ollama, Base = ollama.Base }, local, log);
        run.Start();
        Until(() => run.State == TextServerState.Ready, 2000);
        TextJob lj = ServiceJob(q, "l1", 20);
        run.Nudge();
        Until(() => lj.State == TextJobState.Written, 2000);
        Check(run.Model == "qwen3:4b" && lj.Raw == "Hello." && local.Sent.Exists(s => s.Body != null && s.Body.Contains("\"model\":\"qwen3:4b\"")),
            "your own AI, a program on this PC: with no model named, the first it lists is used; its thinking is taken off the reply");
        run.Dispose();
        var slow = new PretendWeb { Answer = (r, ms) => { Thread.Sleep(Math.Min(ms, 400)); return new TextWebAnswer { TimedOut = true, Failure = "no answer" }; } };
        q = new TextQueue();
        run = new TextService(q, new TextServiceStart { Provider = groq, Base = groq.Base, Key = ServiceKey, Workers = 1 }, slow, log);
        run.Start();
        TextJob soon = ServiceJob(q, "soon", 2.0);
        run.Nudge();
        Until(() => soon.State == TextJobState.Missed, 3000);
        Check(soon.State == TextJobState.Missed && slow.Limit <= 2000 && !run.Resting, "your own AI, the runner: a request is given only the time left to its deadline (" + slow.Limit + " ms of 2 s), and one time-out alone is no rest");
        run.Dispose();
        Check(q.Count == 0, "your own AI, the runner: switched off, nothing of it waits");
        var halted = new PretendWeb { Answer = (r, ms) => Ans(200, "{\"choices\":[{\"message\":{\"content\":\"Hi.\"}}]}") };
        q = new TextQueue();
        run = new TextService(q, new TextServiceStart { Provider = groq, Base = groq.Base, Key = ServiceKey }, halted, log);
        run.Start();
        run.Halt();
        TextJob after = ServiceJob(q, "after", 20);
        run.Nudge();
        Thread.Sleep(TextService.WakeMs * 3);
        Check(halted.Sent.Count == 0 && after.State == TextJobState.Waiting,
            "your own AI, the runner: halted (the player chose another service), it sends nothing more; what waits is left for the next runner of the same queue");
        run.Dispose();
        // The game closing: the engine aborts the workers (ThreadAbortException), or a request ends in an exception while the service
        // is being switched off. Not a fault: one plain line for both workers, never Failed, nothing to mend in the options.
        Check(TextServiceRules.Closing("ThreadAbortException", false) && TextServiceRules.Closing("IOException", true) && !TextServiceRules.Closing("IOException", false)
              && !TextServiceRules.Closing("NullReferenceException", false),
            "your own AI, the runner: a worker aborted as the game closes, or any end while it is switched off, is a quiet stop; any other exception is a fault");
        var gate = new ManualResetEventSlim(false);
        var tearing = new PretendWeb { Answer = (r, ms) => { gate.Wait(3000); throw new IOException("the connection was torn down"); } };
        q = new TextQueue();
        run = new TextService(q, new TextServiceStart { Provider = groq, Base = groq.Base, Key = ServiceKey }, tearing, log);
        run.Start();
        ServiceJob(q, "close1", 20);
        ServiceJob(q, "close2", 20);
        run.Nudge();
        Until(() => tearing.Sent.Count >= 2, 3000);
        int mark;
        lock (logs)
        {
            mark = logs.Count;
        }
        var closing = new Thread(run.Dispose) { IsBackground = true };
        closing.Start();
        Thread.Sleep(60);
        gate.Set();
        closing.Join(5000);
        int closed = 0, mended = 0;
        lock (logs)
        {
            for (int i = mark; i < logs.Count; i++)
            {
                closed += logs[i].Contains("stopped (the game is closing)") ? 1 : 0;
                mended += logs[i].Contains("mended") ? 1 : 0;
            }
        }
        Check(tearing.Sent.Count == 2 && closed == 1 && mended == 0 && run.State == TextServerState.Off && run.Fault != TextFault.Busy,
            "your own AI, the runner: both workers ending as it is switched off say one plain closing line (" + closed + "), nothing to mend (" + mended + "), and it is Off, not Failed ("
            + run.State + ")");
        foreach (string line in logs)
        {
            if (line.Contains(ServiceKey))
            {
                Check(false, "your own AI, the runner: a log line held the key: " + line.Replace(ServiceKey, "<key>"));
            }
        }
        Check(logs.Count > 0 && logs.TrueForAll(l => l.StartsWith("Your own AI: ", StringComparison.Ordinal)), "your own AI, the runner: its log lines say whose they are (" + logs.Count + " lines), none with the key");
    }

    // ---------------------------------------------------------------- the web client against a pretend local service

    /// <summary>A small HTTP server on 127.0.0.1 for the checks: each request answered by the handler (status, body, extra headers), or held unanswered.</summary>
    private sealed class PretendHttp : IDisposable
    {
        private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
        private readonly Func<string, string, string, string[]> _handle;   // (method, target, body) -> { status line, body, extra headers } or null to hold
        private volatile bool _stop;
        public readonly List<string> Seen = new List<string>();

        public PretendHttp(Func<string, string, string, string[]> handle)
        {
            _handle = handle;
            _listener.Start();
            new Thread(Loop) { IsBackground = true }.Start();
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
        public string Base => "http://127.0.0.1:" + Port;

        private void Loop()
        {
            while (!_stop)
            {
                TcpClient c;
                try
                {
                    c = _listener.AcceptTcpClient();
                }
                catch (Exception)
                {
                    return;
                }
                new Thread(() => Serve(c)) { IsBackground = true }.Start();
            }
        }

        private void Serve(TcpClient c)
        {
            try
            {
                NetworkStream net = c.GetStream();
                var all = new MemoryStream();
                var buf = new byte[4096];
                string text = string.Empty;
                int length = -1, headEnd = -1;
                while (true)
                {
                    int n = net.Read(buf, 0, buf.Length);
                    if (n <= 0)
                    {
                        break;
                    }
                    all.Write(buf, 0, n);
                    text = Encoding.UTF8.GetString(all.ToArray());
                    headEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                    if (headEnd >= 0)
                    {
                        Match m = Regex.Match(text.Substring(0, headEnd), @"Content-Length:\s*(\d+)", RegexOptions.IgnoreCase);
                        length = m.Success ? int.Parse(m.Groups[1].Value) : 0;
                        if (Encoding.UTF8.GetByteCount(text.Substring(headEnd + 4)) >= length)
                        {
                            break;
                        }
                    }
                }
                string[] line = text.Split("\r\n")[0].Split(' ');
                string body = headEnd >= 0 ? text.Substring(headEnd + 4) : string.Empty;
                lock (Seen)
                {
                    Seen.Add(line[0] + " " + line[1] + "\n" + text.Substring(0, Math.Max(0, headEnd)) + "\n\n" + body);
                }
                string[] answer = _handle(line[0], line[1], body);
                if (answer == null)
                {
                    Thread.Sleep(15000);   // (a service that takes the connection and never answers)
                    return;
                }
                byte[] b = Encoding.UTF8.GetBytes(answer[1]);
                byte[] h = Encoding.ASCII.GetBytes("HTTP/1.1 " + answer[0] + "\r\nContent-Type: application/json\r\nContent-Length: " + b.Length + "\r\n" + (answer.Length > 2 ? answer[2] : string.Empty) + "Connection: close\r\n\r\n");
                net.Write(h, 0, h.Length);
                net.Write(b, 0, b.Length);
            }
            catch (Exception)
            {
                // (the client went away)
            }
            finally
            {
                c.Close();
            }
        }

        public void Dispose()
        {
            _stop = true;
            _listener.Stop();
        }
    }

    private static void ServiceWeb()
    {
        using (var server = new PretendHttp((method, target, body) =>
                   target.EndsWith("/hold", StringComparison.Ordinal) ? null
                   : target.EndsWith("/busy", StringComparison.Ordinal) ? new[] { "429 Too Many Requests", "{\"error\":{\"message\":\"slow\"}}", "Retry-After: 7\r\n" }
                   : new[] { "200 OK", "{\"choices\":[{\"message\":{\"content\":\"" + method + " " + body.Length + "\"}}]}" }))
        {
            var web = new TextWeb();
            var r = new TextWebRequest { Url = server.Base + "/v1/chat/completions", Body = "{\"x\":\"\u00e9\"}" }.Header("Authorization", "Bearer k");
            TextWebAnswer ok = web.Send(r, 3000);
            string seen = server.Seen.Count > 0 ? server.Seen[0] : string.Empty;
            Check(ok.Status == 200 && TextServiceRules.Read(TextApi.Chat, ok).Words == "POST " + "{\"x\":\"\u00e9\"}".Length && seen.Contains("Authorization: Bearer k") && seen.Contains("\u00e9")
                  && seen.Contains("Content-Type: application/json; charset=utf-8"),
                "your own AI, the web client: a request sent with its headers and a UTF-8 body, the answer read whole");
            TextWebAnswer busy = web.Send(new TextWebRequest { Url = server.Base + "/busy", Body = "{}" }, 3000);
            Check(busy.Status == 429 && busy.RetryAfter == 7 && TextServiceRules.Read(TextApi.Chat, busy).Fault == TextFault.RateLimited,
                "your own AI, the web client: a refusal's status and the wait the service asked for (Retry-After)");
            var watch = Stopwatch.StartNew();
            TextWebAnswer held = web.Send(new TextWebRequest { Url = server.Base + "/hold", Body = "{}" }, 800);
            long ms = watch.ElapsedMilliseconds;
            Check(held.Status == 0 && held.TimedOut && ms < 800 + TextWeb.GraceMs + 400,
                "your own AI, the web client: a service that takes the request and never answers is given up at its time (" + ms + " ms for 800 ms), the worker never held longer");
        }
        int free;
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        free = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var w2 = Stopwatch.StartNew();
        TextWebAnswer nobody = new TextWeb().Send(new TextWebRequest { Url = "http://127.0.0.1:" + free + "/v1/models", Method = "GET", Body = null }, 3000);
        Check(nobody.Status == 0 && !nobody.TimedOut && nobody.Failure.StartsWith("no connection", StringComparison.Ordinal) && w2.ElapsedMilliseconds < 3000
              && TextServiceRules.Read(TextApi.Chat, nobody).Fault == TextFault.Network,
            "your own AI, the web client: nothing listening (a local program not started) is no connection, told at once");
    }

    // ---------------------------------------------------------------- the keys' file

    private static void ServiceKeysFile()
    {
        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rlo-keys-" + Guid.NewGuid().ToString("N"));
        try
        {
            Check(TextKeys.Sealable(), "your own AI, the keys: Windows' protection of the user's data answers here");
            var keys = new TextKeys(folder);
            Check(keys.Set("key.6", ServiceKey, "chatgpt.refresh", "refresh-token-value-123456") && keys.Get("key.6") == ServiceKey && keys.Has("chatgpt.refresh"), "your own AI, the keys: kept and read back");
            byte[] file = File.ReadAllBytes(System.IO.Path.Combine(folder, TextKeys.FileName));
            string asText = Encoding.UTF8.GetString(file) + Encoding.Unicode.GetString(file);
            var again = new TextKeys(folder);
            Check(file.Length > 0 && !asText.Contains(ServiceKey) && !asText.Contains("refresh-token-value") && !asText.Contains("key.6") && again.Get("key.6") == ServiceKey
                  && !File.Exists(System.IO.Path.Combine(folder, TextKeys.FileName + ".new")),
                "your own AI, the keys: the file is sealed (neither a key nor its name can be read in it) and opens again for the same user; nothing half written is left");
            again.Set("key.6", null);
            Check(new TextKeys(folder).Get("key.6") == null && new TextKeys(folder).Get("chatgpt.refresh") == "refresh-token-value-123456", "your own AI, the keys: one forgotten, the others kept");
            File.WriteAllBytes(System.IO.Path.Combine(folder, TextKeys.FileName), new byte[] { 1, 2, 3, 4 });
            var broken = new TextKeys(folder);
            Check(broken.Get("chatgpt.refresh") == null && broken.Why != null, "your own AI, the keys: a file that does not open (another user's, or damaged) gives nothing, and says why");
            var d = new Dictionary<string, string> { { "a", "1\r\n2" }, { "b", "x=y" } };
            var back = new Dictionary<string, string>();
            TextKeys.Parse(TextKeys.Text(d), back);
            Check(back["a"] == "12" && back["b"] == "x=y", "your own AI, the keys: a value keeps its equals signs and can never break into a second line");
        }
        finally
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (Exception)
            {
                // (a temporary folder)
            }
        }
    }

    // ---------------------------------------------------------------- Sign in with ChatGPT: the rules

    private static string Jwt(RSA rsa, string kid, string payload)
    {
        string head = TextChatGpt.Url64(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"kid\":\"" + kid + "\",\"typ\":\"JWT\"}"));
        string body = TextChatGpt.Url64(Encoding.UTF8.GetBytes(payload));
        byte[] sig = rsa.SignData(Encoding.ASCII.GetBytes(head + "." + body), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return head + "." + body + "." + TextChatGpt.Url64(sig);
    }

    private static string Jwks(RSA rsa, string kid)
    {
        RSAParameters p = rsa.ExportParameters(false);
        return "{\"keys\":[{\"kty\":\"RSA\",\"kid\":\"other\",\"n\":\"AQAB\",\"e\":\"AQAB\"},{\"kty\":\"RSA\",\"kid\":\"" + kid + "\",\"alg\":\"RS256\",\"n\":\"" + TextChatGpt.Url64(p.Modulus) + "\",\"e\":\"" + TextChatGpt.Url64(p.Exponent) + "\"}]}";
    }

    private static void ServiceChatGptRules()
    {
        Check(TextChatGpt.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk") == "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM" && TextChatGpt.Fresh() != TextChatGpt.Fresh()
              && TextChatGpt.Fresh().Length == 43 && Regex.IsMatch(TextChatGpt.NewHost(), "^urn:uuid:[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$"),
            "your own AI, ChatGPT: PKCE's S256 challenge as RFC 7636 gives it, fresh random values each time, a host id that is an opaque version 4 UUID");
        string first = TextChatGpt.AuthorizeUrl("https://auth.openai.com", null, "Real Life Overhaul", "urn:uuid:1", null, TextChatGpt.Redirect(1455), "st", "no", "ver");
        Dictionary<string, string> fq = TextChatGpt.Query(first);
        string again = TextChatGpt.AuthorizeUrl("https://auth.openai.com", "oaiapp_123", "Real Life Overhaul", "urn:uuid:1", "id.token.x", TextChatGpt.Redirect(50000), "st", "no", "ver");
        Dictionary<string, string> aq = TextChatGpt.Query(again);
        Check(first.StartsWith("https://auth.openai.com/api/accounts/authorize?", StringComparison.Ordinal) && fq["client_id"] == "dynamic_agent_client" && fq["agent_name_hint"] == "Real Life Overhaul"
              && fq["ext_agent_host_id"] == "urn:uuid:1" && !fq.ContainsKey("id_token_hint") && fq["redirect_uri"] == "http://127.0.0.1:1455/auth/callback" && fq["response_type"] == "code"
              && fq["scope"] == "openid profile email offline_access resource.invoke chatgpt.tokens.use.direct" && fq["resource"] == "https://api.openai.com/v1"
              && fq["code_challenge_method"] == "S256" && fq["code_challenge"] == TextChatGpt.Challenge("ver") && fq["state"] == "st" && fq["nonce"] == "no"
              && aq["client_id"] == "oaiapp_123" && !aq.ContainsKey("agent_name_hint") && aq["id_token_hint"] == "id.token.x" && aq["redirect_uri"] == "http://127.0.0.1:50000/auth/callback",
            "your own AI, ChatGPT: the first sign-in registers the app (dynamic_agent_client, its name, this PC's host id), a later one uses the kept client id with the ID token as a hint; PKCE, the plan's scopes and resource, 127.0.0.1");
        string kept;
        Check(TextChatGpt.Callback(TextChatGpt.Query("/auth/callback?code=c&state=bad&client_id=oaiapp_1"), "st", null, out kept) != null
              && TextChatGpt.Callback(TextChatGpt.Query("/auth/callback?error=access_denied&state=st"), "st", null, out kept).Contains("cancelled")
              && TextChatGpt.Callback(TextChatGpt.Query("/auth/callback?code=c&state=st"), "st", null, out kept).Contains("registration")
              && TextChatGpt.Callback(TextChatGpt.Query("/auth/callback?code=c&state=st&client_id=oaiapp_2"), "st", "oaiapp_1", out kept).Contains("another app")
              && TextChatGpt.Callback(TextChatGpt.Query("/auth/callback?code=c&state=st&client_id=oaiapp_1"), "st", null, out kept) == null && kept == "oaiapp_1"
              && TextChatGpt.Callback(TextChatGpt.Query("/auth/callback?code=c&state=st"), "st", "oaiapp_1", out kept) == null && kept == "oaiapp_1",
            "your own AI, ChatGPT: the callback is refused for another state, a cancelled sign-in, a registration with no client id, or another app's id; accepted otherwise");
        Dictionary<string, string> ex = TextChatGpt.Query("?" + TextChatGpt.ExchangeForm("oaiapp_1", "c o", "v", "http://127.0.0.1:1455/auth/callback"));
        Dictionary<string, string> rf = TextChatGpt.Query("?" + TextChatGpt.RefreshForm("oaiapp_1", "r"));
        Dictionary<string, string> rv = TextChatGpt.Query("?" + TextChatGpt.RevokeForm("oaiapp_1", "r"));
        Check(ex["grant_type"] == "authorization_code" && ex["code"] == "c o" && ex["code_verifier"] == "v" && ex["redirect_uri"] == "http://127.0.0.1:1455/auth/callback" && ex["resource"] == "https://api.openai.com/v1"
              && rf["grant_type"] == "refresh_token" && rf["client_id"] == "oaiapp_1" && rf["refresh_token"] == "r" && !rf.ContainsKey("scope") && rf["resource"] == "https://api.openai.com/v1"
              && rv["token"] == "r" && rv["token_type_hint"] == "refresh_token" && rv["client_id"] == "oaiapp_1",
            "your own AI, ChatGPT: the code's exchange with the verifier and the same address, the refresh with the kept client id and no scope, the revocation of the refresh token");
        TextTokens t = TextChatGpt.Tokens("{\"access_token\":\"a\",\"refresh_token\":\"r\",\"id_token\":\"i\",\"expires_in\":3600,\"scope\":\"openid chatgpt.tokens.use.direct\"}", 1000, true, out string why);
        Check(t != null && t.ExpiresAt == 4600 && TextChatGpt.Plan(t.Scope) && !TextChatGpt.Plan("openid profile") && TextChatGpt.Tokens("{\"access_token\":\"a\"}", 0, true, out why) == null
              && TextChatGpt.Tokens("{\"error\":\"invalid_grant\"}", 0, false, out why) == null && why.Contains("invalid_grant") && TextChatGpt.Gone("refresh_token_reused") && !TextChatGpt.Gone("server_error"),
            "your own AI, ChatGPT: the tokens read with the access token's end; the plan's use only when its scope is granted; a refused refresh told apart from a passing fault");
        using (RSA rsa = RSA.Create(2048))
        {
            string keys = Jwks(rsa, "k1");
            long now = 2000000000;
            string good = Jwt(rsa, "k1", "{\"iss\":\"https://auth.openai.com\",\"aud\":\"oaiapp_1\",\"exp\":" + (now + 600) + ",\"nonce\":\"n1\",\"sub\":\"user-1\",\"email\":\"a@b.c\"}");
            string listAud = Jwt(rsa, "k1", "{\"iss\":\"https://auth.openai.com\",\"aud\":[\"x\",\"oaiapp_1\"],\"exp\":" + (now + 600) + ",\"nonce\":\"n1\",\"sub\":\"user-1\"}");
            string[] parts = good.Split('.');
            string tampered = parts[0] + "." + TextChatGpt.Url64(Encoding.UTF8.GetBytes("{\"iss\":\"https://auth.openai.com\",\"aud\":\"oaiapp_1\",\"exp\":" + (now + 600) + ",\"nonce\":\"n1\",\"sub\":\"admin\"}")) + "." + parts[2];
            using (RSA other = RSA.Create(2048))
            {
                string strange = Jwt(other, "k1", "{\"iss\":\"https://auth.openai.com\",\"aud\":\"oaiapp_1\",\"exp\":" + (now + 600) + ",\"nonce\":\"n1\",\"sub\":\"user-1\"}");
                Check(TextChatGpt.CheckId(good, keys, "https://auth.openai.com", "oaiapp_1", "n1", now, out string sub, out string email) == null && sub == "user-1" && email == "a@b.c"
                      && TextChatGpt.CheckId(listAud, keys, "https://auth.openai.com", "oaiapp_1", "n1", now, out sub, out email) == null
                      && TextChatGpt.CheckId(tampered, keys, "https://auth.openai.com", "oaiapp_1", "n1", now, out sub, out email).Contains("signature")
                      && TextChatGpt.CheckId(strange, keys, "https://auth.openai.com", "oaiapp_1", "n1", now, out sub, out email).Contains("signature")
                      && TextChatGpt.CheckId(good, keys, "https://evil.example", "oaiapp_1", "n1", now, out sub, out email).Contains("issuer")
                      && TextChatGpt.CheckId(good, keys, "https://auth.openai.com", "oaiapp_2", "n1", now, out sub, out email).Contains("another app")
                      && TextChatGpt.CheckId(good, keys, "https://auth.openai.com", "oaiapp_1", "n2", now, out sub, out email).Contains("does not answer")
                      && TextChatGpt.CheckId(good, keys, "https://auth.openai.com", "oaiapp_1", "n1", now + 1000, out sub, out email).Contains("ended")
                      && TextChatGpt.CheckId("not.a", keys, "https://auth.openai.com", "oaiapp_1", "n1", now, out sub, out email) != null,
                    "your own AI, ChatGPT: the ID token passes only signed by OpenAI's published key, issued by OpenAI for this app, not ended and answering this sign-in's nonce");
            }
        }
    }

    // ---------------------------------------------------------------- Sign in with ChatGPT end to end, against a pretend sign-in server

    private static void ServiceSignIn()
    {
        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rlo-signin-" + Guid.NewGuid().ToString("N"));
        using (RSA rsa = RSA.Create(2048))
        {
            string issuer = null, nonce = null, lastRefresh = null;
            int refreshes = 0, revokes = 0;
            bool refuseRefresh = false;
            PretendHttp auth = null;
            auth = new PretendHttp((method, target, body) =>
            {
                Dictionary<string, string> f = TextChatGpt.Query("?" + body);
                if (target == "/.well-known/jwks.json")
                {
                    return new[] { "200 OK", Jwks(rsa, "k1") };
                }
                if (target == "/.well-known/openid-configuration")
                {
                    return new[] { "200 OK", "{\"issuer\":\"" + issuer + "\",\"revocation_endpoint\":\"" + issuer + "/oauth/revoke\"}" };
                }
                if (target == "/oauth/revoke")
                {
                    revokes++;
                    return new[] { "200 OK", string.Empty };
                }
                if (target == "/api/accounts/oauth/token" && f.TryGetValue("grant_type", out string grant))
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    if (grant == "authorization_code" && f["code"] == "the-code" && f["code_verifier"].Length == 43 && f["client_id"] == "oaiapp_new")
                    {
                        string id = Jwt(rsa, "k1", "{\"iss\":\"" + issuer + "\",\"aud\":\"oaiapp_new\",\"exp\":" + (now + 600) + ",\"nonce\":\"" + nonce + "\",\"sub\":\"user-9\",\"email\":\"the-player-email\"}");
                        return new[] { "200 OK", "{\"access_token\":\"access-1\",\"refresh_token\":\"refresh-1\",\"id_token\":\"" + id + "\",\"expires_in\":3600,\"scope\":\"openid profile email offline_access resource.invoke chatgpt.tokens.use.direct\"}" };
                    }
                    if (grant == "refresh_token")
                    {
                        refreshes++;
                        lastRefresh = f["refresh_token"];
                        return refuseRefresh ? new[] { "400 Bad Request", "{\"error\":\"invalid_grant\"}" }
                            : new[] { "200 OK", "{\"access_token\":\"access-2\",\"refresh_token\":\"refresh-2\",\"expires_in\":3600,\"scope\":\"chatgpt.tokens.use.direct\"}" };
                    }
                    return new[] { "400 Bad Request", "{\"error\":\"invalid_request\"}" };
                }
                return new[] { "404 Not Found", "{}" };
            });
            issuer = auth.Base;
            try
            {
                var web = new TextWeb();
                var keys = new TextKeys(folder);
                var s = new TextSignIn(web, keys, issuer) { DonePage = "<p>done</p>", FailedPage = "<p>failed</p>" };
                string url = s.Begin("Real Life Overhaul", 0);
                Dictionary<string, string> q = TextChatGpt.Query(url);
                nonce = q["nonce"];
                Check(url.StartsWith(issuer + "/api/accounts/authorize?", StringComparison.Ordinal) && q["client_id"] == "dynamic_agent_client" && keys.Get(TextChatGpt.KHost) == q["ext_agent_host_id"]
                      && s.State == TextSignInState.Waiting,
                    "your own AI, signing in: the browser's address made, this PC's host id kept before the first sign-in, the answer awaited");
                string page;
                using (var browser = new System.Net.Http.HttpClient())
                {
                    browser.GetStringAsync(q["redirect_uri"].Replace("/auth/callback", "/favicon.ico")).ContinueWith(_ => { }).Wait(3000);
                    page = browser.GetStringAsync(q["redirect_uri"] + "?code=the-code&state=" + Uri.EscapeDataString(q["state"]) + "&client_id=oaiapp_new&scope=x").Result;
                }
                Until(() => s.State != TextSignInState.Waiting, 5000);
                Check(s.State == TextSignInState.Done && page.Contains("<p>done</p>") && s.Email == "the-player-email" && s.First && keys.Get(TextChatGpt.KClient) == "oaiapp_new"
                      && keys.Get(TextChatGpt.KAccess) == "access-1" && keys.Get(TextChatGpt.KRefresh) == "refresh-1" && TextSignIn.SignedIn(keys),
                    "your own AI, signing in: the callback (after a stray request for the page's icon) exchanged, the ID token checked, the plan's scope granted, the app's client id and the tokens kept" + (s.Why != null ? " (" + s.Why + ")" : string.Empty));
                Check(TextSignIn.Access(web, keys, issuer, out TextFault fault, out string why) == "access-1" && refreshes == 0, "your own AI, signing in: the access token is used as it is while it has time left");
                keys.Set(TextChatGpt.KExpires, (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60).ToString());
                string fresh = TextSignIn.Access(web, keys, issuer, out fault, out why);
                Check(fresh == "access-2" && refreshes == 1 && lastRefresh == "refresh-1" && keys.Get(TextChatGpt.KRefresh) == "refresh-2",
                    "your own AI, signing in: near its end the access token is refreshed, and the rotated refresh token kept in place of the old");
                var s2 = new TextSignIn(web, keys, issuer);
                string url2 = s2.Begin("Real Life Overhaul", 0);
                Dictionary<string, string> q2 = TextChatGpt.Query(url2);
                s2.Cancel();
                Until(() => s2.State != TextSignInState.Waiting, 3000);
                Check(q2["client_id"] == "oaiapp_new" && q2["ext_agent_host_id"] == q["ext_agent_host_id"] && !q2.ContainsKey("agent_name_hint") && q2.ContainsKey("id_token_hint")
                      && s2.State == TextSignInState.Failed,
                    "your own AI, signing in again: the kept client id and host id, the ID token as a hint, no registration; a sign-in given up ends");
                refuseRefresh = true;
                keys.Set(TextChatGpt.KExpires, "1");
                Check(TextSignIn.Access(web, keys, issuer, out fault, out why) == null && fault == TextFault.Key && !keys.Has(TextChatGpt.KRefresh) && keys.Get(TextChatGpt.KClient) == "oaiapp_new",
                    "your own AI, signing in: a refresh refused for good clears the tokens (sign in again) and keeps the app's client id");
                keys.Set(TextChatGpt.KRefresh, "refresh-3", TextChatGpt.KAccess, "access-3", TextChatGpt.KExpires, "9999999999");
                bool confirmed = TextSignIn.SignOut(web, keys, issuer);
                Check(confirmed && revokes == 1 && !keys.Has(TextChatGpt.KRefresh) && !keys.Has(TextChatGpt.KAccess) && keys.Get(TextChatGpt.KClient) == "oaiapp_new" && keys.Has(TextChatGpt.KHost),
                    "your own AI, signing out: the refresh token revoked at the address OpenAI names, the tokens cleared, the client id and host id kept");
                var s3 = new TextSignIn(web, new TextKeys(folder + "-b"), issuer) { FailedPage = "<p>failed</p>" };
                string url3 = s3.Begin("Real Life Overhaul", 0);
                Dictionary<string, string> q3 = TextChatGpt.Query(url3);
                using (var browser = new System.Net.Http.HttpClient())
                {
                    page = browser.GetStringAsync(q3["redirect_uri"] + "?error=access_denied&state=" + Uri.EscapeDataString(q3["state"])).Result;
                }
                Until(() => s3.State != TextSignInState.Waiting, 3000);
                Check(s3.State == TextSignInState.Failed && s3.Why.Contains("cancelled") && page.Contains("<p>failed</p>"), "your own AI, signing in: the player saying no ends it without an exchange, and the page says so");
                foreach (string seen in auth.Seen)
                {
                    if (seen.Contains("GET /api/accounts/oauth/token"))
                    {
                        Check(false, "your own AI, signing in: a token was asked for with a GET");
                    }
                }
            }
            finally
            {
                auth.Dispose();
                foreach (string d in new[] { folder, folder + "-b" })
                {
                    try
                    {
                        Directory.Delete(d, true);
                    }
                    catch (Exception)
                    {
                        // (a temporary folder)
                    }
                }
            }
        }
    }

    // ---------------------------------------------------------------- no key or token anywhere it should not be

    private static void ServiceNoSecrets()
    {
        var hub = new TextsHub(new TextQueue(), new TextQueue()) { Service = new PretendService() };
        var keep = TextsAskOf("fp:70", "friend.crash", null, w => { });
        keep.Keep = true;
        hub.Ask(keep, 0);
        TextJob job = hub.ServiceQueue.Take(0.1, true);
        hub.ServiceQueue.Done(job, "Saw you met a bollard on 5th Ave.", 1.0, null);
        hub.Look(1.0);
        string saved = hub.Store.Write();
        Check(hub.Store.Words("fp:70") == "Saw you met a bollard on 5th Ave." && !saved.Contains("sk-") && !saved.Contains("Bearer") && !job.Card.Contains("sk-"),
            "your own AI, the save: the service's kept words go in the save's line as the pack's do, and nothing else of the service (no key, no token)");
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string service = File.ReadAllText(System.IO.Path.Combine(scripts, "Texts", "TextsDirector.Service.cs"));
        string runner = File.ReadAllText(System.IO.Path.Combine(scripts, "Texts", "TextService.cs")) + File.ReadAllText(System.IO.Path.Combine(scripts, "Texts", "TextSignIn.cs")) + File.ReadAllText(System.IO.Path.Combine(scripts, "Texts", "TextKeys.cs"));
        string settings = File.ReadAllText(System.IO.Path.Combine(scripts, "Core", "Settings.cs"));
        bool logsKey = false;
        foreach (string line in (service + runner).Split('\n'))
        {
            if ((line.Contains("Logger?.") || line.Contains("Log(") || line.Contains("_log?.")) && (line.Contains("Keys.Get(") || line.Contains("_start.Key") || line.Contains("url)") || line.Contains("+ url") || line.Contains("typed") || line.Contains("Access")))
            {
                logsKey = true;
                Console.WriteLine("  your own AI, a log line that may hold a secret: " + line.Trim());
            }
        }
        Check(!logsKey && !service.Contains("modData") && !runner.Contains("modData") && !Regex.IsMatch(settings, @"TextsService\w*\s*=\s*(Str|String)\(")
              && Regex.IsMatch(runner, @"private void Log\(string line\)\s*\{\s*_log\?\.Invoke\(Clean\(line\)\);"),
            "your own AI, nothing secret written: no log line names a key, a token, the typed key or the sign-in's address; nothing of it in a save; the options keep only the service's number; the runner's lines all go through Clean");
        Check(service.Contains("TextsGame.AskSecret(") && !Regex.IsMatch(service, @"AskSecret\([^;]*Keys\.Get"), "your own AI, the key's box: always empty, never filled with the kept key (it is never shown again)");
    }

    // ---------------------------------------------------------------- texts

    private static void ServiceTexts()
    {
        string folder = LocalesFolder();
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json"));
        Dictionary<string, string> pt = LoadWords(System.IO.Path.Combine(folder, "pt.json"));
        Dictionary<string, string> zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, "..", "Scripts"));
        string source = File.ReadAllText(System.IO.Path.Combine(scripts, "Texts", "TextsDirector.Service.cs")) + File.ReadAllText(System.IO.Path.Combine(scripts, "Texts", "TextsGuide.cs"));
        var missing = new List<string>();
        foreach (Match m in Regex.Matches(source, "\"(reallife_(textsai|texts_test|texts_status)[a-z0-9_]*)\""))
        {
            string k = m.Groups[1].Value;
            if (!en.ContainsKey(k) || !pt.ContainsKey(k) || !zh.ContainsKey(k))
            {
                missing.Add(k);
            }
        }
        foreach (string k in TextProviders.ChoiceKeys())
        {
            if (!en.ContainsKey(k) || !pt.ContainsKey(k) || !zh.ContainsKey(k))
            {
                missing.Add(k);
            }
        }
        foreach (string k in new[] { "reallife_opt_tip_texts_service", "reallife_opt_sec_textsai", "reallife_meta_title_textsai", "reallife_guide_textsai", "reallife_guide_textsai_about",
                     "reallife_opt_caption_enter", "reallife_opt_caption_sign_in", "reallife_opt_caption_forget", "reallife_contract_part_key_hidden" })
        {
            if (!en.ContainsKey(k) || !pt.ContainsKey(k) || !zh.ContainsKey(k))
            {
                missing.Add(k);
            }
        }
        Check(missing.Count == 0, "your own AI, texts: every key it shows is in en, pt and zh-cn (" + string.Join(", ", missing) + ")");
        string guide = en["reallife_guide_textsai"];
        Check(guide.Contains("{textsai_timeout}") && guide.Contains("{textsai_in_flight}") && guide.Contains("{textsai_per_minute}") && guide.Contains("# Going Public") && guide.Contains("# What is sent")
              && guide.Contains("Copilot") && guide.Contains("Claude subscription") && zh["reallife_guide_textsai"].Contains("{textsai_timeout}") && pt["reallife_guide_textsai"].Contains("{textsai_per_minute}"),
            "your own AI, the Guide: what is sent, the figures from the code, Going Public, and why GitHub Copilot and a Claude subscription are not offered");
        Check(!en["reallife_guide_texts"].Contains("It works offline. Nothing is sent anywhere.") && en["reallife_guide_texts"].Contains("Your own AI") && en["reallife_guide_texts_about"].Contains("Your own AI"),
            "your own AI, the Guide: RLO Texts' page no longer says nothing is ever sent, and points to Your own AI");
        var marks = new Dictionary<string, string> { { "en", "Your own AI: " }, { "pt", "Sua própria IA: " }, { "zh-cn", "你自己的 AI：" }, { "de", "Deine eigene KI: " }, { "fr", "Votre propre IA : " },
            { "es", "Tu propia IA: " }, { "ru", "Ваш собственный ИИ: " }, { "nl", "Je eigen AI: " }, { "tr", "Kendi yapay zekânız: " } };
        var lacking = new List<string>();
        foreach (KeyValuePair<string, string> kv in marks)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, kv.Key + ".json"));
            if (!AnyChangesRow(words, kv.Value) || !words["reallife_guide_texts"].Contains(kv.Key == "en" ? "Your own AI" : kv.Value.TrimEnd(' ', ':', '：')))
            {
                lacking.Add(kv.Key);
            }
        }
        Check(lacking.Count == 0, "your own AI, What's new and the Guide: its line in a What's new row (the next row until a release numbers it) of all nine languages, and RLO Texts' page mended in each (" + string.Join(", ", lacking) + ")");
        var longOnes = new List<string>();
        foreach (string k in new[] { "reallife_textsai_note_sending", "reallife_textsai_chatgpt_welcome", "reallife_textsai_chatgpt_limit", "reallife_textsai_status_ready", "reallife_textsai_key_kept",
                     "reallife_textsai_chatgpt_out_unconfirmed", "reallife_textsai_status_failed", "reallife_textsai_status_resting", "reallife_textsai_test_none" })
        {
            string filled = en[k].Replace("{service}", "Google Gemini").Replace("{model}", "gemini-3.8-flash").Replace("{why}", new string('w', 60)).Replace("{seconds}", "1.2")
                .Replace("{words}", "120").Replace("{fell}", "12").Replace("{text}", "");
            if (filled.Length > 240)
            {
                longOnes.Add(k + " " + filled.Length);
            }
        }
        Check(longOnes.Count == 0, "your own AI, texts: every notice within 240 characters with its values filled (" + string.Join(", ", longOnes) + ")");
    }

    // ---------------------------------------------------------------- the sources: cost, guard, the game's thread

    private static void ServiceSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts", "Texts"));
        string service = File.ReadAllText(System.IO.Path.Combine(scripts, "TextsDirector.Service.cs"));
        string director = File.ReadAllText(System.IO.Path.Combine(scripts, "TextsDirector.cs"));
        Check(Regex.IsMatch(service, @"if \(_serviceIndex == index && _serviceVersion == _version\)\s*\{\s*Watch\(\);\s*return;")
              && director.Contains("RunService();") && director.Contains("if (_pack == null && _service == null)"),
            "your own AI: the director's look costs two comparisons while the service is as the options say; nothing of it per frame");
        var gameFree = new[] { "TextJsonTree.cs", "TextProviders.cs", "TextServiceRules.cs", "TextWeb.cs", "TextKeys.cs", "TextChatGpt.cs", "TextSignIn.cs", "TextService.cs" };
        var problems = new List<string>();
        foreach (string file in gameFree)
        {
            string text = File.ReadAllText(System.IO.Path.Combine(scripts, file));
            if (text.Contains("using UnityEngine") || text.Contains("using System.Linq") || text.Contains("GameInstance") || text.Contains("Application."))
            {
                problems.Add(file);
            }
        }
        Check(problems.Count == 0, "your own AI: its rules, web client, keys, sign-in and runner are game-free and use no LINQ (" + string.Join(", ", problems) + ")");
        string game = File.ReadAllText(System.IO.Path.Combine(scripts, "TextsGame.cs"));
        string runner = File.ReadAllText(System.IO.Path.Combine(scripts, "TextService.cs"));
        Check(game.Contains("Application.OpenURL(url);") && game.Contains("field.contentType = TMP_InputField.ContentType.Password;") && game.Contains("field.contentType = _secretWas;")
              && game.Contains("field.text = string.Empty;") && runner.Contains("IsBackground = true") && service.Contains("ThreadPool.QueueUserWorkItem(_ => service.Dispose());")
              && service.IndexOf("service.Halt();", StringComparison.Ordinal) < service.IndexOf("ThreadPool.QueueUserWorkItem(_ => service.Dispose());", StringComparison.Ordinal) && service.Contains("service.Halt();"),
            "your own AI: the browser and the hidden key box are the Game file's; the box is put back as it was and emptied; the runner's threads are background ones, halted at once and ended off the game's thread");
    }

    private static void ServiceTiming()
    {
        TextProviderInfo router = TextProviders.Of(TextProvider.OpenRouter);
        string card = TextCard.Card(TextServer.TestCard());
        string answer = "{\"choices\":[{\"message\":{\"content\":\"Thinking of you, Alex. Hope the week is kind.\"}}]}";
        var watch = Stopwatch.StartNew();
        const int Rounds = 2000;
        for (int i = 0; i < Rounds; i++)
        {
            TextServiceRules.Build(router, router.Base, router.Model, TextCard.System, card, ServiceKey, false, false);
            TextServiceRules.Read(TextApi.Chat, Ans(200, answer));
        }
        double each = watch.Elapsed.TotalMilliseconds / Rounds;
        Check(each < 0.5, "your own AI: a request built and its answer read in " + each.ToString("0.000") + " ms (under 0.5 ms, on a worker thread)");
    }
}
