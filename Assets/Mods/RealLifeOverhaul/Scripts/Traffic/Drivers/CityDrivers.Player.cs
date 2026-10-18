using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city's drivers and the player: the player's car, the game's parking room behind it and the player on foot as
    /// things on the road, and the two hold lines: one that lets the player out of a space, one that leaves them room to
    /// reverse. A line goes up only once every car that could not stop for it gently has gone by.
    /// </summary>
    public sealed partial class CityDrivers : MonoBehaviour
    {
        private const int LetOutLine = 0, ReverseLine = 1;
        private const float LineWidth = 2.4f;        // metres across a lane a hold line spans: a car's sensor anywhere in the lane meets it, one in the next lane does not
        private const float LineDepth = 0.8f;        // metres along the lane (DriverGame's box)
        private const float WantedFor = 1.5f;        // seconds a waiting line keeps waiting for its reason to come back
        private const float ReverseEndAfter = 3f;    // seconds after the player last reversed that the room behind them is given back

        /// <summary>One hold line's state.</summary>
        private sealed class Line
        {
            public bool Pending;          // wanted: waiting for the cars that cannot stop for it gently to go by
            public bool Up;
            public bool Blocked;          // a car of this look could not stop for it gently
            public int WentBy;            // looks it waited for such cars
            public Vector3 Place;
            public Vector3 Way;           // the way the lane runs there
            public float WantedAt;        // when its reason was last seen
            public float UpAt;
            public bool Tested;           // put up by a test button: it holds its full time
            public int Obstacle = -1;     // its place among this look's obstacles while it is up
        }

        private readonly Line[] _lines = { new Line(), new Line() };

        // The player, as read at this look.
        private Rigidbody _playerBody;
        private bool _playerDriving;
        private Vector3 _playerMiddle;
        private Vector3 _playerForward;
        private float _playerHalfLength, _playerHalfWidth;
        private float _playerAlong;           // its speed along the way it faces, m/s (negative: reversing)
        private float _playerStillAt;         // when it last stood still
        private float _stoodSince;            // since when it has stood still without a break; 0 while it moves
        private float _reversedAt;            // when it last reversed
        private Waypoint _playerLane;         // the lane point of the lane it is in or beside
        private Vector3 _laneWay;             // the way that lane runs
        private float _playerOff;             // metres from that lane's line to its middle (positive: to the lane's right)
        private float _playerAngle;           // degrees from the lane's way to its own (positive: pointing to the lane's right)
        private int _letOutHeld;              // cars that gave way for the let-out line that is up
        private float _playerLastAlong, _playerLastAt, _playerSlowing;   // its speed at the last look, when, and how fast it is losing speed
        private float _playerKeep;            // metres kept behind its car beyond the game's sensor at this look (0 on foot)

        /// <summary>Hold lines up now.</summary>
        private int LinesUp => (_lines[LetOutLine].Up ? 1 : 0) + (_lines[ReverseLine].Up ? 1 : 0);

        // The player at this look: as something on the road, and what their car is doing at the kerb.
        private void ReadPlayer(TrafficManager traffic, Vector3 player, float now)
        {
            VehicleController car = _game.PlayerCar(out Rigidbody body);
            _playerBody = body;
            _playerKeep = 0f;
            if (car == null)
            {
                if (_playerDriving)
                {
                    _playerDriving = false;
                    _stoodSince = 0f;
                    EndLine(LetOutLine, "the player has left the car", now);
                    EndLine(ReverseLine, "the player has left the car", now);
                }
                // On foot in the road: the game's sensor stops for the player 3 m off; here the cars see them in time.
                if (HornReactions.PlaceOf(player, out _) != StreetRules.Place.Pavement)
                {
                    _obstacles.Add(DriverRules.Why.OnFoot, -1, player.x, player.z, 1f, 0f, 0f, 0f, 0.3f, 0.3f, 0f);
                }
                return;
            }
            _playerDriving = true;
            Transform frame = car.transform;
            Vector3 forward = frame.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            Vector3 velocity = body.velocity;
            _playerForward = forward;
            _playerMiddle = frame.position + forward * _game.PlayerMiddle;
            _playerHalfLength = _game.PlayerHalfLength;
            _playerHalfWidth = _game.PlayerHalfWidth;
            _playerAlong = velocity.x * forward.x + velocity.z * forward.z;
            float since = now - _playerLastAt;
            _playerSlowing = _playerLastAt > 0f && since > 0.01f && since < 0.5f
                ? Mathf.Max(0f, (_playerSlowing + (_playerLastAlong - _playerAlong) / since) * 0.5f) : 0f;
            _playerLastAlong = _playerAlong;
            _playerLastAt = now;
            if (Mathf.Abs(_playerAlong) < DriverRules.CreepFrom)
            {
                _playerStillAt = now;
                if (_stoodSince <= 0f)
                {
                    _stoodSince = now;
                }
            }
            else
            {
                _stoodSince = 0f;
            }
            if (DriverRules.Reversing(_playerAlong))
            {
                _reversedAt = now;
            }

            // The lane it is in or beside, and how it stands to it.
            _playerLane = DriverGame.LaneAt(_playerMiddle, forward);
            _laneWay = _playerLane != null ? DriverGame.LaneWay(traffic, _playerLane) : Vector3.zero;
            bool kerb = false;
            if (_laneWay != Vector3.zero)
            {
                Vector3 right = Vector3.Cross(Vector3.up, _laneWay);
                _playerOff = Vector3.Dot(_playerMiddle - _playerLane.position, right);
                _playerAngle = Vector3.SignedAngle(_laneWay, forward, Vector3.up);
                DriverGame.SideLanes(traffic, _playerLane, _laneWay, out int left, out int rightLanes);
                kerb = DriverRules.KerbSide(left, rightLanes) && Mathf.Abs(_playerOff) <= DriverRules.LaneHalf;
            }
            else
            {
                _playerLane = null;
            }

            // The car itself: kept behind by a time gap, and by the room to park while it is slow in a kerb-side lane.
            float moving = _playerAlong > 0f ? _playerAlong : 0f;
            float keep = DriverRules.PlayerGap(moving, DriverRules.SensorDefault, kerb) - DriverRules.SensorDefault;
            DriverRules.Why kind = keep > DriverRules.PlayerTime * moving + 0.01f ? DriverRules.Why.PlayerRoom : DriverRules.Why.Player;
            int itself = _obstacles.Add(kind, -1, _playerMiddle.x, _playerMiddle.z, forward.x, forward.z, velocity.x, velocity.z, _playerHalfLength, _playerHalfWidth, keep);
            if (itself >= 0)
            {
                _obstacles.Slowing[itself] = _playerSlowing;
                _obstacles.KeepAtRest[itself] = DriverRules.PlayerGap(0f, DriverRules.SensorDefault, kerb) - DriverRules.SensorDefault;
                _playerKeep = keep;
            }

            // The game's own parking room behind it, while it offers a space: something the cars stop for, so they come to it gently.
            if (_game.ParkingRoom(out Vector3 room, out Vector3 roomWay, out float roomLength, out float roomWidth))
            {
                roomWay.y = 0f;
                roomWay = roomWay.sqrMagnitude > 0.01f ? roomWay.normalized : forward;
                int parking = _obstacles.Add(DriverRules.Why.Parking, -1, room.x, room.z, roomWay.x, roomWay.z, velocity.x, velocity.z, roomLength, roomWidth, 0f);
                if (parking >= 0)
                {
                    _obstacles.Slowing[parking] = _playerSlowing;
                }
            }

            WantLines(traffic, now);
        }

        // Whether the player wants a line now (pulling out of a space, or reversing in a lane), and whether one that is up is done.
        private void WantLines(TrafficManager traffic, float now)
        {
            if (_playerLane == null)
            {
                EndLine(LetOutLine, "the player's car is not beside a lane", now);
                EndLine(ReverseLine, "the player's car is not in a lane", now);
                return;
            }
            float off = Mathf.Abs(_playerOff);
            float cosine = Mathf.Cos(_playerAngle * Mathf.Deg2Rad);
            float sine = Mathf.Sin(_playerAngle * Mathf.Deg2Rad);
            float across = DriverRules.Across(_playerHalfLength, _playerHalfWidth, cosine, sine);
            float alongHalf = DriverRules.Along(_playerHalfLength, _playerHalfWidth, cosine, sine);
            float towards = _playerOff > 0f ? -_playerAngle : _playerAngle;   // degrees it points towards the lane's line
            Vector3 right = Vector3.Cross(Vector3.up, _laneWay);
            float sideSpeed = _playerBody != null ? -Mathf.Sign(_playerOff) * Vector3.Dot(_playerBody.velocity, right) : 0f;
            // Its rearmost point, on the lane's line: where a line behind it is measured from.
            Vector3 rear = _playerMiddle - _laneWay * alongHalf - right * _playerOff;

            // Letting the player out.
            Line letOut = _lines[LetOutLine];
            float sinceStill = _stoodSince > 0f ? 0f : now - _playerStillAt;
            if (DriverRules.PullingOut(off, off - across, _playerAlong, towards, sideSpeed, sinceStill))
            {
                letOut.WantedAt = now;
                if (!letOut.Pending && !letOut.Up)
                {
                    Want(traffic, letOut, rear, DriverRules.LetOutBack);
                }
            }
            if (letOut.Up)
            {
                float past = Vector3.Dot(_playerMiddle - letOut.Place, letOut.Way) - alongHalf;
                bool done = DriverRules.PulledOut(off, _playerAngle, _playerAlong, past);
                // Standing still since the line went up is the player having stopped trying; before that it is only the start.
                float still = _stoodSince > 0f ? now - Mathf.Max(_stoodSince, letOut.UpAt) : 0f;
                if (DriverRules.HoldOver(now - letOut.UpAt, letOut.Tested ? 0f : still, done, false))
                {
                    EndLine(LetOutLine, done ? "the player's car is out" : now - letOut.UpAt >= DriverRules.HoldMost ? "it has held the most it may"
                        : "the player stopped pulling out", now);
                }
            }
            else if (letOut.Pending && !letOut.Tested && now - letOut.WantedAt > WantedFor)
            {
                EndLine(LetOutLine, "the player stopped pulling out", now);
            }

            // Room to reverse: in a lane, backing at walking pace.
            Line reverse = _lines[ReverseLine];
            if (DriverRules.Reversing(_playerAlong) && off <= DriverRules.LaneHalf)
            {
                reverse.WantedAt = now;
                if (!reverse.Pending && !reverse.Up)
                {
                    Want(traffic, reverse, rear, DriverRules.ParkRoom);
                }
            }
            if (reverse.Up || reverse.Pending)
            {
                bool timeUp = reverse.Up && now - reverse.UpAt >= DriverRules.HoldMost;
                bool stopped = !reverse.Tested && now - _reversedAt > ReverseEndAfter;
                if (timeUp || stopped || off > DriverRules.KerbMost)
                {
                    EndLine(ReverseLine, off > DriverRules.KerbMost ? "the player's car has left the lane" : timeUp ? "it has held the most it may"
                        : "the player has stopped reversing", now);
                }
            }
        }

        // A line is wanted so far back along the lane from a point on its line: it goes up once no car near would be caught by it.
        private void Want(TrafficManager traffic, Line line, Vector3 from, float back)
        {
            Waypoint lane = DriverGame.LaneAt(from, _laneWay);
            if (lane == null || !DriverGame.BackAlong(traffic, lane, from, back, out Vector3 place, out Vector3 way))
            {
                return;   // the lane forks, ends or is closed just behind: nowhere sensible to hold the cars
            }
            line.Pending = true;
            line.Place = place;
            line.Way = way;
            line.WentBy = 0;
            line.Blocked = false;
        }

        // For each line still waiting: would this car be caught by it? It goes up only once no car would be.
        private void WatchLines(int c, int index, float speed, float sensor, float halfWidth)
        {
            for (int slot = 0; slot < _lines.Length; slot++)
            {
                Line line = _lines[slot];
                if (!line.Pending || line.Up)
                {
                    continue;
                }
                if (_road.Project(line.Place.x, line.Place.z, halfWidth + LineWidth * 0.5f + DriverRules.PathMargin, out float along, out float lateral, out _, out _)
                    && DriverRules.InWay(lateral, halfWidth, LineWidth * 0.5f) && DriverRules.Blocks(speed, along, sensor))
                {
                    line.Blocked = true;
                }
            }
        }

        // The lines that are up are things on the road, which the game's sensor reads (or, if they could not be made, which the
        // cars are brought to rest short of and kept at).
        private void AddLines()
        {
            for (int slot = 0; slot < _lines.Length; slot++)
            {
                Line line = _lines[slot];
                line.Obstacle = line.Up
                    ? _obstacles.Add(DriverRules.Why.HoldLine, -1, line.Place.x, line.Place.z, line.Way.x, line.Way.z, 0f, 0f, LineDepth * 0.5f, LineWidth * 0.5f, 0f)
                    : -1;
                if (line.Obstacle >= 0)
                {
                    _obstacles.Stop[line.Obstacle] = !_game.LinesUsable;
                }
            }
        }

        // The look is over: a waiting line no car of it would be caught by goes up.
        private void EndLook(float now, int cars)
        {
            for (int slot = 0; slot < _lines.Length; slot++)
            {
                Line line = _lines[slot];
                if (!line.Pending || line.Up)
                {
                    continue;
                }
                if (line.Blocked)
                {
                    line.WentBy++;
                    line.Blocked = false;
                    continue;
                }
                Raise(slot, now);
            }
        }

        private void Raise(int slot, float now)
        {
            Line line = _lines[slot];
            if (!_game.PlaceLine(slot, line.Place, line.Way, LineWidth, transform) && _game.LinesUsable)
            {
                line.Pending = false;
                return;
            }
            line.Up = true;
            line.Pending = false;
            line.UpAt = now;
            _active = true;
            if (slot == LetOutLine)
            {
                _letOutHeld = 0;
            }
            if (Meta.Dev.Verbose)
            {
                float behind = _playerDriving ? RoomBehindPlayer(line.Place, line.Way) : 0f;
                float wanted = slot == LetOutLine ? DriverRules.LetOutBack : DriverRules.ParkRoom;
                bool pass = !_playerDriving || behind >= wanted - 0.5f;
                Logger?.Info((slot == LetOutLine
                                 ? System.FormattableString.Invariant($"Traffic check (let out): the player's car is pulling out of a space; a hold line goes up {behind:F1} m behind it on the lane")
                                 : System.FormattableString.Invariant($"Traffic check (parking room): the player is reversing in the lane; a hold line goes up {behind:F1} m behind their car"))
                             + System.FormattableString.Invariant($" (wanted {wanted:F1}), after {line.WentBy} look(s) waiting for cars too close to stop gently to go by")
                             + (_game.LinesUsable ? "" : "; held by the mod's own braking") + ": " + (pass ? "PASS" : "FAIL, the lane gave no room that far back"));
            }
        }

        // A line comes down, the traffic told so the cars held for it drive on; one only wanted is forgotten.
        private void EndLine(int slot, string why, float now)
        {
            Line line = _lines[slot];
            if (line.Up)
            {
                _game.ParkLine(slot);
                float held = now - line.UpAt;
                if (Meta.Dev.Verbose)
                {
                    bool letOut = slot == LetOutLine;
                    bool pass = held <= DriverRules.HoldMost + 0.5f;
                    Logger?.Info((letOut ? "Traffic check (let out)" : "Traffic check (parking room)")
                                 + $": the hold line comes down after {held:F0} s ({why}; the most is {DriverRules.HoldMost:F0})"
                                 + (letOut ? $", {_letOutHeld} car(s) gave way for it" : "") + ": " + (pass ? "PASS" : "FAIL, it was held too long"));
                }
            }
            line.Up = false;
            line.Pending = false;
            line.Tested = false;
            line.Blocked = false;
            line.Obstacle = -1;
        }

        // Both lines down at once (the option off, the street paused, the mod closing).
        private void DropLines(string why)
        {
            float now = Time.time;
            EndLine(LetOutLine, why, now);
            EndLine(ReverseLine, why, now);
        }

        /// <summary>True if that obstacle of this look is the hold line in that slot.</summary>
        private bool LineIs(int obstacle, int slot) => obstacle >= 0 && _lines[slot].Up && _lines[slot].Obstacle == obstacle;

        // Metres from a point (a car's nose, a line) facing this way to the rearmost point of the player's car ahead of it.
        private float RoomBehindPlayer(Vector3 from, Vector3 facing)
        {
            facing.y = 0f;
            facing = facing.sqrMagnitude > 0.01f ? facing.normalized : _playerForward;
            float cosine = _playerForward.x * facing.x + _playerForward.z * facing.z;
            float sine = _playerForward.x * facing.z - _playerForward.z * facing.x;
            Vector3 to = _playerMiddle - from;
            return to.x * facing.x + to.z * facing.z - DriverRules.Along(_playerHalfLength, _playerHalfWidth, cosine, sine);
        }
    }
}
