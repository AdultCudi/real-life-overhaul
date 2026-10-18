using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Reads a collision of the car the player drives (the vehicle's onCollision) into CarContactRules, the one rule for
    /// whether the car hit something: what was met by its layer and owner, and each contact point's normal, rise over
    /// the body's underside and closing speed. The driving test, crashes, jobs and outings all judge through here.
    /// The body's underside is read once per car from its largest solid collider (the wheels' own colliders, which
    /// the wheel controllers put on the Ignore Raycast layer, are left out), in the car's own frame, so it moves with the
    /// body however the suspension pitches it. Cost: nothing a frame; on a collision a few sums a contact point and no
    /// allocation; a car's colliders are looked at once, on its first collision.
    /// </summary>
    internal static class CarContacts
    {
        private const int WheelLayer = 2;   // Ignore Raycast: where NWH's wheel controllers put the colliders they make

        private static readonly ContactKind[] Kinds = new ContactKind[CarContactRules.Layers];
        private static readonly List<Collider> Found = new List<Collider>(16);   // reused for the one look at a car's colliders
        private static bool _kindsRead;
        private static Transform _car;      // the car whose underside is known
        private static float _floor;        // metres: its body's underside, in its own frame

        /// <summary>The contact that speaks for a collision of <paramref name="car"/>: a hit if anything in it was one.</summary>
        public static CarContact Judge(Collision collision, Transform car)
        {
            if (collision == null || car == null)
            {
                return CarContactRules.Unpointed(ContactKind.Other);
            }
            ReadKinds();
            if (car != _car)
            {
                _floor = Floor(car);
                _car = car;
            }
            Collider other = collision.collider;
            ContactKind kind = other == null ? ContactKind.Other
                : CarContactRules.KindOf(Kinds[other.gameObject.layer & (CarContactRules.Layers - 1)], other.isTrigger, RidesIn(other, car));
            int count = collision.contactCount;
            if (count == 0)
            {
                return CarContactRules.Unpointed(kind);
            }
            Vector3 rv = collision.relativeVelocity;
            Vector3 origin = car.position;
            Vector3 up = car.up;
            CarContact best = CarContactRules.Unpointed(kind);
            for (int i = 0; i < count; i++)
            {
                ContactPoint point = collision.GetContact(i);
                Vector3 n = point.normal;
                Vector3 p = point.point;
                float rise = CarContactRules.Rise(p.x, p.y, p.z, origin.x, origin.y, origin.z, up.x, up.y, up.z, _floor);
                best = CarContactRules.Stronger(best, CarContactRules.Judge(kind, rv.x, rv.y, rv.z, n.x, n.y, n.z, rise, i));
            }
            return best;
        }

        /// <summary>What was met and how, for a log line: its name and layer, the normal, the impact and the rise.</summary>
        public static string Describe(Collision collision, in CarContact contact)
        {
            Collider other = collision != null ? collision.collider : null;
            string name = other != null ? other.gameObject.name : "nothing";
            string layer = other != null ? LayerMask.LayerToName(other.gameObject.layer) : string.Empty;
            return name + " (layer " + (string.IsNullOrEmpty(layer) ? "unnamed" : layer) + ", " + CarContactRules.Name(contact.Kind) + "), normal ("
                   + F(contact.NormalX) + ", " + F(contact.NormalY) + ", " + F(contact.NormalZ) + "), " + F(contact.Impact) + " m/s along it, "
                   + F(contact.Rise) + " m over the body's underside";
        }

        private static string F(float value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static void ReadKinds()
        {
            if (_kindsRead)
            {
                return;
            }
            _kindsRead = true;
            for (int i = 0; i < Kinds.Length; i++)
            {
                Kinds[i] = CarContactRules.KindOfLayer(LayerMask.LayerToName(i));
            }
        }

        // Something of the car's own, or riding in it with a body of its own (a load).
        private static bool RidesIn(Collider other, Transform car)
        {
            Rigidbody body = other.attachedRigidbody;
            return other.transform.IsChildOf(car) || (body != null && body.transform == car);
        }

        // The body's underside in the car's frame: the lowest corner of its largest solid collider's box.
        private static float Floor(Transform car)
        {
            Found.Clear();
            car.GetComponentsInChildren(false, Found);
            Collider body = null;
            float most = 0f;
            for (int i = 0; i < Found.Count; i++)
            {
                Collider c = Found[i];
                if (c == null || c.isTrigger || c.gameObject.layer == WheelLayer)
                {
                    continue;
                }
                Vector3 size = c.bounds.size;
                float volume = size.x * size.y * size.z;
                if (volume > most)
                {
                    most = volume;
                    body = c;
                }
            }
            Found.Clear();
            if (body == null)
            {
                return 0f;   // the car's origin, which the game's cars keep at the road
            }
            Bounds box;
            Transform frame;
            MeshCollider mesh = body as MeshCollider;
            if (mesh != null && mesh.sharedMesh != null)
            {
                box = mesh.sharedMesh.bounds;   // the mesh's own box, exact whatever the car's tilt now
                frame = mesh.transform;
            }
            else
            {
                box = body.bounds;              // the world's box: exact for a car standing level
                frame = null;
            }
            float lowest = float.MaxValue;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 local = new Vector3((corner & 1) == 0 ? box.min.x : box.max.x, (corner & 2) == 0 ? box.min.y : box.max.y, (corner & 4) == 0 ? box.min.z : box.max.z);
                Vector3 world = frame != null ? frame.TransformPoint(local) : local;
                float y = car.InverseTransformPoint(world).y;
                lowest = y < lowest ? y : lowest;
            }
            return lowest;
        }
    }
}
