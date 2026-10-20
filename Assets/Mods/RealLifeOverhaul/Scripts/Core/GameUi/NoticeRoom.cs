using System;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Keeps the game's notifications off the mod's info panels in the top right corner (<see cref="ShiftCard"/>: a shift
    /// or a gig, a lesson or a driving test, community service, canvassing, an outing's plan, a family objective). Every
    /// such panel carries a <see cref="NoticeRoomMark"/>; while one of them shows, the game's stack of notifications
    /// (Canvases/NotificationsUI/Container, found by <see cref="NoticeLook"/>) starts a gap under the lowest of them
    /// (<see cref="NoticeRoomRules"/>), and once none shows it is back at the game's own place. Nothing of the game's is
    /// changed but that one position, and it is put back as it was found when the panels go, the city goes or the stack
    /// is a new one.
    ///
    /// Cost: nothing per frame. It works when a panel comes on or goes off (its mark's OnEnable and OnDisable, which the
    /// engine calls when the HUD is hidden with the time machine or the map too) and when a panel moves (ShiftCard
    /// stepping under the game's own job panel): four corners of each panel showing (at most a few) and one position.
    /// </summary>
    public static class NoticeRoom
    {
        public static IModLogger Logger;

        private static readonly List<NoticeRoomMark> Marks = new List<NoticeRoomMark>(8);
        private static readonly Vector3[] Corners = new Vector3[4];
        private static RectTransform _stack;
        private static Vector2 _home;
        private static float _drop;
        private static bool _said;

        /// <summary>A panel came on, went off or moved: the stack is placed again.</summary>
        public static void Changed(NoticeRoomMark mark, bool on)
        {
            try
            {
                Marks.Remove(mark);
                if (on && mark != null)
                {
                    Marks.Add(mark);
                }
                Place();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static void Place()
        {
            RectTransform stack = Stack();
            if (stack == null)
            {
                return;
            }
            float lowest = float.NaN;
            for (int i = Marks.Count - 1; i >= 0; i--)
            {
                NoticeRoomMark mark = Marks[i];
                if (mark == null || !mark.isActiveAndEnabled)
                {
                    Marks.RemoveAt(i);
                    continue;
                }
                ((RectTransform)mark.transform).GetWorldCorners(Corners);   // (an overlay canvas: world is screen pixels)
                lowest = NoticeRoomRules.Lowest(lowest, Corners[0].y);
            }
            stack.GetWorldCorners(Corners);
            float scale = stack.lossyScale.y;
            float top = Corners[1].y + _drop * scale;   // where its top stands at its own place
            float want = NoticeRoomRules.Drop(top, lowest, scale);
            if (!NoticeRoomRules.Moves(_drop, want))
            {
                return;
            }
            _drop = want;
            stack.anchoredPosition = new Vector2(_home.x, _home.y - want);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Notice check (room): " + (want > 0f
                    ? "a panel of the mod's shows in the top right; the game's notifications start " + NoticeRoomRules.Gap + " units under its foot, "
                      + want.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " units under their own place"
                    : "no panel of the mod's in their way; the game's notifications are at their own place") + ": PASS.");
            }
        }

        // The game's stack, its own place kept the first time it is seen; a new stack (a new city) is kept anew.
        private static RectTransform Stack()
        {
            Transform found = NoticeLook.Instance != null ? NoticeLook.Instance.Container : null;
            var stack = found as RectTransform;
            if (stack == null)
            {
                if (!_said && found != null)
                {
                    _said = true;
                    Logger?.Warn("Game UI: the game's notifications are not laid out as expected; they may stand over the mod's panels in the top right.");
                }
                return null;
            }
            if (!ReferenceEquals(stack, _stack))
            {
                _stack = stack;
                _home = stack.anchoredPosition;
                _drop = 0f;
            }
            return stack;
        }
    }

    /// <summary>
    /// On one of the mod's info panels in the top right: tells <see cref="NoticeRoom"/> when it comes on and goes off.
    /// The cost: nothing per frame (it has no Update); a pass of <see cref="NoticeRoom"/> when it is switched on or off or moves.
    /// </summary>
    public sealed class NoticeRoomMark : MonoBehaviour
    {
        private void OnEnable()
        {
            NoticeRoom.Changed(this, true);
        }

        private void OnDisable()
        {
            NoticeRoom.Changed(this, false);
        }

        /// <summary>The panel moved while showing (stepped under the game's own job panel, or back).</summary>
        public void Moved()
        {
            if (isActiveAndEnabled)
            {
                NoticeRoom.Changed(this, true);
            }
        }
    }
}
