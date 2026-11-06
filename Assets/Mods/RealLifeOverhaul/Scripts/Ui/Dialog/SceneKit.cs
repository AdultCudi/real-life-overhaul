using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The mod's scenes on the RLO interface: each scene's list window (a hearing, a fame or office scene, an interview,
    /// a venture's or a pitch's meeting) bound through the host every game screen uses (UiGameHost), the window kept open
    /// and alive under the kit (its cancel key, the camera's freed mouse and every feature that asks whether a list is
    /// open see it as before), only unseen and untouchable, given back exactly as found. The kit draws the page its owner
    /// wrote (SceneViews) and answers through the same pick the window's buttons call. Its switch in Mod settings
    /// (ui_screen_scenes) gives the windows back. Cost: nothing while no scene shows; while one does, a number compared a
    /// frame and a layout a page.
    /// </summary>
    public static class SceneKit
    {
        private static readonly Dictionary<string, SceneHost> ByName = new Dictionary<string, SceneHost>(8);
        private static readonly List<SceneHost> Hosts = new List<SceneHost>(8);
        private static readonly HashSet<string> Named = new HashSet<string>(8);
        private static bool _on = true;

        /// <summary>A scene window's name, told as its desk is made: no other screen of the kit may take that window.</summary>
        internal static void Name(string name) => Named.Add(name);

        /// <summary>True for a window that is a scene's.</summary>
        internal static bool Owns(string name) => name != null && Named.Contains(name);

        /// <summary>Where each scene's window stands, for the interface's line at load.</summary>
        public static string State
        {
            get
            {
                if (Hosts.Count == 0)
                {
                    return "none opened yet";
                }
                string state = null;
                for (int i = 0; i < Hosts.Count; i++)
                {
                    string one = Hosts[i].Short + ": " + Hosts[i].Host.State;
                    state = state == null ? one : state + "; " + one;
                }
                return state;
            }
        }

        /// <summary>The scene the kit draws now, for the tests; null when none.</summary>
        internal static SceneScreen Showing
        {
            get
            {
                for (int i = 0; i < Hosts.Count; i++)
                {
                    if (Hosts[i].Host.Showing)
                    {
                        return Hosts[i].Host.Screen as SceneScreen;
                    }
                }
                return null;
            }
        }

        /// <summary>Once a second while the interface is on: each window bound while the switch is on, given back while it is off.</summary>
        internal static void Install(bool on)
        {
            _on = on;
            for (int i = 0; i < Hosts.Count; i++)
            {
                SceneHost h = Hosts[i];
                if (on && h.Current != null)
                {
                    h.Host.Install();
                }
                else
                {
                    h.Host.Remove(on ? "its window went" : "its switch is off");
                }
            }
        }

        internal static void Rearm()
        {
            for (int i = 0; i < Hosts.Count; i++)
            {
                Hosts[i].Host.Rearm();
            }
        }

        internal static void Remove(string why)
        {
            for (int i = 0; i < Hosts.Count; i++)
            {
                Hosts[i].Host.Remove(why);
            }
        }

        /// <summary>A page opened in a scene's window: the kit over it at once, not at the next second.</summary>
        internal static void Opened(SceneList list)
        {
            SceneHost h = HostFor(list.Name);
            if (!ReferenceEquals(h.Current, list))
            {
                h.Host.Remove("its window was made again");
                h.Current = list;
            }
            if (_on && UiDirector.On && UiScreenSwitches.On(DialogKits.ScenesId))
            {
                h.Host.Install();
            }
        }

        /// <summary>The window stood on another canvas (the game's black cover): the kit stands just over it there.</summary>
        internal static void Moved(SceneList list)
        {
            SceneHost h = HostFor(list.Name);
            h.Current = list;
            h.Host.Remove("its window moved");
            if (_on && UiDirector.On && UiScreenSwitches.On(DialogKits.ScenesId))
            {
                h.Host.Install();
            }
        }

        /// <summary>The window gone with its feature.</summary>
        internal static void Lost(string name)
        {
            if (ByName.TryGetValue(name, out SceneHost h))
            {
                h.Host.Remove("its window went");
                h.Current = null;
            }
        }

        private static SceneHost HostFor(string name)
        {
            if (!ByName.TryGetValue(name, out SceneHost h))
            {
                h = new SceneHost(name);
                ByName[name] = h;
                Hosts.Add(h);
            }
            return h;
        }

        // A scene's window as the host needs it: the mod's own copy of the game's list window on the HUD, the whole window the leaf the kit hides.
        // The window is switched on by the owner before the kit binds to it (the first scene of a session makes the host in that same
        // call), so the host's watcher never sees it open: standing says the kit shows the moment it binds over a window already on.
        private sealed class SceneHost : IUiGameScreen, IUiGameStands
        {
            private const string Answers = "Scroll View";

            public readonly string ListName;
            public readonly string Short;
            public readonly UiGameHost Host;
            public SceneList Current;

            public SceneHost(string name)
            {
                ListName = name;
                Short = name.StartsWith("RealLifeOverhaul ", System.StringComparison.Ordinal) ? name.Substring(17) : name;
                Host = new UiGameHost(this);
            }

            public string Id => DialogKits.ScenesId;

            public string Name => "the scene window '" + Short + "'";

            public string Title => UiText.T("reallife_ui_screen_scenes");

            public UiIcon Icon => UiIcon.People;

            public UiGameKind Kind => UiGameKind.Panel;

            public Component Find() => Current != null && Current.Alive ? Current.Window : null;

            public string Check(Component found) => found.transform.Find(Answers) == null ? "its answers are not there" : null;

            public void Conceal(Component found, UiConceal conceal) => conceal.Add(found.transform);

            public IUiScreen Make(Component found) => new SceneScreen(Current);
        }
    }

    /// <summary>
    /// A scene's page on the kit: the decision card for a moment, the conversation docked at the right for questions and
    /// answers (SceneViews), the answers numbered on 1 to 9 (F the first, P the second), each pressed through the window's own pick, and Back the
    /// window's cancel where the scene lets the player leave. Cost: a number compared a frame; a layout a page.
    /// </summary>
    public sealed class SceneScreen : IUiScreen
    {
        internal readonly SceneList List;
        private int _stamp = -1, _heard = -1, _laidPage = -1;
        private float _phone = 1f;   // the phone's place the conversation was docked beside (UiPhone.LeftShare)
        private bool _dirty = true;
        private SceneLook _look;
        private float _offset = -1f;

        internal SceneScreen(SceneList list)
        {
            List = list;
        }

        public string Id => DialogKits.ScenesId;

        public bool Dirty => _dirty || List.Page.Stamp != _stamp || List.Thread.Count != _heard || UiPhone.LeftShare != _phone;   // the phone shown or put away: the dock moves

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            ScenePage page = List.Page;
            var words = new SceneWords
            {
                Move = UiText.T("reallife_ui_prompt_move"), Choose = UiText.T("reallife_ui_prompt_choose"), Back = UiText.T("common_cancel"),
                Menu = UiText.T("input_key_menu"), Present = UiText.T("reallife_ui_scene_present"), Answer = UiText.T("reallife_ui_dialog_your_answer")
            };
            if (page.Stamp != _laidPage)
            {
                _laidPage = page.Stamp;
                _offset = -1f;   // a new page: the conversation shows its newest line
            }
            _look = SceneViews.Lay(s, page, List.Thread, words, _offset, frame, dark, keys);
            _stamp = page.Stamp;
            _heard = List.Thread.Count;
            _phone = UiPhone.LeftShare;
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            bool choice = _look == SceneLook.Talk ? command == UiConversation.ChoiceCommand : command == UiDecision.ChoiceCommand;
            if (choice)
            {
                if (arg < 0 || arg >= List.Page.Entries.Count)
                {
                    return false;
                }
                bool done = List.Pick(List.Page.Entries[arg].Id);
                UiDirector.Logger?.Info("Scene (kit): an answer in '" + List.Name + "'" + (done ? ": the window's own pick" : " was not offered, so nothing was picked"));
                _dirty = true;
                return done;
            }
            return _look == SceneLook.Decision && command == UiDecision.ButtonCommand && Leave();
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back() => Leave();

        public void Tab(int step)
        {
        }

        // F takes the answer the page leads with, P the next, as the caps on them say: each through the window's own pick.
        public bool Act(UiAction action)
        {
            int main = SceneViews.Keyed(List.Page, out int second);
            int answer = action == UiAction.Alt ? main : action == UiAction.Option ? second : -1;
            return answer >= 0 && Press(_look == SceneLook.Talk ? UiConversation.ChoiceCommand : UiDecision.ChoiceCommand, answer);
        }

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
            if (_look == SceneLook.Talk)
            {
                _offset = System.Math.Max(0f, (_offset < 0f ? 100000f : _offset) - notches * UiTokens.S16);
                _dirty = true;
            }
        }

        // The window's own cancel, where the scene lets the player leave (the Legal file).
        private bool Leave()
        {
            if (List.Locked || !List.IsOpen)
            {
                return false;
            }
            UiDirector.Logger?.Info("Scene (kit): '" + List.Name + "' closed as its cancel closes it");
            List.Close();
            return true;
        }
    }
}
