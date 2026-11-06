using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using RealLifeOverhaul.Texts;

// Checks for Player2 as one of Your own AI's services (Scripts/Texts: TextPlayer2, and the Player2 entry of TextProviders, the
// chat request without a model, the 402 of the credits, the runner's App key and heartbeat, the Test). They are built against the
// format of Player2's public API reference (player2.game/api/api.yaml, version 1.0.0; the mod's not-in-repo/plans/player2.md
// quotes it): POST http://localhost:4315/v1/login/web/{game_client_id} answers {"p2Key": ...}; POST {web}/chat/completions follows
// the OpenAI chat API with "Authorization: Bearer <p2Key>" and has no model field; GET {web}/health is the usage ping. Nothing
// here reaches the network: every call is answered by a pretend web.
internal static partial class Program
{
    private const string P2Id = "client-id-0123456789-test";                 // a pretend game client id: never in a log line
    private const string P2Key = "p2key-0123456789abcdefSECRETkeyABCDEFGH";   // a pretend player key: never in a log line, a reason or a request but its header
    private const string P2Chat = "{\"id\":\"chatcmpl-1\",\"object\":\"chat.completion\",\"created\":1677648672,\"model\":\"p2\",\"choices\":[{\"index\":0,\"finish_reason\":\"stop\","
                                  + "\"message\":{\"role\":\"assistant\",\"content\":\"Hello Alex, good to see you.\"}}],\"usage\":{\"completion_tokens\":6,\"prompt_tokens\":25,\"total_tokens\":31}}";

    private static void Player2Checks()
    {
        P2Table();
        P2Requests();
        P2Answers();
        P2Probe();
        P2Runner();
        P2Test();
        P2Secrets();
        P2Texts();
        P2Sources();
    }

    private static string P2Login(string id) => "http://localhost:4315/v1/login/web/" + id;

    // A pretend Player2: the App's health and login on this PC, and the web API's chat and health. Counts what it was asked.
    private sealed class PretendPlayer2
    {
        public bool AppRunning = true, SignedIn = true;
        public int ChatStatus = 200;
        public string ChatBody = P2Chat;
        public int Logins, Chats, Beats, AppHealths;
        public readonly List<TextWebRequest> Chat = new List<TextWebRequest>();

        public TextWebAnswer Answer(TextWebRequest r, int ms)
        {
            if (r.Url.StartsWith("http://localhost:4315/v1/", StringComparison.Ordinal))
            {
                if (!AppRunning)
                {
                    return new TextWebAnswer { Failure = "no connection" };
                }
                if (r.Url.EndsWith("/health", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref AppHealths);
                    return Ans(200, "{\"client_version\":\"1.0.0\"}");
                }
                Interlocked.Increment(ref Logins);
                return SignedIn ? Ans(200, "{\"p2Key\":\"" + P2Key + "\"}") : Ans(401, "{\"message\":\"not signed in\"}");
            }
            if (r.Url.EndsWith("/v1/health", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref Beats);
                return Header(r, "Authorization") == "Bearer " + P2Key ? Ans(200, "{}") : Ans(401, "{\"message\":\"unauthorized\"}");
            }
            Interlocked.Increment(ref Chats);
            lock (Chat)
            {
                Chat.Add(r);
            }
            return Ans(ChatStatus, ChatBody);
        }
    }

    // ---------------------------------------------------------------- the table

