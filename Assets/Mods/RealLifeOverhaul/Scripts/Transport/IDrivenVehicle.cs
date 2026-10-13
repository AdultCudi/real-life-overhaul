using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A vehicle of the mod's own that <see cref="BusDriver"/> can drive: the city bus (<see cref="BusVehicle"/>) and the
    /// tow truck (<see cref="TowTruck"/>). What the driver needs of it: where it is, whether it is out, its body (what it
    /// leaves out of its look ahead), its measurements from its own middle, and the pose it is given every physics step.
    /// Nothing here runs by itself.
    /// </summary>
    public interface IDrivenVehicle
    {
        /// <summary>Its transform: its middle on the road, facing the way it drives.</summary>
        Transform Frame { get; }

        /// <summary>True while it is on the street.</summary>
        bool Out { get; }

        /// <summary>Its kinematic body.</summary>
        Rigidbody Body { get; }

        /// <summary>Metres from its middle forward to its front axle (the point its routes are laid for).</summary>
        float FrontAxleAhead { get; }

        /// <summary>Metres from its middle forward to its rear axle (negative: behind the middle).</summary>
        float RearAxleAhead { get; }

        /// <summary>Metres between its axles.</summary>
        float AxleSpan { get; }

        /// <summary>Metres from its front axle forward to its nose.</summary>
        float NoseBeyondAxle { get; }

        /// <summary>Half its width, metres.</summary>
        float HalfBreadth { get; }

        /// <summary>
        /// Where it stands now, as the driver has moved it: its axles' middles on the road, how far it has rolled since it
        /// was last posed, the front wheels' angle to the body (degrees, to the right positive), and what it is doing with
        /// its speed (m/s2) and its heading (degrees a second). Called from the physics step.
        /// </summary>
        void Pose(Vector3 frontAxle, Vector3 rearAxle, float rolled, float steer, float acceleration, float turning, float speed, float dt);
    }
}
