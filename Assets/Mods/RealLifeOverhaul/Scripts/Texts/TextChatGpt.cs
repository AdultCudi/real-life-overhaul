using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>What a sign-in's tokens say, once checked.</summary>
    public sealed class TextTokens
    {
        public string Access, Refresh, Id, Scope, Subject, Email;
        public long ExpiresAt;            // Unix seconds the access token ends
    }

    /// <summary>
    /// The rules of Sign in with ChatGPT as OpenAI documents it for open-source apps (developers.openai.com/siwc): app
    /// registration, PKCE, a loopback callback, the ID token checked, and the plan's scope required. Game-free.
    /// COST: a few strings at a sign-in or a refresh.
    /// </summary>
    public static class TextChatGpt
    {
        public const string AuthorizePath = "/api/accounts/authorize";
        public const string TokenPath = "/api/accounts/oauth/token";
        public const string KeysPath = "/.well-known/jwks.json";
        public const string DiscoveryPath = "/.well-known/openid-configuration";
        public const string Register = "dynamic_agent_client";          // the client id of a first sign-in, never kept
        public const string Scopes = "openid profile email offline_access resource.invoke chatgpt.tokens.use.direct";
        public const string PlanScope = "chatgpt.tokens.use.direct";     // without it in the grant, the plan cannot be used
        public const string CallbackPath = "/auth/callback";
        public const int Port = 1455;                                   // the callback's port when it is free (any port will do)
        public const long RefreshAhead = 300;                           // seconds before the access token's end that it is refreshed
        public const long Skew = 120;                                   // seconds of difference between this PC's clock and OpenAI's allowed
        public const string UsageUrl = "https://chatgpt.com/settings/usage";   // where a player manages the plan's use in apps

        // Names kept in TextKeys.
        public const string KClient = "chatgpt.client", KHost = "chatgpt.host", KId = "chatgpt.id", KAccess = "chatgpt.access", KRefresh = "chatgpt.refresh",
            KExpires = "chatgpt.expires", KEmail = "chatgpt.email", KSubject = "chatgpt.subject", KScope = "chatgpt.scope", KWelcomed = "chatgpt.welcomed";

        /// <summary>A host id for this PC: opaque, stable once kept, never the player's own (urn:uuid with a version 4 UUID).</summary>
        public static string NewHost() => "urn:uuid:" + Guid.NewGuid().ToString("D");

        /// <summary>A fresh random value for a state, a nonce or a PKCE verifier: 32 random bytes in base64url.</summary>
        public static string Fresh()
        {
            var bytes = new byte[32];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }
            return Url64(bytes);
        }

        /// <summary>The PKCE challenge of a verifier: SHA-256, base64url without padding.</summary>
        public static string Challenge(string verifier)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return Url64(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
            }
        }

        /// <summary>The callback's address on this PC: 127.0.0.1, never localhost.</summary>
        public static string Redirect(int port) => "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + CallbackPath;

        /// <summary>The address the browser opens, never logged; a null <paramref name="clientId"/> is a first sign-in, which registers the app.</summary>
        public static string AuthorizeUrl(string auth, string clientId, string appName, string hostId, string idTokenHint, string redirect,
            string state, string nonce, string verifier)
        {
            var t = new StringBuilder(1024);
            t.Append(auth).Append(AuthorizePath).Append("?client_id=").Append(Uri.EscapeDataString(clientId ?? Register));
            if (clientId == null)
            {
                t.Append("&agent_name_hint=").Append(Uri.EscapeDataString(appName));
            }
            t.Append("&ext_agent_host_id=").Append(Uri.EscapeDataString(hostId));
            if (clientId != null && !string.IsNullOrEmpty(idTokenHint))
            {
                t.Append("&id_token_hint=").Append(Uri.EscapeDataString(idTokenHint));
            }
            t.Append("&response_type=code&redirect_uri=").Append(Uri.EscapeDataString(redirect))
                .Append("&scope=").Append(Uri.EscapeDataString(Scopes))
                .Append("&resource=").Append(Uri.EscapeDataString(TextProviders.OpenAiApi))
                .Append("&state=").Append(Uri.EscapeDataString(state))
                .Append("&nonce=").Append(Uri.EscapeDataString(nonce))
                .Append("&code_challenge_method=S256&code_challenge=").Append(Uri.EscapeDataString(Challenge(verifier)));
            return t.ToString();
        }

        /// <summary>The values of an address's query ("?code=..&amp;state=.."), unescaped.</summary>
        public static Dictionary<string, string> Query(string target)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            int q = target != null ? target.IndexOf('?') : -1;
            if (q < 0)
            {
                return values;
            }
            foreach (string pair in target.Substring(q + 1).Split('&'))
            {
                int eq = pair.IndexOf('=');
                string name = eq >= 0 ? pair.Substring(0, eq) : pair;
                string value = eq >= 0 ? pair.Substring(eq + 1) : string.Empty;
                if (name.Length > 0)
                {
                    values[Unescape(name)] = Unescape(value);
                }
            }
            return values;
        }

        private static string Unescape(string s) => Uri.UnescapeDataString(s.Replace('+', ' '));

        /// <summary>The callback's verdict: null when it may be exchanged, else why not.</summary>
        public static string Callback(Dictionary<string, string> q, string state, string keptClient, out string clientId)
        {
            clientId = null;
            if (!q.TryGetValue("state", out string got) || got != state)
            {
                return "the sign-in's answer did not match the one asked for";
            }
            if (q.TryGetValue("error", out string error))
            {
                return error == "access_denied" ? "the sign-in was cancelled" : "the sign-in failed (" + error + ")";
            }
            if (!q.TryGetValue("code", out string code) || code.Length == 0)
            {
                return "the sign-in gave no code";
            }
            q.TryGetValue("client_id", out string given);
            if (keptClient == null)
            {
                if (string.IsNullOrEmpty(given) || given == Register)
                {
                    return "the app's registration was not completed";
                }
                clientId = given;
                return null;
            }
            if (!string.IsNullOrEmpty(given) && given != keptClient)
            {
                return "the sign-in answered for another app";
            }
            clientId = keptClient;
            return null;
        }

        /// <summary>The form of the code's exchange, with the PKCE verifier and the resource.</summary>
        public static string ExchangeForm(string clientId, string code, string verifier, string redirect) =>
            "grant_type=authorization_code&client_id=" + Uri.EscapeDataString(clientId) + "&code=" + Uri.EscapeDataString(code)
            + "&code_verifier=" + Uri.EscapeDataString(verifier) + "&redirect_uri=" + Uri.EscapeDataString(redirect)
            + "&resource=" + Uri.EscapeDataString(TextProviders.OpenAiApi);

        /// <summary>The form of a refresh, the scope left out so the grant stays as it is.</summary>
        public static string RefreshForm(string clientId, string refresh) =>
            "grant_type=refresh_token&client_id=" + Uri.EscapeDataString(clientId) + "&refresh_token=" + Uri.EscapeDataString(refresh)
            + "&resource=" + Uri.EscapeDataString(TextProviders.OpenAiApi);

        /// <summary>The form of signing out: the refresh token revoked.</summary>
        public static string RevokeForm(string clientId, string refresh) =>
            "token=" + Uri.EscapeDataString(refresh) + "&token_type_hint=refresh_token&client_id=" + Uri.EscapeDataString(clientId);

        /// <summary>The tokens of a token answer, or null with why; <paramref name="now"/> in Unix seconds.</summary>
        public static TextTokens Tokens(string body, long now, bool needId, out string why)
        {
            object root = TextJsonTree.Read(body);
            why = null;
            string access = TextJsonTree.Text(root, "access_token");
            if (access == null)
            {
                string error = TextJsonTree.Text(root, "error") ?? TextJsonTree.Text(root, "error.code");
                why = error != null ? "the sign-in refused it (" + error + ")" : "the sign-in's answer had no token";
                return null;
            }
            double expires = TextJsonTree.Number(root, "expires_in");
            var t = new TextTokens
            {
                Access = access, Refresh = TextJsonTree.Text(root, "refresh_token"), Id = TextJsonTree.Text(root, "id_token"), Scope = TextJsonTree.Text(root, "scope"),
                ExpiresAt = now + (double.IsNaN(expires) ? 3600L : (long)expires)
            };
            if (needId && t.Id == null)
            {
                why = "the sign-in's answer had no ID token";
                return null;
            }
            return t;
        }

        /// <summary>True for a refresh refused for good: the player must sign in again.</summary>
        public static bool Gone(string error)
        {
            switch (error)
            {
                case "invalid_grant":
                case "invalid_refresh_token":
                case "token_expired":
                case "refresh_token_expired":
                case "refresh_token_invalidated":
                case "refresh_token_reused":
                case "invalid_client":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>True when the grant's scope holds the plan's use.</summary>
        public static bool Plan(string scope)
        {
            if (string.IsNullOrEmpty(scope))
            {
                return false;
            }
            foreach (string s in scope.Split(' '))
            {
                if (s == PlanScope)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The ID token checked against OpenAI's keys, issuer, audience, expiry and nonce: null when it passes, else why not.</summary>
        public static string CheckId(string idToken, string keysJson, string issuer, string clientId, string nonce, long now, out string subject, out string email)
        {
            subject = email = null;
            string[] parts = idToken != null ? idToken.Split('.') : null;
            if (parts == null || parts.Length != 3)
            {
                return "the ID token is not a signed token";
            }
            object head = TextJsonTree.Read(Text64(parts[0]));
            object body = TextJsonTree.Read(Text64(parts[1]));
            if (head == null || body == null)
            {
                return "the ID token could not be read";
            }
            if (TextJsonTree.Text(head, "alg") != "RS256")
            {
                return "the ID token is not signed as expected";
            }
            if (!Signed(parts[0] + "." + parts[1], Bytes64(parts[2]), TextJsonTree.Text(head, "kid"), keysJson))
            {
                return "the ID token's signature is not OpenAI's";
            }
            if (TextJsonTree.Text(body, "iss") != issuer)
            {
                return "the ID token is from another issuer";
            }
            object aud = TextJsonTree.At(body, "aud");
            bool ours = aud is string a ? a == clientId : aud is List<object> list && list.Contains(clientId);
            if (!ours)
            {
                return "the ID token is for another app";
            }
            double exp = TextJsonTree.Number(body, "exp");
            if (double.IsNaN(exp) || exp + Skew < now)
            {
                return "the ID token has ended";
            }
            if (TextJsonTree.Text(body, "nonce") != nonce)
            {
                return "the ID token does not answer this sign-in";
            }
            subject = TextJsonTree.Text(body, "sub");
            email = TextJsonTree.Text(body, "email");
            return string.IsNullOrEmpty(subject) ? "the ID token names nobody" : null;
        }

        // The signature of the token's first two parts, with the published key of its kid (RSA, PKCS #1 v1.5, SHA-256).
        private static bool Signed(string signedPart, byte[] signature, string kid, string keysJson)
        {
            if (signature == null || !(TextJsonTree.At(TextJsonTree.Read(keysJson), "keys") is List<object> keys))
            {
                return false;
            }
            for (int i = 0; i < keys.Count; i++)
            {
                if (TextJsonTree.Text(keys[i], "kty") != "RSA" || (kid != null && TextJsonTree.Text(keys[i], "kid") != kid))
                {
                    continue;
                }
                byte[] n = Bytes64(TextJsonTree.Text(keys[i], "n")), e = Bytes64(TextJsonTree.Text(keys[i], "e"));
                if (n == null || e == null)
                {
                    continue;
                }
                try
                {
                    using (RSA rsa = RSA.Create())
                    {
                        rsa.ImportParameters(new RSAParameters { Modulus = n, Exponent = e });
                        if (rsa.VerifyData(Encoding.ASCII.GetBytes(signedPart), signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                        {
                            return true;
                        }
                    }
                }
                catch (CryptographicException)
                {
                    // a key this runtime cannot use: try the next
                }
            }
            return false;
        }

        public static string Url64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static byte[] Bytes64(string s)
        {
            if (s == null)
            {
                return null;
            }
            string b = s.Replace('-', '+').Replace('_', '/');
            b += new string('=', (4 - b.Length % 4) % 4);
            try
            {
                return Convert.FromBase64String(b);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static string Text64(string s)
        {
            byte[] bytes = Bytes64(s);
            return bytes != null ? Encoding.UTF8.GetString(bytes) : null;
        }
    }
}
