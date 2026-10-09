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
    /// The people of the living city: a pool of its own, apart from the six characters the
    /// accident and crime scenes share (People/ActorPool), which it never touches.
    ///
    /// THE BUDGET, enforced here:
    /// - Eight characters at most, ever (<see cref="MaxPeople"/>). Eight is what three sites of
    ///   the largest kind need (a road crew of three, a coffee cart of three, two friends
    ///   talking), and all but the jogger stand still, which is the cheap kind of character:
    ///   no path, no steering, one animation.
    /// - Building a character's look is the expensive part (the game merges its meshes), so the
    ///   eight are made one at a time, seconds apart, after the city has loaded
    ///   (<see cref="Warm"/>), kept, and handed out again in the same work clothes when possible.
    ///   A change of clothes happens only when a site is being set up, which is out of view.
    /// - People who are far away or behind the camera have their animation and rendering
    ///   switched off (the game's own switch): a few comparisons per person per frame, and only
    ///   while somebody is out. With nobody out this component is disabled.
    /// - While the player is driving, the people who are out are compared with the car's box
    ///   every other physics step, exactly as Ragdoll/CarStrikes does for the game's pedestrians
    ///   (it cannot see these people, and is not edited): whoever is hit drops their tool and is
    ///   handed to the mod's casualty system as one of its own.
    /// </summary>
    public sealed class CityPeople : MonoBehaviour
    {
        public const int MaxPeople = 8;

        private const string CharacterPrefab = "Characters/HumanDefinitionLow";
        private const float HideDistance = 150f;   // metres from the camera
        private const float HideBehind = 6f;       // metres behind the camera's own plane
        private const float StrikeSpeed = 1.4f;    // m/s: slower than this a car only nudges
        private const float PersonRadius = 0.3f;
        private const float CarHalfWidth = 0.95f;
        private const float CarHalfLength = 2.3f;
        private const float MaxBuild = 0.55f;      // of the game's 0 (slim) to 1 (heavy): the clips are made for slim to average

        private static readonly AppearanceTag[] CasualWear = { AppearanceTag.Casual };

        private readonly List<CityPerson> _people = new List<CityPerson>(MaxPeople);
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

        /// <summary>How many characters exist, in use or not.</summary>
        public int Count => _people.Count;

        /// <summary>How many of the characters that exist could be put on the street right now.</summary>
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

        /// <summary>Tools dropped by people who were knocked down. Whoever ends a site takes what lies near it.</summary>
        public List<Transform> Fallen => _fallen;

        /// <summary>
        /// Makes one more character if the pool is not full yet, dressed as a civilian and put
        /// away. Called every few seconds after the city loads, so the cost of eight looks is
        /// never paid in one frame. Returns true while there is more to make.
        /// </summary>
        public bool Warm(Vector3 near)
        {
            if (_people.Count >= MaxPeople)
            {
                return false;
            }
            var character = PrefabHelper.CreatePrefab<ThirdPersonCharacter>(CharacterPrefab, near, Quaternion.identity, transform);
            character.name = "RealLifeOverhaul Citizen";
            // Idle fidgets would play over the poses activities set.
            var fidgets = character.GetComponentInChildren<BoredAnimations>(true);
            if (fidgets != null)
            {
                fidgets.pauseBoredAnimations = true;
            }
            // These people go where their route takes them; physics must not shove them about.
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

        /// <summary>
        /// Puts a person on the pavement at a spot, facing a point, in the given clothes. Null if
        /// nobody is free or the spot will not take them; callers must cope with that.
        /// </summary>
        public CityPerson Take(CityLook look, Vector3 spot, Vector3 face)
        {
            return Take(look, spot, face, true);
        }

        /// <summary>
        /// The same; with <paramref name="fresh"/> false a civilian who has been out before keeps the
        /// look they had (no rebuilding of their clothes, which is the costly part): for people who
        /// come and go at a site every few seconds.
        /// </summary>
        public CityPerson Take(CityLook look, Vector3 spot, Vector3 face, bool fresh)
        {
            CityPerson person = Choose(look);
            if (person == null)
            {
                return null;
            }
            try
            {
                person.Character.gameObject.SetActive(true);
                person.Reset(_props);   // whoever had them last (the casualty system, perhaps) left them as they were
                if (person.Look != look || (person.Stale && fresh))
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
                return;   // knocked down: the casualty system has them and puts them away itself when it is done
            }
            person.Actor.InUse = false;
            person.Stale = person.Look == CityLook.Civilian;
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

        // Runs only while somebody is out: hides whoever cannot be seen, shows them again the frame they can.
        private void Update()
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
                // Hiding is only a saving: give it up rather than fault again next frame.
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

        private void FixedUpdate()
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

        // The player's car against the people who are out: positions, not collisions (see Ragdoll/CarStrikes).
        private void Strikes()
        {
            float now = Time.time;
            if (now >= _nextLookup)
            {
                _nextLookup = now + 0.5f;
                var game = InstanceBehavior<GameManager>.Instance;
                CarController car = game != null && PlayerHelper.IsUsingVehicle ? game.selectedVehicle as CarController : null;
                if (!ReferenceEquals(car, _carOf))   // looked up when the car changes, not twice a second
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
                        // The casualty system would not take them after all: they stand where they are, empty-handed, and their site ends.
                        person.Stand();
                    }
                    CityLifeDirector.Logger?.Info($"City life: one of its people was hit by the player's car at {speed * 3.6f:F0} km/h" + (person.Lost ? " and is in the casualty system's hands." : "."));
                    CityLifeDirector.Disturb(person);
                }
            }
        }

        // An idle character already dressed this way, else any idle one (it is re-dressed).
        private CityPerson Choose(CityLook look)
        {
            CityPerson spare = null;
            for (int i = _people.Count - 1; i >= 0; i--)
            {
                CityPerson person = _people[i];
                if (!person.Valid)
                {
                    _people.RemoveAt(i);   // destroyed with a scene we did not unload ourselves
                    continue;
                }
                if (person.InUse || person.Actor.InUse)
                {
                    continue;   // out, or still with the casualty system
                }
                if (person.Look == look && !person.Stale)
                {
                    return person;
                }
                if (spare == null || person.Look == look)
                {
                    spare = person;
                }
            }
            return spare;
        }

        // Who they are (age, build, face, hair) is rolled for everyone; workers then get their work clothes.
        private static void Dress(CityPerson person, CityLook look)
        {
            person.Look = look;
            person.Stale = false;
            AppearanceSetter appearance = person.Character.appearanceSetter;
            CityOutfits.Restore(person.Dyes);
            appearance.SetRandomAge();
            Gender gender = Random.value < 0.5f ? Gender.Male : Gender.Female;
            appearance.RandomizeElements(gender, SkinColorHelper.GetRandom(), CasualWear);
            // The game holds a heavy character's arms wider through an animator layer of its own, which a clip
            // of ours replaces: hands and tools made for an average build would sink into a big belly.
            appearance.data.fatness = Mathf.Min(appearance.data.fatness, MaxBuild);
            CityOutfits.Wear(appearance, look, person.Dyes);
            appearance.UpdateVisuals();   // the one mesh merge for this outfit
        }
    }
}
