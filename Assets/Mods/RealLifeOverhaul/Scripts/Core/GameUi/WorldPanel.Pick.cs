using System;
using System.Globalization;
using UI;
using UI.Elements;
using UI.Overlays;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Who has the game's one box when the player is near two places that want it. The rule is
    /// <see cref="PanelPickRules"/>; this part of the class measures the two claims from the player and gives the box
    /// to the one the rule names, so every panel of the mod's goes through it (DoorPanel and JobPanel among them).
    ///
    /// What the game does, and what is done here because of it:
    ///  - the box shows one IOverlay at a time and OverlayUI.Show simply puts another in its place; nothing in the game
    ///    asks which place is nearer (UI.Overlays.OverlayUI.ShowOverlay);
    ///  - GameManager sends Interact, SecondaryInteract, SpecialInteract and Confirm to the box's own panel alone
    ///    (OverlayUI.OnPlayerActionPressed), so whatever is in the box is what the key runs;
    ///  - the game's own panels are shown by triggers the player, or the player's car, has entered (a pump, a repair
    ///    bay, the private driver's garage, a parking lift) and taken down when it leaves them; the game shows nothing
    ///    in the box at a subway entrance, a door, a cash machine or a stand.
    /// So: a panel that is not in the box asks for it in <see cref="Show"/>. If one of the mod's panels has it, the two
    /// are measured and the box goes to the nearer, faced place by the rule's margin; the one put out is in its owner's
    /// hands as before (its next <see cref="Show"/> asks again, and takes the box once the player turns or steps to its
    /// place, or once the box is free). If one of the game's own panels has it, the player stands in that place, and it
    /// keeps the box: the game has no way to be asked for it back, so it is never taken.
    ///
    /// The cost: nothing per frame. Only a <see cref="Show"/> that finds another panel in the box measures: two
    /// positions and a direction read, a few multiplications. A log line (Developer diagnostics) only when the outcome
    /// between two panels changes.
    /// </summary>
    public sealed partial class WorldPanel
    {
        private const float DegreesPerRadian = 57.29578f;

        private Vector3 _for;
        private bool _forSet;

        // The last outcome said in the log: the panel (or the game's panel's type) that had the box, the one that asked, and whether it was taken.
        private static object _toldHolder;
        private static object _toldAsking;
        private static bool _toldTaken;

        /// <summary>
        /// True when the panel stands over a person the player came to meet: within talking range they count as the
        /// nearest place of all (<see cref="PanelPickRules.TalkRange"/>). Set by its owner.
        /// </summary>
        public bool Meet;

        /// <summary>
        /// The place this panel is for (a door, a desk, a stand, a stop's pole), measured from the player when another
        /// panel wants the box. Set it before <see cref="Show"/>; the box itself may stand elsewhere (over the player's
        /// head while the place is behind the camera). A panel that follows somebody is for them.
        /// </summary>
        public void For(Vector3 place)
        {
            _for = place;
            _forSet = true;
        }

        // The place measured: the person or thing followed, else the place given, else where the box stands.
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

        // From Show, while another panel is in the box: true when this one takes it by the rule.
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

        // One line when the outcome between two panels changes: who has the box, how far and how far off the player's facing each is.
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
            string asking = "the " + Name + " panel (" + Measured(mine) + ")";
            if (holder == null)
            {
                bool game = Theirs(InstanceBehavior<UIs>.Instance.overlayUI, type) != null;
                Logger?.Info("Panel check (who has the box): " + (game ? "the game's own panel (" : "another mod's panel (") + (type != null ? type.Name : "none")
                             + ") keeps it over " + asking + (game ? ": it is shown while the player stands in its place, and is never taken from it: PASS."
                                 : ": FAIL (it cannot be asked to step aside; the mod's panel waits until it goes)."));
                return;
            }
            string other = "the " + holder.Name + " panel (" + Measured(theirs) + ")";
            float margin = PanelPickRules.Margin(PanelPickRules.Counted(theirs));
            Logger?.Info("Panel check (who has the box): " + (takes ? asking + " takes it from " + other + ": the nearer place the player faces has it: PASS."
                             : other + " keeps it over " + asking + ": the one asking does not count nearer by " + margin.ToString("F2", CultureInfo.InvariantCulture)
                               + " m (counted " + PanelPickRules.Counted(mine).ToString("F1", CultureInfo.InvariantCulture) + " against "
                               + PanelPickRules.Counted(theirs).ToString("F1", CultureInfo.InvariantCulture) + "): PASS."));
        }

        private static string Measured(in PanelClaim claim)
        {
            float degrees = Mathf.Acos(Mathf.Clamp(claim.Facing, -1f, 1f)) * DegreesPerRadian;
            return claim.Distance.ToString("F1", CultureInfo.InvariantCulture) + " m, " + degrees.ToString("F0", CultureInfo.InvariantCulture)
                   + " degrees off the way the player faces" + (claim.Meet && claim.Distance <= PanelPickRules.TalkRange ? ", the person met, in talking range" : string.Empty);
        }

        // Forgets the last outcome said once either of its two has gone, so that the next meeting of the two is said again.
        private void Untell()
        {
            if (ReferenceEquals(this, _toldHolder) || ReferenceEquals(this, _toldAsking))
            {
                _toldHolder = null;
                _toldAsking = null;
            }
        }

        // The game's own panel object of the type in the box: public fields of the box, no reflection. Null for another mod's.
        private static IOverlay Theirs(OverlayUI box, Type type)
        {
            if (box == null || type == null)
            {
                return null;
            }
            if (box.gasStation != null && box.gasStation.GetType() == type)
            {
                return box.gasStation;   // a pump, a repair bay (the Garage's own kind of it), the private driver's garage
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

        /// <summary>
        /// True unless the game's box shows a button on the game's interact key, which the game itself runs on that key:
        /// the mod's own use of the key (a door, a car) then does nothing, so one press runs one thing. Asked when the key
        /// is pressed, never per frame.
        /// </summary>
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
