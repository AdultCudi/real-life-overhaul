using System;
using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The session record, part: the city map. The map pauses the game, so the minute's line leaves its frames out; this
    /// counts them, gives a frame of the map over 80 ms a line of its own, and writes one line once the map's camera is
    /// back: how long it was open, its frames, the frame it opened in and the one it shut in, with the mod's share of each.
    /// Then the map check (docs/standards/performance.md, RLO-PERF-022): every part of the mod's that changed something for
    /// the map (<see cref="MapParts"/>) is asked once a frame until none holds anything, which must be within 2 s of play
    /// after the map shut; the player's camera is set against the one before the map, and the frames of play and the mod's
    /// own scripts after it against the minute before it. COST: with Developer diagnostics on, a static read a frame while
    /// the map is shut; while it is open or its check runs, a few sums and the parts' looks (counts, no allocation) a frame;
    /// two lines each time the map is opened.
    /// </summary>
    public sealed partial class SessionWatch
    {
        private const float MapBackMost = 5f;         // seconds after the map shut its camera may take to come back before the check starts anyway
        private const float MapRestoredWithin = 2f;   // seconds of play after the map shut in which every part must be back as it was
        private const float MapGivesUp = 15f;         // seconds of play after which a part still holding something is written down as such
        private const int MapAfterFrames = 120;       // frames of play after the parts were back, set against the minute before the map
        private const float MapAfterMost = 5f;        // seconds those frames may take at most
        private const int MapLongMost = 12;           // lines for long frames of one opening of the map
        private const int MapTop = 3;                 // the mod's scripts named for the frame it opened in and the one it shut in
        private const double MapNamed = 0.05;         // milliseconds a script must take in that frame to be named

        private enum MapStage
        {
            Shut,       // nothing to do but a look at whether it opened
            Open,       // its frames counted
            Closing,    // shut, its camera on its way back down, the game still paused
            Checking,   // play is back: the parts asked once a frame
            After       // the parts are back: the frames of play after it counted
        }

        private MapStage _map;
        private bool _mapBroken;                      // the map's part faulted once: left out for the rest of the session
        private int _mapOpenedFrame = -1, _mapShutFrame = -1;   // the frames the game's event came in; -1 when not seen
        private float _mapOpenedAt, _mapShutAt, _mapPlayAt, _mapBackAt;
        private int _mapFrames, _map33, _map50, _mapLong, _mapWayBack, _mapBackFrames, _mapAfter;
        private double _mapSum, _mapOwnSum, _mapOwnWorst, _mapAfterSum, _mapAfterOwn;
        private float _mapWorst, _mapWayBackWorst;
        private int _mapOwnWorstSlot = -1;
        private double _mapOwnWorstSlotMs;
        private float _mapOpenMs = -1f, _mapShutMs = -1f;
        private double _mapOpenOwn, _mapShutOwn;
        private readonly int[] _mapOpenSlots = new int[MapTop];
        private readonly int[] _mapShutSlots = new int[MapTop];
        private readonly double[] _mapOpenSlotMs = new double[MapTop];
        private readonly double[] _mapShutSlotMs = new double[MapTop];
        private double _mapFrameBefore = -1.0, _mapOwnBefore = -1.0;   // milliseconds a frame of play, and of the mod's scripts, before the map
        private double _minuteFrameMs = -1.0, _minuteOwnMs = -1.0;     // the last whole minute's, for a map opened early in a minute

        // The player's camera as the map opened: read in the mod's first handler on the map's event (Core/MapTreeGuard),
        // before anything is changed for the map.
        private Camera _mapCamera;
        private int _mapMask;
        private float _mapFar;
        private bool _mapSpherical;
        private readonly float[] _mapDistances = new float[32];

        /// <summary>
        /// The game's map event, from the mod's first handler on it (Core/MapTreeGuard), before anything else is changed for
        /// the map. Nothing while the record is off.
        /// </summary>
        public static void MapToggled(bool open)
        {
            SessionWatch watch = _instance;
            if (watch == null || !watch.enabled || watch._mapBroken)
            {
                return;
            }
            try
            {
                if (open)
                {
                    watch.MapOpening(true);
                }
                else if (watch._map == MapStage.Open)
                {
                    watch._map = MapStage.Closing;
                    watch._mapShutFrame = Time.frameCount;
                    watch._mapShutAt = Time.unscaledTime;
                }
            }
            catch (Exception e)
            {
                watch.MapFault(e);
            }
        }

        // The map opens: its sums start, the minute before it and the player's camera are kept. atEvent false: the record
        // found it open (it came on while the map was open, or the event was missed), so what was before is not known.
        private void MapOpening(bool atEvent)
        {
            if (_map == MapStage.Checking || _map == MapStage.After)
            {
                MapCheck(true);   // opened again before its check was over: the line with what was seen
            }
            if (_map == MapStage.Open)
            {
                return;
            }
            _map = MapStage.Open;
            float now = Time.unscaledTime;
            _mapOpenedFrame = atEvent ? Time.frameCount : -1;
            _mapOpenedAt = now;
            _mapShutFrame = -1;
            _mapShutAt = now;
            _mapFrames = _map33 = _map50 = _mapLong = _mapWayBack = _mapBackFrames = _mapAfter = 0;
            _mapSum = _mapOwnSum = _mapOwnWorst = _mapAfterSum = _mapAfterOwn = 0.0;
            _mapWorst = _mapWayBackWorst = 0f;
            _mapOwnWorstSlot = -1;
            _mapOwnWorstSlotMs = 0.0;
            _mapOpenMs = _mapShutMs = -1f;
            _mapOpenOwn = _mapShutOwn = 0.0;
            // The frames of play before it: this minute's so far, or the last whole minute's if this one has hardly begun.
            if (_frames >= 60)
            {
                _mapFrameBefore = (now - _since) * 1000.0 / _frames;
                _mapOwnBefore = HotSpots.Frames > 0 ? HotSpots.TotalMs / HotSpots.Frames : 0.0;
            }
            else
            {
                _mapFrameBefore = _minuteFrameMs;
                _mapOwnBefore = _minuteOwnMs;
            }
            Camera camera = atEvent ? GameManager.GetMainCamera() : null;
            _mapCamera = camera;
            if (camera != null)
            {
                _mapMask = camera.cullingMask;
                _mapFar = camera.farClipPlane;
                _mapSpherical = camera.layerCullSpherical;
                Array.Copy(camera.layerCullDistances, _mapDistances, _mapDistances.Length);   // the engine hands out a copy: one each time the map opens
            }
        }

        // In OnUpdate, before a paused frame is left out (the map pauses the game): the map's frames, the frame it opened in
        // and the one it shut in, and its check once play is back. True for a frame of the map that is to have a line of its
        // own (a long one).
        private bool MapFrame(float took, bool running)
        {
            if (_mapBroken)
            {
                return false;
            }
            try
            {
                return MapStep(Time.frameCount - 1, took, running);
            }
            catch (Exception e)
            {
                MapFault(e);
                return false;
            }
        }

        private bool MapStep(int frame, float took, bool running)
        {
            float now = Time.unscaledTime;
            bool open = CityMap.IsOpen;
            switch (_map)
            {
                case MapStage.Shut:
                    if (open)
                    {
                        MapOpening(false);
                    }
                    return false;
                case MapStage.Open:
                    if (!open)
                    {
                        _map = MapStage.Closing;   // shut with no event seen: the frame it shut in is not known
                        _mapShutAt = now;
                        return false;
                    }
                    if (frame < _mapOpenedFrame || !Application.isFocused)
                    {
                        return false;   // a frame from before the event (the record ran before it in that frame), or the game in the background
                    }
                    if (frame == _mapOpenedFrame)
                    {
                        _mapOpenMs = took;
                        _mapOpenOwn = MapTopOf(frame, _mapOpenSlots, _mapOpenSlotMs);
                    }
                    else
                    {
                        MapCount(frame, took);
                    }
                    return took > LongFrame && ++_mapLong <= MapLongMost;
                case MapStage.Closing:
                    if (open)
                    {
                        MapOpening(false);   // opened again with no event seen
                        return false;
                    }
                    if (frame == _mapShutFrame)
                    {
                        _mapShutMs = took;
                        _mapShutOwn = MapTopOf(frame, _mapShutSlots, _mapShutSlotMs);
                    }
                    else if (frame < _mapShutFrame && frame > _mapOpenedFrame && Application.isFocused)
                    {
                        MapCount(frame, took);   // the map's last frame, when the record ran after the event in the frame it shut in
                    }
                    else if (frame > _mapShutFrame && Application.isFocused)
                    {
                        _mapWayBack++;
                        _mapWayBackWorst = Mathf.Max(_mapWayBackWorst, took);
                    }
                    bool longOne = frame >= _mapShutFrame && took > LongFrame && ++_mapLong <= MapLongMost;
                    if ((running && frame > _mapShutFrame) || now - _mapShutAt > MapBackMost)
                    {
                        MapLine();
                        _map = MapStage.Checking;
                        _mapPlayAt = now;
                    }
                    return longOne;
                case MapStage.Checking:
                    _mapBackFrames++;
                    if (MapParts.Lingering(null) == 0)
                    {
                        _map = MapStage.After;
                        _mapBackAt = now;
                    }
                    else if (now - _mapPlayAt > MapGivesUp)
                    {
                        MapCheck(false);
                    }
                    return false;
                case MapStage.After:
                    if (running)
                    {
                        _mapAfter++;
                        _mapAfterSum += took * 1000.0;
                        _mapAfterOwn += HotSpots.FrameMs(frame);
                    }
                    if (_mapAfter >= MapAfterFrames || now - _mapBackAt > MapAfterMost)
                    {
                        MapCheck(false);
                    }
                    return false;
                default:
                    return false;
            }
        }

        // A frame of the open map into its sums.
        private void MapCount(int frame, float took)
        {
            _mapFrames++;
            _mapSum += took * 1000.0;
            _mapWorst = Mathf.Max(_mapWorst, took);
            if (took > 0.033f)
            {
                _map33++;
                if (took > 0.050f)
                {
                    _map50++;
                }
            }
            double own = HotSpots.FrameMs(frame);
            _mapOwnSum += own;
            if (own > _mapOwnWorst)
            {
                _mapOwnWorst = own;
                _mapOwnWorstSlot = HotSpots.FrameTop(frame, 0, out int slot, out double ms) ? slot : -1;
                _mapOwnWorstSlotMs = ms;
            }
        }

        // The mod's scripts in a frame: their milliseconds, and the largest few kept for the map's line.
        private static double MapTopOf(int frame, int[] slots, double[] slotMs)
        {
            for (int k = 0; k < MapTop; k++)
            {
                bool found = HotSpots.FrameTop(frame, k, out int slot, out double ms) && ms >= MapNamed;
                slots[k] = found ? slot : -1;
                slotMs[k] = found ? ms : 0.0;
            }
            return HotSpots.FrameMs(frame);
        }

        // The map's line, once its camera is back (or MapBackMost after it shut).
        private void MapLine()
        {
            _line.Length = 0;
            _line.Append("Session: the city map was open ").Append(Fixed(_mapShutAt - _mapOpenedAt, "F1")).Append(" s: ").Append(_mapFrames).Append(" frames");
            if (_mapFrames > 0)
            {
                _line.Append(", average ").Append(Fixed(_mapSum / _mapFrames, "F1")).Append(" ms, worst ").Append(Fixed(_mapWorst * 1000.0, "F0"))
                    .Append(" ms, over 33 ms: ").Append(_map33).Append(", over 50 ms: ").Append(_map50)
                    .Append("; the mod's own scripts ").Append(Fixed(_mapOwnSum / _mapFrames, "F2")).Append(" ms a frame on average and ")
                    .Append(Fixed(_mapOwnWorst, "F1")).Append(" ms at most");
                if (_mapOwnWorstSlot >= 0)
                {
                    _line.Append(" (mostly ").Append(HotSpots.Name(_mapOwnWorstSlot)).Append(' ').Append(Fixed(_mapOwnWorstSlotMs, "F1")).Append(" ms)");
                }
            }
            _line.Append("; the frame it opened in ");
            MapEdge(_mapOpenMs, _mapOpenOwn, _mapOpenSlots, _mapOpenSlotMs);
            _line.Append("; the frame it shut in ");
            MapEdge(_mapShutMs, _mapShutOwn, _mapShutSlots, _mapShutSlotMs);
            _line.Append("; its camera's way back down ").Append(_mapWayBack).Append(" frames, the longest ").Append(Fixed(_mapWayBackWorst * 1000.0, "F0"))
                .Append(" ms; frames of the map over ").Append(Fixed(LongFrame * 1000.0, "F0")).Append(" ms: ").Append(_mapLong)
                .Append(" (a line each, up to ").Append(MapLongMost).Append(").");
            Logger?.Info(_line.ToString());
        }

        private void MapEdge(float took, double own, int[] slots, double[] slotMs)
        {
            if (took < 0f)
            {
                _line.Append("not seen (the record came on while the map was open, or its event was missed)");
                return;
            }
            _line.Append("took ").Append(Fixed(took * 1000.0, "F0")).Append(" ms, the mod's own scripts ").Append(Fixed(own, "F1")).Append(" ms of it");
            for (int k = 0; k < MapTop && slots[k] >= 0; k++)
            {
                _line.Append(k == 0 ? " (" : ", ").Append(HotSpots.Name(slots[k])).Append(' ').Append(Fixed(slotMs[k], "F1")).Append(" ms");
            }
            if (slots[0] >= 0)
            {
                _line.Append(')');
            }
        }

        // The map check's line (docs/standards/testing.md, RLO-TST-006): once the parts are back and the frames after it
        // were seen, at MapGivesUp, or when the map opens again first (cut: then a part still on its way back is not judged).
        private void MapCheck(bool cut)
        {
            bool back = _map == MapStage.After;   // every part said it held nothing, at one look
            float now = Time.unscaledTime;
            float within = (back ? _mapBackAt : now) - _mapPlayAt;
            _line.Length = 0;
            _line.Append("Map check (after closing): the map was open ").Append(Fixed(_mapShutAt - _mapOpenedAt, "F1")).Append(" s; ");
            int lingering = MapParts.Lingering(_line);
            _line.Append("; the player's camera ");
            MapCameraWords();
            if (back)
            {
                _line.Append("; every part back ").Append(Fixed(within, "F2")).Append(" s (").Append(_mapBackFrames).Append(" frames) after play came back");
            }
            else
            {
                _line.Append("; ").Append(lingering).Append(" part(s) still holding something ").Append(Fixed(within, "F1")).Append(" s after play came back");
            }
            if (_mapAfter > 0)
            {
                _line.Append("; frames of play ").Append(Fixed(_mapAfterSum / _mapAfter, "F1")).Append(" ms on average in the ").Append(_mapAfter)
                    .Append(" after it, ").Append(_mapFrameBefore >= 0.0 ? Fixed(_mapFrameBefore, "F1") : "not known").Append(" ms in the minute before")
                    .Append("; the mod's own scripts ").Append(Fixed(_mapAfterOwn / _mapAfter, "F2")).Append(" ms a frame after it, ")
                    .Append(_mapOwnBefore >= 0.0 ? Fixed(_mapOwnBefore, "F2") : "not known").Append(" before");
            }
            _line.Append(" (wanted: every part back within ").Append(Fixed(MapRestoredWithin, "F0")).Append(" s, nothing left): ");
            if (cut && !back)
            {
                _line.Append("not judged, the map was opened again first");
            }
            else if (back && lingering == 0 && within <= MapRestoredWithin)
            {
                _line.Append("restored, 0 lingering: PASS");
            }
            else if (back && lingering == 0)
            {
                _line.Append("FAIL, restored only after ").Append(Fixed(within, "F1")).Append(" s");
            }
            else
            {
                _line.Append("FAIL, ").Append(lingering).Append(" lingering");
            }
            Logger?.Info(_line.ToString());
            _map = MapStage.Shut;
        }

        // The player's camera against the one the map opened over: its layers and far plane are the game's own to set back,
        // its draw distances by layer the mod's options' (the parts' look says whether those are the mod's again).
        private void MapCameraWords()
        {
            Camera camera = GameManager.GetMainCamera();
            if (_mapCamera == null || camera == null)
            {
                _line.Append("not known as the map opened");
                return;
            }
            if (camera != _mapCamera)
            {
                _line.Append("is another camera than the one the map opened over");
                return;
            }
            float[] distances = camera.layerCullDistances;   // a copy: once a map
            bool same = camera.layerCullSpherical == _mapSpherical;
            for (int layer = 0; layer < _mapDistances.Length && same; layer++)
            {
                same = Mathf.Approximately(distances[layer], _mapDistances[layer]);
            }
            bool mask = camera.cullingMask == _mapMask;
            bool far = Mathf.Approximately(camera.farClipPlane, _mapFar);
            if (same && mask && far)
            {
                _line.Append("as before the map (its layers, far plane and draw distances)");
                return;
            }
            _line.Append("not as before the map:");
            if (!mask)
            {
                _line.Append(" its layers ").Append(_mapMask.ToString("X8", CultureInfo.InvariantCulture)).Append(" before, ")
                    .Append(camera.cullingMask.ToString("X8", CultureInfo.InvariantCulture)).Append(" now (the game sets them back itself)");
            }
            if (!far)
            {
                _line.Append(" its far plane ").Append(Fixed(_mapFar, "F0")).Append(" m before, ").Append(Fixed(camera.farClipPlane, "F0")).Append(" m now");
            }
            if (!same)
            {
                _line.Append(" its draw distances by layer differ");
            }
        }

        private void MapFault(Exception e)
        {
            Logger?.Error(e);
            Logger?.Warn("Session: the city map's part of the record stopped after an error; the rest of the record goes on.");
            _mapBroken = true;
            _map = MapStage.Shut;
        }

        private static string Fixed(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);
    }
}
