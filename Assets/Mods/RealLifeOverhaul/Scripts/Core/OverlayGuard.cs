using System;
using System.Reflection;
using BAModAPI;
using Player.HUD.ItemInfoOverlays;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Keeps the game's card over a thing (the small one shown while the cursor is on it, and the large one after a click)
    /// from pointing at a thing that is gone. The game moves both cards after its thing every frame
    /// (OverlayManager.LateUpdate, OverlayBase.UpdatePosition) and takes a card away only when its thing is let go
    /// (EntityController.OnDestroy, OnIoExit) and only while the card is on screen. The time machine (a night in hospital,
    /// a sleep, a skip) hides the whole OverlayManager while it runs, so a thing removed meanwhile, by the game or by this
    /// mod, leaves its card behind: when the interface comes back the card asks its deleted thing where it is, every frame,
    /// and Player.log fills with NullReferenceExceptions (94,000 of them after a hospital stay) and the frame time with
    /// their cost.
    ///
    /// <see cref="Release"/> is called by the mod first when it removes a thing the game can point at (a car taken back or
    /// sold, a scooter, a pointer of its own), for that thing. For anything else the component checks
    /// both cards once a frame (two Unity null tests), before the game's LateUpdate, and right as the time machine ends,
    /// and puts away a card whose thing is gone, the game's own way when the card is on screen, else by switching it off.
    /// </summary>
    public sealed class OverlayGuard : MonoBehaviour
    {
        public static IModLogger Logger;

        private const int MostSaid = 5;

        private static readonly FieldInfo SimpleField = typeof(OverlayManager).GetField("simpleOverlay", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo DetailedField = typeof(OverlayManager).GetField("detailedOverlay", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static OverlayManager _manager;
        private static SimpleOverlay _simple;
        private static DetailedOverlay _detailed;
        private static bool _failed;
        private static int _said;
        private static readonly Action OnTimeMachineEnded = Check;

        /// <summary>
        /// Puts away the game's card if it is over the given thing (or a part of it), whether or not the interface is on
        /// screen. Called by the mod before it removes a thing the game can point at. Safe to call with null.
        /// </summary>
        public static void Release(EntityController thing)
        {
            if (ReferenceEquals(thing, null) || !Find())
            {
                return;
            }
            try
            {
                if (_simple != null && _simple.gameObject.activeSelf && (Same(_simple.linkedController, thing) || Same(_simple.relevantController, thing)))
                {
                    HideSimple();
                }
                if (_detailed != null && _detailed.gameObject.activeSelf && (Same(_detailed.linkedController, thing) || Same(_detailed.relevantController, thing)))
                {
                    HideDetailed();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Puts away a card whose thing is gone. Once a frame, and when the time machine ends.</summary>
        public static void Check()
        {
            if (!Find())
            {
                return;
            }
            try
            {
                if (_simple != null && _simple.gameObject.activeSelf && Gone(_simple.relevantController))
                {
                    Say("the small card", _simple.relevantController);
                    HideSimple();
                }
                if (_detailed != null && _detailed.gameObject.activeSelf && Gone(_detailed.relevantController))
                {
                    Say("the large card", _detailed.relevantController);
                    HideDetailed();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _failed = true;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Core: OverlayGuard.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (!_failed && !GameManager.isCitySceneBeingUnloaded)
            {
                Check();
            }
        }

        private void OnDestroy()
        {
            GlobalEvents.onTimeMachineEnded = (Action)Delegate.Remove(GlobalEvents.onTimeMachineEnded, OnTimeMachineEnded);
            _manager = null;
            _simple = null;
            _detailed = null;
        }

        // The game's manager and its two cards, found again for each city (a private field each: read once per city).
        private static bool Find()
        {
            if (_failed)
            {
                return false;
            }
            OverlayManager manager = InstanceBehavior<OverlayManager>.Instance;
            if (manager == null)
            {
                return false;
            }
            if (ReferenceEquals(manager, _manager))
            {
                return true;
            }
            if (SimpleField == null || DetailedField == null)
            {
                Logger?.Warn("Overlay guard: the game's cards could not be reached (OverlayManager.simpleOverlay, detailedOverlay); a card left over a removed thing is not put away.");
                _failed = true;
                return false;
            }
            _manager = manager;
            _simple = SimpleField.GetValue(manager) as SimpleOverlay;
            _detailed = DetailedField.GetValue(manager) as DetailedOverlay;
            // (The game empties its events when a city is unloaded, so the hook is made again for each city.)
            GlobalEvents.onTimeMachineEnded = (Action)Delegate.Remove(GlobalEvents.onTimeMachineEnded, OnTimeMachineEnded);
            GlobalEvents.onTimeMachineEnded = (Action)Delegate.Combine(GlobalEvents.onTimeMachineEnded, OnTimeMachineEnded);
            return true;
        }

        // The game's own hide when the card is on screen (it also clears the call to action); else the card is switched
        // off, which is all the game's hide does to it.
        private static void HideSimple()
        {
            if (_manager.isActiveAndEnabled && _simple.isActiveAndEnabled)
            {
                _manager.HideSimpleOverlayAndClearCta();
            }
            else
            {
                _simple.gameObject.SetActive(false);
                CtaManager.Clear();
            }
        }

        // As above; the game's hide also lets the player walk again and lets the small card show again.
        private static void HideDetailed()
        {
            if (_manager.isActiveAndEnabled && _detailed.isActiveAndEnabled)
            {
                _manager.HideDetailedOverlay();
            }
            else
            {
                _detailed.gameObject.SetActive(false);
                _manager.blockPlayerNavigation = false;
                _manager.EnableOverlays();
            }
        }

        // A Unity object that was made and has been destroyed since, or was never set.
        private static bool Gone(EntityController thing)
        {
            return thing == null;
        }

        private static bool Same(EntityController a, EntityController b)
        {
            return ReferenceEquals(a, b);
        }

        private static void Say(string card, EntityController thing)
        {
            if (_said >= MostSaid)
            {
                return;
            }
            _said++;
            string kind = ReferenceEquals(thing, null) ? "nothing" : thing.GetType().Name;
            Logger?.Info("Overlay guard: " + card + " of the game pointed at a " + kind + " that was removed"
                         + (_manager.isActiveAndEnabled ? string.Empty : " while the interface was hidden") + "; put away."
                         + (_said == MostSaid ? " (Said no more this session.)" : string.Empty));
        }
    }
}
