using UnityEngine;
using Vehicles.Components;
using NwhVehicle = NWH.VehiclePhysics2.VehicleController;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The one place the turn signals meet the game: the car's own signal object (the one its keys work), the two shader
    /// values the game keeps the signals' state in, and the car's physics. Each is read in a small method of its own.
    /// Nothing here runs unless a car is followed.
    /// </summary>
    public static class TurnSignalGame
    {
        private static readonly int LeftId = Shader.PropertyToID("_IsLeftBlinkerOn");
        private static readonly int RightId = Shader.PropertyToID("_IsRightBlinkerOn");

        /// <summary>The car's physics, or null.</summary>
        public static NwhVehicle Physics(CarController car)
        {
            return car != null ? car.vehicleController : null;
        }

        /// <summary>The car's own signal object, which its signal keys toggle, or null. Searches the car: once a car, never in a frame.</summary>
        public static MonoBehaviour SignalsOf(CarController car)
        {
            return car != null ? car.GetComponentInChildren<VehicleBlinker>(true) : null;
        }

        /// <summary>True when the car's body has a mesh to read the signals' state from.</summary>
        public static bool CanRead(CarController car)
        {
            CarFeatures features = car != null ? car.CarFeatures : null;
            Renderer[] meshes = features != null ? features.bodyMeshes : null;
            return meshes != null && meshes.Length > 0 && meshes[0] != null;
        }

        /// <summary>
        /// Which signals show now, as the game keeps them (the same values its own toggle writes on the body's meshes), whoever
        /// switched them on. <paramref name="block"/> is the caller's, kept for the purpose. Both false if they cannot be read.
        /// </summary>
        public static void Showing(CarController car, MaterialPropertyBlock block, out bool left, out bool right)
        {
            left = false;
            right = false;
            if (!CanRead(car))
            {
                return;
            }
            car.CarFeatures.bodyMeshes[0].GetPropertyBlock(block);
            left = block.GetInt(LeftId) == 1;
            right = block.GetInt(RightId) == 1;
        }

        /// <summary>The game's own toggle of one side's signal, as its key makes it: switching one side on puts the other off.</summary>
        public static void Toggle(MonoBehaviour signals, bool left)
        {
            var own = (VehicleBlinker)signals;
            if (left)
            {
                own.ToggleLeftBlinker();
            }
            else
            {
                own.ToggleRightBlinker();
            }
        }

        /// <summary>The car's steering asked for, -1 full left to 1 full right.</summary>
        public static float Steer(NwhVehicle physics)
        {
            return physics.input.Steering;
        }

        /// <summary>The car's speed along its nose in m/s, negative when it is backing.</summary>
        public static float Speed(NwhVehicle physics)
        {
            return physics.SpeedSigned;
        }
    }
}
