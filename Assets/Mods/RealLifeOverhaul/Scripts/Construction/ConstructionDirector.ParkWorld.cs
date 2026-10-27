using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Car parks in the world (CarParkGame): the director stands one when it opens or a load finds it, out of view (no ground of it in the
    /// camera's picture and the player not on it, else at the next black screen), and takes it away when it closes or the option goes
    /// off; the bays' cars follow the hour's share (ParkFill). Each is checked when it stands and the line is in Player.log. In a Going
    /// Public session only the player's own stand, without the game's parked cars (the host's would be sent to the others). COST:
    /// once a second a look at the few car parks and, for one not standing, the camera's planes against its box; a stand is a few
    /// meshes and lanes, once.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly Dictionary<int, float> _parkRetry = new Dictionary<int, float>(4);
        private readonly List<int> _parkGone = new List<int>(4);
        private bool _parkForce;   // a test stands a car park whether or not it is in view

        /// <summary>The world of a car park standing now, or null.</summary>
        internal CarParkWorld ParkWorldOf(int id) => _parkWorlds.TryGetValue(id, out CarParkWorld w) ? w : null;

        private void ParkWorldSecond()
        {
            if (_state == null || !_loaded)
            {
                return;
            }
            bool on = ParkingOn && DistrictLives() && CarParkGame.Ready;
            float now = Time.unscaledTime;
            for (int i = 0; i < _state.CarParks.Count; i++)
            {
                CarParkFile c = _state.CarParks[i];
                bool want = on && c.Standing && (c.Owner == CarParkOwner.Player || !GoingPublic.InSession);
                bool has = _parkWorlds.ContainsKey(c.Id);
                if (!want)
                {
                    if (has)
                    {
                        ParkWorldGone(c.Id);
                    }
                    continue;
                }
                if (has || (_parkRetry.TryGetValue(c.Id, out float again) && now < again))
                {
                    continue;
                }
                ParkStand(c, now);
            }
            _parkGone.Clear();
            foreach (KeyValuePair<int, CarParkWorld> pair in _parkWorlds)
            {
                CarParkFile c = _state.CarPark(pair.Key);
                if (c == null || !c.Standing)
                {
                    _parkGone.Add(pair.Key);
                }
            }
            for (int i = 0; i < _parkGone.Count; i++)
            {
                ParkWorldGone(_parkGone[i]);
            }
        }

        // One car park stood if nobody sees its ground (or the screen is black), else asked again next second.
        private void ParkStand(CarParkFile c, float now)
        {
            if (!ParkGround(c, out LotView site))
            {
                return;   // its ground is not in the district at this load
            }
            if (site.Width != c.Width || site.Depth != c.Depth)
            {
                _parkRetry[c.Id] = now + 600f;
                Logger?.Warn("Construction: car park " + c.Id + " was laid out on " + c.Width + " by " + c.Depth + " m but its ground is " + site.Width + " by " + site.Depth + " m now; not stood.");
                return;
            }
            CarParkLayout layout = CarParkRules.LayoutOf(c.Width, c.Depth, c.Mouth);
            if (!layout.Ok)
            {
                _parkRetry[c.Id] = now + 600f;
                Logger?.Warn("Construction: car park " + c.Id + " has no plan at " + c.Width + " by " + c.Depth + " m (" + layout.Why + "); not stood.");
                return;
            }
            if (!_parkForce && !SceneFinds.Black && (CarParkGame.InView(site, layout) || CarParkGame.PlayerOn(site)))
            {
                return;   // waits: asked again each second
            }
            string neighbourhood = site.DistrictKey ?? DistrictKeyOf(site.District);
            CarParkWorld world = CarParkGame.Stand(c.Id, site, layout, neighbourhood, !GoingPublic.InSession, 100 - CarParkRules.Occupied(c.Cars, c.Bays), out string why);
            if (world == null)
            {
                _parkRetry[c.Id] = now + 60f;
                Logger?.Warn("Construction: car park " + c.Id + " could not be stood (" + why + "); tried again in a minute.");
                return;
            }
            _parkWorlds[c.Id] = world;
            Action<float, float, float, float> given = ConstructionBridges.GroundGiven;
            if (given != null)
            {
                try
                {
                    CarParkGame.GroundBox(world, out float x1, out float z1, out float x2, out float z2);
                    given(x1, z1, x2, z2);   // the works' hoarding and base come down in the same moment
                }
                catch (Exception e)
                {
                    Faulty("GroundGiven", e);
                }
            }
            if (Verbose)
            {
                Logger?.Info("Parking check (car park " + c.Id + " at " + LotWords(c.Lot) + "): " + CarParkGame.Describe(world) + ": " + (world.Ok ? "PASS" : "FAIL (" + world.Why + ")") + ".");
            }
        }

        /// <summary>A car park stood now, in view or not (for the tests); its world, or null with the reason in Player.log.</summary>
        internal CarParkWorld ParkStandNow(CarParkFile c)
        {
            if (c == null)
            {
                return null;
            }
            if (_parkWorlds.TryGetValue(c.Id, out CarParkWorld have))
            {
                return have;
            }
            _parkForce = true;
            try
            {
                _parkRetry.Remove(c.Id);
                ParkStand(c, Time.unscaledTime);
            }
            finally
            {
                _parkForce = false;
            }
            return ParkWorldOf(c.Id);
        }

        private void ParkWorldGone(int id)
        {
            if (_parkWorlds.TryGetValue(id, out CarParkWorld world))
            {
                _parkWorlds.Remove(id);
                CarParkGame.Remove(world);
            }
            _parkRetry.Remove(id);
        }

        // Every car park taken away: the option off, the construction game stood down, the city unloading.
        private void ParkWorldsDown()
        {
            _parkGone.Clear();
            foreach (KeyValuePair<int, CarParkWorld> pair in _parkWorlds)
            {
                _parkGone.Add(pair.Key);
            }
            for (int i = 0; i < _parkGone.Count; i++)
            {
                ParkWorldGone(_parkGone[i]);
            }
            _parkRound.Clear();
            _parkRetry.Clear();
            CarParkCta.Remove();
        }

        // ------------------------------------------------------------------ the pay machine

        /// <summary>The pay machine's dialogue for a car park (from the machine, a notice or the build mode's panel). False when it cannot open.</summary>
        public bool ParkDesk(int id)
        {
            CarParkFile c = _state != null ? _state.CarPark(id) : null;
            return c != null && c.Standing && Running && CarParkDesk.Open(this, id);
        }

        /// <summary>The pay machine was clicked: the player walks up to it, then the dialogue opens.</summary>
        internal void ParkClicked(CarParkPointer pointer)
        {
            if (pointer == null || !Running)
            {
                return;
            }
            int id = pointer.CarPark;
            CarParkCta.WalkUp(pointer, () => ParkDesk(id));
        }
    }
}
