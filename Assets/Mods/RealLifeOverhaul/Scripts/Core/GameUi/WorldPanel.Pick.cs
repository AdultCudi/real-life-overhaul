using System;
using System.Globalization;
using UI;
using UI.Elements;
using UI.Overlays;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Who has the game's one box when two panels want it (<see cref="PanelPickRules"/>): measured from the player in
    /// <see cref="Show"/>; one of the game's own panels keeps it. Costs nothing a frame.
    /// </summary>
    public sealed partial class WorldPanel
    {
        private Vector3 _for;
        private bool _forSet;

        // The last outcome logged: who had the box (a panel, or the game's panel's type), who asked, and whether it was taken.
        private static object _toldHolder;
        private static object _toldAsking;
        private static bool _toldTaken;

        /// <summary>The panel is over a person come to meet: within <see cref="PanelPickRules.TalkRange"/> the nearest place of all. Set by its owner.</summary>
        public bool Meet;

        /// <summary>The place this panel is for, measured from the player when another panel wants the box; set before <see cref="Show"/>.</summary>
        public void For(Vector3 place)
        {
            _for = place;
            _forSet = true;
        }

        // The person or thing followed, else the place given, else where the box stands.
        private Vector3 Place()
        {
            return Follow != null ? Follow.position : _forSet ? _for : Position;
        }

        private PanelClaim Claim(Vector3 player, Vector3 facing)
        {
            Vector3 place = Place();
            float dx = place.x - player.x;
            float dz = place.z - player.z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            float ahead = Mathf.Sqrt(facing.x * facing.x + facing.z * facing.z);
            float cosine = distance > 0.001f && ahead > 0.001f ? (dx * facing.x + dz * facing.z) / (distance * ahead) : 1f;
            return new PanelClaim(distance, cosine, Meet, false);
        }

        // From Show, while another panel is in the box: this one takes it by the rule.
        private bool Contest(OverlayUI box)
        {
            PlayerController player = Helpers.PlayerHelper.PlayerController;
            if (player == null)
            {
                return false;
            }
            Transform body = player.transform;
            Vector3 at = body.position;
            Vector3 facing = body.forward;
            Type type = box.GetCurrentOverlayType;
            WorldPanel holder = type == typeof(WorldPanel) ? _current : null;
            PanelClaim mine = Claim(at, facing);
            PanelClaim theirs = holder != null ? holder.Claim(at, facing) : new PanelClaim(0f, 1f, false, true);
            bool takes = holder != null && holder != this && PanelPickRules.Takes(theirs, mine);
            if (Meta.Dev.Verbose)
            {
                Tell(holder, type, mine, theirs, takes);
            }
            return takes;
        }

        // One line when the outcome between two panels changes: who has the box, and how far and how far off the facing each is.
        private void Tell(WorldPanel holder, Type type, in PanelClaim mine, in PanelClaim theirs, bool takes)
        {
            object held = holder != null ? holder : (object)type;
            if (ReferenceEquals(held, _toldHolder) && ReferenceEquals(this, _toldAsking) && takes == _toldTaken)
            {
                return;
            }
            _toldHolder = held;
            _toldAsking = this;
            _toldTaken = takes;
            string asking = Called(this) + " (" + Measured(mine) + ")";
            if (holder == null)
            {
                bool game = Theirs(InstanceBehavior<UIs>.Instance.overlayUI, type) != null;
                Logger?.Info("Panel check (who has the box): " + (game ? "the game's own panel (" : "another mod's panel (") + (type != null ? type.Name : "none")
                             + ") keeps it over " + asking + (game ? ": it is shown while the player stands in its place, and is never taken from it: PASS."
                                 : ": FAIL (it cannot be asked to step aside; the mod's panel waits until it goes)."));
                return;
            }
            string other = Called(holder) + " (" + Measured(theirs) + ")";
            float margin = PanelPickRules.Margin(PanelPickRules.Counted(theirs));
            Logger?.Info("Panel check (who has the box): " + (takes ? asking + " takes it from " + other + ": the nearer place the player faces has it: PASS."
                             : other + " keeps it over " + asking + ": the one asking does not count nearer by " + margin.ToString("F2", CultureInfo.InvariantCulture)
                               + " m (counted " + PanelPickRules.Counted(mine).ToString("F1", CultureInfo.InvariantCulture) + " against "
                               + PanelPickRules.Counted(theirs).ToString("F1", CultureInfo.InvariantCulture) + "): PASS."));
        }

        // "the stop panel": a name that already ends in "panel" gets no second one.
        private static string Called(WorldPanel panel)
        {
            string name = panel.Name ?? "place";
            return "the " + name + (name.EndsWith("panel", StringComparison.Ordinal) ? string.Empty : " panel");
        }

        private static string Measured(in PanelClaim claim)
        {
            float degrees = Mathf.Acos(Mathf.Clamp(claim.Facing, -1f, 1f)) * Mathf.Rad2Deg;
            return claim.Distance.ToString("F1", CultureInfo.InvariantCulture) + " m, " + degrees.ToString("F0", CultureInfo.InvariantCulture)
                   + " degrees off the way the player faces" + (claim.Meet && claim.Distance <= PanelPickRules.TalkRange ? ", the person met, in talking range" : string.Empty);
        }

        // Once either of the two has gone, so their next meeting is logged again.
        private void Untell()
        {
            if (ReferenceEquals(this, _toldHolder) || ReferenceEquals(this, _toldAsking))
            {
                _toldHolder = null;
                _toldAsking = null;
            }
        }

        // The game's own panel of the type in the box, from the box's public fields; null for another mod's.
        private static IOverlay Theirs(OverlayUI box, Type type)
        {
            if (box == null || type == null)
            {
                return null;
            }
            if (box.gasStation != null && box.gasStation.GetType() == type)
            {
                return box.gasStation;   // a pump, a repair bay (the Garage's kind of it), the private driver's garage
            }
            if (box.elevator != null && box.elevator.GetType() == type)
            {
                return box.elevator;
            }
            if (box.buildingEntrance != null && box.buildingEntrance.GetType() == type)
            {
                return box.buildingEntrance;
            }
            return null;
        }

        /// <summary>False while the game's box shows a button on the interact key: the mod's own use of the key then waits. Asked on a press.</summary>
        public static bool InteractFree()
        {
            if (!OverlayUI.IsVisible)
            {
                return true;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            OverlayUI box = uis != null ? uis.overlayUI : null;
            return box == null || PanelPickRules.KeyFree(true, HasInteract(box));
        }

        private static bool HasInteract(OverlayUI box)
        {
            Type type = box.GetCurrentOverlayType;
            if (type == null)
            {
                return false;
            }
            if (type == typeof(WorldPanel))
            {
                return _current != null && PanelPickRules.KeyToPanel(true, _current._slotKey[0] != null, _current._slotOn[0]);
            }
            IOverlay theirs = Theirs(box, type);
            if (theirs == null)
            {
                return true;   // another mod's panel: the game sends the key to it
            }
            ButtonInfo[] buttons = theirs.GetButtons();
            if (buttons == null)
            {
                return false;
            }
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].playerAction == PlayerAction.Interact && buttons[i].interactable)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
