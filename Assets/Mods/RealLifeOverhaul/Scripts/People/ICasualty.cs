using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Someone lying hurt at a scene who is not one of the scene's own cast: a person knocked
    /// down by a car. The scene sends its crews to them and says when they are taken away; who
    /// they are and how they are removed is the business of whoever put them there.
    /// </summary>
    public interface ICasualty
    {
        /// <summary>True while they are still lying on the street.</summary>
        bool Present { get; }

        /// <summary>Where they lie, or lay when last seen.</summary>
        Vector3 Position { get; }

        /// <summary>
        /// Picks them up to be carried: from now until TakeAway the body is moved as one piece by
        /// whoever asked, through its root. False if they cannot be moved (still falling, or no
        /// longer there); nothing has changed then.
        /// </summary>
        /// <param name="body">The root to move.</param>
        /// <param name="hips">The middle of the body, wherever it lies relative to the root.</param>
        /// <param name="head">With the hips, which way the body lies.</param>
        bool Lift(out Transform body, out Transform hips, out Transform head);

        /// <summary>The ambulance is leaving with them, or the scene is over: off the street, now. Safe to call twice.</summary>
        void TakeAway();
    }

    /// <summary>
    /// A casualty that can say which way it lies, so that whoever attends it can kneel beside
    /// the chest (not at the feet) and bring a stretcher up alongside. Optional: a casualty
    /// without it is treated as a point.
    /// </summary>
    public interface ILyingBody
    {
        /// <summary>Where the hips and the head are now. False if that cannot be told.</summary>
        bool Spine(out Vector3 hips, out Vector3 head);
    }
}
