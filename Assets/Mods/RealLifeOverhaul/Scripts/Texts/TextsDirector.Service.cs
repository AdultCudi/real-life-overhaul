using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// Your own AI: its runner kept as the options say, the options page's buttons and its status. The key is typed hidden
    /// and kept only in TextKeys. COST: two comparisons a look; the rest on a press or the runner's threads.
    /// </summary>
    public sealed partial class TextsDirector
    {
        private const int KeyMost = 400;      // characters a key may be; the longest services' keys are about 170
        private const int ModelMost = 120;    // characters of a model's name
        private const int AddressMost = 200;  // characters of a local program's address
        private const int ClientIdMost = 200; // characters of a Player2 game client id
        private const string ConfigFile = "service.txt";   // the models and addresses the player named, per service; nothing secret

        private static readonly ITextWeb Web = new TextWeb();
        private static TextService _service;
        private static TextKeys _keys;
        private static Dictionary<string, string> _config;
        private static int _serviceIndex = -1, _serviceVersion = -1, _version, _toldIndex;
        private static bool _limitTold, _appTold, _modelTold;
        private static TextSignIn _signIn;
        private static Action<string> _keyTyped, _modelTyped, _addressTyped;

        // Made on the game's thread the first time: it needs the game's data folder.
        private static TextKeys Keys => _keys ?? (_keys = new TextKeys(TextsGame.Home));

        private static TextProviderInfo Chosen => TextProviders.Of(Settings.TextsService);

        // At each look: the runner started, stopped or started again when the service, its model, address or key changed.
        private static void RunService()
        {
            int index = Settings.Texts ? Settings.TextsService : 0;
            if (_serviceIndex == index && _serviceVersion == _version)
            {
                Watch();
                return;   // as it was started: two comparisons
            }
            StopService("the options changed");
            _serviceIndex = index;
            _serviceVersion = _version;
            TextProviderInfo p = TextProviders.Of(index);
            if (p.Id == TextProvider.Off)
            {
                return;
            }
            Directory.CreateDirectory(TextsGame.Home);
            var start = new TextServiceStart
            {
                Provider = p, Base = TextProviders.Base(p, Config("base." + index)), Model = Config("model." + index),
                Key = p.Access == TextAccess.Key ? Keys.Get(KeyName(index)) : null, Keys = p.Access == TextAccess.SignIn ? Keys : null,
                ClientId = p.Access == TextAccess.App ? ClientIdFor(index) : null
            };
            _service = new TextService(ServiceQueue, start, Web, Log);
            Hub.Service = _service;
            _limitTold = false;
            _modelTold = false;
            _appTold = false;
            _service.Start();
            Logger?.Info("Your own AI: cards go to " + p.Name + " (" + (start.Model ?? p.Model ?? "its first listed model") + ")"
                         + (p.Access == TextAccess.Local ? " on this PC" : string.Empty) + "; the pack or the written lines when it fails, is late or rests.");
            if (_toldIndex != index)
            {
                _toldIndex = index;   // once a session for each service: what is sent, or why nothing can be
                if (_service.State == TextServerState.Failed)
                {
                    ShowServiceStatus();
                }
                else
                {
                    TextsGame.Notice("reallife_textsai_note_sending", new Dictionary<string, string>(1) { { "service", p.Name } }, 16f);
                }
            }
        }

        // Tells the player once a session when the ChatGPT plan's allowance (or Player2's joules) is used up, and once when the Player2 App
        // gives no sign-in.
        private static void Watch()
        {
            TextService s = _service;
            if (s == null)
            {
                return;
            }
            if (!_limitTold && s.Fault == TextFault.UsageLimit)
            {
                _limitTold = true;
                TextsGame.Notice(s.Provider.Access == TextAccess.App ? "reallife_textsai_p2_joules" : "reallife_textsai_chatgpt_limit", null, 16f);
            }
            if (!_modelTold && s.ModelRefused && s.State == TextServerState.Failed && !s.Provider.NoModel)
            {
                _modelTold = true;   // once for each start: the model name the player chose, or the mod's own, is not known to the service
                TextsGame.Notice(TextServiceRules.ModelUnknownKey, new Dictionary<string, string>(2) { { "service", s.Provider.Name }, { "model", s.Model ?? "?" } }, 20f);
            }
            if (!_appTold && s.Provider.Access == TextAccess.App && s.AppState != TextAppState.SignedIn)
            {
                _appTold = true;
                TextsGame.Notice(AppStateKey(s.AppState), null, 16f);
            }
        }

        // The game client id the Player2 App is asked with: the player's own line in service.txt while the mod has none built in; null for none.
        private static string ClientIdFor(int index)
        {
            string own = Config("clientid." + index);
            string id = own ?? (TextPlayer2.ClientId.Length > 0 ? TextPlayer2.ClientId : null);
            return id != null && id.Length <= ClientIdMost ? id : null;
        }

        private static string AppStateKey(TextAppState state)
        {
            switch (state)
            {
                case TextAppState.NotRunning: return "reallife_textsai_p2_state_missing";
                case TextAppState.NoClientId: return "reallife_textsai_p2_state_noid";
                case TextAppState.NotSignedIn: return "reallife_textsai_p2_state_signin";
                default: return "reallife_textsai_p2_state_ok";
            }
        }

        private static void StopService(string why)
        {
            TextService service = _service;
            _serviceIndex = -1;   // started again as the options say at the next look that wants it
            if (service == null)
            {
                return;
            }
            _service = null;
            service.Halt();   // at once: nothing more of the queue goes to a service the player has left
            Hub.Service = null;
            Hub.Clear();
            ThreadPool.QueueUserWorkItem(_ => service.Dispose());   // its workers end within a request's time: never on the game's thread
            Logger?.Info("Your own AI: " + service.Provider.Name + " stopped (" + why + "); the pack or the written lines are used.");
        }

        private static string KeyName(int index) => "key." + index.ToString(CultureInfo.InvariantCulture);

        // The player's own models and addresses, per service.
        private static string Config(string name)
        {
            if (_config == null)
            {
                _config = new Dictionary<string, string>(StringComparer.Ordinal);
                try
                {
                    string path = Path.Combine(TextsGame.Home, ConfigFile);
                    if (File.Exists(path))
                    {
                        TextKeys.Parse(File.ReadAllText(path), _config);
                    }
                }
                catch (Exception e)
                {
                    Logger?.Warn("Your own AI: " + ConfigFile + " could not be read (" + e.GetType().Name + "); the services' own models are used.");
                }
            }
            return _config.TryGetValue(name, out string v) && v.Length > 0 ? v : null;
        }

        private static void SetConfig(string name, string value)
        {
            Config(name);
            if (string.IsNullOrEmpty(value))
            {
                _config.Remove(name);
            }
            else
            {
                _config[name] = value;
            }
            try
            {
                Directory.CreateDirectory(TextsGame.Home);
                File.WriteAllText(Path.Combine(TextsGame.Home, ConfigFile), TextKeys.Text(_config));
            }
            catch (Exception e)
            {
                Logger?.Warn("Your own AI: " + ConfigFile + " could not be written (" + e.GetType().Name + ").");
            }
            _version++;
        }

        /// <summary>Asks for the chosen service's key in the game's text box, hidden as it is typed; kept on this PC only.</summary>
        public static void EnterKey()
        {
            TextProviderInfo p = Chosen;
            if (p.Id == TextProvider.Off)
            {
                TextsGame.Notice("reallife_textsai_choose_first", null);
                return;
            }
            if (p.Access == TextAccess.SignIn)
            {
                SignIn();
                return;
            }
            if (p.Access == TextAccess.Local)
            {
                TextsGame.Notice("reallife_textsai_key_local", new Dictionary<string, string>(1) { { "service", p.Name } });
                return;
            }
            if (p.Access == TextAccess.App)
            {
                TextsGame.Notice("reallife_textsai_p2_nokey", null, 16f);
                return;
            }
            _keyTyped = _keyTyped ?? Guard.Wrap<string>(KeyTyped);
            if (!TextsGame.AskSecret("reallife_textsai_key_placeholder", "reallife_textsai_key_keep", _keyTyped, KeyMost))
            {
                TextsGame.Notice("reallife_textsai_load_first", null);
            }
        }

        private static void KeyTyped(string typed)
        {
            TextProviderInfo p = Chosen;
            string key = typed != null ? typed.Trim() : null;
            if (string.IsNullOrEmpty(key) || p.Access != TextAccess.Key)
            {
                return;
            }
            if (Keys.Set(KeyName((int)p.Id), key))
            {
                _version++;
                Logger?.Info("Your own AI: a key for " + p.Name + " is kept on this PC.");
                TextsGame.Notice("reallife_textsai_key_kept", new Dictionary<string, string>(1) { { "service", p.Name } });
            }
            else
            {
                Logger?.Warn("Your own AI: the key for " + p.Name + " was not kept: " + Keys.Why + ".");
                TextsGame.Notice("reallife_textsai_key_not_kept", null, 16f);
            }
        }

        /// <summary>Continue with ChatGPT: opens OpenAI's sign-in in the browser; the answer comes back to this PC.</summary>
        public static void SignIn()
        {
            if (Chosen.Access == TextAccess.App)
            {
                TextsGame.Notice("reallife_textsai_p2_signin", null, 16f);   // Player2 signs in in its own App
                return;
            }
            if (!TextKeys.Sealable())
            {
                TextsGame.Notice("reallife_textsai_key_not_kept", null, 16f);
                return;
            }
            _signIn?.Cancel();
            _signIn = new TextSignIn(Web, Keys)
            {
                DonePage = TextsText.Page("reallife_textsai_page_done"), FailedPage = TextsText.Page("reallife_textsai_page_failed")
            };
            string url = _signIn.Begin(TextServiceRules.AppName);
            if (url == null)
            {
                Logger?.Warn("Your own AI: the ChatGPT sign-in could not begin: " + _signIn.Why + ".");
                TextsGame.Notice("reallife_textsai_chatgpt_failed", new Dictionary<string, string>(1) { { "why", _signIn.Why } }, 16f);
                _signIn = null;
                return;
            }
            TextsGame.OpenUrl(url);   // never logged: it carries the kept sign-in's hint
            Logger?.Info("Your own AI: the browser was opened for the ChatGPT sign-in; its answer is awaited on 127.0.0.1.");
            TextsGame.Notice("reallife_textsai_chatgpt_open", null, 16f);
        }

        // At each look while a sign-in is under way: its end told once.
        private static void SignInLook()
        {
            TextSignIn s = _signIn;
            if (s == null || s.State == TextSignInState.Waiting)
            {
                return;
            }
            _signIn = null;
            if (s.State == TextSignInState.Failed)
            {
                Logger?.Info("Your own AI: the ChatGPT sign-in ended without signing in: " + s.Why + ".");
                TextsGame.Notice("reallife_textsai_chatgpt_failed", new Dictionary<string, string>(1) { { "why", s.Why } }, 16f);
                return;
            }
            _version++;
            Logger?.Info("Your own AI: signed in to ChatGPT; the plan may be used.");
            if (s.First)
            {
                Keys.Set(TextChatGpt.KWelcomed, "1");
                TextsGame.Notice("reallife_textsai_chatgpt_welcome", null, 20f);   // OpenAI's guidelines: told once, at the first sign-in
            }
            else
            {
                TextsGame.Notice("reallife_textsai_chatgpt_done", null, 12f);
            }
        }

        /// <summary>Asks for the chosen service's model in the game's text box; an empty line goes back to the service's own.</summary>
        public static void ChooseModel()
        {
            TextProviderInfo p = Chosen;
            if (p.Id == TextProvider.Off)
            {
                TextsGame.Notice("reallife_textsai_choose_first", null);
                return;
            }
            if (p.NoModel)
            {
                TextsGame.Notice("reallife_textsai_p2_nomodel", null, 12f);
                return;
            }
            _modelTyped = _modelTyped ?? Guard.Wrap<string>(ModelTyped);
            string now = Config("model." + (int)p.Id) ?? p.Model ?? _service?.Model ?? string.Empty;
            if (!TextBox.Ask("reallife_textsai_model_placeholder", "reallife_textsai_model_keep", _modelTyped, null, now, true, ModelMost))
            {
                TextsGame.Notice("reallife_textsai_load_first", null);
            }
        }

        private static void ModelTyped(string typed)
        {
            TextProviderInfo p = Chosen;
            string model = typed != null ? typed.Trim() : string.Empty;
            SetConfig("model." + (int)p.Id, model == p.Model ? null : model);
            TextsGame.Notice("reallife_textsai_model_kept", new Dictionary<string, string>(2) { { "service", p.Name }, { "model", model.Length > 0 ? model : p.Model ?? "?" } });
        }

        /// <summary>Asks for the address of a program on this PC on another port; an empty line goes back to its own.</summary>
        public static void ChooseAddress()
        {
            TextProviderInfo p = Chosen;
            if (p.Access != TextAccess.Local)
            {
                TextsGame.Notice("reallife_textsai_address_local_only", null);
                return;
            }
            _addressTyped = _addressTyped ?? Guard.Wrap<string>(AddressTyped);
            if (!TextBox.Ask("reallife_textsai_address_placeholder", "reallife_textsai_model_keep", _addressTyped, null, Config("base." + (int)p.Id) ?? p.Base, true, AddressMost))
            {
                TextsGame.Notice("reallife_textsai_load_first", null);
            }
        }

        private static void AddressTyped(string typed)
        {
            TextProviderInfo p = Chosen;
            string address = typed != null ? typed.Trim() : string.Empty;
            if (address.Length > 0 && TextProviders.Base(p, address) == null)
            {
                TextsGame.Notice("reallife_textsai_address_bad", null, 16f);
                return;
            }
            SetConfig("base." + (int)p.Id, address == p.Base ? null : address);
            TextsGame.Notice("reallife_textsai_model_kept", new Dictionary<string, string>(2) { { "service", p.Name }, { "model", address.Length > 0 ? address : p.Base } });
        }

        /// <summary>Forgets the chosen service's kept key, or with ChatGPT chosen signs out of it, revoked at OpenAI; nothing for Off and the programs on this PC.</summary>
        public static void Forget()
        {
            TextProviderInfo p = Chosen;
            TextForget what = TextProviders.Forgets(p);
            if (what == TextForget.Key)
            {
                Keys.Set(KeyName((int)p.Id), null);
                _version++;
                Logger?.Info("Your own AI: the key for " + p.Name + " was forgotten.");
                TextsGame.Notice("reallife_textsai_key_forgotten", new Dictionary<string, string>(1) { { "service", p.Name } });
                return;
            }
            if (what == TextForget.Nothing || !TextSignIn.SignedIn(Keys))
            {
                TextsGame.Notice("reallife_textsai_nothing_kept", null);   // only what the chosen service keeps is let go: Ollama, LM Studio and Off never sign the player out of ChatGPT
                return;
            }
            TextKeys keys = Keys;
            _version++;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                bool confirmed = TextSignIn.SignOut(Web, keys, TextProviders.ChatGptAuth);
                _signedOut = confirmed ? 1 : 2;   // told at the next look, on the game's thread
            });
        }

        private static volatile int _signedOut;

        // At each look: a sign-out's end told once.
        private static void SignOutLook()
        {
            int told = _signedOut;
            if (told == 0)
            {
                return;
            }
            _signedOut = 0;
            _version++;
            Logger?.Info("Your own AI: signed out of ChatGPT" + (told == 1 ? "; OpenAI confirmed it." : "; OpenAI could not be asked to confirm it."));
            TextsGame.Notice(told == 1 ? "reallife_textsai_chatgpt_out" : "reallife_textsai_chatgpt_out_unconfirmed", null, 16f);
        }

        public static void ManageUsage()
        {
            TextsGame.OpenUrl(Chosen.Access == TextAccess.App ? TextPlayer2.SiteUrl : TextChatGpt.UsageUrl);
        }

        /// <summary>Sends a friend's small talk to the chosen service only and shows it as a notice, or why not.</summary>
        public static void TestService()
        {
            TextProviderInfo p = Chosen;
            if (p.Id == TextProvider.Off)
            {
                TextsGame.Notice("reallife_textsai_choose_first", null);
                return;
            }
            if (Instance == null || SaveGameManager.Current == null)
            {
                TextsGame.Notice("reallife_texts_test_no_save", null);
                return;
            }
            if (!Settings.Texts)
            {
                TextsGame.Notice("reallife_texts_status_off", null, 12f);
                return;
            }
            if (!Hub.ServiceWrites)
            {
                TextsGame.Notice("reallife_textsai_test_language", new Dictionary<string, string>(1) { { "service", p.Name } }, 16f);   // no checks of a reply in them
                return;
            }
            if (_service == null || _service.State == TextServerState.Failed || !Hub.ServiceUp)
            {
                if (_service != null && _service.State == TextServerState.Failed)
                {
                    _version++;   // started again at the next look: a key or a connection may be mended since
                }
                ShowServiceStatus();
                return;
            }
            TextRequest r = SampleRequest();
            r.Key = "test:service";
            r.ServiceOnly = true;
            double asked = TextClock.Now;
            r.Done = words => TextsGame.Notice(words != null ? "reallife_textsai_test_done" : "reallife_textsai_test_none",
                new Dictionary<string, string>(3) { { "service", p.Name }, { "text", words ?? string.Empty }, { "seconds", (TextClock.Now - asked).ToString("0.0") }, { "why", Short(r.Why) } }, 16f);
            if (!Hub.Ask(r, asked))
            {
                TextsGame.Notice("reallife_textsai_test_none", new Dictionary<string, string>(3) { { "service", p.Name }, { "seconds", "0" }, { "why", Short(r.Why) } }, 16f);
            }
        }

        private static string Short(string why) => why == null ? "?" : why.Length <= WhyMost * 2 ? why : why.Substring(0, WhyMost * 2 - 1) + "\u2026";

        public static void ShowServiceStatus()
        {
            if (Chosen.Access == TextAccess.App)
            {
                AskPlayer2(false);   // Player2: the App is looked for first, on a worker; the notice comes at the next look
                return;
            }
            var data = new Dictionary<string, string>(6);
            TextsGame.Notice(ServiceStatus(data), data, 16f);
        }

        // The status notice's key and values for the chosen service, from its runner.
        private static string ServiceStatus(Dictionary<string, string> data)
        {
            TextProviderInfo p = Chosen;
            data["service"] = p.Name;
            string key;
            TextService s = _service;
            if (p.Id == TextProvider.Off || s == null)
            {
                key = "reallife_textsai_status_off";
            }
            else if (s.State == TextServerState.Failed && s.ModelRefused && !p.NoModel)
            {
                key = TextServiceRules.ModelUnknownKey;
                data["model"] = s.Model ?? "?";
            }
            else if (s.State == TextServerState.Failed)
            {
                key = "reallife_textsai_status_failed";
                data["why"] = Cap(s.Why ?? "?");
            }
            else if (s.Resting)
            {
                key = "reallife_textsai_status_resting";
                data["why"] = Cap(s.Why ?? "?");
            }
            else if (s.State != TextServerState.Ready)
            {
                key = "reallife_textsai_status_starting";
            }
            else
            {
                key = "reallife_textsai_status_ready";
                data["model"] = p.NoModel ? TextsText.T("reallife_textsai_p2_model") : s.Model ?? "?";
                data["words"] = Hub.ServiceShown.ToString(CultureInfo.InvariantCulture);
                data["fell"] = Hub.ServiceFell.ToString(CultureInfo.InvariantCulture);
                data["seconds"] = s.Written > 0 ? s.MeanSeconds.ToString("0.0") : "?";
            }
            return key;
        }

        // ----- Player2: the status look and the Test, each a few calls on a worker, told at the next look

        private sealed class PlayerTwoTold
        {
            public TextAppState State;
            public TextPlayer2Report Report;   // the Test's; null for the status look
        }

        private static volatile PlayerTwoTold _p2Told;
        private static int _p2Busy;   // 1 while a Player2 call is under way

        // Player2's status (the App found and signed in or not, then the runner's own state) or its Test (the App, the web API's health
        // call and one short reply), asked on a worker thread; nothing waits on the game's thread.
        private static void AskPlayer2(bool test)
        {
            if (Interlocked.CompareExchange(ref _p2Busy, 1, 0) != 0)
            {
                TextsGame.Notice("reallife_textsai_p2_busy", null, 8f);
                return;
            }
            string id = ClientIdFor((int)TextProvider.Player2);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (test)
                    {
                        TextPlayer2Report r = TextPlayer2.Run(Web, id);
                        if (r.Pass)
                        {
                            Logger?.Info(r.Line);
                        }
                        else
                        {
                            Logger?.Warn(r.Line);
                        }
                        _p2Told = new PlayerTwoTold { State = r.App, Report = r };
                    }
                    else
                    {
                        _p2Told = new PlayerTwoTold { State = TextPlayer2.Probe(Web, id, out string key) };
                    }
                }
                catch (Exception e)
                {
                    Logger?.Warn("Player2 " + (test ? "test FAIL" : "look failed") + ": " + e.GetType().Name + ".");
                    _p2Told = new PlayerTwoTold { State = TextAppState.NotRunning, Report = test ? new TextPlayer2Report { Why = "the check could not run" } : null };
                }
                finally
                {
                    Interlocked.Exchange(ref _p2Busy, 0);
                }
            });
        }

        /// <summary>Options > Tests: Player2's App found and signed in, its web API pinged, and one short reply asked; PASS or FAIL in Player.log.</summary>
        public static void TestPlayer2()
        {
            AskPlayer2(true);
        }

        // At each look: a Player2 look's or Test's end told once, on the game's thread.
        private static void PlayerTwoLook()
        {
            PlayerTwoTold told = _p2Told;
            if (told == null)
            {
                return;
            }
            _p2Told = null;
            if (told.Report != null)
            {
                TextsGame.Notice(told.Report.Pass ? "reallife_textsai_p2_test_pass" : "reallife_textsai_p2_test_fail",
                    new Dictionary<string, string>(2) { { "seconds", told.Report.Seconds.ToString("0.0") }, { "why", Short(told.Report.Why) } }, 16f);
                return;
            }
            if (told.State != TextAppState.SignedIn)
            {
                TextsGame.Notice(AppStateKey(told.State), null, 16f);
                return;
            }
            var data = new Dictionary<string, string>(6);
            string line = TextsText.T(ServiceStatus(data));
            foreach (KeyValuePair<string, string> kv in data)
            {
                line = line.Replace("{" + kv.Key + "}", kv.Value);
            }
            TextsGame.Notice("reallife_textsai_p2_status", new Dictionary<string, string>(2) { { "state", TextsText.T(AppStateKey(told.State)) }, { "line", line } }, 20f);
        }

        // The tests' sample card: a friend's good news, as the pack's test asks it.
        private static TextRequest SampleRequest()
        {
            var r = new TextRequest
            {
                Use = TextUse.People, Key = "test:sample", Intent = "friend.news.good", Speaker = "test", Priority = TextPriority.Now, Seconds = 15.0, Day = TextsGame.Day
            };
            r.Who.Name = "Dana Whitlock";
            r.Who.First = "Dana";
            r.Who.Age = 31;
            r.Who.Job = TextWords.Jobs[13];
            r.Who.District = "Midtown";
            r.Who.Role = TextWords.Stages[3] + ", " + TextWords.Origins[3];
            r.Who.Mood = TextWords.Mood(1);
            r.Who.Style = TextWords.FriendStyle(TextsGame.Day * 7919 + 13, 0, false);
            r.Who.Player = TextsGame.PlayerName;
            r.Who.When = TextsGame.When();
            r.Slots.Set("happening", TextWords.Happening(TextsGame.Day, true));
            return r;
        }
    }
}
