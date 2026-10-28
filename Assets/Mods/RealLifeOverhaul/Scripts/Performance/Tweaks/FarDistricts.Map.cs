using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The far districts, part: the city map (docs/standards/performance.md, RLO-PERF-022). The map draws the city from
    /// above: a square comes back, lent, in the frame the map's camera first sees it, before the camera culls, of the layers
    /// the map draws; a square it never sees stays out. Once the game says its camera is back down (onCityMapClosed), the
    /// very renderers lent are taken out again, 2 ms a frame, every frame, from where it got; a square the camera is near by
    /// then stays given back. Its look for the map check says whether every square out before the map is out again.
    /// </summary>
    public sealed partial class FarDistricts
    {
        private const float CloseWait = 5f;   // seconds after the map shut by which its camera is surely back down: the latest what was lent is taken out again
        private const double AgainSliceMs = 2.0;   // milliseconds a frame for taking out again what was lent to the map, every frame until it is done
        private const float AgainEvery = 0.001f;   // seconds between ticks while that goes on: every frame
        private const float ViewMargin = 150f;   // metres around a square (twice that up and down) in which a camera is taken to see it: tall buildings, shadows

        private bool _lending;   // the map is open, or its camera on its way back: what comes back is lent, to be taken out again after it
        private bool _again;   // its camera is back: what was lent is taken out again, a slice every frame
        private int _againCell;   // the square taking out again has got to
        private Cell _workingBeforeMap;   // the square half held as the map opened: its holding goes on after the map
        private int _heldBeforeMap;   // renderers out as the map opened
        private int _backFrame = -1, _againFrame = -1;   // the frame the map's camera was back in, and the one all it was lent was out again in
        private readonly Plane[] _view = new Plane[6];   // a camera's view, for the map
        private static readonly Vector3 ViewReach = new Vector3(2f * ViewMargin, 4f * ViewMargin, 2f * ViewMargin);
        private static readonly Predicate<MeshRenderer> Dropped = renderer => ReferenceEquals(renderer, null);

        // Before the game's own handlers (see Apply). Open: what is out is noted, and from now on what the map's camera sees
        // comes back, lent (OnCameraRendering). Shut: its camera goes back down first; what was lent is taken out again once
        // it is back (OnMapClosed), or CloseWait after at the latest.
        private void OnMapToggled(bool open)
        {
            if (open)
            {
                if (!_lending)
                {
                    if (!_again)
                    {
                        _heldBeforeMap = _heldRenderers;
                        _workingBeforeMap = _working;
                    }
                    for (int i = 0; i < _cells.Count; i++)
                    {
                        Cell cell = _cells[i];
                        cell.HeldBeforeMap = cell.Held || cell.Lent.Count + cell.LentOff.Count > 0;
                    }
                }
                _lending = true;
                _again = false;
                _backFrame = _againFrame = -1;
            }
            else
            {
                _quietUntil = Time.unscaledTime + CloseWait;
            }
            _settled = false;
        }

        // The game's word that the map's camera is back down and its time runs again.
        private void OnMapClosed() => Back();

        // What was lent to the map is taken out again from the next tick, a slice every frame (Interval).
        private void Back()
        {
            if (!_lending)
            {
                return;
            }
            _lending = false;
            _again = true;
            _againCell = 0;
            _backFrame = Time.frameCount;
            _settled = false;
        }

        // After the map: the very renderers lent to it are taken out again, as many as the slice has time for, from the square
        // it got to. A square the camera is near now (a ride from the map) stays given back; the square half held as the map
        // opened goes on with its holding.
        private void HoldAgain(Vector3 eye, ref WorkSlice slice)
        {
            while (_againCell < _cells.Count)
            {
                Cell cell = _cells[_againCell];
                int lent = cell.Lent.Count + cell.LentOff.Count;
                if (lent == 0)
                {
                    _againCell++;
                    continue;
                }
                if (cell.LentCursor == 0 && cell.Bounds.SqrDistance(eye) < ReleaseWithin * ReleaseWithin)
                {
                    DropLent(cell);   // the camera is near it now: it stays in the picture
                    _againCell++;
                    continue;
                }
                for (; cell.LentCursor < lent && slice.More(); cell.LentCursor++)
                {
                    bool onMap = cell.LentCursor < cell.Lent.Count;
                    List<MeshRenderer> list = onMap ? cell.Lent : cell.LentOff;
                    int at = onMap ? cell.LentCursor : cell.LentCursor - cell.Lent.Count;
                    MeshRenderer renderer = list[at];
                    if (renderer == null || Kept(renderer) || !TakeOut(renderer))
                    {
                        list[at] = null;   // gone, its drawing the mod's own, or out already (someone else's): no longer the square's
                        continue;
                    }
                    cell.LentHeld++;
                    _heldRenderers++;
                    if (!onMap)
                    {
                        cell.LentHeldOff++;
                        _offMapHeld++;
                    }
                }
                if (cell.LentCursor < lent)
                {
                    return;   // the slice is over
                }
                cell.Lent.RemoveAll(Dropped);
                cell.LentOff.RemoveAll(Dropped);
                cell.LentCursor = cell.LentHeld = cell.LentHeldOff = 0;
                Lend(ref cell.Lent, ref cell.Taken);   // the square's own lists again
                Lend(ref cell.LentOff, ref cell.TakenOff);
                if (!cell.Held)
                {
                    cell.Held = true;
                    _heldCells++;
                }
                if (cell == _workingBeforeMap && _working == null && cell.Cursor < cell.Scenery.Count)
                {
                    _working = cell;   // its holding goes on where it stopped
                }
                _againCell++;
            }
            _again = false;
            _workingBeforeMap = null;
            _againFrame = Time.frameCount;
        }

        // Out of the picture the tweak's way; false if it is out already.
        private bool TakeOut(MeshRenderer renderer)
        {
            if (_byEnabled)
            {
                if (!renderer.enabled)
                {
                    return false;
                }
                renderer.enabled = false;
                return true;
            }
            if (renderer.forceRenderingOff)
            {
                return false;
            }
            renderer.forceRenderingOff = true;
            return true;
        }

        // The map check after the map shut (Diagnostics/SessionWatch.Map): every square out as the map opened is out again,
        // the very renderers, but those the camera is near now; nothing left to do. Counts only; words only with a line.
        private int MapLook(StringBuilder line)
        {
            if (!Active)
            {
                line?.Append("far squares: the option is off, nothing out");
                return 0;
            }
            Vector3 eye = _main != null ? _main.transform.position : Vector3.zero;
            int before = 0, missing = 0;
            for (int i = 0; i < _cells.Count; i++)
            {
                Cell cell = _cells[i];
                if (!cell.HeldBeforeMap)
                {
                    continue;
                }
                before++;
                if (!cell.Held && (_main == null || cell.Bounds.SqrDistance(eye) >= ReleaseWithin * ReleaseWithin))
                {
                    missing++;
                }
            }
            bool busy = _lending || _again;
            if (line != null)
            {
                line.Append("far squares: ").Append(busy ? "still being taken out again, " : string.Empty).Append(_heldCells).Append(" of ").Append(_cells.Count)
                    .Append(" out (").Append(_heldRenderers).Append(" renderers, ").Append(_heldBeforeMap).Append(" as the map opened), ").Append(before - missing)
                    .Append(" of the ").Append(before).Append(" out before it out again");
                if (_backFrame >= 0 && _againFrame >= 0)
                {
                    line.Append(", in ").Append(_againFrame - _backFrame).Append(" frame(s) after its camera was back");
                }
            }
            return busy || missing > 0 ? 1 : 0;
        }

        // While the map is open, or its camera on its way back: each square the camera sees (ViewMargin around it) comes back,
        // of the layers the map draws, lent, before the camera culls; the rest stays out. A square comes back in the frame it is
        // first seen, never later (no hole), and no frame gives back what it does not see.
        private void LendInView(Camera camera)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, _view);   // into the array it is given
            for (int i = 0; i < _cells.Count; i++)
            {
                Cell cell = _cells[i];
                if (cell.Taken.Count == 0 && cell.LentHeld == 0)
                {
                    continue;   // nothing of the map's layers out
                }
                Bounds seen = cell.Bounds;
                seen.Expand(ViewReach);
                if (GeometryUtility.TestPlanesAABB(_view, seen))
                {
                    LendForMap(cell);
                    _settled = false;
                }
            }
        }

        // A square the map sees: what it draws comes back, lent; the rest stays out, and the square counts as given back.
        private void LendForMap(Cell cell)
        {
            if (_working == cell)
            {
                _working = null;
            }
            if (cell.LentHeld > 0)
            {
                GiveBackLent(cell);
            }
            Show(cell.Taken);
            _heldRenderers -= cell.Taken.Count;
            Lend(ref cell.Taken, ref cell.Lent);
            if (cell.Held)
            {
                cell.Held = false;
                _heldCells--;
            }
        }

        // Out, now lent (or back the other way, once out again): the lists swap, nothing copied or allocated; if the other
        // list has some already, they join.
        private static void Lend(ref List<MeshRenderer> from, ref List<MeshRenderer> into)
        {
            if (from.Count == 0)
            {
                return;
            }
            if (into.Count == 0)
            {
                List<MeshRenderer> swap = into;
                into = from;
                from = swap;
                return;
            }
            into.AddRange(from);
            from.Clear();
        }

        // What was taken out again after the map so far comes back (the square is lent again, or given back whole); the rest
        // of what was lent is in the picture already.
        private void GiveBackLent(Cell cell)
        {
            int onMap = Math.Min(cell.LentCursor, cell.Lent.Count);
            ShowFirst(cell.Lent, onMap);
            ShowFirst(cell.LentOff, cell.LentCursor - onMap);
            _heldRenderers -= cell.LentHeld;
            _offMapHeld -= cell.LentHeldOff;
            cell.LentCursor = cell.LentHeld = cell.LentHeldOff = 0;
        }

        // What was lent is let go: it stays in the picture.
        private void DropLent(Cell cell)
        {
            if (cell.LentHeld > 0)
            {
                GiveBackLent(cell);   // never one left out that no list knows
            }
            cell.Lent.Clear();
            cell.LentOff.Clear();
            cell.LentCursor = 0;
        }
    }
}
