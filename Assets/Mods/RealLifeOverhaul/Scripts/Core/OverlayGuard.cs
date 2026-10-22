using System;
using System.Reflection;
using BAModAPI;
using Player.HUD.ItemInfoOverlays;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Keeps the game's small and large cards over a thing from pointing at a thing that is gone. The game takes a card
    /// away only while it is on screen, and the time machine hides the whole OverlayManager, so a thing removed during a
    /// sleep or hospital stay leaves its card asking a destroyed object for its place every frame (a log full of
    /// NullReferenceExceptions). The mod calls <see cref="Release"/> before removing a thing; for anything else both
    /// cards are checked once a frame (two Unity null tests) and when the time machine ends.
    /// </summary>
    public sealed class OverlayGuard : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "OverlayGuard", typeof(OverlayGuard));

        public static IModLogger Logger;

        private const int MostSaid = 5;

        private static readonly FieldInfo SimpleField = GameContract.OverlaySimple.Field;
        private static readonly FieldInfo DetailedField = GameContract.OverlayDetailed.Field;

        private static OverlayManager _manager;
        private static SimpleOverlay _simple;
        private static DetailedOverlay _detailed;
        private static bool _failed;
        private static int _said;
        private static readonly Action OnTimeMachineEnded = Guard.Wrap(FrameGuard, Check);

        /// <summary>Puts away a card over <paramref name="thing"/> before the mod removes it, on screen or not; null is safe.</summary>
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
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
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

        // The game's manager and its two cards, read once per city.
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
            // The game empties its events when a city unloads, so the hook is made again for each city.
            GlobalEvents.onTimeMachineEnded = (Action)Delegate.Remove(GlobalEvents.onTimeMachineEnded, OnTimeMachineEnded);
            GlobalEvents.onTimeMachineEnded = (Action)Delegate.Combine(GlobalEvents.onTimeMachineEnded, OnTimeMachineEnded);
            return true;
        }

        // The game's own hide when on screen; else switched off, which is all that hide does to the card.
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

        // As above; the game's hide also frees the player's walking and the small card.
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

        // A Unity object destroyed since, or never set.
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
