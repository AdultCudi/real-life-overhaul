using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BigAmbitions.InputSystem;
using Player.HUD.ControlHints;
using TMPro;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's control hints as key chips over the bar's right end, never a strip of their own: while the game shows its
    /// hints (ControlsHintsUI, for whatever its providers say: driving, placing, building), each hint's keys and words as the
    /// game wrote them; while walking with the game's hint setting on, the keys that open the map and the pause; and after
    /// either, the game's menu key as the way back, as every prompt row of the interface says how to go back.
    /// The game's hint panel stays up, hidden by leaf while the HUD shows, and is given back while it does not (the interior
    /// designer hides the top bar, and its hints are the game's own there). The chips name the game's own bindings, so a
    /// rebound key shows as bound. Cost: four reads a second of the game's few hint lines while the HUD shows.
    /// </summary>
    internal static class HudHints
    {
        private const int Most = 9;   // the kit's chips carry their keys on Pick1 to Pick9

        private static readonly UiConceal Hidden = new UiConceal();
        private static readonly List<SingleControlHintEntry> Entries = new List<SingleControlHintEntry>(8);
        private static readonly string[] Names = new string[Most];
        private static readonly UiPrompt[] Prompts = new UiPrompt[Most];
        private static readonly HintKeys Keys = new HintKeys();
        private static readonly PlayerAction[] Walking = { PlayerAction.OpenMap, PlayerAction.Pause };
        private static readonly string[] WalkingWords = { "input_key_openmap", "input_key_pause" };

        private static ControlsHintsUI _hints;
        private static int _count;    // the chips, the way back among them
        private static int _picks;    // the chips on Pick1 to Pick9
        private static string _back;  // the game's name for its menu key, the way back's chip; null for none
        private static bool _shown;
        private static bool _kept;   // the interior designer's face has the chips
        private static bool _looked;

        /// <summary>True while the HUD has chips to show: its frame lets the keys lead so they are drawn.</summary>
        public static bool Chips => _shown && _count > 0;

        /// <summary>The kit's keys with the chips' own key names on Pick1 to Pick9.</summary>
        public static IUiKeys Wrap(IUiKeys inner)
        {
            Keys.Inner = inner;
            return Keys;
        }

        /// <summary>The HUD shown or hidden: the game's hint panel hidden under it, or given back (the interior designer's face keeps it hidden while it shows).</summary>
        public static void Shown(bool shown)
        {
            _shown = shown;
            Find();
            if (shown || _kept)
            {
                Hidden.Hide();
            }
            else
            {
                Hidden.Show();
            }
        }

        /// <summary>
        /// The interior designer's face shows the game's hint lines as chips of its own while the HUD is away: the game's panel stays hidden for as long as it does,
        /// whichever comes first, the HUD or the face.
        /// </summary>
        public static void Keep(bool on)
        {
            _kept = on;
            if (on)
            {
                Find();
                Hidden.Hide();
            }
            else if (!_shown)
            {
                Hidden.Show();
            }
        }

        /// <summary>The game's hint lines alone (no walking keys and no way back) read into chips: how many, and a number that changes when a line does.</summary>
        public static int Mirror(UiPrompt[] into, out int sig)
        {
            _count = _picks = 0;
            _back = null;
            sig = 0;
            Find();
            try
            {
                if (_hints != null && _hints.gameObject.activeInHierarchy)
                {
                    Entries.Clear();
                    _hints.GetComponentsInChildren(false, Entries);
                    for (int i = 0; i < Entries.Count && _count < Most - 1; i++)
                    {
                        sig = unchecked(sig * 31 + Hint(Entries[i].transform));
                    }
                }
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the interior designer's key chips");
                _count = _picks = 0;
            }
            int n = Math.Min(_count, into.Length);
            Array.Copy(Prompts, into, n);
            sig = unchecked(sig * 31 + n);
            return n;
        }

        /// <summary>The game's hints given back as found.</summary>
        public static void End()
        {
            Hidden.Clear();
            _shown = false;
            _count = _picks = 0;
            _back = null;
            _looked = false;
        }

        /// <summary>A new city: the hint panel is looked for again.</summary>
        public static void Rearm()
        {
            End();
            _hints = null;
        }

        // The game's hint panel, looked for once a city: under the interface's root as the game builds it.
        private static void Find()
        {
            if (_looked)
            {
                return;
            }
            _looked = true;
            UIs uis = InstanceBehavior<UIs>.Instance;
            Transform at = uis != null ? uis.transform.Find("ControlsHintUIController/Container") : null;
            _hints = at != null ? at.GetComponent<ControlsHintsUI>() : null;
            if (_hints != null)
            {
                Hidden.Add(_hints.transform.Find("Panel"));
            }
        }

        /// <summary>The hints read again into the HUD's model; the signature of what shows.</summary>
        public static int Look(UiHudModel model)
        {
            _count = _picks = 0;
            _back = null;
            int sig = 0;
            try
            {
                sig = Read();
                if (_picks > 0)
                {
                    sig = Back(sig);
                }
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the HUD's key chips");
                _count = _picks = 0;
                _back = null;
            }
            if (model.Prompts.Length < Most)
            {
                model.Prompts = new UiPrompt[Most];
            }
            Array.Copy(Prompts, model.Prompts, _count);
            model.PromptCount = _count;
            return unchecked(sig * 31 + _count);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Read()
        {
            int sig = 0;
            if (_hints != null && _hints.gameObject.activeInHierarchy)
            {
                Entries.Clear();
                _hints.GetComponentsInChildren(false, Entries);
                for (int i = 0; i < Entries.Count && _count < Most; i++)
                {
                    sig = unchecked(sig * 31 + Hint(Entries[i].transform));
                }
                return sig;
            }
            if (!PlayerPrefSettings.ControlHints)
            {
                return 0;
            }
            for (int i = 0; i < Walking.Length; i++)
            {
                string key = Walking[i].GetDisplayName();
                if (!string.IsNullOrEmpty(key))
                {
                    Add(key, UiText.T(WalkingWords[i]));
                    sig = unchecked(sig * 31 + key.GetHashCode());
                }
            }
            return sig;
        }

        // The way back after the hints: the game's own menu key, which opens its menu wherever the player is.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Back(int sig)
        {
            string key = PlayerAction.Menu.GetDisplayName();
            if (string.IsNullOrEmpty(key) || _count >= Most)
            {
                return sig;
            }
            _back = key;
            Prompts[_count++] = new UiPrompt { Action = UiAction.Back, Label = UiText.T("input_key_menu") };
            return unchecked(sig * 31 + key.GetHashCode());
        }

        // One of the game's hint lines: its bindings' names joined as the chip's key, its words between them as its label.
        private static int Hint(Transform entry)
        {
            string key = null, words = null;
            for (int i = 0; i < entry.childCount; i++)
            {
                Transform part = entry.GetChild(i);
                if (!part.gameObject.activeSelf)
                {
                    continue;   // the renderer's two templates
                }
                if (part.GetComponent<ControlsHintBindingUI>() != null)
                {
                    string name = UiWidgets.Text(part.GetComponentInChildren<TMP_Text>(false));
                    if (!string.IsNullOrEmpty(name))
                    {
                        key = key == null ? name : key + " " + name;
                    }
                    continue;
                }
                string piece = UiWidgets.Text(part.GetComponent<TMP_Text>());
                if (!string.IsNullOrEmpty(piece))
                {
                    piece = piece.Trim();
                    words = words == null ? piece : words + " " + piece;
                }
            }
            if (key == null || words == null)
            {
                return 0;
            }
            Add(key, words);
            return unchecked(key.GetHashCode() * 7 + words.GetHashCode());
        }

        private static void Add(string key, string words)
        {
            if (_picks >= Most - 1)
            {
                return;   // the last place is the way back's
            }
            Names[_picks] = key;
            Prompts[_count++] = new UiPrompt { Action = (UiAction)((int)UiAction.Pick1 + _picks), Label = words };
            _picks++;
        }

        /// <summary>The kit's keys, with the game's hint keys named on the number actions the chips carry.</summary>
        private sealed class HintKeys : IUiKeys
        {
            public IUiKeys Inner;

            public UiPad Pad => Inner != null ? Inner.Pad : UiPad.None;

            public string Name(UiAction action)
            {
                int at = action - UiAction.Pick1;
                if (at >= 0 && at < _picks)
                {
                    return Names[at];
                }
                if (action == UiAction.Back && _back != null)
                {
                    return _back;
                }
                return Inner != null ? Inner.Name(action) : null;
            }

            public UiIcon Glyph(UiAction action)
            {
                int at = action - UiAction.Pick1;
                if (at >= 0 && at < _picks)
                {
                    return UiIcon.None;
                }
                if (action == UiAction.Back && _back != null)
                {
                    return UiIcon.None;
                }
                return Inner != null ? Inner.Glyph(action) : UiIcon.None;
            }
        }
    }
}
