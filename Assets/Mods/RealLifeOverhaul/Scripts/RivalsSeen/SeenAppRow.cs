using System;
using BigAmbitions.Rivals;
using Localizor;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Smartphone.Apps.Rivals;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// A rival's head office on the game's own Rivals app, for the old interface: a line of the game's own district text, copied, and a copy of its Open in Contacts button turned
    /// into Show on map, both made once beside their originals inside the app's own layout and shown for a rival who has a head office. Written when the game announces a rival chosen
    /// in the app (its own event, once a click, never per frame). The kit hides that layout while it draws the app, so the copies stand down with it. Taken away when the feature
    /// stops. COST: two copies once; a lookup and two text writes a click.
    /// </summary>
    internal static class SeenAppRow
    {
        private const string Chosen = "ba:gameevent_rivalselectedinapp";   // the game's own, as RivalsApp.ShowRival raises it
        private static GameObject _line, _button;
        private static string _forId;
        private static Action<string> _onEvent, _seen;

        /// <summary>Keeps the handler on the game's event (the game resets it on a load); asked once a second by the director.</summary>
        public static void Keep()
        {
            _onEvent = _onEvent ?? Guard.Wrap<string>(OnEvent);
            EventHold.Keep(ref GameEvent.onGameEventTriggered, _onEvent, ref _seen);
        }

        public static void Stop()
        {
            if (_onEvent != null)
            {
                GameEvent.onGameEventTriggered -= _onEvent;
            }
            _seen = null;
            Clear();
        }

        /// <summary>The copies destroyed: the feature stops, the city unloads, or the game's panel is not as expected.</summary>
        public static void Clear()
        {
            if (_line != null)
            {
                UnityEngine.Object.Destroy(_line);
            }
            if (_button != null)
            {
                UnityEngine.Object.Destroy(_button);
            }
            _line = _button = null;
            _forId = null;
        }

        private static void OnEvent(string name)
        {
            if (name != Chosen)
            {
                return;
            }
            SeenDirector seen = SeenDirector.Instance;
            RivalsApp app = InstanceBehavior<RivalsApp>.Instance;
            if (seen == null || !seen.Running || app == null)
            {
                return;
            }
            var panel = GameContract.RivalsChosen.Field?.GetValue(app) as SelectedRivalUI;
            var contacts = GameContract.RivalsContacts.Field?.GetValue(panel) as Button;
            var hood = GameContract.RivalsHoodText.Field?.GetValue(panel) as Component;
            if (panel == null || contacts == null || hood == null)
            {
                return;
            }
            string id = app.selectedRival != null ? app.selectedRival.rivalId : null;
            string head = !string.IsNullOrEmpty(id) && !app.selectedRival.isDefeated ? seen.OwnerHead(id) : string.Empty;
            if (head.Length == 0)
            {
                _forId = null;
                _line?.SetActive(false);
                _button?.SetActive(false);
                return;
            }
            if (_line == null || _button == null)
            {
                Build(hood, contacts);
            }
            if (_line == null || _button == null)
            {
                return;
            }
            _forId = id;
            UiClone.Words(_line.transform, SeenText.Fill("reallife_seen_app_head", "address", head));
            _line.SetActive(true);
            _button.SetActive(true);
        }

        // A copy made asleep (UiClone) put beside its original in the game's own layout, shown.
        private static GameObject Brought(GameObject copy, Transform beside)
        {
            copy.transform.SetParent(beside.parent, false);
            copy.transform.SetSiblingIndex(beside.GetSiblingIndex() + 1);
            return copy;
        }

        // Once: the district text's object and the contacts button's object copied beside themselves, the button given Show on map and a click that asks for the head office of the rival
        // last chosen.
        private static void Build(Component hood, Button contacts)
        {
            try
            {
                Transform dormant = UiClone.Dormant(hood.transform.parent, "RealLifeOverhaul head office copies");
                _line = Brought(UiClone.Asleep(hood.gameObject, dormant, "RealLifeOverhaul head office"), hood.transform);
                _button = Brought(UiClone.Asleep(contacts.gameObject, dormant, "RealLifeOverhaul show head office"), contacts.transform);
                var label = _button.GetComponentInChildren<TMP_Text>(true);
                UiClone.Key(label, "reallife_career_button_map");
                UiClone.Rewire(_button.transform, () => SeenDirector.Instance?.ShowOwnerHead(_forId));
            }
            catch (Exception e)
            {
                SeenDirector.Logger?.Error(e);
                SeenDirector.Logger?.Warn("Rivals seen: the head office could not be added to the game's Rivals app; the map and the kit's page still show it.");
                Clear();
            }
        }
    }
}
