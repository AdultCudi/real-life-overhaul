using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RealLifeOverhaul.Texts
{
    /// <summary>Where a sign-in stands.</summary>
    public enum TextSignInState
    {
        Waiting,     // the browser is open; the callback is awaited
        Done,        // signed in: the tokens are kept
        Failed       // not signed in, with why
    }

    /// <summary>
    /// One Sign in with ChatGPT by TextChatGpt's rules: a callback listener on 127.0.0.1, then on a thread of its own the
    /// wait for the callback, the code's exchange, the ID token and the grant checked, and the tokens kept in
    /// <see cref="TextKeys"/>. Also the refresh of a kept sign-in and signing out, one at a time (<see cref="Gate"/>).
    /// The caller opens the browser. Game-free. COST: nothing on the game's thread but <see cref="Begin"/>'s listener.
    /// </summary>
    public sealed class TextSignIn
    {
        public const double WaitSeconds = 300.0;   // seconds the browser has to come back, through the sign-in and the consent
        public const int ReadMs = 3000;            // milliseconds a callback's request may take to arrive once connected
        public const int CallMs = 10000;           // milliseconds each of the sign-in's own calls may take

        /// <summary>Refreshing and signing out one at a time, for every runner and sign-in: the refresh token rotates.</summary>
        public static readonly object Gate = new object();

        private readonly ITextWeb _web;
        private readonly TextKeys _keys;
        private readonly string _auth;
        private TcpListener _listener;
        private volatile bool _stop;
        private string _state, _nonce, _verifier, _redirect, _kept, _host;

        public TextSignIn(ITextWeb web, TextKeys keys, string auth = TextProviders.ChatGptAuth)
        {
            _web = web;
            _keys = keys;
            _auth = auth;
        }

        public volatile TextSignInState State = TextSignInState.Waiting;

        /// <summary>Why it failed; null otherwise.</summary>
        public string Why { get; private set; }

        /// <summary>The account signed in to, as its ID token names it, for the notice.</summary>
        public string Email { get; private set; }

        /// <summary>True for the first sign-in to a ChatGPT account on this PC: the welcome is shown once.</summary>
        public bool First { get; private set; }

        /// <summary>The page the browser shows after the callback and after a failed one, made by the caller in the player's language.</summary>
        public string DonePage = "<p>OK</p>", FailedPage = "<p>Not signed in</p>";

        /// <summary>Starts the listener and the wait on its own thread; the address for the browser, or null when no port could be had.</summary>
        public string Begin(string appName, int port = TextChatGpt.Port)
        {
            _kept = _keys.Get(TextChatGpt.KClient);
            _host = _keys.Get(TextChatGpt.KHost);
            if (_host == null)
            {
                _host = TextChatGpt.NewHost();
                if (!_keys.Set(TextChatGpt.KHost, _host))
                {
                    return Fail(_keys.Why ?? "the sign-in could not be kept on this PC");
                }
            }
            _state = TextChatGpt.Fresh();
            _nonce = TextChatGpt.Fresh();
            _verifier = TextChatGpt.Fresh();
            try
            {
                _listener = Listen(port) ?? Listen(0);
                if (_listener == null)
                {
                    return Fail("no port for the sign-in's answer on this PC");
                }
                _redirect = TextChatGpt.Redirect(((IPEndPoint)_listener.LocalEndpoint).Port);
            }
            catch (Exception e)
            {
                return Fail("no port for the sign-in's answer (" + e.GetType().Name + ")");
            }
            string url = TextChatGpt.AuthorizeUrl(_auth, _kept, appName, _host, _kept != null ? _keys.Get(TextChatGpt.KId) : null, _redirect, _state, _nonce, _verifier);
            new Thread(Wait) { IsBackground = true, Name = "RLO Texts sign-in", Priority = ThreadPriority.BelowNormal }.Start();
            return url;
        }

        /// <summary>Gives up the wait.</summary>
        public void Cancel()
        {
            _stop = true;
        }

        private static TcpListener Listen(int port)
        {
            try
            {
                var l = new TcpListener(IPAddress.Loopback, port);
                l.Start();
                return l;
            }
            catch (SocketException)
            {
                return null;
            }
        }

        private string Fail(string why)
        {
            Why = why;
            State = TextSignInState.Failed;
            return null;
        }

        private void Wait()
        {
            try
            {
                double until = TextClock.Now + WaitSeconds;
                while (!_stop && TextClock.Now < until)
                {
                    if (!_listener.Pending())
                    {
                        Thread.Sleep(200);
                        continue;
                    }
                    using (TcpClient client = _listener.AcceptTcpClient())
                    {
                        string target = Target(client);
                        if (target == null || !target.StartsWith(TextChatGpt.CallbackPath, StringComparison.Ordinal))
                        {
                            Answer(client, "404 Not Found", string.Empty);   // the browser's icon and the like: the wait goes on
                            continue;
                        }
                        string why = Exchange(TextChatGpt.Query(target));
                        Answer(client, "200 OK", why == null ? DonePage : FailedPage);
                        if (why != null)
                        {
                            Fail(why);
                        }
                        else
                        {
                            State = TextSignInState.Done;
                        }
                        return;
                    }
                }
                Fail(_stop ? "the sign-in was given up" : "the browser did not come back in time");
            }
            catch (Exception e)
            {
                Fail("the sign-in stopped (" + e.GetType().Name + ")");
            }
            finally
            {
                try
                {
                    _listener.Stop();
                }
                catch (Exception)
                {
                    // stopped already
                }
            }
        }

        // The callback request line's target, or null.
        private static string Target(TcpClient client)
        {
            NetworkStream net = client.GetStream();
            var all = new MemoryStream(1024);
            var buffer = new byte[2048];
            double until = TextClock.Now + ReadMs / 1000.0;
            while (all.Length < 16384)
            {
                double left = until - TextClock.Now;
                if (left <= 0 || !client.Client.Poll((int)(left * 1000000.0), SelectMode.SelectRead))
                {
                    break;
                }
                int read = net.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    break;
                }
                all.Write(buffer, 0, read);
                if (Encoding.ASCII.GetString(all.GetBuffer(), 0, (int)all.Length).Contains("\r\n\r\n"))
                {
                    break;
                }
            }
            string text = Encoding.ASCII.GetString(all.GetBuffer(), 0, (int)all.Length);
            int end = text.IndexOf("\r\n", StringComparison.Ordinal);
            string[] line = (end >= 0 ? text.Substring(0, end) : text).Split(' ');
            return line.Length >= 2 && line[0] == "GET" ? line[1] : null;
        }

        private static void Answer(TcpClient client, string status, string page)
        {
            string html = page.Length > 0
                ? "<!doctype html><html><head><meta charset=\"utf-8\"><title>Real Life Overhaul</title></head>"
                  + "<body style=\"font-family:sans-serif;margin:3em\">" + page + "</body></html>"
                : string.Empty;
            byte[] body = Encoding.UTF8.GetBytes(html);
            byte[] head = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + body.Length
                                                  + "\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");
            NetworkStream net = client.GetStream();
            net.Write(head, 0, head.Length);
            net.Write(body, 0, body.Length);
        }

        // The callback checked, the code exchanged, the ID token and the grant checked, the tokens kept; null when signed in.
        private string Exchange(Dictionary<string, string> q)
        {
            string why = TextChatGpt.Callback(q, _state, _kept, out string clientId);
            if (why != null)
            {
                return why;
            }
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            TextWebAnswer a = _web.Send(Form(_auth + TextChatGpt.TokenPath, TextChatGpt.ExchangeForm(clientId, q["code"], _verifier, _redirect)), CallMs);
            if (a.Status == 0)
            {
                return "the sign-in could not be finished (" + (a.Failure ?? "no answer") + ")";
            }
            TextTokens t = TextChatGpt.Tokens(a.Body, now, true, out why);
            if (t == null)
            {
                return why;
            }
            TextWebAnswer keys = _web.Send(new TextWebRequest { Method = "GET", Url = _auth + TextChatGpt.KeysPath, Body = null }, CallMs);
            if (keys.Status != 200)
            {
                return "OpenAI's keys for checking the sign-in could not be read";
            }
            why = TextChatGpt.CheckId(t.Id, keys.Body, _auth, clientId, _nonce, now, out string subject, out string email);
            if (why != null)
            {
                return why;
            }
            if (!TextChatGpt.Plan(t.Scope))
            {
                return "the sign-in did not allow this app to use your ChatGPT plan";
            }
            First = _keys.Get(TextChatGpt.KWelcomed) == null;
            Email = email;
            lock (Gate)
            {
                if (!_keys.Set(TextChatGpt.KClient, clientId, TextChatGpt.KId, t.Id, TextChatGpt.KAccess, t.Access, TextChatGpt.KRefresh, t.Refresh,
                        TextChatGpt.KExpires, t.ExpiresAt.ToString(System.Globalization.CultureInfo.InvariantCulture), TextChatGpt.KEmail, email,
                        TextChatGpt.KSubject, subject, TextChatGpt.KScope, t.Scope))
                {
                    return _keys.Why ?? "the sign-in could not be kept on this PC";
                }
            }
            return null;
        }

        private static TextWebRequest Form(string url, string form) =>
            new TextWebRequest { Url = url, Body = form, ContentType = "application/x-www-form-urlencoded" }.Header("Accept", "application/json");

        /// <summary>True when a sign-in is kept here: a client id and a refresh token.</summary>
        public static bool SignedIn(TextKeys keys) => keys.Has(TextChatGpt.KClient) && keys.Has(TextChatGpt.KRefresh);

        /// <summary>
        /// The access token to send now, refreshed when it ends soon. Null with <paramref name="fault"/> when there is none:
        /// Key when the refresh was refused for good and the tokens are cleared, Network or Busy when it could not be asked now.
        /// </summary>
        public static string Access(ITextWeb web, TextKeys keys, string auth, out TextFault fault, out string why)
        {
            fault = TextFault.None;
            why = null;
            lock (Gate)
            {
                string access = keys.Get(TextChatGpt.KAccess);
                long.TryParse(keys.Get(TextChatGpt.KExpires), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long expires);
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (access != null && now < expires - TextChatGpt.RefreshAhead)
                {
                    return access;
                }
                string client = keys.Get(TextChatGpt.KClient), refresh = keys.Get(TextChatGpt.KRefresh);
                if (client == null || refresh == null)
                {
                    fault = TextFault.Key;
                    why = "not signed in to ChatGPT";
                    return null;
                }
                TextWebAnswer a = web.Send(Form(auth + TextChatGpt.TokenPath, TextChatGpt.RefreshForm(client, refresh)), CallMs);
                if (a.Status == 0 || a.Status >= 500)
                {
                    fault = a.Status == 0 ? TextFault.Network : TextFault.Busy;
                    why = "the sign-in could not be renewed now";
                    return null;
                }
                TextTokens t = TextChatGpt.Tokens(a.Body, now, false, out why);
                if (t == null)
                {
                    string error = TextJsonTree.Text(TextJsonTree.Read(a.Body), "error");
                    if (TextChatGpt.Gone(error) || a.Status == 400 || a.Status == 401)
                    {
                        keys.Set(TextChatGpt.KAccess, null, TextChatGpt.KRefresh, null, TextChatGpt.KExpires, null);
                        fault = TextFault.Key;
                        why = "the ChatGPT sign-in has ended: sign in again";
                        return null;
                    }
                    fault = TextFault.Busy;
                    return null;
                }
                keys.Set(TextChatGpt.KAccess, t.Access, TextChatGpt.KRefresh, t.Refresh ?? refresh, TextChatGpt.KExpires,
                    t.ExpiresAt.ToString(System.Globalization.CultureInfo.InvariantCulture), TextChatGpt.KScope, t.Scope ?? keys.Get(TextChatGpt.KScope));
                return t.Access;
            }
        }

        /// <summary>
        /// Signs out: the refresh token revoked, then the tokens cleared here, the client and host ids kept as OpenAI asks.
        /// True when OpenAI confirmed it; the tokens are cleared all the same.
        /// </summary>
        public static bool SignOut(ITextWeb web, TextKeys keys, string auth)
        {
            lock (Gate)
            {
                string client = keys.Get(TextChatGpt.KClient), refresh = keys.Get(TextChatGpt.KRefresh);
                bool confirmed = false;
                if (client != null && refresh != null)
                {
                    TextWebAnswer d = web.Send(new TextWebRequest { Method = "GET", Url = auth + TextChatGpt.DiscoveryPath, Body = null }, CallMs);
                    string revoke = d.Status == 200 ? TextJsonTree.Text(TextJsonTree.Read(d.Body), "revocation_endpoint") : null;
                    if (revoke != null && (revoke.StartsWith("https://", StringComparison.Ordinal) || revoke.StartsWith(auth + "/", StringComparison.Ordinal)))
                    {
                        confirmed = web.Send(Form(revoke, TextChatGpt.RevokeForm(client, refresh)), CallMs).Status == 200;
                    }
                }
                keys.Set(TextChatGpt.KAccess, null, TextChatGpt.KRefresh, null, TextChatGpt.KExpires, null, TextChatGpt.KScope, null);
                return confirmed;
            }
        }
    }
}
