using RealLifeOverhaul.FitOut;
using RealLifeOverhaul.Tech;
using UnityEngine;

namespace RealLifeOverhaul.Law
{
    /// <summary>The floor the furnished station leaves bare, fitted out as a city office's back rooms by FitOut/FitView.</summary>
    internal sealed partial class StationInterior
    {
        private FitView _fill;
        private readonly FitView.Request _fillRequest = new FitView.Request();

        private void BeginFill()
        {
            if (_fill == null)
            {
                _fill = new FitView(_logger);
                _fill.Built += v => _logger?.Info("Law: " + v.Said + ".");
            }
            _fill.End();
            if (!Settings.LawStationFurniture || !_cast || FitOutDirector.Instance == null || _building == null || Precinct == null)
            {
                return;
            }
            RoomGrid grid = RoomMeasure.Measure(_building, _origin, false, out float floor);
            if (grid == null)
            {
                return;
            }
            FitView.Request r = _fillRequest;
            r.Reset();
            r.Name = Precinct.Name + "'s back office";
            r.Kind = FitKind.CivicOffice;
            r.Seed = Precinct.Index * 7919 + 17;
            r.Building = _building;
            r.Grid = grid;
            r.Floor = floor;
            r.Door = _origin;
            r.Inward = _in;
            r.Look = false;   // the station has put its own design's walls and floors on
            r.Site.ZoneLamps = !_designLamps;
            for (int i = 0; i < _fixedRects.Count; i++)
            {
                r.Site.Fixed.Add(World(_fixedRects[i], true));
            }
            for (int i = 0; i < _staged.Count; i++)
            {
                if (!_staged[i].Dropped && _staged[i].Floor)
                {
                    r.Site.Fixed.Add(World(_staged[i].Rect, true));
                }
            }
            for (int i = 0; i < _taken.Count; i++)
            {
                Taken t = _taken[i];
                r.Site.KeepFree.Add(World(new FloorRect(t.A, t.D, t.HalfA, t.HalfD), false));
            }
            for (int i = 0; i < _hung.Count; i++)
            {
                r.Site.Fixed.Add(FitRect.Around(_hung[i].x, _hung[i].z, 0.6f, 0.6f, false, wall: true));
            }
            Hung(_board);
            Hung(_evidence);
            Keep(ServiceSpot);
            Keep(_sergeantAt);
            if (HasCell)
            {
                Keep(At(_cellBack - _cellDeep - 0.8f, _cellAside));
            }
            for (int i = 0; i < _seats.Count; i++)
            {
                Keep(_seats[i].Stand);
            }
            _fill.Begin(r, FitOutDirector.Instance);
        }

        private void Hung(Transform piece)
        {
            if (piece != null && piece.gameObject.activeSelf)
            {
                _fillRequest.Site.Fixed.Add(FitRect.Around(piece.position.x, piece.position.z, 0.9f, 0.9f, false, wall: true));
            }
        }

        private void Keep(Vector3 at)
        {
            _fillRequest.Site.Keep.Add(new FitSpot(at.x, at.z, 0.5f));
        }

        // A rectangle of the room's frame (aside, depth from the door) in world axes.
        private FitRect World(FloorRect r, bool solid)
        {
            Vector3 middle = _origin + _right * r.A + _in * r.D;
            float halfX = Mathf.Abs(_right.x) * r.HalfA + Mathf.Abs(_in.x) * r.HalfD;
            float halfZ = Mathf.Abs(_right.z) * r.HalfA + Mathf.Abs(_in.z) * r.HalfD;
            return FitRect.Around(middle.x, middle.z, halfX, halfZ, solid);
        }

        private void TickFill(float now)
        {
            _fill?.Tick(now);
        }

        private void EndFill()
        {
            _fill?.End();
        }
    }
}
