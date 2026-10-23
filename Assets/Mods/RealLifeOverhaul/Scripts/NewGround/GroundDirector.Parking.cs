using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.NewGround
{
    /// <summary>
    /// "Parking: copy a lane": the nearest of the game's parking lanes copied along the street, its cars counted once it
    /// has parked them, and taken away with its cars after a minute of play. Never in a Going Public session, which sends
    /// the host's parked cars to the others.
    /// </summary>
    public sealed partial class GroundDirector
    {
        private const float LaneReach = 40f;     // m round the player the lane to copy is looked for
        private const float LaneShift = 30f;     // m along the street the copy stands, at least
        private const float LaneLook = 1.5f;     // s of play before the copy's cars are counted: it parks them as it wakes

        private ParkingLaneGenerator _lane;
        private float _laneLeft;
        private float _laneCount;
        private float _laneMetres;

        private void CopyLane()
        {
            if (GoingPublic.InSession)
            {
                TestRefusals.TellKey(Logger, "Parking: copy a lane", NewGroundText.Session);
                return;
            }
            EndLane();
            Vector3 at = WalkGame.At;
            ParkingLaneGenerator template = ParkingGame.Nearest(at, LaneReach);
            if (template == null)
            {
                TestRefusals.TellKey(Logger, "Parking: copy a lane", NewGroundText.NoLane, "no straight lane of the game's within " + LaneReach.ToString("0", Inv) + " m");
                return;
            }
            Vector3 way = ParkingGame.Along(template, out float length);
            float metres = Mathf.Max(LaneShift, length + 2f);
            Vector3 shift = way * metres;
            if (!ParkingGame.Room(template, shift, out string why))
            {
                shift = -shift;
                if (!ParkingGame.Room(template, shift, out string other))
                {
                    TestRefusals.TellKey(Logger, "Parking: copy a lane", NewGroundText.NoLane, "ahead: " + why + "; behind: " + other);
                    return;
                }
            }
            _lane = ParkingGame.Copy(template, shift, out string refused);
            if (_lane == null)
            {
                TestRefusals.TellKey(Logger, "Parking: copy a lane", NewGroundText.NoLane, refused);
                return;
            }
            _laneLeft = GroundRules.KeepSeconds;
            _laneCount = LaneLook;
            _laneMetres = metres;
            Logger?.Info("New ground: the parking lane " + template.name + " (" + length.ToString("0", Inv) + " m) copied " + metres.ToString("0", Inv) + " m along the street.");
            Wake();
        }

        private bool TickLane(float played)
        {
            if (_lane == null)
            {
                return false;
            }
            if (_laneCount > 0f)
            {
                _laneCount -= played;
                if (_laneCount <= 0f)
                {
                    int parked = ParkingGame.Parked(_lane), spots = ParkingGame.Spots(_lane);
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Parking check (copy): " + parked + " cars parked, " + spots + " spots for the player's car: "
                                     + (parked + spots > 0 ? "PASS" : "FAIL (the copy parked nothing and made no spot)"));
                    }
                    Notice(NewGroundText.Parking, new Dictionary<string, string>(3)
                    {
                        { "metres", Metres(_laneMetres) },
                        { "cars", parked.ToString(Inv) },
                        { "spots", spots.ToString(Inv) }
                    });
                }
            }
            _laneLeft -= played;
            if (_laneLeft <= 0f)
            {
                EndLane();
                Notice(NewGroundText.Back, null);
            }
            return _lane != null;
        }

        private void EndLane()
        {
            ParkingGame.Remove(ref _lane);
        }
    }
}
