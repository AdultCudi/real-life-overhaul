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
    // over a word, a glyph or a face; the numbers are the order a player presses them in (right first, then down, left and up, backing out of a dead end). One arrow joins every stop
    // to the next, N to N+1, and no arrow joins any other two, so the arrows read as the numbers do; a step that would cross words bends round them (the route with the fewest words
    // under it, through the window's gutters), and a step back to an earlier stop before pressing on is drawn dashed. Nothing is drawn outside the window, and a note in a free
    // corner says how the walk reads and how many rows wait below the fold (the list scrolls as down reaches them). The walk itself is FlareFace.Reach, the one the checks hold to
    // every target, so the picture is of the proof and not a second opinion; DrawReach throws if an arrow joins two stops that are not consecutive.
    private const float MarkerSize = 22f, MarkerText = 13f, MarkerGap = 12f;   // px of the 1920 x 1080 screen; the gap keeps two markers far enough apart for an arrow between them
    private const float CellSize = 4f, RouteStep = 12f;                       // the coarse grid the routes are scored on, and how far apart the bends are tried

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
        var markerStop = new List<int>(met);   // which stop each rectangle of markers belongs to
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
            markerStop.Add(i);
        }
        RectTransform layerRect = UiView.NewRect(parent, "Reach");
        UiView.Stretch(layerRect);
        var ink = new Color(1f, 0.85f, 0.2f, 1f);
        var dark = new Color(0.08f, 0.06f, 0.02f, 1f);
        var grid = new RouteGrid(window, spots, markers, markerStop);
        var arrows = new List<int>();               // each arrow as the stop it leaves
        var routes = new List<List<Vector2>>();     // and the route it was drawn along
        int across = 0;
        for (int i = 1; i < met; i++)
        {
            if (!placed[i - 1] || !placed[i])
            {
                continue;   // a stop below the fold has no marker to leave from or arrive at
            }
            bool back = from[i] != order[i - 1];   // pressed from an earlier stop: the walk went back to it first
            List<Vector2> path = grid.Route(place[i - 1], place[i], i - 1, i);
            across += grid.Hits(path, i - 1, i) > 0 ? 1 : 0;
            Arrow(layerRect, path, ink, back);
            grid.Mark(path);
            arrows.Add(i - 1);
            routes.Add(path);
        }
        ArrowsJoinNeighbours(arrows, routes, place, placed, met);
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
        string words = "Presses in order, 1 to " + met + ": right first, then down, left and up. Each arrow goes from one stop to the next; a dashed one goes back to an earlier stop first.";
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
        Debug.Log(Tag + "pad tour: " + met + " stops by right, down, left and up from the first target, " + hidden + " out of view, " + below + " rows below the fold, " + arrows.Count
            + " arrows each from a stop to the next, " + across + " of them with no way round the words");
    }

    // The proof that the picture reads as its numbers: the route of every arrow starts on the marker of stop N and ends on the marker of stop N+1 and on no other marker, none is
    // drawn twice, and every pair of consecutive stops that both have a marker has one.
    private static void ArrowsJoinNeighbours(List<int> arrows, List<List<Vector2>> routes, Vector2[] place, bool[] placed, int met)
    {
        var seen = new bool[met];
        for (int k = 0; k < arrows.Count; k++)
        {
            int n = arrows[k];
            if (n < 0 || n + 1 >= met || seen[n] || !placed[n] || !placed[n + 1])
            {
                throw new InvalidOperationException("pad arrows: an arrow from stop " + (n + 1) + " joins stops that are not two consecutive placed ones, or is drawn twice");
            }
            List<Vector2> route = routes[k];
            Vector2 start = route[0], end = route[route.Count - 1];
            for (int s = 0; s < met; s++)
            {
                if (!placed[s])
                {
                    continue;
                }
                bool onStart = Vector2.Distance(start, place[s]) < 0.5f, onEnd = Vector2.Distance(end, place[s]) < 0.5f;
                if (onStart != (s == n) || onEnd != (s == n + 1))
                {
                    throw new InvalidOperationException("pad arrows: the arrow from stop " + (n + 1) + " does not join stop " + (n + 1) + " to stop " + (n + 2) + " (it meets stop " + (s + 1) + ")");
                }
            }
            seen[n] = true;
        }
        for (int n = 0; n + 1 < met; n++)
        {
            if (placed[n] && placed[n + 1] && !seen[n])
            {
                throw new InvalidOperationException("pad arrows: stop " + (n + 1) + " has a marker and so has stop " + (n + 2) + " but no arrow goes from one to the other");
            }
        }
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
                cost += Overlap(m, markers[k].xMin - MarkerGap, markers[k].yMin - MarkerGap, markers[k].xMax + MarkerGap, markers[k].yMax + MarkerGap) * 100f;
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

    // The coarse picture of the window the routes are scored on: which 4 px cells hold a word, a glyph or a face, which a marker, and which an arrow already runs through. A route is a
    // polyline of the stop's two ends with at most two bends, tried through every gutter of the window; the cheapest wins (fewest words under it, then fewest markers, then the
    // shortest, then the fewest bends and the least running along an arrow already drawn), so a step round a block of words bends instead of crossing it.
    private sealed class RouteGrid
    {
        private readonly Rect _window;
        private readonly int _w, _h;
        private readonly bool[] _words;
        private readonly short[] _marker;   // the stop whose marker covers the cell, plus one
        private readonly byte[] _lines;

        public RouteGrid(Rect window, List<Spot> spots, List<Rect> markers, List<int> markerStop)
        {
            _window = window;
            _w = Mathf.CeilToInt(window.width / CellSize);
            _h = Mathf.CeilToInt(window.height / CellSize);
            _words = new bool[_w * _h];
            _marker = new short[_w * _h];
            _lines = new byte[_w * _h];
            for (int s = 0; s < spots.Count; s++)
            {
                if (spots[s].Weight >= 10f)
                {
                    Cover(spots[s].X0 - 1f, spots[s].Y0 - 1f, spots[s].X1 + 1f, spots[s].Y1 + 1f, -1);
                }
            }
            for (int m = 0; m < markers.Count; m++)
            {
                Cover(markers[m].xMin, markers[m].yMin, markers[m].xMax, markers[m].yMax, markerStop[m] + 1);
            }
        }

        // A box of cells set as words (stop -1) or as a marker's (its stop plus one).
        private void Cover(float x0, float y0, float x1, float y1, int stop)
        {
            int cx0 = Mathf.Max(0, Mathf.FloorToInt((x0 - _window.xMin) / CellSize)), cx1 = Mathf.Min(_w - 1, Mathf.FloorToInt((x1 - _window.xMin) / CellSize));
            int cy0 = Mathf.Max(0, Mathf.FloorToInt((y0 - _window.yMin) / CellSize)), cy1 = Mathf.Min(_h - 1, Mathf.FloorToInt((y1 - _window.yMin) / CellSize));
            for (int y = cy0; y <= cy1; y++)
            {
                for (int x = cx0; x <= cx1; x++)
                {
                    if (stop < 0)
                    {
                        _words[y * _w + x] = true;
                    }
                    else
                    {
                        _marker[y * _w + x] = (short)stop;
                    }
                }
            }
        }

        private bool Cell(Vector2 p, out int at)
        {
            int x = Mathf.FloorToInt((p.x - _window.xMin) / CellSize), y = Mathf.FloorToInt((p.y - _window.yMin) / CellSize);
            at = y * _w + x;
            return x >= 0 && y >= 0 && x < _w && y < _h;
        }

        /// <summary>The cheapest route from one stop's marker to the next's: straight, an L, or a Z through any gutter of the window.</summary>
        public List<Vector2> Route(Vector2 a, Vector2 b, int from, int to)
        {
            List<Vector2> best = new List<Vector2> { a, b };
            float bestCost = Cost(best, from, to);
            float r = MarkerSize / 2f;
            var tries = new List<Vector2[]> { new[] { a, new Vector2(b.x, a.y), b }, new[] { a, new Vector2(a.x, b.y), b } };
            for (float x = _window.xMin + r + 2f; x < _window.xMax - r - 2f; x += RouteStep)
            {
                tries.Add(new[] { a, new Vector2(x, a.y), new Vector2(x, b.y), b });
            }
            for (float y = _window.yMin + r + 2f; y < _window.yMax - r - 2f; y += RouteStep)
            {
                tries.Add(new[] { a, new Vector2(a.x, y), new Vector2(b.x, y), b });
            }
            for (int t = 0; t < tries.Count; t++)
            {
                var path = new List<Vector2>(4);
                for (int i = 0; i < tries[t].Length; i++)
                {
                    if (path.Count == 0 || Vector2.Distance(path[path.Count - 1], tries[t][i]) > 0.5f)
                    {
                        path.Add(tries[t][i]);
                    }
                }
                if (path.Count < 2)
                {
                    continue;
                }
                float cost = Cost(path, from, to);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = path;
                }
            }
            return best;
        }

        /// <summary>How many samples of a route lie on words (the markers' own ground, the first and last radius, left out).</summary>
        public int Hits(List<Vector2> path, int from, int to) => Count(path, from, to, true);

        private int Count(List<Vector2> path, int from, int to, bool wordsOnly)
        {
            int hits = 0;
            float skip = MarkerSize / 2f + 2f, length = Length(path), at = 0f;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                float seg = Vector2.Distance(path[i], path[i + 1]);
                for (float d = 0f; d < seg; d += 3f)
                {
                    float along = at + d;
                    if (along < skip || along > length - skip)
                    {
                        continue;
                    }
                    if (!Cell(Vector2.Lerp(path[i], path[i + 1], d / seg), out int cell))
                    {
                        hits += wordsOnly ? 1 : 100;   // outside the window
                        continue;
                    }
                    hits += _words[cell] ? (wordsOnly ? 1 : 3) : 0;
                    if (!wordsOnly)
                    {
                        int other = _marker[cell] - 1;
                        hits += other >= 0 && other != from && other != to ? 2 : 0;
                    }
                }
                at += seg;
            }
            return hits;
        }

        private static float Length(List<Vector2> path)
        {
            float len = 0f;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                len += Vector2.Distance(path[i], path[i + 1]);
            }
            return len;
        }

        private float Cost(List<Vector2> path, int from, int to)
        {
            int alongLine = 0;
            float skip = MarkerSize / 2f + 2f, length = Length(path), at = 0f;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                float seg = Vector2.Distance(path[i], path[i + 1]);
                for (float d = 0f; d < seg; d += 3f)
                {
                    if (at + d >= skip && at + d <= length - skip && Cell(Vector2.Lerp(path[i], path[i + 1], d / seg), out int cell) && _lines[cell] > 0)
                    {
                        alongLine++;
                    }
                }
                at += seg;
            }
            return Count(path, from, to, false) * 100f + length * 0.05f + (path.Count - 2) * 8f + alongLine * 0.8f;
        }

        /// <summary>Remembers where an arrow runs, so the next prefers another way where it can.</summary>
        public void Mark(List<Vector2> path)
        {
            for (int i = 0; i + 1 < path.Count; i++)
            {
                float seg = Vector2.Distance(path[i], path[i + 1]);
                for (float d = 0f; d < seg; d += 3f)
                {
                    if (Cell(Vector2.Lerp(path[i], path[i + 1], d / seg), out int cell) && _lines[cell] < 255)
                    {
                        _lines[cell]++;
                    }
                }
            }
        }
    }

    // A thin line along the route from one marker's edge to the next's with a head at the pressed-to end; dashed when the step goes back to an earlier stop first.
    private static void Arrow(RectTransform parent, List<Vector2> path, Color ink, bool dashed)
    {
        float r = MarkerSize / 2f + 1f;
        Vector2 first = path[0], last = path[path.Count - 1];
        Vector2 d0 = (path[1] - first).normalized, dn = (last - path[path.Count - 2]).normalized;
        var pts = new List<Vector2>(path);
        pts[0] = first + d0 * r;
        pts[pts.Count - 1] = last - dn * r;
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            if (!dashed)
            {
                Line(parent, pts[i], pts[i + 1], 2f, ink);
                continue;
            }
            float len = Vector2.Distance(pts[i], pts[i + 1]);
            Vector2 dir = (pts[i + 1] - pts[i]) / Mathf.Max(len, 0.01f);
            for (float d = 0f; d < len; d += 12f)
            {
                Line(parent, pts[i] + dir * d, pts[i] + dir * Mathf.Min(d + 7f, len), 2f, ink);
            }
        }
        Vector2 to = pts[pts.Count - 1], side = new Vector2(-dn.y, dn.x);
        Line(parent, to, to - dn * 8f + side * 4.5f, 2f, ink);
        Line(parent, to, to - dn * 8f - side * 4.5f, 2f, ink);
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
