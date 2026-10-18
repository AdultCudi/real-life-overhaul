using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Someone lying hurt at a scene who is not one of its own cast, such as a person knocked down by a car. Whoever put
    /// them there decides who they are and how they are removed.
    /// </summary>
    public interface ICasualty
    {
        /// <summary>True while they are still lying on the street.</summary>
        bool Present { get; }

        /// <summary>Where they lie, or lay when last seen.</summary>
        Vector3 Position { get; }

        /// <summary>
        /// Picks them up: until <see cref="TakeAway"/> the body is moved as one piece through its root,
        /// <paramref name="body"/>; <paramref name="hips"/> and <paramref name="head"/> say how it lies. False, with
        /// nothing changed, while they are still falling or once they are gone.
        /// </summary>
        bool Lift(out Transform body, out Transform hips, out Transform head);

        /// <summary>The ambulance is leaving with them, or the scene is over: off the street, now. Safe to call twice.</summary>
        void TakeAway();
    }

    /// <summary>
    /// A casualty that can say which way it lies, so a carer kneels at the chest and the stretcher comes alongside.
    /// Without it a casualty is treated as a point.
    /// </summary>
    public interface ILyingBody
    {
        /// <summary>Where the hips and the head are now; false if that cannot be told.</summary>
        bool Spine(out Vector3 hips, out Vector3 head);
    }
}
