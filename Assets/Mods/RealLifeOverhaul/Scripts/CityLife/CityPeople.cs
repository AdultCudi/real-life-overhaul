using System;
using System.Collections.Generic;
using BigAmbitions.Characters;
using BigAmbitions.Characters.Appearance;
using Character;
using Characters;
using Helpers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The living city's own pool of people, apart from the scenes' (People/ActorPool): eight at most
    /// (<see cref="MaxPeople"/>), what three of the largest sites need. A look is the costly part (a mesh merge), so they
    /// are made one at a time after the city loads (<see cref="Warm"/>) and handed out again in the same clothes where they
    /// can be. Per frame, only while somebody is out: a few comparisons a person to hide those far off or behind the
    /// camera, and every other physics step the player's car against them, as Ragdoll/CarStrikes does. Idle, disabled.
    /// </summary>
    public sealed partial class CityPeople : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("citylife", "CityPeople", typeof(CityPeople));

        public const int MaxPeople = 8;

        private const string CharacterPrefab = "Characters/HumanDefinitionLow";
        private const float HideDistance = 150f;   // metres from the camera
        private const float HideBehind = 6f;       // metres behind the camera's own plane
        private const float StrikeSpeed = 1.4f;    // metres a second: slower than this a car only nudges
        private const float PersonRadius = 0.3f;   // metres
        private const float CarHalfWidth = 0.95f;   // metres
        private const float CarHalfLength = 2.3f;   // metres
        private const float MaxBuild = 0.55f;      // of the game's 0 (slim) to 1 (heavy): the clips are made for slim to average

        private static readonly AppearanceTag[] CasualWear = { AppearanceTag.Casual };

        private readonly List<CityPerson> _people = new List<CityPerson>(MaxPeople);
        private readonly List<CityLook> _wanted = new List<CityLook>(4);   // looks nobody on the shelf had, made ready one at a time
        private Coroutine _tending;

        /// <summary>Never builds a look in the frame somebody is asked for: hands out somebody dressed for it, or says no and makes it ready.</summary>
        public bool Thrifty;

        /// <summary>For a test button, which cannot wait: a thrifty pool builds a missing look on the spot.</summary>
        public bool AtNeed;

        /// <summary>A thrifty pool said no because a look was not ready yet; cleared by whoever reads it.</summary>
        public bool Refused;
        private readonly List<Transform> _fallen = new List<Transform>(4);   // tools dropped by people who were hit, until their site clears them
        private CityProps _props;
        private bool _hidingFailed;
        private bool _strikesFailed;
        private bool _skip;
        private float _nextLookup;
        private Rigidbody _car;
        private CarController _carOf;   // the car _car was read from

        public void Setup(CityProps props)
        {
            _props = props;
            enabled = false;
        }

        public int Count => _people.Count;

        /// <summary>One of the characters, in use or not, for finding this pool's people where they stand (People/CordonClear).</summary>
        internal CityPerson At(int index) => _people[index];

        /// <summary>How many characters could be put on the street now.</summary>
        public int Free
        {
            get
            {
                int free = 0;
                for (int i = 0; i < _people.Count; i++)
                {
                    if (!_people[i].InUse && _people[i].Valid && !_people[i].Actor.InUse)
                    {
                        free++;
                    }
                }
                return free;
            }
        }

        /// <summary>Tools dropped by people who were knocked down; whoever ends a site takes what lies near it.</summary>
        public List<Transform> Fallen => _fallen;

        /// <summary>Makes one more character, dressed as a civilian and put away; true while there is more to make.</summary>
        public bool Warm(Vector3 near)
        {
            if (_people.Count >= MaxPeople)
            {
                return false;
            }
            using var piece = SessionWatch.Piece("people: a character being made");
            var character = PrefabHelper.CreatePrefab<ThirdPersonCharacter>(CharacterPrefab, near, Quaternion.identity, transform);
            character.name = "RealLifeOverhaul Citizen";
            // Idle fidgets would play over the poses activities set.
            var fidgets = character.GetComponentInChildren<BoredAnimations>(true);
            if (fidgets != null)
            {
                fidgets.pauseBoredAnimations = true;
            }
            // They go where their route takes them: physics must not shove them about.
            if (character.characterRigidbody != null)
            {
                character.characterRigidbody.isKinematic = true;
            }
            var person = new CityPerson(character);
            _people.Add(person);
            Dress(person, CityLook.Civilian);
            character.gameObject.SetActive(false);
            return _people.Count < MaxPeople;
        }

        /// <summary>Puts a person standing at a spot, facing a point, in the given clothes; null if nobody is free or fits there.</summary>
        public CityPerson Take(CityLook look, Vector3 spot, Vector3 face)
        {
            return Take(look, spot, face, true);
        }

        /// <summary>The same; with fresh false a civilian who has been out keeps their look, for people who come and go every few seconds.</summary>
        public CityPerson Take(CityLook look, Vector3 spot, Vector3 face, bool fresh)
        {
            bool thrifty = Thrifty && !AtNeed && !Looks.Hidden;
            CityPerson person = thrifty ? Ready(look) : Choose(look);
            if (person == null)
            {
                if (thrifty && Free > 0)
                {
                    Refused = true;
                    Want(look);   // made ready on the shelf for the next time it is asked for
                }
                return null;
            }
            try
            {
                person.Character.gameObject.SetActive(true);
                person.Reset(_props);   // whoever had them last, the casualty system perhaps, left them as they were
                if (!thrifty && (person.Look != look || (person.Stale && fresh)))
                {
                    Dress(person, look);
                }
                person.InUse = true;
                person.Actor.InUse = true;
                if (!person.Place(spot, face))
                {
                    Release(person);
                    return null;
                }
                enabled = true;
                return person;
            }
            catch (Exception e)
            {
                CityLifeDirector.Logger?.Error(e);
                Release(person);
                return null;
            }
        }

        public void Release(CityPerson person)
        {
            if (person == null || !person.InUse)
            {
                return;
            }
            bool taken = person.Taken;
            person.InUse = false;
            if (taken && person.Actor.InUse)
            {
                person.Stale = true;
                return;   // knocked down: the casualty system puts them away itself when it is done
            }
            person.Actor.InUse = false;
            person.ReleasedAt = Time.unscaledTime;
            person.Uses++;
            // A thrifty pool keeps a civilian's look for a few outings; the plain one changes it before they are seen again.
            person.Stale = person.Look == CityLook.Civilian && (!Thrifty || Looks.Worn(person.Uses, person.DressedAt));
            if (Thrifty && person.Stale)
            {
                Tend();
            }
            try
            {
                if (person.Valid)
                {
                    person.Reset(_props);
                    person.Character.gameObject.SetActive(false);
                }
            }
            catch (Exception e)
            {
                CityLifeDirector.Logger?.Error(e);
            }
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < _people.Count; i++)
            {
                Release(_people[i]);
            }
            for (int i = 0; i < _fallen.Count; i++)
            {
                _props.Return(_fallen[i]);
            }
            _fallen.Clear();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _people.Count; i++)
            {
                CityPerson person = _people[i];
                try
                {
                    if (person.Valid)
                    {
                        ActivityAnimators.Forget(person.Character.animator);
                        CityOutfits.Restore(person.Dyes);
                        if (!RagdollSystem.Holds(person.Character))
                        {
                            Destroy(person.Character.gameObject);
                        }
                    }
                }
                catch (Exception e)
                {
                    CityLifeDirector.Logger?.Error(e);
                }
                person.InUse = false;
            }
            _people.Clear();
        }

        private static readonly int HotUpdate = HotSpots.Slot("CityLife: CityPeople.Update");
        // Only while somebody is out: hides whoever cannot be seen, and shows them the frame they can.
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            try
            {
                Camera camera = GameManager.GetMainCamera();
                if (camera == null)
                {
                    return;
                }
                Transform eye = camera.transform;
                Vector3 from = eye.position;
                Vector3 forward = eye.forward;
                bool any = false;
                for (int i = 0; i < _people.Count; i++)
                {
                    CityPerson person = _people[i];
                    if (!person.InUse || person.Lost)
                    {
                        continue;
                    }
                    any = true;
                    if (!_hidingFailed)
                    {
                        Vector3 offset = person.Position - from;
                        person.SetVisible(offset.sqrMagnitude < HideDistance * HideDistance && Vector3.Dot(offset, forward) > -HideBehind);
                    }
                }
                if (!any)
                {
                    enabled = false;
                }
            }
            catch (Exception e)
            {
                // Hiding is only a saving: given up rather than fault again next frame.
                CityLifeDirector.Logger?.Error(e);
                _hidingFailed = true;
                for (int i = 0; i < _people.Count; i++)
                {
                    if (_people[i].InUse && !_people[i].Lost)
                    {
                        _people[i].SetVisible(true);
                    }
                }
            }
        }

        private static readonly int HotFixedUpdate = HotSpots.Slot("CityLife: CityPeople.FixedUpdate");
        private void FixedUpdate()
        {
            using var hotSpot = HotSpots.Time(HotFixedUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnFixedUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnFixedUpdate()
        {
            _skip = !_skip;
            if (_skip || _strikesFailed || !Settings.Ragdolls)
            {
                return;
            }
            try
            {
                Strikes();
            }
            catch (Exception e)
            {
                CityLifeDirector.Logger?.Error(e);
                CityLifeDirector.Logger?.Warn("City life: its people can no longer be knocked down by the player's car (switched off after the error above).");
                _strikesFailed = true;
            }
        }

        // The player's car against the people who are out: by positions, not collisions (as Ragdoll/CarStrikes).
        private void Strikes()
        {
            float now = Time.time;
            if (now >= _nextLookup)
            {
                _nextLookup = now + 0.5f;
                var game = InstanceBehavior<GameManager>.Instance;
                CarController car = game != null && PlayerHelper.IsUsingVehicle ? game.selectedVehicle as CarController : null;
                if (!ReferenceEquals(car, _carOf))   // looked up only when the car changes
                {
                    _carOf = car;
                    _car = car != null ? car.GetComponent<Rigidbody>() : null;
                }
            }
            PedestrianIncidents incidents = PedestrianIncidents.Instance;
            RagdollSystem ragdolls = RagdollSystem.Instance;
            if (_car == null || incidents == null || ragdolls == null)
            {
                return;
            }
            Vector3 velocity = _car.velocity;
            float sqrSpeed = velocity.x * velocity.x + velocity.z * velocity.z;
            if (sqrSpeed < StrikeSpeed * StrikeSpeed)
            {
                return;
            }
            float speed = Mathf.Sqrt(sqrSpeed);
            Vector3 origin = _car.worldCenterOfMass;
            Quaternion toCar = Quaternion.Inverse(_car.rotation);
            float along = (toCar * velocity).z;
            float reach = speed * Time.fixedDeltaTime * 2f;
            float side = CarHalfWidth + PersonRadius;
            float front = along >= 0f ? CarHalfLength + PersonRadius + reach : 0f;
            float back = along >= 0f ? 0f : -CarHalfLength - PersonRadius - reach;
            for (int i = 0; i < _people.Count; i++)
            {
                CityPerson person = _people[i];
                if (!person.InUse || person.Lost)
                {
                    continue;
                }
                Vector3 local = toCar * (person.Position - origin);
                if (local.x > -side && local.x < side && local.z > back && local.z < front && local.y > -2f && local.y < 2f
                    && person.Character.visible && ragdolls.CanBegin(person.Character))
                {
                    person.Surrender(_fallen);
                    incidents.StrikeActor(person.Actor, velocity, speed, _car);
                    if (!person.Lost)
                    {
                        // The casualty system would not take them: they stand empty-handed where they are, and their site ends.
                        person.Stand();
                    }
                    CityLifeDirector.Logger?.Info($"City life: one of its people was hit by the player's car at {speed * 3.6f:F0} km/h"
                                                  + (person.Lost ? " and is in the casualty system's hands." : "."));
                    CityLifeDirector.Disturb(person);
                }
            }
        }
    }
}
