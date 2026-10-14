using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Air drag on the car the player is driving: a force against its motion that grows with the square of
    /// its speed. The game's cars have none (a speed limiter stops them instead), and it is what lets a
    /// car's top speed be where its power runs out. Switched on by <see cref="CarTuning"/> only while a
    /// tuned car is being driven; one force per physics step, nothing else.
    /// </summary>
    public sealed class CarDrag : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("driving", "CarDrag", typeof(CarDrag));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private Rigidbody _body;
        private float _coefficient;   // newtons per (m/s) squared

        public void Begin(Rigidbody body, float coefficient)
        {
            _body = body;
            _coefficient = coefficient;
            enabled = body != null && coefficient > 0f;
        }

        public void End()
        {
            _body = null;
            enabled = false;
        }

        private static readonly int HotFixedUpdate = HotSpots.Slot("Driving: CarDrag.FixedUpdate");
        private void FixedUpdate()
        {
            using var hotSpot = HotSpots.Time(HotFixedUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnFixedUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnFixedUpdate()
        {
            if (_body == null)
            {
                enabled = false;   // the car was taken away under us
                return;
            }
            Vector3 velocity = _body.velocity;
            float speed = velocity.magnitude;
            if (speed > 1f)
            {
                _body.AddForce(velocity * (-_coefficient * speed));
            }
        }
    }
}
