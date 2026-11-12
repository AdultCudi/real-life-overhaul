using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Whether a text field has the keyboard, the one rule every key of the mod's asks before it acts: a letter typed in a
    /// field is the field's, never a shortcut. A reader of letters, digits, arrows or the game's own key actions
    /// (<c>PlayerAction.Interact</c>, <c>Move</c>, <c>RotateLeft</c>) calls <see cref="Now"/>; Esc and Enter are left to
    /// the field and the screen that wants them. The game holds its own shortcuts the same way
    /// (<c>GameManager.HasInputSelected</c>: the selected object stands on the UI layer), so a field the mod makes is put on
    /// that layer by <see cref="OnGameLayer"/>. EconomyCheck's TypingChecks fails a source that reads keys and never asks.
    /// COST: read once a frame at most, and only in a frame a key was pressed (callers test the key first); one selected
    /// object compared with the last one, and its field looked up only when it changed. Nothing allocated.
    /// </summary>
    public static class TypingNow
    {
        private const int UiLayerFallback = 5;   // Unity's built-in UI layer, when the project names none

        private static int _frame = -1;          // the frame _now was read in
        private static bool _now;
        private static GameObject _seen;         // the selected object last looked at, and its fields
        private static TMP_InputField _tmp;
        private static InputField _legacy;
        private static int _ui = -2;             // the game's UI layer, looked up once

        /// <summary>A text field of the game's or the mod's is selected and taking the keyboard.</summary>
        public static bool Now
        {
            get
            {
                int frame = Time.frameCount;
                if (frame != _frame)
                {
                    _frame = frame;
                    _now = Read();
                }
                return _now;
            }
        }

        /// <summary>A text field is selected, taking the keyboard or not: the phone will not take the keys over one.</summary>
        public static bool Selected(EventSystem events)
        {
            GameObject on = events != null ? events.currentSelectedGameObject : null;
            return Look(on) && on.activeInHierarchy && (_tmp != null || _legacy != null);
        }

        /// <summary>Puts a field on the layer the game's shortcuts look at, so the game holds them while the field is selected.</summary>
        public static void OnGameLayer(GameObject field)
        {
            if (_ui == -2)
            {
                int layer = LayerMask.NameToLayer("UI");
                _ui = layer >= 0 ? layer : UiLayerFallback;
            }
            field.layer = _ui;
        }

        private static bool Read()
        {
            EventSystem events = EventSystem.current;
            if (!Look(events != null ? events.currentSelectedGameObject : null))
            {
                return false;
            }
            return (_tmp != null && _tmp.isFocused) || (_legacy != null && _legacy.isFocused);
        }

        // Finds the selected object's fields, once for each object that gets selected. False when nothing is selected.
        private static bool Look(GameObject on)
        {
            if (on == null)
            {
                _seen = null;
                _tmp = null;
                _legacy = null;
                return false;
            }
            if (!ReferenceEquals(on, _seen))
            {
                _seen = on;
                on.TryGetComponent(out _tmp);
                on.TryGetComponent(out _legacy);
            }
            return true;
        }
    }
}