    private static void P2Table()
    {
        TextProviderInfo p = TextProviders.Of(TextProvider.Player2);
        Check((int)p.Id == 12 && p.Name == "Player2" && p.Api == TextApi.Chat && p.Access == TextAccess.App && p.Base == "https://api.player2.game/v1" && p.NoModel && p.Heartbeat
              && TextProviders.Forgets(p) == TextForget.Nothing && TextProviders.Base(p, "http://127.0.0.1:1/v1") == p.Base && TextProviders.ChoiceKeys()[12] == "reallife_textsai_provider_12",
            "player2, the table: the thirteenth entry, the chat API at api.player2.game/v1 with no model named, signed in through the App, a health call kept up, nothing to forget, and no address of the player's replaces it");
        Check(TextPlayer2.AppBase == "http://localhost:4315/v1" && TextPlayer2.BeatSeconds == 60.0,
            "player2, the App: on this PC's port 4315, the health call about once a minute as the reference asks");
        Check(Regex.IsMatch(TextPlayer2.ClientId, "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
            "player2, the game client id is built in and has the shape of a UUID");
    }

    // ---------------------------------------------------------------- the requests

    private static void P2Requests()
    {
        TextProviderInfo p = TextProviders.Of(TextProvider.Player2);
        TextWebRequest chat = TextServiceRules.Build(p, p.Base, p.Model, TextCard.System, "who: Dana\nlang: en", P2Key, false, false);
        JsonElement b = Body(chat);
        JsonElement m = b.GetProperty("messages");
        Check(chat.Url == "https://api.player2.game/v1/chat/completions" && chat.Method == "POST" && chat.ContentType == "application/json" && Header(chat, "Authorization") == "Bearer " + P2Key
              && chat.Headers.Count == 1 && !Has(b, "model") && !Has(b, "reasoning_effort") && !Has(b, "max_completion_tokens") && b.GetProperty("stream").GetBoolean() == false
              && b.GetProperty("max_tokens").GetInt32() == TextServiceRules.ReplyTokens && Math.Abs(b.GetProperty("temperature").GetDouble() - TextServiceRules.Temperature) < 1e-9
              && m.GetArrayLength() == 2 && m[0].GetProperty("role").GetString() == "system" && m[0].GetProperty("content").GetString() == TextCard.System
              && m[1].GetProperty("role").GetString() == "user" && m[1].GetProperty("content").GetString().StartsWith("who: Dana", StringComparison.Ordinal) && !chat.Body.Contains(P2Key),
            "player2, the chat request as the reference gives it: POST /chat/completions with a system and a user message, max_tokens, temperature and stream false, no model field, the key only in the Bearer header");
        TextWebRequest other = TextServiceRules.Build(TextProviders.Of(TextProvider.Groq), "https://api.groq.com/openai/v1", "m", "s", "c", "k", true, true);
        Check(Has(Body(other), "model") && Body(other).GetProperty("model").GetString() == "m" && other.Body.StartsWith("{\"model\":\"m\",\"messages\":[", StringComparison.Ordinal),
            "player2, the other services' chat request still names its model first, as before");
        TextWebRequest login = TextPlayer2.Login(P2Id);
        TextWebRequest odd = TextPlayer2.Login("a b/c");
        Check(login.Method == "POST" && login.Url == P2Login(P2Id) && login.Body == null && login.Headers.Count == 0 && odd.Url == P2Login("a%20b%2Fc"),
            "player2, the App's sign-in call: POST http://localhost:4315/v1/login/web/{game client id}, no body, the id escaped in the address");
        TextWebRequest health = TextPlayer2.Health(P2Key), app = TextPlayer2.AppHealth();
        Check(health.Method == "GET" && health.Url == "https://api.player2.game/v1/health" && Header(health, "Authorization") == "Bearer " + P2Key && health.Body == null
              && app.Method == "GET" && app.Url == "http://localhost:4315/v1/health" && app.Headers.Count == 0,
            "player2, the health calls: the web API's with the Bearer key, the App's with none");
    }

    // ---------------------------------------------------------------- the answers

    private static void P2Answers()
    {
        TextServiceResult ok = TextServiceRules.Read(TextApi.Chat, Ans(200, P2Chat), true);
        TextServiceResult nul = TextServiceRules.Read(TextApi.Chat, Ans(200, "{\"choices\":[{\"index\":0,\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":null}}]}"), true);
        TextServiceResult credits = TextServiceRules.Read(TextApi.Chat, Ans(402, "insufficient credits"), true);
        TextServiceResult plain402 = TextServiceRules.Read(TextApi.Chat, Ans(402, "{\"error\":{\"message\":\"pay\"}}"));
        TextServiceResult key = TextServiceRules.Read(TextApi.Chat, Ans(401, "{\"message\":\"The user must be authenticated.\"}"), true);
        TextServiceResult rate = TextServiceRules.Read(TextApi.Chat, Ans(429, "Too many requests", 12), true);
        TextServiceResult busy = TextServiceRules.Read(TextApi.Chat, Ans(500, "{\"message\":\"Internal server error\"}"), true);
        TextServiceResult gone = TextServiceRules.Read(TextApi.Chat, null, true);
        Check(ok.Fault == TextFault.None && ok.Words == "Hello Alex, good to see you." && nul.Fault == TextFault.NoWords && credits.Fault == TextFault.UsageLimit && plain402.Fault != TextFault.UsageLimit
              && key.Fault == TextFault.Key && key.Why.Contains("must be authenticated") && rate.Fault == TextFault.RateLimited && busy.Fault == TextFault.Busy && gone.Fault == TextFault.Network,
            "player2, the chat answer as the reference gives it: choices[0].message.content is the words (null is none), 402 insufficient credits is the long rest only for Player2, 401 the key, 429 the rate, 500 busy, nothing a lost connection");
        Check(TextServiceRules.Rest(TextFault.UsageLimit, 1, 0) == TextServiceRules.RestUsage && TextServiceRules.Rest(TextFault.RateLimited, 1, 12) >= 12,
            "player2, the rests: joules used up rest as long as a ChatGPT allowance, too many requests as long as the service asks");
    }

    // ---------------------------------------------------------------- the App's sign-in and the status

    private static void P2Probe()
    {
        var gone = new PretendPlayer2 { AppRunning = false };
        var out1 = new PretendPlayer2 { SignedIn = false };
        var on = new PretendPlayer2();
        string key;
        TextAppState s1 = TextPlayer2.Probe(new PretendWeb { Answer = gone.Answer }, P2Id, out key);
        TextAppState s2 = TextPlayer2.Probe(new PretendWeb { Answer = out1.Answer }, P2Id, out key);
        string none = key;
        TextAppState s3 = TextPlayer2.Probe(new PretendWeb { Answer = on.Answer }, P2Id, out key);
        TextAppState s4 = TextPlayer2.Probe(new PretendWeb { Answer = on.Answer }, "", out string noKey);
        Check(s1 == TextAppState.NotRunning && gone.Logins == 0 && s2 == TextAppState.NotSignedIn && none == null && s3 == TextAppState.SignedIn && key == P2Key
              && s4 == TextAppState.NoClientId && noKey == null && on.Logins == 1 && on.AppHealths == 2,
            "player2, the status: the App not found (no sign-in is even tried), found but nobody signed in, found and signed in with the key, found but the mod has no game client id");
        Check(TextPlayer2.Key(Ans(200, "{\"p2Key\":\"abc def\"}")) == null && TextPlayer2.Key(Ans(200, "{\"p2Key\":\"abc\\ndef\"}")) == null && TextPlayer2.Key(Ans(200, "{\"p2Key\":\"" + new string('k', 600) + "\"}")) == null
              && TextPlayer2.Key(Ans(200, "{}")) == null && TextPlayer2.Key(Ans(500, "{\"p2Key\":\"abcdef\"}")) == null && TextPlayer2.Key(Ans(200, "{\"p2Key\":\"abcdef\"}")) == "abcdef",
            "player2, the key: taken only from a success, one a header can carry (no space, line break or past ASCII, not very long)");
    }

    // ---------------------------------------------------------------- the runner: the key, the heartbeat, the fallback

    private static TextService P2Service(PretendPlayer2 web, TextQueue q, List<string> logs, string id = P2Id)
    {
        TextProviderInfo p = TextProviders.Of(TextProvider.Player2);
        Action<string> log = s => { lock (logs) { logs.Add(s); } };
        return new TextService(q, new TextServiceStart { Provider = p, Base = p.Base, ClientId = id, Workers = 1 }, new PretendWeb { Answer = web.Answer }, log);
    }

    private static void P2Runner()
    {
        var logs = new List<string>();
        var web = new PretendPlayer2();
        var q = new TextQueue();
        TextService run = P2Service(web, q, logs);
        run.Start();
        TextJob first = ServiceJob(q, "p1", 20);
        run.Nudge();
        Until(() => first.State == TextJobState.Written, 3000);
        TextJob second = ServiceJob(q, "p2", 20);
        run.Nudge();
        Until(() => second.State == TextJobState.Written, 3000);
        Thread.Sleep(100);
        Check(run.State == TextServerState.Ready && first.Raw == "Hello Alex, good to see you." && second.State == TextJobState.Written && web.Logins == 1 && web.Chats == 2 && web.Beats == 1
              && Header(web.Chat[0], "Authorization") == "Bearer " + P2Key && !web.Chat[0].Body.Contains("\"model\"") && run.AppState == TextAppState.SignedIn,
            "player2, the runner: the App is asked for the key once (held in memory, not asked again), each card goes to the chat with it, and the health call goes once a minute, not with every reply (" + web.Logins + " sign-ins, "
            + web.Chats + " chats, " + web.Beats + " health)");
        run.Dispose();

        var off = new PretendPlayer2 { AppRunning = false };
        q = new TextQueue();
        run = P2Service(off, q, logs);
        run.Start();
        TextJob miss = ServiceJob(q, "m1", 20);
        run.Nudge();
        Until(() => run.Resting, 3000);
        TextJob later = ServiceJob(q, "m2", 20);
        Until(() => later.State != TextJobState.Waiting, 2000);
        Check(run.State == TextServerState.Ready && run.Resting && run.Fault == TextFault.Network && run.AppState == TextAppState.NotRunning && miss.State == TextJobState.Missed && later.State == TextJobState.Missed
              && off.Chats == 0 && run.Why.Contains("not running"),
            "player2, the fallback: with the App not running the service rests like a lost connection (not a stop: the App may be started later), what waits is missed at once so the hub falls back to the pack or the written lines, and no card is sent");
        run.Dispose();

        var out2 = new PretendPlayer2 { SignedIn = false };
        q = new TextQueue();
        run = P2Service(out2, q, logs);
        run.Start();
        TextJob w1 = ServiceJob(q, "o1", 20);
        run.Nudge();
        Until(() => run.Resting, 3000);
        Check(run.Resting && run.AppState == TextAppState.NotSignedIn && w1.State == TextJobState.Missed && out2.Chats == 0 && run.Why.Contains("nobody is signed in"),
            "player2, the fallback: with nobody signed in to the App it rests and falls back the same way, and says why");
        run.Dispose();

        var credit = new PretendPlayer2 { ChatStatus = 402, ChatBody = "insufficient credits" };
        q = new TextQueue();
        run = P2Service(credit, q, logs);
        run.Start();
        TextJob j = ServiceJob(q, "c1", 20);
        run.Nudge();
        Until(() => run.Resting, 3000);
        Check(run.Resting && run.Fault == TextFault.UsageLimit && j.State == TextJobState.Missed && run.State == TextServerState.Ready,
            "player2, the fallback: joules used up (402) rest the service long, the pack or the written lines meanwhile, and the player is told once (UsageLimit)");
        run.Dispose();

        var lapsed = new PretendPlayer2 { ChatStatus = 401, ChatBody = "{\"message\":\"The user must be authenticated.\"}" };
        q = new TextQueue();
        run = P2Service(lapsed, q, logs);
        run.Start();
        j = ServiceJob(q, "l1", 20);
        run.Nudge();
        Until(() => run.Resting, 3000);
        Check(run.State == TextServerState.Ready && run.Resting && run.Fault == TextFault.Network && j.State == TextJobState.Missed,
            "player2, the key lapsing: a refused key is not a stop (the App is asked again after a rest), unlike another service's refused key");
        run.Dispose();

        var web2 = new PretendPlayer2();
        q = new TextQueue();
        run = P2Service(web2, q, logs, null);
        run.Start();
        Check(run.State == TextServerState.Failed && run.Why.Contains("client id") && web2.Logins == 0 && web2.Chats == 0 && web2.AppHealths == 0,
            "player2, no game client id in the mod yet: it stops at once, says so, and asks nobody anything");
        run.Dispose();
    }

    // ---------------------------------------------------------------- the Test

    private static void P2Test()
    {
        var web = new PretendPlayer2();
        TextPlayer2Report ok = TextPlayer2.Run(new PretendWeb { Answer = web.Answer }, P2Id);
        Check(ok.Pass && ok.App == TextAppState.SignedIn && ok.Pinged && ok.ReplyLength == "Hello Alex, good to see you.".Length && ok.Line.StartsWith("Player2 test PASS:", StringComparison.Ordinal)
              && ok.Line.Contains("Hello Alex, good to see you.") && web.Chats == 1 && web.Beats == 1 && ok.Why == null,
            "player2, the Test: the App found and signed in, the web health call answered, one reply read; PASS with a short sample in the log line");
        var longReply = new PretendPlayer2 { ChatBody = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"" + new string('x', 300) + "\"}}]}" };
        TextPlayer2Report cut = TextPlayer2.Run(new PretendWeb { Answer = longReply.Answer }, P2Id);
        Check(cut.Pass && cut.ReplyLength == 300 && cut.Line.Length < 260 && !cut.Line.Contains(new string('x', 41)) && !cut.Line.Contains(TextPlayer2.TestCard) && !cut.Line.Contains(TextPlayer2.TestSystem),
            "player2, the Test's log line: at most " + TextPlayer2.SampleMost + " characters of the reply, never the prompt");
        var gone = new PretendPlayer2 { AppRunning = false };
        var bad = new PretendPlayer2 { ChatStatus = 500, ChatBody = "{\"message\":\"Internal server error\"}" };
        var rude = new PretendPlayer2 { ChatBody = "<html>proxy</html>" };
        TextPlayer2Report f1 = TextPlayer2.Run(new PretendWeb { Answer = gone.Answer }, P2Id);
        TextPlayer2Report f2 = TextPlayer2.Run(new PretendWeb { Answer = bad.Answer }, P2Id);
        TextPlayer2Report f3 = TextPlayer2.Run(new PretendWeb { Answer = rude.Answer }, P2Id);
        TextPlayer2Report f4 = TextPlayer2.Run(new PretendWeb { Answer = web.Answer }, "");
        var noPing = new PretendWeb { Answer = (r, ms) => r.Url.EndsWith("/v1/health", StringComparison.Ordinal) && r.Url.StartsWith("https", StringComparison.Ordinal) ? Ans(503, "") : web.Answer(r, ms) };
        TextPlayer2Report f5 = TextPlayer2.Run(noPing, P2Id);
        Check(!f1.Pass && f1.App == TextAppState.NotRunning && f1.Line.StartsWith("Player2 test FAIL:", StringComparison.Ordinal) && f1.Line.Contains("not running")
              && !f2.Pass && f2.Pinged && f2.Line.Contains("500") && !f3.Pass && f3.Pinged && !f4.Pass && f4.App == TextAppState.NoClientId && f4.Line.Contains("client id")
              && !f5.Pass && !f5.Pinged && f5.Line.Contains("503"),
            "player2, the Test fails with a plain reason for each step: the App not found, no client id, the web API's health call refused, an error from the chat, an answer that is not words");
    }

    // ---------------------------------------------------------------- nothing secret

    private static void P2Secrets()
    {
        string real = TextPlayer2.ClientId;
        var realLogs = new List<string>();
        var realWeb = new PretendPlayer2 { ChatStatus = 400, ChatBody = "{\"message\":\"bad request for " + real + "\"}" };
        var realQ = new TextQueue();
        TextService realRun = P2Service(realWeb, realQ, realLogs, real);
        realRun.Start();
        TextJob realJob = ServiceJob(realQ, "s0", 20);
        realRun.Nudge();
        Until(() => realJob.State == TextJobState.Missed, 3000);
        Thread.Sleep(50);
        var realSpoken = new List<string>(realLogs) { realJob.Why ?? string.Empty, realRun.Why ?? string.Empty };
        realRun.Dispose();
        TextPlayer2Report realReport = TextPlayer2.Run(new PretendWeb { Answer = new PretendPlayer2 { ChatBody = "{\"choices\":[{\"message\":{\"content\":\"hello " + real + "\"}}]}" }.Answer }, real);
        realSpoken.Add(realReport.Line);
        realSpoken.Add(realReport.Why ?? string.Empty);
        bool realLeaked = false;
        foreach (string line in realSpoken)
        {
            realLeaked |= line.Contains(real);
        }
        string kept = new TextsStore().Write() ?? string.Empty;
        Check(!realLeaked && !kept.Contains(real) && TextServiceRules.Clean("id " + real, real) == "id [hidden]",
            "player2, the built-in game client id never appears in a log line, a reason, the Test's lines or the kept words, even when the service or the reply repeats it");
        var logs = new List<string>();
        var web = new PretendPlayer2 { ChatStatus = 400, ChatBody = "{\"message\":\"bad request for " + P2Key + " by " + P2Id + "\"}" };
        var q = new TextQueue();
        TextService run = P2Service(web, q, logs);
        run.Start();
        TextJob job = ServiceJob(q, "s1", 20);
        run.Nudge();
        Until(() => job.State == TextJobState.Missed, 3000);
        Thread.Sleep(50);
        var spoken = new List<string>(logs) { job.Why ?? string.Empty, run.Why ?? string.Empty };
        run.Dispose();
        foreach (PretendPlayer2 p in new[] { new PretendPlayer2 { ChatBody = "<html>" + P2Key + "</html>" }, new PretendPlayer2 { AppRunning = false }, new PretendPlayer2 { SignedIn = false }, new PretendPlayer2 { ChatStatus = 402 } })
        {
            TextPlayer2Report r = TextPlayer2.Run(new PretendWeb { Answer = p.Answer }, P2Id);
            spoken.Add(r.Line);
            spoken.Add(r.Why ?? string.Empty);
        }
        TextPlayer2Report echo = TextPlayer2.Run(new PretendWeb { Answer = new PretendPlayer2 { ChatBody = "{\"choices\":[{\"message\":{\"content\":\"" + P2Key + " and " + P2Id + "\"}}]}" }.Answer }, P2Id);
        spoken.Add(echo.Line);
        bool leaked = false;
        foreach (string line in spoken)
        {
            leaked |= line.Contains(P2Key) || line.Contains(P2Id);
        }
        Check(!leaked && spoken.Count >= 9 && echo.Pass,
            "player2, nothing secret in a log line: neither the player's key nor the game client id is in the runner's log lines, its reasons, or the Test's lines, even when the service or the reply repeats them");
        string key = Regex.Replace(TextServiceRules.Clean("sent " + P2Key + " now", P2Key), "\\s+", " ");
        Check(key == "sent [hidden] now" && TextServiceRules.Clean("Authorization: Bearer abc.def-ghi", P2Id) == "Authorization: Bearer [hidden]",
            "player2, the cleaner: a key given to it, or the word after Bearer, becomes [hidden]");
    }

    // ---------------------------------------------------------------- texts

    private static void P2Texts()
    {
        string folder = LocalesFolder();
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json"));
        Dictionary<string, string> pt = LoadWords(System.IO.Path.Combine(folder, "pt.json"));
        Dictionary<string, string> zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, "..", "Scripts", "Texts"));
        string source = File.ReadAllText(System.IO.Path.Combine(scripts, "TextsDirector.Service.cs")) + File.ReadAllText(System.IO.Path.Combine(scripts, "TextsGuide.cs"));
        var missing = new List<string>();
        foreach (Match m in Regex.Matches(source, "\"(reallife_textsai_(p2|provider)[a-z0-9_]*)\""))
        {
            string k = m.Groups[1].Value;
            if (!en.ContainsKey(k) || !pt.ContainsKey(k) || !zh.ContainsKey(k))
            {
                missing.Add(k);
            }
        }
        Check(missing.Count == 0 && en.ContainsKey("reallife_textsai_provider_12"), "player2, texts: every key it shows is in en, pt and zh-cn (" + string.Join(", ", missing) + ")");
        var longOnes = new List<string>();
        foreach (string k in new[] { "reallife_textsai_p2_nokey", "reallife_textsai_p2_signin", "reallife_textsai_p2_joules", "reallife_textsai_p2_state_missing", "reallife_textsai_p2_state_signin",
                     "reallife_textsai_p2_state_noid", "reallife_textsai_p2_test_pass", "reallife_textsai_p2_test_fail", "reallife_textsai_p2_status" })
        {
            string state = en["reallife_textsai_p2_state_ok"];
            string line = en["reallife_textsai_status_ready"].Replace("{service}", "Player2").Replace("{model}", en["reallife_textsai_p2_model"]).Replace("{seconds}", "12.5").Replace("{words}", "120").Replace("{fell}", "12");
            string filled = en[k].Replace("{why}", new string('w', 160)).Replace("{seconds}", "12.5").Replace("{state}", state).Replace("{line}", line);
            if (filled.Length > 240)
            {
                longOnes.Add(k + " " + filled.Length);
            }
        }
        Check(longOnes.Count == 0, "player2, texts: every notice within 240 characters with its values filled (" + string.Join(", ", longOnes) + ")");
        var lacking = new List<string>();
        foreach (string lang in new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, lang + ".json"));
            string row = null;
            foreach (KeyValuePair<string, string> kv in words)
            {
                if (kv.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !kv.Key.EndsWith("_title", StringComparison.Ordinal) && kv.Value.Contains("Player2"))
                {
                    row = kv.Value;
                }
            }
            if (row == null || row.Contains("  ") || row.Contains("!") || row.Contains((char)0x2014) || row.Contains((char)0x2013))
            {
                lacking.Add(lang);
            }
        }
        Check(lacking.Count == 0, "player2, What's new: its line (never repeated: one row holds it once) in a What's new row of all nine languages, no double space, no exclamation mark, no dash (" + string.Join(", ", lacking) + ")");
        int count = 0;
        foreach (KeyValuePair<string, string> kv in en)
        {
            if (kv.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !kv.Key.EndsWith("_title", StringComparison.Ordinal))
            {
                int at = 0;
                while ((at = kv.Value.IndexOf("Player2 joins Your own AI.", at, StringComparison.Ordinal)) >= 0)
                {
                    count++;
                    at++;
                }
            }
        }
        string guide = en["reallife_guide_textsai"];
        string zhGuide = zh["reallife_guide_textsai"], ptGuide = pt["reallife_guide_textsai"];
        Check(count == 1 && guide.Contains("Player2") && guide.Contains("player2.game") && guide.Contains("joules") && guide.Contains("not running")
              && zhGuide.Contains("Player2") && zhGuide.Contains("player2.game") && ptGuide.Contains("Player2") && ptGuide.Contains("player2.game")
              && zhGuide.Split('\n').Length == guide.Split('\n').Length && ptGuide.Split('\n').Length == guide.Split('\n').Length && !guide.Contains("  "),
            "player2, the Guide's AI page: how to use it (install the App from player2.game, sign in, pick Player2), what it costs (joules), the fallback, in en, pt and zh-cn with the page's lines unchanged in number; its What's new line once");
    }

    // ---------------------------------------------------------------- the sources

    private static void P2Sources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts", "Texts"));
        string p2 = File.ReadAllText(System.IO.Path.Combine(scripts, "TextPlayer2.cs"));
        string service = File.ReadAllText(System.IO.Path.Combine(scripts, "TextsDirector.Service.cs"));
        string guide = File.ReadAllText(System.IO.Path.Combine(scripts, "TextsGuide.cs"));
        string runner = File.ReadAllText(System.IO.Path.Combine(scripts, "TextService.cs"));
        string director = File.ReadAllText(System.IO.Path.Combine(scripts, "TextsDirector.cs"));
        Check(!p2.Contains("using UnityEngine") && !p2.Contains("using System.Linq") && !p2.Contains("modData") && !Regex.IsMatch(runner, @"Keys\.Set\([^;]*_appKey") && !service.Contains("Keys.Set(KeyName((int)TextProvider.Player2"),
            "player2: its rules are game-free with no LINQ, and the key is never handed to the keys' file or a save");
        Check(guide.Contains(".Test(\"reallife_textsai_p2_test\", TextsDirector.TestPlayer2)") && service.Contains("Interlocked.CompareExchange(ref _p2Busy, 1, 0)") && service.Contains("ThreadPool.QueueUserWorkItem(_ =>")
              && director.Contains("PlayerTwoLook();") && !Regex.IsMatch(p2, @"\b(Update|LateUpdate|FixedUpdate)\b"),
            "player2: the Test is in Options > Tests, asks on a worker thread one at a time, and is told on the game's thread at the director's look; nothing per frame");
    }
}
