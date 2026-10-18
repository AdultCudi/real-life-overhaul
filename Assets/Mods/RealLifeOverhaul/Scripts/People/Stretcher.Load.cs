using System;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>A casualty lifted onto the stretcher and off it again.</summary>
    public sealed partial class Stretcher : MonoBehaviour
    {
        /// <summary>
        /// The crew at its ends lift whoever was just loaded, in the lifting clip; returns its length, or 0 if nobody can play it
        /// (the body then rises as the stretcher is taken hold of).
        /// </summary>
        public float Lift(ActorRole first, ActorRole second)
        {
            if (_failed || !Out || _mode != Mode.Parked || _patient == null || !_supine || _lift >= 1f
                || !ClipLibrary.TryGet(SceneClips.LiftPatient, out CityClip clip))
            {
                return 0f;
            }
            LetGo();
            _lead = first;
            _rear = second != first ? second : null;
            bool lifted = Lifts(_lead);
            lifted |= Lifts(_rear);
            if (!lifted)
            {
                LetGo();
                return 0f;
            }
            _liftTime = Mathf.Max(LiftTime, clip.Length);
            _lifting = true;
            enabled = true;
            return _liftTime;
        }

        private bool Lifts(ActorRole who)
        {
            if (who == null || !AtAnEnd(who))
            {
                return false;
            }
            Stow(who);
            return who.Act(SceneClips.LiftPatient);
        }

        public float FromAnEnd(Vector3 at)
        {
            return Mathf.Sqrt(Mathf.Min(Flat(at - FootEnd).sqrMagnitude, Flat(at - HeadEnd).sqrMagnitude));
        }

        private bool AtAnEnd(ActorRole who)
        {
            return who.Present && FromAnEnd(who.Position) <= TakeWithin;
        }

        private void FinishLift()
        {
            if (_patient == null || !_supine || _lift >= 1f)
            {
                return;
            }
            _lift = 1f;
            _pose.Set(transform, 1f);
            if (_blanket != null)
            {
                _blanket.SetActive(true);
            }
        }

        private bool AdvanceLift()
        {
            if (_patient == null || !_supine || _lift >= 1f)
            {
                return false;
            }
            float lift = Mathf.Min(1f, _lift + Time.deltaTime / _liftTime);
            if (lift >= 1f)
            {
                FinishLift();
                return false;
            }
            _lift = lift;
            _pose.Set(transform, lift);
            return true;
        }

        /// <summary>
        /// Lays a casualty on their back along the mattress, lifted over a moment and covered to the chest; a skeleton that
        /// cannot be posed is carried as it lies. False, with nothing changed, if they cannot be picked up.
        /// </summary>
        public bool Load(ICasualty patient)
        {
            if (_failed || patient == null || !Out || !patient.Lift(out Transform body, out Transform hips, out Transform head))
            {
                return false;
            }
            try
            {
                Transform bed = transform;
                _pose.Clear();
                Animator animator = body.GetComponentInChildren<Animator>(true);
                var character = body.GetComponent<ThirdPersonCharacter>();
                bool animated = character == null || !RagdollSystem.Holds(character);
                _supine = _pose.Begin(body, animator, bed, new Vector3(0f, BedHeight + HipsAbove, HipsAlong), animated);
                if (_supine)
                {
                    _lift = 0f;
                }
                else
                {
                    Vector3 spine = head.position - hips.position;
                    spine.y = 0f;
                    if (spine.sqrMagnitude > 0.01f)
                    {
                        float turn = Vector3.SignedAngle(spine, bed.forward, Vector3.up);
                        body.rotation = Quaternion.AngleAxis(turn, Vector3.up) * body.rotation;
                    }
                    Vector3 middle = bed.position + Vector3.up * (BedHeight + 0.12f) - bed.forward * 0.1f;
                    body.position += middle - hips.position;
                    _lift = 1f;
                }

                _patient = patient;
                _patientBody = body;
                _patientPosition = bed.InverseTransformPoint(body.position);
                _patientRotation = Quaternion.Inverse(bed.rotation) * body.rotation;
                return true;   // the lift is played by the crew (Lift), as they take hold (Step), or ended at once (Park)
            }
            catch (Exception e)
            {
                Fail(e);
                return false;
            }
        }

        /// <summary>Lets go of whoever is on the bed before their owner takes them off the street; the stretcher stays out.</summary>
        public void Unload()
        {
            _pose.Clear();
            _supine = false;
            _lift = 1f;
            _liftTime = LiftTime;
            _lifting = false;
            _patient = null;
            _patientBody = null;
            if (_blanket != null)
            {
                _blanket.SetActive(false);
            }
        }

        public void Cover()
        {
            if (_sheet != null && Out)
            {
                _sheet.SetActive(true);
            }
        }
    }
}
