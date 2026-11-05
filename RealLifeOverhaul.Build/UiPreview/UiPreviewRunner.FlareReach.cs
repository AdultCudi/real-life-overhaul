using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static partial class UiPreviewRunner
{
    // The d-pad's reach of a Flare screen as a picture a reviewer can read. Each target the walk meets gets a small numbered marker at its corner or in the gutter beside it, never
    // over a word, a glyph or a face; the numbers are the order a player presses them in (right first, then down, left and up, backing out of a dead end); a short arrow joins a stop
    // to the one it was pressed from only where the two stand side by side (a long arrow would only cross the window); nothing is drawn outside the window; and a note in a free
    // corner of the window says how the walk reads and how many rows wait below the fold (the list scrolls as down reaches them). The walk itself is FlareFace.Reach, the one
    // the checks hold to every target, so the picture is of the proof and not a second opinion.
    private const float MarkerSize = 22f, MarkerText = 13f, ArrowMost = 190f;   // px of the 1920 x 1080 screen

    private struct Spot
    {
        public float X0, Y0, X1, Y1;
        public float Weight;   // how much a marker must keep off it: a word counts most, a picture least
    }

    private static void DrawReach(RectTransform parent, UiScene scene, UiFrame frame, int[] order, int[] from, int met)
    {
        frame.Window(out float wx, out float wy, out float ww, out float wh);
        var window = new Rect(wx, wy, ww, wh);
        int start = order[0];
        byte layer = scene.Nodes[start].Layer;
        var spots = AvoidSpots(scene, layer);
        var place = new Vector2[met];
        var placed = new bool[met];
        var markers = new List<Rect>(met);
        int hidden = 0;
        for (int i = 0; i < met; i++)
        {
            if (!VisibleBox(scene, order[i], window, out Rect box))
            {
                hidden++;   // a target laid past its list's view: the walk met it, the shot cannot point at it
                continue;
            }
            place[i] = MarkerAt(box, window, spots, markers);
            placed[i] = true;
            float r = MarkerSize / 2f;
            markers.Add(new Rect(place[i].x - r, place[i].y - r, MarkerSize, MarkerSize));
        }
        RectTransform layerRect = UiView.NewRect(parent, "Reach");
        UiView.Stretch(layerRect);
        var ink = new Color(1f, 0.85f, 0.2f, 1f);
        var dark = new Color(0.08f, 0.06f, 0.02f, 1f);
        var stopOf = new int[scene.NodeCount];
        for (int i = 0; i < met; i++)
        {
            stopOf[order[i]] = i;
        }
        for (int i = 1; i < met; i++)
        {
            int a = from[i] < 0 ? -1 : stopOf[from[i]];
            if (a < 0 || !placed[a] || !placed[i])
            {
                continue;
            }
            Vector2 pa = place[a], pb = place[i];
            if (Vector2.Distance(pa, pb) > ArrowMost || SegmentHits(pa, pb, spots))
            {
                continue;   // neighbours only: a press that jumps across the window is told by the numbers, not by a line through the words
            }
            Arrow(layerRect, pa, pb, ink);
        }
        for (int i = 0; i < met; i++)
        {
            if (placed[i])
            {
                Marker(layerRect, place[i], i + 1, ink, dark);
            }
        }
        // the note in a free corner of the window
        int below = 0;
        for (int l = 0; l < scene.ListCount; l++)
        {
            below += Math.Max(0, scene.Lists[l].Count - (scene.Lists[l].First + scene.Lists[l].Shown));
        }
        string words = "Presses in order, 1 to " + met + ": right first, then down, left and up. Arrows join stops that stand together.";
        if (below > 0 || hidden > 0)
        {
            words += " Below the fold: " + (below + hidden) + " more, which down reaches as the list scrolls.";
        }
        // the note keeps off the targets' own boxes too (a button is not words, but a note over it hides it)
        var boxes = new List<Rect>();
        for (int i = 0; i < scene.NodeCount; i++)
        {
            if (scene.Nodes[i].Layer == layer && VisibleBox(scene, i, window, out Rect b))
            {
                boxes.Add(b);
            }
        }
        Note(layerRect, words, window, spots, markers, boxes);
        Debug.Log(Tag + "pad tour: " + met + " stops by right, down, left and up from the first target, " + hidden + " out of view, " + below + " rows below the fold");
    }

    // A target's box as the screen shows it: its scroll region's movement applied, cut to that region's view and to the window. False when none of it is in view.
    private static bool VisibleBox(UiScene scene, int node, Rect window, out Rect box)
    {
        scene.NodeBox(node, out float x, out float y, out float w, out float h);
        float x0 = x, y0 = y, x1 = x + w, y1 = y + h;
        short clip = scene.Nodes[node].Clip;
        if (clip >= 0)
        {
            UiClip c = scene.Clips[clip];
            x0 = Math.Max(x0, c.X);
            y0 = Math.Max(y0, c.Y);
            x1 = Math.Min(x1, c.X + c.W);
            y1 = Math.Min(y1, c.Y + c.H);
        }
        x0 = Math.Max(x0, window.xMin);
        y0 = Math.Max(y0, window.yMin);
        x1 = Math.Min(x1, window.xMax);
        y1 = Math.Min(y1, window.yMax);
        box = new Rect(x0, y0, x1 - x0, y1 - y0);
        return x1 - x0 > 4f && y1 - y0 > 4f;
    }

    // What a marker keeps off: the words (as wide as they are, not as wide as their box), glyphs, faces and sparklines of the layer, the pictures a little less.
    private static List<Spot> AvoidSpots(UiScene scene, byte layer)
    {
        var spots = new List<Spot>(scene.ItemCount);
        for (int i = 0; i < scene.ItemCount; i++)
        {
            UiItem it = scene.Items[i];
            if (it.Layer != layer || (it.Kind != UiKind.Text && it.Kind != UiKind.Icon && it.Kind != UiKind.Avatar && it.Kind != UiKind.Portrait && it.Kind != UiKind.Spark
                && it.Kind != UiKind.KeyCap && it.Kind != UiKind.Picture && it.Kind != UiKind.HueDot && it.Kind != UiKind.Slice))
            {
                continue;
            }
            float x = it.X, y = it.Y, w = it.W, h = it.H;
            float cx0 = float.MinValue, cy0 = float.MinValue, cx1 = float.MaxValue, cy1 = float.MaxValue;
            if (it.Clip >= 0)
            {
                UiClip c = scene.Clips[it.Clip];
                x += c.X;
                y += c.Y - c.Offset;
                cx0 = c.X;
                cy0 = c.Y;
                cx1 = c.X + c.W;
                cy1 = c.Y + c.H;
            }
            if (it.Kind == UiKind.Text && !string.IsNullOrEmpty(it.Text) && (it.Flags & UiFlags.Wrap) == 0)
            {
                float tw = Math.Min(w, UiTextRules.Width(it.Text, it.Style));
                x = (it.Flags & UiFlags.Right) != 0 ? x + w - tw : (it.Flags & UiFlags.Centre) != 0 ? x + (w - tw) / 2f : x;
                w = tw;
            }
            var spot = new Spot { X0 = Math.Max(x, cx0), Y0 = Math.Max(y, cy0), X1 = Math.Min(x + w, cx1), Y1 = Math.Min(y + h, cy1), Weight = it.Kind == UiKind.Picture ? 1f : 10f };
            if (spot.X1 > spot.X0 && spot.Y1 > spot.Y0)
            {
                spots.Add(spot);
            }
        }
        return spots;
    }

    // Where a stop's marker goes: centred on a corner of its target (a row's padding holds half of it), else in the gutter beside the target, whichever touches least.
    private static Vector2 MarkerAt(Rect box, Rect window, List<Spot> spots, List<Rect> markers)
    {
        float r = MarkerSize / 2f;
        Vector2[] tries =
        {
            new Vector2(box.xMin, box.yMin), new Vector2(box.xMax, box.yMin), new Vector2(box.xMin, box.yMax), new Vector2(box.xMax, box.yMax),
            new Vector2(box.xMin - r - 2f, box.center.y), new Vector2(box.xMax + r + 2f, box.center.y), new Vector2(box.center.x, box.yMin - r - 2f), new Vector2(box.center.x, box.yMax + r + 2f),
            new Vector2(box.xMin + r + 2f, box.yMin + r + 2f), new Vector2(box.xMax - r - 2f, box.yMin + r + 2f), new Vector2(box.xMin + r + 2f, box.yMax - r - 2f),
            new Vector2(box.xMax - r - 2f, box.yMax - r - 2f)
        };
        Vector2 best = tries[0];
        float bestCost = float.MaxValue;
        for (int i = 0; i < tries.Length && bestCost > 0f; i++)
        {
            Vector2 c = new Vector2(Mathf.Clamp(tries[i].x, window.xMin + r + 1f, window.xMax - r - 1f), Mathf.Clamp(tries[i].y, window.yMin + r + 1f, window.yMax - r - 1f));
            var m = new Rect(c.x - r, c.y - r, MarkerSize, MarkerSize);
            float cost = i * 0.01f;   // the corners first
            for (int s = 0; s < spots.Count; s++)
            {
                cost += Overlap(m, spots[s].X0, spots[s].Y0, spots[s].X1, spots[s].Y1) * spots[s].Weight;
            }
            for (int k = 0; k < markers.Count; k++)
            {
                cost += Overlap(m, markers[k].xMin, markers[k].yMin, markers[k].xMax, markers[k].yMax) * 100f;
            }
            if (cost < bestCost)
            {
                bestCost = cost;
                best = c;
            }
        }
        return best;
    }

    private static float Overlap(Rect m, float x0, float y0, float x1, float y1)
    {
        float w = Math.Min(m.xMax, x1) - Math.Max(m.xMin, x0), h = Math.Min(m.yMax, y1) - Math.Max(m.yMin, y0);
        return w > 0f && h > 0f ? w * h : 0f;
    }

    // Whether the segment between two markers passes through a word or a glyph (sampled every 3 px, past the markers' own discs).
    private static bool SegmentHits(Vector2 a, Vector2 b, List<Spot> spots)
    {
        float len = Vector2.Distance(a, b), skip = MarkerSize / 2f + 1f;
        for (float d = skip; d < len - skip; d += 3f)
        {
            Vector2 p = Vector2.Lerp(a, b, d / len);
            for (int s = 0; s < spots.Count; s++)
            {
                if (spots[s].Weight >= 10f && p.x > spots[s].X0 && p.x < spots[s].X1 && p.y > spots[s].Y0 && p.y < spots[s].Y1)
                {
                    return true;
                }
            }
        }
        return false;
    }

    // A thin line from one marker's edge to the next with a head at the pressed-to end.
    private static void Arrow(RectTransform parent, Vector2 a, Vector2 b, Color ink)
    {
        float len = Vector2.Distance(a, b), r = MarkerSize / 2f;
        if (len <= MarkerSize + 2f)
        {
            return;
        }
        Vector2 dir = (b - a) / len, from = a + dir * (r + 1f), to = b - dir * (r + 1f);
        Line(parent, from, to, 2f, ink);
        Vector2 side = new Vector2(-dir.y, dir.x);
        Line(parent, to, to - dir * 8f + side * 4.5f, 2f, ink);
        Line(parent, to, to - dir * 8f - side * 4.5f, 2f, ink);
    }

    private static void Line(RectTransform parent, Vector2 a, Vector2 b, float thickness, Color ink)
    {
        float k = UiTokens.CanvasPerPx;
        RectTransform seg = UiView.NewRect(parent, "Line");
        seg.pivot = new Vector2(0f, 0.5f);
        seg.anchoredPosition = new Vector2(a.x * k, -a.y * k);
        seg.sizeDelta = new Vector2(Vector2.Distance(a, b) * k, thickness * k);
        seg.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        seg.gameObject.AddComponent<Image>().color = ink;
    }

    private static void Marker(RectTransform parent, Vector2 centre, int number, Color ink, Color dark)
    {
        float k = UiTokens.CanvasPerPx;
        RectTransform dot = UiView.NewRect(parent, "Stop" + number);
        dot.pivot = new Vector2(0.5f, 0.5f);
        dot.anchoredPosition = new Vector2(centre.x * k, -centre.y * k);
        dot.sizeDelta = new Vector2(MarkerSize * k, MarkerSize * k);
        var img = dot.gameObject.AddComponent<Image>();
        img.sprite = UiSprites.Circle;
        img.type = Image.Type.Sliced;
        img.color = ink;
        RectTransform tr = UiView.NewRect(dot, "N");
        UiView.Stretch(tr);
        var t = tr.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = UiFonts.For(UiStyle.Number);
        t.fontSize = (number > 99 ? MarkerText - 3f : number > 9 ? MarkerText - 1f : MarkerText) * k;
        t.alignment = TextAlignmentOptions.Center;
        t.color = dark;
        t.enableWordWrapping = false;
        t.text = number.ToString(CultureInfo.InvariantCulture);
    }

    // The note, in the first free place of the window: the nav's empty middle, else wherever a box of its size fits without touching a word.
    private static void Note(RectTransform parent, string words, Rect window, List<Spot> spots, List<Rect> markers, List<Rect> boxes)
    {
        const float width = 232f;
        float height = UiDraw.Snap(UiTextRules.WrappedHeight(words, UiStyle.Small, width - 16f, 12)) + 16f;
        var at = Vector2.zero;
        bool found = false;
        // the nav's column first (it has a free stretch between Post and the account), then anywhere
        for (float y = window.yMin + 40f; y < window.yMax - height - 8f && !found; y += 8f)
        {
            for (float x = window.xMin + 8f; x < window.xMin + FlareFace.NavWidth - width - 8f + 16f && !found; x += 8f)
            {
                var m = new Rect(x, y, width, height);
                float cost = 0f;
                for (int s = 0; s < spots.Count && cost <= 0f; s++)
                {
                    cost += Overlap(m, spots[s].X0, spots[s].Y0, spots[s].X1, spots[s].Y1);
                }
                for (int k = 0; k < markers.Count && cost <= 0f; k++)
                {
                    cost += Overlap(m, markers[k].xMin, markers[k].yMin, markers[k].xMax, markers[k].yMax);
                }
                for (int k = 0; k < boxes.Count && cost <= 0f; k++)
                {
                    cost += Overlap(m, boxes[k].xMin, boxes[k].yMin, boxes[k].xMax, boxes[k].yMax);
                }
                if (cost <= 0f)
                {
                    at = new Vector2(x, y);
                    found = true;
                }
            }
        }
        if (!found)
        {
            return;   // a window with no free box of that size: the log line carries the same figures
        }
        float k2 = UiTokens.CanvasPerPx;
        RectTransform box = UiView.NewRect(parent, "Note");
        box.anchoredPosition = new Vector2(at.x * k2, -at.y * k2);
        box.sizeDelta = new Vector2(width * k2, height * k2);
        box.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.11f, 0.92f);
        RectTransform tr = UiView.NewRect(box, "Words");
        UiView.Stretch(tr);
        tr.offsetMin = new Vector2(8f * k2, 8f * k2);
        tr.offsetMax = new Vector2(-8f * k2, -8f * k2);
        var t = tr.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = UiFonts.For(UiStyle.Small);
        t.fontSize = 12f * k2;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.color = new Color(1f, 0.85f, 0.2f, 1f);
        t.enableWordWrapping = true;
        t.text = words;
    }
}
