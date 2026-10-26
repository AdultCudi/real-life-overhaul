using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>Where the Player2 App stands on this PC, as the sign-in call and the health call tell it.</summary>
    public enum TextAppState
    {
        NotRunning,      // nothing answers on this PC's port 4315: the App is not installed or not started
        NoClientId,      // the App answers, but this build of the mod has no game client id to ask it for a key with
        NotSignedIn,     // the App answers and gave no key: the player is not signed in to it
        SignedIn         // the App gave a key
    }

    /// <summary>What the Test made of Player2: the steps it got through, and a line fit for the log.</summary>
    public sealed class TextPlayer2Report
    {
        public bool Pass;
        public TextAppState App;
        public bool Pinged;
        public int ReplyLength;
        public double Seconds;
        public string Why;        // for the notice; null on a pass
        public string Line;       // the Player.log line, PASS or FAIL, cleaned of the key and the client id
    }

    /// <summary>
    /// Player2, the platform the player's own Player2 App signs in to, as its public API reference gives it (player2.game/api/api.yaml,
    /// version 1.0.0): the App on this PC hands a game its player's key at POST http://localhost:4315/v1/login/web/{game_client_id},
    /// and the key is then sent as "Authorization: Bearer" to https://api.player2.game/v1, whose /chat/completions follows the OpenAI chat
    /// API and whose /health is to be called about once a minute so the usage is counted for the game. The mod keeps the key in memory only
    /// (the reference: never store a user's credentials outside the user's device), so there is nothing to type, keep or forget.
    /// Game-free. COST: a few strings a call, on the worker threads.
    /// </summary>
    public static class TextPlayer2
    {
        /// <summary>The Player2 App's own address on this PC; the reference writes it with localhost.</summary>
        public const string AppBase = "http://localhost:4315/v1";

        /// <summary>Player2's own website, where its joules and plan are managed (the "usage" button opens it).</summary>
        public const string SiteUrl = "https://player2.game";

        /// <summary>
        /// The game client id Player2 gives a game that is registered in its Developer Dashboard (player2.game/profile/developer). EMPTY UNTIL THE
        /// OWNER REGISTERS Real Life Overhaul THERE AND PUTS THE ID HERE: with none, Player2 starts nothing and says so. A player's service.txt may
        /// hold a line "clientid.12=..." to try an id before it is built in. It is not secret by the reference, but it is kept out of every log line.
        /// </summary>
        public const string ClientId = "";

        public const int AppMs = 3000;           // milliseconds the App may take to answer a call on this PC
        public const int PingMs = 4000;          // ...the web health call
        public const int TestReplyMs = 15000;    // ...the Test's one reply, longer than a message gets so a slow free model still shows
        public const double BeatSeconds = 60.0;  // the reference asks for the health call about once a minute
        public const int KeyMost = 512;          // characters a key may be before it is not believed
        public const int SampleMost = 40;        // characters of the Test's reply that the log keeps

        public const string TestSystem = "Reply with one short friendly sentence.";
        public const string TestCard = "Say hello to a friend called Alex.";

        /// <summary>The App's health call, which only has to be answered for the App to count as running.</summary>
        public static TextWebRequest AppHealth() => new TextWebRequest { Method = "GET", Url = AppBase + "/health", Body = null };

        /// <summary>The call that asks the App for the signed-in player's key for this game.</summary>
        public static TextWebRequest Login(string clientId) => new TextWebRequest { Url = AppBase + "/login/web/" + Uri.EscapeDataString(clientId ?? string.Empty), Body = null };

        /// <summary>The web API's health call, which also tells that the key is accepted.</summary>
        public static TextWebRequest Health(string key)
        {
            return new TextWebRequest { Method = "GET", Url = TextProviders.Of(TextProvider.Player2).Base + "/health", Body = null }.Header("Authorization", "Bearer " + key);
        }

        /// <summary>The key of the App's answer: its "p2Key", when the answer is a success and the key is one a header can carry; else null.</summary>
        public static string Key(TextWebAnswer a)
        {
            if (a == null || a.Status < 200 || a.Status > 299)
            {
                return null;
            }
            string key = TextJsonTree.Text(TextJsonTree.Read(a.Body), "p2Key");
            if (string.IsNullOrEmpty(key) || key.Length > KeyMost)
            {
                return null;
            }
            for (int i = 0; i < key.Length; i++)
            {
                if (key[i] <= ' ' || key[i] > '~')
                {
                    return null;   // a space, a line break or anything past ASCII would not be a key, and could break a header
                }
            }
            return key;
        }

        /// <summary>The state the App's sign-in answer tells; a status of 0 is no App on this PC.</summary>
        public static TextAppState State(TextWebAnswer a, string clientId)
        {
            if (a == null || a.Status == 0)
            {
                return TextAppState.NotRunning;
            }
            if (string.IsNullOrEmpty(clientId))
            {
                return TextAppState.NoClientId;
            }
            return Key(a) != null ? TextAppState.SignedIn : TextAppState.NotSignedIn;
        }

        /// <summary>The reason for the log for a state that is not signed in.</summary>
        public static string Why(TextAppState state)
        {
            switch (state)
            {
                case TextAppState.NotRunning: return "the Player2 App is not running on this PC (nothing answers on port 4315)";
                case TextAppState.NoClientId: return "this build of the mod has no Player2 game client id yet";
                case TextAppState.NotSignedIn: return "the Player2 App is running but nobody is signed in to it";
                default: return null;
            }
        }

        /// <summary>The status look: is the App there, and is the player signed in to it. Two small calls on this PC; run on a worker.</summary>
        public static TextAppState Probe(ITextWeb web, string clientId, out string key)
        {
            key = null;
            TextWebAnswer health = web.Send(AppHealth(), AppMs);
            if (health == null || health.Status == 0)
            {
                return TextAppState.NotRunning;   // refused at once when the App is not started
            }
            if (string.IsNullOrEmpty(clientId))
            {
                return TextAppState.NoClientId;
            }
            TextWebAnswer login = web.Send(Login(clientId), AppMs);
            key = Key(login);
            return State(login, clientId);
        }

        /// <summary>
        /// The Test: the App found and signed in, the web health call answered, and one short reply read through the same request and
        /// answer rules as every message. The report's line is for Player.log: PASS or FAIL, the steps, the times, at most
        /// <see cref="SampleMost"/> characters of the reply, and never the prompt, the key or the client id.
        /// </summary>
        public static TextPlayer2Report Run(ITextWeb web, string clientId)
        {
            var report = new TextPlayer2Report();
            string key;
            report.App = Probe(web, clientId, out key);
            if (report.App != TextAppState.SignedIn)
            {
                return Fail(report, Why(report.App), clientId, null);
            }
            var watch = Stopwatch.StartNew();
            TextWebAnswer ping = web.Send(Health(key), PingMs);
            long pingMs = watch.ElapsedMilliseconds;
            if (ping == null || ping.Status < 200 || ping.Status > 299)
            {
                string why = ping == null || ping.Status == 0 ? "the Player2 web API does not answer" : "the Player2 web API answered " + ping.Status.ToString(CultureInfo.InvariantCulture)
                    + (ping.Status == 401 ? " (the key was not accepted)" : string.Empty);
                return Fail(report, why, clientId, key);
            }
            report.Pinged = true;
            TextProviderInfo p = TextProviders.Of(TextProvider.Player2);
            TextWebRequest request = TextServiceRules.Build(p, p.Base, p.Model, TestSystem, TestCard, key, true, false);
            watch.Restart();
            TextWebAnswer answer = web.Send(request, TestReplyMs);
            report.Seconds = watch.Elapsed.TotalSeconds;
            TextServiceResult result = TextServiceRules.Read(p.Api, answer, true);
            if (result.Fault != TextFault.None)
            {
                return Fail(report, result.Why ?? "no words came back", clientId, key);
            }
            report.Pass = true;
            report.ReplyLength = result.Words.Length;
            string sample = TextServiceRules.Clean(result.Words.Replace('\n', ' ').Replace('\r', ' '), key, clientId);   // cleaned before it is cut, so no half of a key is left
            sample = sample.Length <= SampleMost ? sample : sample.Substring(0, SampleMost - 1) + "\u2026";
            report.Line = TextServiceRules.Clean("Player2 test PASS: the App is running and signed in; the web API answered its health call in "
                + pingMs.ToString(CultureInfo.InvariantCulture) + " ms; a reply of " + report.ReplyLength.ToString(CultureInfo.InvariantCulture) + " characters came in "
                + report.Seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s (\"" + sample + "\").", key, clientId);
            return report;
        }

        private static TextPlayer2Report Fail(TextPlayer2Report report, string why, string clientId, string key)
        {
            report.Pass = false;
            report.Why = TextServiceRules.Clean(why, key, clientId);
            report.Line = TextServiceRules.Clean("Player2 test FAIL: " + why + ".", key, clientId);
            return report;
        }
    }
}
