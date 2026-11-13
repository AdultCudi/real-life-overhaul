using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The rules of dragging a scroll region with the mouse, as the game's ScrollRect follows a pointer, game-free so the checks run them (UiView.Pointer carries them
    /// out): which region a press takes, where its content should stand for the pointer's distance, and how long a wheel notch is. Cost: a pass over the scene's regions
    /// at the start of a drag and a few sums at each move of it.
    /// </summary>
    public static class UiPanRules
    {
        /// <summary>
        /// The innermost scroll region holding a place that can move the way the drag goes (a drag down shows what is above: it needs room above its view; a drag up, room
        /// below it); -1 for none. Over a sheet or a list standing on a higher layer only the regions that layer's own targets stand in.
        /// </summary>
        public static int ClipAt(UiScene s, float x, float y, float dy)
        {
            int top = UiFocusRules.TopLayer(s), best = -1;
            float area = float.MaxValue;
            for (int i = 0; i < s.ClipCount; i++)
            {
                ref UiClip c = ref s.Clips[i];
                if (x < c.X || x > c.X + c.W || y < c.Y || y > c.Y + c.H || c.ContentHeight <= c.H + 0.5f || c.W * c.H >= area)
                {
                    continue;
                }
                if (dy > 0f ? c.Offset <= 0.5f : c.Offset >= c.ContentHeight - c.H - 0.5f)
                {
                    continue;
                }
                if (top > 0 && !HasTarget(s, i, top))
                {
                    continue;
                }
                area = c.W * c.H;
                best = i;
            }
            return best;
        }

        /// <summary>
        /// The open drop-down list whose thumb strip (the right edge of its rows, a hand's width) holds a place, or -1: a press there drags the thumb, which moves the list
        /// at the content's pace of the thumb's travel, whichever way the drag goes.
        /// </summary>
        public static int ThumbAt(UiScene s, float x, float y)
        {
            for (int i = 0; i < s.ClipCount; i++)
            {
                ref UiClip c = ref s.Clips[i];
                if (c.Choice && c.ContentHeight > c.H + 0.5f && x >= c.X + c.W - UiTokens.S4 && x <= c.X + c.W && y >= c.Y && y <= c.Y + c.H)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Where the content's offset should be once the thumb has been dragged <paramref name="moved"/> px down from where it was taken.</summary>
        public static float ThumbWant(float start, float moved, float content, float view)
        {
            float thumb = Math.Max(UiTokens.S6, view * view / Math.Max(1f, content)), travel = Math.Max(1f, view - thumb);
            return Math.Max(0f, Math.Min(start + moved * (content - view) / travel, Math.Max(0f, content - view)));
        }

        private static bool HasTarget(UiScene s, int clip, int layer)
        {
            for (int i = 0; i < s.NodeCount; i++)
            {
                if (s.Nodes[i].Clip == clip && s.Nodes[i].Layer == layer)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Where the content's offset should be once the pointer has moved <paramref name="moved"/> px down from where the drag began: the content follows it, stopping at its ends.</summary>
        public static float Want(float start, float moved, float content, float view) => Math.Max(0f, Math.Min(start - moved, Math.Max(0f, content - view)));

        /// <summary>The wheel notches (positive up, which lowers the offset) that move the offset by <paramref name="change"/> when a notch is <paramref name="step"/> px long.</summary>
        public static float Notches(float change, float step) => -change / Math.Max(1f, step);

        /// <summary>A notch's length as the last one measured it, or the old length when it is not a length that makes sense.</summary>
        public static float Step(float moved, float notches, float old)
        {
            float learned = notches == 0f ? 0f : Math.Abs(moved / notches);
            return learned > 1f && learned < 4096f ? learned : old;
        }
    }
}
