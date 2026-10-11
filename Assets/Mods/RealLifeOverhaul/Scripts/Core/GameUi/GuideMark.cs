using System;
using BAModAPI;
using UI.Guiders;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// A mark on a place that is no building, shown the way the game marks a job's destination: its own direction
    /// guider (UI.Guiders.DirectionGuider, the one of type JobDestination), which draws the marker over the place,
    /// the arrow at the edge of the screen while the place is out of view, the distance and the name, and puts the
    /// place on the city map. Nothing is drawn by the mod.
    ///
    /// The game has one guider of that type and the mod's jobs and outings use it too. A mark therefore takes it
    /// only while it is free (or already this mark's), and takes off only what it put there: whoever was first
    /// keeps the guider.
    ///
    /// The cost: nothing per frame of the mod's. <see cref="Show"/> does work only when the place or the name has
    /// changed; the game moves its own marker.
    /// </summary>
    public sealed class GuideMark
    {
        public static IModLogger Logger;

        private Transform _target;     // the guider's own target object while the mark is up: how the mark knows the guider is still its
        private Vector3 _at;
        private string _name;

        /// <summary>True while this mark is the one on the game's guider.</summary>
        public bool Showing
        {
            get
            {
                if (ReferenceEquals(_target, null))
                {
                    return false;
                }
                DirectionGuider guider = Guider();
                return guider != null && _target != null && guider.target == _target && (_target.position - _at).sqrMagnitude < 0.01f;
            }
        }

        /// <summary>
        /// Puts the mark on a place with a name (final words). False if the game's guider is not there or is marking
        /// something else; it can be asked again later. Asking again with the same place and name costs two compares.
        /// </summary>
        public bool Show(Vector3 at, string name)
        {
            try
            {
                DirectionGuider guider = Guider();
                if (guider == null)
                {
                    return false;
                }
                bool mine = Showing;
                if (mine && ReferenceEquals(name, _name) && (at - _at).sqrMagnitude < 0.01f)
                {
                    return true;
                }
                if (!mine && guider.target != null)
                {
                    return false;   // a job or an outing has it
                }
                GuidersManager manager = InstanceBehavior<GuidersManager>.Instance;
                DirectionGuider look = manager.destinationGuider != null ? manager.destinationGuider : guider;
                GuidersManager.SetGuiderTarget(at, name ?? string.Empty, look.poiICon, GuidersManager.GetGuiderColor(DirectionGuiderType.JobDestination), DirectionGuiderType.JobDestination);
                _target = guider.target;
                _at = at;
                _name = name;
                return !ReferenceEquals(_target, null);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _target = null;
                return false;
            }
        }

        /// <summary>Takes the mark off, if it is still this one. Safe at any time, and when the interface has gone.</summary>
        public void Hide()
        {
            if (ReferenceEquals(_target, null))
            {
                return;
            }
            try
            {
                if (Showing)
                {
                    GuidersManager.ResetGuider(DirectionGuiderType.JobDestination);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _target = null;
            _name = null;
        }

        private static DirectionGuider Guider()
        {
            GuidersManager manager = InstanceBehavior<GuidersManager>.Instance;
            return manager != null ? manager.jobDestinationGuider : null;
        }
    }
}
